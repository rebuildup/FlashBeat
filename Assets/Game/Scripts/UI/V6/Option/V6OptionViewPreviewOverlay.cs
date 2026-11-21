using TMPro;
using UnityEngine;
using FlashBeat.Gameplay;
using FlashBeat.UI.V6.Common;

namespace FlashBeat.UI.V6.Option
{
    /// <summary>
    /// V6OptionView の Game Preview オーバーレイ部分 (Issue #607 / 再構築 #677)。
    ///
    /// Issue #733: オーバーレイ (歌詞行・判定ポップ) と rig の保持は
    /// <see cref="V6GamePreview"/> (UI/V6/Common) へ移し、Pause の設定画面と
    /// 同じプレビューを共有する。この partial は検証ハーネスが使う
    /// V6OptionView の公開 API を、共有ホストへ委譲するだけの窓口として残す。
    /// </summary>
    public sealed partial class V6OptionView
    {
        /// <summary>共有ゲームプレビュー (V6SettingsScreen が保持。未構築なら null)。</summary>
        private V6GamePreview GamePreviewHost
        {
            get { return settingsScreen != null ? settingsScreen.GamePreview : null; }
        }

        /// <summary>
        /// プレビューだけを 1 ステップ進める (実機の V6OptionViewDriver.Update
        /// が呼ぶ Tick のプレビュー部分と同じ経路。メニュー入力は処理しない)。
        /// </summary>
        public void TickPreviewForAudit(float mediaTime, float deltaTime)
        {
            if (GamePreviewHost != null)
            {
                GamePreviewHost.TickSimulated(mediaTime, deltaTime);
            }
        }

        /// <summary>レーンフラッシュの現在 α (0..LaneFlashPeakAlpha)。</summary>
        public float PreviewLaneFlashAlphaForAudit(int lane)
        {
            return GamePreviewHost == null ? 0f : GamePreviewHost.LaneFlashAlphaForAudit(lane);
        }

        /// <summary>拍フラッシュ (BGFlash 相当) の現在 α。</summary>
        public float PreviewBeatFlashAlphaForAudit
        {
            get { return GamePreviewHost == null ? 0f : GamePreviewHost.BeatFlashAlphaForAudit; }
        }

        /// <summary>レーンに表示中の判定ラベル (PERFECT 等)。無ければ null。</summary>
        public string PreviewVerdictForAudit(int lane)
        {
            return GamePreviewHost == null ? null : GamePreviewHost.VerdictForAudit(lane);
        }

        /// <summary>いま歌詞スロットに出ている本文 (無ければ null)。</summary>
        public string PreviewLyricForAudit
        {
            get { return GamePreviewHost == null ? null : GamePreviewHost.LyricForAudit; }
        }

        /// <summary>これまでに出した判定ポップの累計数。</summary>
        public int PreviewJudgementCountForAudit
        {
            get { return GamePreviewHost == null ? 0 : GamePreviewHost.JudgementCountForAudit; }
        }

        /// <summary>歌詞ボックスの RectTransform (配置検証用)。無ければ null。</summary>
        public RectTransform PreviewLyricBoxRectForAudit
        {
            get { return GamePreviewHost == null ? null : GamePreviewHost.LyricBoxRectForAudit; }
        }

        /// <summary>歌詞テキストの TMP (フォントサイズ検証用)。無ければ null。</summary>
        public TextMeshProUGUI PreviewLyricTextForAudit
        {
            get { return GamePreviewHost == null ? null : GamePreviewHost.LyricTextForAudit; }
        }

        /// <summary>判定ポップの RectTransform (配置検証用)。無ければ null。</summary>
        public RectTransform PreviewVerdictRectForAudit(int lane)
        {
            return GamePreviewHost == null ? null : GamePreviewHost.VerdictRectForAudit(lane);
        }

        /// <summary>判定ポップの TMP (フォントサイズ検証用)。無ければ null。</summary>
        public TextMeshProUGUI PreviewVerdictTextForAudit(int lane)
        {
            return GamePreviewHost == null ? null : GamePreviewHost.VerdictTextForAudit(lane);
        }

        /// <summary>
        /// プレビューのオートプレイ (Issue #732 のデモ用自動入力) の有効フラグ。
        /// 既定 true。実キー注入で判定を制御する検証ハーネスは false にする。
        /// </summary>
        public bool PreviewAutoPlayForAudit
        {
            get { return GamePreviewHost != null && GamePreviewHost.AutoPlayForAudit; }
            set { if (GamePreviewHost != null) GamePreviewHost.AutoPlayForAudit = value; }
        }

        /// <summary>単調増加の疑似プレビュー時刻 (時計リベース検証用)。</summary>
        public float PreviewTimeForAudit
        {
            get { return GamePreviewHost == null ? 0f : GamePreviewHost.TimeForAudit; }
        }

        /// <summary>判定種別ごとの累計ポップ数 (オートプレイ網羅性の検証用)。</summary>
        public int PreviewVerdictCountForAudit(LongNote.Judgement judgement)
        {
            return GamePreviewHost == null ? 0 : GamePreviewHost.VerdictCountForAudit(judgement);
        }
    }
}
