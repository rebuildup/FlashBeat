using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.EventSystems;
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

        // メインゾーン: 正本 zcSHh は共有フレームの Main を 5 列 ((164,96) 1400x780) に
        // 差し替える。共有フレームの 6 列 1680 を使うと右カラムが design より 156px 外へ出る。
        // 定数は V6ResultLogic.MainX..MainH / RankX (テストと共有)。

        // 結果 (列1-3) (0,24) 840x280。
        private const float ResultBlockY = 24f;
        private const float GradeBoxW = 280f, GradeBoxH = 280f;
        private const float ScoreColX = 336f;

        // トロフィーは結果ブロックの中ではなく、ヘッダー左上の空きスロット
        // (design zcSHh/biox4、他画面の戻るボタンと同じ 56×56) に置く。
        // メイン領域に置くとスコアの左に浮いて読み順を乱し、他画面では
        // 戻るボタンが入る枠が Result だけ空白になっていた。
        private const float TrophyBadgeX = 0f, TrophyBadgeY = 20f, TrophyBadgeSize = 56f;
        private const float TrophyIconSize = 28f;

        // スコアゲージ (0,336) 840x100 → Gauge (0,16) 824x76。
        private const float GaugeZoneY = 336f;
        private const float GaugeY = 16f;
        private const float GaugeTrackY = 24f, GaugeTrackH = 16f;

        // 判定 (0,448) 840x140。5 セル x=0,168,336,504,672 各 160 幅。
        private const float JudgeY = 448f;
        private static readonly float[] JudgeX = { 0f, 168f, 336f, 504f, 672f };
        private const float JudgeCellW = 160f, JudgeCellH = 140f;

        // 楽曲情報 (0,636) 840x120 の寸法は V6ResultLogic.SongInfo* (テストと共有)。

        // ランキング (列4-5, Main 内 x=856 → V6ResultLogic.RankX)。
        private const float SongRankY = 24f, SongRankH = 412f;
        private const float PlayerRankY = 452f, PlayerRankH = 304f;
        private const int SongRankVisibleRows = 6;
        private const int PlayerRankVisibleRows = 4;
        private const float RankRowStep = 52f;

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
        private TextMeshProUGUI songRankTitle;
        private RectTransform songRankRows;
        private TextMeshProUGUI playerRankEmpty;
        // Issue #415: 曲別ランキング行の差分更新用スロット (行オブジェクトを再利用する)。
        private readonly List<GameObject> songRankRowObjects = new List<GameObject>();

        // 段階表示 (Issue #384)。要素は CanvasGroup のアルファで出し入れする
        // (レイアウトは動かさない)。
        private Image trophyIcon;
        private RectTransform trophyRect;
        private CanvasGroup trophyGroup;
        private CanvasGroup gradeGroup;
        private RectTransform gradeRect;
        private CanvasGroup scoreLabelGroup;
        private CanvasGroup scoreValueGroup;
        private CanvasGroup bestLabelGroup;
        private CanvasGroup bestValueGroup;
        private CanvasGroup gaugeGroup;
        private readonly CanvasGroup[] judgeGroups = new CanvasGroup[5];
        private readonly List<CanvasGroup> lateGroups = new List<CanvasGroup>();
        private float revealElapsed;
        private bool revealComplete;
        private bool registerShown;

        // 登録モーダル (Issue #240)。名前入力 + 送信 + 結果表示を実装する。
        private GameObject modal;
        private TextMeshProUGUI modalNameValue;
        private TextMeshProUGUI modalNamePlaceholder;
        private TMP_InputField modalNameInput;
        private string submittedNickname = string.Empty;
        private TextMeshProUGUI modalNamePrompt;
        private GameObject modalChip;
        private CanvasGroup modalChipGroup;
        private TextMeshProUGUI modalChipLabel;
        private Image modalChipBackground;
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

        // チップ (登録しました / 登録できませんでした) の表示時刻と後処理。
        private bool chipActive;
        private float chipShownAt;
        private bool chipClosesModal;

        // モーダルを閉じる・開き直すときに onEndEdit (フォーカス喪失 = 確定) を
        // 抑止する。閉じる操作が送信を誘発しないようにするため。
        private bool suppressNameCommit;

        // 登録送信の状態 (loading / success / duplicate / offline / server error)。
        private V6ResultLogic.SubmitOutcome submitOutcome = V6ResultLogic.SubmitOutcome.Pending;
        private int lastSubmitHttpCode;

        // 登録の方式 (Issue #240)。モーダルを開いた時点で確定する。
        // Online = オンライン順位へ送信 / Local = 端末内だけに保存。
        private V6RegistrationMode registrationMode = V6RegistrationMode.Local;

        // 送信状態 (Idle → Submitting → Succeeded / Failed) と二重送信の抑止
        // (Issue #384)。名前の確定は Enter / フォーカス喪失 / ボタンのどれでも
        // 同じ gate を通る。
        private readonly V6RegistrationGate gate = new V6RegistrationGate();

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

        /// <summary>
        /// 画面監査 (AutoTestV6Screens) 用: 登録モーダル (design 05b lqHaN) を開く。
        /// 実機では フッターの Register ボタン / Enter で開く画面で、自動撮影では
        /// 入力できないため監査経路だけ API を公開する (ゲーム操作は変えない)。
        /// </summary>
        internal void OpenRegisterModalForAudit()
        {
            OpenModal();
        }

        /// <summary>
        /// 画面監査用: 段階表示を最終状態にし、自動で開く登録モーダルを抑止する。
        /// 撮影は「Result 本体」と「登録モーダル」の 2 枚を別々に撮るため、
        /// 撮影前に本体だけの状態を用意する。
        /// </summary>
        internal void PrepareForAudit()
        {
            CompleteReveal(false);
            if (modalOpen)
            {
                CloseModal();
            }
        }

        /// <summary>検証ハーネス用: 段階表示の経過秒。</summary>
        internal float RevealElapsedForAudit
        {
            get { return revealElapsed; }
        }

        /// <summary>検証ハーネス用: 段階表示が終わったか。</summary>
        internal bool RevealCompleteForAudit
        {
            get { return revealComplete; }
        }

        /// <summary>検証ハーネス用: 登録モーダルが開いているか。</summary>
        internal bool ModalOpenForAudit
        {
            get { return modalOpen; }
        }

        /// <summary>検証ハーネス用: トロフィーの不透明度 (生成前は -1)。</summary>
        internal float TrophyAlphaForAudit
        {
            get { return trophyGroup != null ? trophyGroup.alpha : -1f; }
        }

        /// <summary>監査用: 登録モーダルを閉じる。</summary>
        internal void CloseRegisterModalForAudit()
        {
            CloseModal();
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

            AdvanceReveal();

            if (modalOpen)
            {
                UpdateSubmitChip();
                HandleModalInput();
                return;
            }

            // 段階表示中の Space (= 決定) は「スキップ」(Issue #384)。残りの段を
            // すべて最終状態で表示する。スキップは登録モーダルを開かない
            // (押した本人は結果を見たいので、登録欄は Enter / Register で開く)。
            if (!revealComplete && KeyActions.IsDown(KeyActions.KeyAction.Confirm))
            {
                CompleteReveal(false);
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
                if (!gate.Succeeded)
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

        /// <summary>画面遷移は 1 回だけ受け付ける (Issue #426 レビュー指摘)。
        /// フッタの遅延操作とキーボード (R / Space) が競合しても、
        /// カウンタのリセットや Fade 要求が二重に走らないようにする。</summary>
        private bool transitionFired;

        private void Fire(Action callback)
        {
            if (transitionFired || callback == null)
            {
                return;
            }

            transitionFired = true;
            callback();
        }

        // --- 段階表示 (Issue #384) ---
        // 時間割は V6ResultRevealLogic (design 決定 §7.1) が持つ。ここは
        // その時刻を要素へ適用するだけにして、判定可能なロジックを純関数に保つ。

        private void AdvanceReveal()
        {
            if (revealComplete)
            {
                return;
            }

            // 隠れている間は進めない (シーン非表示中に演出を消費しない)。
            revealElapsed += Time.unscaledDeltaTime;
            ApplyReveal(revealElapsed);

            if (!registerShown && V6ResultRevealLogic.ShouldShowRegister(revealElapsed))
            {
                // 2000ms で登録欄 (05b モーダル) を出し、名前欄へフォーカスする
                // (design 決定 §7.1 / §7.3)。
                registerShown = true;
                OpenModal();
            }

            if (V6ResultRevealLogic.IsComplete(revealElapsed))
            {
                revealComplete = true;
            }
        }

        /// <summary>
        /// 段階表示を打ち切って全要素を最終状態で表示する。openRegister が false の
        /// ときは登録モーダルを自動で開かない (Space スキップ / 監査用)。
        /// </summary>
        private void CompleteReveal(bool openRegister)
        {
            revealElapsed = V6ResultRevealLogic.TotalSeconds;
            revealComplete = true;
            ApplyFinalRevealState();

            if (openRegister && !registerShown)
            {
                registerShown = true;
                OpenModal();
            }
            else if (!openRegister)
            {
                // スキップ後に自動で登録欄を開かない (以降の段はもう無い)。
                registerShown = true;
            }
        }

        private void ApplyReveal(float elapsed)
        {
            float trophyAlpha = V6ResultRevealLogic.Alpha(elapsed,
                V6ResultRevealLogic.TrophyStart, V6ResultRevealLogic.TrophyDuration);
            SetAlpha(trophyGroup, trophyAlpha);
            SetScale(trophyRect, V6ResultRevealLogic.TrophyScale(elapsed));

            SetAlpha(gradeGroup, V6ResultRevealLogic.Alpha(elapsed,
                V6ResultRevealLogic.GradeStart, V6ResultRevealLogic.GradeDuration));
            SetScale(gradeRect, V6ResultRevealLogic.GradeScale(elapsed));

            float scoreAlpha = V6ResultRevealLogic.Alpha(elapsed,
                V6ResultRevealLogic.ScoreLabelStart, V6ResultRevealLogic.ScoreLabelDuration);
            SetAlpha(scoreLabelGroup, scoreAlpha);
            SetAlpha(scoreValueGroup, scoreAlpha);
            scoreValue.text = V6ResultLogic.FormatScore(
                V6ResultRevealLogic.CountedScore(runScore, elapsed));

            float bestAlpha = V6ResultRevealLogic.Alpha(elapsed,
                V6ResultRevealLogic.BestStart, V6ResultRevealLogic.BestDuration);
            SetAlpha(bestLabelGroup, bestAlpha);
            SetAlpha(bestValueGroup, bestAlpha);

            for (int i = 0; i < judgeGroups.Length; i++)
            {
                SetAlpha(judgeGroups[i], V6ResultRevealLogic.JudgementAlpha(elapsed, i));
            }

            float gauge = V6ResultRevealLogic.GaugeProgress(elapsed);
            SetAlpha(gaugeGroup, gauge);
            float fill = V6ResultLogic.GaugeFillWidth(runScore);
            gaugeFill.rectTransform.sizeDelta = new Vector2(fill * gauge, GaugeTrackH);

            float lateAlpha = V6ResultRevealLogic.Alpha(elapsed,
                V6ResultRevealLogic.SongInfoStart, V6ResultRevealLogic.SongInfoDuration);
            for (int i = 0; i < lateGroups.Count; i++)
            {
                SetAlpha(lateGroups[i], lateAlpha);
            }
        }

        /// <summary>全要素を最終状態にする (演出の終点とスキップ後)。</summary>
        private void ApplyFinalRevealState()
        {
            SetAlpha(trophyGroup, 1f);
            SetScale(trophyRect, 1f);
            SetAlpha(gradeGroup, 1f);
            SetScale(gradeRect, 1f);
            SetAlpha(scoreLabelGroup, 1f);
            SetAlpha(scoreValueGroup, 1f);
            SetAlpha(bestLabelGroup, 1f);
            SetAlpha(bestValueGroup, 1f);
            for (int i = 0; i < judgeGroups.Length; i++)
            {
                SetAlpha(judgeGroups[i], 1f);
            }

            SetAlpha(gaugeGroup, 1f);
            gaugeFill.rectTransform.sizeDelta = new Vector2(V6ResultLogic.GaugeFillWidth(runScore), GaugeTrackH);
            for (int i = 0; i < lateGroups.Count; i++)
            {
                SetAlpha(lateGroups[i], 1f);
            }

            // カウントアップ途中の値を残さない (RefreshResult と同じ最終表示)。
            scoreValue.text = V6ResultLogic.FormatScore(runScore);
        }

        private static void SetAlpha(CanvasGroup group, float alpha)
        {
            if (group != null)
            {
                group.alpha = alpha;
            }
        }

        /// <summary>段階表示の対象に CanvasGroup を付け、非表示 (alpha 0) で作る。</summary>
        private static CanvasGroup FadeGroup(GameObject go)
        {
            var group = go.GetComponent<CanvasGroup>();
            if (group == null)
            {
                group = go.AddComponent<CanvasGroup>();
            }

            group.alpha = 0f;
            return group;
        }

        private static void SetScale(RectTransform rect, float scale)
        {
            if (rect != null)
            {
                rect.localScale = new Vector3(scale, scale, 1f);
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

        // --- ヘッダー (x24 y0 1680x96): RESULT (design の戻るゾーンは空) ---

        private void BuildHeader()
        {
            // 帯 + タイトルは共通部品 (Issue #385)。帯は Image 付きの透明パネル
            // (従来実装どおり panel=true)。design の戻るゾーンは空なので
            // 戻るボタンは置かない (戻るはフッターの OK / Esc キーが担う)。
            var header = V6Header.Create(frame.gameObject, true);

            // design zcSHh/biox4 の左上スロット (56×56) にトロフィーを置く。
            // 他画面ではここが戻るボタン (Esc) で、Result だけ空いていた。
            // 戻るはフッターの OK / Esc キーが担うので、ここは結果の記号に使う。
            var badge = V5Ui.Panel(header, "Trophy Badge",
                new Vector2(TrophyBadgeSize, TrophyBadgeSize), V5Tokens.Key4, 8f);
            At(badge, TrophyBadgeX, TrophyBadgeY);
            var trophyNode = V5Ui.Node(badge, "Icon");
            var nodeRt = trophyNode.GetComponent<RectTransform>();
            nodeRt.anchorMin = Vector2.zero;
            nodeRt.anchorMax = Vector2.one;
            nodeRt.offsetMin = Vector2.zero;
            nodeRt.offsetMax = Vector2.zero;
            trophyIcon = V6Icons.Create(trophyNode, "trophy", TrophyIconSize, V5Tokens.Text, "Glyph");
            if (trophyIcon != null)
            {
                var iconRt = trophyIcon.rectTransform;
                iconRt.anchorMin = new Vector2(0.5f, 0.5f);
                iconRt.anchorMax = new Vector2(0.5f, 0.5f);
                iconRt.pivot = new Vector2(0.5f, 0.5f);
                iconRt.sizeDelta = new Vector2(TrophyIconSize, TrophyIconSize);
                iconRt.anchoredPosition = Vector2.zero;
            }

            trophyGroup = FadeGroup(badge);
            trophyRect = badge.GetComponent<RectTransform>();

            // design zcSHh/aqq3q の Title (72,20) 400x56 fs38: 文字列は "RESULT"
            // (全画面のヘッダーは画面名を出す。Title 画面だけ "FlashBeat")。
            // 旧実装は pivot (0,0.5) で置いていたが、補正済み anchoredPosition の
            // 結果として矩形は (72,20) 400×56 で同一 — 共通部品の既定配置と一致する。
            V6Header.Title(header, "RESULT");
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

            // design (B7Ko8) は fill なし (背景パネルを敷かない)。以前あった可読性目的の
            // スクリーンは設計に無く、デザインとの差分になっていたので描かない。
            // トロフィーはヘッダーの左上スロットが持つ (BuildHeader)。
            var gradeBox = At(V5Ui.Node(block, "Grade"), 0f, 0f, GradeBoxW, GradeBoxH);
            gradeLetter = V5GameUi.Text(gradeBox.gameObject, "Letter", "–", 240f, V5Tokens.Text,
                TextAlignmentOptions.Center, FontStyles.Bold);
            Stretch(gradeLetter.GetComponent<RectTransform>(), GradeBoxW, GradeBoxH);

            var scoreLabel = V5GameUi.Text(block.gameObject, "Score Label", "SCORE", 20f, V5Tokens.TextDim);
            At(scoreLabel, ScoreColX, 48f, 400f, 26f, new Vector2(0f, 0.5f));

            scoreValue = OneLine(V5GameUi.Text(block.gameObject, "Score Value", "0", 72f, V5Tokens.Text,
                TextAlignmentOptions.Left, FontStyles.Normal));
            At(scoreValue, ScoreColX, 80f, 500f, 80f, new Vector2(0f, 0.5f));

            var bestLabel = V5GameUi.Text(block.gameObject, "Best Label", "前回最高", 20f, V5Tokens.TextDim);
            At(bestLabel, ScoreColX, 206f, 300f, 26f, new Vector2(0f, 0.5f));

            bestValue = OneLine(V5GameUi.Text(block.gameObject, "Best Value", "–", 32f, V5Tokens.Text));
            At(bestValue, ScoreColX, 232f, 400f, 40f, new Vector2(0f, 0.5f));

            // 段階表示 (#384): 各要素は CanvasGroup のアルファで出す。
            // トロフィーのグループはヘッダー側で作る (BuildHeader)。
            gradeGroup = FadeGroup(gradeBox);
            gradeRect = gradeBox.GetComponent<RectTransform>();
            scoreLabelGroup = FadeGroup(scoreLabel.gameObject);
            scoreValueGroup = FadeGroup(scoreValue.gameObject);
            bestLabelGroup = FadeGroup(bestLabel.gameObject);
            bestValueGroup = FadeGroup(bestValue.gameObject);
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

            gaugeGroup = FadeGroup(zone);
        }

        // 判定 (0,448) 840x140: Perfect / Great / Bad / Miss / Combo。

        private void BuildJudgements(GameObject main)
        {
            var block = At(V5Ui.Node(main, "Judgements"), 0f, JudgeY, 840f, JudgeCellH);

            // design (EpX3e) は fill なし。可読性目的のスクリーンは設計に無いため描かない。
            string[] labels = { "Perfect", "Great", "Bad", "Miss", "Combo" };
            for (int i = 0; i < labels.Length; i++)
            {
                var cell = At(V5Ui.Node(block, "J " + labels[i]), JudgeX[i], 0f, JudgeCellW, JudgeCellH);

                // design S5n3P: Label (0,0) 66×29 fs20 / Value (0,43) 83×70 fs48、
                // どちらも左上揃え。以前は VerticalLayoutGroup に任せていたため、
                // TMP の中央揃え (TextAlignmentOptions.Left) と子の既定サイズが
                // 混ざり、判定行だけ design より約 11px 下に描かれていた (r13 実機)。
                var judgeLabel = V5GameUi.Text(cell.gameObject, "Label", labels[i], 20f,
                    V5Tokens.TextDim, TextAlignmentOptions.TopLeft);
                At(judgeLabel, 0f, 0f, JudgeCellW, 29f, new Vector2(0f, 1f));
                judgeValues[i] = OneLine(V5GameUi.Text(cell.gameObject, "Value", "0", 48f, V5Tokens.Text,
                    TextAlignmentOptions.TopLeft, FontStyles.Normal));
                At(judgeValues[i], 0f, 43f, JudgeCellW, 70f, new Vector2(0f, 1f));
                judgeGroups[i] = FadeGroup(cell);
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

            // 曲名 / 作曲者はサムネイルの右 (x144) に左揃えで縦積み (design zcSHh:
            // Title (144,16) fs34 / Artist (144,66) fs23)。幅は Stat 列の左端 x348 まで (204)。
            // 実データは design のサンプルより長いので 1 行固定 + ellipsis。
            songTitle = V5GameUi.Text(panel.gameObject, "Title", "", 34f, V5Tokens.Text,
                TextAlignmentOptions.Left, FontStyles.Normal);
            At(songTitle, V6ResultLogic.SongTitleX, V6ResultLogic.SongTitleY,
                V6ResultLogic.SongTitleW, V6ResultLogic.SongTitleH, new Vector2(0f, 0.5f));
            songTitle.textWrappingMode = TextWrappingModes.NoWrap;
            songTitle.overflowMode = TextOverflowModes.Ellipsis;
            // design h36 < NotoSansJP の行送り (fs34 × 1.448 = 49.2) のため、
            // 何もしないと Ellipsis が 0 行になり曲名が消える (r3 実機で発生)。
            V5Ui.FitEllipsisToLineHeight(songTitle);

            songArtist = V5GameUi.Text(panel.gameObject, "Artist", "", 23f, V5Tokens.TextMuted);
            At(songArtist, V6ResultLogic.SongArtistX, V6ResultLogic.SongArtistY,
                V6ResultLogic.SongArtistW, V6ResultLogic.SongArtistH, new Vector2(0f, 0.5f));
            songArtist.textWrappingMode = TextWrappingModes.NoWrap;
            songArtist.overflowMode = TextOverflowModes.Ellipsis;
            V5Ui.FitEllipsisToLineHeight(songArtist);

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
                songStatValues[i] = OneLine(V5GameUi.Text(cell.gameObject, "Value", "–", 30f, V5Tokens.Text,
                    TextAlignmentOptions.Left, FontStyles.Normal));
                At(songStatValues[i], 0f, V6ResultLogic.SongStatValueY,
                    V6ResultLogic.SongStatW, V6ResultLogic.SongStatValueH);
            }

            lateGroups.Add(FadeGroup(panel));
        }

        // 曲別ランキング (856,24) 528x412: Head + 曲名 + 6 行。

        private void BuildSongRanking(GameObject main)
        {
            var panel = At(V5Ui.Panel(main.gameObject, "Song Ranking", new Vector2(528f, SongRankH),
                V5Tokens.Key2, 8f), V6ResultLogic.RankX, SongRankY);

            var head = At(V5Ui.Node(panel.gameObject, "Head"), V6ResultLogic.RankHeadX, V6ResultLogic.RankHeadY,
                V6ResultLogic.RankHeadW, V6ResultLogic.RankHeadH);
            var headTitle = V5GameUi.Text(head.gameObject, "Title", "曲別ランキング", 18f, V5Tokens.TextDim);
            // design d6Noy (12,5) 126×26。Spacer (150,17.5) 350×1 は fill 無しのレイアウト用で描かない。
            At(headTitle, 12f, 5f, V6ResultLogic.RankHeadTitleW, V6ResultLogic.RankHeadTitleH);

            // design Lo2pD「終焉逃避行」(20,52) 150×43 fs30。150 は 5 文字サンプルの hug 幅なので、
            // 実データ (最長 12 文字) は行幅いっぱいの 484 まで使い、はみ出す分だけ … で省略する。
            songRankTitle = V5GameUi.Text(panel.gameObject, "Song", "", 30f, V5Tokens.Text);
            At(songRankTitle, V6ResultLogic.SongRankTitleX, V6ResultLogic.SongRankTitleY,
                V6ResultLogic.SongRankTitleW, V6ResultLogic.SongRankTitleH);
            songRankTitle.textWrappingMode = TextWrappingModes.NoWrap;
            songRankTitle.overflowMode = TextOverflowModes.Ellipsis;
            V5Ui.FitEllipsisToLineHeight(songRankTitle);

            // 空状態は design に無い実装固有の表示。Song 見出し (y52..95) と重ならないよう
            // 行領域 (y92..404) の縦中央に置く。
            songRankEmpty = V5GameUi.Text(panel.gameObject, "No Data", "", 24f, V5Tokens.TextDim);
            At(songRankEmpty, 20f, 226f, 496f, 44f, new Vector2(0f, 0.5f));

            songRankRows = V5Ui.Node(panel.gameObject, "Rows").GetComponent<RectTransform>();
            At(songRankRows, 0f, 0f, 528f, SongRankH);

            lateGroups.Add(FadeGroup(panel));
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

            lateGroups.Add(FadeGroup(panel));
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

            // キー凡例 (左下, design zcSHh Zone · キー操作: y30 / 項目間 gap24 /
            // 項内 gap8 / KeyCap h32 pad[0,8])。共通部品 V6Legend が TMP 実測で幅を詰める
            // (design 実測の項目幅 127 / 124 / 131、x = 0 / 151 / 299)。
            float lx = 0f;
            for (int i = 0; i < V6ResultLogic.LegendKeys.Length; i++)
            {
                lx = Common.V6Legend.Item(footer, lx, V6ResultLogic.FooterLegendY,
                    V6ResultLogic.LegendKeys[i], V6ResultLogic.LegendLabels[i]);
            }

            // 右下: Retry (secondary) / Register (secondary) / OK (primary) を
            // フッター右端から gap16 で横並び。幅は正本 zcSHh の固定値
            // (Retry 220 / Register 273 / OK 273) を使う。内容から逆算すると
            // Latin の送り幅推定が広く出て design より横に伸びる。
            float[] widths =
            {
                V6ResultLogic.FooterOkButtonW,
                V6ResultLogic.FooterRegisterButtonW,
                V6ResultLogic.FooterRetryButtonW,
            };
            float[] xs = V6ResultLogic.FooterButtonXs(widths);
            // icon は design の lucide 名 (check / save / rotate-ccw)。
            // 画面遷移は Fire 経由にして、キーボード (R / Space) との二重発火を防ぐ。
            BuildFooterButton(footer, xs[0], "Op OK", "OK", "check", "Space", true, widths[0], () => Fire(onSelect));
            BuildFooterButton(footer, xs[1], "Op Register", "Register", "save", "Enter", false, widths[1], OpenModal);
            BuildFooterButton(footer, xs[2], "Op Retry", "Retry", "rotate-ccw", "R", false, widths[2], () => Fire(onRetry));
        }

        /// <summary>
        /// フッター右のボタンを design 座標 (左上 origin, y20 h56) で置く。
        /// 構造は契約 §4 の共通ボタン (正本 Prefab/Button Primary/Secondary:
        /// icon 32 / Label fs30 / Cap h32 / 中身は中央揃え) を共通部品で作る。
        /// </summary>
        private void BuildFooterButton(GameObject footer, float x, string name, string label, string iconSprite,
            string keyCap, bool primary, float width, Action onClick)
        {
            Vector2 topLeft = new Vector2(x, V6ResultLogic.FooterButtonY);
            Vector2 size = new Vector2(width, V6ResultLogic.FooterButtonH);
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
                // フッタの各ボタンは画面遷移かモーダルを伴う。リングが
                // 見えるだけ遅らせても操作感は損なわれないので一律で遅延する。
                button.onClick.AddListener(() => V6ButtonPressFeedback.RunAfterFeedback(go, onClick));
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

            // design YwPw9 の全寸法を Dialog 左上基準で写す。At(...) の既定 pivot (0,1)
            // で矩形の左上が design 座標に来る (pivot 付き版は使わない)。
            // Head (28,28) 180×43 fs30 / Name Prompt (404,38) 380×29 fs20 (右寄せ)。
            modalHead = OneLine(V5GameUi.Text(dialog.gameObject, "Head", "スコアを登録", 30f, V5Tokens.Text,
                TextAlignmentOptions.Left, FontStyles.Bold));
            At(modalHead, 28f, 28f, 180f, 43f);

            var prompt = OneLine(V5GameUi.Text(dialog.gameObject, "Name Prompt", "プレイヤー名を入力↓", 20f,
                V5Tokens.TextMuted, TextAlignmentOptions.Right));
            At(prompt, 404f, 38f, 380f, 29f);
            modalNamePrompt = prompt;

            // Name Field (28,112) 784×84 $k0 r8 / Value (24,20) 83×58 fs40 $text-dim。
            var field = At(V5Ui.Panel(dialog.gameObject, "Name Field", new Vector2(784f, 84f), V5Tokens.Key0, 8f),
                28f, 112f);
            // design lqHaN の Name Field は空のとき "YOU" を $text-dim で出す
            // (v5 ResultView の Placeholder と同じ既定名)。入力すると $text になる。
            modalNameValue = OneLine(V5GameUi.Text(field.gameObject, "Value", string.Empty, 40f,
                V5Tokens.Text, TextAlignmentOptions.Left, FontStyles.Bold));
            // Issue #425: 文字が枠の左端に張り付いて見えるため、内側余白を広げる。
            At(modalNameValue, 32f, 20f, 728f, 58f);
            modalNamePlaceholder = OneLine(V5GameUi.Text(field.gameObject, "Placeholder",
                V6ResultLogic.LocalNameDefault, 40f, V5Tokens.TextDim,
                TextAlignmentOptions.Left, FontStyles.Bold));
            At(modalNamePlaceholder, 32f, 20f, 728f, 58f);

            // 名前入力は TMP_InputField に任せる (Issue #384)。自前の Input.inputString
            // 読み取りはレガシー Input に IME の確定文字が来ないため、日本語の名前を
            // 入力できず、カーソルも描けなかった。TMP_InputField は IME 変換・点滅
            // カーソル・フォーカス喪失 (確定) をまとめて持つ。
            modalNameInput = field.AddComponent<TMP_InputField>();
            modalNameInput.targetGraphic = field.GetComponent<Image>();
            modalNameInput.textViewport = field.GetComponent<RectTransform>();
            modalNameInput.textComponent = modalNameValue;
            modalNameInput.placeholder = modalNamePlaceholder;
            modalNameInput.lineType = TMP_InputField.LineType.SingleLine;
            modalNameInput.contentType = TMP_InputField.ContentType.Standard; // IME 可
            modalNameInput.caretWidth = 2;
            // design は点滅の半周期 530ms。TMP は 1/rate 秒ごとに表示を反転する。
            modalNameInput.caretBlinkRate = 1f / V6ResultRevealLogic.CaretHalfCycleSeconds;
            modalNameInput.customCaretColor = true;
            modalNameInput.caretColor = V5Tokens.Text;
            modalNameInput.selectionColor = new Color(1f, 1f, 1f, 0.2f);
            // 保存名が入っている欄は「全選択」状態で開く (Issue #425)。
            // 何も打たずに Enter なら保存名がそのまま確定し、何か入力すれば
            // 選択範囲が置き換わって保存名の末尾へ追記される事故を防ぐ。
            // Esc で閉じたときに TMP が元の文字列へ戻すと、確定扱いの onEndEdit が
            // 古い名前を送ってしまうため、復元も切る。
            modalNameInput.onFocusSelectAll = false;
            modalNameInput.restoreOriginalTextOnEscape = false;
            modalNameInput.onValidateInput = ValidateNameChar;
            modalNameInput.onEndEdit.AddListener(OnNameEditEnd);

            // Input Hints (28,208) 784×32:
            // Hint Cap (0,0) 104×32 (pad [0,12], Key fs20) / Hint Text (128,4.5) fs16 /
            // Legend · 移動 (509,3) = KeyCap 57×26 + Label fs18 (65,0) /
            // Legend · 削除 (634,3) = KeyCap 106×26 + Label fs18 (114,0)。
            var hints = At(V5Ui.Node(dialog.gameObject, "Input Hints"), 28f, 208f, 784f, 32f);
            var cap = At(V5Ui.Panel(hints, "Hint Cap", new Vector2(104f, 32f), V5Tokens.Key4, 4f), 0f, 0f);
            var capText = OneLine(V5GameUi.Text(cap, "Key", "A-Z 0-9", 20f, V5Tokens.TextMuted,
                TextAlignmentOptions.Left));
            At(capText, 12f, 1.5f, 80f, 29f);
            modalNameHint = OneLine(V5GameUi.Text(hints, "Hint Text",
                "最大" + RankingClient.NicknameMaxLength + "文字", 16f, V5Tokens.TextMuted));
            At(modalNameHint, 128f, 4.5f, 200f, 23f);
            BuildHintLegend(hints, 509f, 57f, "← →", "移動");
            BuildHintLegend(hints, 634f, 106f, "Backspace", "削除");

            // Summary (28,264) 784×140 $k2 r8。
            var summary = At(V5Ui.Panel(dialog.gameObject, "Summary", new Vector2(784f, 140f), V5Tokens.Key2, 8f),
                28f, 264f);
            modalThumb = At(V5Ui.Panel(summary, "Thumb", new Vector2(88f, 50f), V5Tokens.Key3, 4f),
                16f, 20f).GetComponent<Image>();
            // Title (120,18) h38 fs26 / Artist (120,52) h26 fs18。design の hug 幅
            // (131 / 126) はサンプル曲名の幅なので、実データが長くても Grade (x704)
            // に届かない幅 568 まで広げ、1 行固定にする。
            modalSongTitle = OneLine(V5GameUi.Text(summary, "Title", "", 26f, V5Tokens.Text));
            At(modalSongTitle, 120f, 18f, 568f, 38f);
            modalSongTitle.overflowMode = TextOverflowModes.Ellipsis;
            modalSongArtist = OneLine(V5GameUi.Text(summary, "Artist", "", 18f, V5Tokens.TextMuted));
            At(modalSongArtist, 120f, 52f, 568f, 26f);
            modalSongArtist.overflowMode = TextOverflowModes.Ellipsis;
            // fs18×h26 は行送り 26.06 を 0.06px 下回るので、Ellipsis が 0 行に
            // ならないよう実効高を広げる (TMP Ellipsis の共通契約 #354)。
            V5Ui.FitEllipsisToLineHeight(modalSongArtist, 26f);
            // Grade (704,38) 64×40 $k4 r4 / GradeText fs24 centered。
            var gradeChip = At(V5Ui.Panel(summary, "Grade", new Vector2(64f, 40f), V5Tokens.Key4, 4f),
                704f, 38f);
            modalGrade = V5GameUi.Text(gradeChip, "GradeText", "–", 24f, V5Tokens.Text,
                TextAlignmentOptions.Center, FontStyles.Normal);
            Stretch(modalGrade.GetComponent<RectTransform>(), 64f, 40f);
            At(V5Ui.Panel(summary, "Divider", new Vector2(752f, 1f), FromHex("#FFFFFF1F"), 0f), 16f, 94f);
            // Record Label (16,104) h23 fs16 $text-dim / Record Value (440,102)
            // 180×29 fs20 右寄せ / Record Note (640,104) 113×23 fs16 $text-dim。
            modalRecordLabel = OneLine(V5GameUi.Text(summary, "Record Label", "", 16f, V5Tokens.TextDim));
            At(modalRecordLabel, 16f, 104f, 300f, 23f);
            modalRecordValue = OneLine(V5GameUi.Text(summary, "Record Value", "", 20f, V5Tokens.Text,
                TextAlignmentOptions.Right));
            At(modalRecordValue, 440f, 102f, 180f, 29f);
            modalRecordNote = OneLine(V5GameUi.Text(summary, "Record Note", "", 16f, V5Tokens.TextDim));
            At(modalRecordNote, 640f, 104f, 113f, 23f);
            // Score Group (400,26) 288×52:
            // Score Label (91,14.5) 51×23 fs16 / Score Value (156,1.5) 132×49 fs34。
            // 正本の text ノードは alignment 未指定 = 既定 (左)。design 画像の実測でも
            // 値のインクは 157..285 (箱 156..288) で左詰め。右寄せにすると 0 のとき
            // SCORE から離れて Grade の手前へ寄るため、design どおり左にする。
            // 横並びの相対配置ではなく design の実座標に置く (旧 Row 実装では
            // 子の矩形幅が 0 のまま詰められ、SCORE と数値が design と違う位置に出た)。
            var scoreGroup = At(V5Ui.Node(summary, "Score Group"), 400f, 26f, 288f, 52f);
            OneLine(V5GameUi.Text(scoreGroup, "Score Label", "SCORE", 16f, V5Tokens.TextDim));
            At(scoreGroup.transform.Find("Score Label"), 91f, 14.5f, 51f, 23f);
            modalScoreValue = OneLine(V5GameUi.Text(scoreGroup, "Score Value", "0", 34f, V5Tokens.Text,
                TextAlignmentOptions.Left, FontStyles.Bold));
            At(modalScoreValue, 156f, 1.5f, 132f, 49f);

            // Buttons (28,432) 784×50:
            // Skip (318,0) 198×50 / Register (532,0) 252×50。旧 Row は左詰めに
            // していたため design より 318px 左に寄っていた。
            var buttons = At(V5Ui.Node(dialog.gameObject, "Buttons"), 28f, 432f, 784f, 50f);
            var skip = At(Button(buttons, "Button Skip", 198f, 50f, 8f, V5Tokens.Key4, CloseModal), 318f, 0f);
            // Button · Skip: Icon (24,13) 24 / Label (60,6) 54×38 fs26 /
            // KeyCap (126,10.5) 48×29 pad [0,8] Key fs20。
            var skipIcon = V6Icons.Create(skip, "x", 24f, V5Tokens.TextMuted, "Icon");
            PlaceTopLeft(skipIcon.gameObject, 24f, 13f, 24f, 24f);
            OneLine(V5GameUi.Text(skip, "Label", "Skip", 26f, V5Tokens.Text, TextAlignmentOptions.Left));
            At(skip.transform.Find("Label"), 60f, 6f, 54f, 38f);
            BuildModalKeyCap(skip, 126f, 10.5f, 48f, "Esc", FromHex("#00000040"), V5Tokens.TextMuted, 20f);
            var register = At(Button(buttons, "Button Register", 252f, 50f, 8f, V5Tokens.ButtonPrimaryBg,
                TrySubmitFromButton), 532f, 0f);
            // Button · Register: Icon (18.5,13) 24#1B1B1B / Label (54.5,6) 101×38 fs26 /
            // KeyCap (167.5,10.5) 66×29 #00000026 Key fs20 #1B1B1B。
            var registerIcon = V6Icons.Create(register, "check", 24f, V5Tokens.ButtonPrimaryLabel, "Icon");
            PlaceTopLeft(registerIcon.gameObject, 18.5f, 13f, 24f, 24f);
            modalRegisterLabel = OneLine(V5GameUi.Text(register, "Label", "Register", 26f,
                V5Tokens.ButtonPrimaryLabel, TextAlignmentOptions.Left, FontStyles.Normal));
            At(modalRegisterLabel, 54.5f, 6f, 101f, 38f);
            BuildModalKeyCap(register, 167.5f, 10.5f, 66f, "Enter", FromHex("#00000026"),
                V5Tokens.ButtonPrimaryLabel, 20f);
            modalRegisterButton = register.GetComponent<Button>();

            // 状態メッセージ (loading / success / duplicate / offline / server error)。
            // design に無い実装都合の行なので Head/Prompt と重ならない位置 (右側 y74) に置く。
            modalStatus = OneLine(V5GameUi.Text(dialog.gameObject, "Status", "", 17f, V5Tokens.TextMuted,
                TextAlignmentOptions.Left));
            At(modalStatus, 220f, 74f, 592f, 26f);

            // 登録チップ (design TYshI 272×32)。成功は $ok-tint / $ok-light、
            // 失敗は $danger-tint / $danger-light。Head の右に出し、表示中は
            // Name Prompt を隠す (同じ位置に重なるため)。
            modalChip = At(V5Ui.Panel(dialog.gameObject, "Status Chip", new Vector2(272f, 32f),
                V5Tokens.OkTint, 4f), 540f, 28f);
            modalChipBackground = modalChip.GetComponent<Image>();
            modalChipGroup = modalChip.AddComponent<CanvasGroup>();
            modalChipLabel = V5GameUi.Text(modalChip, "Label", "", 16f, V5Tokens.OkLight,
                TextAlignmentOptions.Center, FontStyles.Bold);
            Stretch(modalChipLabel.GetComponent<RectTransform>(), 272f, 32f);
            modalChip.SetActive(false);

            modal.SetActive(false);
        }

        /// <summary>
        /// design YwPw9 の凡例 (Legend): KeyCap h26 / pad [0,8] / Key fs18 +
        /// Label fs18 (capW + 8 の位置)。x は Input Hints 左端からの design 座標。
        /// </summary>
        private void BuildHintLegend(GameObject parent, float x, float capW, string key, string label)
        {
            var item = At(V5Ui.Node(parent, "Legend " + label), x, 3f, capW + 8f + 36f, 26f);
            var cap = At(V5Ui.Panel(item, "KeyCap", new Vector2(capW, 26f), V5Tokens.Key4, 4f), 0f, 0f);
            var keyText = OneLine(V5GameUi.Text(cap, "Key", key, 18f, V5Tokens.TextMuted,
                TextAlignmentOptions.Left));
            At(keyText, 8f, 0f, capW - 16f, 26f);
            var labelText = OneLine(V5GameUi.Text(item, "Label", label, 18f, V5Tokens.TextMuted,
                TextAlignmentOptions.Left));
            At(labelText, capW + 8f, 0f, 36f, 26f);
        }

        /// <summary>
        /// design のモーダル ボタン内 KeyCap (h29 / pad [0,8] / Key fs20)。
        /// 位置を design 座標で受け取る (旧実装は Row の中央寄せに頼っていた)。
        /// </summary>
        private void BuildModalKeyCap(GameObject parent, float x, float y, float w, string key,
            Color fill, Color keyColor, float fontSize)
        {
            var cap = At(V5Ui.Panel(parent, "KeyCap", new Vector2(w, 29f), fill, 4f), x, y);
            var capText = OneLine(V5GameUi.Text(cap, "Key", key, fontSize, keyColor,
                TextAlignmentOptions.Center));
            Stretch(capText.GetComponent<RectTransform>(), w, 29f);
        }

        private void OpenModal()
        {
            if (modal == null || modalOpen)
            {
                return;
            }

            modalOpen = true;
            submitOutcome = V6ResultLogic.SubmitOutcome.Pending;

            // 登録の方式 (送信対象か) を確定する。曲情報はプレイ開始時に固定した
            // (pinned) 版を使う (GManager.GetSong が ResultScene では snapshot を優先)。
            registrationMode = CurrentRegistrationMode();
            modalNameInput.characterLimit = NameMaxLength();

            // 初期値: オンラインはオンライン側の保存名、無ければ端末内の保存名。
            // ローカルは端末内の保存名 (v5 と同じ PlayerPrefs キー)。
            string saved = V6ResultLogic.InitialName(
                registrationMode, RankingClient.LoadNickname(), V6LocalNameStore.Load());
            modalNameInput.SetTextWithoutNotify(saved ?? string.Empty);

            ApplyRegistrationModeTexts();
            HideSubmitChip();

            // 旧オーバーレイ (ResultRankingOverlay) は Result シーンで自動で
            // モーダルを開き、キーも処理する。二重モーダルと二重入力を防ぐため
            // v6 モーダル表示中は停止し、閉じたら戻す (ソースは編集しない)。
            SuspendLegacyOverlay();

            RefreshModal();
            SetModalStatus(gate.Failed ? SubmitStatusText() : "",
                gate.Failed ? FromHex("#FF9999") : V5Tokens.TextMuted);
            modal.SetActive(true);
            FocusNameField();
        }

        private void CloseModal()
        {
            if (modal == null)
            {
                return;
            }

            modalOpen = false;

            // 閉じる操作で onEndEdit (フォーカス喪失 = 確定) が走り、意図しない
            // 自動登録を誘発するのを防ぐ。
            suppressNameCommit = true;
            UnfocusNameField();
            suppressNameCommit = false;

            // 応答待ちの送信は破棄しない (閉じてもコルーチンは完了させる)。
            // gate の送信中フラグも落とさない (落とすと再送できてしまう)。
            SetRegisterInteractable(true);
            HideSubmitChip();
            modal.SetActive(false);
            ResumeLegacyOverlay();
            FlashBeat.UI.SceneInputGate.BlockThisFrame();
        }

        // --- 名前入力 (TMP_InputField) ---

        private void FocusNameField()
        {
            if (modalNameInput == null)
            {
                return;
            }

            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(modalNameInput.gameObject);
            }

            modalNameInput.ActivateInputField();
            // Issue #425: 文末カーソルではなく全選択で開く。入力すると保存名が
            // 丸ごと置き換わり、未入力の Enter は保存名のまま確定する。
            // caret を先に置き、選択範囲を最後に設定する (逆順だと選択が潰れる)。
            modalNameInput.caretPosition = modalNameInput.text.Length;
            modalNameInput.selectionAnchorPosition = 0;
            modalNameInput.selectionFocusPosition = modalNameInput.text.Length;
        }

        private void UnfocusNameField()
        {
            if (modalNameInput == null)
            {
                return;
            }

            if (modalNameInput.isFocused)
            {
                modalNameInput.DeactivateInputField();
            }

            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == modalNameInput.gameObject)
            {
                EventSystem.current.SetSelectedGameObject(null);
            }
        }

        /// <summary>
        /// 入力 1 文字の検証。端末内保存は v5 と同じ A-Z 0-9 だけを受ける
        /// (オンラインは日本語も含めて自由)。
        /// </summary>
        private char ValidateNameChar(string text, int charIndex, char addedChar)
        {
            if (registrationMode == V6RegistrationMode.Local && !IsLocalNameChar(addedChar))
            {
                return '\0';
            }

            return addedChar;
        }

        /// <summary>
        /// Enter / フォーカス喪失で名前が確定した (Issue #384)。空でなければ
        /// 自動で登録する。送信中・登録済みは gate が弾く。
        /// </summary>
        /// <remarks>
        /// Esc で取り消した編集では送信しない (PR #387 レビュー指摘)。
        /// TMP_InputField は Esc で <c>m_WasCanceled</c> を立ててから
        /// <c>ReleaseSelection</c> 経由で <c>onEndEdit</c> を通知する。ここで
        /// <c>wasCanceled</c> を見ないと、保存済みの名前が入力欄に戻った状態で
        /// 自動登録が走ってしまう (「取り消したのに登録された」)。
        /// </remarks>
        private void OnNameEditEnd(string value)
        {
            if (suppressNameCommit || !modalOpen)
            {
                return;
            }

            if (modalNameInput != null && modalNameInput.wasCanceled)
            {
                return; // Esc で取り消した編集は確定しない
            }

            string name = (value ?? string.Empty).Trim();
            if (name.Length == 0)
            {
                return; // 空は確定しない (プレースホルダのまま)
            }

            TrySubmit();
        }

        // --- 登録送信 (Issue #240) ---

        /// <summary>ボタン経由の送信。送信中と登録済みは無視する (二重送信防止)。</summary>
        private void TrySubmitFromButton()
        {
            TrySubmit();
        }

        private void TrySubmit()
        {
            if (!gate.CanStart)
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
                modalNameInput.text, RankingClient.NicknameMaxLength);
            if (error != V6ResultLogic.NicknameError.None)
            {
                SetModalStatus(NicknameErrorMessage(error), FromHex("#FFD64D"));
                return;
            }

            if (!gate.Begin())
            {
                return;
            }

            submitOutcome = V6ResultLogic.SubmitOutcome.Pending;
            SetModalStatus(V6ResultLogic.SubmitStatusMessage(V6ResultLogic.SubmitOutcome.Pending, 0),
                FromHex("#99E5FF"));
            SetRegisterInteractable(false);
            // 送った名前を控える。応答が返るまでに入力欄が編集されても、
            // 保存するのは実際に送った名前 (送った後に書いた文字ではない)。
            submittedNickname = modalNameInput.text.Trim();
            RunCoroutine(SubmitCoroutine(submittedNickname));
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
            string saved = V6ResultLogic.NormalizeLocalName(modalNameInput.text);
            V6LocalNameStore.Save(saved);
            gate.Succeed();

            // 入力欄を正規化済みの保存名にそろえる (onEndEdit は発火させない)。
            suppressNameCommit = true;
            modalNameInput.SetTextWithoutNotify(saved);
            suppressNameCommit = false;

            // 保存済み。Register ボタンで二重保存しない (Esc / Skip で閉じる)。
            SetRegisterInteractable(false);
            SetModalStatus(V6ResultLogic.LocalSaveMessage(saved), FromHex("#99E5FF"));
            ShowSubmitChip("保存しました", true);
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
            if (!gate.InFlight)
            {
                return; // 閉じたあとの応答は捨てる
            }

            lastSubmitHttpCode = result != null ? result.httpCode : 0;
            submitOutcome = V6ResultLogic.ClassifySubmitResult(
                result != null && result.ok, lastSubmitHttpCode,
                result != null ? result.error : null);

            if (submitOutcome == V6ResultLogic.SubmitOutcome.Success
                && result.score != null && result.score.rank > 0)
            {
                // 偽の成功を出さない: 順位はサーバ応答からのみ。
                RankingClient.SaveNickname(submittedNickname);
                gate.Succeed();
                myRank = result.score.rank;
                SetRegisterInteractable(false);
                SetModalStatus("", V5Tokens.TextMuted);
                ShowSubmitChip("登録しました", true);
                // 登録後の順位 (自分の行のハイライト) は再取得したランキングから。
                StartRankingFetch();
                return;
            }

            if (submitOutcome == V6ResultLogic.SubmitOutcome.Duplicate)
            {
                // 同じ内容が既に届いている (429 の 10 秒窓)。登録済みとして扱い、
                // 再送は許さない。
                gate.Succeed();
                SetRegisterInteractable(false);
                SetModalStatus("", V5Tokens.TextMuted);
                ShowSubmitChip("登録ずみです", true);
                return;
            }

            // 失敗: その事実を出して再送を許す (Esc = スキップ)。
            gate.Fail();
            SetRegisterInteractable(true);
            if (modalRegisterLabel != null)
            {
                modalRegisterLabel.text = "再試行";
            }

            SetModalStatus(V6ResultLogic.SubmitStatusMessage(submitOutcome, lastSubmitHttpCode),
                FromHex("#FF9999"));
            ShowSubmitChip("登録できませんでした", false);
        }

        private string SubmitStatusText()
        {
            return V6ResultLogic.SubmitStatusMessage(submitOutcome, lastSubmitHttpCode);
        }

        // --- 登録チップ (Issue #384) ---

        private void ShowSubmitChip(string message, bool success)
        {
            if (modalChip == null)
            {
                return;
            }

            modalChipLabel.text = message;
            modalChipLabel.color = success ? V5Tokens.OkLight : V5Tokens.DangerLight;
            modalChipBackground.color = success ? V5Tokens.OkTint : V5Tokens.DangerTint;
            modalChipGroup.alpha = 0f;
            modalChip.SetActive(true);
            if (modalNamePrompt != null)
            {
                modalNamePrompt.gameObject.SetActive(false);
            }

            chipActive = true;
            chipShownAt = Time.unscaledTime;
            // 成功はチップが消えたあとモーダルを閉じる (結果画面へ戻る)。
            // 失敗は再試行できるようモーダルを残す。
            chipClosesModal = success;
        }

        private void HideSubmitChip()
        {
            chipActive = false;
            if (modalChip != null)
            {
                modalChip.SetActive(false);
            }

            if (modalNamePrompt != null)
            {
                modalNamePrompt.gameObject.SetActive(true);
            }
        }

        private void UpdateSubmitChip()
        {
            if (!chipActive || modalChip == null)
            {
                return;
            }

            float elapsed = Time.unscaledTime - chipShownAt;
            float fadeIn = V6ResultRevealLogic.ChipFadeInSeconds;
            float hold = V6ResultRevealLogic.ChipHoldSeconds;
            float fadeOut = V6ResultRevealLogic.ChipFadeOutSeconds;

            if (elapsed < fadeIn)
            {
                modalChipGroup.alpha = Mathf.Clamp01(elapsed / fadeIn);
            }
            else if (elapsed < fadeIn + hold)
            {
                modalChipGroup.alpha = 1f;
            }
            else if (elapsed < fadeIn + hold + fadeOut)
            {
                modalChipGroup.alpha = Mathf.Clamp01(1f - (elapsed - fadeIn - hold) / fadeOut);
            }
            else
            {
                bool close = chipClosesModal;
                HideSubmitChip();
                if (close)
                {
                    CloseModal();
                }
            }
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
            if (gate.InFlight)
            {
                return; // 応答待ち。Esc で中断はしない (二重送信を防ぐ)。
            }

            // Esc だけを画面の操作として扱い、それ以外のキーは入力欄へ渡す
            // (design 決定 §7.3: 入力欄がフォーカス中は Esc 以外を奪わない)。
            // 文字入力・Backspace・Enter・矢印移動は TMP_InputField が処理する。
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                CloseModal();
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
                if (songRankTitle != null)
                {
                    songRankTitle.text = "";
                }

                for (int i = 0; i < songStatValues.Length; i++)
                {
                    songStatValues[i].text = "–";
                }

                return;
            }

            songTitle.text = song.title ?? "";
            songArtist.text = song.musician ?? "";
            if (songRankTitle != null)
            {
                songRankTitle.text = song.title ?? "";
            }
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
            // Issue #415: 曲別ランキング行はスロット単位で既存オブジェクトを再利用し、
            // 行データの差分だけを既存 Image/TMP に適用する (全行破棄再生成はやめる)。
            int rows = Mathf.Min(rankingRows.Count, SongRankVisibleRows);
            for (int slot = 0; slot < SongRankVisibleRows; slot++)
            {
                bool valid = slot < rows;
                float y = 92f + RankRowStep * slot;
                GameObject row = slot < songRankRowObjects.Count ? songRankRowObjects[slot] : null;
                if (!valid)
                {
                    if (row != null)
                    {
                        row.transform.SetParent(null, false);
                        V5GameUi.DestroyNow(row);
                        songRankRowObjects[slot] = null;
                    }

                    continue;
                }

                var rowData = rankingRows[slot];
                if (row == null)
                {
                    row = BuildRankingRow(songRankRows.gameObject, rowData, y);
                    if (slot < songRankRowObjects.Count) songRankRowObjects[slot] = row;
                    else songRankRowObjects.Add(row);
                }
                else
                {
                    UpdateRankingRow(row, rowData, y);
                }
            }

            while (songRankRowObjects.Count > SongRankVisibleRows)
            {
                int last = songRankRowObjects.Count - 1;
                GameObject tail = songRankRowObjects[last];
                if (tail != null)
                {
                    tail.transform.SetParent(null, false);
                    V5GameUi.DestroyNow(tail);
                }

                songRankRowObjects.RemoveAt(last);
            }
        }

        /// <summary>既存のランキング行を新しい行データで書き換える (行オブジェクトは再利用)。</summary>
        private static void UpdateRankingRow(GameObject row, FlashBeat.UI.V6.Select.V6RankingRow rowData, float y)
        {
            var rt = row.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchoredPosition = V6Layout.ToLocal(new Vector2(8f, y));
            }

            var bg = row.GetComponent<Image>();
            if (bg != null)
            {
                bg.color = rowData.IsYou ? V6Tokens.Key6 : V6Tokens.Key1;
            }

            SetChildText(row, "Pos/PosText", rowData.Position.ToString());
            SetChildText(row, "Name", rowData.IsYou ? "YOU" : rowData.PlayerName);
            SetChildText(row, "Value", V6ResultLogic.FormatScore(rowData.Score));
            SetChildText(row, "Grade/GradeText", string.IsNullOrEmpty(rowData.Grade) ? "–" : rowData.Grade);

            Transform posText = row.transform.Find("Pos/PosText");
            var posTmp = posText != null ? posText.GetComponent<TextMeshProUGUI>() : null;
            if (posTmp != null)
            {
                posTmp.color = rowData.IsYou ? V6Tokens.Text : V6Tokens.TextMuted;
            }
        }

        private static void SetChildText(GameObject root, string path, string value)
        {
            Transform t = root != null ? root.transform.Find(path) : null;
            var tmp = t != null ? t.GetComponent<TextMeshProUGUI>() : null;
            if (tmp != null)
            {
                tmp.text = value;
            }
        }

        private GameObject BuildRankingRow(GameObject parent, FlashBeat.UI.V6.Select.V6RankingRow row, float y)
        {
            // 行内部は共通部品 (design Chlqd 496×44) が持つ。行位置だけがパネル固有点。
            return V6Lists.CompactRankRow(parent, row.Position.ToString(),
                row.IsYou ? "YOU" : row.PlayerName, V6ResultLogic.FormatScore(row.Score),
                row.Grade, new Vector2(8f, y), row.IsYou);
        }

        private void RefreshModal()
        {
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

        // --- ジャケット (解決は V6JacketResources に一本化) ---

        private static void SetThumb(Image thumb, SongData song)
        {
            if (thumb == null)
            {
                return;
            }

            Sprite sprite = V6JacketResources.LoadSprite(song.id, song.title);
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

        /// <summary>中央寄せで作られたアイコンを design の左上座標へ置き直す。</summary>
        private static void PlaceTopLeft(GameObject go, float x, float y, float w, float h)
        {
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
        }

        /// <summary>
        /// design のテキスト矩形は hug 幅 (実測値) なので、フォント差で数 px 足りないと
        /// TMP が折り返す (「スコアを登録」→ 2 行 /「Register」→ "Registe"+"r" /
        /// 「Backspace」→ "Backspac"+"e")。design 座標を変えずに 1 行固定にする。
        /// </summary>
        private static TextMeshProUGUI OneLine(TextMeshProUGUI tmp)
        {
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            return tmp;
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
            V6ButtonPressFeedback.Attach(go);
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
