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
    /// (Create/Dispose/Refresh/SetVisible)・行定義 (RowsForTab/Build*Rows) を持つ。
    /// 行定義とキー一覧 BuildKeyGrid は OptionKeyGridAndPressFeedbackTests /
    /// V6UiWiringTests がこのファイルをソース走査して検証するため、ここに残す
    /// (移すとテストが落ちる)。
    /// </summary>
    public sealed partial class V6OptionView
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

        /// <summary>契約 §3 danger トークン (#FF6B6B)。V6Tokens に未定義のため design 値を直接持つ。</summary>
        private static readonly Color DangerLabel = V5Tokens.FromHex("#FF6B6B");

        private readonly Action onBack;

        private GameObject rootGo;
        private RectTransform frame;
        private int lastWidth;
        private int lastHeight;

        private int selectedTab;
        private int selectedRow;
        private bool confirmClearOpen;

        /// <summary>確認モーダルの種類 (issue-162 C-6: Key Layout のリセットも確認する)。</summary>
        private enum ConfirmKind { ClearSaveData, ResetKeyLayout, MediaCache }

        private ConfirmKind confirmKind = ConfirmKind.ClearSaveData;

        /// <summary>Clear save data の実行結果 (消去完了の文言。空なら未実行)。</summary>
        private string clearResult = "";

        private List<Row> currentRows = new List<Row>();

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
            volumeSliderRowIndex = -1;
        }

        /// <summary>音量スライダ (Sound タブのみ。他タブでは null)。</summary>
        private V6VolumeSlider volumeSlider;

        /// <summary>選択行の値テキスト (スライダのドラッグ中に Rebuild せず書き換える)。</summary>
        private TextMeshProUGUI selectedRowValueText;

        /// <summary>キー操作後の保存予約時刻 (0 以下 = 予約なし。Issue #375 §3.2)。</summary>
        private float volumeSaveAt;

        /// <summary>キー操作を止めてから保存するまでの猶予 (秒)。</summary>
        private const float VolumeSaveDelay = 0.5f;

        /// <summary>キー設定モーダル (Issue #376、design vo4kB)。フレーム直下に 1 個だけ持つ。</summary>
        private V6KeyConfigModal keyConfigModal;

        /// <summary>設定行の定義 (表示値と操作をデリゲートで持つ。V5 OptionView.Row と同じ形)。</summary>
        private sealed class Row
        {
            public readonly string Label;
            public readonly Func<string> Value;
            public readonly Action<int> Adjust;
            public readonly Action Activate;
            public readonly string DescTitle;
            public readonly string Desc;
            public readonly bool Danger;
            public readonly bool IsAction; // ステッパーを持たない行 (キー設定 / Data の 2 行)
            /// <summary>音量行の対象 (0=Main / 1=Effect / 2=BGM、それ以外は -1)。</summary>
            public readonly int VolumeIndex;

            public Row(string label, Func<string> value, Action<int> adjust, Action activate,
                string descTitle, string desc, bool danger = false, bool isAction = false,
                int volumeIndex = -1)
            {
                Label = label;
                Value = value;
                Adjust = adjust;
                Activate = activate;
                DescTitle = descTitle;
                Desc = desc;
                Danger = danger;
                IsAction = isAction;
                VolumeIndex = volumeIndex;
            }
        }

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
                SavePendingVolumeNow();
            }

            if (rootGo != null)
            {
                rootGo.SetActive(visible);
            }
        }

        /// <summary>生成したビューを破棄するとき購読を解除する。</summary>
        public void Dispose()
        {
            // 画面を離れるときに予約済みの音量保存を落とさない (0.5 秒の猶予中に
            // 破棄されると、最後の調整だけが保存されない)。
            SavePendingVolumeNow();
            KeyBindings.Changed -= OnBindingsChanged;
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

        private List<Row> RowsForTab(int tab)
        {
            switch (tab)
            {
                case 0: return BuildPlayRows();
                case 1: return BuildDisplayRows();
                case 2: return BuildSoundRows();
                default: return BuildDataRows();
            }
        }

        private List<Row> BuildPlayRows()
        {
            return new List<Row>
            {
                new Row("NoteSpeed",
                    () => V6OptionLogic.FormatSpeed(GManager.noteSpeed),
                    AdjustNoteSpeed,
                    null,
                    "NoteSpeed",
                    "ノーツが出現してから判定線に届くまでの時間。数値が大きいほど速く落ちます。"),
                new Row("NoteTiming",
                    () => GManager.noteTiming.ToString(),
                    AdjustNoteTiming,
                    null,
                    "NoteTiming",
                    "判定タイミングのずれを調整します。変更は全曲の譜面設定 (offset) に書き込まれます。"),
                new Row("Key Layout",
                    () => IsDefaultKeyLayout() ? "標準" : "カスタム",
                    d =>
                    {
                        if (d > 0)
                        {
                            OpenKeyConfig(); // カスタムはキー設定で編集する
                        }
                        else if (!IsDefaultKeyLayout())
                        {
                            OpenResetKeyLayoutConfirm(); // 破壊的なリセットは確認してから (issue-162 C-6)
                        }
                    },
                    OpenKeyConfig,
                    "Key Layout",
                    "キー配列を選びます。標準は既定の配列、カスタムはキー設定で編集します。← で標準に戻します (確認ダイアログが出ます)。"),
                new Row("キー設定",
                    () => "開く ›",
                    null,
                    OpenKeyConfig,
                    "キー設定",
                    "8 レーンの代表キーを変更します。行を選んで Enter を押し、次に押したキーが割り当てられます。",
                    isAction: true),
            };
        }

        private List<Row> BuildDisplayRows()
        {
            return new List<Row>
            {
                new Row("Flash",
                    () => GManager.Flash ? "On" : "Off",
                    d =>
                    {
                        if (d != 0)
                        {
                            ToggleFlash();
                        }
                    },
                    ToggleFlash,
                    "Flash",
                    "ノーツ判定時の背景フラッシュの On/Off。"),
                new Row("FlashTiming",
                    () => GManager.FlashT.ToString(),
                    AdjustFlashTiming,
                    null,
                    "FlashTiming",
                    "フラッシュのタイミングを調整します。"),
                new Row("FlashSpeed",
                    () => GManager.FlashBG.ToString(),
                    AdjustFlashSpeed,
                    null,
                    "FlashSpeed",
                    "フラッシュの速さ。1 が最も速く、8 が最も遅い。"),
                new Row("Key Guide",
                    () => PlaySession.KeyGuideLabel(PlaySession.KeyGuide),
                    SetKeyGuide,
                    () => SetKeyGuide(1),
                    "Key Guide",
                    "プレイ中の下部キーガイドの表示形式。"),
                new Row("Lyrics",
                    () => PlaySession.LyricsLabel(PlaySession.LyricsEnabled),
                    d =>
                    {
                        if (d != 0)
                        {
                            ToggleLyrics();
                        }
                    },
                    ToggleLyrics,
                    "Lyrics",
                    "プレイ中の歌詞表示の On/Off。"),
            };
        }

        private List<Row> BuildSoundRows()
        {
            // 音量行は VolumeIndex を持ち、←→ / ステッパー / スライダーが同じ値モデルを編集する
            // (Issue #375)。Adjust は null — 調整は AdjustSelected が VolumeIndex 経由で行う。
            return new List<Row>
            {
                new Row("Main Volume",
                    () => V6OptionLogic.FormatVolume(GManager.mainVolume),
                    null,
                    null,
                    "Main Volume",
                    "全体の音量。スライダーをドラッグするか ←→ で調整します (Shift で 10% 刻み)。",
                    volumeIndex: 0),
                new Row("Effect Volume",
                    () => V6OptionLogic.FormatVolume(GManager.effectVolume),
                    null,
                    null,
                    "Effect Volume",
                    "効果音の音量。スライダーをドラッグするか ←→ で調整します (Shift で 10% 刻み)。",
                    volumeIndex: 1),
                new Row("BGM Volume",
                    () => V6OptionLogic.FormatVolume(GManager.BGMVolume),
                    null,
                    null,
                    "BGM Volume",
                    "BGM の音量。スライダーをドラッグするか ←→ で調整します (Shift で 10% 刻み)。",
                    volumeIndex: 2),
            };
        }

        private List<Row> BuildDataRows()
        {
            return new List<Row>
            {
                new Row("Media cache",
                    () => MediaCacheSizeLabel(),
                    null,
                    OpenMediaCacheConfirm,
                    "Media cache",
                    "ダウンロードした楽曲映像のキャッシュ。消すと次回プレイ時に再ダウンロードします。",
                    danger: true,
                    isAction: true),
                new Row("Clear save data",
                    () => clearResult,
                    null,
                    OpenClearConfirm,
                    "Clear save data",
                    "ハイスコアとキー設定 (レーン・メニュー操作) を消去します。確認してから実行されます。",
                    danger: true,
                    isAction: true),
            };
        }
    }
}
