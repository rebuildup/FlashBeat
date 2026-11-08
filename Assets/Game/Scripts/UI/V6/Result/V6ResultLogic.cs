using System.Globalization;
using UnityEngine;

namespace FlashBeat.UI.V6.Result
{
    /// <summary>
    /// v6 リザルト画面 (Issue #217) の純ロジック。Unity API への依存は Mathf のみ
    /// (EditMode テストから直接検証できる)。グレード算出は既存の Assessment 判定
    /// (<see cref="FlashBeat.UI.V5.ResultView.RankFor"/>) と同一の閾値を使い、
    /// v5 リザルトの表示と 1:1 に保つ。寸法は design v6 の 1728x972 基準。
    /// </summary>
    public static class V6ResultLogic
    {
        /// <summary>design v6 共通フレームの基準サイズ。</summary>
        public const float DesignW = 1728f;
        public const float DesignH = 972f;

        /// <summary>スコア満点 (V5 SelectViewLogic.ScoreMax と同じ基準)。</summary>
        public const int ScoreMax = FlashBeat.UI.V5.SelectViewLogic.ScoreMax;

        /// <summary>スコアゲージの全長 (design px, zcSHh の Score Gauge)。</summary>
        public const float GaugeW = 824f;

        /// <summary>
        /// design フレーム (1728×972) を実画面へ <b>1:1 (等倍)</b> で中央配置するときの
        /// 左上 origin = ((w−1728)/2, (h−972)/2) (整数切り捨て)。
        /// 正本: design s88VZk "Resolution Check · Result" の実配置
        /// (FHD 1920×1080 → (96,54) / 2K 2560×1440 → (416,234) / Mac 2560×1664 → (416,346))。
        /// 旧実装の min(w/1728, h/972) による引き伸ばし (等比スケール) は廃止
        /// (Owner 指示: design フレームは固定解像度であり、拡大縮小は起こらない。
        /// 解像度ごとに origin だけが変わる)。異常値 (0 以下) は (0,0) にフォールバックする。
        /// </summary>
        public static Vector2 FrameOrigin(int screenW, int screenH)
        {
            if (screenW <= 0 || screenH <= 0)
            {
                return Vector2.zero;
            }

            return new Vector2(
                Mathf.Floor((screenW - DesignW) / 2f),
                Mathf.Floor((screenH - DesignH) / 2f));
        }

        /// <summary>
        /// スコアからグレード文字列を返す。既存の Assessment 判定
        /// (V5 ResultView.RankFor) と同じ閾値 (1:1 維持)。スコアが無い (0 以下) ときは "–"。
        /// </summary>
        public static string GradeOf(int score)
        {
            if (score <= 0)
            {
                return "–";
            }

            return FlashBeat.UI.V5.ResultView.RankFor(score);
        }

        /// <summary>スコア表示。3桁カンマ区切り (デザイン表記 988,513)。</summary>
        public static string FormatScore(int value)
        {
            return value.ToString("N0", CultureInfo.InvariantCulture);
        }

        /// <summary>ハイスコア差分。更新時は "+66,633"、それ以外は空文字 (V5 と同じ)。</summary>
        public static string DeltaLabel(int prevBest, int score)
        {
            if (score > prevBest)
            {
                return "+" + FormatScore(score - prevBest);
            }

            return string.Empty;
        }

        /// <summary>
        /// スコアゲージの Fill 幅 (design px)。スコアは満点 ScoreMax に対する比率で
        /// 全長 GaugeW に写像される (design 実測: 988,513 → 815px)。
        /// </summary>
        public static float GaugeFillWidth(int score)
        {
            return GaugeRatio(score) * GaugeW;
        }

        /// <summary>
        /// スコアゲージの Best マーカー位置 (design px, 左端基準)。
        /// design 実測: 前回最高 921,880 → x=760。
        /// </summary>
        public static float GaugeBestX(int bestScore)
        {
            return GaugeRatio(bestScore) * GaugeW;
        }

        /// <summary>曲長表示 (秒 → "m:ss")。無効値は "–"。</summary>
        public static string DurationText(float seconds)
        {
            string text = FlashBeat.UI.V5.SelectViewLogic.DurationText(seconds);
            return string.IsNullOrEmpty(text) ? "–" : text;
        }

        private static float GaugeRatio(int score)
        {
            if (score <= 0)
            {
                return 0f;
            }

            return Mathf.Clamp01((float)score / ScoreMax);
        }

        // --- メイン / フッターの layout 幾何 (契約 §4)。
        //     V6ResultView の構築と EditMode 配置テスト (V6ResultFramePlacementTests) で
        //     共有する純定数・純関数。 ---

        /// メインゾーン: 共有フレームの Main (24,96) 1680x780 をそのまま使う (契約 §4)。
        public const float MainX = 24f;
        public const float MainY = 96f;
        public const float MainW = 1680f;
        public const float MainH = 780f;

        /// フッターゾーン (24,876) 1680x96 (契約 §4)。
        public const float FooterW = 1680f;
        public const float FooterH = 96f;
        public const float FooterButtonY = 20f;
        public const float FooterButtonH = 56f;
        public const float FooterButtonGap = 16f;
        public const float FooterLegendY = 30f;
        public const float FooterLegendGap = 16f;

        /// 右カラム (ランキング 2 枚, 幅 528) の Main 内左端 x (Main 右端に右寄せ)。
        public const float RankX = MainX + MainW - 528f;

        /// 楽曲情報バンド (Main 内 (0,636)) 840x120 と子要素の配置 (design zcSHh)。
        /// Stat を y56 (design の y20 ではなく) に置くのは、実データの曲名
        /// (最長 12 文字 x fs34 で y21..55 まで食い込む) と Stat のラベル行が
        /// 衝突するため (design の例は 5 文字想定)。
        public const float SongInfoY = 636f;
        public const float SongInfoW = 840f;
        public const float SongInfoH = 120f;
        public const float SongThumbX = 16f, SongThumbY = 28f, SongThumbW = 112f, SongThumbH = 63f;
        public const float SongTitleX = 144f, SongTitleY = 16f, SongTitleW = 660f, SongTitleH = 40f;
        public const float SongArtistX = 144f, SongArtistY = 66f, SongArtistW = 190f, SongArtistH = 28f;
        public const float SongStatY = 56f, SongStatW = 116f, SongStatH = 64f;
        public const float SongStatLabelH = 20f, SongStatValueY = 20f, SongStatValueH = 42f;
        public static readonly float[] SongStatX = { 348f, 468f, 588f, 708f };

        /// ランキング Head (パネル内 (8,8)) 512x36 と矢印 / 集計軸の配置
        /// (design: padding [0,12] / gap8 / Arrow 32x32 / Metric fs18)。
        /// Metric は「累計スコア」(5 文字 x 18 = 90) が収まる幅 92 を取る
        /// (旧実装の幅 68 は折り返して矢印と重なった)。
        public const float RankHeadX = 8f, RankHeadY = 8f, RankHeadW = 512f, RankHeadH = 36f;
        public const float RankHeadTitleW = 220f;
        public const float RankHeadChevronL = 329f, RankHeadChevronR = 468f, RankHeadChevronSize = 32f;
        public const float RankHeadMetricX = 369f, RankHeadMetricW = 92f;

        /// フッター左のキー凡例 (契約 §4: KeyCap (fill,36) fs22 + Label fs22 $text-muted)。
        public static readonly string[] LegendKeys = { "R", "Enter", "Space" };
        public static readonly string[] LegendLabels = { "リトライ", "登録", "決定" };

        /// KeyCap 幅 (契約 §4: Key fs22 / padding [0,10]。短キーは 56 の下限)。
        public static float KeyCapWidth(string key)
        {
            return Mathf.Max(56f, Mathf.Ceil(V6Ui.LabelAdvanceEm(key) * 22f) + V6Ui.KeyCapPaddingX * 2f);
        }

        /// 凡例 1 項目の幅 (KeyCap + gap8 + Label fs22)。
        public static float LegendItemWidth(string key, string label)
        {
            return KeyCapWidth(key) + 8f + Mathf.Ceil(Common.V6Layout.EstimateTextWidth(label, 22f));
        }

        /// フッター左のキー凡例 3 項目の合計幅 (項目間 gap16 込み)。
        public static float FooterLegendWidth()
        {
            float end = 0f;
            for (int i = 0; i < LegendKeys.Length; i++)
            {
                end += LegendItemWidth(LegendKeys[i], LegendLabels[i]) + FooterLegendGap;
            }

            return end - FooterLegendGap;
        }

        /// <summary>
        /// フッター右ボタンの幅 (契約 §4 Button: padding [0,24] / icon 32 + gap12 /
        /// Label fs30 / gap12 / KeyCap)。Label の字形幅は上側推定なので保険 +8 を足す。
        /// </summary>
        public static float FooterButtonWidth(string label, string keyCap, bool hasIcon)
        {
            return 24f * 2f
                + (hasIcon ? 32f + 12f : 0f)
                + Mathf.Ceil(V6Ui.LabelAdvanceEm(label) * 30f)
                + 12f
                + KeyCapWidth(keyCap)
                + 8f;
        }

        /// 幅をフッター右端から gap16 で右寄せ横並びしたときの各ボタン左端 x (design, フッター内)。
        public static float[] FooterButtonXs(params float[] widths)
        {
            int n = widths == null ? 0 : widths.Length;
            var xs = new float[n];
            float right = FooterW;
            for (int i = 0; i < n; i++)
            {
                xs[i] = right - widths[i];
                right = xs[i] - FooterButtonGap;
            }

            return xs;
        }

        // --- 登録モーダル (Issue #240) の純ロジック ---

        /// <summary>送信結果の区分 (loading / success / duplicate / offline / server error)。</summary>
        public enum SubmitOutcome
        {
            /// <summary>送信中 (RankingClient.PostScore の応答待ち)。</summary>
            Pending,
            /// <summary>サーバが順位を返した (201)。</summary>
            Success,
            /// <summary>同じ内容を送信済み (429, 10 秒窓)。</summary>
            Duplicate,
            /// <summary>輸送失敗 (オフライン / タイムアウト)。</summary>
            Offline,
            /// <summary>その他の失敗 (422 / 5xx / 応答解析失敗)。</summary>
            Rejected,
        }

        /// <summary>名前入力の検証結果。</summary>
        public enum NicknameError
        {
            None,
            /// <summary>空 (トリム後 0 文字)。</summary>
            Empty,
            /// <summary>長すぎ (RankingClient.NicknameMaxLength 超過)。</summary>
            TooLong,
            /// <summary>使えない文字 (制御文字)。</summary>
            InvalidChar,
        }

        /// <summary>
        /// 登録名を検証する (トリム後、RankingClient.NicknameMaxLength 以下)。
        /// 制御文字は Input.inputString 経由でも混入しうるため明示的に拒む。
        /// </summary>
        public static NicknameError ValidateNicknameInput(string raw, int maxLength)
        {
            if (raw == null)
            {
                return NicknameError.Empty;
            }

            string name = raw.Trim();
            if (name.Length == 0)
            {
                return NicknameError.Empty;
            }

            if (name.Length > maxLength)
            {
                return NicknameError.TooLong;
            }

            for (int i = 0; i < name.Length; i++)
            {
                if (char.IsControl(name[i]))
                {
                    return NicknameError.InvalidChar;
                }
            }

            return NicknameError.None;
        }

        /// <summary>
        /// POST /api/v1/scores の結果を区分する (openapi.v1.yaml: 201 / 429 / 422)。
        /// 輸送失敗は RankingClient.UnavailableError か httpCode==0 で判別する。
        /// </summary>
        public static SubmitOutcome ClassifySubmitResult(bool ok, int httpCode, string error)
        {
            if (ok)
            {
                return SubmitOutcome.Success;
            }

            if (httpCode == 429)
            {
                return SubmitOutcome.Duplicate;
            }

            if (httpCode == 0 || error == RankingClient.UnavailableError)
            {
                return SubmitOutcome.Offline;
            }

            return SubmitOutcome.Rejected;
        }

        /// <summary>区分に対応するモーダルの状態メッセージ。失敗時は再送 [Enter] を案内する。</summary>
        public static string SubmitStatusMessage(SubmitOutcome outcome, int httpCode)
        {
            switch (outcome)
            {
                case SubmitOutcome.Pending:
                    return "送信中…";
                case SubmitOutcome.Success:
                    return "登録しました";
                case SubmitOutcome.Duplicate:
                    return "同じ内容を送信済みです  再送 [Enter] / スキップ [Esc]";
                case SubmitOutcome.Offline:
                    return "オフラインのようです  再送 [Enter] / スキップ [Esc]";
                default:
                    return httpCode > 0
                        ? "送信できませんでした (HTTP " + httpCode + ")  再送 [Enter] / スキップ [Esc]"
                        : "送信できませんでした  再送 [Enter] / スキップ [Esc]";
            }
        }
    }
}
