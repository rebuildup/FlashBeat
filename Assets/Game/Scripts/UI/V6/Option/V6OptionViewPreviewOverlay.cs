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
        /// <summary>
        /// プレビューだけを 1 ステップ進める (実機の V6OptionViewDriver.Update
        /// が呼ぶ Tick のプレビュー部分と同じ経路。メニュー入力は処理しない)。
        /// </summary>
        public void TickPreviewForAudit(float mediaTime, float deltaTime)
        {
            if (gamePreview != null)
            {
                gamePreview.TickSimulated(mediaTime, deltaTime);
            }
        }

        /// <summary>レーンフラッシュの現在 α (0..LaneFlashPeakAlpha)。</summary>
        public float PreviewLaneFlashAlphaForAudit(int lane)
        {
            return gamePreview == null ? 0f : gamePreview.LaneFlashAlphaForAudit(lane);
        }

        /// <summary>拍フラッシュ (BGFlash 相当) の現在 α。</summary>
        public float PreviewBeatFlashAlphaForAudit
        {
            get { return gamePreview == null ? 0f : gamePreview.BeatFlashAlphaForAudit; }
        }

        /// <summary>レーンに表示中の判定ラベル (PERFECT 等)。無ければ null。</summary>
        public string PreviewVerdictForAudit(int lane)
        {
            return gamePreview == null ? null : gamePreview.VerdictForAudit(lane);
        }

        /// <summary>いま歌詞スロットに出ている本文 (無ければ null)。</summary>
        public string PreviewLyricForAudit
        {
            get { return gamePreview == null ? null : gamePreview.LyricForAudit; }
        }

        /// <summary>これまでに出した判定ポップの累計数。</summary>
        public int PreviewJudgementCountForAudit
        {
            get { return gamePreview == null ? 0 : gamePreview.JudgementCountForAudit; }
        }

        /// <summary>歌詞ボックスの RectTransform (配置検証用)。無ければ null。</summary>
        public RectTransform PreviewLyricBoxRectForAudit
        {
            get { return gamePreview == null ? null : gamePreview.LyricBoxRectForAudit; }
        }

        /// <summary>歌詞テキストの TMP (フォントサイズ検証用)。無ければ null。</summary>
        public TextMeshProUGUI PreviewLyricTextForAudit
        {
            get { return gamePreview == null ? null : gamePreview.LyricTextForAudit; }
        }

        /// <summary>判定ポップの RectTransform (配置検証用)。無ければ null。</summary>
        public RectTransform PreviewVerdictRectForAudit(int lane)
        {
            return gamePreview == null ? null : gamePreview.VerdictRectForAudit(lane);
        }

        /// <summary>判定ポップの TMP (フォントサイズ検証用)。無ければ null。</summary>
        public TextMeshProUGUI PreviewVerdictTextForAudit(int lane)
        {
            return gamePreview == null ? null : gamePreview.VerdictTextForAudit(lane);
        }

        /// <summary>
        /// プレビューのオートプレイ (Issue #732 のデモ用自動入力) の有効フラグ。
        /// 既定 true。実キー注入で判定を制御する検証ハーネスは false にする。
        /// </summary>
        public bool PreviewAutoPlayForAudit
        {
            get { return gamePreview != null && gamePreview.AutoPlayForAudit; }
            set { if (gamePreview != null) gamePreview.AutoPlayForAudit = value; }
        }

        /// <summary>単調増加の疑似プレビュー時刻 (時計リベース検証用)。</summary>
        public float PreviewTimeForAudit
        {
            get { return gamePreview == null ? 0f : gamePreview.TimeForAudit; }
        }

        /// <summary>判定種別ごとの累計ポップ数 (オートプレイ網羅性の検証用)。</summary>
        public int PreviewVerdictCountForAudit(LongNote.Judgement judgement)
        {
            return gamePreview == null ? 0 : gamePreview.VerdictCountForAudit(judgement);
        }
    }
}
