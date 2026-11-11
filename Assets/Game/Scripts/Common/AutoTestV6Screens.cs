using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEngine;
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

    /// <summary>起動中の v6 Canvas を走査して 3 種の機械チェックを記録する。</summary>
    private void Audit(ScreenReport report)
    {
        AuditFrameContainment(report);
        AuditLegacy(report);
        AuditUnicodeSymbols(report);
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
        for (int i = 0; i < canvases.Length; i++)
        {
            var canvas = canvases[i];
            if (!V6ScreenAudit.IsV6Canvas(canvas)) { continue; }

            // 非表示 (ルートが inactive) の v6 Canvas は「その画面に居ない」ので判定しない。
            // 従来は inactive な Pause の Canvas を毎レポート走査し、フレーム節が無いと
            // FAIL にしていた (HUD レポートに Pause の FAIL が混ざっていた)。
            var graphics = canvas.GetComponentsInChildren<Graphic>(false);
            if (graphics.Length == 0) { continue; }

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

    private IEnumerator Screenshot(ScreenReport report, string label)
    {
        yield return new WaitForEndOfFrame();
        shot++;
        string path = Path.Combine(outDir, "v6screen-" + label + "-" + shot + ".png");
        ScreenCapture.CaptureScreenshot(path);
        yield return new WaitForSecondsRealtime(0.6f);
        bool ok = File.Exists(path);
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
