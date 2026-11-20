using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FlashBeat.UI.V5;
using FlashBeat.UI.V6;
using FlashBeat.UI.V6.Common;

namespace FlashBeat.UI.V6.Option
{
    /// <summary>
    /// Option 画面の v6 UI。共通部品 (<see cref="V6Frame"/> / <see cref="V6Controls"/> /
    /// <see cref="V6Layout"/>) だけで design v6 の 06 Play (B6If6Z) / 06 Display (EzLfb) /
    /// 06 Sound (z7joSy) / 06 Data (DQHHp) を実行時に構築する (共通フレーム 1728x972 / 4 タブ)。
    ///
    /// - design 空間 (1728×972、左上原点・y_down) の座標を 1:1 (scale 1) で描き、
    ///   <see cref="V6OptionLogic.FrameOrigin"/> の origin (design gaI3p "Resolution Check ·
    ///   Option": FHD (96,54) / 2K (416,234) / Mac (416,346)) で画面中央へ配置する。
    /// - 実データ: GManager (noteSpeed / noteTiming / Flash / FlashT / FlashBG / 音量) +
    ///   OptionSettings (保存) + KeyBindings / KeyConfigUI (キー配列) + PlaySession
    ///   (Key Guide / Lyrics) + MediaCacheManager (Media cache)。サンプル値は書かない。
    /// - 設定の書き戻しは既存 API 経由 (OptionSceneManager の SpeedUp/Down・TimingUp/Down、
    ///   GManager 直接 + OptionSettings.Save())。新しい保存経路は作らない。
    /// - design にあるが実データの無い行は表示しない (固定値のダミーは作らない)。
    /// - フッターは design パッチ (QQ4jM / FChYu / F8SwvS) の通りキー凡例のみ
    ///   (ボタン無し)。決定は Enter / 行クリック、戻るは Esc / ヘッダー戻るボタン。
    /// - キー: ↑↓ 行選択 / ←→ 値変更 / Tab タブ切替 / Enter・Space 決定 / Esc 戻る /
    ///   F2 キー設定。マウス: タブ / 行 / ステッパー / 戻る。
    /// - TitleSceneManager は呼ばない。onBack は統合担当が渡す。
    ///
    /// Issue #385 Phase 2.5: 領域ごとに partial class へ分割した
    /// (毎フレーム入力 = V6OptionViewInput.cs、uGUI 構築・配置・タブ = V6OptionViewBuild.cs、
    /// 設定/プレビュー パネル = V6OptionViewPanels.cs、音量スライダ = V6OptionViewVolume.cs、
    /// 確認モーダル = V6OptionViewDialogs.cs、データ操作 API・uGUI ユーティリティ =
    /// V6OptionViewActions.cs)。このファイルは状態フィールド・ライフサイクル
    /// (Create/Dispose/Refresh/SetVisible)・行定義 (RowsForTab/BuildDataRows) を持つ。
    /// Issue #733: 設定行のスキーマ (Play/Display/Sound) は
    /// <see cref="V6SettingsCatalog"/> が単一ソースとして持ち、Pause と共有する。
    /// このファイルに残すのは Option 固有の Data タブ行だけ。
    /// 行定義とキー一覧 BuildKeyGrid は OptionKeyGridAndPressFeedbackTests /
    /// V6UiWiringTests がこのファイルをソース走査して検証するため、ここに残す
    /// (移すとテストが落ちる)。
    /// </summary>
    public sealed partial class V6OptionView : IV6SettingsHost
    {
        /// <summary>現在生成されている v6 Option 画面 (シーンに 1 個だけ)。</summary>
        public static V6OptionView Current { get; private set; }

        // design px 定数は V6OptionLogic (design B6If6Z パッチ実測) に集約。
        // 以下は view 内の略記 (値の単一ソースは V6OptionLogic)。

        private const float FrameW = 1728f;
        private const float FrameH = 972f;

        private const float TabsX = V6OptionLogic.TabsX, TabsY = V6OptionLogic.TabsY;
        private const float TabsW = V6OptionLogic.TabsW, TabsH = V6OptionLogic.TabsH;
        private const float TabW = V6OptionLogic.TabW, TabGap = V6OptionLogic.TabGap;

        private const float SetX = V6OptionLogic.SetX, SetY = V6OptionLogic.SetY;
        private const float SetW = V6OptionLogic.SetW, SetH = V6OptionLogic.SetH;
        private const float SetRowX = V6OptionLogic.SetRowX, SetRowW = V6OptionLogic.SetRowW;
        private const float SetRowH = V6OptionLogic.SettingsRowH, SetRowY0 = V6OptionLogic.SettingsRowFirstY;

        private const float PrevX = V6OptionLogic.PrevX, PrevY = V6OptionLogic.PrevY;
        private const float PrevW = V6OptionLogic.PrevW, PrevH = V6OptionLogic.PrevH;
        private const float GamePrevX = V6OptionLogic.GamePrevX, GamePrevY = V6OptionLogic.GamePrevY;
        private const float GamePrevW = V6OptionLogic.GamePrevW, GamePrevH = V6OptionLogic.GamePrevH;

        private readonly Action onBack;

        private GameObject rootGo;
        private RectTransform frame;
        private int lastWidth;
        private int lastHeight;

        private int selectedTab;
        private int selectedRow;
        private bool confirmClearOpen;
        /// <summary>確認モーダル表示中のコンパクト用パン退避が有効か (issue #748)。</summary>
        private bool confirmPanActive;

        /// <summary>確認モーダルの種類 (issue-162 C-6: Key Layout のリセットも確認する)。</summary>
        private enum ConfirmKind { ClearSaveData, ResetKeyLayout, MediaCache }

        private ConfirmKind confirmKind = ConfirmKind.ClearSaveData;

        /// <summary>Clear save data の実行結果 (消去完了の文言。空なら未実行)。</summary>
        private string clearResult = "";

        private List<V6SettingRow> currentRows = new List<V6SettingRow>();

        // --- Issue #415: 骨格保持の差分更新用の参照 ---
        // Rebuild() は状態変化に応じて、変わった部分だけを既存要素へ適用する。
        // タブが変わったとき / 未構築のときだけ、Settings / Preview パネルを作り直す。
        private bool scaffoldBuilt;
        private int scaffoldTab = -1; // Settings/Preview を作ったときのタブ
        private readonly List<GameObject> tabObjects = new List<GameObject>();
        private readonly List<Image> tabBackgrounds = new List<Image>();
        private readonly List<TextMeshProUGUI> tabLabels = new List<TextMeshProUGUI>();
        private readonly List<GameObject> rowObjects = new List<GameObject>();
        private readonly List<Image> rowBackgrounds = new List<Image>();
        private readonly List<TextMeshProUGUI> rowValueTexts = new List<TextMeshProUGUI>();
        private TextMeshProUGUI settingsHeadTitle;
        private GameObject previewPanelGo;
        private TextMeshProUGUI previewHeadTitle;
        private TextMeshProUGUI descTitleText;
        private TextMeshProUGUI descBodyText;
        private RectTransform descScrollContent;
        private ScrollRect descScrollRect;
        private int volumeSliderRowIndex = -1; // スライダーが結線されている VolumeIndex (-1 = 未構築)

        private void ClearScaffoldRefs()
        {
            scaffoldBuilt = false;
            scaffoldTab = -1;
            tabObjects.Clear();
            tabBackgrounds.Clear();
            tabLabels.Clear();
            rowObjects.Clear();
            rowBackgrounds.Clear();
            rowValueTexts.Clear();
            settingsHeadTitle = null;
            previewPanelGo = null;
            previewHeadTitle = null;
            descTitleText = null;
            descBodyText = null;
            descScrollContent = null;
            descScrollRect = null;
            volumeSliderRowIndex = -1;
        }

        /// <summary>音量スライダ (Sound タブのみ。他タブでは null)。</summary>
        private V6VolumeSlider volumeSlider;

        /// <summary>選択行の値テキスト (スライダのドラッグ中に Rebuild せず書き換える)。</summary>
        private TextMeshProUGUI selectedRowValueText;

        /// <summary>
        /// ゲームプレビューの共有ホスト (Issue #733)。Game Preview well を最初に
        /// 構築したときに生成し、Dispose まで保持する (タブ切替では作り直さない)。
        /// 中身は <see cref="V6OptionPreviewRig"/> で、Pause の設定画面とも同じ。
        /// </summary>
        private V6GamePreview gamePreview;

        /// <summary>音量設定の値モデル (Issue #733)。Pause と同じ <see cref="V6VolumeSettings"/>。</summary>
        private readonly V6VolumeSettings volumeSettings = new V6VolumeSettings();

        /// <summary>キー設定モーダル (Issue #376、design vo4kB)。フレーム直下に 1 個だけ持つ。</summary>
        private V6KeyConfigModal keyConfigModal;

        private V6OptionView(Action onBack)
        {
            this.onBack = onBack;
        }

        /// <summary>View を生成する。統合担当 (OptionSceneManager 側) から呼ぶ。</summary>
        public static V6OptionView Create(RectTransform parent, Action onBack)
        {
            if (parent == null)
            {
                throw new ArgumentNullException(nameof(parent));
            }

            var view = new V6OptionView(onBack);
            view.BuildRoot(parent);
            // キー設定 (KeyConfigUI) での変更をプレビューのキー割当に反映する。
            KeyBindings.Changed += view.OnBindingsChanged;
            view.Rebuild();
            Current = view;
            return view;
        }

        public RectTransform Root => frame;

        /// <summary>実データ (GManager / KeyBindings / MediaCache) から表示を作り直す。</summary>
        public void Refresh()
        {
            Rebuild();
        }

        /// <summary>検証ハーネス用: 音量スライダ (Sound タブ以外では null)。</summary>
        internal V6VolumeSlider VolumeSliderForAudit
        {
            get { return volumeSlider; }
        }

        /// <summary>検証・計測用: ゾーン全体を作り直した回数 (初回構築を含む)。</summary>
        public int RebuildCountForAudit { get; private set; }

        /// <summary>検証・計測用: 骨格を保持したまま変動部分だけを更新した回数。</summary>
        public int IncrementalUpdateCountForAudit { get; private set; }

        public void SetVisible(bool visible)
        {
            if (!visible)
            {
                volumeSettings.FlushNow();
            }

            if (rootGo != null)
            {
                rootGo.SetActive(visible);
            }

            // 画面を離れている間はプレビューカメラの RT 描画を止める
            // (疑似時計は Time.time ベースなので、戻ったとき位相は連続する)。
            if (gamePreview != null)
            {
                gamePreview.SetRenderingEnabled(visible);
            }
        }

        /// <summary>生成したビューを破棄するとき購読を解除する。</summary>
        public void Dispose()
        {
            // 画面を離れるときに予約済みの音量保存を落とさない (0.5 秒の猶予中に
            // 破棄されると、最後の調整だけが保存されない)。
            volumeSettings.FlushNow();
            KeyBindings.Changed -= OnBindingsChanged;
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

        private void OnBindingsChanged()
        {
            if (rootGo == null || !rootGo.activeInHierarchy)
            {
                return;
            }

            Rebuild();
        }

        /// <summary>Play タブ: JIS キーボード + 実際のレーン割当 (design の Key 1..Key _)。</summary>
        /// <remarks>
        /// キーキャップに出すのは **キー名** (1 / Q / A …) で、レーン番号ではない。
        /// 以前は <see cref="V6OptionLogic.DisplayLaneOf"/> の結果を出していたため、
        /// 同じレーンに属するキーが同じ数字になり「1 2 3 4 4 5 5 6 7 8 8 8」のような
        /// 崩れた表示になっていた (2026-11-12 オーナー指摘)。
        /// レーン番号はプレビュー側のレーンラベルが担う。
        /// </remarks>
        private void BuildKeyGrid(GameObject panel)
        {
            for (int r = 0; r < V6OptionLogic.JisGridKeys.Length; r++)
            {
                KeyCode[] keys = V6OptionLogic.JisGridKeys[r];
                float y = V6OptionLogic.GridRowY[r];
                float x0 = V6OptionLogic.GridRowX[r];
                for (int k = 0; k < keys.Length; k++)
                {
                    var cap = V6Controls.Panel(panel, "Key " + V6OptionLogic.KeyDisplayName(keys[k]),
                        new Vector2(x0 + V6OptionLogic.GridKeyStepX * k, y),
                        new Vector2(V6OptionLogic.GridKeyW, V6OptionLogic.GridKeyH),
                        V6Tokens.Key4, V6Tokens.RadiusSmall);
                    V6Controls.Text(cap, "Label", V6OptionLogic.KeyDisplayName(keys[k]),
                        new Vector2(0f, 9f), new Vector2(V6OptionLogic.GridKeyW, 20f), 20f, V6Tokens.Text,
                        TextAlignmentOptions.Center);
                }
            }
        }

        // --- 行定義 (実データに結線) ---
        //
        // Issue #733: Play / Display / Sound の行スキーマは
        // <see cref="V6SettingsCatalog"/> が単一ソースとして持ち、
        // Pause の設定画面と同じ行・同じ順序・同じ値書式・同じ保存経路を共有する。
        // ここに残るのは Option 固有の Data タブ (キャッシュ / セーブデータ操作) だけ。

        private List<V6SettingRow> RowsForTab(int tab)
        {
            if (tab < 3)
            {
                return V6SettingsCatalog.RowsForTab(tab, this);
            }

            return BuildDataRows();
        }

        private List<V6SettingRow> BuildDataRows()
        {
            return new List<V6SettingRow>
            {
                new V6SettingRow("Media cache",
                    () => MediaCacheSizeLabel(),
                    null,
                    OpenMediaCacheConfirm,
                    "Media cache",
                    "ダウンロードした楽曲映像のキャッシュ。消すと次回プレイ時に再ダウンロードします。",
                    danger: true,
                    isAction: true),
                new V6SettingRow("Clear save data",
                    () => clearResult,
                    null,
                    OpenClearConfirm,
                    "Clear save data",
                    "ハイスコアとキー設定 (レーン・メニュー操作) を消去します。確認してから実行されます。",
                    danger: true,
                    isAction: true),
            };
        }

        // --- IV6SettingsHost (Issue #733: Pause と共有する設定ホスト契約) ---

        /// <summary>Option はゲーム外の画面なので BGM Volume 行も出す。</summary>
        bool IV6SettingsHost.IsGameplayHost => false;

        V6VolumeSettings IV6SettingsHost.Volumes => volumeSettings;

        void IV6SettingsHost.OpenKeyConfig() => OpenKeyConfig();

        void IV6SettingsHost.RequestResetKeyLayout() => OpenResetKeyLayoutConfirm();

        /// <summary>セッション設定の変更はゲーム外なので追加の反映は不要。</summary>
        void IV6SettingsHost.OnDisplaySettingChanged()
        {
        }
    }
}
