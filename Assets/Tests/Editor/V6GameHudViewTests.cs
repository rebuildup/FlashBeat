using System.Linq;
using FlashBeat.UI.V6.Game.Hud;
using NUnit.Framework;
using TMPro;
using UnityEngine;

namespace FlashBeat.Tests
{
    /// <summary>
    /// v6 ゲーム HUD の実構築を検証する (Issue #233)。
    ///
    /// HUD はコードから uGUI を生成するため、
    /// 「生成した要素の参照が 1 つ欠けていた」型の不具合が静かに残る。
    /// 実際に作って <see cref="V6GameHudView.Refresh"/> を 1 回呼んで、
    /// 例外が出ないこと (更新経路が最後まで通ること) を確認する。
    /// </summary>
    public class V6GameHudViewTests
    {
        private GameObject canvasGo;
        private V6GameHudView view;

        [SetUp]
        public void SetUp()
        {
            canvasGo = new GameObject("V6GameHudViewTests Canvas", typeof(Canvas));
            var canvasRect = (RectTransform)canvasGo.transform;
            canvasRect.sizeDelta = new Vector2(1920f, 1080f);
            view = V6GameHudView.Create(canvasRect);
        }

        [TearDown]
        public void TearDown()
        {
            if (canvasGo != null)
            {
                Object.DestroyImmediate(canvasGo);
            }

            view = null;
            canvasGo = null;
        }

        /// <summary>
        /// HUD を更新しても例外が出ない (更新経路が最後まで通る)。
        /// 以前はコンボボーナスの「値」側の Text への参照が欠けており、
        /// 毎フレーム NullReferenceException で更新が途切れていた。
        /// </summary>
        [Test]
        public void Refresh_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => view.Refresh());
        }

        /// <summary>
        /// コンボボーナスの 2 行目 (次のボーナス) は「ラベル」と「値」が別々の Text である。
        /// 同一の Text だと、値がラベルの内容を上書きする。
        /// </summary>
        [Test]
        public void ComboBonus_HasSeparateLabelAndValueTexts()
        {
            view.Refresh();

            Transform bonus = view.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(t => t.name == "Combo Bonus");
            Assert.IsNotNull(bonus, "コンボボーナスの行が見つからない");

            Transform[] rows = bonus.GetComponentsInChildren<Transform>(true)
                .Where(t => t.name == "Row")
                .ToArray();
            Assert.AreEqual(2, rows.Length, "コンボボーナスの行数が 2 ではない");

            TextMeshProUGUI label = rows[1].GetComponentsInChildren<TextMeshProUGUI>(true)
                .FirstOrDefault(t => t.name == "Label");
            TextMeshProUGUI value = rows[1].GetComponentsInChildren<TextMeshProUGUI>(true)
                .FirstOrDefault(t => t.name == "Value");

            Assert.IsNotNull(label, "「次のボーナス」のラベルがない");
            Assert.IsNotNull(value, "「次のボーナス」の値がない");
            Assert.AreNotSame(label, value, "ラベルと値が同じ Text を指している");
            StringAssert.StartsWith("次のボーナス", label.text);
            StringAssert.StartsWith("+", value.text);
        }
    }
}
