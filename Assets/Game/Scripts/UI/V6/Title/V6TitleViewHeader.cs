using System;
using System.Collections.Generic;
using FlashBeat.Update;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FlashBeat.UI.V5;
using FlashBeat.UI.V6;
using FlashBeat.UI.V6.Common;

namespace FlashBeat.UI.V6.Title
{
    /// <summary>
    /// V6TitleView のヘッダーゾーン構築部分 (Issue #385 Phase 2.9 で分割)。
    /// Header 帯 (Exit / 更新ボタン / バージョン表示) の生成と、
    /// アイコン色差し替え・自己ベスト読み出しを持つ。
    /// partial class のためフィールド・メソッドは V6TitleView 本体と共有する。
    /// </summary>
    public sealed partial class V6TitleView
    {
        // --- ヘッダー (24,0) 1680x96 (V6Frame の Header ゾーン内) ---

        private void BuildHeader(UpdateFlowModel model)
        {
            var header = headerZone.gameObject;

            // 左上: Exit (design CtA3V)。正本の Exit フレームは塗りなしで、
            // 中に 56×56 の Icon Button ($k4) + KeyCap + Label を載せる
            // (組み立ては V6TitleExit に集約。旧実装は 162×56 を 1 枚塗っていた)。
            V6TitleExit.Build(header, onExit);

            // タイトル文字 (design aqq3q/ci867) は正本で `enabled: false`
            // (Exit ボタン 162×56 と重なるため非表示)。既定では描かない。
            // 左上は戻るではなく Exit (V6TitleExit) なので戻るボタンは置かない。
            if (V6UiMode.TitleHeaderTitle)
            {
                V6Header.Title(header, "FlashBeat", V6TitleLogic.HeaderTitlePos,
                    V6TitleLogic.HeaderTitleSize, V6TitleLogic.HeaderTitleFontSize);
            }

            // 更新ボタン (更新があるときだけ)。design の右上には無いが、更新の導線は
            // Title にしか無いので、同じヘッダー帯の左側に置く (右側は曲情報 / 状態)。
            if (model != null && V6TitleLogic.ShowUpdateButton(model.Phase))
            {
                // 正本 D7gbLV: Header ローカル (1021,20) 355×56、$k4 r8。
                // アイコン refresh-cw (FMObZ は $accent-light)、KeyCap は半透明黒
                // (#00000040、ほかの $k4 Cap とは別)。
                var update = V6Controls.ButtonSecondary(header,
                    V6TitleLogic.UpdateButtonLabel(model.Phase, model.DownloadProgress),
                    V6TitleLogic.UpdateButtonPos, V6TitleLogic.UpdateButtonSize, "refresh-cw",
                    V6TitleLogic.UpdateButtonKey);
                TintChild(update, "Icon", V6Tokens.AccentLight);
                TintChild(update, "KeyCap " + V6TitleLogic.UpdateButtonKey, new Color32(0, 0, 0, 0x40));

                // 押したときに実行できる操作 (Available → DL / Ready → 適用) が
                // あるときだけ結線する。Downloading 中は進捗表示だけなので
                // **押しても何も起きないボタン**にせず、非活性にする。
                // dismiss (F4) 後も操作できる (通知を閉じた = 更新しない、ではない)。
                if (UpdateNoticePresenter.ResolveUserAction(model) != UpdateNoticeAction.None)
                {
                    WireClick(update, TriggerUpdate);
                }
                else
                {
                    var button = update.GetComponent<Button>();
                    if (button != null) { button.interactable = false; }
                }
            }

            // 右上: バージョン (design L6w6G = Header ローカル (1400,32) 280×32)。
            // 正本の要素は [現行版] [→] [新版] の 3 つだけで、曲情報 / カタログ状態 /
            // 自己ベストは設計に無い (v6 移植時に足していた非設計要素) ため描かない。
            var versionZone = V6Controls.Panel(header, "Version", V6TitleLogic.VersionZonePos,
                V6TitleLogic.VersionZoneSize, Color.clear, 0f);
            bool showNewVersion = model != null &&
                V6TitleLogic.ShowNewVersion(model.Phase, model.LatestVersion);
            string currentVersion = V6TitleLogic.VersionLabel(Application.version);
            if (!string.IsNullOrEmpty(currentVersion))
            {
                // 版番号は桁数が変わるので、正本の右端 (矢印の手前 / zone 右端) に右揃えする。
                float rightEdge = showNewVersion
                    ? V6TitleLogic.VersionArrowPos.x - 8f
                    : V6TitleLogic.VersionRightEdge;
                V6Controls.Text(versionZone, "Version Current", currentVersion,
                    new Vector2(0f, V6TitleLogic.VersionCurrentPos.y),
                    new Vector2(rightEdge, V6TitleLogic.VersionCurrentSize.y),
                    V6TitleLogic.VersionFontSize, V6Tokens.TextDim, TextAlignmentOptions.Right);
            }

            if (showNewVersion)
            {
                V6Controls.Text(versionZone, "Version Arrow", "→", V6TitleLogic.VersionArrowPos,
                    V6TitleLogic.VersionArrowSize, V6TitleLogic.VersionArrowFontSize,
                    V6Tokens.TextDim, TextAlignmentOptions.Left);
                V6Controls.Text(versionZone, "Version New",
                    V6TitleLogic.VersionLabel(model.LatestVersion), V6TitleLogic.VersionNewPos,
                    V6TitleLogic.VersionNewSize, V6TitleLogic.VersionFontSize,
                    V6Tokens.AccentLight, TextAlignmentOptions.Left, FontStyles.Bold);
            }
        }

        /// <summary>
        /// 直下の子を名前で探して Image の色だけ差し替える (共通部品の既定色が
        /// 正本と違う 1 点だけを直すため。部品自体は他画面と共有する)。
        /// </summary>
        private static void TintChild(GameObject parent, string childName, Color color)
        {
            Transform child = parent.transform.Find(childName);
            if (child == null)
            {
                return;
            }

            var image = child.GetComponent<Image>();
            if (image != null)
            {
                image.color = color;
            }
        }

        /// <summary>
        /// 自己ベストの読み出し (GManager のカタログ + 保存済みスコア)。
        /// 正本 QJtTv のヘッダーは [Exit] [更新] [バージョン] だけなので v6 Title では
        /// 表示しないが、選曲側の実装と共有するロジックとして残す。
        /// </summary>
        private static V6TitleLogic.V6TitleRecord BestRecordNow()
        {
            SongData[] songs = GManager.ActiveSongs;
            if (songs == null)
            {
                return default(V6TitleLogic.V6TitleRecord);
            }

            int[] scores = new int[songs.Length];
            for (int i = 1; i < songs.Length; i++)
            {
                SongData song = songs[i];
                if (song == null || song.unplayable)
                {
                    continue;
                }

                scores[i] = GManager.BestScoreOf(i);
            }

            return V6TitleLogic.BestRecord(songs, scores);
        }

    }
}
