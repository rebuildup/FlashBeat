using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FlashBeat.Gameplay;
using FlashBeat.UI.V5;
using FlashBeat.UI.V6;
using FlashBeat.UI.V6.Common;

namespace FlashBeat.UI.V6.Game.Pause
{
    /// <summary>
    /// v6 ポーズ画面 (Issue #224, design/flashbeat-ui.pen の `uDNC0` / 共通フレーム `rw7zx`)。
    ///
    /// - すべてコードから uGUI を生成する (v5 PauseMenuView / v6 V6GameHudView と同じ方針)。
    /// - 寸法は design px (1728x972)。共通フレームを 1:1 (等倍) で画面中央に置く
    ///   (design "Resolution Check · Game (PAUSE)": <see cref="V6PauseViewLogic.FrameOrigin"/>)。
    ///   等比拡大 (min(W/1728, H/972)) は行わない — design は各解像度ごとに
    ///   フレーム原点を固定している (FHD (96,54) / 2K (416,234) / MacBook (416,346))。
    /// - 色は V5Tokens の既存値 (v6 は色トークンを追加していない)。
    /// - 表示値はすべて実データ: GManager (曲情報 / noteSpeed / noteTiming / Hiscore)、
    ///   KeyBindings (Key Layout / プレビューのキー割当、Changed で即時更新)、
    ///   PlaySession (Key Guide / Lyrics)。
    /// - キー入力は持たない (統合担当が Esc / 矢印 / T (MenuSwitch) を既存キー処理に接続する)。
    ///   このビューは状態 (IsOpen) とマウス操作、統合側から呼べる操作 API だけを持つ。
    /// - 選曲へ / リトライ は既存 GameSceneManager.RetireGame / RetryGame に直結 (実遷移)。
    /// </summary>
    public sealed class V6PauseView
    {
        /// <summary>現在生成されている v6 ポーズ画面 (GameScene に 1 個)。統合側・検証から参照する。</summary>
        public static V6PauseView Current { get; private set; }

        /// <summary>
        /// v6 ポーズを有効にするか (既定 true)。V6UiMode は統合担当の所有なので、
        /// 連動フラグはここに置く。false のときは <see cref="Create"/> が null を返し、
        /// Esc は旧 v5 (PauseMenuView / レガシー MenuPanel) の経路に戻る。
        /// </summary>
        public static bool Enabled = true;

        // --- design px 定数 (flashbeat-v6-nodes.json uDNC0 / rw7zx) ---
        private const float FrameW = 1728f;
        private const float FrameH = 972f;

        private const float HeaderX = 24f, HeaderY = 0f, HeaderW = 1680f, HeaderH = 96f;
        private const float MainX = 24f, MainY = 96f;
        private const float FooterX = 24f, FooterY = 876f, FooterW = 1680f, FooterH = 96f;

        private static readonly string[] TabNames = { "Play", "Display", "Sound", "Data" };

        /// <summary>設定行の定義。値の読み書きはすべて実データへの委譲。</summary>
        private sealed class Row
        {
            public TextMeshProUGUI Value;
            public Func<string> Read;
            public Action<int> Adjust;
        }

        private GameObject rootGo;
        private RectTransform frame;
        private V6PauseViewDriver driver;
        private GameSceneManager sceneManager;

        private readonly List<GameObject> tabPanels = new List<GameObject>();
        private readonly List<TextMeshProUGUI> tabLabels = new List<TextMeshProUGUI>();
        private readonly List<GameObject> pages = new List<GameObject>();
        private readonly List<List<Row>> pageRows = new List<List<Row>>();
        private readonly List<Image> keyCaps = new List<Image>();
        private readonly List<TextMeshProUGUI> keyLabels = new List<TextMeshProUGUI>();

        private TextMeshProUGUI settingsTitle;
        private TextMeshProUGUI songTitle;
        private TextMeshProUGUI songSub;
        private TextMeshProUGUI levelValue;
        private TextMeshProUGUI notesValue;
        private TextMeshProUGUI durationValue;
        private TextMeshProUGUI previewHeadTitle;
        private Image thumbImage;

        private int selectedTab;
        private int selectedRow;
        private bool open;

        /// <summary>Key Layout リセットの確認ダイアログ (開いていないとき null)。</summary>
        private GameObject confirmRoot;

        private int lastWidth;
        private int lastHeight;

        private string loadedVideoId;

        private V6PauseView()
        {
        }

        /// <summary>View を生成する (非表示のまま)。統合担当から呼ぶ。
        /// <see cref="Enabled"/> が false のときは null を返す (v5 のポーズに任せる)。</summary>
        public static V6PauseView Create(RectTransform parent)
        {
            if (!Enabled)
            {
                return null;
            }

            if (parent == null)
            {
                throw new ArgumentNullException(nameof(parent));
            }

            var view = new V6PauseView();
            view.Build(parent);
            Current = view;
            return view;
        }

        public RectTransform Root
        {
            get { return frame; }
        }

        public bool IsOpen
        {
            get { return open; }
        }

        /// <summary>
        /// 破壊操作の確認ダイアログ (Key Layout リセット) が開いているか。
        /// Scrim はマウスしか遮らないため、キーボード入力の排他は呼び出し側が行う。
        /// </summary>
        public bool HasConfirmDialog
        {
            get { return confirmRoot != null; }
        }

        /// <summary>生成したビューを破棄するときイベント購読を解除する。</summary>
        public void Dispose()
        {
            KeyBindings.Changed -= RefreshKeyBindings;
            if (rootGo != null)
            {
                V5GameUi.DestroyNow(rootGo);
            }

            if (ReferenceEquals(Current, this))
            {
                Current = null;
            }
        }

        /// <summary>ポーズを開く。キー入力は持たない (統合担当が Esc から呼ぶ)。</summary>
        public void Open()
        {
            if (open)
            {
                return;
            }

            // 再開カウントダウン中は開かない。取り消しは V6ResumeCountdown が
            // 自分で Esc を受けてから Open を呼ぶ (IsRunning を先に false にする)。
            if (V6ResumeCountdown.IsRunning)
            {
                return;
            }

            open = true;
            // Issue #224: ポーズ中はメディア時計・判定・譜面進行を止める。
            PauseGate.Enter();
            // Refresh はジャケット取得のコルーチンを起動する。非アクティブな GameObject では
            // "Coroutine couldn't be started ... inactive" になりサムネイルが欠けるため、
            // 先にアクティブ化してから Refresh する。
            rootGo.SetActive(true);
            Refresh();
            UpdatePlacement();
        }

        /// <summary>
        /// 再開 (Issue #382)。ポーズ画面を閉じ、歌詞スロットの 3 → 2 → 1 を経てから
        /// 演奏を再開する。PauseGate はカウントダウン終了まで閉じない。
        /// </summary>
        public void RequestResume()
        {
            if (!open)
            {
                return;
            }

            open = false;
            CancelResetKeyLayout();
            rootGo.SetActive(false);
            V6ResumeCountdown.Begin(this);
        }

        /// <summary>閉じる (再開)。リトライ / 終了 / リタイアなど、演奏を続けない経路で使う。</summary>
        public void Close()
        {
            bool wasCounting = V6ResumeCountdown.IsRunning;
            if (wasCounting)
            {
                V6ResumeCountdown.Abort();
            }

            if (!open && !wasCounting)
            {
                return;
            }

            open = false;
            CancelResetKeyLayout();
            // Issue #224: 再開時に判定基準時刻を壁時計へ再合わせする (ノーツが飛ばない)。
            PauseGate.Exit();
            rootGo.SetActive(false);
        }

        /// <summary>実データ (曲情報 / 設定値 / キー割当) を UI へ反映する。何度呼んでもよい。</summary>
        public void Refresh()
        {
            if (rootGo == null)
            {
                return;
            }

            RefreshHeader();
            RefreshTabs();
            RefreshRows();
            RefreshKeyBindings();
        }

        // --- 統合担当がキー入力 (矢印 / Tab) から呼ぶ操作 API ---

        /// <summary>設定行の選択を移動する (↑↓ / 正本 uDNC0 凡例「選択」)。</summary>
        public void MoveSelection(int delta)
        {
            selectedRow = V6PauseViewLogic.ClampIndex(selectedRow + delta, pageRows[selectedTab].Count);
            RefreshRowHighlight();
        }

        /// <summary>
        /// 選択中の行の値を変更する (←→ / 正本 uDNC0 凡例「変更」)。
        /// 値を持たない行 (キー設定 / 記録の表示) では何もしない。
        /// </summary>
        public void AdjustSelected(int delta)
        {
            if (!open || delta == 0)
            {
                return;
            }

            var rows = pageRows[selectedTab];
            if (selectedRow < 0 || selectedRow >= rows.Count)
            {
                return;
            }

            Row row = rows[selectedRow];
            if (row.Adjust == null)
            {
                return;
            }

            row.Adjust(delta);
            RefreshRows();
        }

        /// <summary>キー設定 (KeyConfigUI) を開く。ポーズ中 (PauseGate) なので判定は止まっている。</summary>
        public void OpenKeyConfig()
        {
            KeyConfigUI.Open();
        }

        /// <summary>Key Layout を標準へ戻す確認を出す (破壊的なので即実行しない)。</summary>
        public void RequestResetKeyLayout()
        {
            if (!IsCustomLayout() || confirmRoot != null)
            {
                return;
            }

            BuildResetConfirm();
        }

        /// <summary>確認ダイアログを閉じる。</summary>
        public void CancelResetKeyLayout()
        {
            if (confirmRoot != null)
            {
                V5GameUi.DestroyNow(confirmRoot);
                confirmRoot = null;
            }
        }

        /// <summary>確認済みのリセット (KeyBindings を既定へ戻し、保存する)。</summary>
        public void ApplyResetKeyLayout()
        {
            CancelResetKeyLayout();
            if (KeyBindings.Current == null)
            {
                return;
            }

            KeyBindings.ResetToDefaults();
            KeyBindings.Save();
            RefreshKeyBindings();
        }

        /// <summary>
        /// Key Layout を標準へ戻す確認ダイアログ (共通フレームのモーダルゾーン:
        /// Scrim 全面 + Dialog (444,231) 840×510 $k1 r16)。Option の同じ操作
        /// (issue-162 C-6: 破壊的なリセットは確認してから) と形を揃える。
        /// </summary>
        private void BuildResetConfirm()
        {
            confirmRoot = V5Ui.Node(frame.gameObject, "V6 Pause Confirm");
            var rootRt = GetRect(confirmRoot);
            Stretch(rootRt);
            rootRt.SetAsLastSibling();

            var scrim = V5Ui.Node(confirmRoot, "Scrim");
            V5Ui.Fill(GetRect(scrim), 0f);
            var scrimImg = scrim.AddComponent<Image>();
            scrimImg.color = new Color(0f, 0f, 0f, 0xCC / 255f);
            scrimImg.raycastTarget = true;

            var dialog = V5Ui.Panel(confirmRoot, "Dialog", new Vector2(840f, 510f), V5Tokens.Key1, 16f);
            PlaceAt(dialog, 444f, 231f, new Vector2(840f, 510f));

            var head = V5Ui.Text(dialog, "Head", "Key Layout", 30f, V5Tokens.Text,
                TextAlignmentOptions.Left, FontStyles.Bold);
            PlaceAt(head.gameObject, 28f, 28f, new Vector2(784f, 40f));

            var desc = V5Ui.Text(dialog, "Desc",
                "すべてのレーンのキー割り当てを標準に戻します。\n現在のカスタム割り当ては消去されます (元に戻せません)。",
                24f, V5Tokens.Text, TextAlignmentOptions.TopLeft);
            PlaceAt(desc.gameObject, 28f, 100f, new Vector2(784f, 120f));
            desc.lineSpacing = 40f;

            var cancel = V5Ui.Panel(dialog, "Op Cancel", new Vector2(198f, 50f), V5Tokens.Key4, 8f);
            PlaceAt(cancel, 28f, 432f, new Vector2(198f, 50f));
            var cancelLabel = V5Ui.Text(cancel, "Label", "キャンセル", 26f, V5Tokens.Text,
                TextAlignmentOptions.Center);
            Stretch(cancelLabel.rectTransform);
            var cancelButton = cancel.AddComponent<Button>();
            cancelButton.targetGraphic = cancel.GetComponent<Image>();
            cancelButton.onClick.AddListener(CancelResetKeyLayout);

            var confirm = V5Ui.Panel(dialog, "Op Reset", new Vector2(252f, 50f),
                new Color(0xE5 / 255f, 0x3E / 255f, 0x3E / 255f), 8f);
            PlaceAt(confirm, 560f, 432f, new Vector2(252f, 50f));
            var confirmLabel = V5Ui.Text(confirm, "Label", "戻す", 26f, V5Tokens.ButtonPrimaryLabel,
                TextAlignmentOptions.Center);
            Stretch(confirmLabel.rectTransform);
            var confirmButton = confirm.AddComponent<Button>();
            confirmButton.targetGraphic = confirm.GetComponent<Image>();
            confirmButton.onClick.AddListener(ApplyResetKeyLayout);
        }

        /// <summary>タブを切り替える (端で折り返す)。</summary>
        public void SelectNextTab(int delta)
        {
            SelectTab(V6PauseViewLogic.WrapIndex(selectedTab, delta, V6PauseViewLogic.TabCount));
        }

        /// <summary>タブを直接指定で切替 (マウスクリックからも)。</summary>
        public void SelectTab(int index)
        {
            selectedTab = V6PauseViewLogic.ClampIndex(index, V6PauseViewLogic.TabCount);
            selectedRow = 0;
            Refresh();
        }

        // --- 構築 ---

        private void Build(RectTransform parent)
        {
            sceneManager = UnityEngine.Object.FindFirstObjectByType<GameSceneManager>();

            rootGo = V5Ui.Node(parent.gameObject, "V6 Pause View");
            var rootRt = rootGo.GetComponent<RectTransform>();
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;

            // design: フレーム全体を #000000CC で暗転 (フレーム外の余地も消す)。
            var dim = V5Ui.Node(rootGo, "Pause Dim");
            V5Ui.Fill(dim.GetComponent<RectTransform>(), 0f);
            var dimImg = dim.AddComponent<Image>();
            dimImg.color = new Color(0f, 0f, 0f, 0xCC / 255f);
            dimImg.raycastTarget = true;

            frame = V5Ui.Node(rootGo, "V6 Pause Frame 1728x972").GetComponent<RectTransform>();
            frame.anchorMin = Vector2.zero;
            frame.anchorMax = Vector2.zero;
            frame.pivot = Vector2.zero;
            frame.sizeDelta = new Vector2(FrameW, FrameH);

            BuildHeader();
            BuildMain();
            BuildFooter();

            driver = rootGo.AddComponent<V6PauseViewDriver>();
            driver.View = this;

            KeyBindings.Changed += RefreshKeyBindings;

            rootGo.SetActive(false);
            UpdatePlacement();
        }

        private void BuildHeader()
        {
            var header = V5Ui.Node(frame.gameObject, "Header");
            PlaceAt(header, HeaderX, HeaderY, new Vector2(HeaderW, HeaderH));

            // design uDNC0/SUD2i: 左上はタイトル "PAUSE" のみ。戻るゾーン (b7VqL) は
            // design の実ノードが中身ゼロ (子・fill 無し) の空プレースホルダで、
            // 正本の描画 uDNC0-pause にも戻るボタンは出ない。よって Esc ボタンは描かない
            // (Esc = 再開は凡例と「再開」ボタンの KeyCap が示す)。
            // 正本 uDNC0/ci867 は Title ゾーン (72,20) 185×56 に "PAUSE" (fs38 w700)。

            // design uDNC0/ci867: Title ゾーン (ヘッダー内 (72,20) 185×56 → ページ (96,20))
            // に "PAUSE" (fs38 w700)。
            var title = V5Ui.Text(header, "Title", "PAUSE", 38f, V5Tokens.Text,
                TextAlignmentOptions.Left, FontStyles.Bold);
            title.textWrappingMode = TextWrappingModes.NoWrap;
            PlaceAt(title.gameObject, 72f, 20f, new Vector2(185f, 56f));

            // 右上: ジャケット + 曲情報 + 区切り + LEVEL / NOTES / 曲長 (実データ)。
            // 座標は正本 PRqZF (ヘッダー右 840×96) の子の実値 — レイアウトグループで
            // 詰めると設計の x (Thumb 24 / Song 100 / Divider 432 / Stat 445・557・669) から
            // ずれて曲名がタブ行に潜り込むため、1 個ずつ設計座標へ置く。
            var info = V5Ui.Node(header, "Song Info");
            var infoRt = GetRect(info);
            infoRt.anchorMin = new Vector2(1f, 1f);
            infoRt.anchorMax = new Vector2(1f, 1f);
            infoRt.pivot = new Vector2(1f, 1f);
            infoRt.anchoredPosition = Vector2.zero;
            infoRt.sizeDelta = new Vector2(840f, HeaderH);

            var thumb = V5Ui.Panel(info, "Thumb", new Vector2(64f, 36f), V5Tokens.Key2, 4f);
            PlaceAt(thumb, 24f, 30f);
            thumbImage = thumb.GetComponent<Image>();
            thumbImage.raycastTarget = false;

            var songCol = V5Ui.Node(info, "Song");
            PlaceAt(songCol, 100f, 8f, new Vector2(320f, 80f));
            songTitle = V5Ui.Text(songCol, "Title", "", 30f, V5Tokens.Text, TextAlignmentOptions.Left);
            songTitle.textWrappingMode = TextWrappingModes.NoWrap;
            songTitle.overflowMode = TextOverflowModes.Ellipsis;
            // design は h43。TMP は行の実高 (NotoSansJP: fs の約 1.448 倍 = 43.44px / fs30)
            // より矩形が低いと Ellipsis で 1 行も描かないため、以前は 46px に逃げていた。
            // いまは margin 補正 (V5Ui.FitEllipsisToLineHeight) で design の h43 を保てる。
            PlaceAt(songTitle.gameObject, 0f, 1f, new Vector2(320f, 43f));
            V5Ui.FitEllipsisToLineHeight(songTitle, 43f);
            songSub = V5Ui.Text(songCol, "Sub", "", 22f, V5Tokens.TextMuted, TextAlignmentOptions.Left);
            songSub.textWrappingMode = TextWrappingModes.NoWrap;
            songSub.overflowMode = TextOverflowModes.Ellipsis;
            PlaceAt(songSub.gameObject, 0f, 48f, new Vector2(320f, 32f));
            // fs22 の行送りは 31.86 で h32 との余裕が 0.14px しかないため確保する。
            V5Ui.FitEllipsisToLineHeight(songSub, 32f);

            var divider = V5Ui.Panel(info, "Divider", new Vector2(1f, 44f), new Color(1f, 1f, 1f, 0x26 / 255f), 0f);
            PlaceAt(divider, 432f, 26f);

            levelValue = BuildStat(info, "LEVEL", 445f);
            notesValue = BuildStat(info, "NOTES", 557f);
            durationValue = BuildStat(info, "曲長", 669f);
        }

        /// <summary>右上の統計 1 個 (Label fs18 dim / Value fs30 w500)。値の TMP を返す。</summary>
        private TextMeshProUGUI BuildStat(GameObject parent, string label, float x)
        {
            var col = V5Ui.Node(parent, "Stat " + label);
            PlaceAt(col, x, 8f, new Vector2(100f, 80f));
            var labelTmp = V5Ui.Text(col, "Label", label, 18f, V5Tokens.TextDim, TextAlignmentOptions.Left);
            PlaceAt(labelTmp.gameObject, 0f, 0f, new Vector2(100f, 26f));
            var value = V5Ui.Text(col, "Value", "", 30f, V5Tokens.Text, TextAlignmentOptions.Left,
                FontStyles.Bold);
            value.textWrappingMode = TextWrappingModes.NoWrap;
            PlaceAt(value.gameObject, 0f, 28f, new Vector2(100f, 43f));
            return value;
        }

        private void BuildMain()
        {
            var main = V5Ui.Node(frame.gameObject, "Main");
            PlaceAt(main, MainX, MainY, new Vector2(1680f, 780f));

            BuildTabs(main);
            BuildSettingsPanel(main);
            BuildPreviewPanel(main);
        }

        private void BuildTabs(GameObject main)
        {
            var tabs = V5Ui.Node(main, "Tabs");
            PlaceAt(tabs, 438f, 0f, new Vector2(824f, 48f));
            var layout = tabs.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 8f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            for (int i = 0; i < V6PauseViewLogic.TabCount; i++)
            {
                var tab = V5Ui.Panel(tabs, "Tab " + TabNames[i], new Vector2(200f, 48f), V5Tokens.Key2, 8f);
                LayoutItem(tab, 200f, 48f);
                var label = V5Ui.Text(tab, "Label", TabNames[i], 24f, V5Tokens.TextMuted,
                    TextAlignmentOptions.Center);
                Stretch(label.rectTransform);
                var button = tab.AddComponent<Button>();
                button.targetGraphic = tab.GetComponent<Image>();
                int captured = i;
                button.onClick.AddListener(() => SelectTab(captured));
                tabPanels.Add(tab);
                tabLabels.Add(label);
            }
        }

        private void BuildSettingsPanel(GameObject main)
        {
            var panel = V5Ui.Panel(main, "Settings", new Vector2(512f, 688f), V5Tokens.Key2, 16f);
            PlaceAt(panel, 304f, 68f);

            settingsTitle = V5Ui.Text(panel, "Head Title", TabNames[0], 18f, V5Tokens.TextDim,
                TextAlignmentOptions.Left);
            PlaceAt(settingsTitle.gameObject, 8f, 20f, new Vector2(472f, 24f));

            for (int page = 0; page < V6PauseViewLogic.TabCount; page++)
            {
                var pageRoot = V5Ui.Node(panel, "Page " + TabNames[page]);
                PlaceAt(pageRoot, 8f, 60f, new Vector2(496f, 620f));
                pages.Add(pageRoot);
                pageRows.Add(BuildPage(pageRoot, page));
            }
        }

        /// <summary>1 タブ分の設定行を作る。行は design: 496×56 $k1 r8、y = 76k。</summary>
        private List<Row> BuildPage(GameObject pageRoot, int page)
        {
            var rows = new List<Row>();
            if (page == 0)
            {
                AddRow(rows, pageRoot, 0, "NoteSpeed",
                    () => V6PauseViewLogic.FormatNoteSpeed(GManager.noteSpeed),
                    dir =>
                    {
                        // 範囲は Option 画面 / Select の Ready と同じ OptionSettings (1..50)。
                        GManager.noteSpeed = PauseMenuView.NextNoteSpeed(GManager.noteSpeed, dir);
                        OptionSettings.Save();
                    });
                AddRow(rows, pageRoot, 1, "NoteTiming",
                    () => V6PauseViewLogic.FormatNoteTiming(GManager.noteTiming),
                    dir =>
                    {
                        GManager.noteTiming = Mathf.Clamp(GManager.noteTiming + dir, -10, 10);
                        OptionSettings.Save();
                    });
                // 正本 uDNC0 の Play タブは 4 行: NoteSpeed / NoteTiming / Key Layout / キー設定 (F2)。
                // 2026-11-11 まで Key Layout と キー設定 の 2 行を「開けないから」と描いて
                // いなかったが、正本に在る行を落とすのは移植の欠落。KeyConfigUI は
                // ポーズ中 (PauseGate.IsPaused) の GameScene でも開けるようにしたので、
                // 2 行とも実動作 (編集 / F2) に接続する。
                AddRow(rows, pageRoot, 2, "Key Layout",
                    () => IsCustomLayout() ? "カスタム" : "標準",
                    dir =>
                    {
                        if (dir > 0)
                        {
                            OpenKeyConfig(); // 編集はキー設定で行う (Option と同じ)
                        }
                        else
                        {
                            RequestResetKeyLayout(); // 破壊的なリセットは確認してから
                        }
                    },
                    OpenKeyConfig);
                AddRow(rows, pageRoot, 3, "キー設定", () => "F2", null, OpenKeyConfig);
            }
            else if (page == 1)
            {
                AddRow(rows, pageRoot, 0, "Key Guide",
                    () => PlaySession.KeyGuideLabel(PlaySession.KeyGuide),
                    dir => PlaySession.SetKeyGuide(PlaySession.NextKeyGuide(PlaySession.KeyGuide, dir)));
                AddRow(rows, pageRoot, 1, "Lyrics",
                    () => PlaySession.LyricsLabel(PlaySession.LyricsEnabled),
                    _ =>
                    {
                        PlaySession.SetLyricsEnabled(!PlaySession.LyricsEnabled);
                        if (sceneManager != null)
                        {
                            sceneManager.ApplyLyricsVisibility();
                        }
                    });
            }
            else if (page == 2)
            {
                AddRow(rows, pageRoot, 0, "MainVolume",
                    () => V6PauseViewLogic.FormatVolume(GManager.mainVolume),
                    dir => AdjustVolume(dir, VolumeTarget.Main));
                // 「BGMVolume」の行は廃止 (issue-162 B-11 再照合): 動画音声は
                // MainVolume に従い (MusicManager)、BGMVolume の実行時消費者は
                // GameScene に居ない (VideoTime は配置されていない)。
                // 押しても効かない行は描画しない (Key Layout 行の廃止と同じ規律)。
                AddRow(rows, pageRoot, 1, "SEVolume",
                    () => V6PauseViewLogic.FormatVolume(GManager.effectVolume),
                    dir => AdjustVolume(dir, VolumeTarget.Se));
            }
            else
            {
                AddRow(rows, pageRoot, 0, "この曲のベスト",
                    () => GameHudLogic.FormatScore(BestScore()),
                    null);
                AddRow(rows, pageRoot, 1, "記録済みの曲",
                    () => PlayedSongs() + "曲",
                    null);
                // 演奏中の実データ (GManager の静的カウンタ。ポーズ中も毎フレーム再描画される)。
                AddRow(rows, pageRoot, 2, "スコア",
                    () => GameHudLogic.FormatScore(GManager.score),
                    null);
                AddRow(rows, pageRoot, 3, "コンボ",
                    () => GManager.combo.ToString(),
                    null);
                AddRow(rows, pageRoot, 4, "判定",
                    () => V6PauseViewLogic.FormatJudgements(GManager.perfect, GManager.great, GManager.bad, GManager.miss),
                    null);
                AddRow(rows, pageRoot, 5, "進捗",
                    () => V6PauseViewLogic.FormatProgress(JudgedNotes(), TotalNotes()),
                    null);
            }

            return rows;
        }

        /// <summary>KeyBindings 未初期化 (null) は既定扱い (IsCustomLayout は null を
        /// 「既定と異なる」と扱うのでここで防ぐ)。</summary>
        private static bool IsCustomLayout()
        {
            return KeyBindings.Current != null && KeyConfigUI.IsCustomLayout();
        }

        /// <summary>Volume の行の値変更 (GManager へ書き、音も即時反映)。</summary>
        private enum VolumeTarget
        {
            Main = 0,
            Se = 2,
        }

        private void AdjustVolume(int dir, VolumeTarget target)
        {
            const float Step = 0.05f;
            switch (target)
            {
                case VolumeTarget.Main:
                    GManager.mainVolume = Mathf.Clamp01(GManager.mainVolume + dir * Step);
                    break;
                case VolumeTarget.Se:
                    GManager.effectVolume = Mathf.Clamp01(GManager.effectVolume + dir * Step);
                    break;
            }

            ApplyVolumes();
            OptionSettings.Save();
        }

        /// <summary>音量は各所有者が起動時にしか読まないので、シーン内の AudioSource にも即時反映
        /// (v5 PauseMenuView.ApplyVolumes と同じ挙動)。</summary>
        private void ApplyVolumes()
        {
            AudioListener.volume = GManager.mainVolume;

            var music = UnityEngine.Object.FindFirstObjectByType<MusicManager>();
            if (music != null)
            {
                var src = music.GetComponent<AudioSource>();
                if (src != null)
                {
                    src.volume = GManager.mainVolume;
                }
            }

            var judge = UnityEngine.Object.FindFirstObjectByType<Judge>();
            if (judge != null)
            {
                var src = judge.GetComponent<AudioSource>();
                if (src != null)
                {
                    src.volume = GManager.effectVolume;
                }
            }
        }

        private static int BestScore()
        {
            int id = GManager.songID;
            if (id >= 0 && id < GManager.Hiscore.Length)
            {
                return GManager.Hiscore[id];
            }

            // 固定テーブル外 (cloud 追加曲) は安定キーの保存先から読む (issue-162 A-6)。
            return GManager.BestScoreOf(id);
        }

        private static int PlayedSongs()
        {
            int played = 0;
            for (int i = 1; i < GManager.Hiscore.Length; i++)
            {
                if (GManager.Hiscore[i] > 0)
                {
                    played++;
                }
            }

            return played;
        }

        /// <summary>判定済みノーツ数 (進捗の分子)。GManager の判定カウンタの和。</summary>
        private static int JudgedNotes()
        {
            return GManager.perfect + GManager.great + GManager.bad + GManager.miss;
        }

        private NotesManager notesManager;

        /// <summary>譜面の総ノーツ数 (進捗の分母)。NotesManager.noteNum (生成した実ノーツ数)。</summary>
        private int TotalNotes()
        {
            if (notesManager == null)
            {
                notesManager = UnityEngine.Object.FindFirstObjectByType<NotesManager>();
            }

            return notesManager != null ? notesManager.noteNum : 0;
        }

        private void AddRow(List<Row> rows, GameObject pageRoot, int row, string label,
            Func<string> read, Action<int> adjust, Action click = null)
        {
            var panel = V5Ui.Panel(pageRoot, "Row " + label, new Vector2(496f, 56f), V5Tokens.Key1, 8f);
            PlaceAt(panel, 0f, 76f * row, new Vector2(496f, 56f));

            var layout = panel.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 12f;
            layout.padding = new RectOffset(0, Mathf.RoundToInt(12f), 0, 0);
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var labelTmp = V5Ui.Text(panel, "Label", label, 24f, V5Tokens.Text, TextAlignmentOptions.Left);
            LayoutItem(labelTmp.gameObject, 200f, 30f);

            // Spacer (design: fill_container)
            var spacer = V5Ui.Node(panel, "Spacer");
            var spacerLe = spacer.AddComponent<LayoutElement>();
            spacerLe.flexibleWidth = 1f;
            spacerLe.preferredHeight = 30f;

            var value = V5Ui.Text(panel, "Value", read != null ? read() : "", 24f, V5Tokens.Text,
                TextAlignmentOptions.Right);
            value.textWrappingMode = TextWrappingModes.NoWrap;
            LayoutItem(value.gameObject, 120f, 30f);

            if (adjust != null)
            {
                Button prev = MakeStepper(panel, "Prev", "chevron-left");
                int capturedRow = rows.Count;
                prev.onClick.AddListener(() =>
                {
                    SelectRow(selectedTab, capturedRow);
                    adjust(-1);
                    RefreshRows();
                });
                Button next = MakeStepper(panel, "Next", "chevron-right");
                next.onClick.AddListener(() =>
                {
                    SelectRow(selectedTab, capturedRow);
                    adjust(1);
                    RefreshRows();
                });
            }

            var record = new Row
            {
                Value = value,
                Read = read,
                Adjust = adjust,
            };

            var button = panel.AddComponent<Button>();
            button.targetGraphic = panel.GetComponent<Image>();
            int capturedIndex = rows.Count;
            button.onClick.AddListener(() =>
            {
                SelectRow(selectedTab, capturedIndex);
                if (click != null)
                {
                    click();
                }
            });

            rows.Add(record);
        }

        /// <summary>design: 40×40 $k4 r8 のステッパー (icon 24 $text-muted、lucide PNG)。</summary>
        private Button MakeStepper(GameObject parent, string name, string iconName)
        {
            var btn = V5Ui.Panel(parent, name, new Vector2(40f, 40f), V5Tokens.Key4, 8f);
            LayoutItem(btn, 40f, 40f);
            var icon = V6Icons.Create(btn, iconName, 24f, V5Tokens.TextMuted, "Icon");
            if (icon != null)
            {
                // Panel はレイアウトグループを持たないので中央に置く (V6Icons の既定)。
                icon.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                icon.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                icon.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                icon.rectTransform.anchoredPosition = Vector2.zero;
            }
            var button = btn.AddComponent<Button>();
            button.targetGraphic = btn.GetComponent<Image>();
            return button;
        }

        private void BuildPreviewPanel(GameObject main)
        {
            var panel = V5Ui.Panel(main, "Preview", new Vector2(512f, 688f), V5Tokens.Key2, 16f);
            PlaceAt(panel, 864f, 68f);

            previewHeadTitle = V5Ui.Text(panel, "Head Title", "", 18f, V5Tokens.TextDim,
                TextAlignmentOptions.Left);
            PlaceAt(previewHeadTitle.gameObject, 8f, 20f, new Vector2(472f, 24f));

            BuildGamePreview(panel);
            BuildKeyList(panel);

            var divider = V5Ui.Panel(panel, "Divider", new Vector2(464f, 1f), new Color(1f, 1f, 1f, 0x1F / 255f), 0f);
            PlaceAt(divider, 24f, 544f);

            var descTitle = V5Ui.Text(panel, "Desc Title", "キー設定", 32f, V5Tokens.Text,
                TextAlignmentOptions.Left, FontStyles.Bold);
            PlaceAt(descTitle.gameObject, 24f, 564f, new Vector2(464f, 40f));

            // design (uDNC0) の文言そのまま。ポーズ中 (PauseGate.IsPaused) は
            // KeyConfigUI を GameScene からも開けるので F2 の案内は実動作と一致する。
            var desc = V5Ui.Text(panel, "Desc", "キーの割り当てを編集します。F2 でも開けます。",
                24f, V5Tokens.Text, TextAlignmentOptions.TopLeft);
            var descRt = desc.rectTransform;
            descRt.anchorMin = new Vector2(0f, 1f);
            descRt.anchorMax = new Vector2(0f, 1f);
            descRt.pivot = new Vector2(0f, 1f);
            descRt.anchoredPosition = new Vector2(24f, -614f);
            descRt.sizeDelta = new Vector2(464f, 68f);
            // design Desc (fs24 / lineHeight 1.4) は 464 幅で 2 行折り返し。
            // V5Ui.Text は 1 行描画用に lineSpacing を -44.8 (fs 分) にしているので、
            // 40 を足すと行送り 34.75 + 24×0.40 = 44.35px になり、design 実測 (uDNC0:
            // 621→659 = 38px) より 2 行目が 11px 下がる (r24 実機)。0 で 34.75px。
            desc.textWrappingMode = TextWrappingModes.Normal;
            desc.lineSpacing = 0f;
        }

        /// <summary>design: 464×280 のゲームプレビュー (8 レーンの遠近線 + ノーツ + 判定線 + レーン番号)。</summary>
        private void BuildGamePreview(GameObject panel)
        {
            var preview = V5Ui.Panel(panel, "Game Preview", new Vector2(464f, 280f), V5Tokens.Key0, 8f);
            PlaceAt(preview, 24f, 60f);

            // design の Lane パス (M26 234l174.5-234 など) の両端点。y は design 上端基準。
            for (int lane = 0; lane < 8; lane++)
            {
                float bottomX = 26f + 58f * lane;
                float topX = 200.5f + 9f * lane;
                Line(preview, "Lane " + lane,
                    new Vector2(bottomX, -234f), new Vector2(topX, 0f),
                    1f, new Color(1f, 1f, 1f, 0x24 / 255f));
            }

            // ノーツ (design のサンプル配置)
            float[,] notes =
            {
                { 62f, 70f, 33f }, { 205f, 130f, 21f }, { 273f, 96f, 27f }, { 291f, 186f, 14f },
                { 204f, 210f, 14f }, { 327f, 58f, 35f }, { 150f, 105f, 20f }, { 390f, 150f, 22f },
            };
            for (int i = 0; i < notes.GetLength(0); i++)
            {
                var note = V5Ui.Panel(preview, "Note " + i,
                    new Vector2(notes[i, 2], 10f), V5Tokens.ButtonPrimaryBg, 0f);
                PlaceAt(note, notes[i, 0], notes[i, 1]);
            }

            var judgeLine = V5Ui.Panel(preview, "Judge Line", new Vector2(464f, 3f), V5Tokens.ButtonPrimaryBg, 0f);
            PlaceAt(judgeLine, 0f, 240f);

            for (int lane = 0; lane < 8; lane++)
            {
                var label = V5Ui.Text(preview, "Lane Label " + lane, (lane + 1).ToString(), 18f,
                    V5Tokens.TextMuted, TextAlignmentOptions.Center);
                PlaceAt(label.gameObject, 6f + 58f * lane, 246f, new Vector2(40f, 22f));
            }
        }

        /// <summary>design: JIS 4 段のキー一覧 (34×40)。刻印は KeyBindings の実割当 (所属レーン番号)。</summary>
        private void BuildKeyList(GameObject panel)
        {
            for (int row = 0; row < V6PauseViewLogic.JisKeyRows.Length; row++)
            {
                char[] keys = V6PauseViewLogic.JisKeyRows[row];
                for (int i = 0; i < keys.Length; i++)
                {
                    var cap = V5Ui.Panel(panel, "Key " + keys[i], new Vector2(34f, 40f), V5Tokens.Key4, 4f);
                    PlaceAt(cap, V6PauseViewLogic.JisKeyRowX[row] + 36f * i, V6PauseViewLogic.JisKeyRowY[row]);
                    var label = V5Ui.Text(cap, "Lane", "", 20f, V5Tokens.Text, TextAlignmentOptions.Center);
                    Stretch(label.rectTransform);
                    keyCaps.Add(cap.GetComponent<Image>());
                    keyLabels.Add(label);
                }
            }
        }

        private void BuildFooter()
        {
            var footer = V5Ui.Node(frame.gameObject, "Footer");
            PlaceAt(footer, FooterX, FooterY, new Vector2(FooterW, FooterH));

            // 左下: キー凡例 4 個 (正本 uDNC0 Zone · キー操作: ↑↓ 選択 / ←→ 変更 /
            // タブ切替 / 再開)。起点は design (フッター内 (0,30)) =
            // ページ (24,906) (V6PauseViewLogic.LegendX/Y)。項目は共通部品
            // V6Legend.Item に座標を渡すだけ (Common は編集しない)。
            // 実際のキーは V6GameRuntime.UpdatePause と一致させる:
            // ↑↓ = 行選択 (MoveSelection) / ←→ = 値の変更 (AdjustSelected) /
            // タブ切替 = KeyAction.MenuSwitch (既定 T。design のキャップは "Tab" だが、
            // 実際に動くキーを表示する) / 再開 = KeyAction.Back。
            // キャップはキー設定 (KeyActions) から取る。固定文字列にすると再割当後に
            // 画面が嘘をつく (Title Exit / Select の決定キーと同じ扱い)。
            // 矢印は KeyActions に ↑↓ の割当が無いため固定キー (Title の H/R と同じ扱い)。
            float legendX = V6PauseViewLogic.LegendX - FooterX;
            float legendY = V6PauseViewLogic.LegendY - FooterY;
            legendX = V6Legend.Item(footer, legendX, legendY, "↑↓", "選択");
            legendX = V6Legend.Item(footer, legendX, legendY, "←→", "変更");
            legendX = V6Legend.Item(footer, legendX, legendY,
                KeyCap(KeyActions.KeyAction.MenuSwitch), "タブ切替");
            V6Legend.Item(footer, legendX, legendY,
                KeyCap(KeyActions.KeyAction.Back), "再開");

            // 右下: design の Op · 選曲・リトライ (520 = 選曲へ 280 + gap16 + リトライ 220、
            // リトライは独立ボタンのまま左ボタンへ統合しない) + Op · 再開 266。
            // 座標は design ページ座標 (V6PauseViewLogic)。
            var opGroup = V5Ui.Node(footer, "Op · 選曲・リトライ");
            PlaceAt(opGroup, V6PauseViewLogic.OpGroupX - FooterX, V6PauseViewLogic.OpButtonsY - FooterY,
                new Vector2(V6PauseViewLogic.OpGroupW, V6PauseViewLogic.OpButtonH));

            BuildActionButton(opGroup, "選曲へ", "log-out",
                KeyCap(KeyActions.KeyAction.MenuRetire), false,
                0f, 0f, V6PauseViewLogic.RetireButtonW, () =>
                {
                    Close();
                    if (sceneManager != null)
                    {
                        sceneManager.RetireGame();
                    }
                });
            BuildActionButton(opGroup, "リトライ", "rotate-ccw",
                KeyCap(KeyActions.KeyAction.MenuRetry), false,
                V6PauseViewLogic.RetireButtonW + V6PauseViewLogic.OpGroupGap, 0f,
                V6PauseViewLogic.RetryButtonW, () =>
                {
                    Close();
                    if (sceneManager != null)
                    {
                        sceneManager.RetryGame();
                    }
                });
            BuildActionButton(footer, "再開", "play",
                KeyCap(KeyActions.KeyAction.Back), true,
                V6PauseViewLogic.ResumeButtonX - FooterX, V6PauseViewLogic.OpButtonsY - FooterY,
                V6PauseViewLogic.ResumeButtonW, RequestResume);
        }

        /// <summary>
        /// ボタン / 凡例のキーキャップをキー設定 (KeyActions) から取る。
        /// 固定文字列だと再割当後に画面が実際のキーと食い違う。
        /// <c>ActionKeyHint.Primary</c> は Backspace を "BS" に縮めるが、design の
        /// キャップは "Backspace" なので <c>KeyName</c> を使う。
        /// </summary>
        private static string KeyCap(KeyActions.KeyAction action)
        {
            KeyCode[] keys = KeyActions.Get(action);
            for (int i = 0; i < keys.Length; i++)
            {
                if (keys[i] != KeyCode.None)
                {
                    return ActionKeyHint.KeyName(keys[i]);
                }
            }

            return "—";
        }

        /// <summary>
        /// design: 選曲へ 280×56 / リトライ 220×56 ($k4) / 再開 266×56 (#EDEDED)。
        /// アイコン 32×32 とラベル・KeyCap は設計の絶対座標へ置く (レイアウトグループで
        /// 詰めると "Backspace" / "リトライ" が折り返して 2 行に潰れる)。
        /// iconName は design の lucide 名そのまま (log-out / rotate-ccw / play、
        /// Resources/Icons/ に PNG あり)。x / y は親ノード内の design 座標
        /// (フッター直下なら (1414,20)、Op グループ内なら (0,0) / (296,0))。
        /// </summary>
        private void BuildActionButton(GameObject parent, string label, string iconName, string key,
            bool primary, float x, float y, float width, Action action)
        {
            var go = V5Ui.Panel(parent, "Button " + label, new Vector2(width, V6PauseViewLogic.OpButtonH),
                primary ? V5Tokens.ButtonPrimaryBg : V5Tokens.Key4, 8f);
            PlaceAt(go, x, y, new Vector2(width, V6PauseViewLogic.OpButtonH));

            Color labelColor = primary ? V5Tokens.ButtonPrimaryLabel : V5Tokens.Text;
            Color iconColor = primary ? V5Tokens.ButtonPrimaryLabel : V5Tokens.TextMuted;

            // 構成 (design の子の x): icon / Label / KeyCap を横に並べる。
            // 中央寄せは各ボタンの幅から計算する (設計値: 選曲へ 90+125 / リトライ 120+30 / 再開 60+51)。
            float labelW = V6Layout.EstimateTextWidth(label, 30f);
            float capW = Mathf.Max(30f, V6Layout.EstimateTextWidth(key, 22f) + 16f);
            float contentW = 32f + 12f + labelW + 12f + capW;
            float startX = Mathf.Max(0f, (width - contentW) * 0.5f);
            float labelX = startX + 32f + 12f;
            float capX = labelX + labelW + 12f;

            var icon = V6Icons.Create(go, iconName, 32f, iconColor, "Icon");
            if (icon != null)
            {
                PlaceAt(icon, startX, 12f, new Vector2(32f, 32f));
            }

            var labelTmp = V5Ui.Text(go, "Label", label, 30f, labelColor, TextAlignmentOptions.Left,
                primary ? FontStyles.Bold : FontStyles.Normal);
            labelTmp.textWrappingMode = TextWrappingModes.NoWrap;
            PlaceAt(labelTmp.gameObject, labelX, 6.5f, new Vector2(labelW + 2f, 43f));

            var cap = V5Ui.Panel(go, "KeyCap " + key, new Vector2(capW, 32f),
                primary ? new Color(0f, 0f, 0f, 0x26 / 255f) : new Color(0f, 0f, 0f, 0x40 / 255f), 4f);
            PlaceAt(cap, capX, 12f, new Vector2(capW, 32f));
            var keyTmp = V5Ui.Text(cap, "Key", key, 22f,
                primary ? V5Tokens.ButtonPrimaryLabel : V5Tokens.TextMuted, TextAlignmentOptions.Center);
            keyTmp.textWrappingMode = TextWrappingModes.NoWrap;
            Stretch(keyTmp.rectTransform);

            var button = go.AddComponent<Button>();
            button.targetGraphic = go.GetComponent<Image>();
            button.onClick.AddListener(() => action());
        }

        /// <summary>2 点を結ぶ細長い Image (design の Lane path / stroke の代用)。</summary>
        private static void Line(GameObject parent, string name, Vector2 from, Vector2 to, float width, Color color)
        {
            var go = V5Ui.Panel(parent, name, new Vector2(1f, 1f), color, 0f);
            var rt = GetRect(go);
            Vector2 delta = to - from;
            float length = delta.magnitude;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = from;
            rt.sizeDelta = new Vector2(length, width);
            float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
            rt.localRotation = Quaternion.Euler(0f, 0f, angle);
        }

        // --- 表示の更新 ---

        private void RefreshHeader()
        {
            var song = GManager.GetSong(GManager.songID);
            songTitle.text = song != null ? song.title : "";
            songSub.text = V6PauseViewLogic.SongSub(song);
            levelValue.text = song != null ? song.level.ToString() : "0";
            notesValue.text = V6PauseViewLogic.FormatNotes(song);
            durationValue.text = V6PauseViewLogic.FormatDuration(song != null ? song.duration : 0f);

            string videoId = song != null ? song.videoId : null;
            if (!string.IsNullOrEmpty(videoId) && loadedVideoId != videoId && driver != null)
            {
                loadedVideoId = videoId;
                driver.StartCoroutine(FetchJacket(videoId));
            }
        }

        /// <summary>ジャケットは YouTube サムネイル (実曲)。失敗時はプレースホルダのまま
        /// (v5 GameReadyView.FetchJacket と同じ方式)。</summary>
        private IEnumerator FetchJacket(string videoId)
        {
            if (thumbImage != null)
            {
                thumbImage.sprite = null;
                thumbImage.color = V5Tokens.Key2;
            }

            if (string.IsNullOrEmpty(videoId))
            {
                yield break;
            }

            var url = "https://img.youtube.com/vi/" + videoId + "/hqdefault.jpg";
            using (var req = UnityEngine.Networking.UnityWebRequestTexture.GetTexture(url))
            {
                yield return req.SendWebRequest();
                if (req.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
                {
                    yield break;
                }

                var tex = UnityEngine.Networking.DownloadHandlerTexture.GetContent(req);
                var sprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height),
                    new Vector2(0.5f, 0.5f), 100f);
                if (thumbImage != null && loadedVideoId == videoId)
                {
                    thumbImage.sprite = sprite;
                    thumbImage.preserveAspect = true;
                    thumbImage.color = Color.white;
                }
            }
        }

        private void RefreshTabs()
        {
            for (int i = 0; i < tabPanels.Count && i < tabLabels.Count; i++)
            {
                bool active = i == selectedTab;
                var img = tabPanels[i].GetComponent<Image>();
                if (img != null)
                {
                    img.color = active ? V5Tokens.Key6 : V5Tokens.Key2;
                }

                tabLabels[i].color = active ? V5Tokens.Text : V5Tokens.TextMuted;
                tabLabels[i].fontStyle = active ? FontStyles.Bold : FontStyles.Normal;
            }

            for (int i = 0; i < pages.Count; i++)
            {
                pages[i].SetActive(i == selectedTab);
            }

            if (settingsTitle != null && selectedTab < TabNames.Length)
            {
                settingsTitle.text = TabNames[selectedTab];
            }
        }

        private void RefreshRows()
        {
            for (int page = 0; page < pageRows.Count; page++)
            {
                foreach (var row in pageRows[page])
                {
                    if (row.Read != null)
                    {
                        row.Value.text = row.Read();
                    }
                }
            }

            RefreshRowHighlight();
        }

        private void RefreshRowHighlight()
        {
            for (int page = 0; page < pageRows.Count; page++)
            {
                var rows = pageRows[page];
                for (int i = 0; i < rows.Count; i++)
                {
                    var panel = rows[i].Value.transform.parent.gameObject;
                    var img = panel.GetComponent<Image>();
                    if (img == null)
                    {
                        continue;
                    }

                    bool selected = page == selectedTab && i == selectedRow;
                    img.color = selected ? V5Tokens.Key6 : V5Tokens.Key1;
                }
            }
        }

        private void SelectRow(int page, int index)
        {
            if (page != selectedTab)
            {
                SelectTab(page);
                return;
            }

            selectedRow = V6PauseViewLogic.ClampIndex(index, pageRows[selectedTab].Count);
            RefreshRowHighlight();
        }

        /// <summary>KeyBindings の変更 (キー設定 / レイアウト) をプレビューへ即時反映。</summary>
        private void RefreshKeyBindings()
        {
            if (previewHeadTitle != null)
            {
                previewHeadTitle.text = "キー割当 (JIS / " + V6PauseViewLogic.LayoutLabel(IsCustomLayout()) + ")";
            }

            KeyCode[][] bindings = KeyBindings.Current;
            for (int i = 0; i < keyLabels.Count && i < keyCaps.Count; i++)
            {
                char c = KeyCharAt(i);
                int lane = V6PauseViewLogic.LaneOfKey(bindings, c);
                keyLabels[i].text = lane >= 0 ? (lane + 1).ToString() : "";
                if (keyCaps[i] != null)
                {
                    keyCaps[i].color = lane >= 0 ? V5Tokens.Key4 : V5Tokens.Key2;
                }
            }
        }

        /// <summary>JIS 全段を通しで並べたときの i 番目の刻印。</summary>
        private static char KeyCharAt(int index)
        {
            int i = index;
            for (int row = 0; row < V6PauseViewLogic.JisKeyRows.Length; row++)
            {
                char[] keys = V6PauseViewLogic.JisKeyRows[row];
                if (i < keys.Length)
                {
                    return keys[i];
                }

                i -= keys.Length;
            }

            return '\0';
        }

        // --- 配置 ---

        /// <summary>
        /// 共通フレームを 1:1 (等倍) で画面中央に置く。
        /// 原点は <see cref="V6PauseViewLogic.FrameOrigin"/> ((w-1728)/2, (h-972)/2) の切捨て —
        /// design "Resolution Check · Game (PAUSE)" の各解像度配置と同じ。
        /// 等比拡大 (min(W/1728, H/972)) は行わない (Owner 指示: 引き伸ばしはバグ)。
        /// </summary>
        private void UpdatePlacement()
        {
            if (frame == null)
            {
                return;
            }

            int w = Screen.width;
            int h = Screen.height;
            if (w == lastWidth && h == lastHeight)
            {
                return;
            }

            lastWidth = w;
            lastHeight = h;
            Vector2 origin = V6PauseViewLogic.FrameOrigin(w, h);
            frame.localScale = Vector3.one;
            // anchor/pivot (0,0) = 親 (全画面 root) の左下基準。design 原点は左上なので
            // y は下端から: h - origin.y - 972 (= (h-972)/2 の切り上げ側)。
            frame.anchoredPosition = new Vector2(origin.x, h - origin.y - FrameH);
        }

        internal void Tick()
        {
            UpdatePlacement();
            if (open)
            {
                Refresh();
            }
        }

        // --- ユーティリティ ---

        /// <summary>design 座標 (上端基準 y) で配置する (anchor は親の左上)。</summary>
        private static RectTransform PlaceAt(Component node, float x, float y, Vector2? size = null)
        {
            return PlaceAt(node.gameObject, x, y, size);
        }

        private static RectTransform PlaceAt(GameObject node, float x, float y, Vector2? size = null)
        {
            var rt = node.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            if (size.HasValue)
            {
                rt.sizeDelta = size.Value;
            }

            return rt;
        }

        /// <summary>レイアウトグループの子にする (minWidth 0 = 内容に合わせる)。</summary>
        private static void LayoutItem(GameObject go, float minWidth, float height)
        {
            var le = go.AddComponent<LayoutElement>();
            if (minWidth > 0f)
            {
                le.minWidth = minWidth;
                le.preferredWidth = minWidth;
            }

            le.preferredHeight = height;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static RectTransform GetRect(Component component)
        {
            return component.GetComponent<RectTransform>();
        }

        private static RectTransform GetRect(GameObject go)
        {
            return go.GetComponent<RectTransform>();
        }
    }

    /// <summary>V6PauseView の毎フレーム処理 (1:1 配置 + 実データの再描画)。</summary>
    internal sealed class V6PauseViewDriver : MonoBehaviour
    {
        internal V6PauseView View;

        private void Update()
        {
            if (View != null)
            {
                View.Tick();
            }
        }
    }
}
