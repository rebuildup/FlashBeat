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
        /// <summary>コラム内でコンボステータス block を積む位置 (ボーナス block の下 + 隙間)。</summary>
        public readonly int StatusTop;
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
            int rightColW, int bonusFs, int statusFs, int bonusRowH, int bonusBlockH, int statusTop, int statusBlockH,
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
            StatusTop = statusTop;
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
                R(360, s), R(22, s), R(22, s), R(44, s), R(90, s), R(98, s), R(160, s),
                R(80, s),
                R(280, s), R(8, s), R(52, s), R(44, s), R(96, s),
                R(150, s), R(40, s),
                R(30, s), R(140, s), R(44, s), R(73, s),
                R(56, s), R(36, s), R(24, s), R(36, s), R(33, s),
                338f * s, 177.42857f * s,
                R(170, s), R(36, s));
        }

        /// <summary>解像度比較用の 4/3 倍スナップショット。ランタイムでは使用しない。</summary>
        public static V6HudMetrics TwoK()
        {
            return Fhd(4f / 3f);
        }

        /// <summary>
        /// 解像度プロファイルに関係なく同じ論理寸法を返す。
        /// 実解像度への倍率は親フレームに一度だけ適用する。
        /// </summary>
        public static V6HudMetrics ForProfile(UISizeProfile profile)
        {
            return Fhd(1f);
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

        private static int R(int fhdPx, float scale)
        {
            return Mathf.RoundToInt(fhdPx * scale);
        }
    }
}
