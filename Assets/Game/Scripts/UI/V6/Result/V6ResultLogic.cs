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

        /// メインゾーン: 正本 zcSHh は **共有フレームの Main を 5 列に差し替える**
        /// ((164,96) 1400×780 = 280×5)。共有フレーム §4 の 6 列 1680 をそのまま
        /// 使うと、右のランキング 2 枚が 156px 外へ出てデザインと一致しない。
        public const float MainX = 164f;
        public const float MainY = 96f;
        public const float MainW = 1400f;
        public const float MainH = 780f;

        /// フッターゾーン (24,876) 1680x96 (契約 §4)。
        public const float FooterW = 1680f;
        public const float FooterH = 96f;
        public const float FooterButtonY = 20f;
        public const float FooterButtonH = 56f;
        public const float FooterButtonGap = 16f;
        public const float FooterLegendY = 30f;

        /// <summary>フッター凡例の項目間 gap (design zcSHh Zone · キー操作 gap24)。</summary>
        public const float FooterLegendGap = 24f;

        /// <summary>
        /// フッター右ボタンの design 幅 (zcSHh の各 Op フレームに固定値が入っている)。
        /// 内容から逆算すると Latin の送り幅の推定誤差で広がるため、正本の固定値をそのまま使う。
        /// </summary>
        public const float FooterRetryButtonW = 220f;
        public const float FooterRegisterButtonW = 273f;
        public const float FooterOkButtonW = 273f;

        /// <summary>
        /// 右カラム (ランキング 2 枚, 幅 528) の Main 内左端 x。
        /// 正本 zcSHh の "曲別ランキング" / "登録名ランキング" は Main 内 (856,24) /
        /// (856,452) で、右端は 856 + 528 = 1384 (フレーム x 1548 = Main 右端 1564 の 16px 内側)。
        /// Main 幅から 528 を引いただけ (872) では 16px 右へずれるため、その内側余白 16 も引く。
        /// </summary>
        public const float RankX = MainW - 528f - 16f;

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

        /// KeyCap 幅 (design zcSHh の Legend KeyCap: Key fs22 / padding [0,8] / 下限なし)。
        /// 実測 (R = 30px) から 56 の下限は正本に無いと分かる。
        public static float KeyCapWidth(string key)
        {
            return Mathf.Max(1f, Mathf.Ceil(V6Ui.LabelAdvanceEm(key) * 22f) + Common.V6Controls.ButtonKeyCapPaddingX * 2f);
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

        // --- 登録モーダルの幾何 (design 05b lqHaN) ---
        //
        // 座標は design px の **左上原点** (V6ResultView の At ヘルパ /
        // V6Layout.FitsIn と同じ方式)。scrim ("Modal 05b") は共通フレーム
        // (1728x972) 全面、Dialog はその中に 840x510 で中央置き。
        // 数値をここに一本化して、配置テスト (V6ResultFramePlacementTests) が
        // 「実レイアウトの値」を検査できるようにする (コピーの検査にしない)。

        /// <summary>登録モーダル Dialog の幅 (design px)。</summary>
        public const float ModalDialogW = 840f;

        /// <summary>登録モーダル Dialog の高さ (design px)。</summary>
        public const float ModalDialogH = 510f;

        /// <summary>登録モーダル Dialog の左端 (design px, 左上原点)。</summary>
        public const float ModalDialogX = 444f;

        /// <summary>登録モーダル Dialog の上端 (design px, 左上原点)。</summary>
        public const float ModalDialogY = 231f;

        /// <summary>
        /// 指定解像度に置いた登録モーダル Dialog の左上 (画面座標, 左上原点)。
        /// 共通フレームは 1:1 固定解像度で置かれるので、フレーム原点
        /// (<see cref="FrameOrigin"/>) に Dialog の design 座標を足すだけ。
        /// </summary>
        public static Vector2 ModalDialogTopLeft(int screenW, int screenH)
        {
            Vector2 origin = FrameOrigin(screenW, screenH);
            return new Vector2(origin.x + ModalDialogX, origin.y + ModalDialogY);
        }

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

        // --- 登録の方式 (Issue #240) ---
        //
        // v6 Result には 2 つの別々の契約がある:
        //   - オンライン順位への送信 (RankingClient)。対象はリモート曲だけで、
        //     応答は 201 / 429 / オフライン / その他の HTTP に区分される。
        //   - ローカル保存 (端末内の名前)。v5 は
        //     <see cref="FlashBeat.UI.V5.ResultView"/> が PlayerPrefs キー
        //     "V5PlayerName" に名前を書いていた。v6 Result は v5 の ResultView を
        //     使わないため、この経路が到達不能になっていた (Issue #240)。
        //
        // 送信対象でない (ローカル) 曲にオンラインの失敗表示 (「オフライン」
        // 「送信失敗」) を出さないため、方式の判定と表示の語彙をここに純ロジック
        // として切り出す。PlayerPrefs への書き込みは
        // <see cref="V6LocalNameStore"/> が担当する。

        /// <summary>ローカル名の PlayerPrefs キー (v5 と同じ)。</summary>
        public const string LocalPlayerNamePrefsKey = "V5PlayerName";

        /// <summary>ローカル名の最大文字数 (v5 と同じ)。</summary>
        public const int LocalNameMaxLength = 8;

        /// <summary>ローカル名の未入力時の既定値 (v5 と同じ)。</summary>
        public const string LocalNameDefault = "YOU";

        /// <summary>
        /// オンライン順位へ送信する対象か。リモート曲で、songId が妥当で、譜面版が
        /// 1 以上のときだけ送信する (ローカル曲・譜面未取得は送信しない)。
        /// </summary>
        public static bool IsSubmittable(bool isRemote, string remoteId, int chartVersion)
        {
            if (!isRemote)
            {
                return false;
            }

            if (string.IsNullOrEmpty(remoteId) || chartVersion <= 0)
            {
                return false;
            }

            return RankingClient.IsValidSongId(remoteId);
        }

        /// <summary>曲情報から登録の方式を返す。</summary>
        public static V6RegistrationMode RegistrationMode(bool isRemote, string remoteId, int chartVersion)
        {
            return IsSubmittable(isRemote, remoteId, chartVersion)
                ? V6RegistrationMode.Online
                : V6RegistrationMode.Local;
        }

        /// <summary>
        /// ローカル名の正規化。v5 の
        /// <see cref="FlashBeat.UI.V5.ResultView.NormalizeName"/> と同じ規則
        /// (大文字化 + A-Z0-9 のみ + 最大 8 文字)。空入力は "YOU"。
        /// オンラインの入力規則 (RankingClient) とは別物なので混ぜない。
        /// </summary>
        public static string NormalizeLocalName(string raw)
        {
            string normalized = FlashBeat.UI.V5.ResultView.NormalizeName(raw);
            return string.IsNullOrEmpty(normalized) ? LocalNameDefault : normalized;
        }

        /// <summary>
        /// ローカル保存の結果メッセージ。オンライン語彙 (「オフライン」「送信」) を含まない。
        /// </summary>
        public static string LocalSaveMessage(string savedName)
        {
            return "この端末に「" + (savedName ?? LocalNameDefault) + "」を保存しました";
        }

        /// <summary>
        /// モーダルに表示する名前の初期値。ローカルは端末内の保存名、
        /// オンラインはオンライン側の保存名 (無ければ端末内の保存名) を使う。
        /// </summary>
        public static string InitialName(
            V6RegistrationMode mode, string onlineNickname, string localName)
        {
            if (mode == V6RegistrationMode.Online && !string.IsNullOrEmpty(onlineNickname))
            {
                return onlineNickname;
            }

            return localName ?? string.Empty;
        }

        /// <summary>
        /// 送信に使う譜面版。プレイ開始時に固定した (pinned) 版を優先する。
        /// 固定版が無いときだけ操作中のカタログの版を使う。
        /// </summary>
        public static int ResolveChartVersion(int pinnedChartVersion, int liveChartVersion)
        {
            return pinnedChartVersion > 0 ? pinnedChartVersion : liveChartVersion;
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
