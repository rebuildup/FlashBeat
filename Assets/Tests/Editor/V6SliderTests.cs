using System;
using FlashBeat.UI.V6.Common;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace FlashBeat.Tests.Editor
{
    /// <summary>
    /// Issue #385 (Phase 1.3): V6Slider が design "Volume Slider" (o0zh7 J7tBYP) の
    /// 操作部 (Track / Fill / Knob) を design 座標・色・値写像どおりに組み立てる
    /// ことを固定する。<see cref="V6VolumeSlider"/> はこの部品の上に値ラベルと
    /// ヒントを載せるだけなので、操作部の契約はここで一度だけ検証する。
    /// </summary>
    public class V6SliderTests
    {
        private GameObject parent;

        [SetUp]
        public void SetUp()
        {
            parent = new GameObject("Parent", typeof(RectTransform));
        }

        [TearDown]
        public void TearDown()
        {
            if (parent != null)
            {
                UnityEngine.Object.DestroyImmediate(parent);
            }
        }

        private static RectTransform FindRect(GameObject root, string path)
        {
            Transform t = root.transform.Find(path);
            return t != null ? t.GetComponent<RectTransform>() : null;
        }

        private static Image FindImage(GameObject root, string path)
        {
            Transform t = root.transform.Find(path);
            return t != null ? t.GetComponent<Image>() : null;
        }

        // 音量スライダと同じ配置 (ルート 464×160、トラックはルート内 (0,84))。
        private V6Slider BuildAtDefault(float value)
        {
            return V6Slider.Build(parent, "Slider", new Vector2(24f, 344f),
                new Vector2(464f, 160f), new Vector2(0f, 84f),
                () => value, (v, final) => { });
        }

        [Test]
        public void Build_ThrowsOnNullParent()
        {
            Assert.Throws<ArgumentNullException>(() =>
                V6Slider.Build(null, "Slider", Vector2.zero, Vector2.zero, Vector2.zero,
                    () => 0f, (v, f) => { }));
        }

        [Test]
        public void Build_LaysOutTrackFillAndKnob_InDesignPixels()
        {
            V6Slider slider = BuildAtDefault(0.5f);
            GameObject root = slider.gameObject;

            var rootRt = root.GetComponent<RectTransform>();
            Assert.AreEqual(new Vector2(24f, -344f), rootRt.anchoredPosition,
                "ルートは design 左上座標 (anchor/pivot 0,1) に置く");
            Assert.AreEqual(new Vector2(464f, 160f), rootRt.sizeDelta);

            var trackRt = FindRect(root, "Track");
            Assert.IsNotNull(trackRt, "Track がある");
            Assert.AreEqual(new Vector2(0f, -84f), trackRt.anchoredPosition);
            Assert.AreEqual(new Vector2(V6Slider.TrackW, V6Slider.TrackH), trackRt.sizeDelta);
            Assert.AreEqual(V6Tokens.Key1, FindImage(root, "Track").color, "トラックは $k1");

            var fillRt = FindRect(root, "Track/Fill");
            Assert.IsNotNull(fillRt, "Fill は Track の子");
            Assert.AreEqual(Vector2.zero, fillRt.anchoredPosition);
            Assert.AreEqual(V6Tokens.Key6, FindImage(root, "Track/Fill").color, "フィルは $k6");

            var knobRt = FindRect(root, "Knob");
            Assert.IsNotNull(knobRt, "Knob がある");
            Assert.AreEqual(new Vector2(V6Slider.KnobSize, V6Slider.KnobSize), knobRt.sizeDelta);
            Assert.AreEqual(V6Tokens.Text, FindImage(root, "Knob").color, "ノブは $text");
        }

        [Test]
        public void Refresh_MovesFillWidth_AndKnobCenter_WithValue()
        {
            float value = 0.5f;
            var slider = V6Slider.Build(parent, "Slider", Vector2.zero,
                new Vector2(464f, 160f), new Vector2(0f, 84f),
                () => value, (v, final) => { });
            GameObject root = slider.gameObject;

            var fillRt = FindRect(root, "Track/Fill");
            var knobRt = FindRect(root, "Knob");

            // 50%: フィル幅 = 464×0.5、ノブ中心 = 24 + 440×0.5 = 244。
            // ノブの anchoredPosition.x は中心 - ノブ幅/2 = 232、y はトラック縦中央 = -82。
            Assert.AreEqual(new Vector2(232f, 20f), fillRt.sizeDelta,
                "フィルはトラック全幅の値割合 (角丸 20 はトラック高)");
            Assert.AreEqual(new Vector2(232f, -82f), knobRt.anchoredPosition,
                "ノブは値の位置へ (中心 244 - 半径 12、トラック縦中央)");

            value = 1f;
            slider.Refresh();
            Assert.AreEqual(V6Slider.TrackW, fillRt.sizeDelta.x, 1e-4f, "100% でフィルは全幅");
            Assert.AreEqual(464f - V6Slider.KnobSize * 0.5f, knobRt.anchoredPosition.x, 1e-4f,
                "100% でノブ中心はトラック右端 (464)");

            value = 0f;
            slider.Refresh();
            Assert.AreEqual(0f, fillRt.sizeDelta.x, 1e-4f, "0% でフィルは幅 0");
            Assert.AreEqual(V6Slider.KnobCenterFrom - V6Slider.KnobSize * 0.5f,
                knobRt.anchoredPosition.x, 1e-4f, "0% でノブ中心は 24");
        }

        [Test]
        public void Value_ReadsThroughGetter_AndClamps()
        {
            float value = 1.4f;
            var slider = V6Slider.Build(parent, "Slider", Vector2.zero,
                new Vector2(464f, 160f), new Vector2(0f, 84f),
                () => value, (v, final) => { });
            Assert.AreEqual(1f, slider.Value, 1e-4f, "範囲外の値は 1 にクランプ");

            value = -0.2f;
            Assert.AreEqual(0f, slider.Value, 1e-4f, "負の値は 0 にクランプ");
        }

        [Test]
        public void ValueAt_And_KnobCenter_MapTrackEnds_AndRoundTrip()
        {
            Assert.AreEqual(0f, V6Slider.ValueAt(0f), 1e-4f);
            Assert.AreEqual(0f, V6Slider.ValueAt(V6Slider.KnobCenterFrom), 1e-4f);
            Assert.AreEqual(0.5f, V6Slider.ValueAt(244f), 1e-4f, "中央 = 50%");
            Assert.AreEqual(1f, V6Slider.ValueAt(V6Slider.TrackW), 1e-4f);
            Assert.AreEqual(1f, V6Slider.ValueAt(999f), 1e-4f, "トラック外はクランプ");

            float[] values = { 0f, 0.07f, 0.42f, 0.5f, 0.93f, 1f };
            for (int i = 0; i < values.Length; i++)
            {
                Assert.AreEqual(values[i],
                    V6Slider.ValueAt(V6Slider.KnobCenter(values[i])), 1e-4f,
                    "ノブ位置から値を戻せる (" + values[i] + ")");
            }
        }

        [Test]
        public void AttachParts_HonorsTrackOffset_InsideRoot()
        {
            // トラックをルート内の別位置に置いても、ノブはトラック縦中央・
            // トラック左端基準の軌跡に乗る (chrome の配置に依らない)。
            var slider = V6Slider.Build(parent, "Slider", Vector2.zero,
                new Vector2(464f, 200f), new Vector2(10f, 60f),
                () => 0.5f, (v, final) => { });
            GameObject root = slider.gameObject;

            var trackRt = FindRect(root, "Track");
            Assert.AreEqual(new Vector2(10f, -60f), trackRt.anchoredPosition);

            var knobRt = FindRect(root, "Knob");
            // ノブ中心 x = トラック左端 10 + 244 = 254 → 左端 242。
            // ノブ上端 y = 60 + 10 - 12 = 58 → anchoredPosition.y = -58。
            Assert.AreEqual(242f, knobRt.anchoredPosition.x, 1e-4f,
                "ノブはトラック左端基準の軌跡に乗る");
            Assert.AreEqual(-58f, knobRt.anchoredPosition.y, 1e-4f,
                "ノブはトラック縦中央に重なる");
        }
    }
}
