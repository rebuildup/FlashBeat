using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// v6 全画面キャプチャ + 機械チェック (<see cref="AutoTestV6"/> の兄弟モード)。
///
/// 使い方 (プレイヤー引数):
///   -autotest v6screens -autotest-out &lt;dir&gt;
///   [-autotest-scenes TitleScene,SelectScene,OptionScene,ResultScene,GameScene]
///
/// 指定シーンを順にロードし、各シーンで数秒待機してからスクリーンショット
/// (v6screen-&lt;SceneName&gt;-&lt;n&gt;.png) を撮り、起動中の v6 Canvas を走査して
/// 機械チェック (フレーム収まり / レガシー混在 / Unicode 記号アイコン) を
/// Debug.Log + pass/fail で記録する。結果は &lt;out&gt;/autotest-v6screens-result.txt。
///
/// スクリーンショットは実機解像度で撮ること (2560×1664 フルスクリーン等)。
/// ウィンドウリサイズでの擬似検証は不可。起動側の引数:
///   -screen-fullscreen 1 -screen-width 2560 -screen-height 1664
///
/// <see cref="AutoTestV6"/> (-autotest v6) と同時に成立する (両方とも引数が
/// 自モードで無ければ何もしない)。
///
/// 機械チェックの判定式は純ロジックとして <see cref="V6ScreenAudit"/> に集約し、
/// EditMode テスト (Assets/Game/Tests/V6CanvasTests/V6FrameContainmentTests.cs)
/// で固定する。
/// </summary>
public class AutoTestV6Screens : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (V6ScreenAudit.GetArg("-autotest") != "v6screens") { return; }
        var go = new GameObject("AutoTestV6Screens");
        DontDestroyOnLoad(go);
        go.AddComponent<AutoTestV6Screens>();
    }

    /// <summary>シーン ロード後、描画が落ち着くまでの待ち秒。</summary>
    private const float SettleSeconds = 4f;

    /// <summary>GameScene の v6 HUD が出るまで待つ上限秒。</summary>
    private const float HudWaitSeconds = 10f;

    private string outDir;
    private int shot;
    private int totalPass;
    private int totalFail;
    private int totalSymbols;

    private sealed class ScreenReport
    {
        public string Label;
        public int Pass;
        public int Fail;
        public int Symbols;
        public readonly List<string> Shots = new List<string>();
        public readonly List<string> Notes = new List<string>();
    }

    private IEnumerator Start()
    {
        outDir = V6ScreenAudit.GetArg("-autotest-out");
        if (string.IsNullOrEmpty(outDir)) { outDir = Application.persistentDataPath; }
        try { Directory.CreateDirectory(outDir); }
        catch (Exception) { }

        string[] scenes = SceneList();
        Debug.Log("[AutoTestV6Screens] start scenes=" + string.Join(",", scenes)
            + " out=" + outDir
            + " resolution=" + Screen.width + "x" + Screen.height
            + " fullscreen=" + Screen.fullScreen);

        var reports = new List<ScreenReport>();
        for (int i = 0; i < scenes.Length; i++)
        {
            yield return RunScene(scenes[i].Trim(), reports);
        }

        WriteResult(reports);
        Application.Quit();
    }

    /// <summary>-autotest-scenes のカンマ区切り。無ければ既定の主要シーン一覧。</summary>
    private string[] SceneList()
    {
        string raw = V6ScreenAudit.GetArg("-autotest-scenes");
        if (string.IsNullOrEmpty(raw)) { raw = V6ScreenAudit.DefaultScenes; }
        return raw.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
    }

    private IEnumerator RunScene(string sceneName, List<ScreenReport> reports)
    {
        if (string.IsNullOrEmpty(sceneName)) { yield break; }

        SceneManager.LoadScene(sceneName);
        yield return new WaitForSecondsRealtime(SettleSeconds);
        if (SceneManager.GetActiveScene().name != sceneName)
        {
            Debug.Log("[AutoTestV6Screens] FAIL the scene " + sceneName
                + " is not loaded (active=" + SceneManager.GetActiveScene().name + ")");
            reports.Add(new ScreenReport { Label = sceneName, Fail = 1 });
            totalFail++;
            yield break;
        }

        if (sceneName == "SelectScene")
        {
            // v6 選曲は 1 Canvas に 3 画面 (選曲 / 履歴 / ランキング) を持つ。
            yield return CaptureAndAudit("SelectScene", reports);

            var select = FlashBeat.UI.V6.Select.View.V6SelectView.Current;
            var history = FlashBeat.UI.V6.History.V6HistoryView.Current;
            var ranking = FlashBeat.UI.V6.Ranking.V6RankingView.Current;

            // Issue #374 受入の自動再現:
            //   (a) 再取得 3 連続でスケルトンへ戻らない (点滅しない)
            //   (b) 取得中かつ表示行なし → スケルトン、取得中でも行あり → rows
            //   (c) 空状態・エラー状態 (ヘッダー Status) が出る
            if (select != null)
            {
                var refetch = new ScreenReport { Label = "SelectScene-Refetch" };
                for (int r = 0; r < 3; r++)
                {
                    select.Data.RefreshAsync();
                    yield return new WaitForSecondsRealtime(0.6f);
                    Check(refetch, "refetch #" + (r + 1) + " keeps rows (no skeleton)",
                        select.ListModeForAudit == "rows");
                    yield return Screenshot(refetch, "SelectScene-Refetch" + (r + 1));
                }
                reports.Add(refetch);

                var ds = select.Data as FlashBeat.UI.V6.Select.Data.V6SelectDataSource;
                if (ds != null)
                {
                    string previous = select.Query;

                    // 取得中 + 表示行なし → スケルトン (初回の未取得状態に相当)。
                    // 実フェッチが残っていると完了時に監査フラグを上書きするので、
                    // 先に実フェッチの終了を待つ。
                    float fetchWait = 0f;
                    while (fetchWait < 5f && ds.IsLoading)
                    {
                        fetchWait += Time.unscaledDeltaTime;
                        yield return null;
                    }
                    select.SetQuery("__no_song_matches__");
                    ds.SetAuditCatalogFetching(true);
                    yield return new WaitForSecondsRealtime(0.3f);
                    var skel = new ScreenReport { Label = "SelectScene-Skeleton" };
                    Check(skel, "loading without visible rows shows the skeleton",
                        select.ListModeForAudit == "skeleton");
                    yield return Screenshot(skel, "SelectScene-Skeleton");
                    reports.Add(skel);

                    // 取得中でも表示行があればスケルトンへ戻らない (点滅しない)。
                    select.SetQuery(previous);
                    yield return new WaitForSecondsRealtime(0.2f);
                    var keep = new ScreenReport { Label = "SelectScene-LoadingKeepsRows" };
                    Check(keep, "loading with visible rows keeps the rows",
                        select.ListModeForAudit == "rows");
                    yield return Screenshot(keep, "SelectScene-LoadingKeepsRows");
                    reports.Add(keep);

                    ds.SetAuditCatalogFetching(false);
                    yield return new WaitForSecondsRealtime(0.3f);
                }

                // エラー状態: 絞り込みで隠れた選択への CommitReady は理由を
                // StatusMessage (ヘッダーの Status) に出す実経路。
                string savedQuery = select.Query;
                select.SetQuery("__no_song_matches__");
                select.Data.CommitReady();
                yield return new WaitForSecondsRealtime(0.3f);
                var err = new ScreenReport { Label = "SelectScene-Error" };
                Check(err, "a rejected ready surfaces the reason in the header",
                    select.Data.HasError && !string.IsNullOrEmpty(select.Data.StatusMessage));
                yield return Screenshot(err, "SelectScene-Error");
                reports.Add(err);

                select.SetQuery(savedQuery);
                select.Data.RefreshAsync(); // hasError を解除する経路 (取得中表示へ)
                // 取得の完了まで待つ。失敗時は HasError が正しく残る
                // (エラー表示の維持そのものが仕様) ので、時間で打ち切る。
                float clearWaited = 0f;
                while (clearWaited < 8f && select.Data.HasError)
                {
                    clearWaited += Time.unscaledDeltaTime;
                    yield return null;
                }
                yield return new WaitForSecondsRealtime(0.3f);
            }

            // design AqFtx (02b 該当なし): 検索ヒット 0 件の空状態を実機で撮る。
            if (select != null)
            {
                string previous = select.Query;
                select.SetQuery("__no_song_matches__");
                yield return new WaitForSecondsRealtime(1f);
                yield return CaptureAndAudit("SelectScene-Empty", reports);
                select.SetQuery(previous);
                yield return new WaitForSecondsRealtime(1f);
            }

            // design wSjAm (02c Select · 設定): 右カラムを「このプレイの設定」へ
            // 差し替えた状態も撮る (実機は O / 設定ボタンで開く)。
            if (select != null)
            {
                select.OpenQuickSettings();
                yield return new WaitForSecondsRealtime(1f);
                yield return CaptureAndAudit("SelectScene-Settings", reports);
                select.CloseQuickSettings();
                yield return new WaitForSecondsRealtime(1f);
            }

            if (select != null && history != null && ranking != null)
            {
                select.SetVisible(false);
                history.SetVisible(true);
                yield return new WaitForSecondsRealtime(1f);
                yield return CaptureAndAudit("SelectScene-History", reports);
                history.SetVisible(false);

                ranking.SetVisible(true);
                yield return new WaitForSecondsRealtime(1f);
                yield return CaptureAndAudit("SelectScene-Ranking", reports);
                ranking.SetVisible(false);

                select.SetVisible(true);
            }
            else
            {
                var report = new ScreenReport { Label = "SelectScene-SubScreens" };
                Check(report, "the v6 history and ranking views exist", history != null && ranking != null);
                reports.Add(report);
            }

            yield break;
        }

        if (sceneName == "ResultScene")
        {
            // 段階表示 (#384) は最終状態にし、2000ms で自動で開く登録モーダルは
            // 抑止する (ResultScene 本体とモーダルの 2 枚を別々に撮るため)。
            var driver = FindFirstObjectByType<FlashBeat.UI.V6.Result.V6ResultViewDriver>();
            var resultView = driver != null ? driver.View : null;
            if (resultView != null)
            {
                resultView.PrepareForAudit();
                yield return new WaitForSecondsRealtime(0.2f);
            }

            yield return CaptureAndAudit("ResultScene", reports);

            // design lqHaN (05b 登録モーダル): 実機ではフッターの Register で開く。
            // 自動撮影は入力できないため、監査用 API で開いて撮る。
            if (resultView != null)
            {
                resultView.OpenRegisterModalForAudit();
                yield return new WaitForSecondsRealtime(1f);
                yield return CaptureAndAudit("ResultScene-RegisterModal", reports);
                resultView.CloseRegisterModalForAudit();
                yield return new WaitForSecondsRealtime(0.5f);
            }
            else
            {
                var report = new ScreenReport { Label = "ResultScene-RegisterModal" };
                Check(report, "the v6 result view exists", false);
                reports.Add(report);
            }

            yield break;
        }

        if (sceneName == "GameScene")
        {
            float waited = 0f;
            while (waited < HudWaitSeconds && FindFirstObjectByType<FlashBeat.UI.V6.Game.Hud.V6GameHudView>() == null)
            {
                waited += 0.5f;
                yield return new WaitForSecondsRealtime(0.5f);
            }

            var hudReport = new ScreenReport { Label = "GameScene-HUD" };
            Check(hudReport, "the v6 hud exists", FindFirstObjectByType<FlashBeat.UI.V6.Game.Hud.V6GameHudView>() != null);
            Audit(hudReport);
            yield return Screenshot(hudReport, "GameScene-HUD");
            reports.Add(hudReport);

            var pause = FlashBeat.UI.V6.Game.Pause.V6PauseView.Current;
            if (pause != null)
            {
                pause.Open();
                yield return new WaitForSecondsRealtime(0.5f);
                yield return CaptureAndAudit("GameScene-Pause", reports);
                pause.Close();
            }
            else
            {
                var report = new ScreenReport { Label = "GameScene-Pause" };
                Check(report, "the v6 pause view exists", false);
                reports.Add(report);
            }

            // design g4Q3F (曲ローディング)。通常は phase が表示を決めるため
            // 自動撮影中は数秒で隠れる。検証用 API で固定表示して撮る
            // (正本と同じ曲 5 = 終焉逃避行 / 柊マグネタイト、進捗 42%)。
            var runtime = FindFirstObjectByType<FlashBeat.UI.V6.V6GameRuntime>();
            if (runtime != null)
            {
                runtime.ShowLoadingForAudit(5, 0.42f);

                // Issue #378: ローディング画面の背景は loading.mp4 の映像。
                // VideoPlayer の prepare 完了を待ち、映像面 (RawImage) が
                // 実際に有効になったことを確認してから撮る。
                var loadingView = FlashBeat.UI.V6.Game.Loading.V6LoadingView.Current;
                float videoWaited = 0f;
                while (videoWaited < 15f
                    && loadingView != null
                    && !(loadingView.VideoPreparedForAudit && loadingView.VideoVisibleForAudit))
                {
                    videoWaited += Time.unscaledDeltaTime;
                    yield return null;
                }
                var videoReport = new ScreenReport { Label = "GameScene-LoadingVideo" };
                Check(videoReport, "the loading video prepares and becomes visible",
                    loadingView != null && loadingView.VideoPreparedForAudit && loadingView.VideoVisibleForAudit);
                Check(videoReport, "the loading surface is the video (not the flat fallback)",
                    loadingView != null && loadingView.VideoSurfaceForAudit == "Video");
                reports.Add(videoReport);

                yield return new WaitForSecondsRealtime(0.8f);
                yield return CaptureAndAudit("GameScene-Loading", reports);
                runtime.HideLoadingForAudit();
                yield return new WaitForSecondsRealtime(0.3f);
            }
            else
            {
                var report = new ScreenReport { Label = "GameScene-Loading" };
                Check(report, "the v6 game runtime exists", false);
                reports.Add(report);
            }

            yield break;
        }

        if (sceneName == "OptionScene")
        {
            // design B6If6Z / EzLfb / z7joSy / DQHHp: Option は 4 タブあり、
            // タブごとに設定行と説明文が違う。実機は Tab / クリックで切り替えるが
            // 自動撮影は入力できないため、監査用 API で全タブを撮る。
            yield return CaptureAndAudit("OptionScene", reports);

            var option = FlashBeat.UI.V6.Option.V6OptionView.Current;
            if (option == null)
            {
                var report = new ScreenReport { Label = "OptionScene-Tabs" };
                Check(report, "the v6 option view exists", false);
                reports.Add(report);
            }
            else
            {
                for (int tab = 1; tab < FlashBeat.UI.V6.Option.V6OptionLogic.TabCount; tab++)
                {
                    option.SelectTabForAudit(tab);
                    yield return new WaitForSecondsRealtime(0.6f);
                    yield return CaptureAndAudit(
                        "OptionScene-" + FlashBeat.UI.V6.Option.V6OptionLogic.TabLabels[tab], reports);
                }

                option.SelectTabForAudit(0);
                yield return new WaitForSecondsRealtime(0.4f);
            }

            yield break;
        }

        yield return CaptureAndAudit(sceneName, reports);
    }

    private IEnumerator CaptureAndAudit(string label, List<ScreenReport> reports)
    {
        var report = new ScreenReport { Label = label };
        Audit(report);
        yield return Screenshot(report, label);
        reports.Add(report);
        Debug.Log("[AutoTestV6Screens] SCREEN " + label
            + " pass=" + report.Pass + " fail=" + report.Fail
            + " symbols=" + report.Symbols
            + " res=" + Screen.width + "x" + Screen.height);
    }

    /// <summary>起動中の v6 Canvas を走査して機械チェックを記録する。</summary>
    private void Audit(ScreenReport report)
    {
        NoteDuplicateCanvases(report);
        AuditFrameContainment(report);
        AuditLegacy(report);
        AuditUnicodeSymbols(report);
        AuditScreenClip(report);
        AuditTextInk(report);
        AuditElementOverlap(report);
    }

    /// <summary>
    /// チェック 0 (観測): 同名の v6 Canvas が同一シーンに複数ある状態を記録する。
    /// 「旧世代 + 新世代の Canvas が同時に存在して pass 数が倍増する」既知 flake
    /// (Issue #850) の兆候。再構築は <see cref="FlashBeat.UI.V6.Common.V6Hierarchy"/>
    /// の DetachAndDestroy (階層から外してから破棄予約) を通す契約だが、
    /// 契約外の経路で二重化した場合に結果ファイルから追えるようにする。
    /// fail にはしない (正当な複数 Canvas の可能性を排除できないため)。
    /// </summary>
    private void NoteDuplicateCanvases(ScreenReport report)
    {
        var canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        var counts = new Dictionary<string, int>();
        var ids = new Dictionary<string, List<string>>();
        for (int i = 0; i < canvases.Length; i++)
        {
            var canvas = canvases[i];
            if (!V6ScreenAudit.IsV6Canvas(canvas) || !canvas.gameObject.activeInHierarchy) { continue; }
            string key = canvas.gameObject.scene.handle + "|" + canvas.name;
            if (!counts.TryGetValue(key, out int n)) { n = 0; }
            counts[key] = n + 1;
            if (!ids.TryGetValue(key, out var list))
            {
                list = new List<string>();
                ids[key] = list;
            }
            list.Add(canvas.GetEntityId().ToString());
        }

        foreach (var kv in counts)
        {
            if (kv.Value <= 1) { continue; }
            string note = "[audit] duplicate v6 canvas name=" + kv.Key.Split('|')[1]
                + " scene=" + kv.Key.Split('|')[0]
                + " count=" + kv.Value
                + " ids=" + string.Join(",", ids[kv.Key])
                + " — 2 世代の Canvas が同時に audit され pass が倍増しうる";
            report.Notes.Add(note);
            Debug.LogWarning("[AutoTestV6Screens] " + note);
        }
    }

    /// <summary>
    /// チェック 1: design フレーム外にはみ出す要素がない。
    /// v6 共通フレーム 1728×972 (HUD / Dim は画面全体) の矩形内に、
    /// 表示中の全 Graphic のワールド矩形が収まるか (許容誤差 1px)。
    /// スクロール親 (Mask / RectMask2D / Scroll・List・Viewport・Rows 名) の
    /// 矩形内に収まる要素は「スクロール領域内」として許容する。
    /// </summary>
    private void AuditFrameContainment(ScreenReport report)
    {
        var canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        bool sawGraphics = false;
        for (int i = 0; i < canvases.Length; i++)
        {
            var canvas = canvases[i];
            if (!V6ScreenAudit.IsV6Canvas(canvas)) { continue; }

            // 非表示 (ルートが inactive) の v6 Canvas は「その画面に居ない」ので判定しない。
            // 従来は inactive な Pause の Canvas を毎レポート走査し、フレーム節が無いと
            // FAIL にしていた (HUD レポートに Pause の FAIL が混ざっていた)。
            // 同じ理由で「active だが子がすべて inactive」の Canvas (閉じている Pause など)
            // も判定しない。ただし **1 枚も描画物が無い画面** は UI 生成の失敗を PASS で
            // 隠してしまうため、ループの後で FAIL にする (監査の pass 数を実際より良く出さない)。
            var graphics = canvas.GetComponentsInChildren<Graphic>(false);
            if (graphics.Length == 0)
            {
                continue;
            }

            sawGraphics = true;

            Rect outer;
            if (V6ScreenAudit.IsFullScreenCanvas(canvas))
            {
                // HUD は解像度別固有フレーム (1920×1080 / 2560×1440 / 2560×1664) を
                // 持つので、画面全体を基準に判定する。Video Dim も全画面が正しい。
                outer = new Rect(0f, 0f, Screen.width, Screen.height);
            }
            else
            {
                var frame = V6ScreenAudit.FindFrame(canvas);
                if (frame != null)
                {
                    // 実フレーム節のワールド矩形 (DesignScale 適用後の実ピクセル)。
                    outer = V6ScreenAudit.WorldRect(frame);
                }
                else
                {
                    // フレーム節が見つからないときの代替: 契約の origin 式
                    // ((w-1728)/2, (h-972)/2 切捨て) で 1728×972 を敷く。
                    var origin = V6ScreenAudit.FrameOrigin(Screen.width, Screen.height);
                    outer = new Rect(origin.x, origin.y, V6ScreenAudit.DesignW, V6ScreenAudit.DesignH);
                    Check(report, "the v6 frame node exists on " + canvas.name, false);
                }
            }

            for (int g = 0; g < graphics.Length; g++)
            {
                var graphic = graphics[g];
                if (graphic.color.a <= 0.001f) { continue; } // 不可視要素は判定外
                if (graphic.name.StartsWith("TMP SubMesh", StringComparison.Ordinal)) { continue; }
                var rt = graphic.rectTransform;
                if (rt == null) { continue; }

                var rect = V6ScreenAudit.WorldRect(rt);

                // 全画面の暗幕 (Pause Dim 等) はフレームより大きいのが設計どおり。
                // 画面全体 (許容誤差つき) を覆う 1 枚だけを正当な背景として許容する。
                var screen = new Rect(0f, 0f, Screen.width, Screen.height);
                if (rect.width >= screen.width - V6ScreenAudit.TolerancePx
                    && rect.height >= screen.height - V6ScreenAudit.TolerancePx
                    && V6ScreenAudit.Contains(rect, screen, V6ScreenAudit.TolerancePx))
                {
                    report.Pass++;
                    continue;
                }

                if (V6ScreenAudit.Contains(outer, rect, V6ScreenAudit.TolerancePx))
                {
                    report.Pass++;
                    continue;
                }

                // スクロール領域の許容: スクロール親の矩形内に収まるなら OUT OF FRAME でも可。
                var scroll = V6ScreenAudit.FindScrollAncestor(rt, canvas.transform);
                var scrollRect = scroll as RectTransform;
                if (scrollRect != null && V6ScreenAudit.Contains(V6ScreenAudit.WorldRect(scrollRect), rect, V6ScreenAudit.TolerancePx))
                {
                    Debug.Log("[AutoTestV6Screens] SCROLL-OK " + V6ScreenAudit.Path(rt)
                        + " is inside the scroll parent " + scroll.name);
                    report.Pass++;
                    continue;
                }

                report.Fail++;
                Debug.Log("[AutoTestV6Screens] FAIL out of frame: " + V6ScreenAudit.Path(rt)
                    + " element=" + rect.ToString("F1")
                    + " frame=" + outer.ToString("F1")
                    + " overshoot=" + V6ScreenAudit.Overshoot(outer, rect).ToString("F1") + "px");
            }
        }

        // この画面に v6 の描画物が 1 枚も無ければ、UI 生成の失敗を「指摘 0 件」で
        // 通してしまう。描画物が 1 枚以上あることを画面ごとに固定する。
        Check(report, "the v6 screen draws at least one graphic: " + report.Label, sawGraphics);
    }

    /// <summary>
    /// チェック 2: レガシー UI が同時に見えていない (AutoTestV6 と同じ判定)。
    /// ただし Ready 中 (再生前) の V5ReadyCanvas は v5 GameReadyView が
    /// 単独所有する設計なので正当な表示。
    /// </summary>
    private void AuditLegacy(ScreenReport report)
    {
        var graphics = FindObjectsByType<Graphic>(FindObjectsSortMode.None);
        int legacyVisible = 0;
        for (int i = 0; i < graphics.Length; i++)
        {
            var g = graphics[i];
            if (g == null || !g.isActiveAndEnabled) { continue; }
            var canvas = g.GetComponentInParent<Canvas>();
            if (canvas == null || V6ScreenAudit.IsV6Canvas(canvas)) { continue; }
            if (canvas.name == "V5ReadyCanvas" && !GManager.played) { continue; }
            legacyVisible++;
            Debug.Log("[AutoTestV6Screens] LEGACY VISIBLE: " + canvas.name + " / " + g.gameObject.name);
        }

        Check(report, "no legacy graphics are visible on " + report.Label, legacyVisible == 0);
    }

    /// <summary>
    /// チェック 3: Unicode 記号アイコンが Text として残っている箇所を列挙する。
    /// 判定はログ出力のみで fail にしない (検出個数を記録する)。
    /// </summary>
    private void AuditUnicodeSymbols(ScreenReport report)
    {
        var canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        int found = 0;
        for (int i = 0; i < canvases.Length; i++)
        {
            var canvas = canvases[i];
            if (!V6ScreenAudit.IsV6Canvas(canvas)) { continue; }

            // 「同じ v6 Canvas が 2 つ監査される」現象 (OptionScene の pass が
            // 181 / 359 で揺れる) を追うための識別情報。id が違えば別インスタンス。
            Debug.Log("[AutoTestV6Screens] AUDIT-CANVAS " + report.Label + " name=" + canvas.name
                + " id=" + canvas.GetEntityId()
                + " scene=" + canvas.gameObject.scene.name
                + " active=" + canvas.gameObject.activeInHierarchy
                + " childCount=" + canvas.transform.childCount
                + " graphics=" + canvas.GetComponentsInChildren<Graphic>(false).Length);

            var tmpTexts = canvas.GetComponentsInChildren<TMP_Text>(false);
            for (int t = 0; t < tmpTexts.Length; t++)
            {
                found += LogSymbolChars(report, tmpTexts[t].transform, tmpTexts[t].text);
            }

            var legacyTexts = canvas.GetComponentsInChildren<Text>(false);
            for (int t = 0; t < legacyTexts.Length; t++)
            {
                found += LogSymbolChars(report, legacyTexts[t].transform, legacyTexts[t].text);
            }
        }

        report.Symbols += found;
        totalSymbols += found;
        Debug.Log("[AutoTestV6Screens] SYMBOLS " + report.Label + " count=" + found + " (log only, not a failure)");
    }

    private int LogSymbolChars(ScreenReport report, Transform node, string text)
    {
        if (string.IsNullOrEmpty(text)) { return 0; }
        var sb = new StringBuilder();
        int count = 0;
        for (int i = 0; i < text.Length; i++)
        {
            if (!V6ScreenAudit.IsUnicodeSymbolChar(text[i])) { continue; }
            count++;
            sb.Append(" U+" + ((int)text[i]).ToString("X4") + "('" + text[i] + "')");
        }

        if (count > 0)
        {
            Debug.Log("[AutoTestV6Screens] SYMBOL-ICON-TEXT " + V6ScreenAudit.Path(node) + ":" + sb);
        }

        return count;
    }

    /// <summary>
    /// 画面端で切れる要素の列挙 (情報のみ、pass/fail にしない)。
    /// 固定フレーム契約 (1728×972 を 1:1、origin は中央寄せ) では、
    /// フレームより小さい解像度 (1024×768 / 1280×720 など) で
    /// 画面外に出る操作・情報が発生しうる。#96 の「画面外に失われる
    /// 情報・操作がないか」の判定材料として、表示中の Selectable
    /// (押せる面) と Text を実画面矩形と照合し、はみ出しを記録する。
    /// Scroll / Mask 内の要素は「スクロール親の矩形 ∩ 実画面」の内側に
    /// 収まる限り対象外 (スクロールで到達できるため)。スクロール親が
    /// 画面外にはみ出す場合は、その交差領域を基準に判定するので、
    /// 縦スクロールリストの左右クリップは引き続き検出される。
    /// </summary>
    private void AuditScreenClip(ScreenReport report)
    {
        var screen = new Rect(0f, 0f, Screen.width, Screen.height);
        var canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        int selPartial = 0;
        int selOutside = 0;
        int textPartial = 0;
        int textOutside = 0;
        int lines = 0;
        const int MaxLines = 80;

        for (int i = 0; i < canvases.Length; i++)
        {
            var canvas = canvases[i];
            if (!V6ScreenAudit.IsV6Canvas(canvas) || !canvas.gameObject.activeInHierarchy) { continue; }

            var selectables = canvas.GetComponentsInChildren<Selectable>(false);
            for (int s = 0; s < selectables.Length; s++)
            {
                var sel = selectables[s];
                if (sel == null || !sel.IsInteractable()) { continue; }
                var rt = sel.transform as RectTransform;
                if (rt == null) { continue; }
                var clip = V6ScreenAudit.ClassifyElementClip(sel.transform, canvas.transform, rt, screen);
                if (clip == V6ScreenAudit.ScreenClip.Inside) { continue; }
                if (clip == V6ScreenAudit.ScreenClip.Outside) { selOutside++; } else { selPartial++; }
                if (lines < MaxLines)
                {
                    lines++;
                    report.Notes.Add("[clip] selectable " + clip.ToString().ToLowerInvariant()
                        + " " + V6ScreenAudit.Path(sel.transform)
                        + " rect=" + V6ScreenAudit.WorldRect(rt).ToString("F0"));
                }
            }

            AuditClipTexts(canvas, report, screen, ref textPartial, ref textOutside, ref lines, MaxLines);
        }

        report.Notes.Add("[clip] summary screen=" + Screen.width + "x" + Screen.height
            + " selectables partial=" + selPartial + " outside=" + selOutside
            + " texts partial=" + textPartial + " outside=" + textOutside
            + (lines >= MaxLines ? " (list truncated at " + MaxLines + " lines)" : string.Empty));
        Debug.Log("[AutoTestV6Screens] CLIP " + report.Label
            + " selectables partial=" + selPartial + " outside=" + selOutside
            + " texts partial=" + textPartial + " outside=" + textOutside);

        // #523 受入: 画面外に完全に出て押せない/読めない要素は機械的 FAIL。
        // パン/スクロールで到達できる要素は ClassifyElementClip が Inside に分類するので、
        // ここに残る Outside は「どの経路でも到達できない」実欠陥だけ。
        Check(report, "no unreachable selectables on " + report.Label, selOutside == 0);
        Check(report, "no unreachable info texts on " + report.Label, textOutside == 0);
    }

    /// <summary>TMP_Text とレガシー Text の両方を走査してクリップを記録する。</summary>
    private void AuditClipTexts(Canvas canvas, ScreenReport report, Rect screen,
        ref int textPartial, ref int textOutside, ref int lines, int maxLines)
    {
        var tmpTexts = canvas.GetComponentsInChildren<TMP_Text>(false);
        for (int t = 0; t < tmpTexts.Length; t++)
        {
            var tmp = tmpTexts[t];
            NoteClippedText(tmp == null ? null : tmp.transform, tmp == null ? null : tmp.text,
                canvas.transform, screen, report, ref textPartial, ref textOutside, ref lines, maxLines);
        }

        var legacyTexts = canvas.GetComponentsInChildren<Text>(false);
        for (int t = 0; t < legacyTexts.Length; t++)
        {
            var legacy = legacyTexts[t];
            NoteClippedText(legacy == null ? null : legacy.transform, legacy == null ? null : legacy.text,
                canvas.transform, screen, report, ref textPartial, ref textOutside, ref lines, maxLines);
        }
    }

    private void NoteClippedText(Transform node, string text, Transform canvasRoot, Rect screen,
        ScreenReport report, ref int textPartial, ref int textOutside, ref int lines, int maxLines)
    {
        if (node == null) { return; }
        // #553: 非表示 (alpha 0 / 無効) と空テキストは「読めない情報」ではないので
        // textOutside の hard FAIL 対象にしない (AuditFrameContainment と同じ除外)。
        var graphic = node.GetComponent<Graphic>();
        if (graphic == null || graphic.color.a <= 0.001f || !graphic.isActiveAndEnabled) { return; }
        if (string.IsNullOrWhiteSpace(text)) { return; }
        var rt = node as RectTransform;
        if (rt == null) { return; }
        var clip = V6ScreenAudit.ClassifyElementClip(node, canvasRoot, rt, screen);
        if (clip == V6ScreenAudit.ScreenClip.Inside) { return; }
        if (clip == V6ScreenAudit.ScreenClip.Outside) { textOutside++; } else { textPartial++; }
        if (lines >= maxLines) { return; }
        lines++;
        var snippet = text ?? string.Empty;
        if (snippet.Length > 24) { snippet = snippet.Substring(0, 24); }
        report.Notes.Add("[clip] text " + clip.ToString().ToLowerInvariant()
            + " " + V6ScreenAudit.Path(node)
            + " rect=" + V6ScreenAudit.WorldRect(rt).ToString("F0")
            + " \"" + snippet + "\"");
    }

    /// <summary>
    /// チェック 5 (Issue #850): TMP テキストのインク (実メッシュ頂点) が
    /// 割当 RectTransform の内側に収まるか。
    ///
    /// RectTransform はフレーム内でもグリフがはみ出すため、矩形ベースの
    /// 既存チェックでは「ボタンが内容より小さい」「テキストが領域外へ出る」
    /// を検出できない。ここでは <see cref="V6ScreenAudit.TryInkLocalRect"/> で
    /// 実インク外接矩形を測り、<see cref="V6ScreenAudit.ClassifyInkFit"/> で
    /// Inside / Partial / Outside に分類する。
    ///
    /// 判定方針 (画面クリップ監査と同じ):
    ///   Outside (インクが割当と交差しない) → FAIL。
    ///   Partial (端をまたぐ = はみ出し) → 記録のみ。誤検出の有無を
    ///   実機ベースラインで確認してから FAIL 化を判断する。
    ///
    /// 対象外: 不可視・空テキスト・Masking/ScrollRect モード
    /// (仕様上インクを矩形で切るため溢れが描画されない)。
    /// Ellipsis / AutoFit は収まる設計なのでそのまま計測する
    /// (収まっていなければそれ自体が欠陥)。
    /// </summary>
    private void AuditTextInk(ScreenReport report)
    {
        var canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        int inside = 0;
        int partial = 0;
        int outside = 0;
        int unmeasurable = 0;
        int lines = 0;
        const int MaxLines = 80;

        for (int i = 0; i < canvases.Length; i++)
        {
            var canvas = canvases[i];
            if (!V6ScreenAudit.IsV6Canvas(canvas) || !canvas.gameObject.activeInHierarchy) { continue; }

            var tmpTexts = canvas.GetComponentsInChildren<TMP_Text>(false);
            for (int t = 0; t < tmpTexts.Length; t++)
            {
                var tmp = tmpTexts[t];
                if (tmp == null || !tmp.isActiveAndEnabled) { continue; }
                if (tmp.color.a <= 0.001f) { continue; }
                if (string.IsNullOrWhiteSpace(tmp.text)) { continue; }
                // Masking / ScrollRect は仕様上インクを矩形で切るので溢れは描画されない。
                if (tmp.overflowMode == TextOverflowModes.Masking
                    || tmp.overflowMode == TextOverflowModes.ScrollRect) { continue; }
                var rt = tmp.rectTransform;
                if (rt == null) { continue; }
                var element = rt.rect;
                if (element.width <= 0f || element.height <= 0f) { unmeasurable++; continue; }
                if (!V6ScreenAudit.TryInkLocalRect(tmp, out var ink)) { unmeasurable++; continue; }

                var fit = V6ScreenAudit.ClassifyInkFit(ink, element);
                if (fit == V6ScreenAudit.ScreenClip.Inside)
                {
                    inside++;
                    continue;
                }

                if (fit == V6ScreenAudit.ScreenClip.Outside) { outside++; } else { partial++; }
                if (lines < MaxLines)
                {
                    lines++;
                    var snippet = tmp.text ?? string.Empty;
                    if (snippet.Length > 24) { snippet = snippet.Substring(0, 24); }
                    report.Notes.Add("[ink] text " + fit.ToString().ToLowerInvariant()
                        + " " + V6ScreenAudit.Path(tmp.transform)
                        + " ink=" + ink.ToString("F1")
                        + " rect=" + element.ToString("F1")
                        + " \"" + snippet + "\"");
                }
            }
        }

        report.Notes.Add("[ink] summary inside=" + inside + " partial=" + partial
            + " outside=" + outside + " unmeasurable=" + unmeasurable
            + (lines >= MaxLines ? " (list truncated at " + MaxLines + " lines)" : string.Empty));
        Debug.Log("[AutoTestV6Screens] INK " + report.Label
            + " inside=" + inside + " partial=" + partial
            + " outside=" + outside + " unmeasurable=" + unmeasurable);

        // インクが割当矩形と完全に交差しない = 配置自体の破綻。FAIL。
        // Partial (はみ出し) は実機ベースライン確認まで記録扱い。
        Check(report, "no text ink fully outside its rect on " + report.Label, outside == 0);
    }

    /// <summary>
    /// チェック 6 (Issue #850): 同じ親を持つ操作要素 (Selectable) 同士が
    /// 意味を持って重なっていないか。
    ///
    /// 「ボタンが内容や他ボタンと重なる」破綻クラスの検出。保守的な条件:
    ///   - 同一親の直接の子同士のみ (親子・従属関係の Selectable は対象外)
    ///   - IsInteractable() のものだけ
    ///   - 交差が両軸で許容誤差を超えるものだけ (端数の擦り合わせは除外)
    ///   - 矩形が退化 (幅/高さ 0) しているものは除外
    ///
    /// テキスト同士・テキストと Selectable の重なりは誤検出が多いため
    /// 対象外 (検出不能として文書化)。判定は記録のみで fail にしない
    /// — 実機ベースラインでノイズを確認してから FAIL 化を判断する。
    /// </summary>
    private void AuditElementOverlap(ScreenReport report)
    {
        var canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        int groups = 0;
        int overlaps = 0;
        int lines = 0;
        const int MaxLines = 80;

        for (int i = 0; i < canvases.Length; i++)
        {
            var canvas = canvases[i];
            if (!V6ScreenAudit.IsV6Canvas(canvas) || !canvas.gameObject.activeInHierarchy) { continue; }

            var selectables = canvas.GetComponentsInChildren<Selectable>(false);
            var byParent = new Dictionary<Transform, List<Selectable>>();
            for (int s = 0; s < selectables.Length; s++)
            {
                var sel = selectables[s];
                if (sel == null || !sel.IsInteractable()) { continue; }
                var rt = sel.transform as RectTransform;
                if (rt == null) { continue; }
                var parent = sel.transform.parent;
                if (parent == null) { continue; }
                var rect = V6ScreenAudit.WorldRect(rt);
                if (rect.width <= V6ScreenAudit.TolerancePx || rect.height <= V6ScreenAudit.TolerancePx) { continue; }
                if (!byParent.TryGetValue(parent, out var list))
                {
                    list = new List<Selectable>();
                    byParent[parent] = list;
                }
                list.Add(sel);
            }

            foreach (var kv in byParent)
            {
                var list = kv.Value;
                if (list.Count < 2) { continue; }
                groups++;
                for (int a = 0; a < list.Count; a++)
                {
                    for (int b = a + 1; b < list.Count; b++)
                    {
                        var ra = list[a].transform as RectTransform;
                        var rb = list[b].transform as RectTransform;
                        if (ra == null || rb == null) { continue; }
                        if (!V6ScreenAudit.OverlapsBeyond(
                                V6ScreenAudit.WorldRect(ra), V6ScreenAudit.WorldRect(rb),
                                V6ScreenAudit.TolerancePx)) { continue; }
                        overlaps++;
                        if (lines < MaxLines)
                        {
                            lines++;
                            report.Notes.Add("[overlap] siblings "
                                + V6ScreenAudit.Path(list[a].transform) + " <-> "
                                + V6ScreenAudit.Path(list[b].transform)
                                + " a=" + V6ScreenAudit.WorldRect(ra).ToString("F0")
                                + " b=" + V6ScreenAudit.WorldRect(rb).ToString("F0"));
                        }
                    }
                }
            }
        }

        report.Notes.Add("[overlap] summary groups=" + groups + " overlaps=" + overlaps
            + (lines >= MaxLines ? " (list truncated at " + MaxLines + " lines)" : string.Empty));
        Debug.Log("[AutoTestV6Screens] OVERLAP " + report.Label
            + " groups=" + groups + " overlaps=" + overlaps);
    }

    private IEnumerator Screenshot(ScreenReport report, string label)
    {
        yield return new WaitForEndOfFrame();
        shot++;
        string path = Path.Combine(outDir, "v6screen-" + label + "-" + shot + ".png");
        // 決定的ファイル名なので、出力 dir の再利用で前回の残りを
        // 撮影成功と誤認しうる。撮影前に消す (#563)。
        try { if (File.Exists(path)) { File.Delete(path); } } catch (Exception) { }
        ScreenCapture.CaptureScreenshot(path);
        // 書き込みは非同期。負荷の高い実機では 0.6 秒を超えることがあるので
        // 上限付きで存在を待つ (Issue #374: 連続撮影で FAIL しないように)。
        float waited = 0f;
        bool ok = false;
        while (waited < 5f)
        {
            yield return new WaitForSecondsRealtime(0.2f);
            waited += 0.2f;
            if (File.Exists(path)) { ok = true; break; }
        }
        Check(report, "the screenshot " + label + " was written", ok);
        if (ok) { report.Shots.Add(Path.GetFileName(path)); }
    }

    private void Check(ScreenReport report, string label, bool ok)
    {
        if (ok)
        {
            report.Pass++;
            totalPass++;
            Debug.Log("[AutoTestV6Screens] PASS " + label);
        }
        else
        {
            report.Fail++;
            totalFail++;
            Debug.Log("[AutoTestV6Screens] FAIL " + label);
        }
    }

    private void WriteResult(List<ScreenReport> reports)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < reports.Count; i++)
        {
            var r = reports[i];
            string line = "[AutoTestV6Screens] SCREEN " + r.Label
                + " pass=" + r.Pass + " fail=" + r.Fail
                + " symbols=" + r.Symbols
                + " shots=" + string.Join(",", r.Shots);
            sb.AppendLine(line);
            Debug.Log(line);
            for (int n = 0; n < r.Notes.Count; n++)
            {
                sb.AppendLine("[AutoTestV6Screens] " + r.Notes[n]);
            }
        }

        string result = "[AutoTestV6Screens] RESULT pass=" + totalPass + " fail=" + totalFail
            + " symbols=" + totalSymbols
            + " screenshots=" + shot
            + " resolution=" + Screen.width + "x" + Screen.height;
        sb.AppendLine(result);
        Debug.Log(result);

        try
        {
            File.WriteAllText(Path.Combine(outDir, "autotest-v6screens-result.txt"), sb.ToString());
        }
        catch (Exception e)
        {
            Debug.LogWarning("[AutoTestV6Screens] could not write the result file: " + e.Message);
        }
    }
}

/// <summary>
/// v6 画面キャプチャの機械チェック判定式 (純ロジック、EditMode テスト対象)。
/// RectTransform を触る補助 (WorldRect / FindFrame / FindScrollAncestor) も
/// ここに置くが、判定そのものは Rect / char のみで決まるのでテストで固定できる。
/// </summary>
public static class V6ScreenAudit
{
    /// <summary>v6 共通フレームの design サイズ (契約 §4)。</summary>
    public const float DesignW = 1728f;
    public const float DesignH = 972f;

    /// <summary>フレーム収まり判定の許容誤差 (px)。</summary>
    public const float TolerancePx = 1f;

    /// <summary>実画面に対する要素の収まり分類 (情報記録用、fail にはしない)。</summary>
    public enum ScreenClip
    {
        /// <summary>画面内に完全に収まる。</summary>
        Inside,
        /// <summary>画面端で一部が切れる (残りは見えて押せる可能性がある)。</summary>
        Partial,
        /// <summary>画面外に完全に出ている (見えず押せない)。</summary>
        Outside,
    }

    /// <summary>
    /// 要素矩形が基準矩形 (通常は実画面、スクロール親がある要素は
    /// スクロール親 ∩ 実画面) に対して Inside / Partial / Outside のどれか。
    /// 基準矩形をはみ出して囲むだけの要素 (Dim 等) は Partial になる。
    /// </summary>
    public static ScreenClip ClassifyScreenClip(Rect element, Rect bounds)
    {
        if (Contains(bounds, element, TolerancePx)) { return ScreenClip.Inside; }
        bool outside = element.xMax <= bounds.xMin + TolerancePx
            || element.xMin >= bounds.xMax - TolerancePx
            || element.yMax <= bounds.yMin + TolerancePx
            || element.yMin >= bounds.yMax - TolerancePx;
        return outside ? ScreenClip.Outside : ScreenClip.Partial;
    }

    /// <summary>
    /// 1 要素の画面クリップ種別。スクロール親がある場合は
    /// 「スクロール親のワールド矩形 ∩ 実画面」を基準矩形にする
    /// (スクロールで画面内へ到達できる範囲を到達可能とみなす)。
    /// コンパクト画面のパンビューポート
    /// (<see cref="FlashBeat.UI.V6.Common.V6FramePan"/>) 配下の要素は、
    /// コンテンツ (フレーム) 全域がドラッグ/ホイールで到達可能なので
    /// 基準をコンテンツ矩形とし、画面外にあっても Inside (到達可能) とする (#96/#523)。
    ///
    /// #564: 名前一致だけのスクロール容器 (v6 の "Rows" / "Song List" 等) は
    /// Mask / RectMask2D / ScrollRect を持たず何もクリップしない。クリップしない
    /// 容器の矩形を基準にすると、パネル外でも画面内に見えている要素を
    /// Outside (到達不能) と誤判定する。実際にクリップする容器だけを基準にし、
    /// クリップ容器の外は「スクロール可能な軸に沿ったはみ出し」だけを
    /// 到達可能 (Inside) とみなす (v6 のカスタムスクロールは縦のみ)。
    /// </summary>
    public static ScreenClip ClassifyElementClip(
        Transform node, Transform canvasRoot, RectTransform rt, Rect screen)
    {
        // パンビューポートは最も外側の到達機構なので、内側のスクロール名を持つ
        // 節 ("Song List" / "Key List" 等) より先に判定する。v6 フレーム配下に
        // 内側マスクは無く、コンテンツ (フレーム) 全域がパンで到達可能。
        var pan = FindPanAncestor(node, canvasRoot);
        if (pan != null && pan.Content != null)
        {
            return ClassifyScreenClip(WorldRect(rt), WorldRect(pan.Content));
        }

        var element = WorldRect(rt);
        var scroll = FindScrollAncestor(node, canvasRoot) as RectTransform;
        if (scroll == null || !HasClipComponent(scroll))
        {
            // スクロール親が無い、またはクリップしない容器: 見える範囲は実画面。
            return ClassifyScreenClip(element, screen);
        }

        var sr = WorldRect(scroll);
        float x0 = Mathf.Max(sr.xMin, screen.xMin);
        float y0 = Mathf.Max(sr.yMin, screen.yMin);
        float x1 = Mathf.Min(sr.xMax, screen.xMax);
        float y1 = Mathf.Min(sr.yMax, screen.yMax);
        var basis = x1 > x0 && y1 > y0
            ? Rect.MinMaxRect(x0, y0, x1, y1)
            : screen;

        var clip = ClassifyScreenClip(element, basis);
        if (clip != ScreenClip.Outside)
        {
            return clip;
        }

        // クリップ容器の完全な外: スクロールで到達できる軸のはみ出しだけを
        // 到達可能とみなす。スクロール不能な軸のはみ出しは Outside のまま。
        return IsScrollReachable(scroll, node, canvasRoot, element, basis)
            ? ScreenClip.Inside
            : ScreenClip.Outside;
    }

    /// <summary>スクロール容器が実際に子をクリップするか (Mask / RectMask2D / ScrollRect)。</summary>
    public static bool HasClipComponent(Transform node)
    {
        return node != null
            && (node.GetComponent<Mask>() != null
                || node.GetComponent<RectMask2D>() != null
                || node.GetComponent<ScrollRect>() != null);
    }

    /// <summary>
    /// クリップ容器の外にある要素がスクロールで到達可能か。
    /// 完全に外れている軸がすべてスクロール可能で、かつ ScrollRect の場合は
    /// スクロールコンテンツの範囲内にあること。v6 のカスタムスクロール
    /// (IScrollHandler のキャッチャ、ホイール/ボタン送り) は縦のみ。
    /// </summary>
    public static bool IsScrollReachable(
        RectTransform scroll, Transform node, Transform canvasRoot, Rect element, Rect basis)
    {
        var scrollRect = scroll.GetComponent<ScrollRect>();
        bool canX;
        bool canY;
        if (scrollRect != null)
        {
            canX = scrollRect.horizontal;
            canY = scrollRect.vertical;
        }
        else
        {
            canX = false;
            canY = HasScrollHandlerInChain(node, canvasRoot);
        }

        bool outX = element.xMax <= basis.xMin + TolerancePx
            || element.xMin >= basis.xMax - TolerancePx;
        bool outY = element.yMax <= basis.yMin + TolerancePx
            || element.yMin >= basis.yMax - TolerancePx;
        if ((!outX && !outY) || (outX && !canX) || (outY && !canY))
        {
            return false;
        }

        // ScrollRect はコンテンツ範囲の外まではスクロールで届かない。
        if (scrollRect != null && scrollRect.content != null)
        {
            var content = WorldRect(scrollRect.content);
            if (outY && (element.yMax <= content.yMin + TolerancePx
                || element.yMin >= content.yMax - TolerancePx))
            {
                return false;
            }
            if (outX && (element.xMax <= content.xMin + TolerancePx
                || element.xMin >= content.xMax - TolerancePx))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>node から canvasRoot までの親鎖に IScrollHandler 実装があるか。</summary>
    public static bool HasScrollHandlerInChain(Transform node, Transform root)
    {
        var t = node == null ? null : node.parent;
        while (t != null && t != root)
        {
            var behaviours = t.GetComponents<MonoBehaviour>();
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is IScrollHandler) { return true; }
            }
            t = t.parent;
        }

        return false;
    }

    /// <summary>
    /// 2 矩形の交差領域。交差しないときは幅・高さ 0 以下の Rect を返す
    /// (<see cref="Rect"/> の既定値ではなく明示的に空を返す)。
    /// </summary>
    public static Rect Intersection(Rect a, Rect b)
    {
        float x0 = Mathf.Max(a.xMin, b.xMin);
        float y0 = Mathf.Max(a.yMin, b.yMin);
        float x1 = Mathf.Min(a.xMax, b.xMax);
        float y1 = Mathf.Min(a.yMax, b.yMax);
        if (x1 <= x0 || y1 <= y0)
        {
            return new Rect(0f, 0f, 0f, 0f);
        }

        return Rect.MinMaxRect(x0, y0, x1, y1);
    }

    /// <summary>
    /// 2 矩形が許容誤差を「両軸で」超えて交差するか。
    /// 端数の 1px 未満の擦り合わせ (レイアウト丸め) は重なりとみなさない。
    /// </summary>
    public static bool OverlapsBeyond(Rect a, Rect b, float tolerance)
    {
        var i = Intersection(a, b);
        return i.width > tolerance && i.height > tolerance;
    }

    /// <summary>
    /// TMP テキストのインク (実際にメッシュへ焼かれるグリフ頂点) の外接矩形を
    /// テキスト自身のローカル座標で返す (Issue #850)。
    ///
    /// RectTransform.rect は「割当てられた領域」であり、グリフがその外へ
    /// はみ出しても矩形は変わらない。テキスト溢れの実害 (ボタンが内容より
    /// 小さい / テキストが領域外へ出る) を検出するには実測が必要なので、
    /// <see cref="TMP_Text.ForceMeshUpdate"/> でメッシュを確定させ、
    /// <c>textInfo.meshInfo[].vertices</c> (ローカル座標) の外接矩形を取る。
    /// V6TextFitTests の InkSize と同じ計測で、こちらは位置も返す
    /// (ローカル空間なので Canvas のスケール・解像度に依存しない)。
    ///
    /// グリフ 0 個・font 未解決・textInfo 未生成のときは false
    /// (呼び出し側は「測れない」としてスキップする)。
    /// </summary>
    public static bool TryInkLocalRect(TMP_Text tmp, out Rect ink)
    {
        ink = new Rect(0f, 0f, 0f, 0f);
        if (tmp == null)
        {
            return false;
        }

        tmp.ForceMeshUpdate(true, true);
        var info = tmp.textInfo;
        if (info == null || info.characterCount <= 0 || info.meshInfo == null)
        {
            return false;
        }

        float minX = float.MaxValue;
        float minY = float.MaxValue;
        float maxX = float.MinValue;
        float maxY = float.MinValue;
        bool any = false;
        for (int m = 0; m < info.meshInfo.Length; m++)
        {
            var verts = info.meshInfo[m].vertices;
            if (verts == null)
            {
                continue;
            }

            for (int i = 0; i < verts.Length; i++)
            {
                var v = verts[i];
                if (v.x < minX) { minX = v.x; }
                if (v.x > maxX) { maxX = v.x; }
                if (v.y < minY) { minY = v.y; }
                if (v.y > maxY) { maxY = v.y; }
                any = true;
            }
        }

        if (!any)
        {
            return false;
        }

        ink = Rect.MinMaxRect(minX, minY, maxX, maxY);
        return true;
    }

    /// <summary>
    /// インク矩形 (ローカル) と割当矩形 <see cref="RectTransform.rect"/> (同じ
    /// ローカル空間) の収まり分類。語彙は <see cref="ClassifyScreenClip"/> と
    /// 同じ: Inside = 割当内に収まる / Partial = 端をまたぐ (はみ出し) /
    /// Outside = 割当と交差しない (完全に外)。
    ///
    /// 「インクが割当より大きいが中央に重なる」ケースは Partial になる
    /// (交差はするが内包しない)。これはテキスト溢れの主症状なので、
    /// 監査側では Partial を FAIL ではなく記録扱いにする。
    /// </summary>
    public static ScreenClip ClassifyInkFit(Rect ink, Rect element)
    {
        if (Contains(element, ink, TolerancePx))
        {
            return ScreenClip.Inside;
        }

        var i = Intersection(element, ink);
        return i.width > TolerancePx && i.height > TolerancePx
            ? ScreenClip.Partial
            : ScreenClip.Outside;
    }

    /// <summary>root に至る親鎖からパンビューポート (<see cref="FlashBeat.UI.V6.Common.V6FramePan"/>) を探す。無ければ null。</summary>
    public static FlashBeat.UI.V6.Common.V6FramePan FindPanAncestor(Transform node, Transform root)
    {
        var t = node == null ? null : node.parent;
        while (t != null && t != root)
        {
            var pan = t.GetComponent<FlashBeat.UI.V6.Common.V6FramePan>();
            if (pan != null) { return pan; }
            t = t.parent;
        }

        return null;
    }

    /// <summary>-autotest-scenes を省略したときの既定シーン一覧。</summary>
    public const string DefaultScenes = "TitleScene,SelectScene,OptionScene,ResultScene,GameScene";

    /// <summary>
    /// v6 共通フレームの画面上 origin (左上原点、px)。
    /// 契約: ((w-1728)/2, (h-972)/2) の切捨て。フレームが画面より大きい
    /// (負になる) 場合は 0 に張り付ける。
    /// FHD (96,54) / 2K (416,234) / Mac 2560×1664 (416,346)。
    /// </summary>
    public static Vector2 FrameOrigin(int screenW, int screenH)
    {
        float x = Mathf.Floor((screenW - DesignW) / 2f);
        float y = Mathf.Floor((screenH - DesignH) / 2f);
        return new Vector2(Mathf.Max(0f, x), Mathf.Max(0f, y));
    }

    /// <summary>inner が outer の許容誤差込みで収まるか (両者とも同一座標系の Rect)。</summary>
    public static bool Contains(Rect outer, Rect inner, float tolerance)
    {
        return inner.xMin >= outer.xMin - tolerance
            && inner.xMax <= outer.xMax + tolerance
            && inner.yMin >= outer.yMin - tolerance
            && inner.yMax <= outer.yMax + tolerance;
    }

    /// <summary>inner が outer からはみ出す最大量 (px)。完全に収まっているときは 0。</summary>
    public static float Overshoot(Rect outer, Rect inner)
    {
        float dx = Mathf.Max(0f, Mathf.Max(outer.xMin - inner.xMin, inner.xMax - outer.xMax));
        float dy = Mathf.Max(0f, Mathf.Max(outer.yMin - inner.yMin, inner.yMax - outer.yMax));
        return Mathf.Max(dx, dy);
    }

    /// <summary>RectTransform の 4 隅からワールド (Screen Space Overlay では画面 px) 矩形を作る。</summary>
    public static Rect WorldRect(RectTransform rt)
    {
        var corners = new Vector3[4];
        rt.GetWorldCorners(corners);
        float minX = corners[0].x;
        float minY = corners[0].y;
        float maxX = corners[0].x;
        float maxY = corners[0].y;
        for (int i = 1; i < 4; i++)
        {
            minX = Mathf.Min(minX, corners[i].x);
            minY = Mathf.Min(minY, corners[i].y);
            maxX = Mathf.Max(maxX, corners[i].x);
            maxY = Mathf.Max(maxY, corners[i].y);
        }

        return Rect.MinMaxRect(minX, minY, maxX, maxY);
    }

    /// <summary>
    /// Canvas 配下の v6 共通フレーム節 (「... Frame 1728x972」) を探す。
    /// 無ければ null (呼び出し側が origin 式の代替矩形を使う)。
    /// </summary>
    public static RectTransform FindFrame(Canvas canvas)
    {
        if (canvas == null) { return null; }
        var rects = canvas.GetComponentsInChildren<RectTransform>(false);
        for (int i = 0; i < rects.Length; i++)
        {
            if (rects[i].name.Contains("Frame 1728x972")) { return rects[i]; }
        }

        return null;
    }

    /// <summary>root に至るまでの親鎖からスクロール親 (クリップ / リスト節) を探す。</summary>
    public static Transform FindScrollAncestor(Transform node, Transform root)
    {
        var t = node == null ? null : node.parent;
        while (t != null && t != root)
        {
            if (IsScrollContainer(t)) { return t; }
            t = t.parent;
        }

        return null;
    }

    /// <summary>
    /// スクロール領域とみなす節: クリップ系コンポーネント (Mask / RectMask2D) を
    /// 持つか、名前がスクロール系 (Scroll / List / Viewport / Rows) を含む。
    /// </summary>
    public static bool IsScrollContainer(Transform node)
    {
        if (node == null) { return false; }
        if (node.GetComponent<RectMask2D>() != null || node.GetComponent<Mask>() != null) { return true; }
        string n = node.name;
        return n.Contains("Scroll") || n.Contains("List") || n.Contains("Viewport") || n.Contains("Rows");
    }

    /// <summary>v6 の Canvas か (名前が "V6" で始まる)。</summary>
    public static bool IsV6Canvas(Canvas canvas)
    {
        return canvas != null && canvas.name.StartsWith("V6", StringComparison.Ordinal);
    }

    /// <summary>
    /// 画面全体を基準に判定する Canvas か。HUD は解像度別固有フレーム
    /// (1920×1080 / 2560×1440 / 2560×1664) を持ち、Video Dim は全画面が正しい。
    /// </summary>
    public static bool IsFullScreenCanvas(Canvas canvas)
    {
        if (canvas == null) { return false; }
        string n = canvas.name;
        return n.Contains("Hud") || n.Contains("Dim");
    }

    /// <summary>
    /// Unicode 記号アイコン (Text に残っているとフォント依存で欠ける) の文字か。
    /// 矢印〜各種記号 (U+2190..U+2BFF) / バリエーションセレクタ (U+FE00..FE0F) /
    /// 中点・ブレット類 (U+2010..U+2027)。char は 16 ビットなので BMP のみ
    /// (絵文字のサロゲート対は CJK 拡張との誤検出を避けるため対象外)。
    /// </summary>
    public static bool IsUnicodeSymbolChar(char c)
    {
        return (c >= 0x2190 && c <= 0x2BFF)
            || (c >= 0xFE00 && c <= 0xFE0F)
            || (c >= 0x2010 && c <= 0x2027);
    }

    /// <summary>デバッグ用の階層パス (シーン ルートから)。</summary>
    public static string Path(Transform node)
    {
        if (node == null) { return "(null)"; }
        var sb = new StringBuilder(node.name);
        var t = node.parent;
        while (t != null)
        {
            sb.Insert(0, t.name + "/");
            t = t.parent;
        }

        return sb.ToString();
    }

    /// <summary>プレイヤー引数から -key value の value を取る (無ければ null)。</summary>
    public static string GetArg(string key)
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == key) { return args[i + 1]; }
        }

        return null;
    }
}
