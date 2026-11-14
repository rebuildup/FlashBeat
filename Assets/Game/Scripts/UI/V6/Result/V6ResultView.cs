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
    public sealed partial class V6ResultView
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

        private readonly Action onRetry;
        private readonly Action onSelect;
        private readonly Action onTitle;

        private GameObject rootGo;
        private RectTransform frame;
        private V6ResultViewDriver driver;
        // run snapshot (Issue #240): 表示前に GManager.ResetSession() が呼ばれて値が
        // 消えるなら、消える前に捕捉する。初回 Refresh で固定し、以後は確定値を使う。
        private int runScore, runPerfect, runGreat, runBad, runMiss, runMaxCombo;
        private bool runCaptured;
        // 前回最高 (リザルト確定時の値)。統合担当が SetPrevHiscore で渡す
        // (未指定なら差分表示は "–")。
        private int prevHiscore = -1;
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

            // トロフィーバッジは V6ResultTrophy.cs (Issue #385 Phase 2.1)。
            BuildTrophyBadge(header);


            // design zcSHh/aqq3q の Title (72,20) 400x56 fs38: 文字列は "RESULT"
            // (全画面のヘッダーは画面名を出す。Title 画面だけ "FlashBeat")。
            // 旧実装は pivot (0,0.5) で置いていたが、補正済み anchoredPosition の
            // 結果として矩形は (72,20) 400×56 で同一 — 共通部品の既定配置と一致する。
            V6Header.Title(header, "RESULT");
        }

        // --- メイン (共有フレーム Main: x24 y96 1680x780, 契約 §4) ---

        private void BuildMain()
        {
            var main = V6Layout.Place(V5Ui.Panel(frame.gameObject, "Main",
                    new Vector2(V6ResultLogic.MainW, V6ResultLogic.MainH), Color.clear, 0f),
                V6ResultLogic.MainX, V6ResultLogic.MainY);
            BuildResultBlock(main);
            BuildGauge(main);
            BuildJudgements(main);
            BuildSongInfo(main);
            BuildSongRanking(main);
            BuildPlayerRanking(main);
        }

        // --- フッター (x24 y876 1680x96) ---

        private void BuildFooter()
        {
            var footer = V6Layout.Place(V5Ui.Panel(frame.gameObject, "Footer", new Vector2(1680f, 96f), Color.clear, 0f), 24f, 876f);

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

        // --- ジャケット (解決は V6JacketProvider / 適用は V6SongBadge に一本化) ---

        private static void SetThumb(Image thumb, SongData song)
        {
            V6SongBadge.ApplyJacket(thumb, song.id, song.title);
        }

        // --- uGUI ユーティリティは V6Layout / V6Button / V6Hierarchy に集約 (Issue #385) ---

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
    }
}
