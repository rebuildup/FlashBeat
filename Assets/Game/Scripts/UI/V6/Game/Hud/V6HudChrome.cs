using System.Collections.Generic;
using FlashBeat.UI.V5;
using FlashBeat.UI.V6.Common;
using FlashBeat.UI.V6.Game.Playfield;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace FlashBeat.UI.V6.Game.Hud
{
    /// <summary>
    /// V6GameHudView の HUD 枠まわり部分 (Issue #385 Phase 2.2 で分割):
    /// 映像ディム / 上部シェード / 進捗バー / メニューボタン / キーガイド。
    /// partial class のためフィールド・メソッドは V6GameHudView 本体と共有する。
    /// </summary>
    public sealed partial class V6GameHudView
    {
        /// <summary>上部シェードのグラデーション (上端の不透明度)。</summary>
        private const byte TopShadeAlpha = 0xA6;

        /// <summary>
        /// 上部シェード用スプライトのキャッシュ (解像度ごと)。
        /// Issue #675: ノーツが通るレーン帯を抜いたグラデーションなので、
        /// キーは (画面幅, 画面高さ, シェード高さ) で分ける。
        /// </summary>
        private static readonly Dictionary<string, Sprite> topShadeSprites =
            new Dictionary<string, Sprite>();

        // --- 生成した部品 (枠まわり) ---
        private RectTransform progressFill;
        private RectTransform progressTrack;
        private GameObject keyGuideRow;
        private readonly List<TextMeshProUGUI> keyCaps = new List<TextMeshProUGUI>();

        private void BuildDim()
        {
            var dim = V5Ui.Node(gameObject, "Video Dim");
            var rt = V6Layout.RectOf(dim);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var img = dim.AddComponent<Image>();
            img.color = V6LaneGeometry.VideoDimColor;
            img.raycastTarget = false;
        }

        private Image topShadeImage;

        private void BuildTopShade()
        {
            var shade = V5Ui.Node(gameObject, "Top Shade");
            V5Ui.PinTop(V6Layout.RectOf(shade), m.TopShadeH, 0f);
            var img = shade.AddComponent<Image>();
            topShadeImage = img;
            // 上端 #000000A6 → 下端 #00000000。一様な黒帯にすると下端の境界が見えるため、
            // 縦グラデーションにして動画の暗部へ溶かす (契約 §8)。
            // Issue #675: レーン帯 (ノーツが通る領域) は抜く。シェードは uGUI overlay で
            // 3D の深度テストを通らず、グラデーションがアプローチ中のノーツを暗くしていた。
            img.sprite = TopShadeSprite(CanvasSize(), m.TopShadeH);
            img.type = Image.Type.Simple;
            img.color = Color.white;
            img.raycastTarget = false;
        }

        /// <summary>
        /// 縦グラデーション (下が透明、上が TopShadeAlpha) に、レーン帯の抜きを
        /// 焼き込んだスプライト。高さはシェード高さと同じ px 数なので、行ごとの
        /// アルファがそのまま画面のグラデーションになる (バイリニア補間の
        /// 縦方向のにじみは無い)。横方向だけ <see cref="V6TopShadeHole.TextureWidth"/>
        /// texel を画面幅へ引き伸ばす (帯の縁が数 px ぼけるが、シェードは元々
        /// 滑らかなグラデーションなので目立たない)。
        /// </summary>
        private static Sprite TopShadeSprite(Vector2 screenSize, int shadeH)
        {
            string key = screenSize.x.ToString("0") + "x" + screenSize.y.ToString("0")
                + "x" + shadeH;
            if (topShadeSprites.TryGetValue(key, out var cached) && cached != null)
            {
                return cached;
            }

            int w = V6TopShadeHole.TextureWidth;
            int h = Mathf.Max(2, shadeH);
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.name = "TopShadeGradient";
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.hideFlags = HideFlags.HideAndDontSave;

            float[] corridorLeft = new float[h];
            float[] corridorRight = new float[h];
            V6TopShadeHole.CorridorRows(screenSize.x, screenSize.y, shadeH, h,
                corridorLeft, corridorRight);

            float topAlpha = TopShadeAlpha / 255f;
            var clear = new Color(0f, 0f, 0f, 0f);
            for (int y = 0; y < h; y++)
            {
                // テクスチャ行 0 = 下端。下端は透明、上端が TopShadeAlpha の線形グラデーション。
                float gradient = topAlpha * (y / (float)(h - 1));
                var shaded = new Color(0f, 0f, 0f, gradient);
                float left = corridorLeft[y];
                float right = corridorRight[y];
                for (int x = 0; x < w; x++)
                {
                    float sx = x / (float)(w - 1) * screenSize.x;
                    float a = V6TopShadeHole.ShadeAlpha(sx, left, right, gradient);
                    tex.SetPixel(x, y, a <= 0f ? clear : shaded);
                }
            }

            tex.Apply(false, false);

            var sprite = Sprite.Create(tex, new Rect(0f, 0f, w, h),
                new Vector2(0.5f, 0.5f), 1f);
            sprite.name = "TopShadeGradient";
            sprite.hideFlags = HideFlags.HideAndDontSave;

            // 解像度プロファイルは 3 種類しかないが、EditMode テスト等で
            // 任意サイズの HUD が作られ得るのでキャッシュは頭打ちにする。
            if (topShadeSprites.Count >= 16)
            {
                topShadeSprites.Clear();
            }

            topShadeSprites[key] = sprite;
            return sprite;
        }

        private void BuildProgressBar()
        {
            var bar = V5Ui.Node(gameObject, "Progress Bar");
            var rt = V5Ui.PinTop(V6Layout.RectOf(bar), m.ProgressH, 0f);
            var img = bar.AddComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0xAA / 255f);
            img.raycastTarget = false;
            progressTrack = rt;

            var fill = V5Ui.Node(bar, "Fill");
            var fillRt = V6Layout.RectOf(fill);
            fillRt.anchorMin = new Vector2(0f, 0.5f);
            fillRt.anchorMax = new Vector2(0f, 0.5f);
            fillRt.pivot = new Vector2(0f, 0.5f);
            fillRt.anchoredPosition = Vector2.zero;
            fillRt.sizeDelta = new Vector2(0f, m.ProgressH);
            var fillImg = fill.AddComponent<Image>();
            fillImg.color = V5Tokens.ButtonPrimaryBg; // design: #EDEDED
            fillImg.raycastTarget = false;
            progressFill = fillRt;
        }

        private void BuildMenuButton()
        {
            var btn = V5Ui.Panel(gameObject, "Menu Button",
                new Vector2(m.MenuW, m.MenuH), V5Tokens.Key4, 8f);
            var rt = V6Layout.RectOf(btn);
            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-m.Margin, -m.Margin);

            // design T3U7u6: [ラベル fs22/30][gap 10][KeyCap] を 180×48 の中で中央寄せ。
            // KeyCap は padding [0,10] の自動幅・自動高 (55×32 @(112,8) at FHD)。
            // 以前は layout padding 16 / gap 8 / 固定幅 52 で置き、さらに Key の fs が
            // 未代入 (0) だったため「Esc」が描画されなかった。
            var label = V5Ui.Text(btn, "Label", "メニュー", m.MenuFs, V5Tokens.Text,
                TextAlignmentOptions.Left);
            float labelW = MeasureTextWidth(label, "メニュー", m.MenuFs);

            var cap = V5Ui.Panel(btn, "KeyCap",
                new Vector2(0f, m.MenuCapH), new Color(0f, 0f, 0f, 0x40 / 255f), 4f);
            var capKey = V5Ui.Text(cap, "Key", "Esc", m.MenuFs, V5Tokens.TextMuted,
                TextAlignmentOptions.Center);
            V6Layout.StretchFill(capKey.rectTransform);
            float capW = MeasureTextWidth(capKey, "Esc", m.MenuFs) + V6Ui.KeyCapPaddingX * 2f;
            V6Layout.RectOf(cap).sizeDelta = new Vector2(capW, m.MenuCapH);

            float total = labelW + m.MenuGap + capW;
            float rowX = Mathf.Round((m.MenuW - total) * 0.5f);
            float rowY = Mathf.Round((m.MenuH - m.MenuCapH) * 0.5f);
            PlaceTopLeft(label.rectTransform, rowX, -rowY);
            V6Layout.RectOf(label).sizeDelta = new Vector2(labelW, m.MenuCapH);
            PlaceTopLeft(V6Layout.RectOf(cap), rowX + labelW + m.MenuGap, -rowY);

            MenuButton = btn.AddComponent<Button>();
            MenuButton.targetGraphic = btn.GetComponent<Image>();
            V6ButtonPressFeedback.Attach(btn);
            MenuButton.onClick.AddListener(OpenPauseMenu);
        }

        /// <summary>
        /// HUD のメニューボタンの既定リスナー。v6 ポーズが有効なときは何もしない —
        /// V6PauseRuntime が同じボタンを v6 ポーズへ接線する。ここで v5 PauseMenuView を
        /// 開くと (非表示のまま活性化して) v6 ポーズと二重表示・二重入力になる。
        /// </summary>
        private void OpenPauseMenu()
        {
            if (FlashBeat.UI.V6.V6UiMode.GameHud && FlashBeat.UI.V6.Game.Pause.V6PauseView.Enabled)
            {
                return;
            }

            var pause = FindFirstObjectByType<PauseMenuView>();
            if (pause != null)
            {
                pause.Open();
            }
        }

        private void BuildKeyGuide()
        {
            if (PlaySession.KeyGuide == KeyGuideMode.Off)
            {
                return;
            }

            keyGuideRow = V5Ui.Node(gameObject, "Key Guide");
            V5Ui.PinBottom(V6Layout.RectOf(keyGuideRow), m.KeyGuideH, m.KeyGuideBottom);

            for (int lane = 0; lane < KeyBindings.LaneCount; lane++)
            {
                // Issue #658: KeyGuideMode.Full ではレーンの全有効キーを出す
                // (v5 GuideKeyText と同じ規約)。キー表は KeyBindings だけを見る。
                string keyText = V6HudLogic.GuideKeyText(lane);
                float keyFs = V6HudLogic.GuideKeyFontSize(
                    m.KeyCapW, m.KeyCapLabelFs, keyText, V6Ui.KeyCapPaddingX);
                // contract json: bg #3A3A3A r4 + ラベル $text-muted (Key fs24, design 空間)
                var cap = V6Ui.KeyCap(keyGuideRow, keyText,
                    new Vector2(m.KeyCapW, m.KeyCapH), keyFs);
                var label = cap.GetComponentInChildren<TextMeshProUGUI>();
                var rt = V6Layout.RectOf(cap);
                rt.anchorMin = new Vector2(0f, 0.5f);
                rt.anchorMax = new Vector2(0f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2(m.KeyColumnCenter(lane), 0f);
                rt.sizeDelta = new Vector2(m.KeyCapW, m.KeyCapH);
                keyCaps.Add(label);
            }

            RefreshKeyCaps();
        }

        /// <summary>KeyBindings (正本) からキーガイドを作り直す (設定変更で即時)。</summary>
        private void RebuildKeyGuide()
        {
            if (keyGuideRow == null)
            {
                if (PlaySession.KeyGuide != KeyGuideMode.Off)
                {
                    BuildKeyGuide();
                }

                return;
            }

            if (PlaySession.KeyGuide == KeyGuideMode.Off)
            {
                V5GameUi.DestroyNow(keyGuideRow);
                keyGuideRow = null;
                keyCaps.Clear();
                return;
            }

            for (int i = keyCaps.Count - 1; i >= 0; i--)
            {
                V5GameUi.DestroyNow(keyCaps[i].gameObject);
            }

            keyCaps.Clear();
            BuildKeyGuide();
        }

        private void RefreshKeyCaps()
        {
            for (int lane = 0; lane < keyCaps.Count; lane++)
            {
                string keyText = V6HudLogic.GuideKeyText(lane);
                keyCaps[lane].text = keyText;
                // フル表示で縮んだ fs をキー数の変化に追従させる。
                keyCaps[lane].fontSize = V6HudLogic.GuideKeyFontSize(
                    m.KeyCapW, m.KeyCapLabelFs, keyText, V6Ui.KeyCapPaddingX);
            }
        }
    }
}
