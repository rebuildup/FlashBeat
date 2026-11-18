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
    /// V6OptionView の Game Preview オーバーレイ部分 (Issue #607 / 再構築 #677)。
    ///
    /// well (464x280) の RawImage (3D シーン) の手前に、ゲーム体験の
    /// 2D 要素を重ねる:
    ///
    /// - 歌詞行: <see cref="V6OptionPreviewLyrics"/> が返す現在行を
    ///   本編と同じスロット位置・寸法で表示する (歌詞 OFF・データ無しでは出さない)。
    /// - 判定ポップ: <see cref="V6OptionPreviewRig"/> の JudgeQueue が
    ///   出した判定を、本編 <see cref="V6HudVerdict"/> と同じ規則で表示する。
    ///   ラベル・色・寿命・立ち上がり・フォントサイズは <see cref="V6HudLogic"/>
    ///   と <see cref="V6HudMetrics"/> (本編と同じ共有関数・同じプロファイル選択)、
    ///   位置は本編レガシー経路と同じ「判定線の実投影 + VerdictRestGapAboveLine」
    ///   から求める。独自の固定 px 定数は使わない。
    ///
    /// 座標系: 本編 HUD の px 定数 (VerdictRestGapAboveLine / LyricSlotOrigin
    /// など) は実スクリーン px。プレビューの RT はカメラの縦 FOV をそのまま
    /// 見せるので、画面 px → well px は well/Screen の比で線形に写る
    /// (垂直方向は射影が一致、水平方向はカメラ aspect 固定の横圧縮が
    /// ビューポート比としてそのまま効く)。
    ///
    /// すべて well の子なので、Preview パネルの再構築で一緒に消える。
    /// 3D 側の状態 (疑似時計・判定キュー・歌詞行) は rig が持ち、
    /// ここは表示だけを担う。GManager の状態には触れない。
    /// </summary>
    public sealed partial class V6OptionView
    {
        private RectTransform previewOverlayRt;
        private GameObject previewLyricBox;
        private TextMeshProUGUI previewLyricText;
        private string previewLastLyric = string.Empty;
        private readonly TextMeshProUGUI[] previewVerdictTexts = new TextMeshProUGUI[Judge.LaneCount];
        private readonly string[] previewVerdictShown = new string[Judge.LaneCount];

        /// <summary>最後に適用したスケール (Screen 解像度・well サイズの変化検知用)。</summary>
        private Vector2 previewLastScreenSize = Vector2.negativeInfinity;
        private Vector2 previewLastWellSize = Vector2.negativeInfinity;

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
            previewLastScreenSize = Vector2.negativeInfinity;
            previewLastWellSize = Vector2.negativeInfinity;
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
                previewVerdictTexts[lane] = MakePreviewText(overlay, "Verdict " + lane);
            }
        }

        /// <summary>
        /// 歌詞行: 本編 <see cref="V6HudLyrics"/> と同じスロット
        /// (<see cref="V6HudLogic.LyricSlotOrigin"/> / <see cref="V6HudLogic.LyricSize"/>)
        /// を well 座標へ写して置く。実際の配置は
        /// <see cref="UpdatePreviewOverlay"/> が解像度変化に追従して行う。
        /// </summary>
        private void BuildPreviewLyric(GameObject overlay)
        {
            var boxGo = new GameObject("Preview Lyric Box", typeof(RectTransform), typeof(Image));
            boxGo.transform.SetParent(overlay.transform, false);
            var brt = boxGo.GetComponent<RectTransform>();
            // 本編と同じ上中央アンカー (V6HudLyrics.LyricSlotAnchoredPosition と同型)。
            brt.anchorMin = new Vector2(0.5f, 1f);
            brt.anchorMax = new Vector2(0.5f, 1f);
            brt.pivot = new Vector2(0.5f, 1f);
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
            previewLyricText = textGo.AddComponent<TextMeshProUGUI>();
            previewLyricText.font = PreviewFont();
            previewLyricText.color = V5Tokens.Text;
            previewLyricText.alignment = TextAlignmentOptions.Center;
            previewLyricText.textWrappingMode = TextWrappingModes.NoWrap;
            previewLyricText.overflowMode = TextOverflowModes.Ellipsis;
            previewLyricText.raycastTarget = false;
        }

        /// <summary>well 中央基準の TMP テキスト (判定ポップ用)。寸法・fs は毎フレーム本編写像で設定。</summary>
        private static TextMeshProUGUI MakePreviewText(GameObject parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent.transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);

            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.font = PreviewFont();
            tmp.color = V5Tokens.Text;
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
        /// 本編 HUD px → well px の換算係数。
        /// well は画面全体 (縦 FOV 全域) を見せるので、画面 px を
        /// well/Screen の比で線形に縮める。x はビューポート比、
        /// y は画面高比でそれぞれ独立に換算する。
        /// </summary>
        private static Vector2 PreviewScale(Vector2 wellSize)
        {
            float screenW = Mathf.Max(1f, Screen.width);
            float screenH = Mathf.Max(1f, Screen.height);
            return new Vector2(wellSize.x / screenW, wellSize.y / screenH);
        }

        /// <summary>本編 HUD と同じプロファイル解決 (V6HudVerdict / V6HudLyrics と同じ入力)。</summary>
        private static V6HudMetrics PreviewHudMetrics()
        {
            return V6HudMetrics.ForResolution(Screen.width, Screen.height);
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

            Vector2 wellSize = previewOverlayRt.rect.size;
            Vector2 screenSize = new Vector2(Screen.width, Screen.height);
            Vector2 scale = PreviewScale(wellSize);
            bool layoutDirty =
                !V6LayoutWrites.NearlyEqual(previewLastWellSize.x, wellSize.x, V6LayoutWrites.PositionEpsilon)
                || !V6LayoutWrites.NearlyEqual(previewLastWellSize.y, wellSize.y, V6LayoutWrites.PositionEpsilon)
                || !V6LayoutWrites.NearlyEqual(previewLastScreenSize.x, screenSize.x, V6LayoutWrites.PositionEpsilon)
                || !V6LayoutWrites.NearlyEqual(previewLastScreenSize.y, screenSize.y, V6LayoutWrites.PositionEpsilon);
            if (layoutDirty)
            {
                previewLastWellSize = wellSize;
                previewLastScreenSize = screenSize;
                ApplyPreviewLayout(wellSize, scale);
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

            // 判定ポップ: rig の JudgeQueue が出した判定を、本編 V6HudVerdict と
            // 同じ写像 (レーン x = 判定エフェクト投影、y = 判定線投影 +
            // VerdictRestGapAboveLine、fs = m.VerdictFs) で well へ配置する。
            Camera cam = previewRig.PreviewCamera;
            Vector3 judgeScreen = cam != null
                ? cam.WorldToViewportPoint(new Vector3(0f, 0.76f, 0.15f))
                : Vector3.zero;
            float screenH = Mathf.Max(1f, Screen.height);
            for (int lane = 0; lane < Judge.LaneCount; lane++)
            {
                var tmp = previewVerdictTexts[lane];
                if (tmp == null)
                {
                    continue;
                }

                if (cam == null || judgeScreen.z <= 0f
                    || !previewRig.TryGetVerdict(lane, out string label, out float startTime))
                {
                    V6LayoutWrites.ActiveIfChanged(tmp.gameObject, false);
                    continue;
                }

                // 本編 UpdateLaneGrids と同じ点 (Judge.EffectPosition 相当
                // (LaneWorldX, 0.76, 0.15)) をプレビューカメラでビューポートへ写す。
                Vector3 world = new Vector3(Judge.LaneWorldX(lane), 0.76f, 0.15f);
                Vector3 vp = cam.WorldToViewportPoint(world);
                if (vp.z <= 0f)
                {
                    V6LayoutWrites.ActiveIfChanged(tmp.gameObject, false);
                    continue;
                }

                float elapsed = Time.unscaledTime - startTime;
                float remaining = V6HudLogic.VerdictTotalSeconds - elapsed;
                float alpha = V6HudLogic.VerdictAlpha(remaining);

                // 本編 verdictBaseY の net 効果: 判定テキスト中心の
                // 画面下原点 y = 判定線 + VerdictRestGapAboveLine − VerdictRiseOffset。
                // 定数は実スクリーン px なので Screen.height でビューポート比へ、
                // そこから well px へ写す。
                float restFrac = judgeScreen.y + V6HudLogic.VerdictRestGapAboveLine / screenH;
                float riseFrac = V6HudLogic.VerdictRiseOffset(elapsed) / screenH;
                float posFrac = restFrac - riseFrac;

                var rt = tmp.rectTransform;
                V6LayoutWrites.AnchoredPositionIfChanged(rt,
                    (vp.x - 0.5f) * wellSize.x, (posFrac - 0.5f) * wellSize.y);

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

        /// <summary>
        /// 歌詞ボックスと判定テキストの寸法・フォントサイズを本編写像で設定する
        /// (解像度・well サイズが変わったときだけ呼ぶ)。
        /// </summary>
        private void ApplyPreviewLayout(Vector2 wellSize, Vector2 scale)
        {
            V6HudMetrics m = PreviewHudMetrics();
            float screenW = Mathf.Max(1f, Screen.width);
            float screenH = Mathf.Max(1f, Screen.height);

            // 歌詞スロット: 本編 V6HudLyrics.BuildLyric / LyricSlotAnchoredPosition
            // と同じ原点・幅・高さを well 座標へ写す。
            if (previewLyricBox != null)
            {
                var brt = (RectTransform)previewLyricBox.transform;
                Vector2 slotOrigin = V6HudLogic.LyricSlotOrigin(screenW, screenH);
                float slotWidth = V6HudLogic.LyricSlotWidth(screenW);
                Vector2 slotSize = V6HudLogic.LyricSize(m.LyricFs);
                brt.sizeDelta = new Vector2(slotWidth * scale.x, slotSize.y * scale.y);
                // 上中央アンカー: x = スロット中心 − well 中央、y = −スロット上端。
                brt.anchoredPosition = new Vector2(
                    (slotOrigin.x + slotWidth * 0.5f) * scale.x - wellSize.x * 0.5f,
                    -slotOrigin.y * scale.y);
            }

            if (previewLyricText != null)
            {
                var trt = previewLyricText.rectTransform;
                // 本編 VerticalLayoutGroup の padding (LyricPaddingX/Y) を well へ写す。
                trt.offsetMin = new Vector2(V6HudLogic.LyricPaddingX * scale.x,
                    V6HudLogic.LyricPaddingY * scale.y);
                trt.offsetMax = new Vector2(-V6HudLogic.LyricPaddingX * scale.x,
                    -V6HudLogic.LyricPaddingY * scale.y);
                // 本編 ScaledLyricFs = m.LyricFs (LyricFs 基準 fs44 への比)。
                previewLyricText.fontSize = m.LyricFs * scale.y;
            }

            // 判定ポップ: 本編 V6HudVerdict と同じ m.VerdictW/H/Fs を well へ写す。
            float verdictW = m.VerdictW * scale.x;
            float verdictH = m.VerdictH * scale.y;
            float verdictFs = m.VerdictFs * scale.y;
            for (int lane = 0; lane < previewVerdictTexts.Length; lane++)
            {
                var tmp = previewVerdictTexts[lane];
                if (tmp == null)
                {
                    continue;
                }
                tmp.rectTransform.sizeDelta = new Vector2(verdictW, verdictH);
                tmp.fontSize = verdictFs;
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

        /// <summary>拍フラッシュ (BGFlash 相当) の現在 α。</summary>
        public float PreviewBeatFlashAlphaForAudit
        {
            get { return previewRig == null ? 0f : previewRig.BeatFlashAlphaForAudit; }
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

        /// <summary>歌詞ボックスの RectTransform (配置検証用)。無ければ null。</summary>
        public RectTransform PreviewLyricBoxRectForAudit
        {
            get
            {
                return previewLyricBox == null ? null
                    : previewLyricBox.GetComponent<RectTransform>();
            }
        }

        /// <summary>歌詞テキストの TMP (フォントサイズ検証用)。無ければ null。</summary>
        public TextMeshProUGUI PreviewLyricTextForAudit
        {
            get { return previewLyricText; }
        }

        /// <summary>判定ポップの RectTransform (配置検証用)。無ければ null。</summary>
        public RectTransform PreviewVerdictRectForAudit(int lane)
        {
            return lane >= 0 && lane < previewVerdictTexts.Length && previewVerdictTexts[lane] != null
                ? previewVerdictTexts[lane].rectTransform
                : null;
        }

        /// <summary>判定ポップの TMP (フォントサイズ検証用)。無ければ null。</summary>
        public TextMeshProUGUI PreviewVerdictTextForAudit(int lane)
        {
            return lane >= 0 && lane < previewVerdictTexts.Length ? previewVerdictTexts[lane] : null;
        }
    }
}
