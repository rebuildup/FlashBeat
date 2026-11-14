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

            BuildTabs(main);
            BuildSettingsPanel(main);
            BuildPreviewPanel(main);
        }

        private void BuildTabs(GameObject main)
        {
            var tabs = V5Ui.Node(main, "Tabs");
            V6Layout.PlaceAt(tabs, 438f, 0f, new Vector2(824f, 48f));
            var layout = tabs.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 8f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            for (int i = 0; i < V6PauseViewLogic.TabCount; i++)
            {
                var tab = V5Ui.Panel(tabs, "Tab " + TabNames[i], new Vector2(200f, 48f), V5Tokens.Key2, 8f);
                V6Layout.LayoutItem(tab, 200f, 48f);
                var label = V5Ui.Text(tab, "Label", TabNames[i], 24f, V5Tokens.TextMuted,
                    TextAlignmentOptions.Center);
                V6Layout.StretchFill(label.rectTransform);
                var button = tab.AddComponent<Button>();
                button.targetGraphic = tab.GetComponent<Image>();
                V6ButtonPressFeedback.Attach(tab);
                int captured = i;
                button.onClick.AddListener(() => SelectTab(captured));
                tabPanels.Add(tab);
                tabLabels.Add(label);
            }
        }

        private void BuildSettingsPanel(GameObject main)
        {
            var panel = V5Ui.Panel(main, "Settings", new Vector2(512f, 688f), V5Tokens.Key2, 16f);
            V6Layout.PlaceAt(panel, 304f, 68f);

            settingsTitle = V5Ui.Text(panel, "Head Title", TabNames[0], 18f, V5Tokens.TextDim,
                TextAlignmentOptions.Left);
            V6Layout.PlaceAt(settingsTitle.gameObject, 8f, 20f, new Vector2(472f, 24f));

            for (int page = 0; page < V6PauseViewLogic.TabCount; page++)
            {
                var pageRoot = V5Ui.Node(panel, "Page " + TabNames[page]);
                V6Layout.PlaceAt(pageRoot, 8f, 60f, new Vector2(496f, 620f));
                pages.Add(pageRoot);
                pageRows.Add(BuildPage(pageRoot, page));
            }
        }

        private void AddRow(List<Row> rows, GameObject pageRoot, int row, string label,
            Func<string> read, Action<int> adjust, Action click = null)
        {
            var panel = V5Ui.Panel(pageRoot, "Row " + label, new Vector2(496f, 56f), V5Tokens.Key1, 8f);
            V6Layout.PlaceAt(panel, 0f, 76f * row, new Vector2(496f, 56f));

            var layout = panel.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 12f;
            layout.padding = new RectOffset(0, Mathf.RoundToInt(12f), 0, 0);
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var labelTmp = V5Ui.Text(panel, "Label", label, 24f, V5Tokens.Text, TextAlignmentOptions.Left);
            V6Layout.LayoutItem(labelTmp.gameObject, 200f, 30f);

            // Spacer (design: fill_container)
            var spacer = V5Ui.Node(panel, "Spacer");
            var spacerLe = spacer.AddComponent<LayoutElement>();
            spacerLe.flexibleWidth = 1f;
            spacerLe.preferredHeight = 30f;

            var value = V5Ui.Text(panel, "Value", read != null ? read() : "", 24f, V5Tokens.Text,
                TextAlignmentOptions.Right);
            value.textWrappingMode = TextWrappingModes.NoWrap;
            V6Layout.LayoutItem(value.gameObject, 120f, 30f);

            if (adjust != null)
            {
                Button prev = MakeStepper(panel, "Prev", "chevron-left");
                int capturedRow = rows.Count;
                prev.onClick.AddListener(() =>
                {
                    SelectRow(selectedTab, capturedRow);
                    adjust(-1);
                    RefreshRows();
                });
                Button next = MakeStepper(panel, "Next", "chevron-right");
                next.onClick.AddListener(() =>
                {
                    SelectRow(selectedTab, capturedRow);
                    adjust(1);
                    RefreshRows();
                });
            }

            var record = new Row
            {
                Label = label,
                Value = value,
                Read = read,
                Adjust = adjust,
            };

            var button = panel.AddComponent<Button>();
            button.targetGraphic = panel.GetComponent<Image>();
            V6ButtonPressFeedback.Attach(panel);
            int capturedIndex = rows.Count;
            button.onClick.AddListener(() =>
            {
                SelectRow(selectedTab, capturedIndex);
                if (click != null)
                {
                    click();
                }
            });

            rows.Add(record);
        }

        /// <summary>design: 40×40 $k4 r8 のステッパー (icon 24 $text-muted、lucide PNG)。</summary>
        private Button MakeStepper(GameObject parent, string name, string iconName)
        {
            var btn = V5Ui.Panel(parent, name, new Vector2(40f, 40f), V5Tokens.Key4, 8f);
            V6Layout.LayoutItem(btn, 40f, 40f);
            var icon = V6Icons.Create(btn, iconName, 24f, V5Tokens.TextMuted, "Icon");
            if (icon != null)
            {
                // Panel はレイアウトグループを持たないので中央に置く (V6Icons の既定)。
                icon.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                icon.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                icon.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                icon.rectTransform.anchoredPosition = Vector2.zero;
            }
            var button = btn.AddComponent<Button>();
            button.targetGraphic = btn.GetComponent<Image>();
            V6ButtonPressFeedback.Attach(btn);
            return button;
        }

        private void BuildPreviewPanel(GameObject main)
        {
            var panel = V5Ui.Panel(main, "Preview", new Vector2(512f, 688f), V5Tokens.Key2, 16f);
            V6Layout.PlaceAt(panel, 864f, 68f);

            previewHeadTitle = V5Ui.Text(panel, "Head Title", "", 18f, V5Tokens.TextDim,
                TextAlignmentOptions.Left);
            V6Layout.PlaceAt(previewHeadTitle.gameObject, 8f, 20f, new Vector2(472f, 24f));

            BuildGamePreview(panel);
            keyListRoot = BuildKeyList(panel);

            // Issue #375: Sound タブはキー一覧の代わりに共通の音量スライダーを出す。
            // 位置は Game Preview (60..340) の直下。部品は Option の Sound タブと同じ。
            volumeSlider = V6VolumeSlider.Build(panel, "Volume Slider", new Vector2(24f, 344f),
                () => SelectedPauseVolume(),
                (value, final) =>
                {
                    SetSelectedPauseVolume(value, final);
                });

            var divider = V5Ui.Panel(panel, "Divider", new Vector2(464f, 1f), new Color(1f, 1f, 1f, 0x1F / 255f), 0f);
            V6Layout.PlaceAt(divider, 24f, 544f);

            var descTitle = V5Ui.Text(panel, "Desc Title", "キー設定", 32f, V5Tokens.Text,
                TextAlignmentOptions.Left, FontStyles.Bold);
            V6Layout.PlaceAt(descTitle.gameObject, 24f, 564f, new Vector2(464f, 40f));
            previewDescTitle = descTitle;

            // design (uDNC0) の文言から F2 を外す (Issue #376 決定 §4.1: 入口は画面内の行だけ)。
            var desc = V5Ui.Text(panel, "Desc", "キーの割り当てを編集します。行を選んで Enter を押してください。",
                24f, V5Tokens.Text, TextAlignmentOptions.TopLeft);
            var descRt = desc.rectTransform;
            descRt.anchorMin = new Vector2(0f, 1f);
            descRt.anchorMax = new Vector2(0f, 1f);
            descRt.pivot = new Vector2(0f, 1f);
            descRt.anchoredPosition = new Vector2(24f, -614f);
            descRt.sizeDelta = new Vector2(464f, 68f);
            // design Desc (fs24 / lineHeight 1.4) は 464 幅で 2 行折り返し。
            // V5Ui.Text は 1 行描画用に lineSpacing を -44.8 (fs 分) にしているので、
            // 40 を足すと行送り 34.75 + 24×0.40 = 44.35px になり、design 実測 (uDNC0:
            // 621→659 = 38px) より 2 行目が 11px 下がる (r24 実機)。0 で 34.75px。
            desc.textWrappingMode = TextWrappingModes.Normal;
            desc.lineSpacing = 0f;
            previewDesc = desc;
        }

        /// <summary>design: 464×280 のゲームプレビュー (8 レーンの遠近線 + ノーツ + 判定線 + レーン番号)。</summary>
        private void BuildGamePreview(GameObject panel)
        {
            var preview = V5Ui.Panel(panel, "Game Preview", new Vector2(464f, 280f), V5Tokens.Key0, 8f);
            V6Layout.PlaceAt(preview, 24f, 60f);

            // design の Lane パス (M26 234l174.5-234 など) の両端点。y は design 上端基準。
            for (int lane = 0; lane < 8; lane++)
            {
                float bottomX = 26f + 58f * lane;
                float topX = 200.5f + 9f * lane;
                Line(preview, "Lane " + lane,
                    new Vector2(bottomX, -234f), new Vector2(topX, 0f),
                    1f, new Color(1f, 1f, 1f, 0x24 / 255f));
            }

            // ノーツ (design のサンプル配置)
            float[,] notes =
            {
                { 62f, 70f, 33f }, { 205f, 130f, 21f }, { 273f, 96f, 27f }, { 291f, 186f, 14f },
                { 204f, 210f, 14f }, { 327f, 58f, 35f }, { 150f, 105f, 20f }, { 390f, 150f, 22f },
            };
            for (int i = 0; i < notes.GetLength(0); i++)
            {
                var note = V5Ui.Panel(preview, "Note " + i,
                    new Vector2(notes[i, 2], 10f), V5Tokens.ButtonPrimaryBg, 0f);
                V6Layout.PlaceAt(note, notes[i, 0], notes[i, 1]);
            }

            var judgeLine = V5Ui.Panel(preview, "Judge Line", new Vector2(464f, 3f), V5Tokens.ButtonPrimaryBg, 0f);
            V6Layout.PlaceAt(judgeLine, 0f, 240f);

            for (int lane = 0; lane < 8; lane++)
            {
                var label = V5Ui.Text(preview, "Lane Label " + lane, (lane + 1).ToString(), 18f,
                    V5Tokens.TextMuted, TextAlignmentOptions.Center);
                V6Layout.PlaceAt(label.gameObject, 6f + 58f * lane, 246f, new Vector2(40f, 22f));
            }
        }

        /// <summary>design: JIS 4 段のキー一覧 (34×40)。刻印は KeyBindings の実割当 (所属レーン番号)。</summary>
        private GameObject BuildKeyList(GameObject panel)
        {
            // タブごとに出し分けるため (Sound では音量スライダーに譲る) 1 個の入れ物にまとめる。
            var root = V5Ui.Node(panel, "Key List");
            V6Layout.PlaceAt(root, 0f, 0f, new Vector2(512f, 688f));

            for (int row = 0; row < V6PauseViewLogic.JisKeyRows.Length; row++)
            {
                char[] keys = V6PauseViewLogic.JisKeyRows[row];
                for (int i = 0; i < keys.Length; i++)
                {
                    var cap = V5Ui.Panel(root, "Key " + keys[i], new Vector2(34f, 40f), V5Tokens.Key4, 4f);
                    V6Layout.PlaceAt(cap, V6PauseViewLogic.JisKeyRowX[row] + 36f * i, V6PauseViewLogic.JisKeyRowY[row]);
                    var label = V5Ui.Text(cap, "Lane", "", 20f, V5Tokens.Text, TextAlignmentOptions.Center);
                    V6Layout.StretchFill(label.rectTransform);
                    keyCaps.Add(cap.GetComponent<Image>());
                    keyLabels.Add(label);
                }
            }

            return root;
        }

        private void BuildFooter()
        {
            var footer = V5Ui.Node(frame.gameObject, "Footer");
            V6Layout.PlaceAt(footer, FooterX, FooterY, new Vector2(FooterW, FooterH));

            // 左下: キー凡例 4 個 (正本 uDNC0 Zone · キー操作: ↑↓ 選択 / ←→ 変更 /
            // タブ切替 / 再開)。起点は design (フッター内 (0,30)) =
            // ページ (24,906) (V6PauseViewLogic.LegendX/Y)。項目は共通部品
            // V6Legend.Item に座標を渡すだけ (Common は編集しない)。
            // 実際のキーは V6GameRuntime.UpdatePause と一致させる:
            // ↑↓ = 行選択 (MoveSelection) / ←→ = 値の変更 (AdjustSelected) /
            // タブ切替 = KeyAction.MenuSwitch (既定 T。design のキャップは "Tab" だが、
            // 実際に動くキーを表示する) / 再開 = KeyAction.Back。
            // キャップはキー設定 (KeyActions) から取る。固定文字列にすると再割当後に
            // 画面が嘘をつく (Title Exit / Select の決定キーと同じ扱い)。
            // 矢印は KeyActions に ↑↓ の割当が無いため固定キー (Title の H/R と同じ扱い)。
            float legendX = V6PauseViewLogic.LegendX - FooterX;
            float legendY = V6PauseViewLogic.LegendY - FooterY;
            legendX = V6Legend.Item(footer, legendX, legendY, "↑↓", "選択");
            legendX = V6Legend.Item(footer, legendX, legendY, "←→", "変更");
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

        /// <summary>2 点を結ぶ細長い Image (design の Lane path / stroke の代用)。</summary>
        private static void Line(GameObject parent, string name, Vector2 from, Vector2 to, float width, Color color)
        {
            var go = V5Ui.Panel(parent, name, new Vector2(1f, 1f), color, 0f);
            var rt = V6Layout.RectOf(go);
            Vector2 delta = to - from;
            float length = delta.magnitude;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = from;
            rt.sizeDelta = new Vector2(length, width);
            float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
            rt.localRotation = Quaternion.Euler(0f, 0f, angle);
        }
    }
}
