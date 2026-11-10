using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FlashBeat.UI.V5;
using FlashBeat.UI.V6;
using FlashBeat.UI.V6.Common;

namespace FlashBeat.UI.V6.Result
{
    /// <summary>
    /// Result 画面 (#217) の v6 UI。design/flashbeat-ui.pen v6 の
    /// 05 Result · v6 (zcSHh) / 05b 登録モーダル (lqHaN) を実行時に構築する。
    ///
    /// - データは実データのみ: GManager (score / combo / maxCombo / perfect / great /
    ///   bad / miss / ActiveSongs / Hiscore)、ScorePersistence のベスト、
    ///   RankingClient のオンラインランキング。デザイン例の固定値
    ///   (サンプル曲名・"988,513" 等) は決して出さない。
    /// - 構築はコードのみ (シーン YAML / Prefab / .meta は触らない)。
    /// - 寸法は design px (1728x972 基準)。フレームは 1:1 (等倍) のまま
    ///   V6ResultLogic.FrameOrigin の origin ((w−1728)/2, (h−972)/2) 切り捨て) で
    ///   画面中央へ配置する。引き伸ばし (等比スケール) はしない
    ///   (design s88VZk "Resolution Check · Result": FHD (96,54) / 2K (416,234) /
    ///   Mac (416,346))。フレーム外の画面全体には v6 の背景色 (契約 §3 bg =
    ///   #1B1B1B) を敷き、レガシー クリーム背景を隠す
    ///   (Result シーンにはゲームのアニメーション背景が無い)。色は V5Tokens を流用する。
    /// - 遷移 (リトライ / 選曲へ / タイトルへ) はコールバックで統合担当が渡す。
    ///   ResultSceneManager は呼ばない。
    /// - 実数据的更新 (スコアの確定後) は Refresh() で反映する。
    /// </summary>
    public sealed class V6ResultView
    {
        /// <summary>
        /// v6 Result を出すか (統合担当が V6UiMode から切替えるための公開フラグ)。
        /// 既定 true。false のとき ResultSceneManager は従来どおり v5 ResultView を作る。
        /// </summary>
        public static bool Enabled = true;

        // --- design px 定数 (zcSHh / lqHaN 実測) ---
        private const float FrameW = 1728f;
        private const float FrameH = 972f;

        // メインゾーン: 共有フレームの Main (24,96) 1680x780 をそのまま使う (契約 §4)。
        // (旧実装は x=164 w=1400 に狭め、右カラムがフレーム右端から 180px 内側に寄っていた)
        // 定数は V6ResultLogic.MainX..MainH / RankX (テストと共有)。

        // 結果 (列1-3) (0,24) 840x280。
        private const float ResultBlockY = 24f;
        private const float GradeBoxW = 280f, GradeBoxH = 280f;
        private const float ScoreColX = 336f;

        // スコアゲージ (0,336) 840x100 → Gauge (0,16) 824x76。
        private const float GaugeZoneY = 336f;
        private const float GaugeY = 16f;
        private const float GaugeTrackY = 24f, GaugeTrackH = 16f;

        // 判定 (0,448) 840x140。5 セル x=0,168,336,504,672 各 160 幅。
        private const float JudgeY = 448f;
        private static readonly float[] JudgeX = { 0f, 168f, 336f, 504f, 672f };
        private const float JudgeCellW = 160f, JudgeCellH = 140f;

        // 楽曲情報 (0,636) 840x120 の寸法は V6ResultLogic.SongInfo* (テストと共有)。

        // ランキング (列4-5, Main 右端に右寄せ → V6ResultLogic.RankX = 1152)。
        private const float SongRankY = 24f, SongRankH = 412f;
        private const float PlayerRankY = 452f, PlayerRankH = 304f;
        private const int SongRankVisibleRows = 6;
        private const int PlayerRankVisibleRows = 4;
        private const float RankRowW = 496f, RankRowH = 44f, RankRowStep = 52f;

        private readonly Action onRetry;
        private readonly Action onSelect;
        private readonly Action onTitle;

        private GameObject rootGo;
        private RectTransform frame;
        private V6ResultViewDriver driver;

        // 動的に更新する要素 (Refresh で差し替える)。
        private TextMeshProUGUI gradeLetter;
        private TextMeshProUGUI scoreValue;
        private TextMeshProUGUI bestValue;
        private readonly TextMeshProUGUI[] judgeValues = new TextMeshProUGUI[5];
        private Image gaugeFill;
        private Image gaugeBestMarker;
        private TextMeshProUGUI songTitle;
        private TextMeshProUGUI songArtist;
        private readonly TextMeshProUGUI[] songStatValues = new TextMeshProUGUI[4];
        private Image songThumb;
        private TextMeshProUGUI songRankEmpty;
        private RectTransform songRankRows;
        private TextMeshProUGUI playerRankEmpty;

        // 登録モーダル (Issue #240)。名前入力 + 送信 + 結果表示を実装する。
        private GameObject modal;
        private TextMeshProUGUI modalNameValue;
        private TextMeshProUGUI modalStatus;
        private TextMeshProUGUI modalHead;
        private TextMeshProUGUI modalNameHint;
        private TextMeshProUGUI modalRegisterLabel;
        private Button modalRegisterButton;
        private ResultRankingOverlay legacyOverlay;
        private bool legacyOverlayWasEnabled;
        private TextMeshProUGUI modalSongTitle;
        private TextMeshProUGUI modalSongArtist;
        private TextMeshProUGUI modalGrade;
        private TextMeshProUGUI modalRecordLabel;
        private TextMeshProUGUI modalRecordValue;
        private TextMeshProUGUI modalRecordNote;
        private TextMeshProUGUI modalScoreValue;
        private Image modalThumb;
        private bool modalOpen;

        // 登録送信の状態 (loading / success / duplicate / offline / server error)。
        private V6ResultLogic.SubmitOutcome submitOutcome = V6ResultLogic.SubmitOutcome.Pending;

        // 登録の方式 (Issue #240)。モーダルを開いた時点で確定する。
        // Online = オンライン順位へ送信 / Local = 端末内だけに保存。
        private V6RegistrationMode registrationMode = V6RegistrationMode.Local;
        private bool submitInFlight;
        private bool submittedThisResult;
        private readonly System.Text.StringBuilder nameBuffer = new System.Text.StringBuilder();

        // run snapshot (Issue #240): 表示前に GManager.ResetSession() が呼ばれて値が
        // 消えるなら、消える前に捕捉する。初回 Refresh で固定し、以後は確定値を使う。
        private int runScore, runPerfect, runGreat, runBad, runMiss, runMaxCombo;
        private bool runCaptured;

        /// <summary>登録成功時にサーバから返された順位 (0 = 未登録)。</summary>
        private int myRank;

        // 前回最高 (リザルト確定時の値)。統合担当が SetPrevHiscore で渡す
        // (未指定なら差分表示は "–")。
        private int prevHiscore = -1;

        // オンラインランキング (実通信は既存 RankingClient を使う)。
        private readonly List<FlashBeat.UI.V6.Select.V6RankingRow> rankingRows
            = new List<FlashBeat.UI.V6.Select.V6RankingRow>();
        private bool rankingFetchInFlight;
        private bool hasRankingError;
        private string rankingStatusMessage = "";

        private int lastWidth;
        private int lastHeight;

        private V6ResultView(Action onRetry, Action onSelect, Action onTitle)
        {
            this.onRetry = onRetry;
            this.onSelect = onSelect;
            this.onTitle = onTitle;
        }

        /// <summary>View を生成する。統合担当 (ResultSceneManager 側) から呼ぶ。</summary>
        public static V6ResultView Create(RectTransform parent, Action onRetry, Action onSelect, Action onTitle)
        {
            if (parent == null)
            {
                throw new ArgumentNullException(nameof(parent));
            }

            var view = new V6ResultView(onRetry, onSelect, onTitle);
            view.BuildRoot(parent);
            view.BuildHeader();
            view.BuildMain();
            view.BuildFooter();
            view.BuildModal();
            view.Refresh();
            view.StartRankingFetch();
            return view;
        }

        public RectTransform Root
        {
            get { return frame; }
        }

        /// <summary>リザルト確定時点のハイスコア (更新差分の表示に使う)。任意。</summary>
        public void SetPrevHiscore(int prev)
        {
            prevHiscore = prev;
            Refresh();
        }

        /// <summary>実データから表示を更新する (スコア確定後に統合担当が呼ぶ)。</summary>
        public void Refresh()
        {
            if (rootGo == null)
            {
                return;
            }

            if (!runCaptured)
            {
                CaptureRun();
            }

            PlaceFrame();
            RefreshResult();
            RefreshSongInfo();
            RefreshRanking();
            RefreshModal();
        }

        public void SetVisible(bool visible)
        {
            if (rootGo != null)
            {
                rootGo.SetActive(visible);
            }
        }

        /// <summary>破棄 (統合担当がシーンを去るときに呼ぶ)。</summary>
        public void Dispose()
        {
            if (rootGo != null)
            {
                V5GameUi.DestroyNow(rootGo);
                rootGo = null;
            }

            ResumeLegacyOverlay();
        }

        // --- 毎フレーム (V6ResultViewDriver から呼ばれる) ---

        internal void Tick()
        {
            if (rootGo == null || !rootGo.activeInHierarchy)
            {
                return;
            }

            PlaceFrame();

            // オーバーレイ (ランキング登録等) の開いた・閉じたフレームは入力を捨てる
            // (v5 ResultView と同じ規律)。
            if (!FlashBeat.UI.SceneInputGate.InputAllowed)
            {
                return;
            }

            if (modalOpen)
            {
                HandleModalInput();
                return;
            }

            // R (リトライ) / Space・Esc (選曲へ) はメニュー操作設定 (KeyActions) に従う。
            if (KeyActions.IsDown(KeyActions.KeyAction.Retry))
            {
                Fire(onRetry);
                return;
            }

            if (KeyActions.IsDown(KeyActions.KeyAction.Confirm)
                || KeyActions.IsDown(KeyActions.KeyAction.Back))
            {
                Fire(onSelect);
                return;
            }

            // Enter = 登録モーダルを開く (design 凡例: Enter 登録)。登録済みなら再送しない。
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                if (!submittedThisResult)
                {
                    OpenModal();
                }

                return;
            }

            // タイトルへ (design に専用ボタンが無いため T キー。PR に記載)。
            if (Input.GetKeyDown(KeyCode.T))
            {
                Fire(onTitle);
            }
        }

        private static void Fire(Action callback)
        {
            if (callback != null)
            {
                callback();
            }
        }

        // --- 構築 ---

        private void BuildRoot(RectTransform parent)
        {
            rootGo = V5Ui.Node(parent.gameObject, "V6 Result View");
            var rt = rootGo.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = Vector2.zero;

            // 全面背景は敷かない: フレームの外側はゲーム自身のアニメーション背景
            // (bg-anim3, cover) が敷く (V6Frame.cs の規約。Title / Select と同じ)。
            // 不透明パネルで覆うとゲーム背景を潰して退行になる。
            frame = V5Ui.Node(rootGo, "V6 Result Frame 1728x972").GetComponent<RectTransform>();
            frame.sizeDelta = new Vector2(FrameW, FrameH);

            driver = rootGo.AddComponent<V6ResultViewDriver>();
            driver.View = this;
            PlaceFrame();
        }

        /// <summary>
        /// フレームを 1:1 (等倍) で中央配置する (引き伸ばしはしない)。
        /// origin = ((w−1728)/2, (h−972)/2) 切り捨て (V6ResultLogic.FrameOrigin、
        /// design s88VZk "Resolution Check · Result" の実配置)。
        /// </summary>
        private void PlaceFrame()
        {
            int w = Screen.width;
            int h = Screen.height;
            if (w == lastWidth && h == lastHeight)
            {
                return;
            }

            lastWidth = w;
            lastHeight = h;
            if (frame != null)
            {
                Common.V6Layout.Place(frame, V6ResultLogic.FrameOrigin(w, h),
                    new Vector2(FrameW, FrameH));
            }
        }

        // --- ヘッダー (x24 y0 1680x96): 戻る + RESULT ---

        private void BuildHeader()
        {
            var header = At(V5Ui.Panel(frame.gameObject, "Header", new Vector2(1680f, 96f), Clear(), 0f), 24f, 0f);

            var backZone = At(V5Ui.Node(header, "Zone Back"), 0f, 20f);
            var back = Button(backZone, "Back Button", 56f, 56f, 8f, V5Tokens.Key4, onSelect);
            var backCol = V5GameUi.Column(back, "Inner", 2f, TextAnchor.MiddleCenter);
            Stretch(backCol.GetComponent<RectTransform>(), 56f, 56f);
            var backIcon = V6Icons.Create(backCol.gameObject, "chevron-left", 22f, V5Tokens.TextMuted, "Icon");
            V5GameUi.Text(backCol.gameObject, "Key", "Esc", 13f, V5Tokens.TextMuted, TextAlignmentOptions.Center);

            // design zcSHh の Title ノード (72,20) fs38: 文字列は "FlashBeat"。
            var title = V5GameUi.Text(header.gameObject, "Title", "FlashBeat", 38f, V5Tokens.Text,
                TextAlignmentOptions.Left, FontStyles.Bold);
            At(title, 72f, 20f, 400f, 56f, new Vector2(0f, 0.5f));
        }

        // --- メイン (共有フレーム Main: x24 y96 1680x780, 契約 §4) ---

        private void BuildMain()
        {
            var main = At(V5Ui.Panel(frame.gameObject, "Main",
                    new Vector2(V6ResultLogic.MainW, V6ResultLogic.MainH), Clear(), 0f),
                V6ResultLogic.MainX, V6ResultLogic.MainY);
            BuildResultBlock(main);
            BuildGauge(main);
            BuildJudgements(main);
            BuildSongInfo(main);
            BuildSongRanking(main);
            BuildPlayerRanking(main);
        }

        // 結果 (0,24) 840x280: グレード文字 + SCORE + 前回最高。

        private void BuildResultBlock(GameObject main)
        {
            var block = At(V5Ui.Node(main, "Result Block"), 0f, ResultBlockY, 840f, GradeBoxH);

            // 可読性: 文字が乗る Result Block の内側だけ薄いスクリーン (全面は覆わない)。
            Scrim(block);

            var gradeBox = At(V5Ui.Node(block, "Grade"), 0f, 0f, GradeBoxW, GradeBoxH);
            gradeLetter = V5GameUi.Text(gradeBox.gameObject, "Letter", "–", 240f, V5Tokens.Text,
                TextAlignmentOptions.Center, FontStyles.Bold);
            Stretch(gradeLetter.GetComponent<RectTransform>(), GradeBoxW, GradeBoxH);

            var scoreLabel = V5GameUi.Text(block.gameObject, "Score Label", "SCORE", 20f, V5Tokens.TextDim);
            At(scoreLabel, ScoreColX, 48f, 400f, 26f, new Vector2(0f, 0.5f));

            scoreValue = V5GameUi.Text(block.gameObject, "Score Value", "0", 72f, V5Tokens.Text,
                TextAlignmentOptions.Left, FontStyles.Normal);
            At(scoreValue, ScoreColX, 80f, 500f, 80f, new Vector2(0f, 0.5f));

            var bestLabel = V5GameUi.Text(block.gameObject, "Best Label", "前回最高", 20f, V5Tokens.TextDim);
            At(bestLabel, ScoreColX, 206f, 300f, 26f, new Vector2(0f, 0.5f));

            bestValue = V5GameUi.Text(block.gameObject, "Best Value", "–", 32f, V5Tokens.Text);
            At(bestValue, ScoreColX, 232f, 400f, 40f, new Vector2(0f, 0.5f));
        }

        // スコアゲージ (0,336) 840x100 → Gauge (0,16) 824x76。

        private void BuildGauge(GameObject main)
        {
            var zone = At(V5Ui.Node(main, "Score Gauge Zone"), 0f, GaugeZoneY, 840f, 100f);
            var gauge = At(V5Ui.Node(zone, "Score Gauge"), 0f, GaugeY, V6ResultLogic.GaugeW, 76f);

            // セグメント (y24 h16): C(0,165) B(168,164) A(335,165) S(503,164) SS(670,55) +(728,96)。
            float[] segX = { 0f, 168f, 335f, 503f, 670f, 728f };
            float[] segW = { 165f, 164f, 165f, 164f, 55f, 96f };
            string[] segNames = { "C", "B", "A", "S", "SS", "+" };
            for (int i = 0; i < segX.Length; i++)
            {
                Color fill = i == 5 ? FromHex("#3A3220") : V5Tokens.Key0;
                At(V5Ui.Panel(gauge.gameObject, "Seg " + segNames[i], new Vector2(segW[i], GaugeTrackH), fill, 2f),
                    segX[i], GaugeTrackY);
                var label = V5GameUi.Text(gauge.gameObject, "Label " + segNames[i], segNames[i], 16f,
                    V5Tokens.TextDim, TextAlignmentOptions.Center);
                At(label, segX[i], 46f, segW[i], 20f, new Vector2(0f, 0.5f));
            }

            gaugeFill = At(V5Ui.Panel(gauge.gameObject, "Fill",
                new Vector2(0f, GaugeTrackH), FromHex("#EDEDED"), 2f), 0f, GaugeTrackY).GetComponent<Image>();

            gaugeBestMarker = At(V5Ui.Panel(gauge.gameObject, "Best Marker", new Vector2(4f, 24f),
                V5Tokens.Text, 0f), 0f, 20f).GetComponent<Image>();
            var bestLabel = V5GameUi.Text(gauge.gameObject, "Best Label", "BEST", 14f, V5Tokens.TextMuted,
                TextAlignmentOptions.Center);
            At(bestLabel, 734f, 0f, 56f, 18f, new Vector2(0f, 0.5f));
        }

        // 判定 (0,448) 840x140: Perfect / Great / Bad / Miss / Combo。

        private void BuildJudgements(GameObject main)
        {
            var block = At(V5Ui.Node(main, "Judgements"), 0f, JudgeY, 840f, JudgeCellH);

            // 可読性: 判定ラベル / 数値が乗る領域だけ薄いスクリーン (全面は覆わない)。
            Scrim(block);

            string[] labels = { "Perfect", "Great", "Bad", "Miss", "Combo" };
            for (int i = 0; i < labels.Length; i++)
            {
                var cell = At(V5Ui.Node(block, "J " + labels[i]), JudgeX[i], 0f, JudgeCellW, JudgeCellH);
                var col = V5GameUi.Column(cell, "Inner", 14f, TextAnchor.UpperLeft);
                Stretch(col.GetComponent<RectTransform>(), JudgeCellW, JudgeCellH);
                V5GameUi.Text(col.gameObject, "Label", labels[i], 20f, V5Tokens.TextDim);
                judgeValues[i] = V5GameUi.Text(col.gameObject, "Value", "0", 48f, V5Tokens.Text,
                    TextAlignmentOptions.Left, FontStyles.Normal);
            }
        }

        // 楽曲情報 (0,636) 840x120: Thumb + Title/Artist + BPM/NOTES/LEVEL/曲長。
        // 寸法は V6ResultLogic.SongInfo* (テストと共有)。

        private void BuildSongInfo(GameObject main)
        {
            var panel = At(V5Ui.Panel(main.gameObject, "Song Info",
                    new Vector2(V6ResultLogic.SongInfoW, V6ResultLogic.SongInfoH), V5Tokens.Key2, 16f),
                0f, V6ResultLogic.SongInfoY);

            songThumb = At(V5Ui.Panel(panel.gameObject, "Thumb",
                    new Vector2(V6ResultLogic.SongThumbW, V6ResultLogic.SongThumbH), V5Tokens.Key3, 4f),
                V6ResultLogic.SongThumbX, V6ResultLogic.SongThumbY).GetComponent<Image>();

            // 曲名 / 作曲者はサムネイルの右 (x144) に左揃えで縦積み (design zcSHh)。
            // Title は幅を全幅 (660) に広げて折り返しをなくす (最長 12 文字 x 34 = 408 <= 660)。
            songTitle = V5GameUi.Text(panel.gameObject, "Title", "", 34f, V5Tokens.Text,
                TextAlignmentOptions.Left, FontStyles.Normal);
            At(songTitle, V6ResultLogic.SongTitleX, V6ResultLogic.SongTitleY,
                V6ResultLogic.SongTitleW, V6ResultLogic.SongTitleH, new Vector2(0f, 0.5f));

            songArtist = V5GameUi.Text(panel.gameObject, "Artist", "", 23f, V5Tokens.TextMuted);
            At(songArtist, V6ResultLogic.SongArtistX, V6ResultLogic.SongArtistY,
                V6ResultLogic.SongArtistW, V6ResultLogic.SongArtistH, new Vector2(0f, 0.5f));

            // Stat (BPM/NOTES/LEVEL/曲長) は Label + Value を明示矩形で縦積みする
            // (旧実装は Column レイアウトで TMP の既定サイズにレイアウトが追従できず、
            // 値がバンドの下へはみ出していた)。
            string[] statLabels = { "BPM", "NOTES", "LEVEL", "曲長" };
            for (int i = 0; i < statLabels.Length; i++)
            {
                var cell = At(V5Ui.Node(panel.gameObject, "Stat " + statLabels[i]),
                    V6ResultLogic.SongStatX[i], V6ResultLogic.SongStatY,
                    V6ResultLogic.SongStatW, V6ResultLogic.SongStatH);
                var label = V5GameUi.Text(cell.gameObject, "Label", statLabels[i], 18f, V5Tokens.TextDim);
                At(label, 0f, 0f, V6ResultLogic.SongStatW, V6ResultLogic.SongStatLabelH);
                songStatValues[i] = V5GameUi.Text(cell.gameObject, "Value", "–", 30f, V5Tokens.Text,
                    TextAlignmentOptions.Left, FontStyles.Normal);
                At(songStatValues[i], 0f, V6ResultLogic.SongStatValueY,
                    V6ResultLogic.SongStatW, V6ResultLogic.SongStatValueH);
            }
        }

        // 曲別ランキング (856,24) 528x412: Head + 曲名 + 6 行。

        private void BuildSongRanking(GameObject main)
        {
            var panel = At(V5Ui.Panel(main.gameObject, "Song Ranking", new Vector2(528f, SongRankH),
                V5Tokens.Key2, 8f), V6ResultLogic.RankX, SongRankY);

            var head = At(V5Ui.Node(panel.gameObject, "Head"), V6ResultLogic.RankHeadX, V6ResultLogic.RankHeadY,
                V6ResultLogic.RankHeadW, V6ResultLogic.RankHeadH);
            var headTitle = V5GameUi.Text(head.gameObject, "Title", "曲別ランキング", 18f, V5Tokens.TextDim);
            At(headTitle, 12f, 0f, V6ResultLogic.RankHeadTitleW, V6ResultLogic.RankHeadH, new Vector2(0f, 0.5f));

            songRankEmpty = V5GameUi.Text(panel.gameObject, "No Data", "", 24f, V5Tokens.TextDim);
            At(songRankEmpty, 20f, 60f, 496f, 44f, new Vector2(0f, 0.5f));

            songRankRows = V5Ui.Node(panel.gameObject, "Rows").GetComponent<RectTransform>();
            At(songRankRows, 0f, 0f, 528f, SongRankH);
        }

        // 登録名ランキング (856,452) 528x304: Head (集計軸) + 4 行。
        // Head は design どおり 1 行 (見出し | 矢印 | 集計軸 | 矢印、padding [0,12] gap8)。
        // 登録名ごとの集計は既存 API に無いため空状態のみ (未実装は PR に記載)。

        private void BuildPlayerRanking(GameObject main)
        {
            var panel = At(V5Ui.Panel(main.gameObject, "Player Ranking", new Vector2(528f, PlayerRankH),
                V5Tokens.Key2, 8f), V6ResultLogic.RankX, PlayerRankY);

            var head = At(V5Ui.Node(panel.gameObject, "Head"), V6ResultLogic.RankHeadX, V6ResultLogic.RankHeadY,
                V6ResultLogic.RankHeadW, V6ResultLogic.RankHeadH);
            var headTitle = V5GameUi.Text(head.gameObject, "Title", "登録名ごとのランキング", 18f, V5Tokens.TextDim);
            At(headTitle, 12f, 0f, V6ResultLogic.RankHeadTitleW, V6ResultLogic.RankHeadH, new Vector2(0f, 0.5f));
            BuildChevron(head, V6ResultLogic.RankHeadChevronL, "chevron-left");
            var metric = V5GameUi.Text(head.gameObject, "Metric", "累計スコア", 18f, V5Tokens.Text,
                TextAlignmentOptions.Center);
            // 累計スコア (5 文字 x 18 = 90) が幅 92 に収まるよう折り返さない
            // (旧実装は幅 68 で折り返し、2 行目が矢印と重なった)。
            metric.textWrappingMode = TextWrappingModes.NoWrap;
            At(metric, V6ResultLogic.RankHeadMetricX, 0f, V6ResultLogic.RankHeadMetricW, V6ResultLogic.RankHeadH,
                new Vector2(0f, 0.5f));
            BuildChevron(head, V6ResultLogic.RankHeadChevronR, "chevron-right");

            playerRankEmpty = V5GameUi.Text(panel.gameObject, "No Data", "NO DATA", 24f, V5Tokens.TextDim);
            At(playerRankEmpty, 20f, 60f, 496f, 44f, new Vector2(0f, 0.5f));
        }

        private void BuildChevron(GameObject head, float x, string iconName)
        {
            var chevron = At(V5Ui.Panel(head.gameObject, "Arrow", new Vector2(32f, 32f), V5Tokens.Key4, 8f), x, 2f);
            var icon = V6Icons.Create(chevron.gameObject, iconName, 24f, V5Tokens.TextMuted, "Icon");
            if (icon != null)
            {
                Stretch(icon.rectTransform, 32f, 32f);
            }
        }

        // --- フッター (x24 y876 1680x96) ---

        private void BuildFooter()
        {
            var footer = At(V5Ui.Panel(frame.gameObject, "Footer", new Vector2(1680f, 96f), Clear(), 0f), 24f, 876f);

            // キー凡例 (左下, 契約 §4: y30 / 項目間 gap16)。KeyCap と Label の幅を
            // design px で明示して置く (Row レイアウトグループは幅未確定の TMP ラベルの
            // 上に隣の KeyCap を重ねるため使わない)。
            float lx = 0f;
            for (int i = 0; i < V6ResultLogic.LegendKeys.Length; i++)
            {
                lx = PlaceLegendItem(footer, lx, V6ResultLogic.LegendKeys[i], V6ResultLogic.LegendLabels[i]);
            }

            // 右下: Retry (secondary) / Register (secondary) / OK (primary) を
            // フッター右端から gap16 で横並び (重ならない)。幅は内容
            // (icon 32 / Label fs30 / KeyCap) から V6ResultLogic.FooterButtonWidth で算出。
            float wOk = V6ResultLogic.FooterButtonWidth("OK", "Space", true);
            float wRegister = V6ResultLogic.FooterButtonWidth("Register", "Enter", true);
            float wRetry = V6ResultLogic.FooterButtonWidth("Retry", "R", true);
            float[] xs = V6ResultLogic.FooterButtonXs(wOk, wRegister, wRetry);
            BuildFooterButton(footer, xs[0], "Op OK", "OK", "skip-forward", "Space", true, onSelect);
            BuildFooterButton(footer, xs[1], "Op Register", "Register", "plus", "Enter", false, OpenModal);
            BuildFooterButton(footer, xs[2], "Op Retry", "Retry", "skip-back", "R", false, onRetry);
        }

        /// <summary>
        /// 凡例 1 項目 (KeyCap + Label fs22) を design 座標で置き、次の項目の左端 x を返す。
        /// </summary>
        private float PlaceLegendItem(GameObject footer, float x, string key, string label)
        {
            float capW = V6ResultLogic.KeyCapWidth(key);
            var cap = V6Ui.KeyCap(footer, key, new Vector2(capW, 36f));
            At(cap, x, V6ResultLogic.FooterLegendY);

            float labelW = Mathf.Ceil(Common.V6Layout.EstimateTextWidth(label, 22f));
            Common.V6Controls.Text(footer, "Legend Label " + label, label,
                new Vector2(x + capW + 8f, V6ResultLogic.FooterLegendY), new Vector2(labelW, 36f),
                22f, V5Tokens.TextMuted);
            return x + capW + 8f + labelW + V6ResultLogic.FooterLegendGap;
        }

        /// <summary>
        /// フッター右のボタンを design 座標 (左上 origin, y20 h56) で置く。
        /// 構造は契約 §4 の共通ボタン (icon 32 / Label fs30 / KeyCap 高さ 36) で、
        /// 共通部品 V6Controls.ButtonPrimary/Secondary を使う (KeyCap 右寄せ・
        /// Label 中央寄せは部品側で確定し、ボタン外へはみ出さない)。
        /// </summary>
        private void BuildFooterButton(GameObject footer, float x, string name, string label, string iconSprite,
            string keyCap, bool primary, Action onClick)
        {
            Vector2 topLeft = new Vector2(x, V6ResultLogic.FooterButtonY);
            Vector2 size = new Vector2(
                V6ResultLogic.FooterButtonWidth(label, keyCap, !string.IsNullOrEmpty(iconSprite)),
                V6ResultLogic.FooterButtonH);
            GameObject go = primary
                ? Common.V6Controls.ButtonPrimary(footer, label, topLeft, size, iconSprite, keyCap)
                : Common.V6Controls.ButtonSecondary(footer, label, topLeft, size, iconSprite, keyCap);
            go.name = name;

            // V6Controls は Button コンポーネントを足すだけなので、イベントと遷移は
            // ここで配線する (旧ローカルヘルパと同じ規律: transition/navigation なし)。
            var button = go.GetComponent<Button>();
            button.transition = Selectable.Transition.None;
            var nav = button.navigation;
            nav.mode = Navigation.Mode.None;
            button.navigation = nav;
            if (onClick != null)
            {
                button.onClick.AddListener(() => onClick());
            }
        }

        // --- 登録モーダル (05b lqHaN) ---
        // 名前の入力 (文字入力 / Backspace / Enter)、送信 (オンライン曲)、
        // ローカル保存 (ローカル曲)、結果表示 (送信中 / 成功 / 429 / オフライン /
        // その他 HTTP) を実装済み。

        private void BuildModal()
        {
            modal = At(V5Ui.Panel(frame.gameObject, "Modal 05b", new Vector2(FrameW, FrameH),
                FromHex("#00000099"), 0f), 0f, 0f);
            var scrimButton = modal.AddComponent<Button>();
            scrimButton.transition = Selectable.Transition.None;
            var nav = scrimButton.navigation;
            nav.mode = Navigation.Mode.None;
            scrimButton.navigation = nav;
            scrimButton.onClick.AddListener(CloseModal);

            var dialog = At(V5Ui.Panel(modal.gameObject, "Dialog",
                    new Vector2(V6ResultLogic.ModalDialogW, V6ResultLogic.ModalDialogH), V5Tokens.Key1, 16f),
                V6ResultLogic.ModalDialogX, V6ResultLogic.ModalDialogY);

            modalHead = V5GameUi.Text(dialog.gameObject, "Head", "スコアを登録", 30f, V5Tokens.Text,
                TextAlignmentOptions.Left, FontStyles.Bold);
            At(modalHead, 28f, 28f, 400f, 40f, new Vector2(0f, 0.5f));

            var prompt = V5GameUi.Text(dialog.gameObject, "Name Prompt", "プレイヤー名を入力↓", 20f, V5Tokens.TextMuted,
                TextAlignmentOptions.Right);
            At(prompt, 404f, 38f, 380f, 28f, new Vector2(0f, 0.5f));

            var field = At(V5Ui.Panel(dialog.gameObject, "Name Field", new Vector2(784f, 84f), V5Tokens.Key0, 8f),
                28f, 112f);
            modalNameValue = V5GameUi.Text(field.gameObject, "Value", "", 40f, V5Tokens.TextDim,
                TextAlignmentOptions.Left, FontStyles.Bold);
            At(modalNameValue, 24f, 20f, 736f, 48f, new Vector2(0f, 0.5f));

            var hints = V5GameUi.Row(dialog.gameObject, "Input Hints", 24f, TextAnchor.MiddleLeft);
            At(hints.GetComponent<RectTransform>(), 28f, 208f, 784f, 32f);
            var cap = V5Ui.Panel(hints.gameObject, "Hint Cap", new Vector2(104f, 32f), V5Tokens.Key4, 4f);
            var capText = V5GameUi.Text(cap.gameObject, "Key", "A-Z 0-9", 20f, V5Tokens.TextMuted,
                TextAlignmentOptions.Center);
            Stretch(capText.GetComponent<RectTransform>(), 104f, 32f);
            modalNameHint = V5GameUi.Text(hints.gameObject, "Hint Text",
                "最大" + RankingClient.NicknameMaxLength + "文字", 16f, V5Tokens.TextMuted);
            var spacer = V5Ui.Node(hints.gameObject, "Spacer");
            var spacerLe = spacer.AddComponent<LayoutElement>();
            spacerLe.flexibleWidth = 1f;
            BuildHintLegend(hints.gameObject, "← →", "移動");
            BuildHintLegend(hints.gameObject, "Backspace", "削除");

            var summary = At(V5Ui.Panel(dialog.gameObject, "Summary", new Vector2(784f, 140f), V5Tokens.Key2, 8f),
                28f, 264f);
            modalThumb = At(V5Ui.Panel(summary.gameObject, "Thumb", new Vector2(88f, 50f), V5Tokens.Key3, 4f),
                16f, 20f).GetComponent<Image>();
            modalSongTitle = V5GameUi.Text(summary.gameObject, "Title", "", 26f, V5Tokens.Text);
            At(modalSongTitle, 120f, 18f, 260f, 32f, new Vector2(0f, 0.5f));
            modalSongArtist = V5GameUi.Text(summary.gameObject, "Artist", "", 18f, V5Tokens.TextMuted);
            At(modalSongArtist, 120f, 52f, 260f, 24f, new Vector2(0f, 0.5f));
            var gradeChip = At(V5Ui.Panel(summary.gameObject, "Grade", new Vector2(64f, 40f), V5Tokens.Key4, 4f),
                704f, 38f);
            modalGrade = V5GameUi.Text(gradeChip.gameObject, "GradeText", "–", 24f, V5Tokens.Text,
                TextAlignmentOptions.Center, FontStyles.Normal);
            Stretch(modalGrade.GetComponent<RectTransform>(), 64f, 40f);
            At(V5Ui.Panel(summary.gameObject, "Divider", new Vector2(752f, 1f), FromHex("#FFFFFF1F"), 0f), 16f, 94f);
            modalRecordLabel = V5GameUi.Text(summary.gameObject, "Record Label", "", 16f, V5Tokens.TextDim);
            At(modalRecordLabel, 16f, 104f, 300f, 22f, new Vector2(0f, 0.5f));
            modalRecordValue = V5GameUi.Text(summary.gameObject, "Record Value", "", 20f, V5Tokens.Text,
                TextAlignmentOptions.Right);
            At(modalRecordValue, 440f, 102f, 180f, 26f, new Vector2(0f, 0.5f));
            modalRecordNote = V5GameUi.Text(summary.gameObject, "Record Note", "", 16f, V5Tokens.TextDim);
            At(modalRecordNote, 640f, 104f, 140f, 22f, new Vector2(0f, 0.5f));
            var scoreGroup = V5GameUi.Row(summary.gameObject, "Score Group", 14f, TextAnchor.MiddleLeft);
            At(scoreGroup.GetComponent<RectTransform>(), 400f, 26f, 288f, 52f);
            V5GameUi.Text(scoreGroup.gameObject, "Score Label", "SCORE", 16f, V5Tokens.TextDim);
            modalScoreValue = V5GameUi.Text(scoreGroup.gameObject, "Score Value", "0", 34f, V5Tokens.Text,
                TextAlignmentOptions.Left, FontStyles.Bold);

            var buttons = V5GameUi.Row(dialog.gameObject, "Buttons", 16f, TextAnchor.MiddleLeft);
            At(buttons.GetComponent<RectTransform>(), 28f, 432f, 784f, 50f);
            var skip = Button(buttons.gameObject, "Button Skip", 198f, 50f, 8f, V5Tokens.Key4, CloseModal);
            var skipRow = V5GameUi.Row(skip, "Inner", 12f, TextAnchor.MiddleCenter);
            Stretch(skipRow.GetComponent<RectTransform>(), 198f, 50f);
            // design Dialog の Button · Skip: Icon 24x24 (icon=x)。
            V6Icons.Create(skipRow.gameObject, "x", 24f, V5Tokens.TextMuted, "Icon");
            V5GameUi.Text(skipRow.gameObject, "Label", "Skip", 26f, V5Tokens.Text, TextAlignmentOptions.Center);
            BuildModalKeyCap(skipRow.gameObject, "Esc");
            var register = Button(buttons.gameObject, "Button Register", 252f, 50f, 8f, V5Tokens.ButtonPrimaryBg,
                TrySubmitFromButton);
            var registerRow = V5GameUi.Row(register, "Inner", 12f, TextAnchor.MiddleCenter);
            Stretch(registerRow.GetComponent<RectTransform>(), 252f, 50f);
            // design Dialog の Button · Register: Icon 24x24 (icon=check)。
            V6Icons.Create(registerRow.gameObject, "check", 24f, V5Tokens.ButtonPrimaryLabel, "Icon");
            modalRegisterLabel = V5GameUi.Text(registerRow.gameObject, "Label", "Register", 26f,
                V5Tokens.ButtonPrimaryLabel, TextAlignmentOptions.Center, FontStyles.Normal);
            BuildModalKeyCap(registerRow.gameObject, "Enter");
            modalRegisterButton = register.GetComponent<Button>();

            // 状態メッセージ (loading / success / duplicate / offline / server error)。
            // Head と Name Field の間 (y72) に置く (field は y112 から)。
            modalStatus = V5GameUi.Text(dialog.gameObject, "Status", "", 17f, V5Tokens.TextMuted,
                TextAlignmentOptions.Left);
            At(modalStatus, 28f, 72f, 784f, 26f, new Vector2(0f, 0.5f));

            modal.SetActive(false);
        }

        private void BuildHintLegend(GameObject parent, string key, string label)
        {
            var item = V5GameUi.Row(parent, "Legend " + label, 8f, TextAnchor.MiddleLeft);
            V6Ui.KeyCap(item.gameObject, key, new Vector2(Mathf.Max(56f, key.Length * 12f + 16f), 32f));
            V5GameUi.Text(item.gameObject, "Label", label, 18f, V5Tokens.TextMuted);
        }

        private void BuildModalKeyCap(GameObject parent, string key)
        {
            var cap = V5Ui.Panel(parent, "KeyCap", new Vector2(Mathf.Max(56f, key.Length * 12f + 16f), 32f),
                FromHex("#00000040"), 4f);
            var capText = V5GameUi.Text(cap.gameObject, "Key", key, 20f, V5Tokens.TextMuted,
                TextAlignmentOptions.Center);
            Stretch(capText.GetComponent<RectTransform>(), cap.GetComponent<RectTransform>().sizeDelta.x, 32f);
        }

        private void OpenModal()
        {
            if (modal == null)
            {
                return;
            }

            modalOpen = true;
            submitInFlight = false;
            submitOutcome = V6ResultLogic.SubmitOutcome.Pending;
            nameBuffer.Length = 0;

            // 登録の方式 (送信対象か) を確定する。曲情報はプレイ開始時に固定した
            // (pinned) 版を使う (GManager.GetSong が ResultScene では snapshot を優先)。
            registrationMode = CurrentRegistrationMode();

            // 初期値: オンラインはオンライン側の保存名、無ければ端末内の保存名。
            // ローカルは端末内の保存名 (v5 と同じ PlayerPrefs キー)。
            string saved = V6ResultLogic.InitialName(
                registrationMode, RankingClient.LoadNickname(), V6LocalNameStore.Load());
            if (!string.IsNullOrEmpty(saved))
            {
                nameBuffer.Append(saved);
            }

            ApplyRegistrationModeTexts();

            // 旧オーバーレイ (ResultRankingOverlay) は Result シーンで自動で
            // モーダルを開き、キーも処理する。二重モーダルと二重入力を防ぐため
            // v6 モーダル表示中は停止し、閉じたら戻す (ソースは編集しない)。
            SuspendLegacyOverlay();

            RefreshModalName();
            RefreshModal();
            SetModalStatus("", V5Tokens.TextMuted);
            modal.SetActive(true);
        }

        private void CloseModal()
        {
            if (modal == null)
            {
                return;
            }

            modalOpen = false;
            if (submitInFlight)
            {
                // 応答待ちの送信は破棄しない (閉じてもコルーチンは完了させる)。
                // 失敗しても 429/10 秒窓でサーバ側が重複を拒否するため二重登録にはならない。
                submitInFlight = false;
            }

            SetRegisterInteractable(true);
            modal.SetActive(false);
            ResumeLegacyOverlay();
            FlashBeat.UI.SceneInputGate.BlockThisFrame();
        }

        // --- 登録送信 (Issue #240) ---

        /// <summary>ボタン経由の送信。送信中と登録済みは無視する (二重送信防止)。</summary>
        private void TrySubmitFromButton()
        {
            if (submitInFlight || submittedThisResult)
            {
                return;
            }

            TrySubmit();
        }

        private void TrySubmit()
        {
            if (submitInFlight || submittedThisResult)
            {
                return;
            }

            // 送信対象でない (ローカル曲 / 譜面未取得) はオンラインへ送らない。
            // v5 と同じ PlayerPrefs キーへ端末内保存だけする (Issue #240)。
            if (registrationMode == V6RegistrationMode.Local)
            {
                SaveLocalName();
                return;
            }

            V6ResultLogic.NicknameError error = V6ResultLogic.ValidateNicknameInput(
                nameBuffer.ToString(), RankingClient.NicknameMaxLength);
            if (error != V6ResultLogic.NicknameError.None)
            {
                SetModalStatus(NicknameErrorMessage(error), FromHex("#FFD64D"));
                return;
            }

            submitInFlight = true;
            submitOutcome = V6ResultLogic.SubmitOutcome.Pending;
            SetModalStatus(V6ResultLogic.SubmitStatusMessage(V6ResultLogic.SubmitOutcome.Pending, 0),
                FromHex("#99E5FF"));
            SetRegisterInteractable(false);
            RunCoroutine(SubmitCoroutine(nameBuffer.ToString().Trim()));
        }

        private IEnumerator SubmitCoroutine(string nickname)
        {
            // 曲情報はプレイ開始時に固定した (pinned) 版。song.chartVersion が
            // 実際にプレイした譜面の版になる (GManager.GetSong は ResultScene で
            // PlaySessionSnapshot の固定を優先する)。
            SongData song = SongOf();
            int chartVersion = V6ResultLogic.ResolveChartVersion(
                PinnedChartVersionOf(), song != null ? song.chartVersion : 0);
            if (!V6ResultLogic.IsSubmittable(
                    song != null && song.isRemote,
                    song != null ? song.remoteId : null,
                    chartVersion))
            {
                // 送信対象が無い (ローカル曲 / 譜面未取得)。オンラインの失敗表示
                // (「オフライン」等) は出さず、端末内保存だけ行う。
                SaveLocalName();
                yield break;
            }

            RankingClient.Result result = null;
            yield return RankingClient.PostScore(song.remoteId, chartVersion, nickname,
                runScore, runPerfect, runGreat, runBad, runMiss, runMaxCombo, r => { result = r; });
            OnSubmitResult(result);
        }

        /// <summary>
        /// 端末内保存 (Issue #240)。v5 と同じ PlayerPrefs キー "V5PlayerName" に
        /// v5 と同じ規則 (大文字化 + A-Z0-9 + 最大8文字) で保存し、
        /// オンライン語彙を含まない結果メッセージを表示する。
        /// </summary>
        private void SaveLocalName()
        {
            string saved = V6ResultLogic.NormalizeLocalName(nameBuffer.ToString());
            V6LocalNameStore.Save(saved);
            submittedThisResult = true;

            // 入力欄を正規化済みの保存名にそろえる。
            nameBuffer.Length = 0;
            nameBuffer.Append(saved);
            RefreshModalName();

            // 保存済み。Register ボタンで二重保存しない (Esc / Skip で閉じる)。
            SetRegisterInteractable(false);
            SetModalStatus(V6ResultLogic.LocalSaveMessage(saved), FromHex("#99E5FF"));
        }

        /// <summary>
        /// 現在の登録の方式を求める。曲情報はプレイ開始時に固定した版を使う。
        /// </summary>
        private V6RegistrationMode CurrentRegistrationMode()
        {
            SongData song = SongOf();
            int chartVersion = V6ResultLogic.ResolveChartVersion(
                PinnedChartVersionOf(), song != null ? song.chartVersion : 0);
            return V6ResultLogic.RegistrationMode(
                song != null && song.isRemote,
                song != null ? song.remoteId : null,
                chartVersion);
        }

        /// <summary>
        /// プレイ開始時に固定した譜面版 (pin)。固定が無い / 結果画面でないときは 0。
        /// </summary>
        private static int PinnedChartVersionOf()
        {
            SongData pinned = PlaySessionSnapshot.PinnedSongFor(GManager.songID);
            return pinned != null ? pinned.chartVersion : 0;
        }

        /// <summary>
        /// 方式に応じてモーダルの見出し・ボタン・入力ヒントを切り替える。
        /// ローカル曲にはオンラインの語彙 (スコア登録 / Register) を出さない。
        /// ローカル曲の上限は v5 と同じ (8 文字 / A-Z 0-9)。
        /// </summary>
        private void ApplyRegistrationModeTexts()
        {
            bool local = registrationMode == V6RegistrationMode.Local;
            if (modalHead != null)
            {
                // ローカル曲ではオンラインの語彙 (スコア登録) を出さない。
                // v5 のローカル登録モーダルと同じ見出し ("この端末の名前") を使う。
                modalHead.text = local ? "この端末の名前" : "スコアを登録";
            }

            if (modalRegisterLabel != null)
            {
                // v5 と同じ (ローカル保存は Save / オンライン送信は Register)。
                modalRegisterLabel.text = local ? "Save" : "Register";
            }

            if (modalNameHint != null)
            {
                modalNameHint.text = local
                    ? "最大" + V6ResultLogic.LocalNameMaxLength + "文字 (A-Z 0-9)"
                    : "最大" + RankingClient.NicknameMaxLength + "文字";
            }
        }

        private void OnSubmitResult(RankingClient.Result result)
        {
            if (!submitInFlight)
            {
                return; // 閉じたあとの応答は捨てる
            }

            submitInFlight = false;
            submitOutcome = V6ResultLogic.ClassifySubmitResult(
                result != null && result.ok, result != null ? result.httpCode : 0,
                result != null ? result.error : null);

            if (submitOutcome == V6ResultLogic.SubmitOutcome.Success
                && result.score != null && result.score.rank > 0)
            {
                // 偽の成功を出さない: 順位はサーバ応答からのみ。
                RankingClient.SaveNickname(nameBuffer.ToString().Trim());
                submittedThisResult = true;
                myRank = result.score.rank;
                SetRegisterInteractable(true);
                SetModalStatus("", V5Tokens.TextMuted);
                CloseModal();
                // 登録後の順位 (自分の行のハイライト) は再取得したランキングから。
                StartRankingFetch();
                return;
            }

            // 失敗: その事実を出して再送を許す (Esc = スキップ)。
            SetRegisterInteractable(true);
            SetModalStatus(V6ResultLogic.SubmitStatusMessage(submitOutcome,
                result != null ? result.httpCode : 0), FromHex("#FF9999"));
        }

        private static string NicknameErrorMessage(V6ResultLogic.NicknameError error)
        {
            switch (error)
            {
                case V6ResultLogic.NicknameError.Empty:
                    return "なまえを いれてください";
                case V6ResultLogic.NicknameError.TooLong:
                    return "なまえが ながすぎます (最大 " + RankingClient.NicknameMaxLength + "文字)";
                case V6ResultLogic.NicknameError.InvalidChar:
                    return "なまえに つかえない文字が あります";
                default:
                    return "";
            }
        }

        private void HandleModalInput()
        {
            if (submitInFlight)
            {
                return; // 応答待ち。Esc で中断はしない (二重送信を防ぐ)。
            }

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                CloseModal();
                return;
            }

            if (Input.GetKeyDown(KeyCode.Backspace))
            {
                if (nameBuffer.Length > 0)
                {
                    nameBuffer.Length -= 1;
                }

                RefreshModalName();
                return;
            }

            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                TrySubmit();
                return;
            }

            // Input.inputString から 1 文字ずつ受け取る (レイアウト非依存・IME 不要。
            // ResultRankingOverlay と同じ方式)。
            string input = Input.inputString;
            for (int i = 0; i < input.Length; i++)
            {
                char c = input[i];
                if (c < ' ')
                {
                    continue; // 制御文字 (\n, \b) は KeyCode 側で処理済み
                }

                if (nameBuffer.Length >= NameMaxLength())
                {
                    break;
                }

                // ローカル保存は v5 と同じ入力規則 (A-Z0-9 のみ)。
                // 規則外の文字は捨てる (v5 は TMP_InputField の Alphanumeric 検証)。
                if (registrationMode == V6RegistrationMode.Local && !IsLocalNameChar(c))
                {
                    continue;
                }

                nameBuffer.Append(c);
            }

            if (input.Length > 0)
            {
                RefreshModalName();
            }
        }

        /// <summary>ローカル名に使える文字 (v5 と同じ A-Z0-9)。</summary>
        private static bool IsLocalNameChar(char c)
        {
            return (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9');
        }

        /// <summary>入力可能な名前の上限文字数 (ローカル 8 / オンライン 16)。</summary>
        private int NameMaxLength()
        {
            return registrationMode == V6RegistrationMode.Local
                ? V6ResultLogic.LocalNameMaxLength
                : RankingClient.NicknameMaxLength;
        }

        private void RefreshModalName()
        {
            if (modalNameValue != null)
            {
                modalNameValue.text = nameBuffer.ToString();
            }
        }

        private void SetModalStatus(string message, Color color)
        {
            if (modalStatus != null)
            {
                modalStatus.text = message;
                modalStatus.color = color;
            }
        }

        private void SetRegisterInteractable(bool interactable)
        {
            if (modalRegisterButton != null)
            {
                modalRegisterButton.interactable = interactable;
            }
        }

        private void SuspendLegacyOverlay()
        {
            if (legacyOverlay == null)
            {
                legacyOverlay = UnityEngine.Object.FindFirstObjectByType<ResultRankingOverlay>();
            }

            if (legacyOverlay != null)
            {
                // 停止前の状態を覚えておく。統合側が v6 有効時に先に enabled=false して
                // いる場合、モーダルを閉じたときに再有効化して自動モーダルを
                // 復活させてしまわないようにする。
                legacyOverlayWasEnabled = legacyOverlay.enabled;
                legacyOverlay.enabled = false;
            }
        }

        private void ResumeLegacyOverlay()
        {
            if (legacyOverlay != null)
            {
                if (legacyOverlayWasEnabled)
                {
                    legacyOverlay.enabled = true;
                }
                legacyOverlay = null;
            }
        }

        // --- 実データの反映 ---

        /// <summary>
        /// 表示前に GManager.ResetSession() が呼ばれて値が消えるなら、消える前に
        /// 捕捉する (初回 Refresh で 1 回だけ)。以後の Refresh は確定値を使う。
        /// </summary>
        private void CaptureRun()
        {
            runScore = Mathf.Max(0, GManager.score);
            runPerfect = GManager.perfect;
            runGreat = GManager.great;
            runBad = GManager.bad;
            runMiss = GManager.miss;
            runMaxCombo = GManager.maxCombo;
            runCaptured = true;
        }

        private void RefreshResult()
        {
            int score = runScore;
            gradeLetter.text = V6ResultLogic.GradeOf(score);
            scoreValue.text = V6ResultLogic.FormatScore(score);
            judgeValues[0].text = runPerfect.ToString();
            judgeValues[1].text = runGreat.ToString();
            judgeValues[2].text = runBad.ToString();
            judgeValues[3].text = runMiss.ToString();
            judgeValues[4].text = runMaxCombo.ToString();

            float fill = V6ResultLogic.GaugeFillWidth(score);
            gaugeFill.rectTransform.sizeDelta = new Vector2(fill, GaugeTrackH);
            int best = BestOf();
            float bestX = V6ResultLogic.GaugeBestX(best);
            gaugeBestMarker.rectTransform.anchoredPosition = new Vector2(bestX, -20f);

            if (prevHiscore >= 0)
            {
                string delta = V6ResultLogic.DeltaLabel(prevHiscore, score);
                bestValue.text = delta.Length > 0 ? delta : V6ResultLogic.FormatScore(prevHiscore);
            }
            else
            {
                bestValue.text = "–";
            }
        }

        private void RefreshSongInfo()
        {
            SongData song = SongOf();
            if (song == null)
            {
                songTitle.text = "";
                songArtist.text = "";
                for (int i = 0; i < songStatValues.Length; i++)
                {
                    songStatValues[i].text = "–";
                }

                return;
            }

            songTitle.text = song.title ?? "";
            songArtist.text = song.musician ?? "";
            songStatValues[0].text = song.bpm > 0 ? song.bpm.ToString() : "–";
            songStatValues[1].text = song.totalHits > 0 ? song.totalHits.ToString() : "–";
            songStatValues[2].text = song.level > 0 ? song.level.ToString() : "–";
            songStatValues[3].text = V6ResultLogic.DurationText(SongLongOf());

            SetThumb(songThumb, song);
        }

        private void RefreshRanking()
        {
            // 曲別ランキング: 実取得結果 (未取得中は状態メッセージ)。
            RebuildSongRankingRows();

            if (rankingFetchInFlight)
            {
                songRankEmpty.text = string.IsNullOrEmpty(rankingStatusMessage)
                    ? "ランキングを取得しています…" : rankingStatusMessage;
                songRankEmpty.gameObject.SetActive(true);
            }
            else if (rankingRows.Count == 0)
            {
                songRankEmpty.text = hasRankingError && !string.IsNullOrEmpty(rankingStatusMessage)
                    ? rankingStatusMessage : "NO DATA";
                songRankEmpty.gameObject.SetActive(true);
            }
            else
            {
                songRankEmpty.gameObject.SetActive(false);
            }
        }

        private void RebuildSongRankingRows()
        {
            var children = new List<GameObject>();
            foreach (Transform child in songRankRows.transform)
            {
                children.Add(child.gameObject);
            }

            for (int i = 0; i < children.Count; i++)
            {
                V5GameUi.DestroyNow(children[i]);
            }

            int rows = Mathf.Min(rankingRows.Count, SongRankVisibleRows);
            for (int i = 0; i < rows; i++)
            {
                BuildRankingRow(songRankRows.gameObject, rankingRows[i], 92f + RankRowStep * i);
            }
        }

        private void BuildRankingRow(GameObject parent, FlashBeat.UI.V6.Select.V6RankingRow row, float y)
        {
            var line = At(V5Ui.Panel(parent, "Score " + row.Position, new Vector2(RankRowW, RankRowH),
                row.IsYou ? V5Tokens.Key6 : V5Tokens.Key1, 8f), 8f, y);

            var pos = At(V5Ui.Panel(line.gameObject, "Pos", new Vector2(36f, 36f), V5Tokens.Key4, 18f), 4f, 4f);
            var posText = V5GameUi.Text(pos.gameObject, "PosText", row.Position.ToString(), 22f,
                row.IsYou ? V5Tokens.Text : V5Tokens.TextMuted, TextAlignmentOptions.Center, FontStyles.Normal);
            Stretch(posText.GetComponent<RectTransform>(), 36f, 36f);

            var name = V5GameUi.Text(line.gameObject, "Name", row.IsYou ? "YOU" : row.PlayerName, 22f, V5Tokens.Text);
            At(name, 52f, 0f, 140f, RankRowH, new Vector2(0f, 0.5f));

            var score = V5GameUi.Text(line.gameObject, "Score", V6ResultLogic.FormatScore(row.Score), 22f,
                V5Tokens.Text, TextAlignmentOptions.Right);
            At(score, 200f, 0f, 180f, RankRowH, new Vector2(0f, 0.5f));

            var grade = At(V5Ui.Panel(line.gameObject, "Grade", new Vector2(56f, 36f), V5Tokens.Key4, 4f), 436f, 4f);
            var gradeText = V5GameUi.Text(grade.gameObject, "GradeText", string.IsNullOrEmpty(row.Grade) ? "–" : row.Grade,
                22f, V5Tokens.Text, TextAlignmentOptions.Center, FontStyles.Normal);
            Stretch(gradeText.GetComponent<RectTransform>(), 56f, 36f);
        }

        private void RefreshModal()
        {
            RefreshModalName();
            int score = runScore;
            modalScoreValue.text = V6ResultLogic.FormatScore(score);
            modalGrade.text = V6ResultLogic.GradeOf(score);

            SongData song = SongOf();
            if (song != null)
            {
                modalSongTitle.text = song.title ?? "";
                modalSongArtist.text = song.musician ?? "";
                SetThumb(modalThumb, song);
            }
            else
            {
                modalSongTitle.text = "";
                modalSongArtist.text = "";
            }

            int best = BestOf();
            if (prevHiscore >= 0)
            {
                string delta = V6ResultLogic.DeltaLabel(prevHiscore, score);
                bool beaten = delta.Length > 0;
                modalRecordLabel.text = "前回最高 " + V6ResultLogic.FormatScore(beaten ? prevHiscore : best);
                modalRecordValue.text = delta;
                modalRecordNote.text = beaten ? "自己ベスト更新" : "";
            }
            else
            {
                modalRecordLabel.text = "前回最高 " + V6ResultLogic.FormatScore(best);
                modalRecordValue.text = "";
                modalRecordNote.text = "";
            }
        }

        // --- ランキング取得 (実通信は既存 RankingClient) ---

        private void StartRankingFetch()
        {
            SongData song = SongOf();
            if (song == null || !song.isRemote)
            {
                return; // ローカル曲にオンラインランキングはない (V6SelectDataSource と同じ規律)
            }

            // 譜面版はプレイ開始時に固定した (pinned) 版を使う (Issue #240)。
            int chartVersion = V6ResultLogic.ResolveChartVersion(
                PinnedChartVersionOf(), song.chartVersion);
            if (string.IsNullOrEmpty(song.remoteId) || chartVersion <= 0
                || !RankingClient.IsValidSongId(song.remoteId))
            {
                return;
            }

            if (!Application.isPlaying)
            {
                return; // EditMode (テスト) では通信しない
            }

            if (rankingFetchInFlight)
            {
                return;
            }

            rankingFetchInFlight = true;
            hasRankingError = false;
            rankingStatusMessage = "ランキングを取得しています…";
            RefreshRanking();
            RunCoroutine(FetchRanking(song.remoteId, chartVersion));
        }

        private IEnumerator FetchRanking(string remoteId, int chartVersion)
        {
            RankingClient.Result result = null;
            yield return RankingClient.GetLeaderboard(remoteId, chartVersion, r => { result = r; });

            rankingFetchInFlight = false;
            rankingRows.Clear();
            if (result != null && result.ok && result.leaderboard != null && result.leaderboard.entries != null)
            {
                hasRankingError = false;
                rankingStatusMessage = "";
                string nickname = RankingClient.LoadNickname();
                RankingClient.LeaderboardEntry[] entries = result.leaderboard.entries;
                for (int i = 0; i < entries.Length; i++)
                {
                    RankingClient.LeaderboardEntry entry = entries[i];
                    if (entry == null)
                    {
                        continue;
                    }

                    rankingRows.Add(new FlashBeat.UI.V6.Select.V6RankingRow
                    {
                        Position = entry.rank,
                        PlayerName = entry.nickname ?? "",
                        Score = entry.score,
                        Grade = FlashBeat.UI.V6.Select.Data.V6SelectDataLogic.GradeOf(entry.score),
                        IsYou = (!string.IsNullOrEmpty(nickname) && entry.nickname == nickname)
                            || (myRank > 0 && entry.rank == myRank),
                    });
                }
            }
            else
            {
                hasRankingError = true;
                rankingStatusMessage = RankingFailureMessage(result);
            }

            RefreshRanking();
        }

        private static string RankingFailureMessage(RankingClient.Result result)
        {
            if (result == null)
            {
                return "ランキングを取得できませんでした";
            }

            if (result.error == RankingClient.UnavailableError)
            {
                return "ネットワークに接続できないためランキングを取得できませんでした";
            }

            if (result.httpCode > 0)
            {
                return string.Format(CultureInfo.InvariantCulture,
                    "ランキングの取得に失敗しました (HTTP {0})", result.httpCode);
            }

            return string.IsNullOrEmpty(result.error)
                ? "ランキングを取得できませんでした"
                : "ランキングの取得に失敗しました (" + result.error + ")";
        }

        private static Coroutine RunCoroutine(IEnumerator routine)
        {
            if (host == null)
            {
                var go = new GameObject("V6ResultCoroutineHost");
                go.hideFlags = HideFlags.HideAndDontSave;
                host = go.AddComponent<V6ResultCoroutineHost>();
            }

            return host.StartCoroutine(routine);
        }

        private static V6ResultCoroutineHost host;

        // --- データアクセス (実データのみ) ---

        private static SongData SongOf()
        {
            SongData[] songs = GManager.ActiveSongs;
            int id = GManager.songID;
            if (songs == null || id < 0 || id >= songs.Length)
            {
                return null;
            }

            // GManager.GetSong は ResultScene / GameScene ではプレイ開始時に固定した
            // (pinned) 曲・譜面版を優先する (Issue #240: chartVersion は固定版を使う)。
            return GManager.GetSong(id) ?? songs[id];
        }

        private static int BestOf()
        {
            int[] hiscores = GManager.Hiscore;
            if (hiscores == null)
            {
                return 0;
            }

            int idx = GManager.songID;
            if (idx >= 0 && idx < hiscores.Length)
            {
                return hiscores[idx];
            }

            // 固定テーブル外の remote songs はスロット 0 を共有しない
            // (issue-162 A-6 再照合)。安定キーの保存先から読む。
            return GManager.BestScoreOf(idx);
        }

        private static float SongLongOf()
        {
            float[] lengths = GManager.SongLong;
            int idx = GManager.songID;
            if (lengths == null || idx < 0 || idx >= lengths.Length)
            {
                return 0f;
            }

            return lengths[idx];
        }

        // --- ジャケット (V6SelectDataSource.JacketResourcePathOf と同じ対応表。
        //     Select/** は編集不可のため Result 側に同じ写しを置く) ---

        private static readonly Dictionary<int, string> JacketFiles = new Dictionary<int, string>
        {
            { 1, "01-rosen.jpg" }, { 2, "02-mani.jpg" }, { 3, "03-who.jpg" }, { 4, "04-tweet.jpg" },
            { 5, "05-shuumatsu.jpg" }, { 6, "06-euta.jpg" }, { 7, "07-chartreuse.jpg" }, { 8, "08-psyched.jpg" },
            { 9, "09-dogbite.jpg" }, { 10, "10-tutorial.jpg" }, { 11, "11-kyoukiranmai.jpg" },
            { 12, "12-poison.jpg" }, { 13, "13-selfie.jpg" }, { 14, "14-4thsmile.jpg" },
            { 15, "15-calamity.jpg" }, { 16, "16-panopticon.jpg" }, { 17, "17-guru.jpg" }, { 18, "18-abun.jpg" },
            { 19, "19-mushin.jpg" }, { 20, "20-haruarashi.jpg" }, { 21, "21-bibibibi.jpg" },
            { 23, "23-override.jpg" }, { 24, "24-igaku.jpg" }, { 25, "25-tekkyu.jpg" }, { 26, "26-ikken.jpg" },
        };

        private static string JacketResourcePathOf(int songId)
        {
            string file;
            if (JacketFiles.TryGetValue(songId, out file))
            {
                return "V6/select/jacket/" + file;
            }

            return "";
        }

        private static void SetThumb(Image thumb, SongData song)
        {
            if (thumb == null)
            {
                return;
            }

            Sprite sprite = null;
            string path = JacketResourcePathOf(song.id);
            if (!string.IsNullOrEmpty(path))
            {
                var tex = Resources.Load<Texture2D>(path);
                if (tex != null)
                {
                    sprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height),
                        new Vector2(0.5f, 0.5f), 100f);
                }
            }

            if (sprite != null)
            {
                thumb.type = Image.Type.Simple;
                thumb.sprite = sprite;
                thumb.color = Color.white;
                thumb.preserveAspect = true;
            }
            else
            {
                // ジャケット無しは空枠 (Select v6 の Thumb と同じ $k3)。
                thumb.type = Image.Type.Sliced;
                thumb.sprite = V5Ui.RoundedSprite(4);
                thumb.color = V5Tokens.Key3;
            }
        }

        // --- uGUI ユーティリティ (design px 左上原点, V6SelectView と同じ方式) ---

        private static GameObject At(GameObject go, float x, float y)
        {
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            return go;
        }

        private static GameObject At(GameObject go, float x, float y, float w, float h)
        {
            At(go, x, y);
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(w, h);
            return go;
        }

        private static GameObject At(Component node, float x, float y, float w, float h)
        {
            return At(node.gameObject, x, y, w, h);
        }

        private static GameObject At(Component node, float x, float y, float w, float h, Vector2 pivot)
        {
            At(node.gameObject, x, y, w, h);
            var rt = node.GetComponent<RectTransform>();
            rt.pivot = pivot;
            // anchoredPosition は pivot の位置を指す。pivot を (0,1) から変えたときも
            // 矩形の左上が (x, -y) に来るよう pivot 分を補正する (旧実装は補正が無く、
            // 矩形全体が (1 - pivot.y) * h だけ上へずれ、パネル見出しや「累計スコア」が
            // パネル上端の枠線に乗っていた)。
            rt.anchoredPosition = new Vector2(x + pivot.x * w, -(y + (1f - pivot.y) * h));
            return node.gameObject;
        }

        private static void Stretch(RectTransform rt, float w, float h)
        {
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(w, h);
        }

        private static GameObject Button(GameObject parent, string name, float w, float h, float radius, Color bg,
            Action onClick)
        {
            var go = V5Ui.Panel(parent, name, new Vector2(w, h), bg, radius);
            var button = go.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            var nav = button.navigation;
            nav.mode = Navigation.Mode.None;
            button.navigation = nav;
            if (onClick != null)
            {
                button.onClick.AddListener(() => onClick());
            }

            return go;
        }

        private static Color Clear()
        {
            return new Color(0f, 0f, 0f, 0f);
        }

        /// <summary>
        /// 親いっぱいに広がる薄い暗色スクリーン ($scrim #00000099、raycast なし)。
        /// 全面背景は敷かない代わりに、文字が乗る領域の可読性を確保する。
        /// </summary>
        private static void Scrim(GameObject parent)
        {
            var scrim = new GameObject("Scrim", typeof(RectTransform), typeof(Image));
            scrim.transform.SetParent(parent.transform, false);
            var image = scrim.GetComponent<Image>();
            image.color = V6Tokens.Scrim;
            image.raycastTarget = false;
            var rt = scrim.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static Color FromHex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            return c;
        }
    }
}
