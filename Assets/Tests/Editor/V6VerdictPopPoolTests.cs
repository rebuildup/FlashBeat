using System.Collections.Generic;
using FlashBeat.UI.V6.Common;
using FlashBeat.UI.V6.Game.Hud;
using NUnit.Framework;
using TMPro;
using UnityEngine;

namespace FlashBeat.Tests
{
    /// <summary>
    /// <see cref="V6VerdictPopPool"/> の単体検証 (Issue #793)。
    /// 判定イベントごとの独立インスタンス、同一レーン連打での共存、
    /// スロット上限時の最古回収、寿命切れの解放を確認する。
    /// </summary>
    public class V6VerdictPopPoolTests
    {
        private readonly List<GameObject> spawned = new List<GameObject>();
        private Transform parent;

        [SetUp]
        public void SetUp()
        {
            var go = new GameObject("VerdictPopPoolTests");
            parent = go.transform;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in spawned)
            {
                if (go != null) Object.DestroyImmediate(go);
            }
            spawned.Clear();
            if (parent != null) Object.DestroyImmediate(parent.gameObject);
        }

        private V6VerdictPopPool NewPool()
        {
            return new V6VerdictPopPool(Judge.LaneCount,
                (lane, slot) =>
                {
                    var go = new GameObject($"Verdict {lane} {slot}",
                        typeof(RectTransform));
                    go.transform.SetParent(parent, false);
                    var tmp = go.AddComponent<TextMeshProUGUI>();
                    spawned.Add(go);
                    return tmp;
                });
        }

        [Test]
        public void Spawn_SameLaneTwice_KeepsBothInstances()
        {
            var pool = NewPool();

            var e1 = pool.Spawn(2, "PERFECT", Color.white, 10f);
            var e2 = pool.Spawn(2, "GREAT", Color.gray, 10.05f);

            Assert.IsNotNull(e1);
            Assert.IsNotNull(e2);
            Assert.AreNotSame(e1, e2,
                "同一レーンの連続判定は別インスタンス (上書きしない)");
            Assert.AreEqual("PERFECT", e1.Text.text);
            Assert.AreEqual("GREAT", e2.Text.text);
            Assert.AreEqual(2, pool.ActiveCount);
            Assert.IsTrue(e1.Active && e2.Active);
            Assert.IsTrue(e1.Text.gameObject.activeSelf);
            Assert.IsTrue(e2.Text.gameObject.activeSelf);
        }

        [Test]
        public void Tick_ReleasesExpiredEntries()
        {
            var pool = NewPool();
            pool.Spawn(0, "PERFECT", Color.white, 10f);
            pool.Spawn(1, "MISS", Color.red, 10f);

            pool.Tick(10f + V6HudLogic.VerdictTotalSeconds - 0.01f,
                V6HudLogic.VerdictTotalSeconds);
            Assert.AreEqual(2, pool.ActiveCount, "寿命内は残る");

            pool.Tick(10f + V6HudLogic.VerdictTotalSeconds + 0.01f,
                V6HudLogic.VerdictTotalSeconds);
            Assert.AreEqual(0, pool.ActiveCount, "寿命切れは解放される");
        }

        [Test]
        public void Spawn_BeyondSlotLimit_RecyclesOldestOnSameLane()
        {
            var pool = NewPool();
            for (int i = 0; i < V6VerdictPopPool.SlotsPerLane + 2; i++)
            {
                pool.Spawn(3, "P" + i, Color.white, 10f + i * 0.01f);
            }

            Assert.AreEqual(V6VerdictPopPool.SlotsPerLane, pool.ActiveCount,
                "同一レーンの同時表示はスロット数で有界");

            // 最古 2 件 (P0, P1) は回収され、残るのは直近 4 件 (P2..P5)。
            var labels = new List<string>();
            foreach (var e in pool.ActiveEntries)
            {
                labels.Add(e.Text.text);
            }
            labels.Sort();
            CollectionAssert.AreEqual(
                new[] { "P2", "P3", "P4", "P5" }, labels.ToArray(),
                "最古の表示から順に回収され、最新の表示が残る");
        }

        [Test]
        public void Spawn_DifferentLanes_DoNotShareSlots()
        {
            var pool = NewPool();
            pool.Spawn(0, "PERFECT", Color.white, 10f);
            pool.Spawn(Judge.LaneCount - 1, "PERFECT", Color.white, 10f);

            Assert.AreEqual(2, pool.ActiveCount);
            Assert.IsNotNull(pool.LatestActiveOnLane(0));
            Assert.IsNotNull(pool.LatestActiveOnLane(Judge.LaneCount - 1));
            Assert.IsNull(pool.LatestActiveOnLane(4));
        }

        [Test]
        public void Clear_ReleasesAllActive()
        {
            var pool = NewPool();
            pool.Spawn(0, "PERFECT", Color.white, 10f);
            pool.Spawn(5, "MISS", Color.red, 10f);

            pool.Clear();
            Assert.AreEqual(0, pool.ActiveCount);
            foreach (var e in pool.AllEntries)
            {
                Assert.IsFalse(e.Active);
            }
        }

        [Test]
        public void SpawnedEntry_CanBeReusedAfterExpiry()
        {
            var pool = NewPool();
            var e1 = pool.Spawn(2, "PERFECT", Color.white, 10f);
            pool.Tick(10f + V6HudLogic.VerdictTotalSeconds + 0.1f,
                V6HudLogic.VerdictTotalSeconds);

            var e2 = pool.Spawn(2, "GREAT", Color.gray, 20f);
            Assert.AreSame(e1, e2, "寿命切れスロットは再利用される (Instantiate 無し)");
            Assert.AreEqual("GREAT", e2.Text.text);
            Assert.AreEqual(20f, e2.StartTime);
        }
    }
}
