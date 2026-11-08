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
