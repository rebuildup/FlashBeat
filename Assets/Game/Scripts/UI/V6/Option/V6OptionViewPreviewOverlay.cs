using TMPro;
using UnityEngine;
using UnityEngine.UI;
using FlashBeat.Gameplay;
using FlashBeat.UI.V5;
using FlashBeat.UI.V6;
using FlashBeat.UI.V6.Game.Hud;
using FlashBeat.UI.V6.Game.Playfield;

namespace FlashBeat.UI.V6.Option
{
    /// <summary>
    /// V6OptionView の Game Preview オーバーレイ部分 (Issue #607)。
    ///
    /// well (464x280) の RawImage (3D シーン) の手前に、ゲーム体験の
    /// 2D 要素を重ねる:
    ///
    /// - 歌詞行: <see cref="V6OptionPreviewLyrics"/> が返す現在行を
    ///   well 上部に表示 (歌詞 OFF・データ無しでは出さない)。
    /// - 判定ポップ: <see cref="V6OptionPreviewRig"/> の JudgeQueue が
    ///   出した判定を、レーン位置の上・判定線の直上へ表示する。
    ///   ラベル・色・寿命・立ち上がりは <see cref="V6HudLogic"/>
    ///   (本編 V6HudVerdict と同じ共有関数) で、ここでは配置だけを行う。
    ///
    /// すべて well の子なので、Preview パネルの再構築で一緒に消える。
    /// 3D 側の状態 (疑似時計・判定キュー・歌詞行) は rig が持ち、
    /// ここは表示だけを担う。GManager の状態には触れない。
    /// </summary>
    public sealed partial class V6OptionView
    {
        /// <summary>判定ポップのフォントサイズ (well 280px 高の中で読める大きさ)。</summary>
        private const float PreviewVerdictFontSize = 15f;

        /// <summary>歌詞行のフォントサイズ (design fs44 を well 高さへ厳密に
        /// 写すと約 13px で同期確認に小さすぎるため、判読性を優先して 16px)。</summary>
        private const float PreviewLyricFontSize = 16f;

        private RectTransform previewOverlayRt;
        private GameObject previewLyricBox;
        private TextMeshProUGUI previewLyricText;
        private string previewLastLyric = string.Empty;
        private readonly TextMeshProUGUI[] previewVerdictTexts = new TextMeshProUGUI[Judge.LaneCount];
        private readonly string[] previewVerdictShown = new string[Judge.LaneCount];

        /// <summary>
        /// well の RawImage の後ろにオーバーレイを構築する
        /// (<see cref="BuildGamePreview"/> から呼ぶ)。
        /// </summary>
        private void BuildPreviewOverlay(GameObject well)
        {
            previewOverlayRt = null;
            previewLyricBox = null;
            previewLyricText = null;
            previewLastLyric = string.Empty;
            for (int i = 0; i < previewVerdictTexts.Length; i++)
            {
                previewVerdictTexts[i] = null;
                previewVerdictShown[i] = null;
            }

            var overlay = new GameObject("Preview Overlay", typeof(RectTransform));
            overlay.transform.SetParent(well.transform, false);
            var ort = overlay.GetComponent<RectTransform>();
            ort.anchorMin = Vector2.zero;
            ort.anchorMax = Vector2.one;
            ort.offsetMin = Vector2.zero;
            ort.offsetMax = Vector2.zero;
            previewOverlayRt = ort;

            BuildPreviewLyric(overlay);
            for (int lane = 0; lane < Judge.LaneCount; lane++)
            {
                previewVerdictTexts[lane] = MakePreviewText(overlay, "Verdict " + lane,
                    PreviewVerdictFontSize, V5Tokens.Text);
            }
        }

        /// <summary>歌詞行: well 上端の帯 (本編の歌詞スロットに相当する位置)。</summary>
        private void BuildPreviewLyric(GameObject overlay)
        {
            var boxGo = new GameObject("Preview Lyric Box", typeof(RectTransform), typeof(Image));
            boxGo.transform.SetParent(overlay.transform, false);
            var brt = boxGo.GetComponent<RectTransform>();
            brt.anchorMin = new Vector2(0.06f, 1f);
            brt.anchorMax = new Vector2(0.94f, 1f);
            brt.pivot = new Vector2(0.5f, 1f);
            brt.offsetMin = new Vector2(0f, -36f);
            brt.offsetMax = new Vector2(0f, -4f);
            var image = boxGo.GetComponent<Image>();
            // 本編の歌詞ボックスと同系の半透明黒 (V6HudLyrics.LyricBoxColor)。
            image.color = new Color(0f, 0f, 0f, 0x99 / 255f);
            image.raycastTarget = false;
            previewLyricBox = boxGo;
            boxGo.SetActive(false);

            var textGo = new GameObject("Text", typeof(RectTransform));
            textGo.transform.SetParent(boxGo.transform, false);
            var trt = textGo.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(8f, 0f);
            trt.offsetMax = new Vector2(-8f, 0f);
            previewLyricText = textGo.AddComponent<TextMeshProUGUI>();
            previewLyricText.font = PreviewFont();
            previewLyricText.fontSize = PreviewLyricFontSize;
            previewLyricText.color = V5Tokens.Text;
            previewLyricText.alignment = TextAlignmentOptions.Center;
            previewLyricText.textWrappingMode = TextWrappingModes.NoWrap;
            previewLyricText.overflowMode = TextOverflowModes.Ellipsis;
            previewLyricText.raycastTarget = false;
        }

        /// <summary>well 中央基準の TMP テキスト (判定ポップ用)。</summary>
        private static TextMeshProUGUI MakePreviewText(GameObject parent, string name,
            float fontSize, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent.transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(120f, 34f);

            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.font = PreviewFont();
            tmp.fontSize = fontSize;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.raycastTarget = false;
            go.SetActive(false);
            return tmp;
        }

        /// <summary>フォントは v6 部品と同じ解決順 (TMP_Settings → Resources)。</summary>
        private static TMP_FontAsset PreviewFont()
        {
            return TMP_Settings.defaultFontAsset != null
                ? TMP_Settings.defaultFontAsset
                : Resources.Load<TMP_FontAsset>("NotoSansJP-Medium SDF");
        }

        /// <summary>
        /// オーバーレイの毎フレーム更新。<see cref="V6OptionView.Tick"/> から
        /// <see cref="V6OptionPreviewRig.Tick"/> の直後に呼ぶ。
        /// rig が持つ状態 (歌詞行・判定ポップ) を読んで表示へ写すだけで、
        /// ここから GManager / Judge / MusicManager へは書き込まない。
        /// </summary>
        private void UpdatePreviewOverlay()
        {
            if (previewRig == null || previewOverlayRt == null)
            {
                return;
            }

            // 歌詞: 行が変わったときだけ書き換える (毎フレームの dirty を避ける)。
            string lyric = previewRig.CurrentLyric;
            string lyricKey = lyric ?? string.Empty;
            if (lyricKey != previewLastLyric)
            {
                previewLastLyric = lyricKey;
                if (previewLyricText != null)
                {
                    previewLyricText.text = lyricKey;
                }
                if (previewLyricBox != null)
                {
                    V6LayoutWrites.ActiveIfChanged(previewLyricBox, lyricKey.Length > 0);
                }
            }

            // 判定ポップ: rig の JudgeQueue が出した判定をレーン位置へ配置する。
            Vector2 wellSize = previewOverlayRt.rect.size;
            Camera cam = previewRig.PreviewCamera;
            for (int lane = 0; lane < Judge.LaneCount; lane++)
            {
                var tmp = previewVerdictTexts[lane];
                if (tmp == null)
                {
                    continue;
                }

                if (!previewRig.TryGetVerdict(lane, out string label, out float startTime))
                {
                    V6LayoutWrites.ActiveIfChanged(tmp.gameObject, false);
                    continue;
                }

                float elapsed = Time.unscaledTime - startTime;
                float remaining = V6HudLogic.VerdictTotalSeconds - elapsed;
                float alpha = V6HudLogic.VerdictAlpha(remaining);

                // 本編 Judge.EffectPosition (x = lane - 3.5, y = 0.76, z = 0.15)
                // と同じ点をプレビューカメラでビューポートへ写す。
                Vector3 world = new Vector3(Judge.LaneWorldX(lane), 0.76f, 0.15f);
                Vector3 vp = cam != null ? cam.WorldToViewportPoint(world) : Vector3.zero;
                if (vp.z <= 0f)
                {
                    V6LayoutWrites.ActiveIfChanged(tmp.gameObject, false);
                    continue;
                }

                // 静止位置 = 判定線の VerdictRestGapAboveLine 上、出現時は
                // VerdictRiseOffset だけ判定線側 (下) から立ち上がる。
                // design px → 画面比はデザイン高さ 972 で揃える
                // (V6HudLogic の px 定数をそのまま比率化して使う)。
                float restVp = V6HudLogic.VerdictRestGapAboveLine / V6LaneGeometry.DesignHeight;
                float riseVp = V6HudLogic.VerdictRiseOffset(elapsed) / V6LaneGeometry.DesignHeight;
                float vpY = vp.y + restVp - riseVp;

                var rt = tmp.rectTransform;
                V6LayoutWrites.AnchoredPositionIfChanged(rt,
                    (vp.x - 0.5f) * wellSize.x, (vpY - 0.5f) * wellSize.y);

                // ラベルが変わったときだけ文字と色を更新し、
                // アルファは共有のフェード規則で毎フレーム追従する。
                if (previewVerdictShown[lane] != label)
                {
                    previewVerdictShown[lane] = label;
                    tmp.text = label;
                    tmp.color = V6HudLogic.VerdictColor(label);
                }
                V6LayoutWrites.AlphaIfChanged(tmp, alpha);
                V6LayoutWrites.ActiveIfChanged(tmp.gameObject, true);
            }
        }

        // --- 検証ハーネス用 (EditMode テストが公開 API 経由で駆動する) ---

        /// <summary>
        /// プレビューだけを 1 ステップ進める (実機の V6OptionViewDriver.Update
        /// が呼ぶ Tick のプレビュー部分と同じ経路。メニュー入力は処理しない)。
        /// </summary>
        public void TickPreviewForAudit(float mediaTime, float deltaTime)
        {
            if (previewRig == null)
            {
                return;
            }

            previewRig.TickSimulated(mediaTime, deltaTime);
            UpdatePreviewOverlay();
        }

        /// <summary>レーンフラッシュの現在 α (0..LaneFlashPeakAlpha)。</summary>
        public float PreviewLaneFlashAlphaForAudit(int lane)
        {
            return previewRig == null ? 0f : previewRig.LaneFlashAlphaForAudit(lane);
        }

        /// <summary>レーンに表示中の判定ラベル (PERFECT 等)。無ければ null。</summary>
        public string PreviewVerdictForAudit(int lane)
        {
            if (previewRig == null)
            {
                return null;
            }

            return previewRig.TryGetVerdict(lane, out string label, out _) ? label : null;
        }

        /// <summary>いま歌詞スロットに出ている本文 (無ければ null)。</summary>
        public string PreviewLyricForAudit
        {
            get { return previewRig == null ? null : previewRig.CurrentLyric; }
        }

        /// <summary>これまでに出した判定ポップの累計数。</summary>
        public int PreviewJudgementCountForAudit
        {
            get { return previewRig == null ? 0 : previewRig.JudgementCount; }
        }
    }
}
