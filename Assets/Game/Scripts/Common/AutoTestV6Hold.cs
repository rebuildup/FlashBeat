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
using FlashBeat.UI.V6.Select;
using FlashBeat.UI.V6.Select.View;

/// <summary>
/// 「HOLD (ロングノーツ) 入り譜面が実機 (macOS / Windows プレイヤー) で
/// 判定され、HOLD の 3 段階 (開始 / 保持 / 終端) が観測できる」ことを確かめる
/// ハーネス (Issue #278 / Issue #59 の残り受入条件)。
///
/// 使い方:
///   プレイヤー引数に "-autotest v6hold" と "-autotest-out &lt;dir&gt;" を渡す。
///   任意で "-autotest-song &lt;曲名&gt;" (既定: ツイッターランド) を渡すと、
///   その曲を鳴らす。
///
/// 谱面は **差し替え** で持ち込む (カタログは増やさない):
///   - PlaySessionSnapshot.ChartJsonOverride に HoldProbeChart.Json (HOLD 3 +
///     タップ 4) を設定してから Ready を押す。
///   - NotesManager.ParseChart は固定済み譜面 JSON を CloudCatalog.ParseChartResponse
///     に通すので、封筒つきの JSON を渡している。
///   - GManager の 7 並列配列にも Assets/Game/Resources/ にも何も追加しない。
///
/// 確認する内容:
///   a. GameScene に入り、譜面が HOLD 入りで読み込まれている (ノーツ数 = 7、
///      HOLD = 3)。
///   b. HOLD のノーツ実体が存在し、胴体 (holdSeconds) が伸びている。
///   c. HOLD の先頭が判定され、終端フェーズ (保持の終了) まで到達している。
///   d. スコア / コンボがタップだけの譜面 (−) 不一样的値になる
///      (maxScore = 7 x 5 = 35, スコア > 0, コンボ > 0)。
///   e. HOLD の胴体が描画されている (レンダラーが見えている / 長さが伸びている)。
///      保持中のスクリーンショットも撮る。
///   f. 曲終了 → ResultScene まで到達する。
///
/// HOLD の「保持」フェーズは判定が **Input.GetKey (実キーの押下状態)** を
/// 読むため、キー注入 (KeyBindings.InjectKeyDown) だけでは保持できない。
/// 実機では「キーを押した状態」を作，考试の 3 段階を観測する。
/// macOS ではハーネス (シェル) 側の CGEvent が、そのキーを実際に押す
/// (スクリプト script/mac-holdprobe-harness.sh)。
/// </summary>
public class AutoTestV6Hold : MonoBehaviour
{
    const string Tag = "[AutoTestV6Hold]";

    /// <summary>既定で再生する曲 (カタログ内で duration が最も短い = 44 秒)。</summary>
    public const string DefaultSongTitle = "ツイッターランド";

    /// <summary>メディアの準備 (ダウンロードを含む) を待つ上限。</summary>
    const float MediaReadyTimeout = 300f;

    /// <summary>再生開始から曲終了を待つ上限 (実時間)。</summary>
    const float SongEndTimeout = 300f;

    /// <summary>選曲画面が立つまでの待ち時間。</summary>
    const float SelectSceneWait = 6f;

    /// <summary>ノーツを叩く注入の窓 (秒)。</summary>
    const float HitWindowSeconds = 0.05f;

    /// <summary>v6 の Result Canvas の GameObject 名。</summary>
    const string ResultCanvasName = "V6 Result Canvas";

    /// <summary>OS レベルのキー押下スケジュール (ハーネス → シェル) のファイル名。</summary>
    public const string ScheduleFileName = "hold-schedule.txt";

    /// <summary>結果ファイル名。</summary>
    public const string ResultFileName = "autotest-v6hold-result.txt";

    /// <summary>保持フェーズを観測する区間 (先頭からの経過秒)。</summary>
    const float HoldObserveFrom = 0.3f;

    /// <summary>保持フェーズを観測する区間 (終端までの残り秒)。</summary>
    const float HoldObserveTo = 0.4f;

    int pass;
    int fail;
    int shot;
    string outDir;
    string songTitle;

    /// <summary>既にキー入力を送ったノーツ (同じノーツを 2 回叩かない)。</summary>
    readonly HashSet<int> hitSent = new HashSet<int>();

    /// <summary>このフレームに注入済みのレーン (1 レーン 1 打)。</summary>
    readonly bool[] laneInjectedThisFrame = new bool[8];

    // --- ログ観測 (Judge の "Hold clear" / "Hold broken" / "Miss") ------------

    /// <summary>HOLD が終端まで到達した (成功) 回数。</summary>
    int holdClearCount;

    /// <summary>HOLD が途中で切れた (失敗) 回数。</summary>
    int holdBrokenCount;

    /// <summary>Miss のログ回数。</summary>
    int missLogCount;

    /// <summary>HOLD の終端ログ (観測用)。</summary>
    readonly List<string> holdEndEvents = new List<string>();

    // --- 譜面 / ノーツの観測値 ----------------------------------------------

    /// <summary>差し込んだ譜面のノーツ数 (HOLD 込み)。</summary>
    int probeNoteCount;

    /// <summary>差し込んだ譜面の HOLD 数。</summary>
    int probeHoldCount;

    /// <summary>差し込んだ譜面のタップ数。</summary>
    int probeTapCount;

    /// <summary>NotesManager が公開している総ノーツ数 (実行時)。</summary>
    int runtimeNoteCount;

    /// <summary>HOLD のノーツ実体 (holdSeconds > 0) の個数。</summary>
    int holdInstanceCount;

    /// <summary>HOLD の胴体の長さ (秒) の最大値。</summary>
    float maxHoldSeconds;

    /// <summary>胴体が描画されていることを観測した HOLD の個数。</summary>
    int holdBodiesVisible;

    /// <summary>終端まで到達した HOLD の個数。</summary>
    int holdEndedCount;

    /// <summary>保持フェーズを観測できた HOLD の個数。</summary>
    int holdObservedCount;

    /// <summary>再生中に観測した combo の最大値。</summary>
    int comboPeak;

    int hitsSent;

    /// <summary>OS キー押下スケジュールを書き出したか。</summary>
    bool scheduleWritten;

    /// <summary>保持中のスクリーンショットを撮ったか。</summary>
    bool holdShotTaken;

    /// <summary>保持フェーズを観測できたノーツ (index)。</summary>
    readonly HashSet<int> holdObserved = new HashSet<int>();

    /// <summary>終端に到達したノーツ (index)。</summary>
    readonly HashSet<int> holdEnded = new HashSet<int>();

    /// <summary>先頭判定の瞬間に見た HOLD 終端ログ数 (ノーツごとの帰属に使う)。</summary>
    readonly Dictionary<int, int> holdEndCountAtHead = new Dictionary<int, int>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (GetArg("-autotest") != "v6hold") { return; }
        var go = new GameObject("AutoTestV6Hold");
        DontDestroyOnLoad(go);
        go.AddComponent<AutoTestV6Hold>();
    }

    /// <summary>
    /// 入口。例外が投げられても結果ファイル (fail=1) が書けるようにする。
    /// </summary>
    IEnumerator Start()
    {
        outDir = GetArg("-autotest-out");
        if (string.IsNullOrEmpty(outDir)) { outDir = Application.persistentDataPath; }
        songTitle = GetArg("-autotest-song");
        if (string.IsNullOrEmpty(songTitle)) { songTitle = DefaultSongTitle; }

        try { Directory.CreateDirectory(outDir); }
        catch (Exception) { }

        Application.logMessageReceived += OnLog;

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

    void OnDestroy()
    {
        Application.logMessageReceived -= OnLog;
    }

    /// <summary>Judge などのログを見て HOLD の終端を数える。</summary>
    void OnLog(string condition, string stackTrace, LogType type)
    {
        if (string.IsNullOrEmpty(condition)) { return; }
        if (condition == "Hold clear")
        {
            holdClearCount++;
            holdEndEvents.Add("clear@" + GManager.step_time.ToString("F2", CultureInfo.InvariantCulture));
        }
        else if (condition == "Hold broken")
        {
            holdBrokenCount++;
            holdEndEvents.Add("broken@" + GManager.step_time.ToString("F2", CultureInfo.InvariantCulture));
        }
        else if (condition == "Miss")
        {
            missLogCount++;
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

        // 3. 譜面を差し込む (カタログには追加しない)。
        //    固定は Ready → SelectSceneManager.GameScene → GManager.ResetSession
        //    → BeginPlaySession → PlaySessionSnapshot.Begin のときに行われるので、
        //    **Ready を押す前** に差し替えを設定する。
        PlaySessionSnapshot.ChartJsonOverride = HoldProbeChart.Json;
        Debug.Log(Tag + " chart override set: " + HoldProbeChart.Json.Length + " chars");

        GManager.songID = target.SongId;
        GManager.ResetSession();

        yield return new WaitForEndOfFrame();
        string selectShot = CaptureScreenshot("01-select");
        yield return new WaitForSecondsRealtime(0.8f);
        Check("the screenshot 01-select was written", File.Exists(selectShot));

        // 4. Ready → ゲーム画面。
        view.TriggerReady();

        float waited = 0f;
        while (waited < 20f)
        {
            if (SceneManager.GetActiveScene().name == "GameScene") { break; }
            waited += 0.5f;
            yield return new WaitForSecondsRealtime(0.5f);
        }
        Check("ready starts the game scene", SceneManager.GetActiveScene().name == "GameScene");
        Check("the started song is the picked one", GManager.songID == target.SongId);

        // 差し込んだ譜面が固定されたか (StartTime ではなく ChartJson で確認する)。
        PlaySessionSnapshot snapshot = PlaySessionSnapshot.Current;
        Check("the play session pinned the probe chart",
              snapshot != null && snapshot.ChartJson == HoldProbeChart.Json);

        // 5. 譜面が読めるまで待つ。
        NotesManager notes = null;
        float notesWaited = 0f;
        while (notesWaited < 60f)
        {
            notes = FindFirstObjectByType<NotesManager>();
            if (notes != null && notes.IsReady) { break; }
            notesWaited += 0.5f;
            yield return new WaitForSecondsRealtime(0.5f);
        }
        Check("the chart is ready", notes != null && notes.IsReady);
        if (notes == null || !notes.IsReady)
        {
            Finish();
            yield break;
        }

        // a. HOLD 入り譜面が読み込まれている。
        runtimeNoteCount = notes.noteNum;
        CountNoteTypes(notes);
        Debug.Log(Tag + " chart: notes=" + runtimeNoteCount + " taps=" + probeTapCount +
                  " holds=" + probeHoldCount + " maxScore=" + GManager.maxScore);

        Check("the probe chart is loaded (runtime notes == " + HoldProbeChart.TotalNotes + ")",
              runtimeNoteCount == HoldProbeChart.TotalNotes);
        Check("the probe chart has " + HoldProbeChart.HoldCount + " HOLD notes",
              probeHoldCount == HoldProbeChart.HoldCount);
        Check("the probe chart has " + HoldProbeChart.TapCount + " tap notes",
              probeTapCount == HoldProbeChart.TapCount);
        Check("the score ceiling follows the probe chart (maxScore == notes x 5)",
              GManager.maxScore == HoldProbeChart.TotalNotes * 5);

        // b. HOLD のノーツ実体と胴体 (holdSeconds)。
        InspectHoldInstances(notes);
        Check("the HOLD notes exist as objects with a stretched body (holdSeconds > 0)",
              holdInstanceCount == HoldProbeChart.HoldCount);
        Check("the HOLD body length is positive (maxHoldSeconds > 0)", maxHoldSeconds > 0f);

        yield return new WaitForSecondsRealtime(1f);
        yield return new WaitForEndOfFrame();
        string gameShot = CaptureScreenshot("02-game");
        yield return new WaitForSecondsRealtime(0.8f);
        Check("the screenshot 02-game was written", File.Exists(gameShot));

        // 6. メディアの準備 (実機の yt-dlp ダウンロード) を待つ。
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

        // 7. Space で再生を開始する。
        KeyBindings.InjectKeyDown(KeyCode.Space);
        yield return new WaitForSecondsRealtime(1.5f);
        Check("the playback started", music.PlaybackStarted);
        yield return new WaitForEndOfFrame();
        string playShot = CaptureScreenshot("03-gameplay");
        yield return new WaitForSecondsRealtime(0.8f);
        Check("the screenshot 03-gameplay was written", File.Exists(playShot));

        // 8. 再生 → 判定 → 曲終了 → Result まで待つ。
        SongEndState lastSongEndState = SongEndState.Playing;
        bool songEndCompleted = false;
        bool songEndForcedByDeadline = false;
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

                AutoHit(notes);
                ObserveHolds(notes);

                if (GManager.combo > comboPeak) { comboPeak = GManager.combo; }

                if (lastSongEndState == SongEndState.Completed)
                {
                    songEndCompleted = true;
                    songEndForcedByDeadline = music.SongEndForcedByDeadline;
                }
            }

            if (scene == "ResultScene") { break; }
            if (lastSongEndState == SongEndState.Failed) { break; }

            yield return null;
        }

        int judged = GManager.perfect + GManager.great + GManager.bad + GManager.miss;
        int settled = GManager.settledNoteCount;

        Debug.Log(Tag + " playback summary: songEndState=" + lastSongEndState +
                  " mediaTime=" + lastMediaTime.ToString("F2", CultureInfo.InvariantCulture) +
                  " hitsSent=" + hitsSent +
                  " perfect=" + GManager.perfect + " great=" + GManager.great +
                  " bad=" + GManager.bad + " miss=" + GManager.miss +
                  " score=" + GManager.score + " combo=" + GManager.combo +
                  " maxCombo=" + GManager.maxCombo + " comboPeak=" + comboPeak +
                  " judged=" + judged + " settled=" + settled +
                  " chartNotes=" + probeNoteCount + " runtimeNotes=" + runtimeNoteCount +
                  " holdClear=" + holdClearCount + " holdBroken=" + holdBrokenCount +
                  " missLog=" + missLogCount +
                  " holdObserved=" + holdObservedCount + " holdEnded=" + holdEndedCount +
                  " holdBodiesVisible=" + holdBodiesVisible +
                  " forcedByDeadline=" + songEndForcedByDeadline);

        Check("the media clock advances", mediaClockAdvanced);
        Check("every probe note is settled (settled == chart notes)", settled == HoldProbeChart.TotalNotes);
        Check("the score is greater than zero", GManager.score > 0);
        Check("the combo rises above zero during the playback", comboPeak > 0);
        Check("the peak combo is recorded", GManager.maxCombo > 0);
        Check("the score is not the tap-only score (maxScore == 35, not the catalog chart)",
              GManager.maxScore == HoldProbeChart.TotalNotes * 5);

        // c. HOLD の判定: 先頭が当たり、終端フェーズまで到達している。
        Check("all HOLD notes reached their end phase", holdEndedCount == HoldProbeChart.HoldCount);
        Check("the HOLD bodies were visible while held", holdBodiesVisible == HoldProbeChart.HoldCount);
        Check("the HOLD hold phase was observed (start / hold / end)",
              holdObservedCount == HoldProbeChart.HoldCount);
        Check("every HOLD was sustained to its end (no broken hold)",
              holdBrokenCount == 0 && holdClearCount == HoldProbeChart.HoldCount);

        Check("the song end state machine completes", songEndCompleted);
        Check("the song end is not a forced deadline completion", songEndCompleted && !songEndForcedByDeadline);
        Check("the result scene is loaded", SceneManager.GetActiveScene().name == "ResultScene");

        // 9. Result 画面が実際に描画されているか。
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
            string resultShot = CaptureScreenshot("06-result");
            yield return new WaitForSecondsRealtime(0.8f);
            Check("the screenshot 06-result was written", File.Exists(resultShot));
        }

        Finish();
    }

    /// <summary>譜面のノーツ種別を数える (HOLD / タップ)。</summary>
    void CountNoteTypes(NotesManager notes)
    {
        probeNoteCount = 0;
        probeHoldCount = 0;
        probeTapCount = 0;
        if (notes == null || notes.NoteType == null) { return; }
        for (int i = 0; i < notes.NoteType.Count; i++)
        {
            int type = notes.NoteType[i];
            if (type != LongNote.TYPE_HOLD && type != LongNote.TYPE_TAP) { continue; }
            probeNoteCount++;
            if (type == LongNote.TYPE_HOLD) { probeHoldCount++; }
            else { probeTapCount++; }
        }
    }

    /// <summary>
    /// HOLD のノーツ実体 (胴体が伸びているか) を見る。
    /// </summary>
    void InspectHoldInstances(NotesManager notes)
    {
        holdInstanceCount = 0;
        maxHoldSeconds = 0f;
        if (notes == null || notes.NotesObj == null) { return; }
        for (int i = 0; i < notes.NotesObj.Count; i++)
        {
            GameObject go = notes.NotesObj[i];
            if (go == null) { continue; }
            Notes component = go.GetComponent<Notes>();
            if (component == null) { continue; }
            if (component.holdSeconds <= 0f) { continue; }
            holdInstanceCount++;
            if (component.holdSeconds > maxHoldSeconds) { maxHoldSeconds = component.holdSeconds; }
        }
    }

    /// <summary>
    /// 毎フレーム: HOLD のフェーズ (開始 / 保持 / 終端) を観測する。
    ///
    ///   - 開始: 先頭の押下 (キー注入) で判定が始まる。holdEndCountAtHead に
    ///     その時点の HOLD 終端ログ数を控えて、終端ログとの帰属を
    ///     できるようにする。
    ///   - 保持: 先頭と終端のあいだに胴体が描画されている (レンダラーが見えて
    ///     いて、長さが伸びている)。
    ///   - 終端: 終端時刻をすぎたときに HOLD の終端ログが増えている。
    /// </summary>
    void ObserveHolds(NotesManager notes)
    {
        if (notes == null || !notes.IsReady) { return; }
        if (notes.NoteType == null || notes.NotesTime == null || notes.NotesEndTime == null) { return; }

        float mediaTime = GManager.step_time;
        int count = Mathf.Min(notes.NoteType.Count, Mathf.Min(notes.NotesTime.Count, notes.NotesEndTime.Count));

        for (int i = 0; i < count; i++)
        {
            if (notes.NoteType[i] != LongNote.TYPE_HOLD) { continue; }
            float head = notes.NotesTime[i];
            float end = notes.NotesEndTime[i];
            if (end <= head) { continue; }

            if (!holdEndCountAtHead.ContainsKey(i))
            {
                holdEndCountAtHead[i] = holdClearCount + holdBrokenCount;
            }

            // 保持フェーズ: 胴体が描画されているか。
            bool inHold = mediaTime >= head + HoldObserveFrom && mediaTime <= end - HoldObserveTo;
            if (inHold && !holdObserved.Contains(i))
            {
                holdObserved.Add(i);
                holdObservedCount++;
                if (IsHoldBodyVisible(notes, i)) { holdBodiesVisible++; }
                if (!holdShotTaken)
                {
                    holdShotTaken = true;
                    StartCoroutine(CaptureHoldShot());
                }
            }

            // 終端フェーズ: 終端をすぎて、終端ログが増えているか。
            if (!holdEnded.Contains(i) && mediaTime > end + 0.4f)
            {
                if (holdClearCount + holdBrokenCount > holdEndCountAtHead[i])
                {
                    holdEnded.Add(i);
                    holdEndedCount++;
                }
            }
        }

        if (!scheduleWritten && mediaTime > 0.2f)
        {
            WriteHoldSchedule(notes, mediaTime);
        }
    }

    /// <summary>HOLD の胴体 (3D の箱) が画面に映っているか。</summary>
    static bool IsHoldBodyVisible(NotesManager notes, int index)
    {
        if (notes.NotesObj == null || index < 0 || index >= notes.NotesObj.Count) { return false; }
        GameObject go = notes.NotesObj[index];
        if (go == null) { return false; }
        MeshRenderer renderer = go.GetComponentInChildren<MeshRenderer>();
        if (renderer == null) { return false; }
        if (!renderer.enabled) { return false; }
        // 胴体は長さぶん伸びている (scale.z = holdSeconds x noteSpeed)。
        if (renderer.bounds.size.z <= 0.5f) { return false; }
        return renderer.isVisible;
    }

    /// <summary>保持中のスクリーンショットを撮る (1 フレーム待って保存)。</summary>
    IEnumerator CaptureHoldShot()
    {
        yield return new WaitForEndOfFrame();
        string path = CaptureScreenshot("04-hold-body");
        Debug.Log(Tag + " hold body screenshot: " + path);
        yield return new WaitForSecondsRealtime(1f);
        Check("the screenshot 04-hold-body was written", File.Exists(path));
    }

    /// <summary>
    /// OS レベルのキー押下スケジュールをハーネス外 (シェル) へ渡す。
    ///
    /// HOLD の保持は Input.GetKey (実キー) を見ているので、キー注入だけでは
    /// 保持できない。macOS のハーネス (script/mac-holdprobe-harness.sh) が
    /// このファイルを読み、該当のキーを **押したまま** にする。
    ///
    /// 書き出す行:
    ///   lane=&lt;lane&gt; keycode=&lt;macOS 仮想キーコード or -1&gt;
    ///   headMedia=&lt;判定開始のメディア時刻&gt; endMedia=&lt;終端のメディア時刻&gt;
    ///   headWall=&lt;その絶対時刻 (epoch 秒)&gt; endWall=&lt;その絶対時刻 (epoch 秒)&gt;
    /// </summary>
    void WriteHoldSchedule(NotesManager notes, float mediaTime)
    {
        scheduleWritten = true;
        if (notes == null || notes.NoteType == null || notes.NotesTime == null || notes.NotesEndTime == null) { return; }

        double nowEpoch = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0;
        int count = Mathf.Min(notes.NoteType.Count, Mathf.Min(notes.NotesTime.Count, notes.NotesEndTime.Count));

        var lines = new List<string>();
        for (int i = 0; i < count; i++)
        {
            if (notes.NoteType[i] != LongNote.TYPE_HOLD) { continue; }
            int lane = notes.LaneNum[i];
            float head = notes.NotesTime[i];
            float end = notes.NotesEndTime[i];
            int keycode = CGEventKeycodeForLane(lane);
            double headWall = nowEpoch + (head - mediaTime);
            double endWall = nowEpoch + (end - mediaTime);
            lines.Add(string.Format(CultureInfo.InvariantCulture,
                "lane={0} keycode={1} headMedia={2:F3} endMedia={3:F3} headWall={4:F3} endWall={5:F3}",
                lane, keycode, head, end, headWall, endWall));
        }

        try
        {
            string path = Path.Combine(outDir, ScheduleFileName);
            File.WriteAllLines(path, lines);
            Debug.Log(Tag + " hold schedule written: " + path + " lines=" + lines.Count);
            foreach (string line in lines) { Debug.Log(Tag + " schedule: " + line); }
        }
        catch (Exception e)
        {
            Debug.LogWarning(Tag + " could not write the hold schedule: " + e.Message);
        }
    }

    /// <summary>
    /// そのレーンで押せるキー对应的 macOS 仮想キーコード。
    /// 既定の割り当てで英字を含むレーン (A / D / H) だけを Corresponding キーとして使う。
    /// そのレーンに対応キーが無いときは -1 (押下なし = キー注入のみ)。
    /// </summary>
    static int CGEventKeycodeForLane(int lane)
    {
        KeyCode wanted;
        switch (lane)
        {
            case 0: wanted = KeyCode.A; break;
            case 2: wanted = KeyCode.D; break;
            case 4: wanted = KeyCode.H; break;
            default: return -1;
        }

        KeyCode[] keys = KeyBindings.GetKeys(lane);
        if (keys == null) { return -1; }
        for (int i = 0; i < keys.Length; i++)
        {
            if (keys[i] == wanted) { return CGEventKeycodeFor(wanted); }
        }
        return -1;
    }

    /// <summary>Unity の KeyCode → macOS の仮想キーコード (ANSI の物理位置)。</summary>
    static int CGEventKeycodeFor(KeyCode key)
    {
        switch (key)
        {
            case KeyCode.A: return 0;
            case KeyCode.S: return 1;
            case KeyCode.D: return 2;
            case KeyCode.F: return 3;
            case KeyCode.H: return 4;
            case KeyCode.G: return 5;
            case KeyCode.Z: return 6;
            case KeyCode.X: return 7;
            case KeyCode.C: return 8;
            case KeyCode.V: return 9;
            case KeyCode.Q: return 12;
            case KeyCode.W: return 13;
            case KeyCode.E: return 14;
            case KeyCode.R: return 15;
            case KeyCode.Y: return 16;
            case KeyCode.T: return 17;
            default: return -1;
        }
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
    /// ノーツの判定時刻に合わせてレーンのキーを注入する (先頭の押下)。
    /// HOLD の先頭もこれで押す (保持フェーズだけは実キーが必要)。
    /// </summary>
    void AutoHit(NotesManager notes)
    {
        if (notes == null || !notes.IsReady) { return; }
        if (notes.NotesTime == null || notes.LaneNum == null) { return; }

        float mediaTime = GManager.step_time;
        int count = Mathf.Min(notes.NotesTime.Count, notes.LaneNum.Count);

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
        public bool CanvasFound;
        public bool TextFound;
        public bool TitleFound;
        public bool ScoreFound;
        public string Detail;
    }

    /// <summary>
    /// Result 画面が描画されているか: v6 の Result Canvas が生きていて、
    /// その Canvas の下に曲名と最終スコアが出ているか。
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

    /// <summary>Result 画面に表示されるスコアの文字列 (桁区切り付き)。</summary>
    static string FormatScoreForResult()
    {
        if (GManager.score <= 0) { return ""; }
        return GManager.score.ToString("N0", CultureInfo.InvariantCulture);
    }

    /// <summary>スクリーンショットのファイル名を返す (撮影自体は同期)。</summary>
    string CaptureScreenshot(string name)
    {
        shot++;
        string path = Path.Combine(outDir, string.Format("autotest-v6hold-{0:00}-{1}.png", shot, name));
        ScreenCapture.CaptureScreenshot(path);
        return path;
    }

    private void Finish()
    {
        Debug.Log(Tag + " RESULT pass=" + pass + " fail=" + fail);
        try
        {
            File.WriteAllText(Path.Combine(outDir, ResultFileName),
                Tag + " RESULT pass=" + pass + " fail=" + fail + "\n" +
                "song=" + songTitle + "\n" +
                "scene=" + SceneManager.GetActiveScene().name + "\n" +
                "chartNotes=" + probeNoteCount + " runtimeNotes=" + runtimeNoteCount +
                " taps=" + probeTapCount + " holds=" + probeHoldCount + "\n" +
                "holdInstances=" + holdInstanceCount + " maxHoldSeconds=" +
                maxHoldSeconds.ToString("F2", CultureInfo.InvariantCulture) + "\n" +
                "holdObserved=" + holdObservedCount + " holdEnded=" + holdEndedCount +
                " holdBodiesVisible=" + holdBodiesVisible + "\n" +
                "holdClear=" + holdClearCount + " holdBroken=" + holdBrokenCount +
                " missLog=" + missLogCount + " holdEndEvents=" + string.Join(",", holdEndEvents) + "\n" +
                "perfect=" + GManager.perfect + " great=" + GManager.great +
                " bad=" + GManager.bad + " miss=" + GManager.miss + "\n" +
                "judged=" + (GManager.perfect + GManager.great + GManager.bad + GManager.miss) + "\n" +
                "settled=" + GManager.settledNoteCount + "\n" +
                "maxScore=" + GManager.maxScore + " score=" + GManager.score +
                " combo=" + GManager.combo + " maxCombo=" + GManager.maxCombo +
                " comboPeak=" + comboPeak + " hitsSent=" + hitsSent + "\n");
        }
        catch (Exception e) { Debug.LogWarning(Tag + " could not write the result file: " + e.Message); }

        PlaySessionSnapshot.ChartJsonOverride = null;
        Application.Quit();
    }

    /// <summary>例外で中断されたときに呼ぶ (fail=1 の結果ファイルを残す)。</summary>
    void WriteFailureResult(Exception e)
    {
        fail++;
        Debug.LogError(Tag + " FAIL the harness threw an exception: " + e);
        try
        {
            File.WriteAllText(Path.Combine(outDir, ResultFileName),
                Tag + " RESULT pass=" + pass + " fail=" + fail + "\n" +
                "song=" + songTitle + "\n" +
                "exception=" + e.GetType().Name + ": " + e.Message + "\n" +
                "scene=" + SceneManager.GetActiveScene().name + "\n");
        }
        catch (Exception writeError)
        {
            Debug.LogWarning(Tag + " could not write the failure result file: " + writeError.Message);
        }

        PlaySessionSnapshot.ChartJsonOverride = null;
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
