using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using FlashBeat.Gameplay;
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
/// 何を確認する인가:
///   1. 選曲画面から実カタログの曲を選んで Ready → GameScene へ到達する。
///   2. メディアのパイプライン (yt-dlp でのダウンロード) が完了する。
///   3. Space で再生が始まり、メディア時計 (media time) が進む。
///   4. ノーツが判定される (判定数 = perfect+great+bad+miss が増える / スコアが動く)。
///   5. 曲終了が検出される (SongEndMachine が Completed になる)。
///   6. ResultScene へ遷移し、Result 画面が実際に描画される。
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

    /// <summary>ノーツを叩く注入の窓 (秒)。 judge は次のフレームで Keys を見る。</summary>
    const float HitWindowSeconds = 0.05f;

    int pass;
    int fail;
    int shot;
    string outDir;
    string songTitle;

    /// <summary>既にキー入力を送ったノーツ (同じノーツを 2 回叩かない)。</summary>
    readonly HashSet<int> hitSent = new HashSet<int>();

    /// <summary>
    /// 最後に見た曲終了の状態 (Result へ遷移すると MusicManager が消えるため保持)。
    ///
    /// 注意: このハーネスは **GManager.nextNotes を読みません**。nextNotes は
    /// 「判定イベント数」であり、HOLD 中断 (BAD 差し替え) では同一ノーツが 2 回分
    /// 数えられ得る (Issue #182 / PR #256)。「曲終了が起きた」ことは
    /// 状態機械そのもの (`MusicManager.SongEndState` = SongEndMachine.State) で
    /// 観測するので、nextNotes の定義に依存しない。
    /// 読み取るのは判定の実数 (perfect / great / bad / miss) とスコアだけで、
    /// こちらは「ノーツ 1 個につき 1 判定」なので HOLD でも二重計上されない。
    /// </summary>
    SongEndState lastSongEndState = SongEndState.Playing;

    int hitsSent;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (GetArg("-autotest") != "v6songend") { return; }
        var go = new GameObject("AutoTestV6SongEnd");
        DontDestroyOnLoad(go);
        go.AddComponent<AutoTestV6SongEnd>();
    }

    IEnumerator Start()
    {
        outDir = GetArg("-autotest-out");
        if (string.IsNullOrEmpty(outDir)) { outDir = Application.persistentDataPath; }
        songTitle = GetArg("-autotest-song");
        if (string.IsNullOrEmpty(songTitle)) { songTitle = DefaultSongTitle; }

        try { Directory.CreateDirectory(outDir); }
        catch (Exception) { }

        Debug.Log(Tag + " start: song='" + songTitle + "' outDir='" + outDir + "'");

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
        yield return Screenshot("01-select");

        // 3. Ready → ゲーム画面 (実セッションとして遷移する)。
        int expectedSongId = data.Selected == null ? -1 : data.Selected.SongId;
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
        yield return new WaitForSecondsRealtime(3f);
        yield return Screenshot("02-game");

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
        yield return Screenshot("03-gameplay");

        // 6. 再生 → 判定 → 曲終了 → Result まで待つ。
        NotesManager notes = FindFirstObjectByType<NotesManager>();
        bool mediaClockAdvanced = false;
        bool notesJudged = false;
        bool scoreMoved = false;
        bool songEndCompleted = false;
        float lastMediaTime = -1f;
        float songWaited = 0f;

        while (songWaited < SongEndTimeout)
        {
            string scene = SceneManager.GetActiveScene().name;

            if (music != null)
            {
                lastSongEndState = music.SongEndState;
                float mediaTime = music.MediaTimeNow;
                if (lastMediaTime >= 0f && mediaTime > lastMediaTime + 0.01f) { mediaClockAdvanced = true; }
                if (lastMediaTime < 0f || mediaTime > lastMediaTime) { lastMediaTime = mediaTime; }

                AutoHit(notes);

                int judged = GManager.perfect + GManager.great + GManager.bad + GManager.miss;
                if (judged > 0) { notesJudged = true; }
                if (GManager.perfect + GManager.great > 0 || GManager.score > 0) { scoreMoved = true; }
                if (lastSongEndState == SongEndState.Completed) { songEndCompleted = true; }
            }

            if (scene == "ResultScene") { break; }
            if (lastSongEndState == SongEndState.Failed) { break; }

            songWaited += 0.25f;
            yield return new WaitForSecondsRealtime(0.25f);
        }

        Debug.Log(Tag + " playback summary: songEndState=" + lastSongEndState +
                  " mediaTime=" + lastMediaTime.ToString("F2") +
                  " hitsSent=" + hitsSent +
                  " perfect=" + GManager.perfect + " great=" + GManager.great +
                  " bad=" + GManager.bad + " miss=" + GManager.miss +
                  " score=" + GManager.score + " combo=" + GManager.combo);

        Check("the media clock advances", mediaClockAdvanced);
        Check("the notes are judged", notesJudged);
        Check("the score and the combo move", scoreMoved);
        Check("the song end state machine completes", songEndCompleted);
        Check("the result scene is loaded", SceneManager.GetActiveScene().name == "ResultScene");

        // 7. Result 画面が実際に描画されているか。
        if (SceneManager.GetActiveScene().name == "ResultScene")
        {
            yield return new WaitForSecondsRealtime(2f);
            Check("the result screen is drawn", ResultScreenDrawn(out string detail));
            Debug.Log(Tag + " result screen: " + detail);
            yield return Screenshot("04-result");
        }

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
    /// ノーツの判定時刻に合わせてレーンのキーを注入する (命中の証拠)。
    /// 判定は Judge が GManager.step_time (メディア時刻) で見るので、同じ時計で
    /// 叩く。judge 側は入力を 1 フレーム遅れて読むため、わずかに早めに打って
    /// 、丁度 judge の Perfect 窓 (0.1 秒) に入るようにする。
    /// </summary>
    void AutoHit(NotesManager notes)
    {
        if (notes == null || !notes.IsReady) { return; }
        if (notes.NotesTime == null || notes.LaneNum == null) { return; }

        float mediaTime = GManager.step_time;
        int count = Mathf.Min(notes.NotesTime.Count, notes.LaneNum.Count);
        for (int i = 0; i < count; i++)
        {
            if (hitSent.Contains(i)) { continue; }
            float time = notes.NotesTime[i];
            if (time > 900f) { hitSent.Add(i); continue; } // 番兵 (ダミー行)

            int lane = notes.LaneNum[i];
            if (lane < 0 || lane > 7) { hitSent.Add(i); continue; }

            float delta = time - mediaTime;
            if (delta > HitWindowSeconds) { continue; }      // まだ早い
            if (delta < -0.25f) { hitSent.Add(i); continue; }  // 窗口を過ぎた (Miss 相当)

            KeyCode[] keys = KeyBindings.GetKeys(lane);
            if (keys == null || keys.Length == 0) { hitSent.Add(i); continue; }
            KeyBindings.InjectKeyDown(keys[0]);
            hitSent.Add(i);
            hitsSent++;
        }
    }

    /// <summary>
    /// Result 画面が描画されているか: v6 の Result Canvas が生きていて、
    /// 画面の中に空でない文字があること。
    /// </summary>
    static bool ResultScreenDrawn(out string detail)
    {
        detail = "";
        bool canvasFound = false;
        bool textFound = false;

        var texts = FindObjectsOfType<TextMeshProUGUI>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            TextMeshProUGUI text = texts[i];
            if (text == null) { continue; }
            if (!text.isActiveAndEnabled) { continue; }
            var canvas = text.GetComponentInParent<Canvas>();
            if (canvas == null || !canvas.isActiveAndEnabled) { continue; }
            if (canvas.gameObject.name == "V6 Result Canvas") { canvasFound = true; }
            if (!string.IsNullOrEmpty(text.text)) { textFound = true; }
        }

        detail = "v6ResultCanvas=" + canvasFound + " nonEmptyText=" + textFound +
                 " texts=" + texts.Length + " scene=" + SceneManager.GetActiveScene().name;
        return canvasFound && textFound;
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
                "scene=" + SceneManager.GetActiveScene().name + "\n" +
                "perfect=" + GManager.perfect + " great=" + GManager.great +
                " bad=" + GManager.bad + " miss=" + GManager.miss + "\n" +
                "score=" + GManager.score + " combo=" + GManager.combo + "\n");
        }
        catch (Exception e) { Debug.LogWarning(Tag + " could not write the result file: " + e.Message); }

        Application.Quit();
    }

    private IEnumerator Screenshot(string name)
    {
        yield return new WaitForEndOfFrame();
        shot++;
        string path = Path.Combine(outDir, string.Format("autotest-v6songend-{0:00}-{1}.png", shot, name));
        ScreenCapture.CaptureScreenshot(path);
        yield return new WaitForSecondsRealtime(0.8f);
        Check("the screenshot " + name + " was written", File.Exists(path));
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
