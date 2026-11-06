using NUnit.Framework;
using UnityEngine;

namespace FlashBeat.UI.V5
{
    /// <summary>
    /// Issue #120: Title/Option v5 View の純ロジック (表示書式・クランプ・循環・折り返し)。
    /// </summary>
    public static class TitleOptionV5Tests
    {
        [Test]
        public static void CatalogLabel_ShowsRemoteAndLocalWording()
        {
            Assert.AreEqual("オンライン · 42曲", TitleView.CatalogLabel(true, 42));
            Assert.AreEqual("ローカル · 29曲", TitleView.CatalogLabel(false, 29));
        }

        [Test]
        public static void AdjustVolume_StepsAndClamps()
        {
            Assert.AreEqual(0.5f, OptionView.AdjustVolume(0.4f, 1), 0.001f);
            Assert.AreEqual(0.3f, OptionView.AdjustVolume(0.4f, -1), 0.001f);
            Assert.AreEqual(0f, OptionView.AdjustVolume(0f, -1), 0.001f);
            Assert.AreEqual(1f, OptionView.AdjustVolume(0.95f, 1), 0.001f);
            // 浮動小数点の蓄積誤差を丸めで防ぐ
            Assert.AreEqual(0.7f, OptionView.AdjustVolume(0.30000001f, 4), 0.001f);
        }

        [Test]
        public static void FormatVolume_IsIntegerPercent()
        {
            Assert.AreEqual("100%", OptionView.FormatVolume(1f));
            Assert.AreEqual("30%", OptionView.FormatVolume(0.3f));
            Assert.AreEqual("0%", OptionView.FormatVolume(0f));
        }

        [Test]
        public static void FormatSpeed_KeepsOneDecimal()
        {
            Assert.AreEqual("10.0", OptionView.FormatSpeed(10f));
            Assert.AreEqual("10.1", OptionView.FormatSpeed(10.1f));
        }

        [Test]
        public static void CycleFlashSpeed_CyclesPowerOfTwoSteps()
        {
            Assert.AreEqual(2, OptionView.CycleFlashSpeed(1, 1));
            Assert.AreEqual(4, OptionView.CycleFlashSpeed(2, 1));
            Assert.AreEqual(8, OptionView.CycleFlashSpeed(4, 1));
            Assert.AreEqual(1, OptionView.CycleFlashSpeed(8, 1));
            Assert.AreEqual(8, OptionView.CycleFlashSpeed(1, -1));
            // 既定値 4 以外の値が入っていても壊れない (1 に戻して次へ)
            Assert.AreEqual(2, OptionView.CycleFlashSpeed(3, 1));
        }

        [Test]
        public static void WrapIndex_WrapsAround()
        {
            Assert.AreEqual(2, OptionView.WrapIndex(0, 3, -1));
            Assert.AreEqual(0, OptionView.WrapIndex(2, 3, 1));
            Assert.AreEqual(1, OptionView.WrapIndex(1, 3, 0));
            Assert.AreEqual(0, OptionView.WrapIndex(0, 0, 1));
        }

        [Test]
        public static void KeyLayoutLabel_ReflectsSavedBindings()
        {
            PlayerPrefs.DeleteKey(OptionView.KeyBindingsPrefsKey);
            Assert.AreEqual("標準", OptionView.KeyLayoutLabel());
            PlayerPrefs.SetInt(OptionView.KeyBindingsPrefsKey, 1); // ダミー値 (存在判定のみ)
            Assert.AreEqual("カスタム", OptionView.KeyLayoutLabel());
            PlayerPrefs.DeleteKey(OptionView.KeyBindingsPrefsKey);
        }
    }
}
