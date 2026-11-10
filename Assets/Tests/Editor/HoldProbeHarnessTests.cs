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
}
