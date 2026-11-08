using NUnit.Framework;
using UnityEngine;
using FlashBeat.UI.V6.Common;

namespace FlashBeat.UI.V6.Common.Tests
{
    /// <summary>
    /// v6 共通フレーム (1728×972) の解像度別 origin 検証。
    /// design 正本 (docs/design/v6/flashbeat-v6-nodes.json) の "Resolution Check" 実配置:
    /// FHD 1920×1080 → (96,54) / 2K 2560×1440 → (416,234) / Mac 2560×1664 → (416,346)。
    /// origin は ((w-1728)/2, (h-972)/2) の整数切り捨て。フレームは実ピクセル 1:1 で
    /// 描かれ、親側で scale を掛けない (Owner 指示: 等比拡大は行わない)。
    /// </summary>
    public sealed class V6FrameOriginTests
    {
        private const int FrameW = 1728;
        private const int FrameH = 972;

        [Test]
        public void OriginFor_Fhd1920x1080()
        {
            Assert.AreEqual(new Vector2(96f, 54f), V6Frame.OriginFor(1920, 1080));
        }

        [Test]
        public void OriginFor_2k2560x1440()
        {
            Assert.AreEqual(new Vector2(416f, 234f), V6Frame.OriginFor(2560, 1440));
        }

        [Test]
        public void OriginFor_Mac2560x1664()
        {
            Assert.AreEqual(new Vector2(416f, 346f), V6Frame.OriginFor(2560, 1664));
        }

        [Test]
        public void OriginFor_FrameSize_IsZero()
        {
            Assert.AreEqual(Vector2.zero, V6Frame.OriginFor(FrameW, FrameH));
        }

        [Test]
        public void OriginFor_SmallerThanFrame_GoesNegativeWithoutClamp()
        {
            // 画面がフレームより小さい場合は負 origin (スクリーン外)。クランプしない。
            Assert.AreEqual(new Vector2(-224f, -126f), V6Frame.OriginFor(1280, 720));
        }

        [Test]
        public void OriginFor_FloorsOddRemainders()
        {
            // (1930-1728)/2 = 101, (1090-972)/2 = 59 → 奇数余りは切り捨てで偶数中央に寄せる
            Assert.AreEqual(new Vector2(101f, 59f), V6Frame.OriginFor(1930, 1090));
        }

        [Test]
        public void BackdropColor_MatchesContractSection3BgToken()
        {
            // 契約 §3 の bg トークン (#1B1B1B)。ApplyBackdrop の呼び出し側が渡す値。
            Assert.AreEqual(new Color32(0x1B, 0x1B, 0x1B, 0xFF), V6Tokens.Bg);
        }
    }
}
