using FlashBeat.UI.V6;
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

            // Issue #848 のテストが V6JacketProvider の遠隔キャッシュを使うので
            // 画面間で持ち越さない (seed した Texture は provider が破棄する)。
            V6JacketProvider.DestroyAll();
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

        // --- Issue #848: cover フィットと (songId, videoId) バインド照合 ---

        /// <summary>videoId を持つ曲をカタログから 1 件探す (リモート曲を含む)。</summary>
        private static bool TryFindSongWithVideoId(out int songId, out string videoId)
        {
            int count = GManager.ActiveSongs != null ? GManager.ActiveSongs.Length : 0;
            for (int i = 0; i < count; i++)
            {
                SongData song = GManager.GetSong(i);
                if (song != null && !string.IsNullOrEmpty(song.videoId))
                {
                    songId = i;
                    videoId = song.videoId;
                    return true;
                }
            }

            songId = -1;
            videoId = null;
            return false;
        }

        private GameObject CreateThumb(float w, float h)
        {
            var badge = V6SongBadge.Create(parent, new V6SongBadge.Spec
            {
                ThumbSize = new Vector2(w, h),
            });
            return badge.Thumb;
        }

        [Test]
        public void ApplyJacket_RemoteThumbnail_AppliesCoverCroppedSprite()
        {
            int songId;
            string videoId;
            Assert.IsTrue(TryFindSongWithVideoId(out songId, out videoId),
                "videoId を持つ曲がカタログに必要");

            // hqdefault 相当の 4:3 テクスチャを遠隔キャッシュへ seed する
            // (EditMode では HTTP を回せないため、取得済み経路を直接使う)。
            var tex = new Texture2D(480, 360, TextureFormat.RGBA32, false);
            V6JacketProvider.SeedRemoteEntryForAudit(videoId, tex, 0f);

            GameObject thumb = CreateThumb(512f, 288f);
            V6SongBadge.ApplyJacket(thumb, songId, "テスト");

            var img = thumb.GetComponent<Image>();
            Assert.AreEqual(Image.Type.Simple, img.type);
            Assert.IsNotNull(img.sprite);
            Assert.AreEqual(480f, img.sprite.rect.width, 0.01f);
            Assert.AreEqual(270f, img.sprite.rect.height, 0.01f,
                "4:3 を 16:9 枠へ cover crop する (上下を削る)");
            Assert.IsTrue(img.preserveAspect);
            Assert.AreEqual(Color.white, img.color);
        }

        [Test]
        public void ApplyRemoteJacket_RejectsStaleCallbacks()
        {
            int songId;
            string videoId;
            Assert.IsTrue(TryFindSongWithVideoId(out songId, out videoId));

            var tex = new Texture2D(480, 360, TextureFormat.RGBA32, false);
            V6JacketProvider.SeedRemoteEntryForAudit(videoId, tex, 0f);

            GameObject thumb = CreateThumb(512f, 288f);
            V6SongBadge.ApplyJacket(thumb, songId, "テスト");
            var img = thumb.GetComponent<Image>();
            Sprite applied = img.sprite;
            Assert.IsNotNull(applied, "seed 済みなら ApplyJacket は同期で画像を適用する");

            // 遅れて届いた別画像 (16:9 → スロットと同アスペクトなので crop 無し)。
            var otherTex = new Texture2D(640, 360, TextureFormat.RGBA32, false);
            var other = Sprite.Create(otherTex,
                new Rect(0f, 0f, otherTex.width, otherTex.height),
                new Vector2(0.5f, 0.5f), 100f);

            // 同じ SongId でも videoId が違う結果は捨てる (カタログ更新後の遅延)。
            V6SongBadge.ApplyRemoteJacket(img, songId, "other-video-id", other);
            Assert.AreSame(applied, img.sprite,
                "videoId が違う遅延結果でジャケットを上書きしてはいけない");

            // 別 SongId の結果も捨てる (行スロットの使い回し)。
            V6SongBadge.ApplyRemoteJacket(img, songId + 1, videoId, other);
            Assert.AreSame(applied, img.sprite,
                "SongId が違う遅延結果でジャケットを上書きしてはいけない");

            // 正しい (songId, videoId) の結果は適用する。
            V6SongBadge.ApplyRemoteJacket(img, songId, videoId, other);
            Assert.AreSame(other, img.sprite,
                "アスペクト一致なら crop 無しでそのまま適用される");

            Object.DestroyImmediate(other);
            Object.DestroyImmediate(otherTex);
        }

        [Test]
        public void ApplyJacket_SongWithoutVideoId_InvalidatesPreviousBinding()
        {
            int songId;
            string videoId;
            Assert.IsTrue(TryFindSongWithVideoId(out songId, out videoId));

            var tex = new Texture2D(480, 360, TextureFormat.RGBA32, false);
            V6JacketProvider.SeedRemoteEntryForAudit(videoId, tex, 0f);

            GameObject thumb = CreateThumb(512f, 288f);
            V6SongBadge.ApplyJacket(thumb, songId, "テスト");
            var img = thumb.GetComponent<Image>();
            Assert.AreEqual(Image.Type.Simple, img.type);

            // videoId を解決できない曲へスロットを差し替えると空枠に戻り、
            // 前の曲のバインドも無効化される。
            V6SongBadge.ApplyJacket(thumb, 999999, "存在しない曲");
            Assert.AreEqual(Image.Type.Sliced, img.type, "画像無しは角丸プレートに戻る");

            // 無効化後に前の曲の遅延結果が届いても適用されない。
            var otherTex = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            var other = Sprite.Create(otherTex, new Rect(0f, 0f, 64f, 64f),
                new Vector2(0.5f, 0.5f), 100f);
            Sprite plate = img.sprite;
            V6SongBadge.ApplyRemoteJacket(img, songId, videoId, other);
            Assert.AreSame(plate, img.sprite,
                "videoId 無しへ差し替えたあとの遅延結果は捨てる");

            Object.DestroyImmediate(other);
            Object.DestroyImmediate(otherTex);
        }

        [Test]
        public void ApplyRemoteJacket_WithoutBinding_DoesNothing()
        {
            // ApplyJacket を通っていない枠にはバインドが無いので何も起きない。
            var img = new GameObject("Thumb", typeof(RectTransform), typeof(Image))
                .GetComponent<Image>();
            var tex = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            var sprite = Sprite.Create(tex, new Rect(0f, 0f, 64f, 64f),
                new Vector2(0.5f, 0.5f), 100f);

            V6SongBadge.ApplyRemoteJacket(img, 1, "video", sprite);

            Assert.IsNull(img.sprite);
            Assert.AreEqual(Image.Type.Simple, img.type);

            Object.DestroyImmediate(img.gameObject);
            Object.DestroyImmediate(sprite);
            Object.DestroyImmediate(tex);
        }
    }
}
