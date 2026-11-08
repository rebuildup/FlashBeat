using UnityEngine;
using FlashBeat.UI.V5;

namespace FlashBeat.UI.V6.Game.Hud
{
    /// <summary>
    /// v6 Game HUD の 1920×1080 論理テンプレート寸法。
    /// すべての解像度で同じ論理値を使い、V6GameRuntime がテンプレート全体を
    /// min(width / 1920, height / 1080) で等比拡大する。表示フレームは画面全体を覆う
    /// 論理サイズに広げ、余分な軸を HUD 内の top/center/bottom anchor へ反映する。
    /// </summary>
    public sealed class V6HudMetrics
    {
        public const int TemplateWidth = 1920;
        public const int TemplateHeight = 1080;

        /// <summary>この寸法セットに適用済みの倍率 (ランタイムでは常に 1)。</summary>
        public readonly float Scale;

        // --- 画面帯 ---
        /// <summary>上部の暗がり (Top Shade) の高さ。</summary>
        public readonly int TopShadeH;
        /// <summary>画面最上部の進捗バーの高さ。</summary>
        public readonly int ProgressH;
        /// <summary>画面端の共通マージン。</summary>
        public readonly int Margin;

        // --- ランク / スコア (左上) ---
        public readonly int GradeFs;
        public readonly int GaugeX;
        public readonly int GaugeY;
        public readonly int GaugeW;
        public readonly int GaugeH;
        public readonly int ScoreLabelY;
        public readonly int ScoreValueY;
        public readonly int ScoreLabelFs;
        public readonly int ScoreFs;

        // --- スコアゲージ内部 (Component/スコアゲージ cNEBa) ---
        public readonly int GaugeTrackH;
        public readonly int GaugeSegY;
        public readonly int GaugeSegH;
        public readonly int GaugeFillH;
        public readonly int GaugeLabelY;
        public readonly int GaugeLabelFs;
        /// <summary>C/B/A/S/SS/+ セグメントの x (左端基準)。</summary>
        public readonly float[] GaugeSegX;
        /// <summary>C/B/A/S/SS/+ セグメントの幅。</summary>
        public readonly float[] GaugeSegW;
        /// <summary>塗り (Fill) の最大幅 = GaugeW - + セグメント幅。</summary>
        public readonly int GaugeFillMax;
        public readonly int GaugeBestW;
        public readonly int GaugeBestH;

        // --- メニューボタン (右上) ---
        public readonly int MenuW;
        public readonly int MenuH;
        public readonly int MenuFs;
        public readonly int MenuKeyFs;

        // --- コンボ (左中央) ---
        public readonly int ComboW;
        public readonly int ComboH;
        public readonly int ComboLabelFs;
        public readonly int ComboValueFs;
        public readonly int ComboValueY;

        // --- 右中央コラム (コンボステータス / コンボボーナス) ---
        public readonly int RightColW;
        public readonly int BonusFs;
        public readonly int StatusFs;
        public readonly int BonusRowH;
        /// <summary>コンボボーナス block の高さ。</summary>
        public readonly int BonusBlockH;
        /// <summary>コンボステータス block の高さ。</summary>
        public readonly int StatusBlockH;
        /// <summary>右コラム上端の y オフセット (画面中央基準, 負で上)。</summary>
        public readonly int RightColTopOffset;
        public readonly int StatusTrackW;
        public readonly int StatusTrackH;
        public readonly int StatusTrackY;
        public readonly int StatusRowH;
        public readonly int StatusRowY;
        public readonly int FcChipW;
        public readonly int FcChipH;

        // --- 判定行 (画面最下部) / キーガイド ---
        public readonly int VerdictFs;
        public readonly int VerdictW;
        public readonly int VerdictH;
        public readonly int VerdictBottom;
        public readonly int KeyCapW;
        public readonly int KeyCapH;
        /// <summary>キーガイド キャップのラベル fs (契約 json: FHD 24 / 2K 32)。</summary>
        public readonly int KeyCapLabelFs;
        public readonly int KeyGuideH;
        public readonly int KeyGuideBottom;
        /// <summary>キーガイド 8 列のうち第 0 列の中心 x。</summary>
        public readonly float KeyFirstCenter;
        /// <summary>キーガイド 8 列の列間隔。</summary>
        public readonly float KeySpacing;

        // --- 歌詞 (v6 拡張: デザインの HUD フレームには歌詞ノードが無い。
        // Top Shade 内央に配置し、Issue #220 の要件「再生時刻と一致した行だけを出す」を満たす) ---
        public readonly int LyricY;
        public readonly int LyricFs;

        private V6HudMetrics(
            float scale,
            int topShadeH, int progressH, int margin,
            int gradeFs, int gaugeX, int gaugeY, int gaugeW, int gaugeH,
            int scoreLabelY, int scoreValueY, int scoreLabelFs, int scoreFs,
            int menuW, int menuH, int menuFs,
            int comboW, int comboH, int comboLabelFs, int comboValueFs, int comboValueY,
            int rightColW, int bonusFs, int statusFs, int bonusRowH, int bonusBlockH, int statusBlockH,
            int rightColTopOffset,
            int statusTrackW, int statusTrackH, int statusTrackY, int statusRowH, int statusRowY,
            int fcChipW, int fcChipH,
            int verdictFs, int verdictW, int verdictH, int verdictBottom,
            int keyCapW, int keyCapH, int keyCapLabelFs, int keyGuideH, int keyGuideBottom,
            float keyFirstCenter, float keySpacing,
            int lyricY, int lyricFs)
        {
            Scale = scale;
            TopShadeH = topShadeH;
            ProgressH = progressH;
            Margin = margin;
            GradeFs = gradeFs;
            GaugeX = gaugeX;
            GaugeY = gaugeY;
            GaugeW = gaugeW;
            GaugeH = gaugeH;
            ScoreLabelY = scoreLabelY;
            ScoreValueY = scoreValueY;
            ScoreLabelFs = scoreLabelFs;
            ScoreFs = scoreFs;
            MenuW = menuW;
            MenuH = menuH;
            MenuFs = menuFs;
            ComboW = comboW;
            ComboH = comboH;
            ComboLabelFs = comboLabelFs;
            ComboValueFs = comboValueFs;
            ComboValueY = comboValueY;
            RightColW = rightColW;
            BonusFs = bonusFs;
            StatusFs = statusFs;
            BonusRowH = bonusRowH;
            BonusBlockH = bonusBlockH;
            StatusBlockH = statusBlockH;
            RightColTopOffset = rightColTopOffset;
            StatusTrackW = statusTrackW;
            StatusTrackH = statusTrackH;
            StatusTrackY = statusTrackY;
            StatusRowH = statusRowH;
            StatusRowY = statusRowY;
            FcChipW = fcChipW;
            FcChipH = fcChipH;
            VerdictFs = verdictFs;
            VerdictW = verdictW;
            VerdictH = verdictH;
            VerdictBottom = verdictBottom;
            KeyCapW = keyCapW;
            KeyCapH = keyCapH;
            KeyCapLabelFs = keyCapLabelFs;
            KeyGuideH = keyGuideH;
            KeyGuideBottom = keyGuideBottom;
            KeyFirstCenter = keyFirstCenter;
            KeySpacing = keySpacing;
            LyricY = lyricY;
            LyricFs = lyricFs;

            // ゲージ内部もテンプレート寸法を GaugeW/GaugeH 比で伸縮する。
            float g = (float)GaugeW / 600f;
            float gh = (float)GaugeH / 36f;
            GaugeTrackH = Mathf.RoundToInt(18f * gh);
            GaugeSegY = Mathf.RoundToInt(2f * gh);
            GaugeSegH = R(14, gh);
            GaugeFillH = R(14, gh);
            GaugeLabelY = Mathf.RoundToInt(18f * gh);
            GaugeLabelFs = Mathf.RoundToInt(16f * g);
            GaugeSegX = new float[6];
            GaugeSegW = new float[6];
            int[] fhdSegX = { 0, 122, 244, 366, 488, 530 };
            int[] fhdSegW = { 120, 120, 120, 120, 40, 70 };
            for (int i = 0; i < 6; i++)
            {
                GaugeSegX[i] = Mathf.RoundToInt(fhdSegX[i] * g);
                GaugeSegW[i] = Mathf.RoundToInt(fhdSegW[i] * g);
            }

            // design template: Fill は 524px (FHD 600 のトラックに対して "+" セグメントの
            // 開始 530 までで 6px 止める)。"+ セグメント幅" から引くと 530 になり
            // 6px ずれるので、design の実測値 524 を基準にする。
            GaugeFillMax = Mathf.RoundToInt(524f * g);
            GaugeBestW = Mathf.RoundToInt(4f * g);
            GaugeBestH = GaugeTrackH;
        }

        /// <summary>唯一の HUD 座標テンプレート: FHD 1920×1080。</summary>
        public static V6HudMetrics Fhd(float scale)
        {
            float s = scale;
            return new V6HudMetrics(
                s,
                R(260, s), R(8, s), R(48, s),
                R(95, s), R(188, s), R(48, s), R(600, s), R(36, s),
                R(78, s), R(100, s), R(22, s), R(38, s),
                R(180, s), R(48, s), R(22, s),
                R(600, s), R(220, s), R(22, s), R(180, s), R(24, s),
                R(360, s), R(22, s), R(22, s), R(44, s), R(90, s), R(160, s),
                R(80, s),
                R(280, s), R(8, s), R(52, s), R(44, s), R(96, s),
                R(150, s), R(40, s),
                R(30, s), R(140, s), R(44, s), R(73, s),
                R(56, s), R(36, s), R(24, s), R(36, s), R(33, s),
                338f * s, 177.42857f * s,
                R(170, s), R(36, s));
        }

        /// <summary>
        /// 契約 §8 の 2K (<c>E51yo</c> 2560×1440) design 実測テーブル (design px)。
        /// ランタイムは FHD テンプレートの等比拡大で描くので直接は使わないが、
        /// 契約値の正本として EditMode テスト (<c>Metrics_TwoK_MatchesDesign</c>) と
        /// <see cref="LaneGridDesign"/> の KeyGuide 格子が参照する。
        /// 注意: デザイナーの丸めで FHD×4/3 と一致しない項目がある
        /// (Grade fs 95×4/3=126.7 に対し design 実測は 125)。
        /// </summary>
        public static V6HudMetrics TwoK()
        {
            return new V6HudMetrics(
                4f / 3f,
                347, 11, 64,
                125, 251, 64, 800, 48,
                104, 133, 30, 50,
                240, 64, 30,
                800, 293, 30, 240, 32,
                480, 30, 30, 59, 120, 213,
                107,
                373, 11, 69, 59, 128,
                200, 53,
                40, 187, 59, 98,
                75, 48, 32, 48, 45,
                566.5f, 203.57143f,
                227, 48);
        }

        /// <summary>現在の画面用 HUD テンプレート寸法 (解像度非依存)。</summary>
        public static V6HudMetrics Current
        {
            get { return Fhd(1f); }
        }

        /// <summary>
        /// 1920×1080 論理テンプレートを画面内に収める等比倍率。
        /// 0 以下のサイズは未初期化画面として 1 倍を返す。
        /// </summary>
        public static float ScaleForResolution(float width, float height)
        {
            if (width <= 0f || height <= 0f)
            {
                return 1f;
            }

            return Mathf.Min(width / TemplateWidth, height / TemplateHeight);
        }

        /// <summary>
        /// 等比スケール後に画面全体を覆う論理ビューポート寸法。
        /// 短辺側はテンプレート寸法、余分な軸はアンカーが使える領域になる。
        /// </summary>
        public static Vector2 LogicalViewportSize(float width, float height)
        {
            float scale = ScaleForResolution(width, height);
            return new Vector2(width / scale, height / scale);
        }

        /// <summary>キーガイド / 判定行の第 lane 列の中心 x。</summary>
        public float KeyColumnCenter(int lane)
        {
            return KeyFirstCenter + lane * KeySpacing;
        }

        /// <summary>
        /// 契約 §8 の 8 列格子 (KeyGuide と判定行は同じ格子) を design px で返す。
        /// 第 0 列の中心 x と列間隔: FHD (338, 177.43) / 2K (566.5, 203.57) /
        /// Mac (501.5, 222.14)。design 実測値であり等比スケールとは一致しない
        /// (契約 §8 ⚠: FHD 177.4 / 2K 203.6 / Mac 222 ≒ レーン 166.7×スケール)。
        /// 契約に無い解像度は FHD 格子の等比拡大 (design px = FHD 値 × scale)。
        /// </summary>
        public static void LaneGridDesign(float width, float height, out float firstCenter, out float spacing)
        {
            string profile = UISizeProfile.Select(
                Mathf.RoundToInt(width), Mathf.RoundToInt(height)).Name;
            if (profile == "mac")
            {
                // 契約 §8 Mac: KeyGuide x = 464..2019 (KeyCap 75 幅 → 中心は +37.5)
                firstCenter = 464f + 37.5f;
                spacing = (2019f + 37.5f - firstCenter) / 7f;
                return;
            }
            if (profile == "2k")
            {
                // 契約 §8 2K: KeyGuide x = 529..1954
                firstCenter = 529f + 37.5f;
                spacing = (1954f + 37.5f - firstCenter) / 7f;
                return;
            }

            float s = ScaleForResolution(width, height);
            firstCenter = 338f * s;
            spacing = 177.42857f * s;
        }

        /// <summary>
        /// 契約 §8 の第 lane 列中心を HUD テンプレート座標 (画面左原点) へ写した値。
        /// design px を等比倍率で割る (描画時に frameScale を掛けて design px に戻る)。
        /// </summary>
        public static float KeyGuideTemplateX(float width, float height, int lane)
        {
            LaneGridDesign(width, height, out float firstCenter, out float spacing);
            return (firstCenter + lane * spacing) / ScaleForResolution(width, height);
        }

        /// <summary>
        /// 中央アンカー + 上オフセット (テンプレート px) で置いた要素の上端の design px y
        /// (画面上端原点)。V6GameRuntime は HUD フレームを論理ビューポート
        /// (= canvas / scale) に広げるので、中央アンカー要素は論理高さの中央へ追従する。
        /// 契約 §8: Combo 上端 FHD 430 / 2K 573 / Mac 685、Combo Status 上端 460 / 613 / 725。
        /// </summary>
        public static float MiddleAnchoredTopDesignY(float width, float height, float templateTopOffset)
        {
            float s = ScaleForResolution(width, height);
            return (height / s * 0.5f - templateTopOffset) * s;
        }

        /// <summary>
        /// 下端アンカー + マージンで置いた帯 (テンプレート高さ h) の上端の design px y。
        /// 契約 §8: KeyGuide 上端 FHD 1011 / 2K 1347 / Mac 1571。
        /// </summary>
        public static float BottomAnchoredTopDesignY(
            float width, float height, float templateBottomMargin, float templateH)
        {
            float s = ScaleForResolution(width, height);
            return (height / s - templateBottomMargin - templateH) * s;
        }

        private static int R(int fhdPx, float scale)
        {
            return Mathf.RoundToInt(fhdPx * scale);
        }
    }
}
