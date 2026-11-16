using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using FlashBeat.UI.V5;

namespace FlashBeat.Tests.Editor
{
    /// <summary>
    /// PlaySession (Key Guide / Lyrics) の寿命契約 (Issue #241)。
    ///
    /// これらは「このプレイの設定」= セッション設定であり、PlayerPrefs へは
    /// 保存しない。来場者交代の導線 (Result → Title) で
    /// <see cref="PlaySession.Reset"/> が既定値へ戻し、同じ人の
    /// Retry・選曲変更・Option 変更・Title への戻る操作
    /// (Option → Title / Select → Title) では値が保持される。
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
            // 同じ人の操作 (Option → Select → Game → Retry → Result → Select、
            // および Option → Title / Select → Title の戻る) は Reset が
            // 呼ばれない限り値は保持される。
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
            // Set* が PlayerPrefs へ書かないことを、旧キーの有無が
            // 前後で変わらないことで直接確認する (キーの事前存在に依存しない)。
            bool hadKeyGuide = PlayerPrefs.HasKey(PlaySession.KeyGuideKey);
            bool hadLyrics = PlayerPrefs.HasKey(PlaySession.LyricsKey);

            PlaySession.SetKeyGuide(KeyGuideMode.Full);
            PlaySession.SetLyricsEnabled(false);

            Assert.AreEqual(hadKeyGuide, PlayerPrefs.HasKey(PlaySession.KeyGuideKey),
                "Key Guide は PlayerPrefs に保存しない (セッション設定)");
            Assert.AreEqual(hadLyrics, PlayerPrefs.HasKey(PlaySession.LyricsKey),
                "Lyrics は PlayerPrefs に保存しない (セッション設定)");

            var type = typeof(PlaySession);
            Assert.IsNull(type.GetMethod("Save"), "PlaySession.Save は存在しない");
            Assert.IsNull(type.GetMethod("Load"), "PlaySession.Load は存在しない");
            Assert.IsNull(type.GetProperty("Storage"), "PlaySession.Storage は存在しない");
        }

        [Test]
        public void HandoverEntry_ResetsSessionSettings()
        {
            // 来場者境界: ResultSceneManager.TitleScene (Result → Title) が
            // PlaySession.Reset() を呼ぶことをソースで固定する。
            // TitleSceneManager は通常進入 (Option/Select の戻る) も担うため、
            // そこで Reset すると同じ人の Option 設定変更が捨てられる (#547)。
            string path = Path.Combine(
                Application.dataPath,
                "Game", "Scripts", "UI", "ResultSceneManager.cs");
            string src = File.ReadAllText(path);

            StringAssert.Contains("PlaySession.Reset()", src,
                "来場者交代の導線でセッション設定を既定値へ戻すこと");
            Assert.IsFalse(src.Contains("PlaySession.Load()"),
                "旧永続化の復元呼び出しは残さない");
        }

        [Test]
        public void Reset_IsOnlyCalledOnTheHandoverPath()
        {
            // 契約は「来場者交代 (Result → Title) だけが境界」。他の画面で
            // Reset() が足されると、同じ人が Title へ戻っただけで
            // Ready / Option で選んだ設定が消える回帰を検出する。
            string scriptsRoot = Path.Combine(
                Application.dataPath, "Game", "Scripts");
            string[] callers = Directory.GetFiles(scriptsRoot, "*.cs", SearchOption.AllDirectories)
                .Where(f => File.ReadAllText(f).Contains("PlaySession.Reset()"))
                .Select(Path.GetFileName)
                .OrderBy(n => n, System.StringComparer.Ordinal)
                .ToArray();

            Assert.AreEqual(new[] { "ResultSceneManager.cs" }, callers,
                "PlaySession.Reset() の呼び出しは来場者交代の導線だけのはず");
        }

        [Test]
        public void DeadPersistenceStorage_IsRemoved()
        {
            // issue-162 D-1 の PlayerPrefs 永続化は #241 で廃止。
            // IDisplaySettingsStorage / DisplaySettingsStorage の実装ファイルは残さない。
            string dir = Path.Combine(
                Application.dataPath, "Game", "Scripts", "Persistence");
            Assert.IsFalse(File.Exists(Path.Combine(dir, "DisplaySettingsStorage.cs")),
                "DisplaySettingsStorage.cs は削除済みであること");
        }
    }
}
