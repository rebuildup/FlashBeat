using System;
using System.Collections.Generic;
using FlashBeat.Update;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FlashBeat.UI.V5;
using FlashBeat.UI.V6;
using FlashBeat.UI.V6.Common;

namespace FlashBeat.UI.V6.Title
{
    /// <summary>
    /// Title 画面の v6 UI。design/flashbeat-ui.pen v6 の
    /// 01 Title · v6 (QJtTv) を実行時に構築する。
    ///
    /// - 共通フレームは V6Frame (1728×972、契約 §4) を使い、実画面へは
    ///   <see cref="V6TitleLogic.FrameOrigin"/> で 1:1 (等倍) 中央配置する。
    ///   引き伸ばし (等比スケール) はしない (design POJwG "Resolution Check · Title":
    ///   FHD (96,54) / 2K (416,234) / Mac (416,346))。フレーム外の画面外周には
    ///   不透明パネルを敷かない (design QJtTv の fill はゲーム自身の背景 = タイトル動画)。
    /// - design の Logo (CnRJj / assets/13.png) は
    ///   Assets/Game/Resources/V5/Common/13.png を描く (タイトル動画の上に重ねる)。
    /// - 実データ: 曲数 / カタログ状態は GManager (ActiveSongs / HasRemoteCatalog)、
    ///   右上の曲情報 (自己ベスト) は GManager のカタログと保存済みベストスコア
    ///   (GManager.BestScoreOf)、バージョンと更新状態は Application.version と
    ///   UpdateChecker.Model。design 例の固定値 (v1.1.0 → v1.2.0 等) は決して出さない。
    ///   更新ボタンは design には無いノードだが、更新の導線は Title にしか無いので
    ///   Header 帯の左側 (340,20) に置く (実機能のため残す)。
    /// - キーのキャップ (Space / O / Esc ...) は実際の割り当て (KeyActions) から取る。
    /// - 構築はコードのみ (シーン YAML / Prefab / .meta は触らない)。
    /// - キー入力は読まない: 選択の移動 (MoveSelection) と決定 (ActivateSelected) は
    ///   既存の TitleSceneManager.Update (KeyActions) が受け持ち、遷移は
    ///   onStart / onOption / onExit コールバックで統合担当が渡す。
    ///   History / Ranking タブは操作が設定されているときだけ描く
    ///   (押しても何も起きないボタンを作らない)。
    ///
    /// Issue #385 Phase 2.9: 領域ごとに partial class へ分割した
    /// (ヘッダー = V6TitleViewHeader.cs、メイン/タブ = V6TitleViewMain.cs、
    /// フッター/uGUI ユーティリティ = V6TitleViewFooter.cs)。このファイルは
    /// ライフサイクル・公開 API・毎フレーム駆動・ルート/フレーム構築と
    /// ゾーン再構築 (Rebuild) を持つ。
    /// </summary>
    public sealed partial class V6TitleView
    {
        /// <summary>現在生成されている v6 タイトル (タイトルシーンに 1 個だけ)。</summary>
        public static V6TitleView Current { get; private set; }

        /// <summary>
        /// v6 のタイトルを使うか (統合担当が切り替える。既定 true)。
        ///
        /// false のときは旧 (シーン配置) のタイトルをそのまま動かす。
        /// V6UiMode は統合担当が所有しているため、この画面固有のフラグは
        /// ここに持つ (V6ResultView.Enabled と同じ運用)。
        /// </summary>
        public static bool Enabled = true;

        // --- design px 配置 (QJtTv 実測)。フレーム基準サイズと origin は V6TitleLogic。---

        private readonly Action onStart;
        private readonly Action onOption;
        private readonly Action onExit;
        private Action onHistory;
        private Action onRanking;

        /// <summary>タブ 1 件の表示情報 (ラベル / アイコンスプライト名 / 実キー / 操作)。</summary>
        private sealed class Tab
        {
            public string Name;
            public string IconSprite; // スプライト名 (Resources/Icons/ 以下)。空なら非表示。
            public string Label;
            public string Key;
            public Action Action;
        }

        /// <summary>今あるタブ (操作が設定されているものだけ)。</summary>
        private readonly List<Tab> tabList = new List<Tab>();

        /// <summary>キーボードで選択中のタブ。</summary>
        private int selectedTab;

        private GameObject rootGo;
        private RectTransform frame;
        private Transform headerZone;
        private Transform mainZone;
        private Transform footerZone;

        // 更新状態の前回表示値 (変化したときだけ再描画する)。
        private string lastUpdateStateKey = "";

        private V6TitleView(Action onStart, Action onOption, Action onExit,
            Action onHistory, Action onRanking)
        {
            this.onStart = onStart;
            this.onOption = onOption;
            this.onExit = onExit;
            this.onHistory = onHistory;
            this.onRanking = onRanking;
        }

        /// <summary>View を生成する。統合担当 (TitleSceneManager 側) から呼ぶ。</summary>
        public static V6TitleView Create(RectTransform parent, Action onStart, Action onOption, Action onExit,
            Action onHistory = null, Action onRanking = null)
        {
            if (parent == null)
            {
                throw new ArgumentNullException(nameof(parent));
            }

            var view = new V6TitleView(onStart, onOption, onExit, onHistory, onRanking);
            // 旧 (v5) タイトル UI と更新通知バナーを隠す (v6 が自前で更新情報を
            // 描くので、旧側は二重表示になるだけ)。Create は v6 モード
            // (V6TitleView.Enabled) でしか呼ばれないので、ここで分岐する。
            V6UiMode.HideLegacyTitleUi();
            V6UiMode.HideLegacyUpdateNotice();
            view.BuildRoot(parent);
            view.Rebuild();
            Current = view;
            return view;
        }

        public RectTransform Root => frame;

        /// <summary>実データ (曲数 / カタログ状態 / 更新状態) から表示を作り直す。</summary>
        public void Refresh()
        {
            Rebuild();
        }

        /// <summary>
        /// History / Ranking タブの操作を設定する (統合担当が渡す)。
        /// null のタブは描かない: 押しても何も起きないボタンを作らない。
        /// </summary>
        public void SetPanelHandlers(Action history, Action ranking)
        {
            if (onHistory == history && onRanking == ranking)
            {
                return;
            }

            onHistory = history;
            onRanking = ranking;
            Rebuild();
        }

        /// <summary>タブ選択を delta 行だけ動かす (端で折り返す)。</summary>
        public void MoveSelection(int delta)
        {
            if (tabList.Count == 0)
            {
                return;
            }

            selectedTab = V6TitleLogic.MoveIndex(selectedTab, delta, tabList.Count);
            Rebuild();
        }

        /// <summary>選択中のタブを実行する (キーボードの決定)。</summary>
        public void ActivateSelected()
        {
            if (tabList.Count == 0)
            {
                return;
            }

            Tab tab = tabList[selectedTab];
            if (tab != null && tab.Action != null)
            {
                tab.Action();
            }
        }

        /// <summary>選択中のタブのラベル (テスト / 統合担当向け)。</summary>
        public string SelectedTabLabel
        {
            get
            {
                if (selectedTab < 0 || selectedTab >= tabList.Count)
                {
                    return "";
                }

                return tabList[selectedTab].Label;
            }
        }

        public void SetVisible(bool visible)
        {
            if (rootGo != null)
            {
                rootGo.SetActive(visible);
            }
        }

        /// <summary>生成したビューを破棄するとき呼ぶ (V6SelectView.Dispose と同じ契約)。</summary>
        public void Dispose()
        {
            if (rootGo != null)
            {
                V5GameUi.DestroyNow(rootGo);
            }

            if (ReferenceEquals(Current, this))
            {
                Current = null;
            }
        }

        // --- 毎フレーム (V6TitleViewDriver から呼ばれる) ---

        internal void Tick()
        {
            if (rootGo == null || !rootGo.activeInHierarchy)
            {
                return;
            }

            PlaceFrame();

            // 更新フロー (チェック / ダウンロード進捗 / 適用待ち) は時間で変わるので
            // 状態キーが変わったときだけ作り直す。
            string key = UpdateStateKey();
            if (key != lastUpdateStateKey)
            {
                Rebuild();
            }
        }

        // --- 更新フローの実データ (UpdateNoticeUI と同じ供給経路) ---

        private static UpdateFlowModel UpdateModel()
        {
            UpdateChecker.EnsureExists();
            var checker = UnityEngine.Object.FindAnyObjectByType<UpdateChecker>();
            return checker != null ? checker.Model : null;
        }

        private string UpdateStateKey()
        {
            UpdateFlowModel model = UpdateModel();
            if (model == null)
            {
                return "none";
            }

            // dismiss (F4) も表示状態に影響するのでキーに含める (消えた表示を
            // 再描画で復活させない)。
            return model.Phase + "|" + (model.LatestVersion ?? "") + "|"
                + Mathf.RoundToInt(model.DownloadProgress * 100f) + "|"
                + (model.DismissedForSession ? "dismissed" : "shown");
        }

        /// <summary>更新ボタンのクリック。UpdateNoticeUI.Trigger と同じ分岐
        /// (Available → ダウンロード、Ready → 適用) を既存 API で実行する。</summary>
        private static void TriggerUpdate()
        {
            var notice = UnityEngine.Object.FindAnyObjectByType<UpdateNoticeUI>();
            if (notice != null)
            {
                notice.Trigger();
            }
        }

        // --- 構築 ---

        private void BuildRoot(RectTransform parent)
        {
            rootGo = V5Ui.Node(parent.gameObject, "V6 Title View");
            var rt = rootGo.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = Vector2.zero;

            // 画面外周には不透明パネルを敷かない: design QJtTv の fill は
            // assets/bg-anim3.png (mode=cover) 相当 = ゲーム自身の背景
            // (タイトル動画 VideoPlayer "Screen") が見えるままにする。

            // 共通フレーム (1728×972、Header/Main/Footer/Modal ゾーン + 外周付き)。
            frame = V6Frame.Create(rootGo.transform, "V6 Title Frame 1728x972").GetComponent<RectTransform>();
            headerZone = frame.transform.Find("Header");
            mainZone = frame.transform.Find("Main");
            footerZone = frame.transform.Find("Footer");

            var driver = rootGo.AddComponent<V6TitleViewDriver>();
            driver.View = this;
            PlaceFrame();
        }

        /// <summary>
        /// フレームを 1:1 (等倍) で中央配置する (引き伸ばしはしない)。
        /// origin = ((w−1728)/2, (h−972)/2) 切り捨て (V6TitleLogic.FrameOrigin、
        /// design POJwG "Resolution Check · Title" の実配置)。
        /// </summary>
        private void PlaceFrame()
        {
            if (frame == null)
            {
                return;
            }

            V6Layout.Place(frame, V6TitleLogic.FrameOrigin(Screen.width, Screen.height),
                new Vector2(V6TitleLogic.FrameW, V6TitleLogic.FrameH));
        }

        private void Rebuild()
        {
            if (rootGo == null)
            {
                return;
            }

            PlaceFrame();
            lastUpdateStateKey = UpdateStateKey();

            // 更新の実状態 (UpdateChecker.Model)。ヘッダー / メインの双方から参照する。
            UpdateFlowModel model = UpdateModel();

            // 各ゾーンの中身を作り直す (ゾーン自体 / Modal / 外周は V6Frame が所有)。
            V6Hierarchy.ClearChildren(headerZone);
            V6Hierarchy.ClearChildren(mainZone);
            V6Hierarchy.ClearChildren(footerZone);

            BuildHeader(model);
            BuildTabList();
            BuildMain(model);
            BuildFooter();
        }

    }
}
