using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using FlashBeat.Common;

namespace FlashBeat.Tests
{
    /// <summary>
    /// Issue #369: 起動時全画面の補正ロジック。
    /// PlayerPrefs に保存された表示モードは ProjectSettings より優先されるため、
    /// FullScreenWindow 以外を検出して補正できることを固定する。
    /// 実機の全画面ピクセル被覆は Mac / Win64 の受入で確認する。
    /// </summary>
    public class DisplayModeBootstrapTests
    {
        [Test]
        public void NeedsCorrection_IsTrueForEveryModeExceptFullScreenWindow()
        {
            Assert.IsTrue(DisplayModePolicy.NeedsCorrection(FullScreenMode.ExclusiveFullScreen));
            Assert.IsTrue(DisplayModePolicy.NeedsCorrection(FullScreenMode.MaximizedWindow));
            Assert.IsTrue(DisplayModePolicy.NeedsCorrection(FullScreenMode.Windowed));
            Assert.IsFalse(DisplayModePolicy.NeedsCorrection(FullScreenMode.FullScreenWindow));
        }

        [Test]
        public void RequiredMode_IsBorderlessFullScreenWindow()
        {
            Assert.AreEqual(FullScreenMode.FullScreenWindow, DisplayModePolicy.RequiredMode);
            Assert.AreEqual(1, (int)DisplayModePolicy.RequiredMode,
                "PlayerPrefs のシリアライズ値 1 が FullScreenWindow であること");
        }

        [Test]
        public void StoredModeIsStale_DetectsMacWindowedValue()
        {
            // Mac 実機の PlayerPrefs に残っていた値 (MaximizedWindow=2)。
            Assert.IsTrue(DisplayModePolicy.StoredModeIsStale(2));
            Assert.IsTrue(DisplayModePolicy.StoredModeIsStale(3));
            Assert.IsTrue(DisplayModePolicy.StoredModeIsStale(0));
            Assert.IsFalse(DisplayModePolicy.StoredModeIsStale(1));
        }

        [Test]
        public void StoredModeKey_MatchesUnityPlayerPrefsName()
        {
            Assert.AreEqual("Screenmanager Fullscreen mode", DisplayModePolicy.StoredModeKey);
        }

        [Test]
        public void StoredScreenKeys_AreUniqueAndIncludeMode()
        {
            var keys = DisplayModePolicy.StoredScreenKeys;
            Assert.Contains(DisplayModePolicy.StoredModeKey, keys);

            for (int i = 0; i < keys.Length; i++)
            {
                for (int j = i + 1; j < keys.Length; j++)
                {
                    Assert.AreNotEqual(keys[i], keys[j], "重複した保存キー: " + keys[i]);
                }
            }
        }

        [Test]
        public void FillsDisplay_RequiresFullPixelCoverage()
        {
            // Mac 実機 (#369): メニューバー分 2224−2148 が欠けた作業領域サイズ。
            Assert.IsFalse(DisplayModePolicy.FillsDisplay(3420, 2148, 3420, 2224),
                "高さが足りない = 全画面ではない");
            Assert.IsTrue(DisplayModePolicy.FillsDisplay(3420, 2224, 3420, 2224));
            Assert.IsTrue(DisplayModePolicy.FillsDisplay(1920, 1080, 1920, 1080));
        }

        [Test]
        public void FillsDisplay_DoesNotCorrectWhenDisplaySizeIsUnknown()
        {
            Assert.IsTrue(DisplayModePolicy.FillsDisplay(1920, 1080, 0, 0),
                "ディスプレイ情報が取れない環境ではトグルしない");
        }

        [Test]
        public void SavedResolutionIsStale_DetectsWorkAreaSizedSave()
        {
            // ウィンドウ表示で終了した端末の保存値 (3420x2082) はネイティブ (3420x2224) より小さい。
            Assert.IsTrue(DisplayModePolicy.SavedResolutionIsStale(3420, 2082, 3420, 2224));
            Assert.IsFalse(DisplayModePolicy.SavedResolutionIsStale(3420, 2224, 3420, 2224),
                "ネイティブと同じ保存値は残す");
            Assert.IsFalse(DisplayModePolicy.SavedResolutionIsStale(0, 0, 3420, 2224),
                "保存値が無い場合は触らない");
            Assert.IsFalse(DisplayModePolicy.SavedResolutionIsStale(3420, 2082, 0, 0),
                "ディスプレイ情報が無い場合は触らない");
        }

        [Test]
        public void Apply_RunsBeforeFirstSceneLoad()
        {
            var method = typeof(DisplayModeBootstrap).GetMethod(
                "Apply", BindingFlags.Public | BindingFlags.Static);
            Assert.IsNotNull(method, "DisplayModeBootstrap.Apply が見つからない");

            var attribute = method.GetCustomAttributesData()
                .FirstOrDefault(a => a.AttributeType == typeof(RuntimeInitializeOnLoadMethodAttribute));
            Assert.IsNotNull(attribute, "Apply に RuntimeInitializeOnLoadMethod が必要");
            Assert.AreEqual(1, attribute.ConstructorArguments.Count,
                "読み込みタイミングを明示したコンストラクタを使うこと");
            Assert.AreEqual(RuntimeInitializeLoadType.BeforeSceneLoad,
                (RuntimeInitializeLoadType)attribute.ConstructorArguments[0].Value,
                "表示モードは最初のシーンのカメラ初期化より前に揃える");
        }
    }
}
