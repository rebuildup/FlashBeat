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
        var mode = GetArg("-autotest");
        if (mode != "v6" && mode != "v6playfield") { return; }
        // v6playfield モードでは -playfield 1 で遠近法プレイフィールドを
        // 有効にする (-playfield 0 または無指定なら既定の false のまま)。
        if (mode == "v6playfield")
        {
            FlashBeat.UI.V6.V6UiMode.Playfield = GetArg("-playfield") == "1";
        }
        Debug.Log("[AutoTestV6] playfield=" + (FlashBeat.UI.V6.V6UiMode.Playfield ? "1" : "0")
            + " (mode=" + mode + ")");
        var go = new GameObject("AutoTestV6");
        DontDestroyOnLoad(go);
        go.AddComponent<AutoTestV6>();
    }

    private int pass;
    private int fail;
    private int shot;
    private string outDir;

    // 直近の ScanVisibleNotes の測定結果 (イテレータは out 引数を持てないため保持する)。
    private int lastVisibleNotes;
    private float lastNearestZ;
    private float lastFarthestZ;

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

        // Issue #378: Ready → 最初の視覚変化 (フェード開始) / GameScene 到達 /
        // ローディング画面の表示までの実測。フレーム単位で追う。
        float readyAt = Time.unscaledTime;
        float fadeAt = -1f, gameAt = -1f, loadingAt = -1f, overlayAt = -1f;
        float measureWaited = 0f;
        while (measureWaited < 15f)
        {
            if (fadeAt < 0f)
            {
                var faderGo = GameObject.Find("Fader");
                var faderGroup = faderGo != null ? faderGo.GetComponent<CanvasGroup>() : null;
                if (faderGroup != null && faderGroup.alpha > 0.001f)
                {
                    fadeAt = Time.unscaledTime - readyAt;
                }
            }

            if (overlayAt < 0f && LoadingOverlay.IsVisibleForAudit)
            {
                overlayAt = Time.unscaledTime - readyAt;
            }

            if (gameAt < 0f && SceneManager.GetActiveScene().name == "GameScene")
            {
                gameAt = Time.unscaledTime - readyAt;
            }

            if (loadingAt < 0f)
            {
                var loadingView = FlashBeat.UI.V6.Game.Loading.V6LoadingView.Current;
                if (loadingView != null && loadingView.IsShownForAudit)
                {
                    loadingAt = Time.unscaledTime - readyAt;
                }
            }

            if (gameAt >= 0f && loadingAt >= 0f) break;
            measureWaited += Time.unscaledDeltaTime;
            yield return null;
        }

        Debug.Log("[AutoTestV6] ready->fade=" + fadeAt.ToString("F3")
            + "s ready->overlay=" + overlayAt.ToString("F3")
            + "s ready->game=" + gameAt.ToString("F3")
            + "s ready->loading=" + loadingAt.ToString("F3") + "s");
        Check("ready shows a visual change quickly (fade starts)", fadeAt >= 0f && fadeAt < 1.0f);
        Check("a loading indicator covers the scene transition", overlayAt >= 0f && overlayAt < 1.0f);
        Check("the loading view appears during scene entry", loadingAt >= 0f);

        float waited = 0f;
        while (waited < 12f && gameAt < 0f)
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
        //     判定は「実際に描かれているか」(GraphicVisibility.IsDrawn) に
        //     統一する (PR #263 レビュー N5/N6: CanvasGroup を 1 段だけ見る
        //     実装だと、親の alpha と子の alpha の積や、無効な Canvas を
        //     見落とす)。
        var legacyOverlay = GameObject.Find(FlashBeat.UI.V5.GameUiExclusivity.LegacyLoadingOverlayName);
        bool legacyOverlayDrawn = false;
        if (legacyOverlay != null)
        {
            // 非 active の Graphic も対象にする (「隠されている」と
            // 「描かれていない」を混同しないため)。
            var overlayGraphics = legacyOverlay.GetComponentsInChildren<UnityEngine.UI.Graphic>(true);
            for (int i = 0; i < overlayGraphics.Length; i++)
            {
                if (!FlashBeat.UI.V5.GraphicVisibility.IsDrawn(overlayGraphics[i])) continue;
                legacyOverlayDrawn = true;
                Debug.Log("[AutoTestV6] LEGACY OVERLAY DRAWN: " + overlayGraphics[i].gameObject.name);
                break;
            }
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
        yield return CaptureGameplay("04-media-ready");

        if (music != null && music.MediaReady)
        {
            KeyBindings.InjectKeyDown(KeyCode.Space);
            yield return new WaitForSecondsRealtime(1.5f);
            Check("the playback started", music.PlaybackStarted);

            // 曲が終わってから撮ると 05-gameplay が取れない (短い疎な譜面で
            // 密区間の待機中に曲が終わると、シーン確認ごと失敗する)。
            // 先に **再生中** のゲーム画面を撮ってから密区間を探す
            // (PR #263 レビュー)。
            yield return CaptureGameplay("05-gameplay");

            // Issue #233: 譜面が疎な区間 (先頭) で撮るとノートが画面に 1 枚も
            // 出ない。曲に依存しない方法として「実際にカメラに写っている
            // ノーツが 3 枚以上あり、手前 (z <= 5.5) まで入るまで待つ」ので、
            // どの曲でも白い帯 (ノート) が見える状態で撮れる。
            yield return WaitForDenseNotes(3, 30f);
            // 1 枚目のノート数は 2 枚目の比較のために **別に保持** する
            // (上書きすると「2 枚目が 1 枚目より密か」を判定できない)。
            int firstVisibleNotes = lastVisibleNotes;
            float firstNearestZ = lastNearestZ;
            float farthestZ = lastFarthestZ;
            Check("the media clock advances", music.MediaTimeNow > 0f);
            Debug.Log("[AutoTestV6] media time at capture = " + music.MediaTimeNow.ToString("F2")
                + " visible notes = " + firstVisibleNotes
                + " z range = " + firstNearestZ.ToString("F2") + " .. " + farthestZ.ToString("F2"));
            // 密区間に入るか否かは **譜面に依存する** ので、失敗にはしない
            // (PR #263 レビュー S5: 曲によって必ず失敗する断言は自動テストの
            // 信頼性を下げる)。soft な記録として残す。
            CheckSoft("the capture lands in a dense chart region (soft)", firstVisibleNotes >= 3);

            // 密区間に到達した最初の区間も撮る (Issue #233 の「白い帯が
            // 並んでいる」証拠)。撮れなくても曲子には影響しない。
            yield return CaptureGameplay("05a-gameplay-dense");

            // 5b. さらに **別の** 密区間まで進めて撮る (Issue #233)。
            // 1 枚目では手前のノートが HUD の歌詞パネルに隠れるため、
            // 「譜面の密区間にノートが並んでいる」ことが 2 枚目で確認できる。
            // 同じ区間だと同じ絵になるので、1 枚目より **ノート数が多い** 区間を待つ。
            // 待ちは上限つきの **短時間** (S5: 無制限に待たない)。
            yield return WaitForDenseNotes(firstVisibleNotes + 1, 15f);
            int secondVisibleNotes = lastVisibleNotes;
            float secondNearestZ = lastNearestZ;
            float secondFarthestZ = lastFarthestZ;
            Debug.Log("[AutoTestV6] media time at 2nd capture = " + music.MediaTimeNow.ToString("F2")
                + " visible notes = " + secondVisibleNotes
                + " z range = " + secondNearestZ.ToString("F2") + " .. " + secondFarthestZ.ToString("F2"));
            // 2 枚目の撮影条件は「1 枚目よりノートが多い」ことと
            // 「手前 (z <= 5.5) まで入ること」。ただし **どちらも譜面依存** なので
            // soft 判定にする (S5)。画像が撮れていても条件を満たさないことは
            // ログで分かるようにしておく。
            CheckSoft("the 2nd capture has more notes than the 1st (soft)",
                secondVisibleNotes > firstVisibleNotes);
            CheckSoft("the 2nd capture reaches the near field (soft)", secondNearestZ <= 5.5f);
            yield return CaptureGameplay("05b-gameplay-dense");
        }

        Finish();
    }

    /// <summary>
    /// 実際にカメラに写っているノーツが <paramref name="minNotes"/> 枚以上
    /// になるまで待つ (Issue #233)。手前 (z &lt;= 5.5) まで进来的ら条件成立。
    /// 見つからなくても <paramref name="maxWait"/> 秒で打ち切る。
    /// 結果は lastVisibleNotes / lastNearestZ / lastFarthestZ に入れて返す
    /// (イテレータは out 引数を持てないため)。
    /// </summary>
    private IEnumerator WaitForDenseNotes(int minNotes, float maxWait)
    {
        float waited = 0f;
        while (waited < maxWait)
        {
            // 曲が終わってシーンが変わったら待たない (S5)。
            if (SceneManager.GetActiveScene().name != "GameScene") break;
            ScanVisibleNotes(out lastVisibleNotes, out lastNearestZ, out lastFarthestZ);
            if (lastVisibleNotes >= minNotes && lastNearestZ <= 5.5f) break;
            waited += 0.25f;
            yield return new WaitForSecondsRealtime(0.25f);
        }
        ScanVisibleNotes(out lastVisibleNotes, out lastNearestZ, out lastFarthestZ);
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

        // 撮影対象はゲーム画面 (MainCamera) なので、可視判定もそのカメラ
        // の視錐台で行う。Renderer.isVisible を使うと、Editor の Scene view
        // カメラに写るゲーム画面外のノーツまで数えてしまう
        // (PR #263 レビュー)。
        Camera camera = Camera.main;
        Plane[] planes = null;
        if (camera != null)
        {
            planes = new Plane[6];
            GeometryUtility.CalculateFrustumPlanes(camera, planes);
        }

        Notes[] notes = FindObjectsByType<Notes>(FindObjectsSortMode.None);
        for (int i = 0; i < notes.Length; i++)
        {
            if (notes[i] == null) continue;
            float z = notes[i].transform.position.z;
            bool visible = false;

            Renderer renderer = notes[i].GetComponentInChildren<Renderer>();
            if (renderer != null && renderer.enabled)
            {
                if (planes != null)
                {
                    visible = GeometryUtility.TestPlanesAABB(planes, renderer.bounds);
                }
                else
                {
                    visible = renderer.isVisible;
                }
            }
            else
            {
                // v6 プレイフィールド (2D) の場合はレンダラではなく
                // Image (Canvas 上の CanvasRenderer) で描かれる。v6 の
                // プレイフィールドが有効なときは Image 側を数える。
                var head = notes[i].HeadImage;
                visible = head != null
                          && head.isActiveAndEnabled
                          && FlashBeat.UI.V5.GraphicVisibility.IsDrawn(head);
            }

            if (!visible) continue;
            count++;
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

    /// <summary>
    /// ゲーム画面のスクリーンショットを撮る前に、**まだ GameScene にいるか**
    /// を確認する (PR #263 レビュー S5)。
    ///
    /// 曲が終わって ResultScene に遷移したあとに撮ると、プレイフィールドの
    /// 絵ではないものを「プレイフィールドの証拠」として残してしまう。
    /// </summary>
    private IEnumerator CaptureGameplay(string name)
    {
        bool inGameScene = SceneManager.GetActiveScene().name == "GameScene";
        Check("the game scene is still active before the capture " + name, inGameScene);
        if (!inGameScene) yield break;
        yield return Screenshot(name);
    }

    /// <summary>
    /// **soft** な検査 (PR #263 レビュー S5)。
    ///
    /// 結果は記録するが、失敗には数えない。曲や譜面に依存する条件
    /// (ノーツの密度など) は、これで自動テスト全体が曲に左右されるのを
    /// 防ぐ。
    /// </summary>
    private void CheckSoft(string label, bool ok)
    {
        if (ok) { Debug.Log("[AutoTestV6] SOFT PASS " + label); }
        else { Debug.LogWarning("[AutoTestV6] SOFT FAIL " + label); }
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
