using System.IO;
using FlashBeat.UI.V6.Common;
using FlashBeat.UI.V6.Option;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace FlashBeat.Tests.Editor
{
    /// <summary>
    /// 2026-11-12 オーナー指摘の Option キー一覧と、ボタン押下の手応え。
    ///
    /// - Option のキー一覧は **キー名** を出す。以前はレーン番号を出していたため、
    ///   同じレーンに属するキーが同じ数字になり「1 2 3 4 4 5 5 6 7 8 8 8」のように
    ///   崩れて見えていた。
    /// - ボタンを押すと輪郭が外へ広がって消える (地味な手応え)。
    /// </summary>
    public class OptionKeyGridAndPressFeedbackTests
    {
        // --- キー一覧 ---

        [Test]
        public void KeyGridShowsKeyNamesNotLaneNumbers()
        {
            // ソース走査: キーキャップの中身が KeyDisplayName になっていること。
            string src = File.ReadAllText(Path.Combine(
                Application.dataPath, "Game/Scripts/UI/V6/Option/V6OptionView.cs"));

            Assert.IsTrue(src.Contains("V6OptionLogic.KeyDisplayName(keys[k])"),
                "キーキャップにキー名を出していない");
            Assert.IsFalse(src.Contains("DisplayLaneOf(keys[k], table)"),
                "キーキャップにレーン番号を出している (崩れの原因)");
        }

        [Test]
        public void KeyGridCoversEveryJisKey()
        {
            int total = 0;
            for (int r = 0; r < V6OptionLogic.JisGridKeys.Length; r++)
            {
                total += V6OptionLogic.JisGridKeys[r].Length;
            }

            // design のキー一覧は 13 / 12 / 12 / 11 の 4 段 = 48 キー。
            Assert.AreEqual(48, total, "design のキー数と一致する");
        }

        [Test]
        public void KeyNamesAreUniquePerRowPosition()
        {
            // 各行の中でキー名が重複しないこと (重複 = 崩れた表示)。
            for (int r = 0; r < V6OptionLogic.JisGridKeys.Length; r++)
            {
                KeyCode[] row = V6OptionLogic.JisGridKeys[r];
                var seen = new System.Collections.Generic.HashSet<string>();
                for (int k = 0; k < row.Length; k++)
                {
                    string name = V6OptionLogic.KeyDisplayName(row[k]);
                    Assert.IsTrue(seen.Add(name),
                        "行 " + r + " でキー名が重複している: " + name);
                }
            }
        }

        [Test]
        public void KeyGridRowsMatchTheDesignStagger()
        {
            // design: 上段 8 / 2 段目 25 / 3 段目 42 / 4 段目 59 (キーボードの段差)。
            Assert.AreEqual(4, V6OptionLogic.GridRowY.Length);
            Assert.AreEqual(4, V6OptionLogic.GridRowX.Length);
            Assert.Less(V6OptionLogic.GridRowX[0], V6OptionLogic.GridRowX[3],
                "下の段ほど右へずれる");
        }

        // --- ボタン押下の手応え ---

        private GameObject root;

        [TearDown]
        public void TearDown()
        {
            if (root != null)
            {
                Object.DestroyImmediate(root);
                root = null;
            }
        }

        private V6ButtonPressFeedback MakeButton()
        {
            root = new GameObject("Press Probe", typeof(RectTransform));
            var feedback = root.AddComponent<V6ButtonPressFeedback>();
            // Awake は AddComponent では走らないので、明示的に呼ぶ。
            var awake = typeof(V6ButtonPressFeedback).GetMethod("Awake",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(awake, "Awake が見つからない");
            awake.Invoke(feedback, null);
            return feedback;
        }

        [Test]
        public void TheRingStartsHidden()
        {
            var feedback = MakeButton();
            Assert.IsFalse(feedback.RingVisibleForAudit, "押す前は枠を出さない");
        }

        [Test]
        public void PressingShowsTheRing()
        {
            var feedback = MakeButton();
            feedback.OnPointerDown(null);

            Assert.IsTrue(feedback.RingVisibleForAudit, "押したら枠を出す");
            Assert.Greater(feedback.RingAlphaForAudit, 0f, "押した瞬間は見える濃さ");
        }

        [Test]
        public void TheRingIsSubtle()
        {
            var feedback = MakeButton();
            feedback.OnPointerDown(null);

            // 「地味なアニメーションくらいがちょうどいい」という指摘に合わせ、
            // 押した瞬間の不透明度は 0.3 未満、広がりは 1.2 倍未満。
            Assert.LessOrEqual(feedback.RingAlphaForAudit, 0.3f, "濃すぎる");
            Assert.Less(V6ButtonPressFeedback.ExpandScale, 1.2f, "広がりすぎる");
        }

        [Test]
        public void AttachIsIdempotent()
        {
            root = new GameObject("Attach Probe", typeof(RectTransform));
            var image = root.AddComponent<Image>();
            image.raycastTarget = false;

            V6ButtonPressFeedback.Attach(root);
            V6ButtonPressFeedback.Attach(root);

            Assert.AreEqual(1, root.GetComponents<V6ButtonPressFeedback>().Length,
                "同じボタンに二重に付かない");
        }

        [Test]
        public void AttachDoesNothingForNull()
        {
            Assert.DoesNotThrow(() => V6ButtonPressFeedback.Attach(null));
        }
    }
}
