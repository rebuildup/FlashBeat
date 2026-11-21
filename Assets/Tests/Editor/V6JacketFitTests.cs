using System.Collections.Generic;
using FlashBeat.UI.V6;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace FlashBeat.Tests
{
    /// <summary>
    /// Issue #848: ジャケットの cover フィット (<see cref="V6JacketFit"/>) を固定する。
    /// hqdefault (4:3) が 16:9 のスロットでレターボックス状に縮んでいた不具合に対し、
    /// ローディング背景と同じ cover 規則で原 Texture から切り抜いた Sprite を返す。
    /// </summary>
    public class V6JacketFitTests
    {
        private readonly List<Object> owned = new List<Object>();
        private GameObject thumbGo;
        private Image thumb;

        [SetUp]
        public void SetUp()
        {
            V6JacketProvider.DestroyAll();
            V6JacketProvider.ResetCountersForAudit();

            thumbGo = new GameObject("Thumb", typeof(RectTransform), typeof(Image));
            thumb = thumbGo.GetComponent<Image>();
            // Hero と同じ 16:9 スロット。
            thumb.rectTransform.sizeDelta = new Vector2(512f, 288f);
        }

        [TearDown]
        public void TearDown()
        {
            if (thumbGo != null)
            {
                Object.DestroyImmediate(thumbGo);
            }

            for (int i = 0; i < owned.Count; i++)
            {
                if (owned[i] != null)
                {
                    Object.DestroyImmediate(owned[i]);
                }
            }

            owned.Clear();
            V6JacketProvider.DestroyAll();
        }

        private Texture2D NewTexture(int w, int h)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            owned.Add(tex);
            return tex;
        }

        private Sprite FullSprite(Texture2D tex)
        {
            var sprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height),
                new Vector2(0.5f, 0.5f), 100f);
            owned.Add(sprite);
            return sprite;
        }

        [Test]
        public void CoverSprite_4x3TextureIn16x9Slot_CropsTopBottom()
        {
            Texture2D tex = NewTexture(480, 360);
            Sprite source = FullSprite(tex);

            Sprite fitted = V6JacketFit.CoverSprite(thumb, source);

            Assert.IsNotNull(fitted);
            Assert.AreNotSame(source, fitted, "4:3 を 16:9 へ入れるときは切り抜きが要る");
            // cover: 幅いっぱい・上下を中央で削る → 480×270 (16:9)。
            Assert.AreEqual(480f, fitted.rect.width, 0.01f);
            Assert.AreEqual(270f, fitted.rect.height, 0.01f);
            Assert.AreEqual(512f / 288f, fitted.rect.width / fitted.rect.height, 0.001f,
                "切り抜き後の sprite はスロットと同じアスペクト");
        }

        [Test]
        public void CoverSprite_SameAspect_ReturnsOriginalSprite()
        {
            Texture2D tex = NewTexture(512, 288);
            Sprite source = FullSprite(tex);

            Assert.AreSame(source, V6JacketFit.CoverSprite(thumb, source),
                "アスペクトが一致するときは切り抜き実体を増やさない");
        }

        [Test]
        public void CoverSprite_UnknownSlotSize_ReturnsOriginalSprite()
        {
            thumb.rectTransform.sizeDelta = Vector2.zero;
            Texture2D tex = NewTexture(480, 360);
            Sprite source = FullSprite(tex);

            Assert.AreSame(source, V6JacketFit.CoverSprite(thumb, source),
                "レイアウト未確定 (rect 0) のときは従来どおり元 sprite を返す");
        }

        [Test]
        public void CoverSprite_WideTextureIn43Slot_CropsLeftRight()
        {
            // 16:9 のジャケットを 4:3 の枠 (例: 別 spec の Thumb) へ。
            thumb.rectTransform.sizeDelta = new Vector2(400f, 300f);
            Texture2D tex = NewTexture(640, 360);
            Sprite source = FullSprite(tex);

            Sprite fitted = V6JacketFit.CoverSprite(thumb, source);

            Assert.AreNotSame(source, fitted);
            Assert.AreEqual(480f, fitted.rect.width, 0.01f);
            Assert.AreEqual(360f, fitted.rect.height, 0.01f);
        }

        [Test]
        public void CoverSprite_SameTextureAndSlot_ReturnsCachedInstance()
        {
            Texture2D tex = NewTexture(480, 360);
            Sprite source = FullSprite(tex);

            Sprite first = V6JacketFit.CoverSprite(thumb, source);
            Sprite second = V6JacketFit.CoverSprite(thumb, source);

            Assert.AreSame(first, second, "同じ原 Texture + 同じ枠なら同じ crop を返す");
        }

        [Test]
        public void CoverSprite_NullInputs_ReturnInputOrNull()
        {
            Texture2D tex = NewTexture(480, 360);
            Sprite source = FullSprite(tex);

            Assert.AreSame(source, V6JacketFit.CoverSprite(null, source));
            Assert.IsNull(V6JacketFit.CoverSprite(thumb, null));
        }
    }
}
