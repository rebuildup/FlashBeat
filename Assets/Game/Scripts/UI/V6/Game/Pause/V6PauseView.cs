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
using FlashBeat.UI.V6.Game.Hud;

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
    /// - キー入力は持たない (統合担当が Esc / 矢印 / MenuSwitch を既存キー処理に接続する)。
    ///   このビューは状態 (IsOpen) とマウス操作、統合側から呼べる操作 API だけを持つ。
    /// - 選曲へ / リトライ は既存 GameSceneManager.RetireGame / RetryGame に直結 (実遷移)。
    ///
    /// Issue #385 Phase 2.3: 領域ごとに partial class へ分割した
    /// (uGUI 構築 = V6PauseViewBuild.cs、確認ダイアログ = V6PauseViewDialogs.cs、
    /// 設定値・実データ参照 = V6PauseViewSettings.cs、表示更新 = V6PauseViewRefresh.cs、
    /// 毎フレーム駆動 = V6PauseViewDriver.cs)。このファイルはライフサイクル・
    /// 公開操作 API・設定行定義 (BuildPage)・1:1 配置を持つ。
    /// Issue #733: 設定行のスキーマ (Play/Display/Sound) は
    /// <see cref="V6SettingsCatalog"/> が単一ソースとして持ち、Option と共有する。
    /// ゲームプレビューも <see cref="V6GamePreview"/> (Option と同じ rig) を使う。
    /// Pause 固有なのは再開/戻るナビゲーション・状態凍結 (PauseGate)・
    /// Data タブの実行時統計だけ。
    /// </summary>
    public sealed partial class V6PauseView : IV6SettingsHost
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

        private GameObject rootGo;
        private RectTransform frame;
        private V6PauseViewDriver driver;
        private GameSceneManager sceneManager;

        private readonly List<GameObject> tabPanels = new List<GameObject>();
        private readonly List<TextMeshProUGUI> tabLabels = new List<TextMeshProUGUI>();
        private readonly List<GameObject> pages = new List<GameObject>();
        /// <summary>設定行の定義 (Issue #733: Play/Display/Sound は V6SettingsCatalog、Data はこの画面固有)。</summary>
        private readonly List<List<V6SettingRow>> pageRows = new List<List<V6SettingRow>>();
        /// <summary>設定行のビュー (選択ハイライト・値更新用。pageRows と同じ並び)。</summary>
        private readonly List<List<V6SettingRowView>> pageRowViews = new List<List<V6SettingRowView>>();
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

        /// <summary>
        /// ゲームプレビューの共有ホスト (Issue #733)。Option と同じ
        /// <see cref="V6OptionPreviewRig"/> を well に結線する。
        /// </summary>
        private V6GamePreview gamePreview;

        /// <summary>音量設定の値モデル (Issue #733)。Option と同じ <see cref="V6VolumeSettings"/>。</summary>
        private readonly V6VolumeSettings volumeSettings = new V6VolumeSettings();

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

        /// <summary>確認ダイアログ表示中のコンパクト用パン退避が有効か (issue #748)。</summary>
        private bool confirmPanActive;

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
            // 猶予中の音量保存を落とさない (0.5 秒の猶予中に破棄されると
            // 最後の調整だけが保存されない。Option と同じ規律)。
            volumeSettings.FlushNow();
            // 破棄時にモーダル用パン退避が残っていれば元へ戻す (issue #748)。
            if (confirmPanActive)
            {
                V6ModalPan.Resume(frame);
                confirmPanActive = false;
            }
            if (gamePreview != null)
            {
                gamePreview.Dispose();
                gamePreview = null;
            }
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
            if (gamePreview != null)
            {
                gamePreview.SetRenderingEnabled(true);
            }
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
            volumeSettings.FlushNow();
            if (gamePreview != null)
            {
                gamePreview.SetRenderingEnabled(false);
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
            volumeSettings.FlushNow();
            if (gamePreview != null)
            {
                gamePreview.SetRenderingEnabled(false);
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
                    : "キー割当 (JIS / " + V6PauseViewLogic.LayoutLabel(V6SettingsCatalog.IsCustomKeyLayout()) + ")";
            }

            if (previewDescTitle != null)
            {
                previewDescTitle.text = sound ? "音量" : "キー設定";
            }

            if (previewDesc != null)
            {
                // Issue #733: 「←→」の文字グリフはアイコン化したため、説明文も
                // グリフを使わない表現へ (スライダーのヒントはアイコン表示)。
                previewDesc.text = sound
                    ? "スライダーをドラッグするか矢印キーで 1% (Shift で 10%) 調整します。変更は少し遅れて保存され、音へは即時に反映されます。"
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
        /// 値を持たない行 (記録の表示) では何もしない。
        /// </summary>
        public void AdjustSelected(int delta)
        {
            AdjustSelected(delta, ShiftHeld());
        }

        /// <summary>Shift 併用の読み取り (音量の 10% 刻み。V6VolumeSettings と同じ判定)。</summary>
        private static bool ShiftHeld()
        {
            return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        }

        private void AdjustSelected(int delta, bool shift)
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

            V6SettingRow row = rows[selectedRow];
            if (row.Adjust == null)
            {
                return;
            }

            // Issue #733: 値の書き込み・音への即時反映・保存予約は
            // カタログの行定義 (V6VolumeSettings / OptionSettings) が担う。
            row.Adjust(delta, shift);
            RefreshRows();
            if (volumeSlider != null)
            {
                volumeSlider.Refresh();
            }
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
            if (!V6SettingsCatalog.IsCustomKeyLayout() || confirmRoot != null)
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

            // コンパクト用パン退避を戻す (issue #748)。
            if (confirmPanActive)
            {
                V6ModalPan.Resume(frame);
                confirmPanActive = false;
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

        /// <summary>タブ・行の現在値 (Value をその場で評価)。</summary>
        public string RowValueForAudit(int tab, int row)
        {
            var read = pageRows[tab][row].Value;
            return read != null ? read() : null;
        }

        /// <summary>タブ・行に実際に描画されている値テキスト。</summary>
        public string RenderedRowValueForAudit(int tab, int row)
        {
            var views = pageRowViews[tab];
            return row >= 0 && row < views.Count && views[row].ValueText != null
                ? views[row].ValueText.text
                : null;
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

            // Issue #733: Option と同じ規則 — 決定は行の Activate を呼ぶ
            // (アクション行・トグル行とも)。値の無い行は何も起きない。
            V6SettingRow row = rows[selectedRow];
            if (row.Activate == null)
            {
                return;
            }

            row.Activate();
            RefreshRows();
        }

        /// <summary>選択中の行を決定する (ActivateSelected と同じ経路)。</summary>
        public void ActivateSelectedForAudit()
        {
            ActivateSelected();
        }

        /// <summary>選択中タブの番号。</summary>
        public int SelectedTabForAudit
        {
            get { return selectedTab; }
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

            // プレビューの RT 描画はポーズを開いている間だけ (Option の
            // SetVisible と同じ規律。閉じている間はカメラを止める)。
            if (gamePreview != null)
            {
                gamePreview.SetRenderingEnabled(false);
            }

            driver = rootGo.AddComponent<V6PauseViewDriver>();
            driver.View = this;

            KeyBindings.Changed += RefreshKeyBindings;

            rootGo.SetActive(false);
            UpdatePlacement();
        }

        /// <summary>
        /// 1 タブ分の設定行を作る。行は design: 496×56 $k1 r8、y = 76k。
        /// Issue #733: Play / Display / Sound の行定義は <see cref="V6SettingsCatalog"/>
        /// (Option と同一スキーマ・同一保存経路)。Data タブはこの画面固有の
        /// 実行時統計 (ベスト・記録・スコア・コンボ・判定・進捗) だけを持つ。
        /// </summary>
        private List<V6SettingRow> BuildPage(GameObject pageRoot, int page)
        {
            var rows = RowsForPage(page);
            var views = new List<V6SettingRowView>(rows.Count);
            for (int i = 0; i < rows.Count; i++)
            {
                int index = i;
                V6SettingRow row = rows[i];
                views.Add(V6SettingRowView.Build(pageRoot, row, index,
                    new Vector2(0f, V6SettingRowView.StepLoose * i), false,
                    (delta, shift) =>
                    {
                        SelectRow(page, index);
                        AdjustSelected(delta, shift);
                    },
                    () =>
                    {
                        SelectRow(page, index);
                        // Option と同じ規則: クリック＝選択 + アクション行なら決定。
                        if (row.IsAction && row.Activate != null)
                        {
                            row.Activate();
                            RefreshRows();
                        }
                    }));
            }

            pageRowViews.Add(views);
            return rows;
        }

        /// <summary>タブの行定義。Play/Display/Sound は共有カタログ、Data は画面固有。</summary>
        private List<V6SettingRow> RowsForPage(int page)
        {
            if (page < V6PauseViewLogic.TabCount - 1)
            {
                return V6SettingsCatalog.RowsForTab(page, this);
            }

            return BuildDataRows();
        }

        /// <summary>Data タブ: 演奏中の実データ (GManager の静的カウンタ。ポーズ中も毎フレーム再描画される)。</summary>
        private List<V6SettingRow> BuildDataRows()
        {
            return new List<V6SettingRow>
            {
                new V6SettingRow("この曲のベスト",
                    () => GameHudLogic.FormatScore(BestScore()),
                    null, null, null, null),
                new V6SettingRow("記録済みの曲",
                    () => PlayedSongs() + "曲",
                    null, null, null, null),
                new V6SettingRow("スコア",
                    () => GameHudLogic.FormatScore(GManager.score),
                    null, null, null, null),
                new V6SettingRow("コンボ",
                    () => GManager.combo.ToString(),
                    null, null, null, null),
                new V6SettingRow("判定",
                    () => V6PauseViewLogic.FormatJudgements(GManager.perfect, GManager.great, GManager.bad, GManager.miss),
                    null, null, null, null),
                new V6SettingRow("進捗",
                    () => V6PauseViewLogic.FormatProgress(JudgedNotes(), TotalNotes()),
                    null, null, null, null),
            };
        }

        // --- IV6SettingsHost (Issue #733: Option と共有する設定ホスト契約) ---

        /// <summary>Pause はゲーム進行中の画面。BGM Volume のような実行時消費者の無い行は出さない。</summary>
        bool IV6SettingsHost.IsGameplayHost => true;

        V6VolumeSettings IV6SettingsHost.Volumes => volumeSettings;

        void IV6SettingsHost.OpenKeyConfig() => OpenKeyConfig();

        void IV6SettingsHost.RequestResetKeyLayout() => RequestResetKeyLayout();

        /// <summary>歌詞表示の変更はプレイ中の表示へ即時反映する。</summary>
        void IV6SettingsHost.OnDisplaySettingChanged()
        {
            if (sceneManager != null)
            {
                sceneManager.ApplyLyricsVisibility();
            }
        }

        /// <summary>検証用: 猶予中の音量保存を即時確定する (画面を離れるときと同じ経路)。</summary>
        public void FlushPendingVolumeSaveForAudit()
        {
            volumeSettings.FlushNow();
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
            V6CompactScreen.Place(frame, w, h);
        }

        internal void Tick()
        {
            UpdatePlacement();
            if (open)
            {
                // 猶予中の音量保存 (0.5 秒) とゲームプレビューの疑似時計を進める。
                // プレビューはポーズ中も動き続ける (設定の見え方を確認する画面なので)。
                volumeSettings.Tick();
                if (gamePreview != null)
                {
                    gamePreview.Tick();
                }

                Refresh();
            }
        }

        // --- ユーティリティは V6Layout に集約 (Issue #385) ---
    }
}
