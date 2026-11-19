using FlashBeat.Gameplay;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// MediaRenderClock (Issue #739) の契約テスト。
///
/// VideoPlayer.time 由来の step_time が動画フレーム単位に量子化されても、
/// ノーツの描画位置がレンダーフレームごとに滑らかに進むこと、
/// メディア停止時に外挿が頭打ちになること、シークで張り直すことを固定する。
/// 判定が使う step_time 自体には一切触れない (ここは描画専用の時計)。
/// </summary>
public class MediaRenderClockTests
{
    [SetUp]
    public void SetUp()
    {
        MediaRenderClock.Reset();
    }

    /// <summary>
    /// 24fps 相当の量子化時計 (壁時計 60fps、step は 1/24 刻み) を流し、
    /// 補間値が「毎フレーム少しずつ進む」ことを確認する。
    /// 生の step_time は 2〜3 フレーム同じ値のあと 1/24 秒跳ぶので、
    /// 1 フレームあたりの進み幅の最大値で両者を区別できる。
    /// </summary>
    [Test]
    public void AQuantizedMediaClockStillRendersSmoothly()
    {
        const float frameDt = 1f / 60f;
        const float videoFps = 24f;
        const float duration = 2f;

        float wall = 0f;
        float maxAdvance = 0f;
        float minAdvance = float.MaxValue;
        float maxLead = 0f;
        float minLead = float.MaxValue;
        int frames = 0;
        float prevRender = -1f;

        for (int i = 0; i < (int)(duration / frameDt); i++)
        {
            // VideoPlayer.time が動画フレームのタイムスタンプを返すと仮定:
            // 壁時計が進んでも step は 1/videoFps 刻みでしか進まない。
            float step = Mathf.Floor(wall * videoFps) / videoFps;
            MediaRenderClock.Observe(step, wall);

            float render = MediaRenderClock.RenderTime;
            // 権威あるメディア時刻に対して、描画時計は先行しすぎず
            // 遅れすぎもしない (スナップの残差は小さい)。
            float lead = render - step;
            if (lead > maxLead) maxLead = lead;
            if (lead < minLead) minLead = lead;

            if (prevRender >= 0f)
            {
                float advance = render - prevRender;
                if (advance > maxAdvance) maxAdvance = advance;
                if (advance < minAdvance) minAdvance = advance;
            }
            prevRender = render;
            frames++;
            wall += frameDt;
        }

        // 1 フレームの進み幅が動画フレーム 1 枚分 (≈41.7ms) を超えないこと。
        // 生の step_time では 1/24 ≈ 41.7ms のジャンプが周期的に起きる。
        Assert.Less(maxAdvance, 1f / videoFps,
            "the render clock still jumps by a whole video frame - interpolation is not working");
        // スナップ時の逆行は量子の位相残差 (≒数 ms) に収まる。
        Assert.Greater(minAdvance, -0.02f,
            "the render clock ticks backwards by more than the quantum phase residual");
        // 権威ある時刻からの先行は外挿上限以内、遅れはスナップ残差以内。
        Assert.LessOrEqual(maxLead, MediaRenderClock.MaxLeadSeconds + 0.001f,
            "the render clock runs too far ahead of the media clock");
        Assert.GreaterOrEqual(minLead, -0.02f,
            "the render clock falls behind the media clock");
    }

    /// <summary>
    /// step_time が毎フレーム滑らかに進む経路 (音声時計・壁時計
    /// フォールバック) では、補間は実質 no-op で step_time と同じ値を返す。
    /// </summary>
    [Test]
    public void ASmoothClockIsPassedThroughAlmostUnchanged()
    {
        float wall = 10f;
        for (int i = 0; i < 120; i++)
        {
            float step = 3f + i * (1f / 60f);
            MediaRenderClock.Observe(step, wall);
            Assert.AreEqual(step, MediaRenderClock.RenderTime, 0.0005f,
                "a smooth clock must not be shifted by the render clock");
            wall += 1f / 60f;
        }
    }

    /// <summary>
    /// メディアが止まる (バッファ stall) と step_time が進まなくなる。
    /// 外挿は上限で頭打ちになり、ノーツは従来どおり保持される。
    /// </summary>
    [Test]
    public void AStalledClockHoldsTheNotesWithinABoundedLead()
    {
        float wall = 0f;
        float step = 0f;
        // まず普通に進めてレートと量子を推定させる (24fps 相当)。
        for (int i = 0; i < 240; i++)
        {
            step = Mathf.Floor(wall * 24f) / 24f;
            MediaRenderClock.Observe(step, wall);
            wall += 1f / 60f;
        }

        // メディア停止: step は固定、壁時計だけが進む。
        for (int i = 0; i < 600; i++) // 10 秒
        {
            MediaRenderClock.Observe(step, wall);
            wall += 1f / 60f;
        }

        float lead = MediaRenderClock.RenderTime - step;
        Assert.LessOrEqual(lead, MediaRenderClock.MaxLeadSeconds + 0.0001f,
            "a stalled media clock must not let notes run ahead without bound");
        Assert.GreaterOrEqual(lead, 0f, "the render clock must never run backwards");
    }

    /// <summary>シーク (後退) では外挿を捨てて現在値に張り直す。</summary>
    [Test]
    public void ABackwardSeekReanchorsImmediately()
    {
        float wall = 0f;
        for (int i = 0; i < 120; i++)
        {
            MediaRenderClock.Observe(5f + i * (1f / 60f), wall);
            wall += 1f / 60f;
        }

        // 古い値が残っている間に大きく後退するシーク。
        MediaRenderClock.Observe(2f, wall);
        Assert.AreEqual(2f, MediaRenderClock.RenderTime, 0.0001f,
            "a backward seek must snap the render clock to the authoritative time");
    }

    /// <summary>長い観測の空白 (ポーズ等) のあとは張り直す。</summary>
    [Test]
    public void AGapInObservationReanchors()
    {
        MediaRenderClock.Observe(1f, 0f);
        // 0.6 秒 Observe が呼ばれない (ポーズ中を想定)。
        MediaRenderClock.Observe(1.05f, 0.6f);
        Assert.AreEqual(1.05f, MediaRenderClock.RenderTime, 0.0001f);
    }

    /// <summary>
    /// Observe が一度も呼ばれていない / 古い場合は step_time に
    /// フォールバックする (テスト環境や MusicManager 不在の経路で
    /// 従来どおりの動きになること)。
    /// </summary>
    [Test]
    public void TheFallbackReturnsTheAuthoritativeStepTime()
    {
        // 未観測: 常にフォールバック。
        Assert.IsFalse(MediaRenderClock.IsFresh(1f));
        Assert.AreEqual(7.25f, MediaRenderClock.RenderTimeOrFallback(7.25f, 1f), 0.0001f);

        MediaRenderClock.Observe(1f, 0f);
        Assert.IsTrue(MediaRenderClock.IsFresh(0.1f));
        Assert.AreEqual(1f, MediaRenderClock.RenderTimeOrFallback(9f, 0.1f), 0.0001f,
            "while fresh, the interpolated value wins over the raw step");

        // 新鮮さが切れたらフォールバックへ戻る。
        Assert.IsFalse(MediaRenderClock.IsFresh(1f));
        Assert.AreEqual(9f, MediaRenderClock.RenderTimeOrFallback(9f, 1f), 0.0001f);
    }
}
