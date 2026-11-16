using System.IO;
using NUnit.Framework;
using FlashBeat.UI.V5;

namespace FlashBeat.Tests.Editor
{
    /// <summary>
    /// PlaySession (Key Guide / Lyrics) の寿命契約 (Issue #241)。
    ///
    /// これらは「このプレイの設定」= セッション設定であり、PlayerPrefs へは
    /// 保存しない。Title 進入 (来場者の交代境界) で <see cref="PlaySession.Reset"/>
    /// が既定値へ戻し、同じ人の Retry・選曲変更・Option 変更 (Title を通らない
    /// 遷移) では値が保持される。
    ///
    /// 設計の対応表は docs/operations/issue-241-session-lifetime-design.md。
    /// </summary>
    public class PlaySessionLifetimeTests
    {
        [SetUp]
        public void SetUp()
        {
            PlaySession.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            PlaySession.Reset();
        }

        [Test]
        public void Setters_ChangeValues_WithinTheSession()
        {
            PlaySession.SetKeyGuide(KeyGuideMode.Full);
            PlaySession.SetLyricsEnabled(false);

            Assert.AreEqual(KeyGuideMode.Full, PlaySession.KeyGuide);
            Assert.IsFalse(PlaySession.LyricsEnabled);
        }

        [Test]
        public void Reset_RestoresSessionDefaults()
        {
            PlaySession.SetKeyGuide(KeyGuideMode.Off);
            PlaySession.SetLyricsEnabled(false);

            PlaySession.Reset();

            Assert.AreEqual(KeyGuideMode.Compact, PlaySession.KeyGuide);
            Assert.IsTrue(PlaySession.LyricsEnabled);
        }

        [Test]
        public void Values_SurviveUntilReset_SameUserFlow()
        {
            // 同じ人の操作 (Option → Select → Game → Retry → Result → Select)
            // は Title を通らないので、Reset が呼ばれない限り値は保持される。
            PlaySession.SetKeyGuide(KeyGuideMode.Full);
            PlaySession.SetLyricsEnabled(false);

            // 「画面遷移」を模倣する間に値が消えないことを固定する。
            Assert.AreEqual(KeyGuideMode.Full, PlaySession.KeyGuide);
            Assert.IsFalse(PlaySession.LyricsEnabled);
        }

        [Test]
        public void SessionSettings_AreNotPersistedToPlayerPrefs()
        {
            // 永続化 API (Storage / Save / Load) は #241 で除去した。
            // Set* が PlayerPrefs へ書かないことを、旧キーが増えないことで確認する
            // (開発者環境に残る既存キーの有無には依存しない)。
            PlaySession.SetKeyGuide(KeyGuideMode.Full);
            PlaySession.SetLyricsEnabled(false);

            var type = typeof(PlaySession);
            Assert.IsNull(type.GetMethod("Save"), "PlaySession.Save は存在しない");
            Assert.IsNull(type.GetMethod("Load"), "PlaySession.Load は存在しない");
            Assert.IsNull(type.GetProperty("Storage"), "PlaySession.Storage は存在しない");
        }

        [Test]
        public void TitleEntry_ResetsSessionSettings()
        {
            // 来場者境界: TitleSceneManager.Start が PlaySession.Load() ではなく
            // PlaySession.Reset() を呼ぶことをソースで固定する
            // (Title 進入 = セッション開始。Load による旧保存値の復元は #241 で廃止)。
            string path = Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets", "Game", "Scripts", "UI", "TitleSceneManager.cs");
            string src = File.ReadAllText(path);

            StringAssert.Contains("PlaySession.Reset()", src,
                "Title 進入でセッション設定を既定値へ戻すこと");
            Assert.IsFalse(src.Contains("PlaySession.Load()"),
                "旧永続化の復元呼び出しは残さない");
        }

        [Test]
        public void DeadPersistenceStorage_IsRemoved()
        {
            // issue-162 D-1 の PlayerPrefs 永続化は #241 で廃止。
            // IDisplaySettingsStorage / DisplaySettingsStorage の実装ファイルは残さない。
            string dir = Path.Combine(
                Directory.GetCurrentDirectory(), "Assets", "Game", "Scripts", "Persistence");
            Assert.IsFalse(File.Exists(Path.Combine(dir, "DisplaySettingsStorage.cs")),
                "DisplaySettingsStorage.cs は削除済みであること");
        }
    }
}
