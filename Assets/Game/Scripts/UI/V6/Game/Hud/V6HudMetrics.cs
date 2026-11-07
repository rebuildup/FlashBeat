using UnityEngine;
using FlashBeat.UI.V5;

namespace FlashBeat.UI.V6.Game.Hud
{
    /// <summary>
    /// v6 Game HUD の寸法表 (design/flashbeat-ui.pen の Game · HUD フレーム実測値)。
    ///
    /// 一次ソースは Pencil ノード (feature/pencil-ui @ 19f08b3):
    /// - Uc9v6 "Game · HUD · FHD 1920×1080"
    /// - E51yo "Game · HUD · 2K 2560×1440"
    /// - QrSwA "Game · HUD · MacBook Air M5 2560×1664"
    ///
    /// 2K/Mac は FHD の 4/3 倍だが、デザイナーの丸めで一致しない値 (グレード fs 95→125 等)
    /// があるため、抽出できた値は実測値をそのまま持つ。fhd 縮小プロファイル
    /// (UISizeProfile の "fhd*s") は FHD 値を s 倍する。
    /// Mac (2560×1664) は 2K と同じ寸法で、縦方向の位置は中央/下端アンカーで自動的に
    /// 追従する (デザイン上も中央ブロックは中央固定・下端ブロックは下端固定)。
    /// </summary>
    public sealed class V6HudMetrics
    {
        /// <summary>プロファイル倍率 (fhd=1, 2k/mac=4/3, fhd*s=s)。視認用の余白に使う。</summary>
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

            // ゲージ内部は FHD 実測値を GaugeW/GaugeH 比で伸縮する (2K 実測と一致することを確認済み)。
            float g = (float)GaugeW / 600f;
            float gh = (float)GaugeH / 36f;
            GaugeTrackH = Mathf.RoundToInt(18f * gh);
            GaugeSegY = Mathf.RoundToInt(2f * gh);
            GaugeSegH = 14; // 2K 実測でも 14 (デザイナー未拡大)
            GaugeFillH = 14; // 同上
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

            GaugeFillMax = GaugeW - (int)GaugeSegW[5];
            GaugeBestW = Mathf.RoundToInt(4f * g);
            GaugeBestH = GaugeTrackH;
        }

        /// <summary>design 実測: FHD 1920×1080。</summary>
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
                R(56, s), R(36, s), R(22, s), R(36, s), R(33, s),
                338f * s, 177.42857f * s,
                R(170, s), R(36, s));
        }

        /// <summary>design 実測: 2K 2560×1440 (Mac 2560×1664 も同じ寸法)。</summary>
        public static V6HudMetrics TwoK()
        {
            return new V6HudMetrics(
                4f / 3f,
                347, 11, 64,
                125, 251, 64, 800, 48,
                104, 133, 30, 50,
                240, 64, 30,
                800, 293, 30, 240, 32,
                480, 30, 30, 59, 120, 131, 213,
                107,
                373, 11, 69, 59, 128,
                200, 53,
                40, 187, 59, 98,
                75, 48, 30, 48, 45,
                566.5f, 203.57143f,
                227, 48);
        }

        /// <summary>
        /// 現在の解像度用の寸法表。UISizeProfile (v5 と共有, 編集不可) の選択結果を
        /// v6 の寸法表へ写す。縮小プロファイル ("fhd*s") は FHD 値の s 倍。
        /// </summary>
        public static V6HudMetrics Current
        {
            get
            {
                var profile = UISizeProfile.Current;
                if (profile.Name == "2k" || profile.Name == "mac")
                {
                    return TwoK();
                }

                return Fhd(profile.Scale);
            }
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
