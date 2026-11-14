using FlashBeat.UI.V6.Common;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FlashBeat.Tests.Editor
{
    /// <summary>
    /// Issue #385 (Phase 1.2): V6SongBadge が「ジャケット + 曲名 (+ 副題)」を
    /// 画面ごとの design 座標・フォント・色で組み立てることを固定する。
    /// Ranking ヘッダー / Result Song Info / 曲チップ / Loading (LayoutDriven) の
    /// 各 spec が同じ規約で RectTransform を作ることを検証する。
    /// </summary>
    public class V6SongBadgeTests
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
                Object.DestroyImmediate(parent);
            }
        }

        private static RectTransform Rect(Transform root, string path)
        {
            Transform t = root.Find(path);
            return t != null ? t.GetComponent<RectTransform>() : null;
        }

        private static V6SongBadge.Spec HeaderLikeSpec()
        {
            // Ranking ヘッダー右上 (Thumb 64×36 + Song 列 320×76) と同じ形。
            return new V6SongBadge.Spec
            {
                ThumbPos = new Vector2(864f, 30f),
                ThumbSize = new Vector2(64f, 36f),
                ThumbColor = V6Tokens.Key3,
                ColumnName = "Song",
                ColumnPos = new Vector2(944f, 10f),
                ColumnSize = new Vector2(320f, 76f),
                Title = new V6SongBadge.Line
                {
                    Pos = new Vector2(0f, 1f), Size = new Vector2(320f, 43f), FontSize = 30f,
                    Color = V6Tokens.Text, Text = "曲名",
                    FitLineHeight = true,
                },
                Sub = new V6SongBadge.Line
                {
                    Name = "Artist", Pos = new Vector2(0f, 46f), Size = new Vector2(320f, 29f),
                    FontSize = 20f, Color = V6Tokens.TextMuted, Text = "アーティスト",
                    FitLineHeight = true,
                },
            };
        }

        [Test]
        public void Create_WithColumn_BuildsThumbColumnAndTwoLines()
        {
            var badge = V6SongBadge.Create(parent, HeaderLikeSpec());

            Assert.AreEqual("Thumb", badge.Thumb.name);
            Assert.AreEqual(parent.transform, badge.Thumb.transform.parent);
            var thumbRt = badge.Thumb.GetComponent<RectTransform>();
            Assert.AreEqual(new Vector2(864f, -30f), thumbRt.anchoredPosition);
            Assert.AreEqual(new Vector2(64f, 36f), thumbRt.sizeDelta);
            Assert.AreEqual(V6Tokens.Key3, badge.Thumb.GetComponent<Image>().color);

            Assert.AreEqual("Song", badge.Column.name);
            var columnRt = badge.Column.GetComponent<RectTransform>();
            Assert.AreEqual(new Vector2(944f, -10f), columnRt.anchoredPosition);
            Assert.AreEqual(new Vector2(320f, 76f), columnRt.sizeDelta);

            Assert.AreEqual("Title", badge.Title.gameObject.name);
            Assert.AreEqual("曲名", badge.Title.text);
            Assert.AreEqual(30f, badge.Title.fontSize);
            Assert.AreEqual(V6Tokens.Text, badge.Title.color);
            Assert.AreEqual(TextWrappingModes.NoWrap, badge.Title.textWrappingMode);
            Assert.AreEqual(TextOverflowModes.Ellipsis, badge.Title.overflowMode);
            Assert.AreEqual(TextAlignmentOptions.Left, badge.Title.alignment);

            Assert.AreEqual("Artist", badge.Sub.gameObject.name);
            Assert.AreEqual("アーティスト", badge.Sub.text);
            Assert.AreEqual(20f, badge.Sub.fontSize);
            Assert.AreEqual(V6Tokens.TextMuted, badge.Sub.color);
        }

        [Test]
        public void Create_LineRects_UseDesignTopLeftCoordinates()
        {
            V6SongBadge.Create(parent, HeaderLikeSpec());

            // 列ノード基準の design 座標 → anchoredPosition (x, -y)。
            var titleRt = Rect(badgeColumn(), "Title");
            Assert.AreEqual(new Vector2(0f, -1f), titleRt.anchoredPosition);
            Assert.AreEqual(new Vector2(320f, 43f), titleRt.sizeDelta);

            var artistRt = Rect(badgeColumn(), "Artist");
            Assert.AreEqual(new Vector2(0f, -46f), artistRt.anchoredPosition);
            Assert.AreEqual(new Vector2(320f, 29f), artistRt.sizeDelta);
        }

        private Transform badgeColumn()
        {
            return parent.transform.Find("Song");
        }

        [Test]
        public void Create_WithoutColumn_PlacesLinesOnParent()
        {
            // Result / 曲チップ型: 列ノード無しで Title/Sub (または Title のみ) を
            // parent 直下に置く。
            var badge = V6SongBadge.Create(parent, new V6SongBadge.Spec
            {
                ThumbPos = new Vector2(16f, 20f),
                ThumbSize = new Vector2(88f, 50f),
                Title = new V6SongBadge.Line
                {
                    Name = "Title", Pos = new Vector2(120f, 18f), Size = new Vector2(568f, 38f),
                    FontSize = 26f, Text = "曲名",
                },
                Sub = new V6SongBadge.Line
                {
                    Name = "Artist", Pos = new Vector2(120f, 52f), Size = new Vector2(568f, 26f),
                    FontSize = 18f, Color = V6Tokens.TextMuted,
                    FitLineHeight = true, FitHeight = 26f,
                },
            });

            Assert.IsNull(badge.Column);
            Assert.AreEqual(parent.transform, badge.Title.transform.parent);
            Assert.AreEqual(new Vector2(120f, -18f), badge.Title.rectTransform.anchoredPosition);
            Assert.AreEqual(new Vector2(568f, 38f), badge.Title.rectTransform.sizeDelta);
            Assert.AreEqual(26f, badge.Title.fontSize);
            Assert.AreEqual(TextOverflowModes.Ellipsis, badge.Sub.overflowMode);
        }

        [Test]
        public void Create_TitleOnlyChip_HasNoSub()
        {
            // Ranking SCORE RANKING の曲チップ (Thumb + Text のみ)。
            var badge = V6SongBadge.Create(parent, new V6SongBadge.Spec
            {
                ThumbPos = new Vector2(0f, 4f),
                ThumbSize = new Vector2(40f, 24f),
                Title = new V6SongBadge.Line
                {
                    Name = "Text", Pos = new Vector2(48f, 0f), Size = new Vector2(111f, 32f),
                    FontSize = 22f, Text = "曲名",
                },
            });

            Assert.IsNull(badge.Sub, "Sub 未指定なら 2 行目を作らない");
            Assert.AreEqual("Text", badge.Title.gameObject.name);
            Assert.AreEqual(new Vector2(48f, 0f), badge.Title.rectTransform.anchoredPosition);
            Assert.AreEqual(new Vector2(111f, 32f), badge.Title.rectTransform.sizeDelta);
        }

        [Test]
        public void Create_PivotLine_KeepsDesignRectWithCustomPivot()
        {
            // Result Song Info は pivot (0,0.5) — design の見た目の矩形は同じで
            // anchoredPosition だけ pivot 分ずれる (画面側 At の pivot 式と同一)。
            V6SongBadge.Create(parent, new V6SongBadge.Spec
            {
                ThumbPos = new Vector2(16f, 28f),
                ThumbSize = new Vector2(112f, 63f),
                Title = new V6SongBadge.Line
                {
                    Pos = new Vector2(144f, 16f), Size = new Vector2(204f, 36f),
                    FontSize = 34f, Pivot = new Vector2(0f, 0.5f),
                },
            });

            var rt = parent.transform.Find("Title").GetComponent<RectTransform>();
            Assert.AreEqual(new Vector2(0f, 0.5f), rt.pivot);
            // anchoredPosition = (x + pivot.x*w, -(y + (1-pivot.y)*h)) = (144, -34)。
            Assert.AreEqual(new Vector2(144f, -34f), rt.anchoredPosition);
            Assert.AreEqual(new Vector2(204f, 36f), rt.sizeDelta);
        }

        [Test]
        public void Create_LayoutDriven_DoesNotPlaceThumbAndColumn()
        {
            // Loading の Song Info (HorizontalLayoutGroup) 用: Thumb/Column は
            // sizeDelta だけ設定し、絶対配置 (anchor 0,1 + anchoredPosition) はしない。
            var badge = V6SongBadge.Create(parent, new V6SongBadge.Spec
            {
                LayoutDriven = true,
                ThumbName = "Jacket",
                ThumbSize = new Vector2(56f, 56f),
                ThumbColor = V6Tokens.Key2,
                ThumbRaycastTarget = false,
                ColumnName = "Texts",
                ColumnSize = new Vector2(113f, 56f),
                Title = new V6SongBadge.Line
                {
                    Name = "Song Title", Pos = Vector2.zero, Size = new Vector2(111f, 32f),
                    FontSize = 22f, Align = TextAlignmentOptions.TopLeft,
                },
                Sub = new V6SongBadge.Line
                {
                    Name = "Song Artist", Pos = new Vector2(0f, 34f), Size = new Vector2(113f, 23f),
                    FontSize = 16f, Align = TextAlignmentOptions.TopLeft,
                },
            });

            Assert.AreEqual("Jacket", badge.Thumb.name);
            Assert.AreEqual(new Vector2(56f, 56f), badge.Thumb.GetComponent<RectTransform>().sizeDelta);
            Assert.IsFalse(badge.Thumb.GetComponent<Image>().raycastTarget);
            Assert.AreEqual("Texts", badge.Column.name);

            // 列の子は絶対配置のまま (HLG の影響を受けない孫ノード)。
            var titleRt = Rect(badge.Column.transform, "Song Title");
            Assert.AreEqual(new Vector2(0f, 0f), titleRt.anchoredPosition);
            Assert.AreEqual(new Vector2(111f, 32f), titleRt.sizeDelta);
            Assert.AreEqual(TextAlignmentOptions.TopLeft, badge.Title.alignment);
        }

        [Test]
        public void ApplyJacket_UnknownSong_FallsBackToEmptyFrame()
        {
            var thumb = V6SongBadge.Create(parent, new V6SongBadge.Spec()).Thumb;
            var img = thumb.GetComponent<Image>();

            V6SongBadge.ApplyJacket(thumb, 9999, "存在しない曲");

            Assert.AreEqual(Image.Type.Sliced, img.type, "ジャケット無しは 9-slice の空枠");
            Assert.AreEqual(V6Tokens.Key3, img.color);
            Assert.IsNotNull(img.sprite);
            Assert.IsFalse(img.preserveAspect, "空枠では preserveAspect を立てない");
        }

        [Test]
        public void ApplyJacket_NullThumb_DoesNothing()
        {
            Assert.DoesNotThrow(() => V6SongBadge.ApplyJacket((GameObject)null, 1, "x"));
            Assert.DoesNotThrow(() => V6SongBadge.ApplyJacket((Image)null, 1, "x"));
        }
    }
}
