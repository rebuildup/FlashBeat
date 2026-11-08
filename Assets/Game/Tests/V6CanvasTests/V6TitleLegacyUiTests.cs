using NUnit.Framework;

namespace FlashBeat.UI.V6.Title.Tests
{
    /// <summary>
    /// v6 タイトルモードで旧 (v5) UI を隠す判定 (V6TitleLogic.IsLegacyTitleObjectName) の検証。
    ///
    /// 実機スクリーンショット (2560×1664) で旧タイトル UI (Menu / Logo / Catalog Status など)
    /// と v6 が二重表示された修正。対象名は TitleScene.unity 実測。
    /// v6 自身のオブジェクト名 ("V6 ...", "Tabs", "KeyCap") は絶対に一致させない
    /// (一致すると v6 自身を隠して画面が消える)。
    /// </summary>
    public sealed class V6TitleLegacyUiTests
    {
        // --- 旧タイトル UI の名前は隠す ---

        [Test]
        public void LegacySceneCanvas_AndStageZone_AreHidden()
        {
            Assert.IsTrue(V6TitleLogic.IsLegacyTitleObjectName("Canvas"));
            Assert.IsTrue(V6TitleLogic.IsLegacyTitleObjectName("Stage 16:9"));
            Assert.IsTrue(V6TitleLogic.IsLegacyTitleObjectName("BG"));
        }

        [Test]
        public void LegacyMenu_Logo_Catalog_AreHidden()
        {
            Assert.IsTrue(V6TitleLogic.IsLegacyTitleObjectName("Logo"));
            Assert.IsTrue(V6TitleLogic.IsLegacyTitleObjectName("Menu"));
            Assert.IsTrue(V6TitleLogic.IsLegacyTitleObjectName("Menu GameStart"));
            Assert.IsTrue(V6TitleLogic.IsLegacyTitleObjectName("Menu Option"));
            Assert.IsTrue(V6TitleLogic.IsLegacyTitleObjectName("Menu Exit"));
            Assert.IsTrue(V6TitleLogic.IsLegacyTitleObjectName("Catalog Status"));
        }

        [Test]
        public void LegacyDecorations_AreHidden()
        {
            Assert.IsTrue(V6TitleLogic.IsLegacyTitleObjectName("Edge Deco Left"));
            Assert.IsTrue(V6TitleLogic.IsLegacyTitleObjectName("Edge Deco Right"));
            Assert.IsTrue(V6TitleLogic.IsLegacyTitleObjectName("Bar V"));
            Assert.IsTrue(V6TitleLogic.IsLegacyTitleObjectName("Bar H"));
        }

        // --- v6 自身と無関係な名前は隠さない ---

        [Test]
        public void V6OwnObjectNames_AreNeverHidden()
        {
            Assert.IsFalse(V6TitleLogic.IsLegacyTitleObjectName("V6 Title Canvas"));
            Assert.IsFalse(V6TitleLogic.IsLegacyTitleObjectName("V6 Title View"));
            Assert.IsFalse(V6TitleLogic.IsLegacyTitleObjectName("V6 Title Frame 1728x972"));
            // v6 のタブ / キーキャップは旧 UI の "Menu" / "Key" と名前が違うことを固定する。
            Assert.IsFalse(V6TitleLogic.IsLegacyTitleObjectName("Tabs"));
            Assert.IsFalse(V6TitleLogic.IsLegacyTitleObjectName("KeyCap"));
        }

        [Test]
        public void UnrelatedNames_Null_Empty_Whitespace_AreNotHidden()
        {
            Assert.IsFalse(V6TitleLogic.IsLegacyTitleObjectName("BackGround"));
            Assert.IsFalse(V6TitleLogic.IsLegacyTitleObjectName("UpdateNoticeCanvas"));
            Assert.IsFalse(V6TitleLogic.IsLegacyTitleObjectName("FlashBeatUpdateNotice"));
            Assert.IsFalse(V6TitleLogic.IsLegacyTitleObjectName(null));
            Assert.IsFalse(V6TitleLogic.IsLegacyTitleObjectName(""));
            Assert.IsFalse(V6TitleLogic.IsLegacyTitleObjectName(" "));
        }
    }
}
