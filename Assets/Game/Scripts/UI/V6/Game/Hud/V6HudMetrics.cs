using UnityEngine;
using FlashBeat.UI.V5;
using FlashBeat.UI.V6.Game.Playfield;

namespace FlashBeat.UI.V6.Game.Hud
{
    /// <summary>
    /// v6 Game HUD の寸法テーブル。契約 §8
    /// (docs/design/v6/flashbeat-v6-contract.md「ゲーム HUD — 3 解像度」) の design 実測値を
    /// <b>design px = 実ピクセル</b> で保持する (2026-11-08 Owner 指示: HUD は解像度ごとに
    /// 固有フレームを持ち、等比拡大 / 引き伸ばしは一切しない。1:1 で中央配置)。
    ///
    /// - FHD  <c>Uc9v6</c> 1920×1080
    /// - 2K   <c>E51yo</c> 2560×1440
    /// - Mac  <c>QrSwA</c> 2560×1664 (2K と同寸法、縦位置だけずれる)
    ///
    /// 実行時は <see cref="ForResolution"/> で実スクリーン解像度に最も近い既知プロファイルを
    /// <b>そのまま</b>選ぶ (縮尺は変えない)。未知解像度で縮小すると design の数値が無効になる。
    ///
    /// かつては 1920×1080 論理テンプレートを min(w/1920, h/1080) で等比拡大していた
    /// (<see cref="ScaleForResolution"/> / <see cref="LogicalViewportSize"/>)。
    /// この方式は Owner 指示 (固定解像度・引き伸ばしなし) に反するため廃止。
    /// 描画経路からは使わず、EditMode テスト (<c>V6GameHudLogicTests</c>) が参照するため
    /// API だけ残してある (レガシー。新規コードは使わないこと)。
    /// </summary>
    public sealed class V6HudMetrics
    {
        public const int TemplateWidth = 1920;
        public const int TemplateHeight = 1080;

        /// <summary>この寸法セットに適用済みの倍率 (1:1 固定配置なので常に 1)。</summary>
        public readonly float Scale;

        /// <summary>プロファイル名 ("fhd" / "2k" / "mac")。解像度変更の検知に使う。</summary>
        public readonly string Name;

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

        // --- コンボ (左) ---
        /// <summary>コンボ block の左上 x (design px、画面左上原点)。</summary>
        public readonly int ComboX;
        /// <summary>コンボ block の左上 y (design px、画面左上原点)。</summary>
        public readonly int ComboY;
        public readonly int ComboW;
        public readonly int ComboH;
        public readonly int ComboLabelFs;
        public readonly int ComboValueFs;
        public readonly int ComboValueY;

        // --- 右中央コラム (コンボステータス / コンボボーナス) ---
        /// <summary>右コラムの左上 x (design px)。右端アンカー時は Margin と等価。</summary>
        public readonly int RightColX;
        /// <summary>右コラムの左上 y (design px)。契約 §8: FHD 460 / 2K 613 / Mac 725。</summary>
        public readonly int RightColY;
        public readonly int RightColW;
        public readonly int BonusFs;
        public readonly int StatusFs;
        public readonly int BonusRowH;
        /// <summary>コンボボーナス block の高さ。</summary>
        public readonly int BonusBlockH;
        /// <summary>コンボステータス block の高さ。</summary>
        public readonly int StatusBlockH;
        /// <summary>右コラム上端の y オフセット (画面中央基準, 負で上)。レガシー (中央アンカー方式)。</summary>
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
        /// <summary>キーガイド キャップのラベル fs (契約 §8: FHD 24 / 2K・Mac 32)。</summary>
        public readonly int KeyCapLabelFs;
        public readonly int KeyGuideH;
        public readonly int KeyGuideBottom;
        /// <summary>キーガイド 8 列のうち第 0 列の中心 x (design px、契約 §8 の KeyCap 左端 + 幅/2)。</summary>
        public readonly float KeyFirstCenter;
        /// <summary>キーガイド 8 列の列間隔 (design px)。</summary>
        public readonly float KeySpacing;

        // --- 歌詞 (v6 拡張: design の HUD フレームには歌詞ノードが無い。契約 §10 の枠
        // 1120×220 を Top Shade 内央に置く。キーガイド行 (y963/1283/1507) とは重ならない) ---
        public readonly int LyricY;
        public readonly int LyricFs;

        private V6HudMetrics(
            string name,
            int topShadeH, int progressH, int margin,
            int gradeFs, int gaugeX, int gaugeY, int gaugeW, int gaugeH,
            int scoreLabelY, int scoreValueY, int scoreLabelFs, int scoreFs,
            int menuW, int menuH, int menuFs,
            int comboX, int comboY, int comboW, int comboH, int comboLabelFs, int comboValueFs, int comboValueY,
            int rightColX, int rightColY, int rightColW, int bonusFs, int statusFs, int bonusRowH, int bonusBlockH, int statusBlockH,
            int rightColTopOffset,
            int statusTrackW, int statusTrackH, int statusTrackY, int statusRowH, int statusRowY,
            int fcChipW, int fcChipH,
            int verdictFs, int verdictW, int verdictH, int verdictBottom,
            int keyCapW, int keyCapH, int keyCapLabelFs, int keyGuideH, int keyGuideBottom,
            float keyFirstCenter, float keySpacing,
            int lyricY, int lyricFs,
            int gaugeSegHOverride = -1, int gaugeFillHOverride = -1)
        {
            Name = name;
            Scale = 1f;
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
            ComboX = comboX;
            ComboY = comboY;
            ComboW = comboW;
            ComboH = comboH;
            ComboLabelFs = comboLabelFs;
            ComboValueFs = comboValueFs;
            ComboValueY = comboValueY;
            RightColX = rightColX;
            RightColY = rightColY;
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

            // ゲージ内部。FHD は design 値そのもの (比率 = 1)。2K / Mac は契約 §8 の
            // 実測値 (Seg h14 / Fill h14、track 800×24) をそのまま使い、FHD 比の拡大値
            // (14 × 4/3 = 18.7 → 19) に合わせない (design ファイル側が正)。
            float g = (float)GaugeW / 600f;
            float gh = (float)GaugeH / 36f;
            GaugeTrackH = Mathf.RoundToInt(18f * gh);
            GaugeSegY = Mathf.RoundToInt(2f * gh);
            GaugeSegH = gaugeSegHOverride >= 0 ? gaugeSegHOverride : R(14, gh);
            GaugeFillH = gaugeFillHOverride >= 0 ? gaugeFillHOverride : R(14, gh);
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

            // design template: Fill は 524px (FHD 600 のトラックに対して "+ セグメントの
            // 開始 530 までで 6px 止める)。"+ セグメント幅" から引くと 530 になり
            // 6px ずれるので、design の実測値 524 を基準にする。
            GaugeFillMax = Mathf.RoundToInt(524f * g);
            GaugeBestW = Mathf.RoundToInt(4f * g);
            GaugeBestH = GaugeTrackH;
        }

        /// <summary>契約 §8 の FHD (<c>Uc9v6</c> 1920×1080) design 実測テーブル。</summary>
        public static V6HudMetrics Fhd(float scale)
        {
            // scale 引数はレガシー (等比拡大方式) 名残。1:1 固定配置では使わない。
            float s = scale;
            return new V6HudMetrics(
                "fhd",
                R(260, s), R(8, s), R(48, s),
                R(95, s), R(188, s), R(48, s), R(600, s), R(36, s),
                R(78, s), R(100, s), R(22, s), R(38, s),
                R(180, s), R(48, s), R(22, s),
                R(48, s), R(430, s), R(600, s), R(220, s), R(22, s), R(180, s), R(24, s),
                R(1512, s), R(460, s), R(360, s), R(22, s), R(22, s), R(44, s), R(90, s), R(160, s),
                R(80, s),
                R(280, s), R(8, s), R(52, s), R(44, s), R(96, s),
                R(150, s), R(40, s),
                R(30, s), R(140, s), R(44, s), R(73, s),
                R(56, s), R(36, s), R(24, s), R(36, s), R(33, s),
                338f * s, 177.42857f * s,
                R(170, s), R(36, s));
        }

        /// <summary>
        /// 契約 §8 の 2K (<c>E51yo</c> 2560×1440) design 実測テーブル (design px = 実ピクセル)。
        /// 注意: デザイナーの丸めで FHD×4/3 と一致しない項目がある
        /// (Grade fs 95×4/3=126.7 に対し design 実測は 125、ゲージ Seg/Fill h は 14 のまま)。
        /// </summary>
        public static V6HudMetrics TwoK()
        {
            return new V6HudMetrics(
                "2k",
                347, 11, 64,
                125, 251, 64, 800, 48,
                104, 133, 30, 50,
                240, 64, 30,
                64, 573, 800, 293, 30, 240, 32,
                2016, 613, 480, 30, 30, 59, 120, 213,
                80,
                373, 11, 69, 59, 128,
                200, 53,
                40, 187, 59, 98,
                75, 48, 32, 48, 45,
                566.5f, 203.57143f,
                227, 48,
                14, 14);
        }

        /// <summary>
        /// 契約 §8 の Mac (<c>QrSwA</c> 2560×1664) design 実測テーブル。
        /// 2K と同寸法で、縦位置だけ design 値がずれる
        /// (Combo y685 / Combo Status・Bonus y725 / 判定行 y1507 / KeyGuide y1571 x464..)。
        /// </summary>
        public static V6HudMetrics Mac()
        {
            return new V6HudMetrics(
                "mac",
                347, 11, 64,
                125, 251, 64, 800, 48,
                104, 133, 30, 50,
                240, 64, 30,
                64, 685, 800, 293, 30, 240, 32,
                2016, 725, 480, 30, 30, 59, 120, 213,
                80,
                373, 11, 69, 59, 128,
                200, 53,
                40, 187, 59, 98,
                75, 48, 32, 48, 45,
                501.5f, 222.14285f,
                227, 48,
                14, 14);
        }

        /// <summary>
        /// 実スクリーン解像度 (Canvas px。Constant Pixel Size x1 の ScreenSpaceOverlay では
        /// Screen.width/height と等しい) に対応する HUD 寸法テーブル。
        ///
        /// 契約 §8: HUD は 3 解像度の固有フレーム (1920×1080 / 2560×1440 / 2560×1664)。
        /// 既知解像度は design 値を <b>実ピクセルのまま</b>返す (等比拡大・縮小はしない)。
        /// 未知解像度は最も近い既知プロファイル (中心間距離最小) を <b>そのまま</b>使う
        /// (Owner 指示 2026-11-08: 縮尺を変えない)。
        /// </summary>
        public static V6HudMetrics ForResolution(float width, float height)
        {
            if (width <= 0f || height <= 0f)
            {
                return Fhd(1f);
            }

            // 毎フレーム呼ばれる (解像度変更の検知) ので、プロファイルはキャッシュする。
            int best = 0;
            float bestD = float.MaxValue;
            for (int i = 0; i < ProfileResolutions.Length; i++)
            {
                float dx = width - ProfileResolutions[i].x;
                float dy = height - ProfileResolutions[i].y;
                float d = dx * dx + dy * dy;
                if (d < bestD)
                {
                    bestD = d;
                    best = i;
                }
            }

            return ProfileCache()[best];
        }

        /// <summary>契約 §8 の 3 プロファイル解像度 (FHD / 2K / Mac)。</summary>
        private static readonly Vector2[] ProfileResolutions =
        {
            new Vector2(1920f, 1080f), new Vector2(2560f, 1440f), new Vector2(2560f, 1664f),
        };

        private static V6HudMetrics[] profileCache;

        private static V6HudMetrics[] ProfileCache()
        {
            return profileCache ?? (profileCache = new[] { Fhd(1f), TwoK(), Mac() });
        }

        /// <summary><see cref="ForResolution"/> が選ぶプロファイル名 (解像度変更の検知用)。</summary>
        public static string ProfileNameFor(float width, float height)
        {
            return ForResolution(width, height).Name;
        }

        /// <summary>このテーブルが (width, height) と同じプロファイルか。</summary>
        public bool MatchesResolution(float width, float height)
        {
            return Name == ProfileNameFor(width, height);
        }

        /// <summary>
        /// レガシー: 1920×1080 論理テンプレートを画面内に収める等比倍率。
        /// Owner 指示 (固定解像度・引き伸ばしなし) により廃止済み。
        /// 描画経路からは使わず、EditMode テストが参照するため残す。
        /// 新規コードは <see cref="ForResolution"/> を使うこと。
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
        /// レガシー (等比拡大方式): 等比スケール後に画面全体を覆う論理ビューポート寸法。
        /// <see cref="ScaleForResolution"/> とセットで、EditMode テストが参照する。
        /// 描画経路からは使わない。
        /// </summary>
        public static Vector2 LogicalViewportSize(float width, float height)
        {
            float scale = ScaleForResolution(width, height);
            return new Vector2(width / scale, height / scale);
        }

        /// <summary>キーガイド / 判定行の第 lane 列の中心 x (design px、契約 §8 の KeyGuide 格子)。</summary>
        public float KeyColumnCenter(int lane)
        {
            return KeyFirstCenter + lane * KeySpacing;
        }

        /// <summary>
        /// 契約 §6 のレーン中心線 (判定線 y=792 上の下端 x = 339 + 150·lane) を
        /// 実画面 x (左原点 px) へ写した値。<see cref="V6LaneGeometry.DesignToScreen"/>
        /// (高さ合わせ + 水平中央寄せ) と同じ写像なので、キーガイド / 判定行は
        /// v6 プレイフィールドのレーン中心と <b>必ず一致</b>する (Owner 指示 2026-11-08:
        /// キーガイドをレーン中心に一致させる。契約 §8 の KeyGuide 格子は design サンプル値)。
        /// </summary>
        public static float LaneCenterScreenX(int lane, float screenW, float screenH)
        {
            V6LaneGeometry.TryLanePoint(lane, 0f, out Vector2 design, out _);
            return V6LaneGeometry.DesignToScreen(design, new Vector2(screenW, screenH)).x;
        }

        /// <summary>
        /// 契約 §8 の 8 列格子 (KeyGuide と判定行は同じ格子) を design px で返す。
        /// 第 0 列の中心 x と列間隔: FHD (338, 177.43) / 2K (566.5, 203.57) / Mac (501.5, 222.14)。
        /// 契約 §8 ⚠: この格子は KeyGuide の design 実測値であり、レーン中心線
        /// (150 px 間隔 × スケール) とは一致しない。実装はレーン中心
        /// (<see cref="LaneCenterScreenX"/>) に載せる (Owner 指示 2026-11-08)。
        /// 契約に無い解像度は FHD 格子をそのまま使う (縮尺を変えない)。
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

            firstCenter = 338f;
            spacing = 177.42857f;
        }

        /// <summary>
        /// レガシー (等比拡大方式): 契約 §8 の第 lane 列中心を HUD テンプレート座標へ写した値。
        /// 等比拡大廃止により新規コードは使わない (EditMode テストが参照するため残す)。
        /// </summary>
        public static float KeyGuideTemplateX(float width, float height, int lane)
        {
            LaneGridDesign(width, height, out float firstCenter, out float spacing);
            return (firstCenter + lane * spacing) / ScaleForResolution(width, height);
        }

        /// <summary>
        /// レガシー (中央アンカー方式): 中央アンカー + 上オフセット (テンプレート px) で置いた
        /// 要素の上端の design px y。EditMode テストが参照するため残す。
        /// 新規コードは各プロファイルの ComboY / RightColY / KeyGuideBottom を直接使うこと。
        /// </summary>
        public static float MiddleAnchoredTopDesignY(float width, float height, float templateTopOffset)
        {
            float s = ScaleForResolution(width, height);
            return (height / s * 0.5f - templateTopOffset) * s;
        }

        /// <summary>
        /// レガシー (下端アンカー方式): 下端アンカー + マージンで置いた帯 (テンプレート高さ h) の
        /// 上端の design px y。EditMode テストが参照するため残す。
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
