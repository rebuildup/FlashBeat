using System.Linq;
using FlashBeat.UI.V5;
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
        private int originalScore;
        private int originalCombo;

        [SetUp]
        public void SetUp()
        {
            originalScore = GManager.score;
            originalCombo = GManager.combo;

            canvasGo = new GameObject("V6GameHudViewTests Canvas", typeof(Canvas));
            var canvasRect = (RectTransform)canvasGo.transform;
            // ScreenSpaceOverlay の Canvas は実機では画面サイズに追従するため
            // sizeDelta は書き換わらない (EditMode では保持される)。
            // このテストはレイアウトの寸法に依存しない (要素の構造と参照だけを確かめる)。
            canvasRect.sizeDelta = new Vector2(1920f, 1080f);
            view = V6GameHudView.Create(canvasRect);
        }

        [TearDown]
        public void TearDown()
        {
            GManager.score = originalScore;
            GManager.combo = originalCombo;

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
        ///
        /// Refresh() は非表示なら何もしないため、例外が出ないだけでは
        /// 「更新経路が通った」証明にならない。ここでは score を既知の値に
        /// して更新し、表示が実際に書き換わったこと (= 早期 return していない
        /// こと) を併せて確認する。
        /// </summary>
        [Test]
        public void Refresh_UpdatesWithoutThrowing()
        {
            GManager.score = 123456;
            GManager.combo = 7;

            Assert.DoesNotThrow(() => view.Refresh(), "Refresh が例外を投げた");

            TextMeshProUGUI score = view.GetComponentsInChildren<TextMeshProUGUI>(true)
                .FirstOrDefault(t => t.name == "Score Value");
            Assert.IsNotNull(score, "スコアの値 Text が見つからない");
            Assert.AreEqual(
                GameHudLogic.FormatScore(GManager.score), score.text,
                "Refresh が早期 return してスコアの表示が更新されていない");

            // コンボボーナスの値側 Text も更新されている (=": 更新経路が最後まで通った")。
            TextMeshProUGUI bonusValue = BonusRows(view)[0]
                .GetComponentsInChildren<TextMeshProUGUI>(true)
                .FirstOrDefault(t => t.name == "Value");
            Assert.IsNotNull(bonusValue, "コンボボーナスの値 Text が見つからない");
            StringAssert.StartsWith("+", bonusValue.text);
        }

        /// <summary>
        /// コンボボーナスの 2 行目 (次のボーナス) は「ラベル」と「値」が別々の Text である。
        ///
        /// 実害は「参照の取り違え」だった — 以前は 2 行目の**値**側の Text が
        /// ラベル用の変数へ代入され、値用の変数 (nextBonusReward) が null のまま
        /// 残っていた。その結果 Refresh() が毎フレーム NullReferenceException で
        /// 止まり、歌詞・進捗バー・レーン判定行の更新が一切走っていなかった。
        /// ここでは「ラベルと値が別の Text であること」と「値側の参照が
        /// 実際に更新されること」の両方を固定する。
        /// </summary>
        [Test]
        public void ComboBonus_HasSeparateLabelAndValueTexts()
        {
            GManager.combo = 7;

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

        /// <summary>コンボボーナスの 2 行 (0 = ボーナス合計、1 = 次のボーナス)。</summary>
        private static Transform[] BonusRows(V6GameHudView target)
        {
            Transform bonus = target.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(t => t.name == "Combo Bonus");
            Assert.IsNotNull(bonus, "コンボボーナスの行が見つからない");
            return bonus.GetComponentsInChildren<Transform>(true)
                .Where(t => t.name == "Row")
                .ToArray();
        }
    }
}
