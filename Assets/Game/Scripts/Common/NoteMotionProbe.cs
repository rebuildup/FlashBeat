using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Video;
using FlashBeat.Gameplay;

/// <summary>
/// ノーツ移動の更新頻度を計測するプローブ (Issue #739)。プレイヤー引数
/// <c>-fbnoteprobe</c> があるときだけ動き、GameScene の演奏中に 5 秒ごとに
/// 次をログへ出す。
///
///   stepHz / stepZeroPct / stepP50Ms / stepP95Ms
///       GManager.step_time (= VideoPlayer.time 由来のメディア時計) の
///       フレームごとの変化量の分布。動画フレーム単位に量子化されていれば
///       「変化なしフレームが大半 + 一定量のジャンプが周期的に来る」形になる。
///   noteMovePct / noteHz
///       画面内のノーツ 1 個を追跡し、位置 (3D は z、v6 は anchoredPosition)
///       が実際に変わったフレームの割合 = ノーツの実効更新レート。
///       stepHz とほぼ同じなら「時計の量子化がそのまま見た目に出ている」、
///       レンダー fps 近くなら時計以外が原因。
///   videoFps / videoRate / avDriftMs / avDriftMaxMs
///       VideoPlayer.frameCount の増分 (= デコード fps)、映像時計の
///       実時間に対する進み具合、壁時計との乖離。
///
/// 出力形式は <see cref="FrameTimeProbe"/> と同じ `event=window` / `event=spike`
/// なので、mac-fbperf.sh と同じ要領で Player.log から集計できる。
/// オーバーヘッドはフレームあたり数回の比較と、30 フレームに 1 回の
/// FindObjectsByType だけ。引数が無ければ何も作らない。
/// </summary>
public sealed class NoteMotionProbe : MonoBehaviour
{
    const string Tag = "[NoteProbe]";
    const string GameSceneName = "GameScene";

    /// <summary>計測結果を出す間隔 (秒)。FrameTimeProbe と揃える。</summary>
    const float ReportInterval = 5f;

    /// <summary>起動直後のシェーダ準備などを平均に混ぜないための捨てフレーム数。</summary>
    const int WarmupFrames = 90;

    /// <summary>step 変化量サンプルの保持上限 (区間ごとに捨てる)。</summary>
    const int Capacity = 2048;

    /// <summary>これを超えるフレームは spike として即時記録する (秒)。</summary>
    const float SpikeThresholdSeconds = 0.25f;

    /// <summary>step_time の「変化あり」判定の許容誤差 (秒)。</summary>
    const float StepEpsilon = 0.0001f;

    /// <summary>
    /// 1 フレームの step 変化がこれを超えたらシーク / 停止明けとみなす
    /// (MediaRenderClock.MaxJumpSeconds と同じ目安)。
    /// </summary>
    const float MaxPlausibleStep = 0.5f;

    /// <summary>この秒数 step が止まったらメディア停止として 1 回だけ記録する。</summary>
    const float StallLogSeconds = 2f;

    /// <summary>ノーツ候補の再走査間隔 (フレーム)。</summary>
    const int NoteRescanIntervalFrames = 30;

    /// <summary>ノーツが「見えている」とみなす z の範囲 (手前は少しだけ許す)。</summary>
    const float NoteVisibleZMin = -1f;

    /// <summary>3D ノーツの「動いた」判定しきい値 (world z)。</summary>
    const float NoteMoveEpsilonWorld = 0.001f;

    /// <summary>v6 ノーツの「動いた」判定しきい値 (anchoredPosition px)。</summary>
    const float NoteMoveEpsilonPixel = 0.01f;

    readonly List<float> stepSamples = new List<float>(Capacity);
    float intervalStart;
    int frames;
    int framesSinceStart;
    bool wasPlaying;
    bool finalReported;

    // --- step_time (メディア時計) の計測 ---
    bool hasPrevStep;
    float prevStep;
    int stepChanges;
    int zeroStepFrames;
    int stepJumps;
    float stepAdvanceSum;
    float stallSeconds;
    bool stallLogged;

    // --- ノーツ位置の計測 ---
    Notes probeNote;
    int framesUntilRescan;
    int visibleNoteFrames;
    int noteMoveFrames;
    float lastNotePos;
    bool hasLastNotePos;

    // --- VideoPlayer の計測 ---
    MusicManager musicManager;
    int framesUntilManagerSearch;
    bool videoAnchored;
    float videoAnchorTime;
    float videoAnchorWall;
    long lastFrameCount = -1;
    float lastVideoTime;
    long videoFrameDelta;
    float videoAdvanceSum;
    float lastAvDriftMs;
    float maxAvDriftMs;

    /// <summary>プレイヤー引数に <c>-fbnoteprobe</c> があるときだけ計測器を立てる。</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (!HasCommandLineFlag("-fbnoteprobe")) return;

        var go = new GameObject("NoteMotionProbe");
        UnityEngine.Object.DontDestroyOnLoad(go);
        go.AddComponent<NoteMotionProbe>();
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
        if (framesSinceStart == WarmupFrames + 1)
        {
            ResetInterval();
        }

        // GameScene 以外 (選曲・結果など) では記録しない。
        if (SceneManager.GetActiveScene().name != GameSceneName) return;

        // 演奏開始 (GManager.Start) の立ち上がりで区間を切り直す。
        // 開始前の「step が 0 のまま」のフレームを演奏中の統計に混ぜない。
        bool playing = GManager.Start;
        if (playing && !wasPlaying)
        {
            ResetInterval();
        }
        wasPlaying = playing;
        if (!playing) return;

        float wall = Time.unscaledTime;
        float dt = Time.unscaledDeltaTime;

        if (dt > SpikeThresholdSeconds)
        {
            Debug.Log(Tag + " event=spike scene=" + GameSceneName
                + " frameMs=" + (dt * 1000f).ToString("F1", CultureInfo.InvariantCulture)
                + " t=" + wall.ToString("F2", CultureInfo.InvariantCulture)
                + " mark=" + PerfMarks.Describe()
                + " gc0Total=" + GC.CollectionCount(0));
        }

        ObserveStepClock(wall, dt);
        ObserveNotePosition();
        ObserveVideoPlayer(wall);

        frames++;
        if (wall - intervalStart >= ReportInterval)
        {
            Report(wall);
            ResetInterval();
        }
    }

    void OnApplicationQuit()
    {
        if (!finalReported && frames > 0)
        {
            Report(Time.unscaledTime);
            finalReported = true;
        }
    }

    /// <summary>step_time のフレームごとの差分を集計する。</summary>
    void ObserveStepClock(float wall, float dt)
    {
        float step = GManager.step_time;
        if (!hasPrevStep)
        {
            hasPrevStep = true;
            prevStep = step;
            return;
        }

        float dStep = step - prevStep;
        prevStep = step;

        if (dStep > StepEpsilon && dStep <= MaxPlausibleStep)
        {
            stepChanges++;
            stepAdvanceSum += dStep;
            if (stepSamples.Count >= Capacity) stepSamples.RemoveAt(0);
            stepSamples.Add(dStep);
            stallSeconds = 0f;
            stallLogged = false;
        }
        else if (dStep < -StepEpsilon || dStep > MaxPlausibleStep)
        {
            // シーク / 停止明けのジャンプ。量子化の分析対象からは外す。
            stepJumps++;
            stallSeconds = 0f;
            stallLogged = false;
            Debug.Log(Tag + " event=stepjump scene=" + GameSceneName
                + " dStep=" + dStep.ToString("F3", CultureInfo.InvariantCulture)
                + " mediaTime=" + step.ToString("F3", CultureInfo.InvariantCulture)
                + " t=" + wall.ToString("F2", CultureInfo.InvariantCulture));
        }
        else
        {
            zeroStepFrames++;
            stallSeconds += dt;
            if (stallSeconds > StallLogSeconds && !stallLogged)
            {
                stallLogged = true;
                Debug.Log(Tag + " event=mediastall scene=" + GameSceneName
                    + " stillS=" + stallSeconds.ToString("F1", CultureInfo.InvariantCulture)
                    + " mediaTime=" + step.ToString("F3", CultureInfo.InvariantCulture)
                    + " t=" + wall.ToString("F2", CultureInfo.InvariantCulture));
            }
        }
    }

    /// <summary>
    /// 画面内のノーツを 1 個だけ追跡し、位置が実際に変わったフレームを数える。
    /// 追跡対象は 30 フレームごと (または見失ったとき) に選び直す。
    /// </summary>
    void ObserveNotePosition()
    {
        framesUntilRescan--;
        if (probeNote == null || framesUntilRescan <= 0)
        {
            framesUntilRescan = NoteRescanIntervalFrames;
            probeNote = FindVisibleNote();
            hasLastNotePos = false;
        }

        if (probeNote == null) return;

        float pos;
        bool visible;
        float epsilon;
        if (FlashBeat.UI.V6.V6UiMode.Playfield)
        {
            UnityEngine.UI.Image head = probeNote.HeadImage;
            visible = head != null && head.gameObject.activeSelf;
            pos = head != null ? head.rectTransform.anchoredPosition.y : 0f;
            epsilon = NoteMoveEpsilonPixel;
        }
        else
        {
            float z = probeNote.transform.position.z;
            visible = z > NoteVisibleZMin && z < MediaTimeline.VisibleDepthZ;
            pos = z;
            epsilon = NoteMoveEpsilonWorld;
        }

        if (!visible) return;

        visibleNoteFrames++;
        if (!hasLastNotePos || Mathf.Abs(pos - lastNotePos) > epsilon)
        {
            noteMoveFrames++;
            lastNotePos = pos;
            hasLastNotePos = true;
        }
    }

    /// <summary>画面内にあるノーツを 1 個返す。無ければ null。</summary>
    Notes FindVisibleNote()
    {
        Notes[] notes = FindObjectsByType<Notes>(FindObjectsSortMode.None);
        bool v6 = FlashBeat.UI.V6.V6UiMode.Playfield;
        for (int i = 0; i < notes.Length; i++)
        {
            Notes n = notes[i];
            if (n == null) continue;
            if (v6)
            {
                UnityEngine.UI.Image head = n.HeadImage;
                if (head != null && head.gameObject.activeSelf) return n;
            }
            else
            {
                float z = n.transform.position.z;
                if (z > NoteVisibleZMin && z < MediaTimeline.VisibleDepthZ) return n;
            }
        }
        return null;
    }

    /// <summary>
    /// VideoPlayer のフレーム数・時刻を集計する。映像時計が壁時計どおりに
    /// 進んでいるか (映像は 24/30fps でも時刻は連続のはず)、デコード fps、
    /// 壁時計との乖離を見る。
    /// </summary>
    void ObserveVideoPlayer(float wall)
    {
        if (musicManager == null)
        {
            framesUntilManagerSearch--;
            if (framesUntilManagerSearch <= 0)
            {
                framesUntilManagerSearch = 120;
                musicManager = FindFirstObjectByType<MusicManager>();
            }
        }

        VideoPlayer vp = musicManager != null ? musicManager.SongVideoForAudit : null;
        if (vp == null)
        {
            videoAnchored = false;
            lastFrameCount = -1;
            return;
        }

        long frameCount = (long)vp.frameCount;
        float videoTime = (float)vp.time;

        if (vp.isPrepared && frameCount > 0)
        {
            if (!videoAnchored)
            {
                videoAnchored = true;
                videoAnchorTime = videoTime;
                videoAnchorWall = wall;
                maxAvDriftMs = 0f;
            }

            if (lastFrameCount >= 0)
            {
                long dFrames = frameCount - lastFrameCount;
                if (dFrames > 0) videoFrameDelta += dFrames;

                float dVideo = videoTime - lastVideoTime;
                if (dVideo >= -StepEpsilon && dVideo <= MaxPlausibleStep)
                {
                    videoAdvanceSum += Mathf.Max(0f, dVideo);
                }
            }

            // 壁時計から見た映像時計の乖離 (正 = 映像が進みすぎ / 負 = 遅れ)。
            float driftMs = ((videoTime - videoAnchorTime) - (wall - videoAnchorWall)) * 1000f;
            lastAvDriftMs = driftMs;
            float absDrift = Mathf.Abs(driftMs);
            if (absDrift > maxAvDriftMs) maxAvDriftMs = absDrift;
        }
        else
        {
            videoAnchored = false;
        }

        lastFrameCount = frameCount;
        lastVideoTime = videoTime;
    }

    void ResetInterval()
    {
        intervalStart = Time.unscaledTime;
        frames = 0;
        hasPrevStep = false;
        stepChanges = 0;
        zeroStepFrames = 0;
        stepJumps = 0;
        stepAdvanceSum = 0f;
        stallSeconds = 0f;
        stallLogged = false;
        visibleNoteFrames = 0;
        noteMoveFrames = 0;
        hasLastNotePos = false;
        videoFrameDelta = 0;
        videoAdvanceSum = 0f;
        lastAvDriftMs = 0f;
        // maxAvDriftMs は再生全体の最大なので区間をまたいで保持する。
        // ただし映像が切り替わった (videoAnchored が折れた) あとは
        // ObserveVideoPlayer 側で 0 に戻る。
        stepSamples.Clear();
    }

    void Report(float wall)
    {
        if (frames <= 0) return;

        float elapsed = Mathf.Max(wall - intervalStart, 0.0001f);
        var sorted = new List<float>(stepSamples);
        sorted.Sort();

        float stepHz = stepChanges / elapsed;
        float stepRate = stepAdvanceSum / elapsed;
        float zeroPct = frames > 0 ? zeroStepFrames * 100f / frames : 0f;
        float videoFps = videoFrameDelta / elapsed;
        // 映像時計の実時間レート (1.0 が正常)。時刻差分の合計を区間経過で
        // 割るので、量子化された時計でも「平均して実時間どおりか」が出る。
        float videoRate = videoAdvanceSum / elapsed;

        var sb = new StringBuilder(384);
        sb.Append(Tag);
        sb.Append(" event=window");
        sb.Append(" scene=").Append(GameSceneName);
        sb.Append(" frames=").Append(frames);
        sb.Append(" avgFps=").Append((frames / elapsed).ToString("F1", CultureInfo.InvariantCulture));
        sb.Append(" stepHz=").Append(stepHz.ToString("F1", CultureInfo.InvariantCulture));
        sb.Append(" stepRate=").Append(stepRate.ToString("F3", CultureInfo.InvariantCulture));
        sb.Append(" stepZeroPct=").Append(zeroPct.ToString("F1", CultureInfo.InvariantCulture));
        sb.Append(" stepP50Ms=").Append(FrameTimeProbe.PercentileMs(sorted, 50f).ToString("F1", CultureInfo.InvariantCulture));
        sb.Append(" stepP95Ms=").Append(FrameTimeProbe.PercentileMs(sorted, 95f).ToString("F1", CultureInfo.InvariantCulture));
        sb.Append(" jumps=").Append(stepJumps);
        sb.Append(" noteMovePct=").Append(visibleNoteFrames > 0 ? (noteMoveFrames * 100f / visibleNoteFrames).ToString("F1", CultureInfo.InvariantCulture) : "0.0");
        sb.Append(" noteHz=").Append((noteMoveFrames / elapsed).ToString("F1", CultureInfo.InvariantCulture));
        sb.Append(" noteSeenPct=").Append(frames > 0 ? (visibleNoteFrames * 100f / frames).ToString("F1", CultureInfo.InvariantCulture) : "0.0");
        sb.Append(" videoFps=").Append(videoFps.ToString("F1", CultureInfo.InvariantCulture));
        sb.Append(" videoRate=").Append(videoRate.ToString("F3", CultureInfo.InvariantCulture));
        sb.Append(" avDriftMs=").Append(lastAvDriftMs.ToString("F1", CultureInfo.InvariantCulture));
        sb.Append(" avDriftMaxMs=").Append(maxAvDriftMs.ToString("F1", CultureInfo.InvariantCulture));
        Debug.Log(sb.ToString());
    }
}
