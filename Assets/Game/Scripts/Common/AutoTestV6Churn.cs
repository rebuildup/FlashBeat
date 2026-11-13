using System;
using System.Collections;
using System.Diagnostics;
using Debug = UnityEngine.Debug;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// v6 History / Ranking の更新コストをプレイヤー実機で測る計測モード
/// (<see cref="AutoTestV6Screens"/> の兄弟モード、Issue #415 / #397 / #381)。
///
/// 使い方 (プレイヤー引数):
///   -autotest v6churn -autotest-out &lt;dir&gt; [-autotest-seconds &lt;秒/フェーズ, 既定 8&gt;]
///
/// SelectScene を開き、History → Ranking の各フェーズで
///   1. Refresh() を連続で呼んだときの壁時計時間 (再構築か差分更新かの直接指標)
///   2. マネージドヒープの増分 (GC.GetTotalMemory、バッチ前に Collect 済み)
///   3. View 配下の GameObject 数 (スロット再利用で頭打ちになるか)
///   4. フレーム単位の操作チャーン (Refresh + MoveSelection) を数秒流し、
///      -fbperf の FrameTimeProbe 5 秒窓に載せる
/// を記録する。結果は Debug.Log ([V6Churn] ...) と
/// &lt;out&gt;/autotest-v6churn-result.txt。
///
/// 監査カウンタ (RebuildCountForAudit / IncrementalUpdateCountForAudit) は
/// リフレクションで読む。カウンタ未実装のビルド (変更前) でも同じコードで
/// 動き、"-" と記録される。before/after を同じ計測系で比較するためのモード。
/// </summary>
public class AutoTestV6Churn : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (V6ScreenAudit.GetArg("-autotest") != "v6churn") { return; }
        var go = new GameObject("AutoTestV6Churn");
        DontDestroyOnLoad(go);
        go.AddComponent<AutoTestV6Churn>();
    }

    private const float SettleSeconds = 4f;
    private const float ViewWaitSeconds = 15f;
    private const int RefreshBatch = 60;
    private const int WarmupRefresh = 10;

    private string outDir;
    private readonly StringBuilder result = new StringBuilder();
    private float phaseSeconds = 8f;

    private IEnumerator Start()
    {
        outDir = V6ScreenAudit.GetArg("-autotest-out");
        if (string.IsNullOrEmpty(outDir)) { outDir = Application.persistentDataPath; }
        string secs = V6ScreenAudit.GetArg("-autotest-seconds");
        float parsed;
        if (!string.IsNullOrEmpty(secs) && float.TryParse(secs, NumberStyles.Float,
                CultureInfo.InvariantCulture, out parsed) && parsed > 0f)
        {
            phaseSeconds = parsed;
        }
        try { Directory.CreateDirectory(outDir); }
        catch (Exception) { }

        Log("start out=" + outDir + " phaseSeconds=" + phaseSeconds
            + " resolution=" + Screen.width + "x" + Screen.height);

        SceneManager.LoadScene("SelectScene");
        yield return new WaitForSecondsRealtime(SettleSeconds);

        var history = WaitForView<FlashBeat.UI.V6.History.V6HistoryView>(
            () => FlashBeat.UI.V6.History.V6HistoryView.Current);
        var ranking = WaitForView<FlashBeat.UI.V6.Ranking.V6RankingView>(
            () => FlashBeat.UI.V6.Ranking.V6RankingView.Current);
        yield return history;
        yield return ranking;

        var historyView = FlashBeat.UI.V6.History.V6HistoryView.Current;
        var rankingView = FlashBeat.UI.V6.Ranking.V6RankingView.Current;
        if (historyView == null || rankingView == null)
        {
            Log("FAIL v6 history/ranking views did not appear");
            Finish();
            yield break;
        }

        yield return RunPhase("history", historyView, () => historyView.MoveSelection(1),
            () => historyView.MoveSelection(-1), () => historyView.Refresh());
        yield return RunPhase("ranking", rankingView, () => rankingView.MoveSelection(1),
            () => rankingView.MoveSelection(-1), () => rankingView.Refresh());

        // Select フェーズ (#415 phase 4 の要否判断用): SelectView は
        // RefreshVolatile で可変ゾーンを毎回破棄→再構築する (#399)。
        // 行スロット再利用を入れるかはここの per-call コストで決める。
        // 公開の行移動 API が無いため op は no-op、Refresh が主計測対象。
        var selectView = FlashBeat.UI.V6.Select.View.V6SelectView.Current;
        if (selectView == null)
        {
            Log("FAIL v6 select view did not appear");
            Finish();
            yield break;
        }

        yield return RunPhase("select", selectView, () => { }, () => { },
            () => selectView.Refresh());

        // Option フェーズ: OptionScene を開き直して同じ計測を行う。
        // OptionView の監査 API は MoveSelectionForAudit / Refresh / SelectTabForAudit。
        SceneManager.LoadScene("OptionScene");
        yield return new WaitForSecondsRealtime(SettleSeconds);
        yield return WaitForView<FlashBeat.UI.V6.Option.V6OptionView>(
            () => FlashBeat.UI.V6.Option.V6OptionView.Current);
        var optionView = FlashBeat.UI.V6.Option.V6OptionView.Current;
        if (optionView == null)
        {
            Log("FAIL v6 option view did not appear");
            Finish();
            yield break;
        }

        // MoveSelectionForAudit は差分更新側にのみ存在する監査 API。
        // 変更前ビルドではリフレクションで見つからず no-op になるが、
        // churn ループは毎フレーム Refresh() を呼ぶため Refresh 経路の
        // before/after 比較はそのまま有効 (選択移動 op のみ前後で差が出る)。
        var moveMethod = optionView.GetType().GetMethod("MoveSelectionForAudit",
            BindingFlags.Public | BindingFlags.Instance);
        Action<int> move = moveMethod != null
            ? d => moveMethod.Invoke(optionView, new object[] { d })
            : d => { };

        yield return RunPhase("option", optionView,
            () => move(1), () => move(-1), () => optionView.Refresh());

        Log("done");
        Finish();
    }

    private IEnumerator WaitForView<T>(Func<T> get) where T : class
    {
        float deadline = Time.realtimeSinceStartup + ViewWaitSeconds;
        while (get() == null && Time.realtimeSinceStartup < deadline)
        {
            yield return null;
        }
    }

    /// <summary>
    /// 1 フェーズ: 表示 → ウォームアップ → Refresh バッチ計測 →
    /// フレームチャーン (probe 窓用) → センサス。
    /// </summary>
    private IEnumerator RunPhase(string name, object view, Action opPos, Action opNeg, Action opRefresh)
    {
        var setVisible = view.GetType().GetMethod("SetVisible");
        setVisible.Invoke(view, new object[] { true });
        yield return new WaitForSecondsRealtime(1f);

        // ウォームアップ (初回構築・JIT を計測から外す)。
        for (int i = 0; i < WarmupRefresh; i++) { opRefresh(); }
        yield return null;

        // Refresh バッチ: 壁時計とマネージドヒープ増分。
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        long memBefore = GC.GetTotalMemory(false);
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < RefreshBatch; i++) { opRefresh(); }
        sw.Stop();
        long memAfter = GC.GetTotalMemory(false);
        Log(string.Format(CultureInfo.InvariantCulture,
            "phase={0} refreshBatch n={1} totalMs={2:F1} avgMs={3:F3} managedDeltaKB={4}",
            name, RefreshBatch, sw.Elapsed.TotalMilliseconds,
            sw.Elapsed.TotalMilliseconds / RefreshBatch,
            (memAfter - memBefore) / 1024));

        // フレームチャーン: probe の 5 秒窓に載るよう位相秒ぶん流す。
        // Refresh と選択移動を交互に (Refresh は毎回全量更新経路を通る)。
        float deadline = Time.realtimeSinceStartup + phaseSeconds;
        int churnOps = 0;
        int dir = 1;
        var churnSw = Stopwatch.StartNew();
        while (Time.realtimeSinceStartup < deadline)
        {
            opRefresh();
            if ((churnOps & 1) == 0) { (dir > 0 ? opPos : opNeg)(); }
            else { (dir > 0 ? opNeg : opPos)(); }
            if ((churnOps % 16) == 15) { dir = -dir; }
            churnOps++;
            yield return null;
        }
        churnSw.Stop();
        Log(string.Format(CultureInfo.InvariantCulture,
            "phase={0} churn ops={1} wallMs={2:F0} opsPerSec={3:F1}",
            name, churnOps, churnSw.Elapsed.TotalMilliseconds,
            churnOps / Math.Max(0.001, churnSw.Elapsed.TotalSeconds)));

        // センサスと監査カウンタ (あれば)。
        Log(Census(name, view));

        setVisible.Invoke(view, new object[] { false });
        yield return new WaitForSecondsRealtime(0.5f);
    }

    private string Census(string phase, object view)
    {
        var rootProp = view.GetType().GetProperty("Root");
        var root = rootProp != null ? rootProp.GetValue(view, null) as RectTransform : null;
        int objects = root != null ? CountDescendants(root.transform) : -1;
        object rebuild = ReadCounter(view, "RebuildCountForAudit");
        object incremental = ReadCounter(view, "IncrementalUpdateCountForAudit");
        return string.Format(CultureInfo.InvariantCulture,
            "phase={0} census objects={1} rebuild={2} incremental={3} managedKB={4}",
            phase, objects,
            rebuild != null ? rebuild.ToString() : "-",
            incremental != null ? incremental.ToString() : "-",
            GC.GetTotalMemory(false) / 1024);
    }

    private static object ReadCounter(object target, string name)
    {
        var prop = target.GetType().GetProperty(name,
            BindingFlags.Public | BindingFlags.Instance);
        return prop != null ? prop.GetValue(target, null) : null;
    }

    private static int CountDescendants(Transform root)
    {
        int count = 0;
        var stack = new System.Collections.Generic.Stack<Transform>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var t = stack.Pop();
            count++;
            for (int i = 0; i < t.childCount; i++) { stack.Push(t.GetChild(i)); }
        }
        return count;
    }

    private void Log(string line)
    {
        string full = "[V6Churn] " + line;
        Debug.Log(full);
        result.AppendLine(full);
    }

    private void Finish()
    {
        string path = Path.Combine(outDir, "autotest-v6churn-result.txt");
        try { File.WriteAllText(path, result.ToString()); }
        catch (Exception e) { Debug.LogWarning("[V6Churn] write result failed: " + e.Message); }
        Application.Quit();
    }
}
