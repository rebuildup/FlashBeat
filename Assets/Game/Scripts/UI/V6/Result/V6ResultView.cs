using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FlashBeat.UI.V5;
using FlashBeat.UI.V6;

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
    /// - 寸法は design px (1728x972 基準)。frame を u = min(W/1728, H/972) 倍して
    ///   3 解像度 (FHD / 2K / Mac) でクリップしないようにする
    ///   (v6 HUD / Select と同じ方式)。色は V5Tokens を流用する。
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

        // メインゾーン (zcSHh は共有フレームの 6 列を 2 列に差し替え: x=164 w=1400)。
        private const float MainX = 164f, MainY = 96f, MainW = 1400f, MainH = 780f;

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

        // 楽曲情報 (0,636) 840x120。
        private const float SongInfoY = 636f, SongInfoH = 120f;
        private static readonly float[] SongStatX = { 348f, 468f, 588f, 708f };

        // ランキング (列4-5)。
        private const float RankX = 856f;
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

        private float u = 1f;
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

            UpdateScale();
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

            UpdateScale();

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

            frame = V5Ui.Node(rootGo, "V6 Result Frame 1728x972").GetComponent<RectTransform>();
            frame.anchorMin = new Vector2(0.5f, 0.5f);
            frame.anchorMax = new Vector2(0.5f, 0.5f);
            frame.pivot = new Vector2(0.5f, 0.5f);
            frame.sizeDelta = new Vector2(FrameW, FrameH);

            driver = rootGo.AddComponent<V6ResultViewDriver>();
            driver.View = this;
            UpdateScale();
        }

        private void UpdateScale()
        {
            int w = Screen.width;
            int h = Screen.height;
            if (w == lastWidth && h == lastHeight)
            {
                return;
            }

            lastWidth = w;
            lastHeight = h;
            u = V6ResultLogic.DesignScale(w, h);
            if (frame != null)
            {
                frame.localScale = new Vector3(u, u, 1f);
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

            var title = V5GameUi.Text(header.gameObject, "Title", "RESULT", 38f, V5Tokens.Text,
                TextAlignmentOptions.Left, FontStyles.Bold);
            At(title, 72f, 20f, 400f, 56f, new Vector2(0f, 0.5f));
        }

        // --- メイン (x164 y96 1400x780) ---

        private void BuildMain()
        {
            var main = At(V5Ui.Panel(frame.gameObject, "Main", new Vector2(MainW, MainH), Clear(), 0f),
                MainX, MainY);
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

        private void BuildSongInfo(GameObject main)
        {
            var panel = At(V5Ui.Panel(main.gameObject, "Song Info", new Vector2(840f, SongInfoH),
                V5Tokens.Key2, 16f), 0f, SongInfoY);

            songThumb = At(V5Ui.Panel(panel.gameObject, "Thumb", new Vector2(112f, 63f), V5Tokens.Key3, 4f),
                16f, 28f).GetComponent<Image>();

            songTitle = V5GameUi.Text(panel.gameObject, "Title", "", 34f, V5Tokens.Text,
                TextAlignmentOptions.Left, FontStyles.Normal);
            At(songTitle, 144f, 16f, 200f, 40f, new Vector2(0f, 0.5f));

            songArtist = V5GameUi.Text(panel.gameObject, "Artist", "", 23f, V5Tokens.TextMuted);
            At(songArtist, 144f, 66f, 200f, 30f, new Vector2(0f, 0.5f));

            string[] statLabels = { "BPM", "NOTES", "LEVEL", "曲長" };
            for (int i = 0; i < statLabels.Length; i++)
            {
                var cell = At(V5Ui.Node(panel.gameObject, "Stat " + statLabels[i]), SongStatX[i], 20f, 116f, 64f);
                var col = V5GameUi.Column(cell, "Inner", 2f, TextAnchor.UpperLeft);
                Stretch(col.GetComponent<RectTransform>(), 116f, 64f);
                V5GameUi.Text(col.gameObject, "Label", statLabels[i], 18f, V5Tokens.TextDim);
                songStatValues[i] = V5GameUi.Text(col.gameObject, "Value", "–", 30f, V5Tokens.Text,
                    TextAlignmentOptions.Left, FontStyles.Normal);
            }
        }

        // 曲別ランキング (856,24) 528x412: Head + 曲名 + 6 行。

        private void BuildSongRanking(GameObject main)
        {
            var panel = At(V5Ui.Panel(main.gameObject, "Song Ranking", new Vector2(528f, SongRankH),
                V5Tokens.Key2, 8f), RankX, SongRankY);

            var head = At(V5Ui.Node(panel.gameObject, "Head"), 8f, 8f, 512f, 36f);
            var headTitle = V5GameUi.Text(head.gameObject, "Title", "曲別ランキング", 18f, V5Tokens.TextDim);
            At(headTitle, 12f, 0f, 300f, 36f, new Vector2(0f, 0.5f));

            songRankEmpty = V5GameUi.Text(panel.gameObject, "No Data", "", 24f, V5Tokens.TextDim);
            At(songRankEmpty, 20f, 60f, 496f, 44f, new Vector2(0f, 0.5f));

            songRankRows = V5Ui.Node(panel.gameObject, "Rows").GetComponent<RectTransform>();
            At(songRankRows, 0f, 0f, 528f, SongRankH);
        }

        // 登録名ランキング (856,452) 528x304: Head (集計軸) + 4 行。
        // 登録名ごとの集計は既存 API に無いため空状態のみ (未実装は PR に記載)。

        private void BuildPlayerRanking(GameObject main)
        {
            var panel = At(V5Ui.Panel(main.gameObject, "Player Ranking", new Vector2(528f, PlayerRankH),
                V5Tokens.Key2, 8f), RankX, PlayerRankY);

            var head = At(V5Ui.Node(panel.gameObject, "Head"), 8f, 8f, 512f, 36f);
            var headTitle = V5GameUi.Text(head.gameObject, "Title", "登録名ごとのランキング", 18f, V5Tokens.TextDim);
            At(headTitle, 12f, 0f, 300f, 36f, new Vector2(0f, 0.5f));
            BuildChevron(head, 380f, "chevron-left");
            var metric = V5GameUi.Text(head.gameObject, "Metric", "累計スコア", 18f, V5Tokens.Text,
                TextAlignmentOptions.Center);
            At(metric, 412f, 0f, 68f, 36f, new Vector2(0f, 0.5f));
            BuildChevron(head, 480f, "chevron-right");

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

            // キー凡例 (左下): R リトライ / Enter 登録 / Space 決定。
            var legend = V5GameUi.Row(footer.gameObject, "Legend", 24f, TextAnchor.MiddleLeft);
            At(legend.GetComponent<RectTransform>(), 0f, 30f, 620f, 36f);
            BuildLegendItem(legend.gameObject, "R", "リトライ");
            BuildLegendItem(legend.gameObject, "Enter", "登録");
            BuildLegendItem(legend.gameObject, "Space", "決定");

            // 右下: Retry (secondary) + Register (secondary) + OK (primary)。
            var retry = BuildFooterButton(footer, "Op Retry", 220f, V5Tokens.Key4, "skip-back", "Retry", "R",
                V5Tokens.TextMuted, V5Tokens.Text, onRetry);
            At(retry, 882f, 20f);
            var register = BuildFooterButton(footer, "Op Register", 273f, V5Tokens.Key4, "plus", "Register", "Enter",
                V5Tokens.TextMuted, V5Tokens.Text, OpenModal);
            At(register, 1118f, 20f);
            var ok = BuildFooterButton(footer, "Op OK", 273f, V5Tokens.ButtonPrimaryBg, "skip-forward", "OK", "Space",
                V5Tokens.ButtonPrimaryLabel, V5Tokens.ButtonPrimaryLabel, onSelect);
            At(ok, 1407f, 20f);
        }

        private void BuildLegendItem(GameObject parent, string key, string label)
        {
            var item = V5GameUi.Row(parent, "Legend " + label, 8f, TextAnchor.MiddleLeft);
            V6Ui.KeyCap(item.gameObject, key, new Vector2(Mathf.Max(56f, key.Length * 14f + 16f), 36f));
            V5GameUi.Text(item.gameObject, "Label", label, 22f, V5Tokens.TextMuted);
        }

        private GameObject BuildFooterButton(GameObject footer, string name, float w, Color bg, string iconSprite,
            string label, string keyCap, Color iconColor, Color labelColor, Action onClick)
        {
            var btn = Button(footer, name, w, 56f, 8f, bg, onClick);
            var row = V5GameUi.Row(btn, "Inner", 12f, TextAnchor.MiddleCenter);
            Stretch(row.GetComponent<RectTransform>(), w, 56f);
            row.padding = new RectOffset(0, 0, 0, 0);
            if (!string.IsNullOrEmpty(iconSprite))
            {
                var icon = V6Icons.Create(row.gameObject, iconSprite, 28f, iconColor, "Icon");
            }
            V5GameUi.Text(row.gameObject, "Label", label, 30f, labelColor, TextAlignmentOptions.Center,
                FontStyles.Normal);
            var cap = V5Ui.Panel(row.gameObject, "KeyCap", new Vector2(Mathf.Max(56f, keyCap.Length * 14f + 16f), 36f),
                bg == V5Tokens.ButtonPrimaryBg ? FromHex("#00000026") : FromHex("#00000040"), 4f);
            var capText = V5GameUi.Text(cap.gameObject, "Key", keyCap, 22f, iconColor, TextAlignmentOptions.Center);
            Stretch(capText.GetComponent<RectTransform>(), cap.GetComponent<RectTransform>().sizeDelta.x, 36f);
            return btn;
        }

        // --- 登録モーダル (05b lqHaN): 見た目と開閉のみ (入力機能は未実装) ---

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

            var dialog = At(V5Ui.Panel(modal.gameObject, "Dialog", new Vector2(840f, 510f), V5Tokens.Key1, 16f),
                444f, 231f);

            var head = V5GameUi.Text(dialog.gameObject, "Head", "スコアを登録", 30f, V5Tokens.Text,
                TextAlignmentOptions.Left, FontStyles.Bold);
            At(head, 28f, 28f, 400f, 40f, new Vector2(0f, 0.5f));

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
            V5GameUi.Text(hints.gameObject, "Hint Text",
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
            V5GameUi.Text(skipRow.gameObject, "Icon", "✕", 22f, V5Tokens.TextMuted, TextAlignmentOptions.Center);
            V5GameUi.Text(skipRow.gameObject, "Label", "Skip", 26f, V5Tokens.Text, TextAlignmentOptions.Center);
            BuildModalKeyCap(skipRow.gameObject, "Esc");
            var register = Button(buttons.gameObject, "Button Register", 252f, 50f, 8f, V5Tokens.ButtonPrimaryBg,
                TrySubmitFromButton);
            var registerRow = V5GameUi.Row(register, "Inner", 12f, TextAnchor.MiddleCenter);
            Stretch(registerRow.GetComponent<RectTransform>(), 252f, 50f);
            V5GameUi.Text(registerRow.gameObject, "Icon", "✓", 22f, V5Tokens.ButtonPrimaryLabel,
                TextAlignmentOptions.Center);
            V5GameUi.Text(registerRow.gameObject, "Label", "Register", 26f, V5Tokens.ButtonPrimaryLabel,
                TextAlignmentOptions.Center, FontStyles.Normal);
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
            string saved = RankingClient.LoadNickname();
            if (!string.IsNullOrEmpty(saved))
            {
                nameBuffer.Append(saved);
            }

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
            SongData song = SongOf();
            if (song == null || string.IsNullOrEmpty(song.remoteId) || song.chartVersion <= 0
                || !RankingClient.IsValidSongId(song.remoteId))
            {
                // 送信対象が無い (ローカル曲 / 譜面未取得)。偽の成功は出さない。
                OnSubmitResult(null);
                yield break;
            }

            RankingClient.Result result = null;
            yield return RankingClient.PostScore(song.remoteId, song.chartVersion, nickname,
                runScore, runPerfect, runGreat, runBad, runMiss, runMaxCombo, r => { result = r; });
            OnSubmitResult(result);
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

                if (nameBuffer.Length >= RankingClient.NicknameMaxLength)
                {
                    break;
                }

                nameBuffer.Append(c);
            }

            if (input.Length > 0)
            {
                RefreshModalName();
            }
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

            if (string.IsNullOrEmpty(song.remoteId) || song.chartVersion <= 0
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
            RunCoroutine(FetchRanking(song.remoteId, song.chartVersion));
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

            return songs[id];
        }

        private static int BestOf()
        {
            int[] hiscores = GManager.Hiscore;
            if (hiscores == null)
            {
                return 0;
            }

            int idx = GManager.songID;
            if (idx < 0 || idx >= hiscores.Length)
            {
                idx = 0; // 固定テーブル外の remote songs は slot 0 (ResultSceneManager と同じ規律)
            }

            return hiscores[idx];
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
            // pivot 変更で位置がずれるので左上 (x, -y) を維持する。
            rt.anchoredPosition = new Vector2(x, -y);
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

        private static Color FromHex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            return c;
        }
    }
}
