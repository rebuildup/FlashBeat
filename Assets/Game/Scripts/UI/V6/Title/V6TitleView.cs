using System;
using System.Collections.Generic;
using FlashBeat.Update;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FlashBeat.UI.V5;

namespace FlashBeat.UI.V6.Title
{
    /// <summary>
    /// Title 画面の v6 UI。design/flashbeat-ui.pen v6 の
    /// Title · v6 (lyK9E) → 01 Title · v6 (QJtTv) を実行時に構築する。
    ///
    /// - 既存のタイトル (VideoPlayer "Screen" の動画) は消さない。この view は
    ///   動画の上に重ねる overlay で、背景は透明 (design の各 Zone は fill なし)。
    /// - design の Logo (CnRJj / assets/13.png) は描かない: タイトル動画自体が
    ///   ロゴを映しており、repo に 13.png は無い (上から重ねると二重に見える)。
    /// - 実データ: 曲数 / カタログ状態は GManager (ActiveSongs / HasRemoteCatalog)、
    ///   バージョンと更新状態は Application.version と UpdateChecker.Model。
    ///   design 例の固定値 (v1.1.0 → v1.2.0 等) は決して出さない。
    /// - 構築はコードのみ (シーン YAML / Prefab / .meta は触らない)。
    /// - 寸法は design px (1728x972 基準)。frame を u = min(W/1728, H/972) 倍して
    ///   3 解像度 (FHD / 2K / Mac) でクリップしないようにする
    ///   (v6 HUD / Select と同じ方式)。色は V5Tokens を流用する。
    /// - キー入力は読まない: Start / Option / Exit は既存の TitleSceneManager.Update
    ///   (KeyActions) が受け持ち、遷移は onStart / onOption / onExit コールバックで
    ///   統合担当が渡す。History / Ranking タブはマウスクリックのみ
    ///   (onHistory / onRanking を渡さなければ押しても何も起きない)。
    /// </summary>
    public sealed class V6TitleView
    {
        /// <summary>現在生成されている v6 タイトル (タイトルシーンに 1 個だけ)。</summary>
        public static V6TitleView Current { get; private set; }

        // --- design px 定数 (QJtTv / lyK9e 実測)。フレーム基準サイズは V6TitleLogic。---

        // Header / Footer は (24, 0) / (24, 876) 1680x96。Main は (24, 96) 1680x780。
        private const float HeaderX = 24f, HeaderY = 0f, HeaderW = 1680f, HeaderH = 96f;
        private const float FooterX = 24f, FooterY = 876f, FooterW = 1680f, FooterH = 96f;

        // Controls · タブ (FHcxk): main-local (280, 552) 1120x48、4 タブ各 280x48。
        private const float TabsX = 280f, TabsY = 552f, TabsW = 1120f, TabsH = 48f;
        private const float TabW = 280f;

        private readonly Action onStart;
        private readonly Action onOption;
        private readonly Action onExit;
        private readonly Action onHistory;
        private readonly Action onRanking;

        private GameObject rootGo;
        private RectTransform frame;

        private float u = 1f;
        private int lastWidth;
        private int lastHeight;

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

            UpdateScale();

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

            return model.Phase + "|" + (model.LatestVersion ?? "") + "|"
                + Mathf.RoundToInt(model.DownloadProgress * 100f);
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

            frame = V5Ui.Node(rootGo, "V6 Title Frame 1728x972").GetComponent<RectTransform>();
            frame.anchorMin = new Vector2(0.5f, 0.5f);
            frame.anchorMax = new Vector2(0.5f, 0.5f);
            frame.pivot = new Vector2(0.5f, 0.5f);
            frame.sizeDelta = new Vector2(V6TitleLogic.FrameW, V6TitleLogic.FrameH);

            var driver = rootGo.AddComponent<V6TitleViewDriver>();
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
            u = V6TitleLogic.DesignScale(w, h);
            if (frame != null)
            {
                frame.localScale = new Vector3(u, u, 1f);
            }
        }

        private void Rebuild()
        {
            if (rootGo == null)
            {
                return;
            }

            UpdateScale();
            lastUpdateStateKey = UpdateStateKey();

            // frame の全子を作り直す (V6SelectView.Rebuild と同じ方針)。
            var children = new List<GameObject>();
            foreach (Transform child in frame.transform)
            {
                children.Add(child.gameObject);
            }

            for (int i = 0; i < children.Count; i++)
            {
                V5GameUi.DestroyNow(children[i]);
            }

            BuildHeader();
            BuildMain();
            BuildFooter();
        }

        // --- ヘッダー (x24 y0 1680x96) ---

        private void BuildHeader()
        {
            var header = At(V5Ui.Panel(frame.gameObject, "Header", new Vector2(HeaderW, HeaderH), Clear(), 0f),
                HeaderX, HeaderY);

            // 左上: 終了 (icon button + KeyCap E + "Exit")。design lyK9E の zUcsi 差し替え
            // (Back button → Exit)。タイトルテキスト (ci867) は design で enabled:false
            // (動画がロゴを映す) のため作らない。
            var exit = At(V5GameUi.Row(header.gameObject, "Zone Exit", 12f, TextAnchor.MiddleLeft), 0f, 20f, 400f, 56f);
            var exitButton = Button(exit.gameObject, "Exit Button", 56f, 56f, 8f, V5Tokens.Key4, onExit);
            var exitIcon = V5GameUi.Text(exitButton, "Icon", "✕", 32f, V5Tokens.TextMuted, TextAlignmentOptions.Center);
            Stretch(exitIcon.GetComponent<RectTransform>(), 56f, 56f);
            V5Ui.KeyCap(exit.gameObject, "E", new Vector2(44f, 36f));
            V5GameUi.Text(exit.gameObject, "Label", "Exit", 30f, V5Tokens.TextMuted, TextAlignmentOptions.Left);

            // 右上: [更新して再起動 (更新中のみ)] [曲数 / カタログ状態] [バージョン]
            // design zPAF8 (space_between, padding 24)。
            var info = At(V5Ui.Panel(header.gameObject, "Zone Info", new Vector2(840f, HeaderH), Clear(), 0f),
                840f, 0f);
            var infoRow = info.AddComponent<HorizontalLayoutGroup>();
            infoRow.spacing = 24f;
            infoRow.padding = new RectOffset(24, 24, 0, 0);
            infoRow.childAlignment = TextAnchor.MiddleRight;
            infoRow.childControlWidth = false;
            infoRow.childControlHeight = false;
            infoRow.childForceExpandWidth = false;
            infoRow.childForceExpandHeight = false;

            UpdateFlowModel model = UpdateModel();
            if (model != null && V6TitleLogic.ShowUpdateButton(model.Phase))
            {
                var update = Button(info, "Update Button", 460f, 56f, 8f, V5Tokens.Key4, TriggerUpdate);
                var updateRow = V5GameUi.Row(update, "Inner", 12f, TextAnchor.MiddleCenter);
                Stretch(updateRow.GetComponent<RectTransform>(), 460f, 56f);
                V5GameUi.Text(updateRow.gameObject, "Label", V6TitleLogic.UpdateButtonLabel(model.Phase,
                    model.DownloadProgress), 30f, V5Tokens.Text, TextAlignmentOptions.Left);
                var updateSpacer = V5Ui.Node(updateRow.gameObject, "Spacer");
                var updateSpacerLe = updateSpacer.AddComponent<LayoutElement>();
                updateSpacerLe.flexibleWidth = 1f;
                var cap = V5Ui.Panel(updateRow.gameObject, "KeyCap F3", new Vector2(48f, 32f),
                    new Color(0f, 0f, 0f, 0.25f), 4f);
                var capText = V5GameUi.Text(cap, "Key", "F3", 22f, V5Tokens.TextMuted, TextAlignmentOptions.Center);
                Stretch(capText.GetComponent<RectTransform>(), 48f, 32f);
            }

            // 曲数 / カタログ状態 (実データ)。design の右上情報ゾーンは更新ボタン +
            // バージョンで埋まるため、Exit と情報ゾーンの間 (x440..840) に出す。
            int songCount = V6TitleLogic.PlayableSongCount(GManager.ActiveSongs);
            var status = V5GameUi.Text(header.gameObject, "Catalog Status",
                V6TitleLogic.CatalogStatusText(GManager.HasRemoteCatalog, songCount), 22f, V5Tokens.TextDim,
                TextAlignmentOptions.Left);
            At(status, 440f, 30f, 396f, 36f, new Vector2(0f, 0.5f));

            var versionRow = V5GameUi.Row(info, "Version", 8f, TextAnchor.MiddleRight);
            var versionLe = versionRow.gameObject.AddComponent<LayoutElement>();
            versionLe.preferredWidth = 280f;
            versionLe.preferredHeight = HeaderH;
            var currentVersion = V5GameUi.Text(versionRow.gameObject, "Current",
                V6TitleLogic.VersionLabel(Application.version), 22f, V5Tokens.TextDim, TextAlignmentOptions.Right);
            Stretch(currentVersion.GetComponent<RectTransform>(), 120f, HeaderH);
            if (model != null && V6TitleLogic.ShowNewVersion(model.Phase, model.LatestVersion))
            {
                var newVersion = V5GameUi.Text(versionRow.gameObject, "New",
                    V6TitleLogic.VersionLabel(model.LatestVersion), 22f, V5Tokens.AccentLight,
                    TextAlignmentOptions.Right, FontStyles.Bold);
                Stretch(newVersion.GetComponent<RectTransform>(), 100f, HeaderH);
                var arrow = V5GameUi.Text(versionRow.gameObject, "Arrow", "→", 20f, V5Tokens.TextDim,
                    TextAlignmentOptions.Right);
                Stretch(arrow.GetComponent<RectTransform>(), 24f, HeaderH);
            }
        }

        // --- メイン (x24 y96 1680x780) ---

        private void BuildMain()
        {
            var main = At(V5Ui.Panel(frame.gameObject, "Main", new Vector2(1680f, 780f), Clear(), 0f), 24f, 96f);

            // Logo (CnRJj) は描かない (動画がロゴを映す。クラスコメント参照)。

            // Controls · タブ (FHcxk): (280, 552) 1120x48, $overlay r8, 4 タブ各 280x48。
            var tabs = At(V5Ui.Panel(main.gameObject, "Tabs", new Vector2(TabsW, TabsH), V5Tokens.Overlay,
                V5Tokens.RadiusMedium), TabsX, TabsY);

            BuildTab(tabs, "Tab GameStart", "▶", "GameStart", "Space", true, onStart);
            BuildTab(tabs, "Tab History", "≡", "History", "H", false, onHistory);
            BuildTab(tabs, "Tab Ranking", "★", "Ranking", "R", false, onRanking);
            BuildTab(tabs, "Tab Option", "⚙", "Option", "O", false, onOption);
        }

        private void BuildTab(GameObject tabs, string name, string icon, string label, string key, bool selected,
            Action onClick)
        {
            var tab = At(V5Ui.Panel(tabs, name, new Vector2(TabW, TabsH),
                selected ? V5Tokens.Key6 : Clear(), 0f), TabW * TabIndex(name), 0f);
            MakeButton(tab, onClick);

            var row = V5GameUi.Row(tab, "Inner", 8f, TextAnchor.MiddleLeft);
            Stretch(row.GetComponent<RectTransform>(), TabW, TabsH);
            row.padding = new RectOffset(10, 10, 0, 0);
            V5GameUi.Text(row.gameObject, "Icon", icon, 24f, selected ? V5Tokens.Text : V5Tokens.TextMuted,
                TextAlignmentOptions.Center);
            V5GameUi.Text(row.gameObject, "Label", label, 26f, selected ? V5Tokens.Text : V5Tokens.TextMuted,
                TextAlignmentOptions.Left);
            var spacer = V5Ui.Node(row.gameObject, "Spacer");
            var spacerLe = spacer.AddComponent<LayoutElement>();
            spacerLe.flexibleWidth = 1f;
            var cap = V5Ui.Panel(row.gameObject, "KeyCap " + key, new Vector2(88f, 34f),
                new Color(0f, 0f, 0f, 0.25f), 4f);
            var capText = V5GameUi.Text(cap, "Key", key, 22f, V5Tokens.TextMuted, TextAlignmentOptions.Center);
            Stretch(capText.GetComponent<RectTransform>(), 88f, 34f);
        }

        /// <summary>タブ名から design 上の列位置 (0..3) を出す。</summary>
        private static int TabIndex(string name)
        {
            switch (name)
            {
                case "Tab GameStart": return 0;
                case "Tab History": return 1;
                case "Tab Ranking": return 2;
                default: return 3;
            }
        }

        // --- フッター (x24 y876 1680x96) ---

        private void BuildFooter()
        {
            var footer = At(V5Ui.Panel(frame.gameObject, "Footer", new Vector2(FooterW, FooterH), Clear(), 0f),
                FooterX, FooterY);

            // 左下: キー操作の凡例 (design QQ4jM, gap 16)。Space 決定 / ↑↓ 選択 / E 終了。
            // (lyK9E で RpQyz のラベルは「選択」、e6RJ2 は「E / 終了」に差し替え済み。)
            var legend = At(V5GameUi.Row(footer.gameObject, "Legend", 16f, TextAnchor.MiddleLeft),
                0f, 30f, 840f, 36f);
            V5GameUi.LegendItem(legend.gameObject, "Space", "決定");
            V5GameUi.LegendItem(legend.gameObject, "↑↓", "選択");
            V5GameUi.LegendItem(legend.gameObject, "E", "終了");

            // 右下の操作ボタン (T8HuQS) は design lyK9E で enabled:false (タブに統合済み)。
        }

        // --- uGUI ユーティリティ (design px 左上原点, V6SelectView と同じ) ---

        private static GameObject At(GameObject go, float x, float y, float w, float h)
        {
            return At(go.GetComponent<RectTransform>(), x, y, w, h);
        }

        private static GameObject At(GameObject go, float x, float y)
        {
            return At(go.GetComponent<RectTransform>(), x, y);
        }

        private static GameObject At(Component node, float x, float y, float w, float h)
        {
            var rt = node.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
            return node.gameObject;
        }

        private static GameObject At(Component node, float x, float y)
        {
            var rt = node.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            return node.gameObject;
        }

        /// <summary>pivot 指定版 (左上 (x, -y) を維持する)。</summary>
        private static GameObject At(Component node, float x, float y, float w, float h, Vector2 pivot)
        {
            At(node, x, y, w, h);
            var rt = node.GetComponent<RectTransform>();
            rt.pivot = pivot;
            rt.anchoredPosition = new Vector2(x, -y);
            return node.gameObject;
        }

        /// <summary>親 (row 等) の中央に stretch する (Row/Column の子として使う)。</summary>
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
            return MakeButton(go, onClick);
        }

        private static GameObject MakeButton(GameObject go, Action onClick)
        {
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
    }
}
