using System.Collections.Generic;
using FlashBeat.Gameplay;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// HOLD 実機検証ハーネス (Issue #278) の **純粋な部分** の EditMode テスト。
///
/// 実機 (macOS / Windows プレイヤー) で見ている 2 つの罠を here で固定する。
///
/// 1. <see cref="NotesManager"/> は実ノーツの末尾に **番兵 10 個** (type = TAP,
///    time = 10000) を足す。ノーツ種別を配列全体で数えるとタップが 10 個多く
///    なり、譜面が 7 ノーツでも「タップ 14 / HOLD 3」と誤る。
/// 2. HOLD の保持フェーズは「キーが **押されたまま** いるか」を毎フレーム見る。
///    押下の 1 フレームだけ注入しても保持できないので、保持状態の注入が要る。
///    (実測: 2026-11-10 macOS run 2 で、HOLD が 3 個とも Broken になった。)
/// </summary>
public class HoldProbeHarnessTests
{
    /// <summary>キー割り当てを読み込まないための偽ストレージ。</summary>
    private class FakeStorage : KeyBindings.IStorage
    {
        public string LoadRaw() { return null; }
        public void SaveRaw(string json) { }
    }

    [SetUp]
    public void SetUp()
    {
        // 開発環境の設定ファイルでレーンの割り当てが変わった状態で
        // テストが壊れないよう、既定値に固定する。
        KeyBindings.SetStorage(new FakeStorage());
        KeyBindings.ResetToDefaults();
        KeyBindings.ClearInjectedKeyHolds();
    }

    [TearDown]
    public void TearDown()
    {
        KeyBindings.ClearInjectedKeyHolds();
    }

    // ---- ノーツ種別の数え方 (番兵 10 個) ----------------------------------

    [Test]
    public void CountNoteTypes_CountsOnlyTheRealNotes()
    {
        // 検証譜面と同じ並び: タップ 4 + HOLD 3 のあとに番兵 10 個。
        var types = new List<int>
        {
            LongNote.TYPE_HOLD,
            LongNote.TYPE_TAP,
            LongNote.TYPE_HOLD,
            LongNote.TYPE_TAP,
            LongNote.TYPE_HOLD,
            LongNote.TYPE_TAP,
            LongNote.TYPE_TAP,
        };
        for (int i = 0; i < 10; i++) { types.Add(LongNote.TYPE_TAP); }

        AutoTestV6Hold.CountNoteTypes(types, HoldProbeChart.TotalNotes, out int taps, out int holds);

        Assert.AreEqual(HoldProbeChart.TapCount, taps, "タップ数は番兵を除いた個数になる");
        Assert.AreEqual(HoldProbeChart.HoldCount, holds);
    }

    [Test]
    public void CountNoteTypes_WithoutTheRealCount_SeesTheSentinels()
    {
        // 対照: 実ノーツの個数を指定しないと番兵 10 個も数える。
        var types = new List<int> { LongNote.TYPE_TAP, LongNote.TYPE_TAP };
        for (int i = 0; i < 10; i++) { types.Add(LongNote.TYPE_TAP); }

        AutoTestV6Hold.CountNoteTypes(types, types.Count, out int taps, out int holds);

        Assert.AreEqual(12, taps, "番兵を数えるとタップが 10 個多く見える");
        Assert.AreEqual(0, holds);
    }

    [Test]
    public void CountNoteTypes_RealCountLargerThanTheArray_ClampsToTheArray()
    {
        var types = new List<int> { LongNote.TYPE_HOLD, LongNote.TYPE_TAP };

        AutoTestV6Hold.CountNoteTypes(types, 99, out int taps, out int holds);

        Assert.AreEqual(1, taps);
        Assert.AreEqual(1, holds);
    }

    // ---- 保持状態の注入 (HOLD の保持フェーズ) ------------------------------

    [Test]
    public void InjectedHold_MakesTheLaneHeld()
    {
        Assert.IsFalse(KeyBindings.IsLaneKeyHeld(0), "注入していないときは押されていない");

        KeyBindings.InjectKeyHold(KeyCode.A);

        Assert.IsTrue(KeyBindings.IsLaneKeyHeld(0), "注入したキーは保持されている");
    }

    [Test]
    public void ReleasedHold_MakesTheLaneNotHeld()
    {
        KeyBindings.InjectKeyHold(KeyCode.A);
        Assert.IsTrue(KeyBindings.IsLaneKeyHeld(0));

        KeyBindings.ReleaseInjectedKeyHold(KeyCode.A);

        Assert.IsFalse(KeyBindings.IsLaneKeyHeld(0), "解除したら保持は終わる");
    }

    [Test]
    public void InjectedHold_DoesNotCreateAPress()
    {
        // 保持の注入は「押下」ではない。押下の 1 フレーム判定
        // (GetKeyDownOrInjected) には影響しない。
        KeyBindings.InjectKeyHold(KeyCode.A);

        Assert.IsFalse(KeyBindings.GetKeyDownOrInjected(KeyCode.A),
            "保持の注入は押下として扱わない");
    }

    [Test]
    public void ClearInjectedHolds_ReleasesEveryLane()
    {
        KeyBindings.InjectKeyHold(KeyCode.A);
        KeyBindings.InjectKeyHold(KeyCode.D);

        KeyBindings.ClearInjectedKeyHolds();

        Assert.IsFalse(KeyBindings.IsLaneKeyHeld(0));
        Assert.IsFalse(KeyBindings.IsLaneKeyHeld(2));
    }

    // ---- 注入 → 判定 → 保持ウィンドウの連結 (issue #59) --------------------
    //
    // 実機の保持フェーズは「Judge がそのレーンのキーが押されているか
    // (Judge.IsLaneKeyDown → KeyBindings.IsLaneKeyHeld)」を毎フレーム
    // JudgeQueue.UpdateHold へ渡し、時刻判定は LongNote が単一の出所。
    // LongNote / JudgeQueue は Input に依存しない純粋クラスなので、
    // 実プレイヤーなしでここまでの連結を検証できる。

    /// <summary>
    /// ハーネスの注入が Judge の保持判定に届くこと。Judge 側は private なので
    /// reflection で呼ぶ (実装が KeyBindings の注入を見なくてもコンパイルは
    /// 通ってしまうため、配線をここで固定する)。
    /// </summary>
    [Test]
    public void TheInjectedHoldReachesTheJudgeSeam()
    {
        var judgeSeesKeyDown = typeof(Judge).GetMethod("IsLaneKeyDown",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
        Assert.IsNotNull(judgeSeesKeyDown, "Judge.IsLaneKeyDown が見つからない");
        var laneDown = (System.Func<int, bool>)System.Delegate.CreateDelegate(
            typeof(System.Func<int, bool>), judgeSeesKeyDown);

        KeyBindings.InjectKeyHold(KeyCode.A);
        Assert.IsTrue(laneDown(0), "注入した保持が Judge の保持判定に届く");

        KeyBindings.ReleaseInjectedKeyHold(KeyCode.A);
        Assert.IsFalse(laneDown(0), "解除すると Judge の保持判定も離れる");
    }

    /// <summary>
    /// 保持ウィンドウの時刻判定 (LongNote が単一の出所): 開始ちょうどと
    /// 終端ちょうどは保持中、その外は保持中ではない。
    /// </summary>
    [Test]
    public void TheHoldWindowIsInclusiveAtBothEnds()
    {
        const float start = 10f;
        const float end = 12.5f;

        Assert.IsFalse(LongNote.IsHolding(9.99f, start, end), "開始の前は保持中ではない");
        Assert.IsTrue(LongNote.IsHolding(start, start, end), "開始ちょうどは保持中");
        Assert.IsTrue(LongNote.IsHolding(11f, start, end), "区間の途中は保持中");
        Assert.IsTrue(LongNote.IsHolding(end, start, end), "終端ちょうどは保持中");
        Assert.IsFalse(LongNote.IsHolding(12.51f, start, end), "終端の後は保持中ではない");
    }

    /// <summary>
    /// 保持フェーズの状態遷移 (実プレイヤーなしで到達できる範囲):
    /// 注入した保持のままでは保持が続き、離すと終端からの距離で
    /// Released / Broken が決まり、押したまま終端 + 許容幅をすぎると Expired。
    /// </summary>
    [Test]
    public void TheHoldPhaseTransitionsFollowTheInjectedKeyAndTheClock()
    {
        const int lane = 0;
        const float greatWindow = LongNote.GREAT_WINDOW;

        var queue = new JudgeQueue(8);
        queue.BeginHold(new JudgeQueue.NoteEntry
        {
            Time = 10f,
            EndTime = 12.5f,
            Type = LongNote.TYPE_HOLD,
            Lane = lane,
            SourceIndex = 0,
        });
        Assert.IsTrue(queue.IsHolding(lane), "開始判定の後は保持中");

        // 注入した保持のままなら、区間中は何も確定しない。
        KeyBindings.InjectKeyHold(KeyCode.A);
        Assert.AreEqual(JudgeQueue.HoldResult.None,
            queue.UpdateHold(lane, 11f, KeyBindings.IsLaneKeyHeld(lane), greatWindow, out var settled),
            "保持中のレーンは次の判定を生まない");
        Assert.IsTrue(queue.IsHolding(lane));

        // 終端の許容幅内で離す → 成功。
        KeyBindings.ReleaseInjectedKeyHold(KeyCode.A);
        Assert.AreEqual(JudgeQueue.HoldResult.Released,
            queue.UpdateHold(lane, 12.6f, KeyBindings.IsLaneKeyHeld(lane), greatWindow, out settled),
            "終端の許容幅内の離しは成功");
        Assert.IsFalse(queue.IsHolding(lane), "確定したら保持は終わる");

        // 許容幅より前の離し → 中断 (BAD)。
        var queue2 = new JudgeQueue(8);
        queue2.BeginHold(new JudgeQueue.NoteEntry { Time = 10f, EndTime = 12.5f, Type = LongNote.TYPE_HOLD, Lane = lane, SourceIndex = 1 });
        Assert.AreEqual(JudgeQueue.HoldResult.Broken,
            queue2.UpdateHold(lane, 11.5f, false, greatWindow, out settled),
            "許容幅より前の離しは中断");

        // 押したまま終端 + 許容幅をすぎた → 成功扱い (離し忘れ)。
        var queue3 = new JudgeQueue(8);
        queue3.BeginHold(new JudgeQueue.NoteEntry { Time = 10f, EndTime = 12.5f, Type = LongNote.TYPE_HOLD, Lane = lane, SourceIndex = 2 });
        KeyBindings.InjectKeyHold(KeyCode.A);
        Assert.AreEqual(JudgeQueue.HoldResult.Expired,
            queue3.UpdateHold(lane, 12.5f + greatWindow + 0.01f, KeyBindings.IsLaneKeyHeld(lane), greatWindow, out settled),
            "押し続けたままの終端超過は成功扱い");
    }
}
