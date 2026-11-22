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
    ///   (Key Guide / Lyrics) + MediaCacheManager (映像キャッシュ)。サンプル値は書かない。
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
    /// (毎フレーム入力 = V6OptionViewInput.cs、uGUI 構築・配置 = V6OptionViewBuild.cs、
    /// 確認モーダル = V6OptionViewDialogs.cs、データ操作 API・uGUI ユーティリティ =
    /// V6OptionViewActions.cs)。このファイルは状態フィールド・ライフサイクル
    /// (Create/Dispose/Refresh/SetVisible)・行定義 (RowsForTab/BuildDataRows) を持つ。
    /// Issue #733: 設定行のスキーマ (Play/Display/Sound) は
    /// <see cref="V6SettingsCatalog"/> が単一ソースとして持ち、Pause と共有する。
    /// Issue #733 フォローアップ: タブ帯・設定パネル・プレビュー (キー一覧 /
    /// 判定凡例 / 音量スライダー / 説明文) は共有部品
    /// <see cref="V6SettingsScreen"/> (UI/V6/Common) が組み立て、Pause と
    /// 同一実装を使う。このファイルに残すのは Option 固有の
    /// Header/Footer・確認モーダル・Data タブ行だけ。
    /// </summary>
    public sealed partial class V6OptionView : IV6SettingsHost
    {
        /// <summary>現在生成されている v6 Option 画面 (シーンに 1 個だけ)。</summary>
        public static V6OptionView Current { get; private set; }

        private readonly Action onBack;

        private GameObject rootGo;
        private RectTransform frame;
        private int lastWidth;
        private int lastHeight;

        private bool confirmClearOpen;
        /// <summary>確認モーダル表示中のコンパクト用パン退避が有効か (issue #748)。</summary>
        private bool confirmPanActive;

        /// <summary>確認モーダルの種類 (issue-162 C-6: Key Layout のリセットも確認する)。</summary>
        private enum ConfirmKind { ClearSaveData, ResetKeyLayout, MediaCache }

        // Issue #872: Data タブの行名と確認ダイアログのタイトルは同じ文言を使う。
        // 内部実装名 (MediaCacheManager / save data) ではなく、ユーザーが
        // 消去対象を理解できる日本語の操作名に統一する。
        private const string DataCacheRowLabel = "映像キャッシュ";
        private const string DataSaveRowLabel = "セーブデータ消去";

        private ConfirmKind confirmKind = ConfirmKind.ClearSaveData;

        /// <summary>セーブデータ消去の実行結果 (消去完了の文言。空なら未実行)。</summary>
        private string clearResult = "";

        // --- Issue #415: 骨格保持の差分更新 ---
        // Header/Footer は静的なので初回だけ作る。Main の中身 (タブ帯・設定・
        // プレビュー) は Issue #733 フォローアップで共有部品
        // <see cref="V6SettingsScreen"/> へ移し、Pause と同一実装を使う。
        private bool scaffoldBuilt;
        private V6SettingsScreen settingsScreen;

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
            get { return settingsScreen == null ? null : settingsScreen.VolumeSliderForAudit; }
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
            if (settingsScreen != null && settingsScreen.GamePreview != null)
            {
                settingsScreen.GamePreview.SetRenderingEnabled(visible);
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

        private void OnBindingsChanged()
        {
            if (rootGo == null || !rootGo.activeInHierarchy)
            {
                return;
            }

            Rebuild();
        }

        // --- 行定義 (実データに結線) ---
        //
        // Issue #733: Play / Display / Sound の行スキーマは
        // <see cref="V6SettingsCatalog"/> が単一ソースとして持ち、
        // Pause の設定画面と同じ行・同じ順序・同じ値書式・同じ保存経路を共有する。
        // ここに残るのは Option 固有の Data タブ (映像キャッシュ / セーブデータ消去) だけ。

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
                new V6SettingRow(DataCacheRowLabel,
                    () => MediaCacheSizeLabel(),
                    null,
                    OpenMediaCacheConfirm,
                    DataCacheRowLabel,
                    "ダウンロードした楽曲映像のキャッシュ。消去すると次回プレイ時に再ダウンロードが必要になります。",
                    danger: true,
                    isAction: true),
                new V6SettingRow(DataSaveRowLabel,
                    () => clearResult,
                    null,
                    OpenClearConfirm,
                    DataSaveRowLabel,
                    "ハイスコアとキー設定 (レーン・メニュー操作) を消去します。実行後は元に戻せません。",
                    danger: true,
                    isAction: true),
            };
        }

        // --- IV6SettingsHost (Issue #733: Pause と共有する設定ホスト契約) ---

        /// <summary>Option はゲーム外の画面 (行スキーマは Pause と同一)。</summary>
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
