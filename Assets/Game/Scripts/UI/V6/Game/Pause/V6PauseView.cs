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
    /// 公開操作 API・設定行定義 (RowsForTab/BuildDataRows)・1:1 配置を持つ。
    /// Issue #733: 設定行のスキーマ (Play/Display/Sound) は
    /// <see cref="V6SettingsCatalog"/> が単一ソースとして持ち、Option と共有する。
    /// Issue #733 フォローアップ: タブ帯・設定パネル・プレビュー (キー一覧 /
    /// 判定凡例 / 音量スライダー / 説明文) と行選択・値変更の規則は、
    /// Option と同じ共有部品 <see cref="V6SettingsScreen"/> (UI/V6/Common) が
    /// 組み立てる。「設定でポーズ時とオプションで違うものを表示する」問題の
    /// 根本対応で、両画面は同一の設定画面 UI を使う。
    /// Pause 固有なのは再開/戻るナビゲーション・状態凍結 (PauseGate)・
    /// Data タブの実行時統計だけ。
    /// </summary>
    public sealed partial class V6PauseView : IV6SettingsHost
    {
        /// <summary>現在生成されている v6 ポーズ画面 (GameScene に 1 個)。統合側・検証から参照する。</summary>
        public static V6PauseView Current { get; private set; }

        // --- design px 定数 (flashbeat-v6-nodes.json uDNC0 / rw7zx) ---
        private const float FrameW = 1728f;
        private const float FrameH = 972f;

        // ヘッダー帯の寸法は V6Header (Width/Height/FramePos) が正本。
        private const float MainX = 24f, MainY = 96f;
        private const float FooterX = 24f, FooterY = 876f, FooterW = 1680f, FooterH = 96f;

        private GameObject rootGo;
        private RectTransform frame;
        private V6PauseViewDriver driver;
        private GameSceneManager sceneManager;

        /// <summary>
        /// 設定画面の共有スキャフォールド (Issue #733 フォローアップ)。
        /// タブ帯・設定パネル・プレビュー (キー一覧 / 判定凡例 / 音量スライダー /
        /// 説明文) と行選択・値変更の規則は Option と同じ
        /// <see cref="V6SettingsScreen"/> (UI/V6/Common) が持つ。
        /// Pause 固有なのはヘッダーの曲情報・フッターのナビゲーション・
        /// 開閉と状態管理 (PauseGate / 再開カウントダウン)・Data タブの
        /// 実行時統計だけ。
        /// </summary>
        private V6SettingsScreen settingsScreen;

        private TextMeshProUGUI songTitle;
        private TextMeshProUGUI songSub;
        private TextMeshProUGUI levelValue;
        private TextMeshProUGUI notesValue;
        private TextMeshProUGUI durationValue;
        private Image thumbImage;

        /// <summary>音量設定の値モデル (Issue #733)。Option と同じ <see cref="V6VolumeSettings"/>。</summary>
        private readonly V6VolumeSettings volumeSettings = new V6VolumeSettings();

        /// <summary>キー設定モーダル (Issue #376、design vo4kB)。フレーム直下に 1 個だけ持つ。</summary>
        private V6KeyConfigModal keyConfigModal;

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

        /// <summary>View を生成する (非表示のまま)。統合担当から呼ぶ。</summary>
        public static V6PauseView Create(RectTransform parent)
        {
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
            // 猶予中の音量保存を落とさない (0.5 秒の猶予中に破棄されると
            // 最後の調整だけが保存されない。Option と同じ規律)。
            volumeSettings.FlushNow();
            // 破棄時にモーダル用パン退避が残っていれば元へ戻す (issue #748)。
            if (confirmPanActive)
            {
                V6ModalPan.Resume(frame);
                confirmPanActive = false;
            }
            if (settingsScreen != null)
            {
                settingsScreen.Dispose();
                settingsScreen = null;
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
            if (settingsScreen != null && settingsScreen.GamePreview != null)
            {
                settingsScreen.GamePreview.SetRenderingEnabled(true);
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
            if (settingsScreen != null && settingsScreen.GamePreview != null)
            {
                settingsScreen.GamePreview.SetRenderingEnabled(false);
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
            if (settingsScreen != null && settingsScreen.GamePreview != null)
            {
                settingsScreen.GamePreview.SetRenderingEnabled(false);
            }
            // Issue #224: 再開時に判定基準時刻を壁時計へ再合わせする (ノーツが飛ばない)。
            PauseGate.Exit();
            rootGo.SetActive(false);
        }

        /// <summary>実データ (曲情報 / 設定値) を UI へ反映する。何度呼んでもよい。</summary>
        public void Refresh()
        {
            if (rootGo == null)
            {
                return;
            }

            RefreshHeader();
            // 設定領域 (タブ帯・設定行・プレビュー) は共有部品
            // <see cref="V6SettingsScreen"/> が差分更新する (Issue #733
            // フォローアップ: Option と同一実装)。毎フレーム呼んでも
            // 変化の無い部分は再構築しない。
            if (settingsScreen != null)
            {
                settingsScreen.Refresh();
            }
        }

        // --- 統合担当がキー入力 (矢印 / Tab) から呼ぶ操作 API ---
        // 行選択・タブ切替・値変更・決定の規則は共有部品
        // <see cref="V6SettingsScreen"/> (Option と同一実装) が持つ。

        /// <summary>設定行の選択を移動する (↑↓ / 正本 uDNC0 凡例「選択」)。</summary>
        public void MoveSelection(int delta)
        {
            if (settingsScreen != null)
            {
                settingsScreen.MoveSelection(delta);
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
            if (!open || delta == 0 || settingsScreen == null)
            {
                return;
            }

            // Issue #733: 値の書き込み・音への即時反映・保存予約は
            // 共有部品とカタログの行定義 (V6VolumeSettings / OptionSettings) が担う。
            settingsScreen.AdjustSelected(delta, shift);
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
            // Key Layout 行の値 (カスタム/標準) とプレビュー見出しを即時反映。
            if (settingsScreen != null)
            {
                settingsScreen.Refresh();
            }
        }

        /// <summary>タブを切り替える (端で止める。Option と同じ規則)。</summary>
        public void SelectNextTab(int delta)
        {
            if (settingsScreen != null)
            {
                settingsScreen.SelectNextTab(delta);
            }
        }

        /// <summary>タブを直接指定で切替 (マウスクリックからも)。</summary>
        public void SelectTab(int index)
        {
            if (settingsScreen != null)
            {
                settingsScreen.SelectTab(index);
            }
        }

        // --- 検証ハーネス用の読み取り API (製品動作は変えない) ---

        /// <summary>タブの設定行数。</summary>
        public int RowCountForAudit(int tab)
        {
            return settingsScreen != null ? settingsScreen.RowsForTab(tab).Count : 0;
        }

        /// <summary>タブ・行のラベル (例: "NoteSpeed")。</summary>
        public string RowLabelForAudit(int tab, int row)
        {
            var rows = settingsScreen != null ? settingsScreen.RowsForTab(tab) : null;
            return rows != null && row >= 0 && row < rows.Count ? rows[row].Label : null;
        }

        /// <summary>タブ・行の現在値 (Value をその場で評価)。</summary>
        public string RowValueForAudit(int tab, int row)
        {
            var rows = settingsScreen != null ? settingsScreen.RowsForTab(tab) : null;
            if (rows == null || row < 0 || row >= rows.Count)
            {
                return null;
            }

            var read = rows[row].Value;
            return read != null ? read() : null;
        }

        /// <summary>タブ・行に実際に描画されている値テキスト。</summary>
        public string RenderedRowValueForAudit(int tab, int row)
        {
            // 共有部品は選択中タブの行ビューだけを持つ (旧実装は全ページを
            // 常駐させていた)。未選択タブの描画値は null を返す。
            if (settingsScreen == null || tab != settingsScreen.SelectedTab)
            {
                return null;
            }

            return settingsScreen.RenderedRowValue(row);
        }

        /// <summary>タブと行を選択する (クリックと同じ SelectTab + SelectRow 経路)。</summary>
        public void SelectRowForAudit(int tab, int row)
        {
            if (settingsScreen == null)
            {
                return;
            }

            settingsScreen.SelectTab(tab);
            settingsScreen.SelectRow(row);
        }

        /// <summary>選択中の行を値変更する (←→ と同じ AdjustSelected 経路)。</summary>
        public void AdjustSelectedForAudit(int delta)
        {
            AdjustSelected(delta);
        }

        /// <summary>
        /// 選択中の行を決定する (Enter / Space の Confirm と同じ経路)。
        /// 行の Activate (キー設定を開く等) を実行する。マウスクリックと同じ効果。
        /// </summary>
        public void ActivateSelected()
        {
            if (!open || settingsScreen == null)
            {
                return;
            }

            // Issue #733: Option と同じ規則 — 決定は行の Activate を呼ぶ
            // (アクション行・トグル行とも)。値の無い行は何も起きない。
            settingsScreen.ActivateSelected();
        }

        /// <summary>選択中の行を決定する (ActivateSelected と同じ経路)。</summary>
        public void ActivateSelectedForAudit()
        {
            ActivateSelected();
        }

        /// <summary>選択中タブの番号。</summary>
        public int SelectedTabForAudit
        {
            get { return settingsScreen != null ? settingsScreen.SelectedTab : 0; }
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
            // モーダルでのキー編集後は、開いている間の Refresh (Tick) が
            // 共有設定画面の行値 (Key Layout の カスタム/標準 等) を追従させる。
            keyConfigModal = V6KeyConfigModal.Build(frame.gameObject);

            // プレビューの RT 描画はポーズを開いている間だけ (Option の
            // SetVisible と同じ規律。閉じている間はカメラを止める)。
            if (settingsScreen != null && settingsScreen.GamePreview != null)
            {
                settingsScreen.GamePreview.SetRenderingEnabled(false);
            }

            driver = rootGo.AddComponent<V6PauseViewDriver>();
            driver.View = this;

            rootGo.SetActive(false);
            UpdatePlacement();
        }

        /// <summary>
        /// タブの行定義。Play/Display/Sound は共有カタログ、Data は画面固有。
        /// Issue #733 フォローアップ: この関数を <see cref="V6SettingsScreen"/>
        /// へ渡し、タブ帯・設定パネル・プレビューは Option と同一実装が組み立てる。
        /// </summary>
        private List<V6SettingRow> RowsForTab(int tab)
        {
            if (tab < V6SettingsLayout.TabCount - 1)
            {
                return V6SettingsCatalog.RowsForTab(tab, this);
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

        /// <summary>Pause はゲーム進行中の画面 (行スキーマは Option と同一。BGM Volume も MusicManager が消費する)。</summary>
        bool IV6SettingsHost.IsGameplayHost => true;

        V6VolumeSettings IV6SettingsHost.Volumes => volumeSettings;

        void IV6SettingsHost.OpenKeyConfig() => OpenKeyConfig();

        void IV6SettingsHost.RequestResetKeyLayout() => RequestResetKeyLayout();

        /// <summary>
        /// 歌詞表示の変更は v6 HUD が <see cref="PlaySession.LyricsEnabled"/> を
        /// 毎フレーム読むため追加の反映は不要 (旧 TextPanel 経路は Stage B で撤去)。
        /// </summary>
        void IV6SettingsHost.OnDisplaySettingChanged()
        {
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
                if (settingsScreen != null && settingsScreen.GamePreview != null)
                {
                    settingsScreen.GamePreview.Tick();
                }

                Refresh();
            }
        }

        // --- ユーティリティは V6Layout に集約 (Issue #385) ---
    }
}
