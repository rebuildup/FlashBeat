using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using FlashBeat.Gameplay;
using FlashBeat.UI.V5;
using FlashBeat.UI.V6;
using FlashBeat.UI.V6.Game.Hud;
using FlashBeat.UI.V6.Option;

namespace FlashBeat.UI.V6.Common
{
    /// <summary>
    /// ゲームプレビューの共有ホスト (Issue #733)。Option / Pause の設定画面が
    /// 同じプレビュー (本編と同じ 3D レーン・ノーツ・判定・歌詞) を表示するための
    /// 部品。3D 側は <see cref="V6OptionPreviewRig"/> (Issue #579 / #732 の
    /// 疑似時計・自動プレイ・共有 Judge 経路) をそのまま使い、
    /// ここは well への結線と 2D オーバーレイ (歌詞行・判定ポップ) を担う。
    ///
    /// well (464x280) の RawImage (3D シーン) の手前に、ゲーム体験の
    /// 2D 要素を重ねる:
    ///
    /// - 歌詞行: <see cref="V6OptionPreviewLyrics"/> が返す現在行を
    ///   本編と同じスロット位置・寸法で表示する (歌詞 OFF・データ無しでは出さない)。
    /// - 判定ポップ: rig の JudgeQueue が出した判定を、本編
    ///   <see cref="V6HudVerdict"/> と同じ規則で表示する。
    ///   ラベル・色・寿命・位置/透明度のタイムラインは
    ///   <see cref="V6HudLogic.VerdictPoseAt"/>、フォントサイズとレーン x は
    ///   <see cref="V6HudMetrics"/> と判定エフェクト投影から求める。
    ///
    /// 座標系: 本編 HUD の px 定数は実スクリーン px。プレビューの RT は
    /// カメラの縦 FOV をそのまま見せるので、画面 px → well px は well/Screen の
    /// 比で線形に写る。位置は軸別のビューポート比 (<see cref="PreviewScale"/>)、
    /// 寸法は縦横同一の uniform scale (<see cref="PreviewContentScale"/>) で
    /// 本編と同じアスペクト比を保つ (Issue #706)。
    ///
    /// オーバーレイは <see cref="Bind"/> で渡した well の子として作るため、
    /// well が再構築されても rig (3D シーン) は生き残る。
    /// GManager / Judge / MusicManager の実行時状態には触れない。
    /// </summary>
    public sealed class V6GamePreview
    {
        private V6OptionPreviewRig rig;

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
        /// well (Game Preview の Panel) へプレビューを結び付ける。
        /// RawImage + オーバーレイを well の子として作る。well が作り直される
        /// たびに呼ぶ (rig は初回だけ生成し、ビューの寿命まで保持する)。
        /// </summary>
        public void Bind(GameObject well)
        {
            if (well == null)
            {
                return;
            }

            if (rig == null)
            {
                rig = new V6OptionPreviewRig();
            }

            var rawGo = new GameObject("Game Preview 3D", typeof(RectTransform), typeof(RawImage));
            rawGo.transform.SetParent(well.transform, false);
            var rawRt = rawGo.GetComponent<RectTransform>();
            rawRt.anchorMin = Vector2.zero;
            rawRt.anchorMax = Vector2.one;
            rawRt.offsetMin = Vector2.zero;
            rawRt.offsetMax = Vector2.zero;
            var raw = rawGo.GetComponent<RawImage>();
            raw.texture = rig.Texture;
            raw.raycastTarget = false;

            BuildPreviewOverlay(well);
        }

        /// <summary>
        /// プレビューを 1 フレーム進める (疑似時計 + オーバーレイ更新)。
        /// 画面の Tick から毎フレーム呼ぶ。
        /// </summary>
        public void Tick()
        {
            if (rig == null)
            {
                return;
            }

            rig.Tick();
            UpdatePreviewOverlay();
        }

        /// <summary>プレビューだけを 1 ステップ進める (検証ハーネス用)。</summary>
        public void TickSimulated(float mediaTime, float deltaTime)
        {
            if (rig == null)
            {
                return;
            }

            rig.TickSimulated(mediaTime, deltaTime);
            UpdatePreviewOverlay();
        }

        /// <summary>画面を離れている間はプレビューカメラの RT 描画を止める。</summary>
        public void SetRenderingEnabled(bool enabled)
        {
            if (rig != null)
            {
                rig.SetRenderingEnabled(enabled);
            }
        }

        /// <summary>rig とリソースを破棄する (ビューの Dispose から呼ぶ)。</summary>
        public void Dispose()
        {
            if (rig != null)
            {
                rig.Dispose();
                rig = null;
            }
        }

        // --- オーバーレイ構築 ---

        /// <summary>well の RawImage の後ろにオーバーレイを構築する。</summary>
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
        /// (<see cref="V6HudLogic.LyricSlotOrigin"/> / <see cref="V6HudLogic.LyricSlotSize"/>)
        /// を well 座標へ写して置く。
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

        /// <summary>well 中央基準の TMP テキスト (判定ポップ用)。</summary>
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
        /// ビューポート比は本編の画面 px 比と一致する。
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
        /// 本編と同じアスペクト比を保つ (Issue #706)。
        /// </summary>
        private static float PreviewContentScale(Vector2 wellSize)
        {
            return wellSize.y / Mathf.Max(1f, Screen.height);
        }

        /// <summary>本編 HUD と同じプロファイル解決。</summary>
        private static V6HudMetrics PreviewHudMetrics()
        {
            return V6HudMetrics.ForResolution(Screen.width, Screen.height);
        }

        /// <summary>
        /// オーバーレイの毎フレーム更新。<see cref="Tick"/> から
        /// rig.Tick() の直後に呼ぶ。rig が持つ状態 (歌詞行・判定ポップ) を
        /// 読んで表示へ写すだけで、ここから GManager / Judge / MusicManager へは
        /// 書き込まない。
        /// </summary>
        private void UpdatePreviewOverlay()
        {
            if (rig == null || previewOverlayRt == null)
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
            string lyric = rig.CurrentLyric;
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
            // VerdictPoseAt のオフセット、fs = m.VerdictFs) で well へ配置する。
            Camera cam = rig.PreviewCamera;
            // 「判定線」の画面位置は本編 V6HudVerdict.UpdateVerdictAnchor と
            // 同じ投影点 <see cref="V6HudLogic.LegacyJudgeLineAnchor"/>。
            // アンカーは本編ワールド座標系の値。rig は (0,0,-RigDistance) へ
            // 平行移動されているので、必ず rig ローカル→ワールド変換を通す。
            Vector3 judgeScreen = cam != null
                ? cam.WorldToViewportPoint(rig.RigLocalToWorld(V6HudLogic.LegacyJudgeLineAnchor()))
                : Vector3.zero;
            float screenH = Mathf.Max(1f, Screen.height);
            V6HudMetrics hudMetrics = PreviewHudMetrics();
            for (int lane = 0; lane < Judge.LaneCount; lane++)
            {
                var tmp = previewVerdictTexts[lane];
                if (tmp == null)
                {
                    continue;
                }

                if (cam == null || judgeScreen.z <= 0f
                    || !rig.TryGetVerdict(lane, out string label, out float startTime))
                {
                    V6LayoutWrites.ActiveIfChanged(tmp.gameObject, false);
                    continue;
                }

                // 本編 UpdateLaneGrids (レガシー 3D 経路) と同じ点
                // (Judge.EffectPosition 相当 (LaneWorldX, 0.76, 0.15)) を
                // プレビューカメラでビューポートへ写す。
                Vector3 world = rig.RigLocalToWorld(
                    new Vector3(Judge.LaneWorldX(lane), 0.76f, 0.15f));
                Vector3 vp = cam.WorldToViewportPoint(world);
                if (vp.z <= 0f)
                {
                    V6LayoutWrites.ActiveIfChanged(tmp.gameObject, false);
                    continue;
                }

                // 本編 V6HudVerdict と同じ純粋タイムライン
                // (V6HudLogic.VerdictPoseAt) を使う。
                float elapsed = Time.unscaledTime - startTime;
                V6HudLogic.VerdictPose pose =
                    V6HudLogic.VerdictPoseAt(elapsed, hudMetrics);
                float posFrac = judgeScreen.y + pose.OffsetPx / screenH;
                float alpha = pose.Alpha;

                var rt = tmp.rectTransform;
                V6LayoutWrites.AnchoredPositionIfChanged(rt,
                    (vp.x - 0.5f) * wellSize.x, (posFrac - 0.5f) * wellSize.y);

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
            float content = PreviewContentScale(wellSize);

            if (previewLyricBox != null)
            {
                var brt = (RectTransform)previewLyricBox.transform;
                Vector2 slotOrigin = V6HudLogic.LyricSlotOrigin(screenW, screenH);
                Vector2 slotSize = V6HudLogic.LyricSlotSize(m.LyricFs, screenW, screenH);
                brt.sizeDelta = new Vector2(slotSize.x * content, slotSize.y * content);
                brt.anchoredPosition = new Vector2(
                    (slotOrigin.x + slotSize.x * 0.5f) * scale.x - wellSize.x * 0.5f,
                    -slotOrigin.y * scale.y);
            }

            if (previewLyricText != null)
            {
                var trt = previewLyricText.rectTransform;
                trt.offsetMin = new Vector2(V6HudLogic.LyricPaddingX * content,
                    V6HudLogic.LyricPaddingY * content);
                trt.offsetMax = new Vector2(-V6HudLogic.LyricPaddingX * content,
                    -V6HudLogic.LyricPaddingY * content);
                previewLyricText.fontSize = m.LyricFs * content;
            }

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

        /// <summary>レーンフラッシュの現在 α (0..LaneFlashPeakAlpha)。</summary>
        public float LaneFlashAlphaForAudit(int lane)
        {
            return rig == null ? 0f : rig.LaneFlashAlphaForAudit(lane);
        }

        /// <summary>拍フラッシュ (BGFlash 相当) の現在 α。</summary>
        public float BeatFlashAlphaForAudit
        {
            get { return rig == null ? 0f : rig.BeatFlashAlphaForAudit; }
        }

        /// <summary>レーンに表示中の判定ラベル (PERFECT 等)。無ければ null。</summary>
        public string VerdictForAudit(int lane)
        {
            if (rig == null)
            {
                return null;
            }

            return rig.TryGetVerdict(lane, out string label, out _) ? label : null;
        }

        /// <summary>いま歌詞スロットに出ている本文 (無ければ null)。</summary>
        public string LyricForAudit
        {
            get { return rig == null ? null : rig.CurrentLyric; }
        }

        /// <summary>これまでに出した判定ポップの累計数。</summary>
        public int JudgementCountForAudit
        {
            get { return rig == null ? 0 : rig.JudgementCount; }
        }

        /// <summary>歌詞ボックスの RectTransform (配置検証用)。無ければ null。</summary>
        public RectTransform LyricBoxRectForAudit
        {
            get
            {
                return previewLyricBox == null ? null
                    : previewLyricBox.GetComponent<RectTransform>();
            }
        }

        /// <summary>歌詞テキストの TMP (フォントサイズ検証用)。無ければ null。</summary>
        public TextMeshProUGUI LyricTextForAudit
        {
            get { return previewLyricText; }
        }

        /// <summary>判定ポップの RectTransform (配置検証用)。無ければ null。</summary>
        public RectTransform VerdictRectForAudit(int lane)
        {
            return lane >= 0 && lane < previewVerdictTexts.Length && previewVerdictTexts[lane] != null
                ? previewVerdictTexts[lane].rectTransform
                : null;
        }

        /// <summary>判定ポップの TMP (フォントサイズ検証用)。無ければ null。</summary>
        public TextMeshProUGUI VerdictTextForAudit(int lane)
        {
            return lane >= 0 && lane < previewVerdictTexts.Length ? previewVerdictTexts[lane] : null;
        }

        /// <summary>
        /// プレビューのオートプレイ (Issue #732 のデモ用自動入力) の有効フラグ。
        /// 既定 true。実キー注入で判定を制御する検証ハーネスは false にする。
        /// </summary>
        public bool AutoPlayForAudit
        {
            get { return rig != null && rig.AutoPlayEnabled; }
            set { if (rig != null) rig.AutoPlayEnabled = value; }
        }

        /// <summary>単調増加の疑似プレビュー時刻 (時計リベース検証用)。</summary>
        public float TimeForAudit
        {
            get { return rig == null ? 0f : rig.PreviewTimeForAudit; }
        }

        /// <summary>判定種別ごとの累計ポップ数 (オートプレイ網羅性の検証用)。</summary>
        public int VerdictCountForAudit(LongNote.Judgement judgement)
        {
            return rig == null ? 0 : rig.VerdictCountForAudit(judgement);
        }
    }
}
