using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using FlashBeat.UI.V6.Select;
using FlashBeat.UI.V6.Select.View;

/// <summary>
/// v6 選曲→Ready→ゲーム の実データ経路を検証する (Issue #223)。
///
/// 使い方: プレイヤー引数に "-autotest v6" と "-autotest-out &lt;dir&gt;" を渡す。
/// 選曲画面で実カタログの曲を選び、Ready でゲーム画面へ遷移したことを
/// スクリーンショット付きで確認する。
/// </summary>
public class AutoTestV6 : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (GetArg("-autotest") != "v6") { return; }
        var go = new GameObject("AutoTestV6");
        DontDestroyOnLoad(go);
        go.AddComponent<AutoTestV6>();
    }

    private int pass;
    private int fail;
    private int shot;
    private string outDir;

    private IEnumerator Start()
    {
        outDir = GetArg("-autotest-out");
        if (string.IsNullOrEmpty(outDir)) { outDir = Application.persistentDataPath; }
        try { Directory.CreateDirectory(outDir); }
        catch (Exception) { }

        // 1. 選曲画面 (v6 の 3 カラム) が立ち上がる。
        SceneManager.LoadScene("SelectScene");
        yield return new WaitForSecondsRealtime(5f);
        Check("the select scene is loaded", SceneManager.GetActiveScene().name == "SelectScene");

        V6SelectView view = V6SelectView.Current;
        Check("the v6 select view exists", view != null);
        Check("the select screen lists real songs", view != null && view.VisibleSongCount > 0);
        yield return Screenshot("01-select");

        if (view == null)
        {
            Finish();
            yield break;
        }

        IV6SelectDataSource data = view.Data;
        Check("the data source is present", data != null);

        // 2. 実カタログから曲を選ぶ (固定データではないことを確かめる)。
        IReadOnlyList<V6SongEntry> songs = data.AllSongs;
        Check("the catalog has more than one song", songs != null && songs.Count > 1);

        V6SongEntry target = null;
        for (int i = 0; i < songs.Count; i++)
        {
            if (songs[i] != null && !string.IsNullOrEmpty(songs[i].Title)) { target = songs[i]; break; }
        }
        Check("a song with a real title exists", target != null);

        if (target != null)
        {
            data.Select(target.SongId);
            yield return new WaitForSecondsRealtime(0.4f);
            Check("the selection follows the picked song", data.Selected != null && data.Selected.SongId == target.SongId);
            Check("the selected song has a title", data.Selected != null && !string.IsNullOrEmpty(data.Selected.Title));
            yield return Screenshot("02-select-selected");
        }

        // 3. Ready → ゲーム画面 (実セッションとして遷移する)。
        int expectedSongId = data.Selected == null ? -1 : data.Selected.SongId;
        view.TriggerReady();

        float waited = 0f;
        while (waited < 12f)
        {
            if (SceneManager.GetActiveScene().name == "GameScene") { break; }
            waited += 0.5f;
            yield return new WaitForSecondsRealtime(0.5f);
        }
        Check("ready starts the game scene", SceneManager.GetActiveScene().name == "GameScene");
        Check("the started song is the picked one", GManager.songID == expectedSongId);
        yield return new WaitForSecondsRealtime(3f);
        yield return Screenshot("03-game");

        // 4. 旧 UI が残っていないか。v6 モードでは表示の正本 (GameUiExclusivity,
        //    Issue #233) が全フェーズで v5 の Canvas を隠す。Ready 中 (再生前) の
        //    V5ReadyCanvas も正当な表示ではない (Ready 案内は v6 HUD が出す)。
        //
        //    「残っている」の判定は **実際に描かれているか** で見る。
        //    isActiveAndEnabled だけでは、実行時生成の旧ロード表示
        //    (LoadingOverlay) は alpha 0 で待機しているだけなのに
        //    「見えている」ことになってしまう (実測の誤検出)。
        var graphics = FindObjectsByType<UnityEngine.UI.Graphic>(FindObjectsSortMode.None);
        int legacyVisible = 0;
        for (int i = 0; i < graphics.Length; i++)
        {
            var g = graphics[i];
            if (g == null) continue;
            if (!FlashBeat.UI.V5.GraphicVisibility.IsDrawn(g)) continue;
            var canvas = g.GetComponentInParent<Canvas>();
            if (canvas == null || canvas.name.StartsWith("V6")) continue;
            legacyVisible++;
            Debug.Log("[AutoTestV6] LEGACY VISIBLE: " + canvas.name + " / " + g.gameObject.name);
        }
        Check("no legacy graphics are visible", legacyVisible == 0);

        // 4b. 旧 v5 の歌詞オブジェクト (QuestionText / FuriganaText / RomajiText) が
        //     active だと v6 の HUD の歌詞と **二重表示** になる (Issue #233)。
        //     旧 UI 排他 (GameSceneManager.ApplyTextPanelVisibility) が
        //     TextPanel を隠すので、歌詞の 3 オブジェクトも inactive になる。
        var legacyLyricActive = 0;
        var transforms = FindObjectsByType<Transform>(FindObjectsSortMode.None);
        for (int i = 0; i < transforms.Length; i++)
        {
            var t = transforms[i];
            if (t == null) continue;
            if (!FlashBeat.UI.V5.GameUiExclusivity.IsLegacyLyricObjectName(t.name)) continue;
            if (!t.gameObject.activeInHierarchy) continue;
            legacyLyricActive++;
            Debug.Log("[AutoTestV6] LEGACY LYRIC ACTIVE: " + t.name);
        }
        Check("no legacy lyric objects are active", legacyLyricActive == 0);

        // 4c. 旧ロード表示 (LoadingOverlay) が画面に被っていないか。
        //     v6 モードの曲準備は V6LoadingView が所有し、旧オーバーレイは
        //     alpha 0 で待機しているだけなので「描かれていない」ことが正。
        var legacyOverlay = GameObject.Find(FlashBeat.UI.V5.GameUiExclusivity.LegacyLoadingOverlayName);
        bool legacyOverlayDrawn = false;
        if (legacyOverlay != null)
        {
            var group = legacyOverlay.GetComponent<CanvasGroup>();
            legacyOverlayDrawn = group == null
                || group.alpha > FlashBeat.UI.V5.GraphicVisibility.InvisibleAlpha;
        }
        Check("the legacy loading overlay is not drawn", !legacyOverlayDrawn);

        // 5. v6 のプレイフィールドと HUD が scene にある。
        // v6 の遠近法プレイフィールドは既定で無効 (元の 3D レーンを使う)。
        if (FlashBeat.UI.V6.V6UiMode.Playfield)
        {
            Check("the v6 playfield exists", FlashBeat.UI.V6.Game.Playfield.V6PlayfieldView.Current != null);
        }
        else
        {
            Check("the original 3D lanes are kept", FlashBeat.UI.V6.Game.Playfield.V6PlayfieldView.Current == null);
        }
        Check("the v6 hud exists", FindFirstObjectByType<FlashBeat.UI.V6.Game.Hud.V6GameHudView>() != null);

        // 5. 実機確認: メディア的准备 → Space で開始 → ノーツが流れる状態の画面を撮る。
        MusicManager music = FindFirstObjectByType<MusicManager>();
        float mediaWaited = 0f;
        while (mediaWaited < 90f)
        {
            if (music != null && music.MediaReady) break;
            mediaWaited += 1f;
            yield return new WaitForSecondsRealtime(1f);
        }
        Check("the media is ready", music != null && music.MediaReady);
        yield return Screenshot("04-media-ready");

        if (music != null && music.MediaReady)
        {
            KeyBindings.InjectKeyDown(KeyCode.Space);
            yield return new WaitForSecondsRealtime(1.5f);
            Check("the playback started", music.PlaybackStarted);

            // Issue #233: 譜面が疎な区間 (先頭) で撮るとノートが画面に 1 枚も
            // 出ない。曲に依存しない方法として「実際にカメラに写っている
            // ノーツが 3 枚以上あり、手前 (z <= 5.5) まで入るまで待つ」ので、
            // どの曲でも白い帯 (ノート) が見える状態で撮れる。
            float denseWaited = 0f;
            int visibleNotes = 0;
            float nearestZ = 0f;
            float farthestZ = 0f;
            while (denseWaited < 60f)
            {
                ScanVisibleNotes(out visibleNotes, out nearestZ, out farthestZ);
                if (visibleNotes >= 3 && nearestZ <= 5.5f) break;
                denseWaited += 0.25f;
                yield return new WaitForSecondsRealtime(0.25f);
            }
            ScanVisibleNotes(out visibleNotes, out nearestZ, out farthestZ);
            Check("the media clock advances", music.MediaTimeNow > 0f);
            Debug.Log("[AutoTestV6] media time at capture = " + music.MediaTimeNow.ToString("F2")
                + " visible notes = " + visibleNotes
                + " z range = " + nearestZ.ToString("F2") + " .. " + farthestZ.ToString("F2"));
            Check("the capture lands in a dense chart region", visibleNotes >= 3);

            yield return Screenshot("05-gameplay");
        }

        Finish();
    }

    /// <summary>
    /// 実際にカメラに写っている 3D ノーツの枚数と z の範囲 (Issue #233)。
    /// Renderer.isVisible は「カメラに写っている」ことを表すので、
    /// 譜面が密でもカメラ外なら 0 になる。
    /// カメラ可視範囲は z &lt;= MediaTimeline.VisibleDepthZ (8.61) なので、
    /// この測定値がそのまま「見えているか」の実測値になる。
    /// </summary>
    private void ScanVisibleNotes(out int count, out float nearestZ, out float farthestZ)
    {
        count = 0;
        nearestZ = 0f;
        farthestZ = 0f;
        Notes[] notes = FindObjectsByType<Notes>(FindObjectsSortMode.None);
        for (int i = 0; i < notes.Length; i++)
        {
            if (notes[i] == null) continue;
            Renderer renderer = notes[i].GetComponentInChildren<Renderer>();
            if (renderer == null) continue;
            if (!renderer.enabled) continue;
            if (!renderer.isVisible) continue;
            count++;
            float z = notes[i].transform.position.z;
            if (count == 1 || z < nearestZ) nearestZ = z;
            if (count == 1 || z > farthestZ) farthestZ = z;
        }
    }

    private void Finish()
    {
        Debug.Log("[AutoTestV6] RESULT pass=" + pass + " fail=" + fail);
        try
        {
            File.WriteAllText(Path.Combine(outDir, "autotest-v6-result.txt"),
                "[AutoTestV6] RESULT pass=" + pass + " fail=" + fail + "\n");
        }
        catch (Exception e) { Debug.LogWarning("[AutoTestV6] could not write the result file: " + e.Message); }

        Application.Quit();
    }

    private IEnumerator Screenshot(string name)
    {
        yield return new WaitForEndOfFrame();
        shot++;
        string path = Path.Combine(outDir, string.Format("autotest-v6-{0}.png", shot));
        ScreenCapture.CaptureScreenshot(path);
        yield return new WaitForSecondsRealtime(0.6f);
        Check("the screenshot " + name + " was written", File.Exists(path));
    }

    private void Check(string label, bool ok)
    {
        if (ok) { pass++; Debug.Log("[AutoTestV6] PASS " + label); }
        else { fail++; Debug.Log("[AutoTestV6] FAIL " + label); }
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
