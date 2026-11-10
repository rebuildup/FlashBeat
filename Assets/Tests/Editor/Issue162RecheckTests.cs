using System.Collections.Generic;
using System.Reflection;
using FlashBeat.Persistence;
using FlashBeat.UI.V6.Game.Pause;
using FlashBeat.UI.V6.Result;
using NUnit.Framework;
using TMPro;
using UnityEngine;

namespace FlashBeat.Tests.Editor
{
    /// <summary>
    /// issue-162 §8 の再照合 (2026-11-10) で修正した項目の回帰テスト。
    ///
    /// - A-6: 固定テーブル外 (cloud 追加曲) のベストスコアが Hiscore スロット 0 を
    ///   共有していた。安定キー (remoteId@chartVersion) の保存先へ書く。
    /// - D-7: Result の Retry が自前のゼロ化 (settledNoteCount / プレイセッションの
    ///   固定を漏らす) から正本 GManager.ResetSession へ切り替わったこと。
    /// - B-11: v6 ポーズの「BGMVolume」行 (GameScene では何にも効かない) が廃止されたこと。
    /// </summary>
    public class Issue162RecheckTests
    {
        // --- A-6: 固定テーブル外のベストスコア ---

        private SongData[] originalCatalogSnapshot;
        private bool hadRemoteCatalog;
        private IScoreStorage previousStorage;

        [SetUp]
        public void SetUp()
        {
            hadRemoteCatalog = GManager.HasRemoteCatalog;
            originalCatalogSnapshot = GManager.RemoteSongs;

            // 各テストにまっさらな保存先を与える。共有のメモリ保存先をそのまま使うと、
            // 同じエディタセッションで再実行したときに前回の記録が残り、
            // 「初回の前回値は 0」の前提が崩れる (ScoreStoreTests と同じ規律)。
            previousStorage = ScorePersistence.Storage;
            ScorePersistence.Storage = new MemoryScoreStorage();
            ScorePersistence.Unload();
        }

        [TearDown]
        public void TearDown()
        {
            // カタログを元に戻す (他のテスト / 後続のプレイに影響しない)。
            if (originalCatalogSnapshot != null)
            {
                GManager.ApplyRemoteCatalog(originalCatalogSnapshot);
            }
            else if (!hadRemoteCatalog)
            {
                GManager.ClearRemoteCatalog();
            }

            ScorePersistence.Unload();
            ScorePersistence.Storage = previousStorage;
        }

        /// <summary>固定テーブル (29 曲) より後に並ぶ cloud 曲を含むカタログを作る。
        /// Hiscore 配列は 45 項目なので、index 45 に cloud 曲を置く。
        /// idPrefix: 保存先のキー (remoteId@version) をテストごとに隔離する。</summary>
        private static void ApplyCatalogWithCloudSongAt(int targetIndex, string idPrefix)
        {
            var remote = new List<SongData>
            {
                new SongData(0, "noSong", "no", "", 0, 0, 0, 0f),
            };
            for (int id = 1; remote.Count <= targetIndex; id++)
            {
                remote.Add(new SongData(id, idPrefix + id, "a6", "a6vid" + id, 120, 3, 42, 60f)
                {
                    isRemote = true,
                    remoteId = idPrefix + "-" + id,
                    chartVersion = 1,
                });
            }

            GManager.ApplyRemoteCatalog(remote.ToArray());
        }

        [Test]
        public void ApplyBestScore_BeyondTheFixedTable_StoresUnderTheStableKeyNotSlot0()
        {
            ApplyCatalogWithCloudSongAt(45, "a6t1");
            Assert.IsTrue(GManager.ActiveSongs[45].isRemote, "前提: index 45 は cloud 曲");

            int prev = ResultSceneManager.ApplyBestScore(45, 777);

            Assert.AreEqual(0, prev, "初回の前回値は 0");
            Assert.AreEqual(0, GManager.Hiscore[0], "スロット 0 (noSong) に書いてはいけない (A-6)");
            Assert.AreEqual(777, ScorePersistence.BestOf(45),
                "安定キー (remoteId@chartVersion) の保存先に書かれる");

            // 別回線の更新: 低いスコアではベストが消えず、前回値として返る。
            int prev2 = ResultSceneManager.ApplyBestScore(45, 500);
            Assert.AreEqual(777, prev2, "前回のベストが返る (別曲の値ではない)");
            Assert.AreEqual(777, ScorePersistence.BestOf(45), "低いスコアでベストが置き換わらない");

            // 別の cloud 曲をプレイしても、先の曲のベストは残る (スロット共有しない)。
            ResultSceneManager.ApplyBestScore(46, 9999);
            Assert.AreEqual(777, ScorePersistence.BestOf(45), "別曲の結果でベストが消えない");
            Assert.AreEqual(9999, ScorePersistence.BestOf(46));
        }

        [Test]
        public void V6ResultView_BestOf_BeyondTheFixedTable_ReadsTheStableStore()
        {
            ApplyCatalogWithCloudSongAt(45, "a6t2");
            ScorePersistence.SubmitForCatalogIndex(45, 4242);
            int originalSongId = GManager.songID;
            try
            {
                GManager.songID = 45;

                MethodInfo bestOf = typeof(V6ResultView).GetMethod("BestOf",
                    BindingFlags.NonPublic | BindingFlags.Static);
                Assert.IsNotNull(bestOf, "V6ResultView.BestOf が見つからない");

                Assert.AreEqual(4242, (int)bestOf.Invoke(null, null),
                    "固定テーブル外の曲はスロット 0 ではなく安定キーから読む (A-6)");
            }
            finally
            {
                GManager.songID = originalSongId;
            }
        }

        // --- D-7: Result の Retry は正本 ResetSession を使う ---

        [Test]
        public void ResetRunCounters_ClearsTheWholeSessionState_IncludingSettledNoteCount()
        {
            int originalSongId = GManager.songID;
            try
            {
                // 旧の自前実装が消していなかった状態を前もって汚す。
                GManager.score = 123456;
                GManager.combo = 42;
                GManager.maxCombo = 42;
                GManager.miss = 3;
                GManager.settledNoteCount = 99;

                ResultSceneManager.ResetRunCounters();

                Assert.AreEqual(0, GManager.score);
                Assert.AreEqual(0, GManager.combo);
                Assert.AreEqual(0, GManager.maxCombo);
                Assert.AreEqual(0, GManager.miss);
                Assert.AreEqual(0, GManager.settledNoteCount,
                    "リトライで settledNoteCount も消える (旧自前実装は残していた, D-7)");
            }
            finally
            {
                GManager.songID = originalSongId;
                PlaySessionSnapshot.Clear();
                GManager.ResetSession();
                PlaySessionSnapshot.Clear();
            }
        }

        // --- B-11: v6 ポーズの効かない BGMVolume 行 ---

        [Test]
        public void V6Pause_SoundTab_HasNoDeadBgmVolumeRow()
        {
            var parent = new GameObject("Issue162RecheckTests Pause Parent", typeof(RectTransform));
            V6PauseView view = null;
            try
            {
                view = V6PauseView.Create(parent.GetComponent<RectTransform>());
                Assert.IsNotNull(view, "v6 ポーズが生成される");

                string[] texts = AllTexts(view.Root.gameObject);
                Assert.That(texts, Has.None.EqualTo("BGMVolume"),
                    "GameScene で効かない BGMVolume 行を描かない (B-11)");
                Assert.That(texts, Has.Some.EqualTo("MainVolume"), "MainVolume の行は残る");
                Assert.That(texts, Has.Some.EqualTo("SEVolume"), "SEVolume の行は残る");
            }
            finally
            {
                if (view != null)
                {
                    view.Dispose();
                }
                Object.DestroyImmediate(parent);
            }
        }

        private static string[] AllTexts(GameObject root)
        {
            TextMeshProUGUI[] texts = root.GetComponentsInChildren<TextMeshProUGUI>(true);
            var values = new string[texts.Length];
            for (int i = 0; i < texts.Length; i++)
            {
                values[i] = texts[i].text;
            }
            return values;
        }
    }
}
