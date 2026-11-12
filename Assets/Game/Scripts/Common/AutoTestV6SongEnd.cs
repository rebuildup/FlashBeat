using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using FlashBeat.Gameplay;
using FlashBeat.UI;
using FlashBeat.UI.V5;
using FlashBeat.UI.V6.Game.Loading;
using FlashBeat.UI.V6.Select;
using FlashBeat.UI.V6.Select.View;

/// <summary>
/// 「1 曲が最後まで始まり、判定され、曲終了が検出され、Result 画面まで到達する」
/// ことを **実機 (macOS / Windows プレイヤー)** で確認するハーネス
/// (Issue #233 の受入条件「結果へ進める」の Windows 実機なしで検証できる形)。
///
/// 使い方:
///   プレイヤー引数に "-autotest v6songend" と "-autotest-out &lt;dir&gt;" を渡す。
///   任意で "-autotest-song &lt;曲名&gt;" (既定: ツイッターランド = カタログ内で
///   一番短い曲) を渡すと、その曲を鳴らす。
///
/// 既存.AutoTestV6 (Issue #223) とは別のスイッチで動く独立ハーネス。既存ファイルは
/// 編集していない (他の作業と並行するため)。
///
/// 何を確認するか:
///   1. 選曲画面から実カタログの曲を選んで Ready → GameScene へ到達する。
///   2. メディアのパイプライン (yt-dlp でのダウンロード) が完了する。
///   3. Space で再生が始まり、メディア時計 (media time) が進む。
///   4. **譜面の全ノーツが判定される** (perfect+great+bad+miss = 譜面のノーツ数)。
///   5. スコアが動き、コンボが 1 以上になる。
///   6. 曲終了が検出され、**期限切れ強制完了ではない** (ForcedByDeadline = false)。
///   7. ResultScene へ遷移し、Result 画面が **v6 の Result Canvas の中で**
///      曲名と最終スコアを文字として表示していることを確かめる。
///
/// 手段の選択 (なぜこの設計にしたか) は docs/evidence/233-song-end-harness.md を参照。
/// ここでは **実挙動を一切書き換えない**: クロック注入も譜面の差し替えも行わず、
/// 本物の曲 (カタログ内で最短の ツイッターランド) を最後まで再生して、本物の
/// 終了判定と本物の遷移を確かめる。
/// </summary>
public class AutoTestV6SongEnd : MonoBehaviour
{
    const string Tag = "[AutoTestV6SongEnd]";

    /// <summary>既定で再生する曲 (カタログ内で duration が最も短い = 44 秒)。</summary>
    public const string DefaultSongTitle = "ツイッターランド";

    /// <summary>メディアの準備 (ダウンロードを含む) を待つ上限。</summary>
    const float MediaReadyTimeout = 300f;

    /// <summary>再生開始から曲終了を待つ上限 (実時間)。</summary>
    const float SongEndTimeout = 240f;

    /// <summary>選曲画面が立つまでの待ち時間。</summary>
    const float SelectSceneWait = 6f;

    /// <summary>
    /// ノーツを叩く注入の窓 (秒)。 judge は次のフレームで Keys を見る。
    /// ポーリングは毎フレーム行うので、0.05 秒の窓でも取りこぼさない。
    /// </summary>
    const float HitWindowSeconds = 0.05f;

    /// <summary>
    /// ローディング画面が一度も見えないときに、表示を待つ最小の観測時間 (秒)。
    /// これだけ待ってメディアの準備も終わっていれば、キャッシュ済みで表示が
    /// 無かったと判断して打ち切る。時間だけで切ってはいけない: 冷たいメディア
    /// では resolve とダウンロードに 10 秒以上かかり、その後に表示が出る。
    /// </summary>
    const float NotVisibleMinWaitSeconds = 5f;

    /// <summary>v6 の Result Canvas の GameObject 名。</summary>
    const string ResultCanvasName = "V6 Result Canvas";

    int pass;
    int fail;
    int shot;
    string outDir;
    string songTitle;

    /// <summary>既にキー入力を送ったノーツ (同じノーツを 2 回叩かない)。</summary>
    readonly HashSet<int> hitSent = new HashSet<int>();

    /// <summary>このフレームに注入済みのレーン (1 レーン 1 打)。</summary>
    readonly bool[] laneInjectedThisFrame = new bool[8];

    /// <summary>
    /// 最後に見た曲終了の状態 (Result へ遷移すると MusicManager が消えるため保持)。
    ///
    /// 注意: このハーネスは **GManager.nextNotes を読みません**。nextNotes は
    /// 旧 API の「1 + 確定数」互換値で、「確定数そのもの」ではない
    /// (Issue #182 / PR #256)。確定数は正本の `GManager.settledNoteCount`
    /// (および判定の内訳 perfect / great / bad / miss) を読む。
    /// 「曲終了が起きた」ことは状態機械そのもの
    /// (`MusicManager.SongEndState` = `SongEndMachine.State`) で観測するので、
    /// nextNotes の定義に依存しない。
    /// </summary>
    SongEndState lastSongEndState = SongEndState.Playing;

    /// <summary>
    /// 曲終了が期限切れ強制完了だったか。
    /// PR の主張「自然終了, 期限切れ強制完了ではない」を **実行時に確かめる** ための
    /// 値。Completed を見たフレームで MusicManager から控えておく (Result へ
    /// 遷移すると MusicManager が消えるため)。
    /// </summary>
    bool songEndForcedByDeadline;

    /// <summary>Completed を見たか (SongEndForcedByDeadline が意味を持つか)。</summary>
    bool songEndCompleted;

    /// <summary>譜面 (Resources) から数えた全ノーツ数 (判定されたはずの総数)。</summary>
    int chartNoteCount;

    /// <summary>ローディング画面を実際に観測したか (#378)。</summary>
    bool loadingObserved;

    /// <summary>ローディング画面が見えていた秒数 (#378 / #377 の待ち時間)。</summary>
    float loadingVisibleSeconds;

    /// <summary>NotesManager が公開している総ノーツ数 (実行時)。</summary>
    int runtimeNoteCount;

    /// <summary>再生中に観測した combo の最大値 (GManager.combo のピーク)。</summary>
    int comboPeak;

    int hitsSent;

    /// <summary>ランキング登録方式 (Online / Local)。結果ファイルに記録する。</summary>
    string rankingMode = "unknown";

    /// <summary>判定数の合計が譜面のノーツ数と一致したか (ランキング受理条件)。</summary>
    bool judgedMatchesChart;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (GetArg("-autotest") != "v6songend") { return; }
        var go = new GameObject("AutoTestV6SongEnd");
        DontDestroyOnLoad(go);
        go.AddComponent<AutoTestV6SongEnd>();
    }

    /// <summary>
    /// 入口。本体のコルーチン (RunBody) を手で進めて、途中で例外が投げられても
    /// 結果ファイル (fail=1) が書けるようにする (Issue: 例外で Finish() に
    /// 辿り着かず、結果ファイルもログも残らない = 失敗が「無言」で終わる)。
    /// </summary>
    IEnumerator Start()
    {
        outDir = GetArg("-autotest-out");
        if (string.IsNullOrEmpty(outDir)) { outDir = Application.persistentDataPath; }
        songTitle = GetArg("-autotest-song");
        if (string.IsNullOrEmpty(songTitle)) { songTitle = DefaultSongTitle; }

        try { Directory.CreateDirectory(outDir); }
        catch (Exception) { }

        Debug.Log(Tag + " start: song='" + songTitle + "' outDir='" + outDir + "'");

        IEnumerator body = RunBody();
        while (true)
        {
            object current;
            try
            {
                if (!body.MoveNext()) { break; }
                current = body.Current;
            }
            catch (Exception e)
            {
                Debug.LogError(Tag + " EXCEPTION " + e);
                WriteFailureResult(e);
                yield break;
            }
            yield return current;
        }
    }

    /// <summary>ハーネスの本体。</summary>
    IEnumerator RunBody()
    {
        // 1. 選曲画面 (v6 の 3 カラム) が立ち上がる。
        SceneManager.LoadScene("SelectScene");
        yield return new WaitForSecondsRealtime(SelectSceneWait);
        Check("the select scene is loaded", SceneManager.GetActiveScene().name == "SelectScene");

        V6SelectView view = V6SelectView.Current;
        Check("the v6 select view exists", view != null);
        if (view == null)
        {
            Finish();
            yield break;
        }

        IV6SelectDataSource data = view.Data;
        Check("the data source is present", data != null);
        if (data == null)
        {
            Finish();
            yield break;
        }

        // 2. 実カタログから、目的の曲 (既定: 一番短い曲) を選ぶ。
        IReadOnlyList<V6SongEntry> songs = data.AllSongs;
        Check("the catalog has more than one song", songs != null && songs.Count > 1);

        V6SongEntry target = FindSong(songs, songTitle);
        Check("the song '" + songTitle + "' is in the catalog", target != null);

        if (target == null)
        {
            Finish();
            yield break;
        }

        data.Select(target.SongId);
        yield return new WaitForSecondsRealtime(0.4f);
        Check("the selection follows the picked song", data.Selected != null && data.Selected.SongId == target.SongId);

        // 譜面の総ノーツ数を先に控えておく (判定数が「全ノーツ数」と一致する
        // ことを確かめるため)。譜面が無い場合は 0 のまま (下の判定で落ちる)。
        chartNoteCount = ChartNoteCount(songTitle);
        Debug.Log(Tag + " chart: title='" + songTitle + "' notes=" + chartNoteCount);

        yield return new WaitForEndOfFrame();
        string selectShot = CaptureScreenshot("01-select");
        yield return new WaitForSecondsRealtime(0.8f);
        Check("the screenshot 01-select was written", File.Exists(selectShot));

        // 3. Ready → ゲーム画面 (実セッションとして遷移する)。
        int expectedSongId = data.Selected == null ? -1 : data.Selected.SongId;

        // 前セッションの判定数 (perfect / great / bad / miss / score / combo) が
        // 残っていると「全ノーツが判定された」の確認が結果的に常に通ってしまう
        // ので、**遷移の前**に明示的にリセットする。
        //
        // この位置が安全な理由:
        //   - ResetSession() は GManager.maxScore を 0 にするが、譜面の
        //     maxScore は GameScene の NotesManager.LoadRoutine が
        //     「ノーツ数 x 5」で設定し直すので (この後)、判定は壊れない。
        //   - ResetSession() は BeginPlaySession() を含む。曲の identity を
        //     先に GManager.songID へ入れておくので、ここで固定されるのも
        //     目的曲そのもの (CommitReady と同じ動き)。
        //   - 遷移前なので、GameScene が読み込まれた後にカウンタを消して
        //     スコア計算を壊すこともない。
        int staleJudged = GManager.perfect + GManager.great + GManager.bad + GManager.miss;
        Debug.Log(Tag + " resetting the session before the play (stale judged=" + staleJudged +
                  " stale score=" + GManager.score + ")");
        GManager.songID = target.SongId;
        GManager.ResetSession();

        view.TriggerReady();

        float waited = 0f;
        while (waited < 20f)
        {
            if (SceneManager.GetActiveScene().name == "GameScene") { break; }
            waited += 0.5f;
            yield return new WaitForSecondsRealtime(0.5f);
        }
        Check("ready starts the game scene", SceneManager.GetActiveScene().name == "GameScene");
        Check("the started song is the picked one", GManager.songID == expectedSongId);

        // 3b. ローディング画面 (#378): メディアが未キャッシュなら、準備の間
        //     実際に出ているかを毎フレーム見て、見えている間を撮る。
        yield return ObserveLoading();

        yield return new WaitForSecondsRealtime(3f);
        yield return new WaitForEndOfFrame();
        string gameShot = CaptureScreenshot("02-game");
        yield return new WaitForSecondsRealtime(0.8f);
        Check("the screenshot 02-game was written", File.Exists(gameShot));

        // 4. メディアの準備 (実機の yt-dlp ダウンロード) を待つ。
        MusicManager music = FindFirstObjectByType<MusicManager>();
        Check("the music manager exists", music != null);
        if (music == null)
        {
            Finish();
            yield break;
        }

        float mediaWaited = 0f;
        while (mediaWaited < MediaReadyTimeout)
        {
            if (music.MediaReady) { break; }
            mediaWaited += 1f;
            yield return new WaitForSecondsRealtime(1f);
        }
        Check("the media pipeline finished", music.MediaReady);
        Debug.Log(Tag + " media: ready=" + music.MediaReady + " audioMode=" + music.AudioModeName +
                  " videoPrepared=" + music.VideoPrepared + " failure=" + MusicManager.LastFailure);

        // 5. Space で再生を開始する。
        KeyBindings.InjectKeyDown(KeyCode.Space);
        yield return new WaitForSecondsRealtime(1.5f);
        Check("the playback started", music.PlaybackStarted);
        yield return new WaitForEndOfFrame();
        string playShot = CaptureScreenshot("03-gameplay");
        yield return new WaitForSecondsRealtime(0.8f);
        Check("the screenshot 03-gameplay was written", File.Exists(playShot));

        // 6. 再生 → 判定 → 曲終了 → Result まで待つ。
        //    ポーリングは **毎フレーム** (0.25 秒ごとだと 0.05 秒の注入窓を
        //    跨ぐノーツを取りこぼす = Scores が欠ける)。
        NotesManager notes = FindFirstObjectByType<NotesManager>();
        bool mediaClockAdvanced = false;
        float lastMediaTime = -1f;
        float loopStart = Time.realtimeSinceStartup;

        while (Time.realtimeSinceStartup - loopStart < SongEndTimeout)
        {
            string scene = SceneManager.GetActiveScene().name;

            if (music != null)
            {
                lastSongEndState = music.SongEndState;
                float mediaTime = music.MediaTimeNow;
                if (lastMediaTime >= 0f && mediaTime > lastMediaTime + 0.01f) { mediaClockAdvanced = true; }
                if (lastMediaTime < 0f || mediaTime > lastMediaTime) { lastMediaTime = mediaTime; }

                if (notes != null && notes.noteNum > 0) { runtimeNoteCount = notes.noteNum; }

                AutoHit(notes);

                if (GManager.combo > comboPeak) { comboPeak = GManager.combo; }

                if (lastSongEndState == SongEndState.Completed)
                {
                    songEndCompleted = true;
                    // 主張 (自然終了) の根拠。期限切れ強制完了なら true になる。
                    songEndForcedByDeadline = music.SongEndForcedByDeadline;
                }
            }

            if (scene == "ResultScene") { break; }
            if (lastSongEndState == SongEndState.Failed) { break; }

            yield return null;
        }

        // 確定数 (settled) = 判定の内訳の総数。曲終了判定そのもの
        // (MusicManager) が読む `GManager.settledNoteCount` と一致するはず。
        int judged = GManager.perfect + GManager.great + GManager.bad + GManager.miss;
        int settled = GManager.settledNoteCount;

        Debug.Log(Tag + " playback summary: songEndState=" + lastSongEndState +
                  " mediaTime=" + lastMediaTime.ToString("F2") +
                  " hitsSent=" + hitsSent +
                  " perfect=" + GManager.perfect + " great=" + GManager.great +
                  " bad=" + GManager.bad + " miss=" + GManager.miss +
                  " score=" + GManager.score + " combo=" + GManager.combo +
                  " maxCombo=" + GManager.maxCombo +
                  " comboPeak=" + comboPeak +
                  " judged=" + judged + " settled=" + settled +
                  " chartNotes=" + chartNoteCount +
                  " runtimeNotes=" + runtimeNoteCount +
                  " forcedByDeadline=" + songEndForcedByDeadline);

        Check("the media clock advances", mediaClockAdvanced);
        Check("the chart note count is known", chartNoteCount > 0);
        Check("the runtime note count matches the chart (runtimeNotes == chartNotes)", runtimeNoteCount == chartNoteCount);
        Check("every chart note is settled (settled == chart notes)", settled == chartNoteCount);
        Check("the score is greater than zero", GManager.score > 0);
        Check("the combo rises above zero during the playback (GManager.combo peaked above zero)", comboPeak > 0);
        Check("the peak combo is recorded (GManager.maxCombo > 0)", GManager.maxCombo > 0);
        Check("the song end state machine completes", songEndCompleted);
        Check("the song end is not a forced deadline completion", songEndCompleted && !songEndForcedByDeadline);
        Check("the result scene is loaded", SceneManager.GetActiveScene().name == "ResultScene");

        // 7. Result 画面が実際に描画されているか (v6 の Result Canvas の中で)。
        if (SceneManager.GetActiveScene().name == "ResultScene")
        {
            yield return new WaitForSecondsRealtime(2f);
            ResultScreenInfo info = InspectResultScreen();
            Debug.Log(Tag + " result screen: " + info.Detail);
            Check("the v6 result canvas exists", info.CanvasFound);
            Check("the v6 result canvas has non-empty text", info.TextFound);
            Check("the v6 result canvas shows the song title", info.TitleFound);
            Check("the v6 result canvas shows the final score", info.ScoreFound);

            yield return new WaitForEndOfFrame();
            string resultShot = CaptureScreenshot("04-result");
            yield return new WaitForSecondsRealtime(0.8f);
            Check("the screenshot 04-result was written", File.Exists(resultShot));
        }

        // 8. ランキング送信の前提 (ネットワーク不要の検証)。
        //
        //    ランキング API (POST /api/v1/scores) は、送信された判定数の合計が
        //    譜面のノーツ数と一致する時だけ受理する
        //    (contracts/openapi.v1.yaml「Server validates judgment sum against
        //    published version」/ 422 = "Invalid counts or chart version"。
        //    実装は web/routes/api/v1/_score-model.ts の
        //    scoreSemanticsDiagnostics)。
        //
        //    ここでは **ネットワークを使わずに** その受理条件が満たされているか
        //    を確かめる:
        //      - 判定数の合計 (perfect + great + bad + miss) == 譜面のノーツ数
        //      - この曲のランキング登録方式 (オンライン送信 / 端末内保存) が決まる
        //
        //    API への実送信 (201 / 422) はネットワークと認証が要るので、
        //    ここでは行わない。サーバ側の規則そのものは
        //    web/tests/score-model.test.ts が担保している。
        Check("the judgement total equals the chart note count (the ranking acceptance condition)",
            judged == chartNoteCount);

        SongData playedSong = GManager.GetSong(GManager.songID);
        FlashBeat.UI.V6.Result.V6RegistrationMode registrationMode = FlashBeat.UI.V6.Result.V6ResultLogic.RegistrationMode(
            playedSong != null && playedSong.isRemote,
            playedSong != null ? playedSong.remoteId : null,
            playedSong != null ? playedSong.chartVersion : 0);
        Check("the ranking registration mode is resolved",
            registrationMode == FlashBeat.UI.V6.Result.V6RegistrationMode.Local ||
            registrationMode == FlashBeat.UI.V6.Result.V6RegistrationMode.Online);
        rankingMode = registrationMode.ToString();
        judgedMatchesChart = judged == chartNoteCount;
        Debug.Log(Tag + " ranking: judged=" + judged + " chartNotes=" + chartNoteCount +
                  " registrationMode=" + registrationMode);

        Finish();
    }

    /// <summary>目当ての曲を探す (タイトル一致)。</summary>
    static V6SongEntry FindSong(IReadOnlyList<V6SongEntry> songs, string title)
    {
        if (songs == null) { return null; }
        for (int i = 0; i < songs.Count; i++)
        {
            V6SongEntry entry = songs[i];
            if (entry != null && entry.Title == title) { return entry; }
        }
        return null;
    }

    /// <summary>
    /// 譜面 (Resources) の総ノーツ数。譜面が無い / 読めないときは 0。
    /// 「判定された数」と比べる基準 (Issue: 判定が 5/130 でも通っていた)。
    /// </summary>
    static int ChartNoteCount(string title)
    {
        try
        {
            TextAsset asset = Resources.Load<TextAsset>(title);
            if (asset == null) { return 0; }
            Data chart = JsonUtility.FromJson<Data>(asset.text);
            if (chart == null || chart.notes == null) { return 0; }
            return chart.notes.Length;
        }
        catch (Exception e)
        {
            Debug.LogWarning(Tag + " could not read the chart '" + title + "': " + e.Message);
            return 0;
        }
    }

    /// <summary>
    /// ノーツの判定時刻に合わせてレーンのキーを注入する (命中の証拠)。
    /// 判定は Judge が GManager.step_time (メディア時刻) で見るので、同じ時計で
    /// 叩く。judge 側は入力を 1 フレーム遅れて読むため、わずかに早めに打って、
    /// 丁度 judge の Perfect 窓 (0.1 秒) に入るようにする。
    /// </summary>
    void AutoHit(NotesManager notes)
    {
        if (notes == null || !notes.IsReady) { return; }
        if (notes.NotesTime == null || notes.LaneNum == null) { return; }

        float mediaTime = GManager.step_time;
        int count = Mathf.Min(notes.NotesTime.Count, notes.LaneNum.Count);

        // 同じレーンの 2 ノーツを同じフレームで注入すると、注入記録は
        // KeyCode 単位 (KeyBindings.InjectKeyDown) なので後勝ちになり、
        // 前のノーツの注入が見えなくなる。よって **1 フレームにつき 1 レーン
        // 1 打** だけにする (残りは次のフレームに回す)。
        for (int lane = 0; lane < laneInjectedThisFrame.Length; lane++)
        {
            laneInjectedThisFrame[lane] = false;
        }

        for (int i = 0; i < count; i++)
        {
            if (hitSent.Contains(i)) { continue; }
            float time = notes.NotesTime[i];
            if (time > 900f) { hitSent.Add(i); continue; } // 番兵 (ダミー行)

            int lane = notes.LaneNum[i];
            if (lane < 0 || lane > 7) { hitSent.Add(i); continue; }
            if (laneInjectedThisFrame[lane]) { continue; }

            float delta = time - mediaTime;
            if (delta > HitWindowSeconds) { continue; }      // まだ早い
            if (delta < -0.25f) { hitSent.Add(i); continue; }  // 窗口を過ぎた (Miss 相当)

            KeyCode[] keys = KeyBindings.GetKeys(lane);
            if (keys == null || keys.Length == 0) { hitSent.Add(i); continue; }
            KeyBindings.InjectKeyDown(keys[0]);
            hitSent.Add(i);
            laneInjectedThisFrame[lane] = true;
            hitsSent++;
        }
    }

    /// <summary>Result 画面の観測結果。</summary>
    struct ResultScreenInfo
    {
        /// <summary>v6 の Result Canvas があるか (active)。</summary>
        public bool CanvasFound;
        /// <summary>その Canvas の下で空でない文字があるか。</summary>
        public bool TextFound;
        /// <summary>その Canvas の下に曲名が出ているか。</summary>
        public bool TitleFound;
        /// <summary>その Canvas の下に最終スコアが出ているか。</summary>
        public bool ScoreFound;
        /// <summary>デバッグ用の説明。</summary>
        public string Detail;
    }

    /// <summary>
    /// Result 画面が描画されているか: v6 の Result Canvas が生きていて、
    /// **その Canvas の下** に空でない文字があり、曲名と最終スコアが
    /// 表示されていること。「どこかの Canvas に何か文字がある」だけでは
    /// Result 画面以外の画面の文字でも通ってしまうため、必ず Result Canvas の
    /// 子孫 (IsChildOf) で判定する。
    /// </summary>
    static ResultScreenInfo InspectResultScreen()
    {
        ResultScreenInfo info = new ResultScreenInfo();

        Canvas resultCanvas = null;
        var canvases = FindObjectsOfType<Canvas>(true);
        for (int i = 0; i < canvases.Length; i++)
        {
            Canvas canvas = canvases[i];
            if (canvas == null) { continue; }
            if (canvas.gameObject.name != ResultCanvasName) { continue; }
            if (!canvas.isActiveAndEnabled) { continue; }
            resultCanvas = canvas;
            break;
        }
        info.CanvasFound = resultCanvas != null;
        if (resultCanvas == null)
        {
            info.Detail = "v6ResultCanvas=False scene=" + SceneManager.GetActiveScene().name;
            return info;
        }

        string title = SongTitleForResult();
        string score = FormatScoreForResult();

        int texts = 0;
        int underCanvas = 0;
        var tmp = FindObjectsOfType<TextMeshProUGUI>(true);
        for (int i = 0; i < tmp.Length; i++)
        {
            TextMeshProUGUI text = tmp[i];
            if (text == null) { continue; }
            texts++;
            if (!text.isActiveAndEnabled) { continue; }
            if (!text.transform.IsChildOf(resultCanvas.transform)) { continue; }
            underCanvas++;
            if (string.IsNullOrEmpty(text.text)) { continue; }

            info.TextFound = true;
            if (!string.IsNullOrEmpty(title) && text.text.Contains(title)) { info.TitleFound = true; }
            if (!string.IsNullOrEmpty(score) && text.text.Contains(score)) { info.ScoreFound = true; }
        }

        info.Detail = "v6ResultCanvas=True nonEmptyTextUnderCanvas=" + info.TextFound +
                      " titleFound=" + info.TitleFound + " scoreFound=" + info.ScoreFound +
                      " textsUnderCanvas=" + underCanvas + " texts=" + texts +
                      " scene=" + SceneManager.GetActiveScene().name;
        return info;
    }

    /// <summary>Result 画面に表示される曲名 (GManager の実値)。</summary>
    static string SongTitleForResult()
    {
        SongData song = GManager.GetSong(GManager.songID);
        if (song == null) { return ""; }
        return song.title ?? "";
    }

    /// <summary>
    /// Result 画面に表示されるスコアの文字列 (V6ResultLogic.FormatScore と同じ
    /// 書式: 桁区切り付き)。
    /// </summary>
    static string FormatScoreForResult()
    {
        if (GManager.score <= 0) { return ""; }
        return GManager.score.ToString("N0", CultureInfo.InvariantCulture);
    }

    /// <summary>スクリーンショットのファイル名を返す (撮影自体は同期)。</summary>
    string CaptureScreenshot(string name)
    {
        shot++;
        string path = Path.Combine(outDir, string.Format("autotest-v6songend-{0:00}-{1}.png", shot, name));
        ScreenCapture.CaptureScreenshot(path);
        return path;
    }

    /// <summary>
    /// ローディング画面 (#378) の観測。GameScene に入った直後から、v6 の
    /// ローディングが実際に表示されているかを毎フレーム見る。表示中は 0.8 秒
    /// おきに最大 3 枚撮る。メディアがキャッシュ済みで準備が一瞬で終わる場合は
    /// 見えないので、その場合は「観測できなかった」とログに残す (表示の
    /// 有無を PASS に読み替えない)。
    /// </summary>
    IEnumerator ObserveLoading()
    {
        // 経過は時計で測る。このループは撮影のために最大 1.2 秒待つので、
        // フレームの差分を積むと実時間より短く記録され、表示時間も猶予も狂う。
        float startedAt = Time.unscaledTime;
        float elapsed = 0f;
        float visibleSince = -1f;
        float nextShotAt = 0f;
        float nextProbeAt = 0f;
        int loadingShots = 0;
        float lastProgress = -1f;
        bool firstSeenLogged = false;
        // 準備完了後も Preparing に戻ることは無いので、一度見えた後の
        // 非表示を終了条件にする。一度も見えないなら、表示を駆動している
        // 段階 (GameHudController.LastPhase) が準備中でなくなった時点で
        // 打ち切る。時間だけで切ってはいけない: 冷たいメディアは resolve と
        // ダウンロードに 10 秒以上かかり、表示はその後に出る。
        while (true)
        {
            elapsed = Time.unscaledTime - startedAt;
            if (elapsed >= 60f) { break; }

            if (!loadingObserved && elapsed >= NotVisibleMinWaitSeconds)
            {
                HudPlayPhase phase = GameHudController.LastPhase;
                bool preparing = phase == HudPlayPhase.Preparing || phase == HudPlayPhase.Failed;
                if (!preparing) { break; }
            }
            V6LoadingView view = V6LoadingView.Current;
            bool visible = view != null && view.Root != null && view.Root.gameObject.activeInHierarchy;
            if (visible)
            {
                if (!firstSeenLogged)
                {
                    firstSeenLogged = true;
                    Debug.Log(Tag + " loading visible after " + elapsed.ToString("F2") + "s");
                }

                loadingObserved = true;
                if (visibleSince < 0f) { visibleSince = elapsed; }
                if (GameLoadingProgress.HasNotesProgress) { lastProgress = GameLoadingProgress.NotesProgress; }

                if (elapsed >= nextProbeAt)
                {
                    nextProbeAt = elapsed + 1f;
                    Debug.Log(Tag + " loading probe t=" + elapsed.ToString("F1")
                        + " clip=" + view.VideoClipForAudit
                        + " prepared=" + view.VideoPreparedForAudit
                        + " videoVisible=" + view.VideoVisibleForAudit
                        + " surface=" + view.VideoSurfaceForAudit);
                }

                if (loadingShots < 3 && elapsed >= nextShotAt)
                {
                    yield return new WaitForEndOfFrame();
                    loadingShots++;
                    nextShotAt = elapsed + 0.8f;
                    string path = CaptureScreenshot("00-loading-" + loadingShots);
                    yield return new WaitForSecondsRealtime(0.4f);
                    Check("the loading screenshot 00-loading-" + loadingShots + " was written", File.Exists(path));
                }
            }
            else if (loadingObserved)
            {
                break;
            }

            yield return null;
        }

        if (visibleSince >= 0f)
        {
            loadingVisibleSeconds = Mathf.Max(elapsed - visibleSince, 0f);
        }

        Debug.Log(Tag + " loading: observed=" + loadingObserved
            + " seconds=" + loadingVisibleSeconds.ToString("F2")
            + " chartProgress=" + lastProgress.ToString("F2"));
    }

    private void Finish()
    {
        Debug.Log(Tag + " RESULT pass=" + pass + " fail=" + fail);
        try
        {
            File.WriteAllText(Path.Combine(outDir, "autotest-v6songend-result.txt"),
                Tag + " RESULT pass=" + pass + " fail=" + fail + "\n" +
                "song=" + songTitle + "\n" +
                "songEndState=" + lastSongEndState + "\n" +
                "forcedByDeadline=" + songEndForcedByDeadline + "\n" +
                "scene=" + SceneManager.GetActiveScene().name + "\n" +
                "perfect=" + GManager.perfect + " great=" + GManager.great +
                " bad=" + GManager.bad + " miss=" + GManager.miss + "\n" +
                "judged=" + (GManager.perfect + GManager.great + GManager.bad + GManager.miss) + "\n" +
                "settled=" + GManager.settledNoteCount + "\n" +
                "chartNotes=" + chartNoteCount + " runtimeNotes=" + runtimeNoteCount + "\n" +
                "score=" + GManager.score + " combo=" + GManager.combo +
                " maxCombo=" + GManager.maxCombo + " comboPeak=" + comboPeak + "\n" +
                "rankingMode=" + rankingMode + "\n" +
                "loadingObserved=" + loadingObserved + "\n" +
                "loadingVisibleSeconds=" + loadingVisibleSeconds.ToString("F2", CultureInfo.InvariantCulture) + "\n" +
                "judgedMatchesChart=" + judgedMatchesChart + "\n");
        }
        catch (Exception e) { Debug.LogWarning(Tag + " could not write the result file: " + e.Message); }

        Application.Quit();
    }

    /// <summary>
    /// 例外で中断されたときに呼ぶ。fail=1 の結果ファイルを残し、異常が
    /// 「無言」で終わらないようにする。
    /// </summary>
    void WriteFailureResult(Exception e)
    {
        fail++;
        Debug.LogError(Tag + " FAIL the harness threw an exception: " + e);
        try
        {
            File.WriteAllText(Path.Combine(outDir, "autotest-v6songend-result.txt"),
                Tag + " RESULT pass=" + pass + " fail=" + fail + "\n" +
                "song=" + songTitle + "\n" +
                "exception=" + e.GetType().Name + ": " + e.Message + "\n" +
                "scene=" + SceneManager.GetActiveScene().name + "\n");
        }
        catch (Exception writeError)
        {
            Debug.LogWarning(Tag + " could not write the failure result file: " + writeError.Message);
        }

        Application.Quit();
    }

    private void Check(string label, bool ok)
    {
        if (ok) { pass++; Debug.Log(Tag + " PASS " + label); }
        else { fail++; Debug.Log(Tag + " FAIL " + label); }
    }

    private static string GetArg(string key)
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == key) { return args[i + 1]; }
        }
        return null;
    }
}
