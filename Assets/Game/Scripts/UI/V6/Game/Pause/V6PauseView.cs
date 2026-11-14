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
    ///
    /// Issue #385 Phase 2.3: 領域ごとに partial class へ分割した
    /// (uGUI 構築 = V6PauseViewBuild.cs、確認ダイアログ = V6PauseViewDialogs.cs、
    /// 設定値・実データ参照 = V6PauseViewSettings.cs、表示更新 = V6PauseViewRefresh.cs、
    /// 毎フレーム駆動 = V6PauseViewDriver.cs)。このファイルはライフサイクル・
    /// 公開操作 API・設定行定義 (BuildPage)・1:1 配置を持つ。
    /// BuildPage は V6UiWiringTests がこのファイルをソース走査して検証するため、
    /// 行定義はここに残す (移すとテストが落ちる)。
    /// </summary>
    public sealed partial class V6PauseView
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

        // ヘッダー帯の寸法は V6Header (Width/Height/FramePos) が正本。
        private const float MainX = 24f, MainY = 96f;
        private const float FooterX = 24f, FooterY = 876f, FooterW = 1680f, FooterH = 96f;

        private static readonly string[] TabNames = { "Play", "Display", "Sound", "Data" };

        /// <summary>設定行の定義。値の読み書きはすべて実データへの委譲。</summary>
        private sealed class Row
        {
            public string Label;
            public TextMeshProUGUI Value;
            public Func<string> Read;
            public Action<int> Adjust;
            /// <summary>行クリック時の動作 (キー設定を開く等)。null なら決定しても何も起きない。</summary>
            public Action Click;
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

        /// <summary>右パネルのキー一覧 (Play タブのみ表示)。</summary>
        private GameObject keyListRoot;

        /// <summary>Sound タブの共通音量スライダー (Issue #375)。</summary>
        private V6VolumeSlider volumeSlider;

        /// <summary>右パネル下部の説明 (タブごとに文言を差し替える)。</summary>
        private TextMeshProUGUI previewDescTitle;
        private TextMeshProUGUI previewDesc;

        /// <summary>音量スライダーを出すタブ (Sound)。</summary>
        private const int VolumePage = 2;

        /// <summary>キー設定モーダル (Issue #376、design vo4kB)。フレーム直下に 1 個だけ持つ。</summary>
        private V6KeyConfigModal keyConfigModal;

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
            if (keyConfigModal != null)
            {
                keyConfigModal.Hide(); // 編集は破棄 (ポーズを閉じたらモーダルも残さない)
            }
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
            if (keyConfigModal != null)
            {
                keyConfigModal.Hide();
            }
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
            RefreshPreviewForPage();
        }

        /// <summary>
        /// 右パネルの出し分け (Issue #375): Play = キー一覧 / Sound = 音量スライダー。
        /// 音量スライダーは選択行 (Main / SE) の値へ追従する。
        /// </summary>
        private void RefreshPreviewForPage()
        {
            bool sound = selectedTab == VolumePage;
            if (keyListRoot != null)
            {
                keyListRoot.SetActive(!sound);
            }

            if (volumeSlider != null)
            {
                volumeSlider.gameObject.SetActive(sound);
                volumeSlider.Refresh();
            }

            if (previewHeadTitle != null)
            {
                previewHeadTitle.text = sound
                    ? "音量"
                    : "キー割当 (JIS / " + V6PauseViewLogic.LayoutLabel(IsCustomLayout()) + ")";
            }

            if (previewDescTitle != null)
            {
                previewDescTitle.text = sound ? "音量" : "キー設定";
            }

            if (previewDesc != null)
            {
                previewDesc.text = sound
                    ? "スライダーをドラッグするか ←→ で 1% (Shift で 10%) 調整します。変更は即時に音へ反映されます。"
                    : "キーの割り当てを編集します。行を選んで Enter を押してください。";
            }
        }

        // --- 統合担当がキー入力 (矢印 / Tab) から呼ぶ操作 API ---

        /// <summary>設定行の選択を移動する (↑↓ / 正本 uDNC0 凡例「選択」)。</summary>
        public void MoveSelection(int delta)
        {
            selectedRow = V6PauseViewLogic.ClampIndex(selectedRow + delta, pageRows[selectedTab].Count);
            RefreshRowHighlight();
            // 音量スライダーは選択行の値へ追従する (Issue #375)。
            if (volumeSlider != null && selectedTab == VolumePage)
            {
                volumeSlider.Refresh();
            }
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

        /// <summary>キー設定モーダル (design vo4kB) を開く。ポーズ中 (PauseGate) なので判定は止まっている。</summary>
        public void OpenKeyConfig()
        {
            if (keyConfigModal != null)
            {
                keyConfigModal.Show();
            }
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

        // --- 検証ハーネス用の読み取り API (製品動作は変えない) ---

        /// <summary>タブの設定行数。</summary>
        public int RowCountForAudit(int tab)
        {
            return pageRows[tab].Count;
        }

        /// <summary>タブ・行のラベル (例: "NoteSpeed")。</summary>
        public string RowLabelForAudit(int tab, int row)
        {
            return pageRows[tab][row].Label;
        }

        /// <summary>タブ・行の現在値 (Read をその場で評価)。</summary>
        public string RowValueForAudit(int tab, int row)
        {
            var read = pageRows[tab][row].Read;
            return read != null ? read() : null;
        }

        /// <summary>タブと行を選択する (クリックと同じ SelectTab + SelectRow 経路)。</summary>
        public void SelectRowForAudit(int tab, int row)
        {
            SelectTab(tab);
            SelectRow(tab, row);
        }

        /// <summary>選択中の行を値変更する (←→ と同じ AdjustSelected 経路)。</summary>
        public void AdjustSelectedForAudit(int delta)
        {
            AdjustSelected(delta);
        }

        /// <summary>
        /// 選択中の行を決定する (Enter / Space の Confirm と同じ経路)。
        /// 行の Click (キー設定を開く等) を実行する。マウスクリックと同じ効果。
        /// </summary>
        public void ActivateSelected()
        {
            if (!open)
            {
                return;
            }

            var rows = pageRows[selectedTab];
            if (selectedRow < 0 || selectedRow >= rows.Count)
            {
                return;
            }

            Row row = rows[selectedRow];
            if (row.Click == null)
            {
                return;
            }

            row.Click();
            RefreshRows();
        }

        /// <summary>選択中の行を決定する (ActivateSelected と同じ経路)。</summary>
        public void ActivateSelectedForAudit()
        {
            ActivateSelected();
        }

        /// <summary>キー設定モーダル (開いていなければ null)。</summary>
        public V6KeyConfigModal KeyConfigModalForAudit
        {
            get { return keyConfigModal; }
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

            // キー設定モーダル (design vo4kB)。フレーム直下に置き、開くとき最前面へ出す。
            keyConfigModal = V6KeyConfigModal.Build(frame.gameObject);
            keyConfigModal.Closed += RefreshKeyBindings;

            driver = rootGo.AddComponent<V6PauseViewDriver>();
            driver.View = this;

            KeyBindings.Changed += RefreshKeyBindings;

            rootGo.SetActive(false);
            UpdatePlacement();
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
                        // Option と同じく曲ごとの offset 設定へも書き戻す
                        // (保存先の値モデルを画面間で一致させる)。
                        OptionSceneManager.WriteNoteTimingToSongOffsets();
                        OptionSettings.Save();
                    });
                // 正本 uDNC0 の Play タブは 4 行: NoteSpeed / NoteTiming / Key Layout / キー設定。
                // どれも画面内の操作だけで開ける (F2 は表示しない、Issue #376 決定 §4.1)。
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
                AddRow(rows, pageRoot, 3, "キー設定", () => "開く ›", null, OpenKeyConfig);
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

        // --- ユーティリティは V6Layout に集約 (Issue #385) ---
    }
}
