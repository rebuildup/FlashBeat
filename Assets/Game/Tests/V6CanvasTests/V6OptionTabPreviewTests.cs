using NUnit.Framework;
using TMPro;
using UnityEngine;
using FlashBeat.UI.V5;
using FlashBeat.UI.V6;
using FlashBeat.UI.V6.Common;
using FlashBeat.UI.V6.Option;

namespace FlashBeat.V6CanvasTests
{
    /// <summary>
    /// Option 右パネルのタブ別プレビュー (design B6If6Z / EzLfb / z7joSy / DQHHp)。
    ///
    /// r23 (2560×1664 実フルスクリーン) で 4 タブを初めて個別に撮ったところ、
    /// Play タブの座標で作った説明ブロック (Divider 544) を全タブへ流用していたため、
    /// Display / Sound / Data の説明文が design より 88〜234px 下に落ちていた。
    /// また Display の判定色凡例と Sound の音量値・トラックが丸ごと欠けていた。
    /// design ノードの実測 bounds をここで固定する。
    /// </summary>
    public sealed class V6OptionTabPreviewTests
    {
        private GameObject canvasGo;

        [SetUp]
        public void SetUp()
        {
            canvasGo = new GameObject("V6OptionTabPreviewTests Canvas", typeof(RectTransform), typeof(Canvas));
        }

        [TearDown]
        public void TearDown()
        {
            var view = V6OptionView.Current;
            if (view != null)
            {
                view.Dispose();
            }

            if (canvasGo != null)
            {
                Object.DestroyImmediate(canvasGo);
            }
            canvasGo = null;
        }

        private V6OptionView CreateView()
        {
            return V6OptionView.Create(canvasGo.GetComponent<RectTransform>(), () => { });
        }

        /// <summary>親の左上を原点とする design 座標 (y_down)。</summary>
        private static Vector2 DesignTopLeft(RectTransform rt)
        {
            return new Vector2(rt.anchoredPosition.x, -rt.anchoredPosition.y);
        }

        private static TMP_Text TextUnder(Transform root, string path)
        {
            Transform t = root.Find(path);
            Assert.IsNotNull(t, path + " not found");
            var tmp = t.GetComponent<TMP_Text>();
            Assert.IsNotNull(tmp, path + " must be a TMP text");
            return tmp;
        }

        // --- タブ別の説明ブロック位置 (design EzLfb / z7joSy / DQHHp) ---

        [TestCase(0, 544f)]
        [TestCase(1, 456f)]
        [TestCase(2, 472f)]
        [TestCase(3, 380f)]
        public void DescDivider_UsesTheTabDesignY(int tab, float expectedY)
        {
            Assert.AreEqual(expectedY, V6OptionLogic.DescDividerYOf(tab), 0.01f);

            var view = CreateView();
            view.SelectTabForAudit(tab);

            var divider = (RectTransform)view.Root.Find("Main/Preview/Divider");
            Assert.IsNotNull(divider, "Divider");
            Assert.AreEqual(24f, DesignTopLeft(divider).x, 0.01f, "Divider x = 24");
            Assert.AreEqual(expectedY, DesignTopLeft(divider).y, 0.01f, "Divider y = design");

            var title = (RectTransform)view.Root.Find("Main/Preview/Desc Title");
            Assert.AreEqual(expectedY + 20f, DesignTopLeft(title).y, 0.01f, "Desc Title は Divider +20");
            var desc = (RectTransform)view.Root.Find("Main/Preview/Desc");
            Assert.AreEqual(expectedY + 70f, DesignTopLeft(desc).y, 0.01f, "Desc は Divider +70");
        }

        // --- Desc の行送り (design lineHeight 1.4) ---

        [Test]
        public void DescLines_UseTheDesignLinePitch()
        {
            // r24 実機: Desc の 2 行目が design より 9〜10px 下に出てパネル下端 (688) に
            // 食い込んでいた。V6Controls.Text は 1 行描画用に lineSpacing を -44.8 (fs 分の
            // 行送り) にしており、40 を上書きすると 34.75 + 24×0.40 = 44.35px になる。
            // design 実測 (Data タブ 457→492) は 35px。
            var view = CreateView();
            view.SelectTabForAudit(3); // Data は 2 行の説明
            var desc = TextUnder(view.Root, "Main/Preview/Desc");
            desc.ForceMeshUpdate(true, true);
            Assert.GreaterOrEqual(desc.textInfo.lineCount, 2, "Data タブの説明は design でも 2 行");
            float pitch = Mathf.Abs(desc.textInfo.lineInfo[1].baseline - desc.textInfo.lineInfo[0].baseline);
            Assert.AreEqual(34.75f, pitch, 1.5f,
                "行送り = design lineHeight 1.4 (fs24)。40 を足すと 44.35 になり design より 9px 下がる");
        }

        // --- Display: 判定色の凡例 (design EzLfb Judge PERFECT..MISS) ---

        [Test]
        public void DisplayTab_DrawsFourJudgeChips_AtDesignCoords()
        {
            var view = CreateView();
            view.SelectTabForAudit(1);

            for (int i = 0; i < V6OptionLogic.JudgeLabels.Length; i++)
            {
                var chip = (RectTransform)view.Root.Find("Main/Preview/Judge " + V6OptionLogic.JudgeLabels[i]);
                Assert.IsNotNull(chip, "Judge chip " + i);
                Assert.AreEqual(new Vector2(24f + 118f * i, 360f), DesignTopLeft(chip),
                    "Judge chip " + i + " は design の 118 ピッチ");
                Assert.AreEqual(new Vector2(106f, 64f), chip.sizeDelta, "Judge chip " + i + " 106×64");

                var label = TextUnder(chip, "Label");
                Assert.AreEqual(V6OptionLogic.JudgeLabels[i], label.text);
                Assert.AreEqual(18f, label.fontSize, "Label fs18");
                Assert.AreEqual(V6Tokens.TextDim, label.color, "Label は $text-dim");

                var marker = (RectTransform)chip.Find("Marker");
                Assert.IsNotNull(marker, "Marker " + i);
                Assert.AreEqual(new Vector2(37f, 16f), DesignTopLeft(marker), "Marker (37,16)");
                Assert.AreEqual(new Vector2(32f, 4f), marker.sizeDelta, "Marker 32×4");
            }

            // 判定色は design の $text / $ok / $warn / $danger。
            Assert.AreEqual(V5Tokens.Text, MarkerColor(view, "PERFECT"));
            Assert.AreEqual(V5Tokens.Ok, MarkerColor(view, "GREAT"));
            Assert.AreEqual(V5Tokens.Warn, MarkerColor(view, "BAD"));
            Assert.AreEqual(V5Tokens.Danger, MarkerColor(view, "MISS"));
        }

        private static Color MarkerColor(V6OptionView view, string label)
        {
            var marker = view.Root.Find("Main/Preview/Judge " + label + "/Marker");
            Assert.IsNotNull(marker, "Marker " + label);
            return marker.GetComponent<UnityEngine.UI.Image>().color;
        }

        // --- Sound: 音量の値とトラック (design z7joSy) ---

        [Test]
        public void SoundTab_DrawsVolumeValueAndTrack_WithProportionalFill()
        {
            var view = CreateView();
            view.SelectTabForAudit(2);

            var value = TextUnder(view.Root, "Main/Preview/Volume Value");
            Assert.AreEqual(40f, value.fontSize, "Value fs40");
            Assert.AreEqual(new Vector2(24f, 360f), DesignTopLeft((RectTransform)value.transform), "Value (24,360)");

            var track = (RectTransform)view.Root.Find("Main/Preview/Volume Track");
            Assert.IsNotNull(track, "Volume Track");
            Assert.AreEqual(new Vector2(24f, 420f), DesignTopLeft(track), "Track (24,420)");
            Assert.AreEqual(new Vector2(464f, 20f), track.sizeDelta, "Track 464×20");

            // Fill は実値の比率 — design の 100% = 464 全幅をそのまま比例配分。
            int percent = 0;
            int.TryParse(value.text.Replace("%", ""), out percent);
            var fill = (RectTransform)track.Find("Fill");
            Assert.IsNotNull(fill, "Fill");
            Assert.AreEqual(V6OptionLogic.VolumeFillWidth(percent), fill.sizeDelta.x, 0.01f,
                "Fill 幅 = 音量比 × 464");
            Assert.LessOrEqual(fill.sizeDelta.x, 464f, "Fill はトラックを超えない");
        }

        [Test]
        public void VolumeFillWidth_IsClampedAndProportional()
        {
            Assert.AreEqual(464f, V6OptionLogic.VolumeFillWidth(100), 0.01f);
            Assert.AreEqual(232f, V6OptionLogic.VolumeFillWidth(50), 0.01f);
            Assert.AreEqual(0f, V6OptionLogic.VolumeFillWidth(0), 0.01f);
            Assert.AreEqual(464f, V6OptionLogic.VolumeFillWidth(140), 0.01f, "100 超は 100");
            Assert.AreEqual(0f, V6OptionLogic.VolumeFillWidth(-5), 0.01f, "負は 0");
        }
    }
}
