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

        /// <summary>上部シェード用の縦グラデーションスプライト (全 HUD で共有)。</summary>
        private static Sprite topShadeSprite;

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

        private void BuildTopShade()
        {
            var shade = V5Ui.Node(gameObject, "Top Shade");
            V5Ui.PinTop(V6Layout.RectOf(shade), m.TopShadeH, 0f);
            var img = shade.AddComponent<Image>();
            // 上端 #000000A6 → 下端 #00000000。一様な黒帯にすると下端の境界が見えるため、
            // 縦グラデーションにして動画の暗部へ溶かす (契約 §8)。
            img.sprite = TopShadeSprite();
            img.type = Image.Type.Simple;
            img.color = Color.white;
            img.raycastTarget = false;
        }

        /// <summary>
        /// 1×2 px の縦グラデーション。下が透明、上が TopShadeAlpha。
        /// バイリニア補間で高さ方向に滑らかに伸ばすため、解像度ごとに作り直さない。
        /// </summary>
        private static Sprite TopShadeSprite()
        {
            if (topShadeSprite != null)
            {
                return topShadeSprite;
            }

            var tex = new Texture2D(1, 2, TextureFormat.RGBA32, false);
            tex.name = "TopShadeGradient";
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.hideFlags = HideFlags.HideAndDontSave;
            tex.SetPixel(0, 0, new Color(0f, 0f, 0f, 0f));
            tex.SetPixel(0, 1, new Color(0f, 0f, 0f, TopShadeAlpha / 255f));
            tex.Apply(false, false);

            topShadeSprite = Sprite.Create(tex, new Rect(0f, 0f, 1f, 2f),
                new Vector2(0.5f, 0.5f), 1f);
            topShadeSprite.name = "TopShadeGradient";
            topShadeSprite.hideFlags = HideFlags.HideAndDontSave;
            return topShadeSprite;
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
                // contract json: bg #3A3A3A r4 + ラベル $text-muted (Key fs24, design 空間)
                var cap = V6Ui.KeyCap(keyGuideRow, V6HudLogic.RepresentativeKeyText(KeyBindings.GetKeys(lane)),
                    new Vector2(m.KeyCapW, m.KeyCapH), m.KeyCapLabelFs);
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
                keyCaps[lane].text = V6HudLogic.RepresentativeKeyText(KeyBindings.GetKeys(lane));
            }
        }
    }
}
