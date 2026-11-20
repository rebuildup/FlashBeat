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
    /// V6PauseView の uGUI 構築部分 (Issue #385 Phase 2.3 で分割)。
    /// ヘッダー / メイン (タブ・設定パネル・プレビュー) / フッターの生成と、
    /// 設定行・ステッパー・アクションボタンなどの部品ビルダを持つ。
    /// partial class のためフィールド・メソッドは V6PauseView 本体と共有する。
    /// </summary>
    public sealed partial class V6PauseView
    {
        private void BuildHeader()
        {
            // 帯 + タイトルは共通部品 (Issue #385)。帯は素の RectTransform
            // (従来実装どおり panel=false)。
            var header = V6Header.Create(frame.gameObject, false);

            // design uDNC0/SUD2i: 左上はタイトル "PAUSE" のみ。戻るゾーン (b7VqL) は
            // design の実ノードが中身ゼロ (子・fill 無し) の空プレースホルダで、
            // 正本の描画 uDNC0-pause にも戻るボタンは出ない。よって Esc ボタンは描かない
            // (Esc = 再開は凡例と「再開」ボタンの KeyCap が示す)。
            // 正本 uDNC0/ci867 は Title ゾーン (72,20) 185×56 に "PAUSE" (fs38 w700)。
            // 185×56 は正本の矩形 — 共通部品の既定 400×56 ではなく明示する。
            V6Header.Title(header, "PAUSE", new Vector2(72f, 20f), new Vector2(185f, 56f));

            // 右上: ジャケット + 曲情報 + 区切り + LEVEL / NOTES / 曲長 (実データ)。
            // 座標は正本 PRqZF (ヘッダー右 840×96) の子の実値 — レイアウトグループで
            // 詰めると設計の x (Thumb 24 / Song 100 / Divider 432 / Stat 445・557・669) から
            // ずれて曲名がタブ行に潜り込むため、1 個ずつ設計座標へ置く。
            var info = V5Ui.Node(header, "Song Info");
            var infoRt = V6Layout.RectOf(info);
            infoRt.anchorMin = new Vector2(1f, 1f);
            infoRt.anchorMax = new Vector2(1f, 1f);
            infoRt.pivot = new Vector2(1f, 1f);
            infoRt.anchoredPosition = Vector2.zero;
            infoRt.sizeDelta = new Vector2(840f, V6Header.Height);

            // 曲バッジ (Thumb + Song 列) は共通部品 (Issue #385 Phase 1.2)。
            // 2 行目は Artist ではなく「作家・BPM」の複合副題 (Sub)。
            var badge = V6SongBadge.Create(info, new V6SongBadge.Spec
            {
                ThumbPos = new Vector2(24f, 30f),
                ThumbSize = new Vector2(64f, 36f),
                ThumbColor = V5Tokens.Key2,
                ThumbRaycastTarget = false,
                ColumnName = "Song",
                ColumnPos = new Vector2(100f, 8f),
                ColumnSize = new Vector2(320f, 80f),
                Title = new V6SongBadge.Line
                {
                    Pos = new Vector2(0f, 1f), Size = new Vector2(320f, 43f), FontSize = 30f,
                    Color = V5Tokens.Text,
                    // design は h43。TMP は行の実高 (NotoSansJP: fs の約 1.448 倍 = 43.44px / fs30)
                    // より矩形が低いと Ellipsis で 1 行も描かないため、以前は 46px に逃げていた。
                    // いまは margin 補正 (V5Ui.FitEllipsisToLineHeight) で design の h43 を保てる。
                    FitLineHeight = true, FitHeight = 43f,
                },
                Sub = new V6SongBadge.Line
                {
                    Name = "Sub", Pos = new Vector2(0f, 48f), Size = new Vector2(320f, 32f),
                    FontSize = 22f, Color = V5Tokens.TextMuted,
                    // fs22 の行送りは 31.86 で h32 との余裕が 0.14px しかないため確保する。
                    FitLineHeight = true, FitHeight = 32f,
                },
            });
            thumbImage = badge.Thumb.GetComponent<Image>();
            songTitle = badge.Title;
            songSub = badge.Sub;

            var divider = V5Ui.Panel(info, "Divider", new Vector2(1f, 44f), new Color(1f, 1f, 1f, 0x26 / 255f), 0f);
            V6Layout.PlaceAt(divider, 432f, 26f);

            levelValue = BuildStat(info, "LEVEL", 445f);
            notesValue = BuildStat(info, "NOTES", 557f);
            durationValue = BuildStat(info, "曲長", 669f);
        }

        /// <summary>右上の統計 1 個 (Label fs18 dim / Value fs30 w500)。値の TMP を返す。</summary>
        private TextMeshProUGUI BuildStat(GameObject parent, string label, float x)
        {
            var col = V5Ui.Node(parent, "Stat " + label);
            V6Layout.PlaceAt(col, x, 8f, new Vector2(100f, 80f));
            var labelTmp = V5Ui.Text(col, "Label", label, 18f, V5Tokens.TextDim, TextAlignmentOptions.Left);
            V6Layout.PlaceAt(labelTmp.gameObject, 0f, 0f, new Vector2(100f, 26f));
            var value = V5Ui.Text(col, "Value", "", 30f, V5Tokens.Text, TextAlignmentOptions.Left,
                FontStyles.Bold);
            value.textWrappingMode = TextWrappingModes.NoWrap;
            V6Layout.PlaceAt(value.gameObject, 0f, 28f, new Vector2(100f, 43f));
            return value;
        }

        private void BuildMain()
        {
            var main = V5Ui.Node(frame.gameObject, "Main");
            V6Layout.PlaceAt(main, MainX, MainY, new Vector2(1680f, 780f));

            // Issue #733 フォローアップ: 設定画面の中身 (タブ帯・設定パネル・
            // プレビュー) は共有部品 <see cref="V6SettingsScreen"/> が組み立て、
            // Option と同一実装を使う。Pause 固有なのはこの Main ゾーンの
            // 置き場所と、行定義の Data タブ (実行時統計) だけ。
            settingsScreen = new V6SettingsScreen(main, this,
                V6SettingsLayout.TabLabels, RowsForTab);
            settingsScreen.Refresh();
        }

        private void BuildFooter()
        {
            var footer = V5Ui.Node(frame.gameObject, "Footer");
            V6Layout.PlaceAt(footer, FooterX, FooterY, new Vector2(FooterW, FooterH));

            // 左下: キー凡例 4 個 (正本 uDNC0 Zone · キー操作: ↑↓ 選択 / ←→ 変更 /
            // タブ切替 / 再開)。起点は design (フッター内 (0,30)) =
            // ページ (24,906) (V6PauseViewLogic.LegendX/Y)。項目は共通部品
            // V6Legend / V6Legend.IconItem に座標を渡すだけ。
            // 実際のキーは V6GameRuntime.UpdatePause と一致させる:
            // ↑↓ = 行選択 (MoveSelection) / ←→ = 値の変更 (AdjustSelected) /
            // タブ切替 = KeyAction.MenuSwitch (既定 Tab。design のキャップと一致。
            // 再割当時は実際に動くキーを表示する) / 再開 = KeyAction.Back。
            // キャップはキー設定 (KeyActions) から取る。固定文字列にすると再割当後に
            // 画面が嘘をつく (Title Exit / Select の決定キーと同じ扱い)。
            // 矢印は KeyActions に ↑↓ の割当が無いためアイコンで示す
            // (Issue #733: 文字グリフ → chevron スプライト。Option と同じ V6Legend.IconItem。
            // 項目は共通部品 V6Legend / V6Legend.IconItem に座標を渡すだけ)。
            float legendX = V6PauseViewLogic.LegendX - FooterX;
            float legendY = V6PauseViewLogic.LegendY - FooterY;
            legendX = V6Legend.IconItem(footer, legendX, legendY,
                new[] { "chevron-up", "chevron-down" }, "選択");
            legendX = V6Legend.IconItem(footer, legendX, legendY,
                new[] { "chevron-left", "chevron-right" }, "変更");
            legendX = V6Legend.Item(footer, legendX, legendY,
                KeyCap(KeyActions.KeyAction.MenuSwitch), "タブ切替");
            V6Legend.Item(footer, legendX, legendY,
                KeyCap(KeyActions.KeyAction.Back), "再開");

            // 右下: design の Op · 選曲・リトライ (520 = 選曲へ 280 + gap16 + リトライ 220、
            // リトライは独立ボタンのまま左ボタンへ統合しない) + Op · 再開 266。
            // 座標は design ページ座標 (V6PauseViewLogic)。
            var opGroup = V5Ui.Node(footer, "Op · 選曲・リトライ");
            V6Layout.PlaceAt(opGroup, V6PauseViewLogic.OpGroupX - FooterX, V6PauseViewLogic.OpButtonsY - FooterY,
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
            BuildActionButton(footer, "再開", "circle-stop",
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
        /// iconName は design の lucide 名そのまま (log-out / rotate-ccw / circle-stop、
        /// Resources/Icons/ に PNG あり)。x / y は親ノード内の design 座標
        /// (フッター直下なら (1414,20)、Op グループ内なら (0,0) / (296,0))。
        /// </summary>
        private void BuildActionButton(GameObject parent, string label, string iconName, string key,
            bool primary, float x, float y, float width, Action action)
        {
            var go = V5Ui.Panel(parent, "Button " + label, new Vector2(width, V6PauseViewLogic.OpButtonH),
                primary ? V5Tokens.ButtonPrimaryBg : V5Tokens.Key4, 8f);
            V6Layout.PlaceAt(go, x, y, new Vector2(width, V6PauseViewLogic.OpButtonH));

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
                V6Layout.PlaceAt(icon, startX, 12f, new Vector2(32f, 32f));
            }

            var labelTmp = V5Ui.Text(go, "Label", label, 30f, labelColor, TextAlignmentOptions.Left,
                primary ? FontStyles.Bold : FontStyles.Normal);
            labelTmp.textWrappingMode = TextWrappingModes.NoWrap;
            V6Layout.PlaceAt(labelTmp.gameObject, labelX, 6.5f, new Vector2(labelW + 2f, 43f));

            var cap = V5Ui.Panel(go, "KeyCap " + key, new Vector2(capW, 32f),
                primary ? new Color(0f, 0f, 0f, 0x26 / 255f) : new Color(0f, 0f, 0f, 0x40 / 255f), 4f);
            V6Layout.PlaceAt(cap, capX, 12f, new Vector2(capW, 32f));
            var keyTmp = V5Ui.Text(cap, "Key", key, 22f,
                primary ? V5Tokens.ButtonPrimaryLabel : V5Tokens.TextMuted, TextAlignmentOptions.Center);
            keyTmp.textWrappingMode = TextWrappingModes.NoWrap;
            V6Layout.StretchFill(keyTmp.rectTransform);

            var button = go.AddComponent<Button>();
            button.targetGraphic = go.GetComponent<Image>();
            V6ButtonPressFeedback.Attach(go);
            // ポーズのアクション (再開 / リトライ / 終了) は画面遷移を伴うので遅延する。
            button.onClick.AddListener(() => V6ButtonPressFeedback.RunAfterFeedback(go, () => action()));
        }

    }
}
