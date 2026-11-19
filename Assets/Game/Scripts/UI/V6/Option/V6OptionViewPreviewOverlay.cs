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
    ///   ラベル・色・寿命・落下アニメーションは <see cref="V6HudLogic"/>
    ///   (Issue #671 の VerdictFallOffset を含む共有関数)、フォントサイズと
    ///   レーン x は <see cref="V6HudMetrics"/> と判定エフェクト投影から求める。
    ///   独自の固定 px 定数は使わない。
    ///
    /// 座標系: 本編 HUD の px 定数 (VerdictRestGapAboveLine / LyricSlotOrigin
    /// など) は実スクリーン px。プレビューの RT はカメラの縦 FOV をそのまま
    /// 見せるので、画面 px → well px は well/Screen の比で線形に写る。
    /// 位置は軸別のビューポート比 (<see cref="PreviewScale"/>) で画面
    /// フラグメントへ一致させ、寸法 (箱・文字・padding) は縦横同一の
    /// uniform scale (<see cref="PreviewContentScale"/>) で本編と同じ
    /// アスペクト比を保つ (Issue #706: 軸別スケールだと well が 16:9 より
    /// 縦長な分だけ約 7% の横歪みが出る)。
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
        /// (<see cref="V6HudLogic.LyricSlotOrigin"/> / <see cref="V6HudLogic.LyricSlotSize"/>、
        /// Issue #674 の共有スロット高) を well 座標へ写して置く。
        /// 実際の配置は <see cref="UpdatePreviewOverlay"/> が解像度変化に
        /// 追従して行う。
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
        /// 本編 HUD px → well px の位置換算係数 (軸別)。
        /// well は画面全体 (縦 FOV 全域) を見せ、プレビューカメラの射影は
        /// 本編カメラと同じ fitted aspect (16:9) なので、ワールド→
        /// ビューポート比は本編の画面 px 比と一致する。位置だけは
        /// この軸別写像で画面フラグメントへ厳密に合わせる。
        /// </summary>
        private static Vector2 PreviewScale(Vector2 wellSize)
        {
            float screenW = Mathf.Max(1f, Screen.width);
            float screenH = Mathf.Max(1f, Screen.height);
            return new Vector2(wellSize.x / screenW, wellSize.y / screenH);
        }

        /// <summary>
        /// 本編 HUD px → well px の寸法換算係数 (uniform)。
        /// 縦軸はカメラの縦 FOV が本編と一致するため well/Screen 高比が厳密で、
        /// 横方向にも同じ比を使うことで判定ポップ・歌詞ボックスの文字と枠が
        /// 本編と同じアスペクト比を保つ。TMP のフォントは単一サイズしか
        /// 持てないため、軸別スケール (約 7% の横歪み) は採用しない (Issue #706)。
        /// 残差: well は 16:9 より縦長なので、要素の well 幅に対する割合は
        /// 本編の画面幅割合から screenAspect/wellAspect 倍ずれる (避けられない)。
        /// </summary>
        private static float PreviewContentScale(Vector2 wellSize)
        {
            return wellSize.y / Mathf.Max(1f, Screen.height);
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
            // 「判定線」の画面位置は本編 V6HudVerdict.UpdateVerdictAnchor と
            // 同じ投影点 (0, 0.76, 0.15) = Judge.EffectPosition 相当を使う。
            // 描画される判定線 quad (y=0.5, z=0) の投影ではない — 本編の
            // レガシー 3D 経路 (V6UiMode.Playfield=false、出荷モード) が
            // この点を判定アンカーとして使っており、プレビューは本編の
            // 見え方を再現するのが契約なので、真の判定線へ「修正」すると
            // 本編と約 1 文字分ずれる (Issue #706 横断監査で確認)。
            Vector3 judgeScreen = cam != null
                ? cam.WorldToViewportPoint(new Vector3(0f, 0.76f, 0.15f))
                : Vector3.zero;
            float screenH = Mathf.Max(1f, Screen.height);
            // 落下距離は本編と同じ判定ボックス高さ (実スクリーン px)。
            float verdictFallDistance =
                V6HudLogic.VerdictFallDistancePx(PreviewHudMetrics());
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

                // 本編 UpdateLaneGrids (レガシー 3D 経路) と同じ点
                // (Judge.EffectPosition 相当 (LaneWorldX, 0.76, 0.15)) を
                // プレビューカメラでビューポートへ写す。上の judgeScreen と
                // 同じアンカー系なので、レーン x と判定 y は本編と一致する。
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
                // 画面下原点 y = 判定線 + VerdictRestGapAboveLine + VerdictFallOffset。
                // Issue #671: 出現時は静止位置の真上 VerdictFallDistancePx から
                // VerdictFallSeconds かけて落下する (画面上端ではない)。
                // 定数は実スクリーン px なので Screen.height でビューポート比へ写す。
                float restFrac = judgeScreen.y + V6HudLogic.VerdictRestGapAboveLine / screenH;
                float fallFrac =
                    V6HudLogic.VerdictFallOffset(elapsed, verdictFallDistance) / screenH;
                float posFrac = restFrac + fallFrac;

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
            // 寸法は uniform scale (縦横同一)。位置は軸別の scale のまま。
            float content = PreviewContentScale(wellSize);

            // 歌詞スロット: 本編 V6HudLyrics.BuildLyric / LyricSlotAnchoredPosition
            // と同じ原点・幅・高さを well 座標へ写す。
            // Issue #706: 高さは歌詞本文だけの LyricSize ではなく、
            // 歌詞・カウントダウン・終了タイトルの共有スロット高
            // (V6HudLogic.LyricSlotHeight、Issue #674) を使う。
            if (previewLyricBox != null)
            {
                var brt = (RectTransform)previewLyricBox.transform;
                Vector2 slotOrigin = V6HudLogic.LyricSlotOrigin(screenW, screenH);
                Vector2 slotSize = V6HudLogic.LyricSlotSize(m.LyricFs, screenW, screenH);
                brt.sizeDelta = new Vector2(slotSize.x * content, slotSize.y * content);
                // 上中央アンカー: x = スロット中心 − well 中央、y = −スロット上端。
                brt.anchoredPosition = new Vector2(
                    (slotOrigin.x + slotSize.x * 0.5f) * scale.x - wellSize.x * 0.5f,
                    -slotOrigin.y * scale.y);
            }

            if (previewLyricText != null)
            {
                var trt = previewLyricText.rectTransform;
                // 本編 VerticalLayoutGroup の padding (LyricPaddingX/Y) を well へ写す。
                trt.offsetMin = new Vector2(V6HudLogic.LyricPaddingX * content,
                    V6HudLogic.LyricPaddingY * content);
                trt.offsetMax = new Vector2(-V6HudLogic.LyricPaddingX * content,
                    -V6HudLogic.LyricPaddingY * content);
                // 本編 ScaledLyricFs = m.LyricFs (LyricFs 基準 fs44 への比)。
                previewLyricText.fontSize = m.LyricFs * content;
            }

            // 判定ポップ: 本編 V6HudVerdict と同じ m.VerdictW/H/Fs を well へ写す。
            float verdictW = m.VerdictW * content;
            float verdictH = m.VerdictH * content;
            float verdictFs = m.VerdictFs * content;
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
