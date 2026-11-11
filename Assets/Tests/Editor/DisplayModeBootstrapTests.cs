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
