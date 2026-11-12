using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// フレームタイムの計測器 (issue #381)。プレイヤー引数 <c>-fbperf</c> がある
/// ときだけ動き、5 秒ごとにフレーム時間の分布と GC の発生をログに出す。
///
/// Profiler を繋がずに「どの画面で、どのくらいカクついたか」を数値で残すため
/// のもので、計測のたびに開発ビルドを作り直さないで済む。オーバーヘッドは
/// 1 フレーム 1 回の <see cref="Time.unscaledDeltaTime"/> の記録だけで、
/// 計測していないプレイヤーには存在しない (引数が無ければ何も作らない)。
///
/// 出力の読み方:
///   avgFps  区間全体の平均 (フレーム数の合計 ÷ 時間)
///   p50/p95/p99  フレーム時間の百分位 (ミリ秒)。60fps は 16.7ms
///   worst   区間内の最悪フレーム
///   gc0     gen0 GC の回数。プレイ中は 0 が目標 (毎フレームのアロケーションが
///           無いことの代理指標。プロファイラ無しでも取れる)
///   gcKBPerFrame  区間のマネージドヒープ増分 ÷ フレーム数 (確保量の目安)
/// </summary>
public sealed class FrameTimeProbe : MonoBehaviour
{
    const string Tag = "[PerfProbe]";

    /// <summary>計測結果を出す間隔 (秒)。</summary>
    const float ReportInterval = 5f;

    /// <summary>起動直後のシェーダ準備などを平均に混ぜないための捨てフレーム数。</summary>
    const int WarmupFrames = 90;

    /// <summary>百分位の計算に使う区間のサンプル上限 (60fps × 5 秒 = 300)。</summary>
    const int Capacity = 2048;

    readonly List<float> samples = new List<float>(Capacity);
    float intervalStart;
    int frames;
    int gc0AtIntervalStart;
    long memoryAtIntervalStart;
    int framesSinceStart;
    bool finalReported;
    float worstSeen;

    /// <summary>これを超えるフレームは「引っかかり」として即時に記録する (秒)。</summary>
    const float SpikeThresholdSeconds = 0.25f;

    /// <summary>プレイヤー引数に <c>-fbperf</c> があるときだけ計測器を立てる。</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (!HasCommandLineFlag("-fbperf")) return;

        var go = new GameObject("FrameTimeProbe");
        UnityEngine.Object.DontDestroyOnLoad(go);
        go.AddComponent<FrameTimeProbe>();
    }

    static bool HasCommandLineFlag(string flag)
    {
        string[] args;
        try { args = Environment.GetCommandLineArgs(); }
        catch { return false; }

        for (int i = 0; i < args.Length; i++)
        {
            if (string.Equals(args[i], flag, StringComparison.Ordinal)) return true;
        }
        return false;
    }

    void Awake()
    {
        ResetInterval();
    }

    void Update()
    {
        framesSinceStart++;
        if (framesSinceStart <= WarmupFrames) return;

        float dt = Time.unscaledDeltaTime;

        // 引っかかりは区間の集計を待たずに、起きた場所と時刻を残す。
        // 「どの画面の、どの処理で固まったか」を窓の平均から読み解くのは
        // 不可能なので、しきい値を超えたフレームだけ即時に出す。
        if (dt > SpikeThresholdSeconds && dt > worstSeen)
        {
            worstSeen = dt;
            Debug.Log(Tag + " event=spike scene=" + SceneManager.GetActiveScene().name
                + " frameMs=" + (dt * 1000f).ToString("F1", CultureInfo.InvariantCulture)
                + " t=" + Time.unscaledTime.ToString("F2", CultureInfo.InvariantCulture));
        }

        if (samples.Count >= Capacity) samples.RemoveAt(0);
        samples.Add(dt);
        frames++;

        if (Time.unscaledTime - intervalStart >= ReportInterval)
        {
            Report();
            ResetInterval();
        }
    }

    void OnApplicationQuit()
    {
        if (!finalReported && frames > 0)
        {
            Report();
            finalReported = true;
        }
    }

    void ResetInterval()
    {
        intervalStart = Time.unscaledTime;
        frames = 0;
        gc0AtIntervalStart = GC.CollectionCount(0);
        memoryAtIntervalStart = GC.GetTotalMemory(false);
    }

    void Report()
    {
        if (frames <= 0 || samples.Count == 0) return;

        var sorted = new List<float>(samples);
        sorted.Sort();

        float elapsed = Mathf.Max(Time.unscaledTime - intervalStart, 0.0001f);
        int gc0 = GC.CollectionCount(0) - gc0AtIntervalStart;
        long memoryDelta = GC.GetTotalMemory(false) - memoryAtIntervalStart;

        var sb = new StringBuilder(256);
        sb.Append(Tag);
        sb.Append(" event=window");
        sb.Append(" scene=").Append(SceneManager.GetActiveScene().name);
        sb.Append(" frames=").Append(frames);
        sb.Append(" avgFps=").Append((frames / elapsed).ToString("F1", CultureInfo.InvariantCulture));
        sb.Append(" p50=").Append(PercentileMs(sorted, 50f).ToString("F1", CultureInfo.InvariantCulture));
        sb.Append(" p95=").Append(PercentileMs(sorted, 95f).ToString("F1", CultureInfo.InvariantCulture));
        sb.Append(" p99=").Append(PercentileMs(sorted, 99f).ToString("F1", CultureInfo.InvariantCulture));
        sb.Append(" worst=").Append((sorted[sorted.Count - 1] * 1000f).ToString("F1", CultureInfo.InvariantCulture));
        sb.Append(" gc0=").Append(gc0);
        sb.Append(" gcKBPerFrame=").Append((memoryDelta / 1024.0 / frames).ToString("F1", CultureInfo.InvariantCulture));
        Debug.Log(sb.ToString());
    }

    /// <summary>
    /// 昇順に並べたフレーム時間 (秒) の百分位をミリ秒で返す。空のリストは 0。
    /// 区間の端は clamp するので、p99 は「最悪フレーム」に寄る。
    /// </summary>
    public static float PercentileMs(List<float> sortedSamples, float percentile)
    {
        if (sortedSamples == null || sortedSamples.Count == 0) return 0f;

        int index = Mathf.Clamp(
            Mathf.CeilToInt(percentile / 100f * sortedSamples.Count) - 1,
            0,
            sortedSamples.Count - 1);
        return sortedSamples[index] * 1000f;
    }
}
