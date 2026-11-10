using FlashBeat.Persistence;
using FlashBeat.UI.V5;
using NUnit.Framework;

namespace FlashBeat.Tests.Editor
{
    /// <summary>
    /// issue-162 D-1: Key Guide / Lyrics の表示設定が永続化されること。
    ///
    /// 旧実装は静的フィールドだけだったため、アプリを再起動すると選んだ値が
    /// 既定 (Compact + 歌詞自動) に戻っていた (Settings タブの値は即時保存される
    /// のに、Display タブだけが消える非対称)。
    /// </summary>
    public class PlaySessionPersistenceTests
    {
        private IDisplaySettingsStorage previousStorage;

        [SetUp]
        public void SetUp()
        {
            // 各テストにまっさらな保存先を与える (前のテストの値が残らないように)。
            previousStorage = PlaySession.Storage;
            PlaySession.Storage = new MemoryDisplaySettingsStorage();
            PlaySession.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            PlaySession.Reset();
            PlaySession.Storage = previousStorage;
        }

        [Test]
        public void SetKeyGuide_IsRestoredAfterARestart()
        {
            PlaySession.SetKeyGuide(KeyGuideMode.Full);

            // 再起動 = 静的フィールドが既定値へ戻り、保存値から読み直す。
            PlaySession.Reset();
            Assert.AreEqual(KeyGuideMode.Compact, PlaySession.KeyGuide, "前提: Reset で既定値");

            PlaySession.Load();

            Assert.AreEqual(KeyGuideMode.Full, PlaySession.KeyGuide,
                "再起動後も Key Guide の選択が残る (D-1)");
        }

        [Test]
        public void SetLyricsEnabled_IsRestoredAfterARestart()
        {
            PlaySession.SetLyricsEnabled(false);

            PlaySession.Reset();
            Assert.IsTrue(PlaySession.LyricsEnabled, "前提: Reset で既定値 (歌詞あり)");

            PlaySession.Load();

            Assert.IsFalse(PlaySession.LyricsEnabled,
                "再起動後も Lyrics = Off が残る (D-1)");
        }

        [Test]
        public void Load_WithoutSavedValues_KeepsTheDefaults()
        {
            PlaySession.Load();

            Assert.AreEqual(KeyGuideMode.Compact, PlaySession.KeyGuide);
            Assert.IsTrue(PlaySession.LyricsEnabled);
        }

        [Test]
        public void Load_OfAnOutOfRangeKeyGuide_FallsBackToTheDefault()
        {
            // 古い形式 / 手で書き換えられた値を想定する。
            PlaySession.Storage.SetInt(PlaySession.KeyGuideKey, 99);
            PlaySession.Storage.SetInt(PlaySession.LyricsKey, 0);

            PlaySession.Load();

            Assert.AreEqual(KeyGuideMode.Compact, PlaySession.KeyGuide,
                "成立しない保存値は既定値へ寄せる (例外にしない)");
            Assert.IsFalse(PlaySession.LyricsEnabled, "Lyrics は保存値どおり (0 = Off)");
        }

        [Test]
        public void NextKeyGuide_ThenSet_WritesTheSameModeItShows()
        {
            // 実際の UI 経路 (Ready / Pause / Option の ←→) と同じ順序で回す。
            KeyGuideMode next = PlaySession.NextKeyGuide(PlaySession.KeyGuide, 1);
            PlaySession.SetKeyGuide(next);
            Assert.AreEqual("Full", PlaySession.KeyGuideLabel(next));

            int stored = PlaySession.Storage.GetInt(PlaySession.KeyGuideKey, -1);
            Assert.AreEqual((int)KeyGuideMode.Full, stored, "表示したモードがそのまま保存される");

            PlaySession.Reset();
            PlaySession.Load();
            Assert.AreEqual(KeyGuideMode.Full, PlaySession.KeyGuide);
        }

        [Test]
        public void Reset_DoesNotEraseTheSavedChoice()
        {
            PlaySession.SetKeyGuide(KeyGuideMode.Off);

            // プレイのやり直し (ResetSession) で利用者の選択を消さない。
            PlaySession.Reset();
            PlaySession.Load();

            Assert.AreEqual(KeyGuideMode.Off, PlaySession.KeyGuide,
                "Reset はセッション値を戻すだけで、保存値には触らない");
        }

        [Test]
        public void Storage_Null_IsReplacedByThePlayerPrefsStorage()
        {
            // 誤って null を入れても保存が止まらないようにする
            // (ScorePersistence.Storage と同じ規律)。
            PlaySession.Storage = null;
            Assert.IsInstanceOf<PlayerPrefsDisplaySettingsStorage>(PlaySession.Storage);
        }
    }
}
