using System.Collections.Generic;
using FlashBeat.Gameplay;
using FlashBeat.UI.V6;
using FlashBeat.UI.V6.Game.Hud;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace FlashBeat.Tests
{
    /// <summary>
    /// Issue #675: 上部シェードがレーン帯 (ノーツが通る領域) を暗くしない
    /// ことを検証する構造テスト。
    ///
    /// シェードは Screen Space Overlay の Image で 3D の深度テストを通らないため、
    /// グラデーションのままだとアプローチ中のノーツが黒く沈む。修正後は
    /// テクスチャにレーン帯の抜きが焼き込まれ、帯の内側のアルファは 0。
    /// </summary>
    public class V6TopShadeHoleTests
    {
        private const float ScreenW = 1920f;
        private const float ScreenH = 1080f;
        private const int ShadeH = 260; // V6HudMetrics.Fhd().TopShadeH
        private const int Rows = 64;

        [Test]
        public void CorridorRows_NarrowsTowardTop_AndStaysOnScreen()
        {
            var left = new float[Rows];
            var right = new float[Rows];
            V6TopShadeHole.CorridorRows(ScreenW, ScreenH, ShadeH, Rows, left, right);

            for (int i = 0; i < Rows; i++)
            {
                Assert.Less(left[i], right[i], $"row {i}: corridor inverted");
                Assert.GreaterOrEqual(left[i], -V6TopShadeHole.CorridorMarginPx - 1f);
                Assert.LessOrEqual(right[i], ScreenW + V6TopShadeHole.CorridorMarginPx + 1f);
            }

            // 行 0 = シェード下端、行 Rows-1 = 上端。遠近法なので帯は上ほど狭い。
            Assert.Less(left[Rows - 1], left[0] + ScreenW * 0.25f,
                "corridor should move right toward the top");
            Assert.Greater(right[Rows - 1], right[0] - ScreenW * 0.25f,
                "corridor should move left toward the top");
            Assert.Less(right[Rows - 1] - left[Rows - 1],
                right[0] - left[0], "top corridor must be narrower than bottom");
        }

        [Test]
        public void ShadeAlpha_InsideCorridor_IsClear_Outside_KeepsGradient()
        {
            const float gradient = 0.65f;
            Assert.AreEqual(0f, V6TopShadeHole.ShadeAlpha(500f, 400f, 600f, gradient));
            Assert.AreEqual(gradient, V6TopShadeHole.ShadeAlpha(100f, 400f, 600f, gradient));
            Assert.AreEqual(gradient, V6TopShadeHole.ShadeAlpha(700f, 400f, 600f, gradient));
        }

        /// <summary>
        /// 実際に構築した HUD の Top Shade スプライトを読み、レーン帯の内側が
        /// 透明・帯の外側 (上端) が暗いことを確認する。EditMode ではカメラが
        /// 無いことが多いので、帯の位置は <see cref="V6TopShadeHole.CorridorRows"/>
        /// (本番と同じ関数) から取る。
        /// </summary>
        [Test]
        public void TopShadeSprite_HasLaneCorridorHole()
        {
            var canvasGo = new GameObject("V6TopShadeHoleTests Canvas", typeof(Canvas));
            try
            {
                var canvasRect = (RectTransform)canvasGo.transform;
                canvasRect.sizeDelta = new Vector2(ScreenW, ScreenH);
                var view = FlashBeat.UI.V6.Game.Hud.V6GameHudView.Create(canvasRect);

                var shade = view.TopShadeForAudit;
                Assert.NotNull(shade, "Top Shade image was not built");
                Assert.NotNull(shade.sprite, "Top Shade has no sprite");

                var tex = shade.sprite.texture;
                Assert.NotNull(tex);
                Assert.AreEqual(V6TopShadeHole.TextureWidth, tex.width);
                Assert.AreEqual(ShadeH, tex.height);

                // 上端行 (テクスチャ行 = 高さ-1) のレーン帯中央が透明であること。
                var left = new float[ShadeH];
                var right = new float[ShadeH];
                V6TopShadeHole.CorridorRows(ScreenW, ScreenH, ShadeH, ShadeH, left, right);
                int topRow = ShadeH - 1;
                float midX = (left[topRow] + right[topRow]) * 0.5f;
                int texX = Mathf.Clamp(Mathf.RoundToInt(midX / ScreenW * (tex.width - 1)), 0, tex.width - 1);
                Assert.AreEqual(0f, tex.GetPixel(texX, topRow).a, 0.001f,
                    "lane corridor must be clear at the top of the shade");

                // 帯の外側 (左端) は上端でほぼ不透明のまま (HUD 文字の可読性を維持)。
                float edgeAlpha = tex.GetPixel(2, topRow).a;
                Assert.Greater(edgeAlpha, 0.5f,
                    "outside the lane corridor the shade must keep its gradient");
            }
            finally
            {
                Object.DestroyImmediate(canvasGo);
            }
        }

        /// <summary>
        /// Issue #760: 歌詞ボックス (V6HudLyrics) のパネルスプライトにも
        /// レーン帯の制御が焼き込まれていることを確認する。
        /// 上部シェードと同じ <see cref="V6TopShadeHole.CorridorRows"/> の
        /// 領域オフセット版で帯を計算する。
        /// Issue #881: 演奏中 (ノーツが帯を通過し得る) のパネルも
        /// 回廊の内側をべた塗りにする — パネル/歌詞の視認性を
        /// ノーツの明るさより優先する (オーナー指摘の優先順位)。
        /// 検証点は「レーン帯の内側」かつ「枠の外側」の交差。
        /// </summary>
        [Test]
        public void LyricPanelSprite_HasLaneCorridorHole()
        {
            // 演奏中相当 (ノーツが画面に存在し得る) のスプライトを選ばせる。
            bool oldPlayed = GManager.played;
            bool oldComplete = GManager.judgmentsComplete;
            GManager.played = true;
            GManager.judgmentsComplete = false;
            var canvasGo = new GameObject("V6PanelHoleTests Canvas", typeof(Canvas));
            try
            {
                var canvasRect = (RectTransform)canvasGo.transform;
                canvasRect.sizeDelta = new Vector2(ScreenW, ScreenH);
                var view = FlashBeat.UI.V6.Game.Hud.V6GameHudView.Create(canvasRect);

                var box = view.LyricBoxForAudit;
                Assert.NotNull(box, "Lyric Box was not built");
                var img = box.GetComponent<Image>();
                Assert.NotNull(img, "Lyric Box has no Image");
                Assert.NotNull(img.sprite, "Lyric Box has no sprite");
                Assert.AreEqual(Image.Type.Simple, img.type,
                    "cutout sprite must be used as Simple (9-slice と併用しない)");

                var tex = img.sprite.texture;
                Assert.NotNull(tex);
                int w = tex.width;
                int h = tex.height;
                Assert.Greater(w, 100, "panel texture too narrow");
                Assert.Greater(h, 20, "panel texture too short");

                // パネルの実配置 (画面左上原点 px)。
                Vector2 slotOrigin = V6HudLogic.LyricSlotOrigin(ScreenW, ScreenH);
                float panelLeft = slotOrigin.x;
                float panelTop = slotOrigin.y;

                // パネル矩形に対応する corridor を本番と同じ関数で計算する。
                var left = new float[h];
                var right = new float[h];
                V6TopShadeHole.CorridorRows(ScreenW, ScreenH, panelTop, h, h, left, right);

                // 中央行 (上下の角丸に掛からない高さ) で、帯の内側も
                // べた塗り (Issue #881: パネルは全状態で不透明 —
                // 歌詞パネルの視認性がノーツの明るさより優先)。
                int midRow = h / 2;
                float midX = (left[midRow] + right[midRow]) * 0.5f;
                int texX = Mathf.Clamp(Mathf.RoundToInt(midX - panelLeft), 0, w - 1);
                Assert.Greater(tex.GetPixel(texX, midRow).a, 0.9f,
                    "corridor interior must be fully opaque during playback");

                // 帯の外側 (パネル左端から 4.5 px、中央行なので角丸の影響なし)
                // は不透明のまま (パネルの見た目を維持)。
                Assert.Greater(tex.GetPixel(4, midRow).a, 0.5f,
                    "outside the lane corridor the panel must keep its alpha");

                // Issue #881: 演奏中でも枠 (縁から FrameThicknessPx 以内) は
                // 回廊の内側で消えてはいけない — 「歌詞だけ浮いて
                // パネルが無い」状態の直接の回帰防止。
                int frameRow = h - 1 - 3;
                int frameX = w / 2;
                float fsx = panelLeft + frameX + 0.5f;
                Assert.Greater(fsx, left[frameRow],
                    "test premise: frame probe x must be inside the lane corridor");
                Assert.Less(fsx, right[frameRow],
                    "test premise: frame probe x must be inside the lane corridor");
                Assert.Greater(tex.GetPixel(frameX, frameRow).a, 0.5f,
                    "panel frame must stay visible inside the corridor during playback");
            }
            finally
            {
                Object.DestroyImmediate(canvasGo);
                GManager.played = oldPlayed;
                GManager.judgmentsComplete = oldComplete;
            }
        }

        /// <summary>
        /// Issue #789/#881: ノーツが画面に無い状態 (開始待ち等) の
        /// パネルは回廊の内側も含めてべた塗りで、角丸矩形の縁から
        /// <see cref="V6PanelCorridorHole.FrameThicknessPx"/> 以内の枠も
        /// 常に視認できることを確認する。
        /// 検証点は「レーン帯の内側」かつ「枠の内側」の交差 (上辺中央)。
        /// </summary>
        [Test]
        public void LyricPanelSprite_KeepsFrameInsideCorridor()
        {
            V6PanelCorridorHole.ClearSpriteCacheForTests();
            // アイドルスプライトは「ノーツ非描画」の状態で選ばれる
            // (開始待ち相当の played=false を明示する)。
            bool oldPlayed = GManager.played;
            bool oldComplete = GManager.judgmentsComplete;
            GManager.played = false;
            GManager.judgmentsComplete = false;
            var canvasGo = new GameObject("V6PanelHoleTests Canvas", typeof(Canvas));
            try
            {
                var canvasRect = (RectTransform)canvasGo.transform;
                canvasRect.sizeDelta = new Vector2(ScreenW, ScreenH);
                var view = FlashBeat.UI.V6.Game.Hud.V6GameHudView.Create(canvasRect);

                var img = view.LyricBoxForAudit.GetComponent<Image>();
                Assert.NotNull(img, "Lyric Box has no Image");
                var tex = img.sprite.texture;
                int w = tex.width;
                int h = tex.height;

                Vector2 slotOrigin = V6HudLogic.LyricSlotOrigin(ScreenW, ScreenH);
                var left = new float[h];
                var right = new float[h];
                V6TopShadeHole.CorridorRows(ScreenW, ScreenH, slotOrigin.y, h, h, left, right);

                // 上辺の中央・縁から 3px 内側 (角丸の影響が無い位置)。
                // テクスチャ行 h-1 がパネル上端、行 index は CorridorRows と同じ
                // (0 = 帯下端)。
                int frameRow = h - 1 - 3;
                int frameX = w / 2;
                float sx = slotOrigin.x + frameX + 0.5f;
                Assert.Greater(sx, left[frameRow],
                    "test premise: probe x must be inside the lane corridor");
                Assert.Less(sx, right[frameRow],
                    "test premise: probe x must be inside the lane corridor");

                Assert.Greater(tex.GetPixel(frameX, frameRow).a, 0.5f,
                    "panel frame must stay visible even inside the lane corridor");

                // 対照 (Issue #881): ノーツ非描画のアイドルでは回廊の内側も
                // べた塗り (完全なパネル) でなければならない。
                Assert.Greater(tex.GetPixel(w / 2, h / 2).a, 0.9f,
                    "idle panel must be fully opaque inside the lane corridor");
            }
            finally
            {
                Object.DestroyImmediate(canvasGo);
                GManager.played = oldPlayed;
                GManager.judgmentsComplete = oldComplete;
            }
        }

        /// <summary>
        /// GameScene.unity の Main Camera と同じ姿勢・画角のカメラを立てる
        /// (pos 0,4.9,-0.8 / pitch 54.8 / fov 60)。
        /// </summary>
        /// <summary>
        /// CorridorRows は Camera.main を使う。EditMode では他テストや
        /// 残存シーンの MainCamera が先に拾われうるので、有効な MainCamera を
        /// 一時的に無効化してこのカメラだけを残す (PlayfieldGeometryTests と
        /// 同じ手順)。戻り値は後で戻すカメラ一覧。
        /// </summary>
        private static List<Camera> IsolateMainCamera(Camera keep)
        {
            var disabled = new List<Camera>();
            var others = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
            for (int i = 0; i < others.Length; i++)
            {
                if (others[i] == keep || !others[i].CompareTag("MainCamera")) continue;
                if (!others[i].enabled) continue;
                others[i].enabled = false;
                disabled.Add(others[i]);
            }
            return disabled;
        }

        private static void RestoreCameras(List<Camera> disabled)
        {
            for (int i = 0; i < disabled.Count; i++)
            {
                if (disabled[i] != null) disabled[i].enabled = true;
            }
        }

        private static GameObject CreateLegacyCamera()
        {
            var go = new GameObject("V6TopShadeHoleTests Camera");
            go.tag = "MainCamera";
            var cam = go.AddComponent<Camera>();
            cam.transform.SetPositionAndRotation(
                new Vector3(0f, 4.9f, -0.8f), Quaternion.Euler(54.8f, 0f, 0f));
            cam.fieldOfView = 60f;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 1000f;
            return go;
        }

        /// <summary>
        /// Issue #675: 3D プレイフィールド (V6UiMode.Playfield = false) で
        /// CorridorRows が使うカメラ投影は、描画時に PlayfieldLaneGuides が
        /// 掛ける水平 fit 後の aspect と一致していなければならない。
        ///
        /// HUD (Top Shade スプライト) は PlayfieldLaneGuides の Start/Update が
        /// camera.aspect を fit するより先に構築されうる。その場合 corridor は
        /// fit 前の aspect で計算され、キャッシュされたまま残るため、非 16:9
        /// 画面ではレーン帯の抜きが実際のノーツ位置から横にずれ、シェードが
        /// ノーツの縁を暗くする。CorridorRows 側で fit を先に掛けるのが修正。
        ///
        /// 再現: fit 未適用の aspect (1.2) を仕込んで CorridorRows を呼び、
        /// その後 fit 済み aspect に戻して (＝実際の描画状態) ノーツ面上の
        /// 代表点を投影し、すべて corridor の内側に収まることを確認する。
        /// 修正前は corridor が aspect 1.2 の空間で計算されるため、端のレーンの
        /// ノーツが帯からはみ出して失敗する。
        /// </summary>
        [Test]
        public void CorridorRows_Legacy3D_CorridorMatchesFittedRender()
        {
            bool prevPlayfield = V6UiMode.Playfield;
            V6UiMode.Playfield = false;
            var camGo = CreateLegacyCamera();
            var cam = camGo.GetComponent<Camera>();
            var disabled = IsolateMainCamera(cam);
            try
            {
                float sw = cam.pixelWidth;
                float sh = cam.pixelHeight;
                if (sw <= 0f || sh <= 0f)
                {
                    Assert.Ignore("camera has no pixel size in this environment");
                }

                // HUD 構築が fit 適用より先に走った状態を再現する。
                cam.aspect = 1.2f;

                float shadeH = sh * 0.25f;
                const int rows = 64;
                var left = new float[rows];
                var right = new float[rows];
                V6TopShadeHole.CorridorRows(sw, sh, shadeH, rows, left, right);

                // ここから先は実際の描画状態 (PlayfieldLaneGuides.Update が
                // 毎フレーム fit を掛けた後) を再現する。
                float fitted = PlayfieldCameraFit.FittedAspect(PlayfieldCameraFit.ScreenAspect());
                cam.aspect = fitted;

                // ノーツ面上の代表点 (全レーン × アプローチ中の奥行き) のうち
                // シェード領域に写るものは、対応する行の corridor 内側にあること。
                var depths = new[] { 0.5f, 1.5f, 3f, 5f, 7.5f };
                int checkedPoints = 0;
                for (int lane = 0; lane < Judge.LaneCount; lane++)
                {
                    for (int d = 0; d < depths.Length; d++)
                    {
                        // ノーツ上面 (y = NotePlaneY + 半分の厚み 0.005 + 余裕)。
                        var p = new Vector3(Judge.LaneWorldX(lane),
                            MediaTimeline.NotePlaneY + 0.01f, depths[d]);
                        var vp = cam.WorldToViewportPoint(p);
                        if (vp.z <= 0f) continue;
                        float sx = vp.x * sw;
                        float yFromTop = (1f - vp.y) * sh;
                        if (yFromTop < 0f || yFromTop > shadeH) continue;

                        int row = Mathf.Clamp(Mathf.RoundToInt(
                            (1f - yFromTop / shadeH) * (rows - 1)), 0, rows - 1);
                        checkedPoints++;
                        Assert.GreaterOrEqual(sx, left[row] - 1f,
                            $"lane {lane} z {depths[d]}: note x {sx:F1}px left of corridor " +
                            $"[{left[row]:F1}, {right[row]:F1}] at row {row}");
                        Assert.LessOrEqual(sx, right[row] + 1f,
                            $"lane {lane} z {depths[d]}: note x {sx:F1}px right of corridor " +
                            $"[{left[row]:F1}, {right[row]:F1}] at row {row}");
                    }
                }
                Assert.Greater(checkedPoints, 0, "no note probe landed inside the shade region");
            }
            finally
            {
                V6UiMode.Playfield = prevPlayfield;
                RestoreCameras(disabled);
                Object.DestroyImmediate(camGo);
            }
        }

        /// <summary>
        /// 同じ再現条件で、CorridorRows が呼び出し時点で camera.aspect を
        /// fit 済みの値に揃えることを直接確認する (上のテストの前提条件)。
        /// </summary>
        [Test]
        public void CorridorRows_Legacy3D_AppliesCameraFit()
        {
            bool prevPlayfield = V6UiMode.Playfield;
            V6UiMode.Playfield = false;
            var camGo = CreateLegacyCamera();
            var cam = camGo.GetComponent<Camera>();
            var disabled = IsolateMainCamera(cam);
            try
            {
                cam.aspect = 1.2f; // fit 前の aspect を再現

                var left = new float[Rows];
                var right = new float[Rows];
                V6TopShadeHole.CorridorRows(ScreenW, ScreenH, ShadeH, Rows, left, right);

                float expected = PlayfieldCameraFit.FittedAspect(PlayfieldCameraFit.ScreenAspect());
                Assert.AreEqual(expected, cam.aspect, 0.0001f,
                    "corridor must be projected with the fitted aspect, " +
                    "the same one the renderer will use");
            }
            finally
            {
                V6UiMode.Playfield = prevPlayfield;
                RestoreCameras(disabled);
                Object.DestroyImmediate(camGo);
            }
        }
    }
}
