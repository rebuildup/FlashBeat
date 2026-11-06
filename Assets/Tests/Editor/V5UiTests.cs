using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using FlashBeat.UI.V5;

namespace FlashBeat.Tests.Editor
{
    /// <summary>
    /// Issue #106: v5 UI 基盤のテスト。プロファイル選択、アンカーヘルパ、
    /// 角丸スプライト、Components v5 相当部品の生成を検証する。
    /// </summary>
    public class V5UiTests
    {
        // --- UISizeProfile 選択 ---

        [Test]
        public void Select_1920x1080_ReturnsFhd()
        {
            var p = UISizeProfile.Select(1920, 1080);
            Assert.AreEqual("fhd", p.Name);
            Assert.AreEqual(48, p.Margin);
            Assert.AreEqual(52f, p.FsXl);
            Assert.AreEqual(420f, p.FsRank);
            Assert.AreEqual(new Vector2(460, 800), p.CardCenter);
            Assert.AreEqual(78, p.RowH);
            Assert.AreEqual(new Vector2(360, 120), p.Btn);
        }

        [Test]
        public void Select_2560x1440_Returns2k()
        {
            var p = UISizeProfile.Select(2560, 1440);
            Assert.AreEqual("2k", p.Name);
            Assert.AreEqual(64, p.Margin);
            Assert.AreEqual(560f, p.FsRank);
            Assert.AreEqual(104, p.RowH);
        }

        [Test]
        public void Select_1440x900_ReturnsMac()
        {
            var p = UISizeProfile.Select(1440, 900);
            Assert.AreEqual("mac", p.Name);
            Assert.AreEqual(36, p.Margin);
            Assert.AreEqual(330f, p.FsRank);
            Assert.AreEqual(64, p.RowH);
        }

        [Test]
        public void Select_1024x768_ReturnsFhdScaledByHeightOver1080()
        {
            // 4:3 は mac/fhd にも 2k にも当てはまらない → fhd を h/1080 倍に縮小。
            var p = UISizeProfile.Select(1024, 768);
            Assert.AreEqual("fhd*0.711", p.Name);
            float scale = 768f / 1080f;
            Assert.AreEqual(Mathf.RoundToInt(48 * scale), p.Margin);
            Assert.AreEqual(52f * scale, p.FsXl, 0.001f);
            // int フィールドは RoundToInt (Margin と同じ丸め規約)、float フィールドは無丸め。
            Assert.AreEqual(Mathf.RoundToInt(78 * scale), p.RowH);
        }

        [Test]
        public void Select_1280x720_ReturnsFhdScaledAndNeverClips()
        {
            // 縮小後の最大要素 (fs_rank 420 * scale) が画面高に収まること。
            var p = UISizeProfile.Select(1280, 720);
            float scale = 720f / 1080f;
            Assert.AreEqual("fhd*0.667", p.Name);
            Assert.LessOrEqual(p.FsRank, 720f);
            Assert.AreEqual(420f * scale, p.FsRank, 0.001f);
            Assert.AreEqual(Mathf.RoundToInt(48 * scale), p.Margin);
        }

        [Test]
        public void FhdScaled_AllFieldsScaleUniformly()
        {
            var p = UISizeProfile.FhdScaled(0.5f);
            Assert.AreEqual(24, p.Margin);
            // fhd fs_m=30 → 30*0.5。全フィールドが同じ scale で一様に縮小される。
            Assert.AreEqual(15f, p.FsM);
            Assert.AreEqual(90f, p.FsCombo);
            Assert.AreEqual(new Vector2(230, 400), p.CardCenter);
            Assert.AreEqual(new Vector2(180, 60), p.Btn);
        }

        // --- アンカーヘルパ ---

        [Test]
        public void PinTop_StretchesHorizontally_AndFixesHeightBelowTopMargin()
        {
            var root = new GameObject("root", typeof(RectTransform));
            var child = V5Ui.Node(root, "child").GetComponent<RectTransform>();
            var rt = V5Ui.PinTop(child, 100f, 48f);
            Assert.AreEqual(new Vector2(0f, 1f), rt.anchorMin);
            Assert.AreEqual(new Vector2(1f, 1f), rt.anchorMax);
            Assert.AreEqual(new Vector2(48f, -148f), rt.offsetMin);
            Assert.AreEqual(new Vector2(-48f, -48f), rt.offsetMax);
            Object.DestroyImmediate(root);
        }

        [Test]
        public void PinBottom_StretchesHorizontally_AndFixesHeightAboveBottomMargin()
        {
            var root = new GameObject("root", typeof(RectTransform));
            var child = V5Ui.Node(root, "child").GetComponent<RectTransform>();
            var rt = V5Ui.PinBottom(child, 56f, 10f);
            Assert.AreEqual(new Vector2(0f, 0f), rt.anchorMin);
            Assert.AreEqual(new Vector2(1f, 0f), rt.anchorMax);
            Assert.AreEqual(new Vector2(10f, 10f), rt.offsetMin);
            Assert.AreEqual(new Vector2(-10f, 66f), rt.offsetMax);
            Object.DestroyImmediate(root);
        }

        [Test]
        public void PinLeft_And_PinRight_FixWidthBesideSideMargins()
        {
            var root = new GameObject("root", typeof(RectTransform));
            var left = V5Ui.Node(root, "left").GetComponent<RectTransform>();
            var leftRt = V5Ui.PinLeft(left, 200f, 36f);
            Assert.AreEqual(new Vector2(0f, 0f), leftRt.anchorMin);
            Assert.AreEqual(new Vector2(0f, 1f), leftRt.anchorMax);
            Assert.AreEqual(new Vector2(36f, 36f), leftRt.offsetMin);
            Assert.AreEqual(new Vector2(236f, -36f), leftRt.offsetMax);

            var right = V5Ui.Node(root, "right").GetComponent<RectTransform>();
            var rightRt = V5Ui.PinRight(right, 200f, 36f);
            Assert.AreEqual(new Vector2(1f, 0f), rightRt.anchorMin);
            Assert.AreEqual(new Vector2(1f, 1f), rightRt.anchorMax);
            Assert.AreEqual(new Vector2(-236f, 36f), rightRt.offsetMin);
            Assert.AreEqual(new Vector2(-36f, -36f), rightRt.offsetMax);
            Object.DestroyImmediate(root);
        }

        [Test]
        public void Fill_InsetsByMargin()
        {
            var root = new GameObject("root", typeof(RectTransform));
            var child = V5Ui.Node(root, "child").GetComponent<RectTransform>();
            var rt = V5Ui.Fill(child, 64f);
            Assert.AreEqual(Vector2.zero, rt.anchorMin);
            Assert.AreEqual(Vector2.one, rt.anchorMax);
            Assert.AreEqual(new Vector2(64f, 64f), rt.offsetMin);
            Assert.AreEqual(new Vector2(-64f, -64f), rt.offsetMax);
            Object.DestroyImmediate(root);
        }

        // --- 角丸スプライト / 部品 ---

        [Test]
        public void RoundedSprite_HasNineSliceBorderWiderThanRadius()
        {
            var sprite = V5Ui.RoundedSprite(8);
            Assert.AreEqual(8 + 2, sprite.border.x);
            Assert.AreEqual(8 + 2, sprite.border.w);
            Assert.GreaterOrEqual(sprite.texture.width, sprite.border.x + sprite.border.z + 1);
        }

        [Test]
        public void Panel_UsesSlicedRoundedSprite()
        {
            var root = new GameObject("root", typeof(RectTransform));
            var go = V5Ui.Panel(root, "panel", new Vector2(360, 120), V5Tokens.ControlBg, V5Tokens.RadiusMedium);
            var img = go.GetComponent<Image>();
            Assert.AreEqual(Image.Type.Sliced, img.type);
            Assert.AreEqual(V5Tokens.ControlBg, img.color);
            Assert.IsNotNull(img.sprite);
            Object.DestroyImmediate(root);
        }

        [Test]
        public void Buttons_UseComponentsV5ColorsAndRadius()
        {
            var root = new GameObject("root", typeof(RectTransform));
            var primary = V5Ui.ButtonPrimary(root, "Start", new Vector2(360, 120));
            var primaryImg = primary.GetComponent<Image>();
            Assert.AreEqual(V5Tokens.ButtonPrimaryBg, primaryImg.color);
            Assert.AreEqual(V5Tokens.RadiusMedium, primaryImg.sprite.border.x - 2f);
            Assert.IsNotNull(primary.GetComponent<Button>());

            var secondary = V5Ui.ButtonSecondary(root, "Back", new Vector2(360, 120));
            Assert.AreEqual(V5Tokens.ControlBg, secondary.GetComponent<Image>().color);
            Object.DestroyImmediate(root);
        }

        [Test]
        public void KeyCap_And_Tag_UseSmallRadius()
        {
            var root = new GameObject("root", typeof(RectTransform));
            var cap = V5Ui.KeyCap(root, "R", new Vector2(40, 40));
            var capImg = cap.GetComponent<Image>();
            Assert.AreEqual(V5Tokens.RadiusSmall, capImg.sprite.border.x - 2f);

            var tag = V5Ui.Tag(root, "10", new Vector2(60, 32));
            Assert.AreEqual(V5Tokens.RadiusSmall, tag.GetComponent<Image>().sprite.border.x - 2f);
            Object.DestroyImmediate(root);
        }

        [Test]
        public void SongRow_And_RankRow_HaveExpectedHeights()
        {
            var root = new GameObject("root", typeof(RectTransform));
            root.GetComponent<RectTransform>().sizeDelta = new Vector2(1920, 1080);

            var song = V5Ui.SongRow(root, "Poison", "LeaF", "10");
            Assert.AreEqual(UISizeProfile.Current.RowH, song.GetComponent<RectTransform>().sizeDelta.y, 0.01f);

            var rank = V5Ui.RankRow(root, "1", "AAA", "1,000,000");
            Assert.AreEqual(UISizeProfile.Current.RowH * (56f / 72f), rank.GetComponent<RectTransform>().sizeDelta.y, 0.01f);
            Object.DestroyImmediate(root);
        }
    }
}
