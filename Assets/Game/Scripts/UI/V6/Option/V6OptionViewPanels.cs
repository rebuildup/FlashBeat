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
    /// V6OptionView の設定/プレビュー パネル構築部分 (Issue #385 Phase 2.5 で分割)。
    /// 設定行 (UpdateSettings/BuildSettings/BuildSettingRow)、プレビュー
    /// (UpdatePreview/BuildPreview/BuildGamePreview/BuildJudgeLegend/JudgeColor/
    /// BuildDesc/SelectedRowDesc) を持つ。キー一覧 BuildKeyGrid は
    /// OptionKeyGridAndPressFeedbackTests が V6OptionView.cs をソース走査して
    /// 検証するため本体ファイル側に残す。partial class のため
    /// フィールド・メソッドは V6OptionView 本体と共有する。
    /// </summary>
    public sealed partial class V6OptionView
    {
        // --- 設定 (design crVgG: メイン (304,68) 512x688 $k2 r16) ---
        // Head (8,8) 496x36 (Title fs18 $text-dim)。行 (8, 60+step*k) 496x56 r8:
        // 背景 $k1 (選択行 / アクション行 キー設定 は $k6)。Label w200 fs24、
        // Value w120 右寄せ fs24、Prev/Next 40x40 $k4 r8 (icon 24 $text-muted)。
        // 行の内側 padding [0,12] gap12 → Prev x=392 / Next x=444 / Value 右端 x=380。

        /// <summary>
        /// Issue #415: 設定パネルは骨格 (行の背景・ラベル・ステッパー) を保持し、
        /// 選択行の背景色・値テキスト・selectedRowValueText だけを差分更新する。
        /// </summary>
        private void UpdateSettings()
        {
            if (rowValueTexts.Count != currentRows.Count)
            {
                // 行数が合わない (骨格破損) ときは Settings/Preview ごと作り直す。
                RebuildMainPanels();
                return;
            }

            if (settingsHeadTitle != null)
            {
                settingsHeadTitle.text = V6OptionLogic.TabLabels[selectedTab];
            }

            for (int i = 0; i < currentRows.Count; i++)
            {
                bool selected = i == selectedRow;
                if (i < rowBackgrounds.Count && rowBackgrounds[i] != null)
                {
                    rowBackgrounds[i].color = selected ? V6Tokens.Key6 : V6Tokens.Key1;
                }

                if (rowValueTexts[i] != null)
                {
                    rowValueTexts[i].text = currentRows[i].Value();
                }
            }

            selectedRowValueText = selectedRow >= 0 && selectedRow < rowValueTexts.Count
                ? rowValueTexts[selectedRow]
                : null;
        }

        private void BuildSettings()
        {
            rowObjects.Clear();
            rowBackgrounds.Clear();
            rowValueTexts.Clear();
            var main = Zone("Main");
            var panel = V6Controls.Panel(main.gameObject, "Settings",
                new Vector2(SetX, SetY), new Vector2(SetW, SetH), V6Tokens.Key2, V6Tokens.RadiusLarge);

            var head = V6Layout.Row(panel, "Head", new Vector2(8f, 8f), new Vector2(SetRowW, 36f), 12f);
            settingsHeadTitle = V6Controls.Text(head, "Title", V6OptionLogic.TabLabels[selectedTab],
                new Vector2(12f, 0f), new Vector2(200f, 36f), 18f, V6Tokens.TextDim);

            float step = V6OptionLogic.RowStep(currentRows.Count);
            for (int i = 0; i < currentRows.Count; i++)
            {
                BuildSettingRow(panel, currentRows[i], i, SetRowY0 + step * i);
            }
        }

        private void BuildSettingRow(GameObject panel, Row row, int index, float y)
        {
            bool selected = index == selectedRow;
            var line = V6Controls.Panel(panel, "Set " + row.Label, new Vector2(SetRowX, y),
                new Vector2(SetRowW, SetRowH), selected ? V6Tokens.Key6 : V6Tokens.Key1, V6Tokens.RadiusMedium);

            var rowButton = line.AddComponent<Button>();
            rowButton.transition = Selectable.Transition.None;
            var nav = rowButton.navigation;
            nav.mode = Navigation.Mode.None;
            rowButton.navigation = nav;
            V6ButtonPressFeedback.Attach(line);
            rowButton.onClick.AddListener(() =>
            {
                selectedRow = index;

                // ステッパー (◀ ▶) を持たない行 (キー設定 / Media cache / Clear save data) は
                // クリック＝実行にする。値を変える行は ◀ ▶ ボタンで変えられるので、
                // どの行もマウスだけで操作できるようにするため (Acceptance: マウスとキーボードの両方)。
                if (row.IsAction && row.Activate != null)
                {
                    row.Activate();
                }

                Rebuild();
            });

            var label = V6Controls.Text(line, "Label", row.Label, new Vector2(12f, 0f),
                new Vector2(200f, SetRowH), 24f, row.Danger ? DangerLabel : V6Tokens.Text);
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Ellipsis;

            // Issue #429: 編集値は左右の矢印の「間」に中央揃えで置く
            // (Prev 300-340 / Next 444-484 → 値は 344-440 の中央 392)。
            // アクション行 (キー設定等) は従来どおり右寄せの値を残す。
            bool centered = !row.IsAction;
            var value = V6Controls.Text(line, "Value", row.Value(),
                centered ? new Vector2(344f, 0f) : new Vector2(260f, 0f),
                centered ? new Vector2(96f, SetRowH) : new Vector2(120f, SetRowH),
                24f, V6Tokens.Text,
                centered ? TextAlignmentOptions.Center : TextAlignmentOptions.Right);
            value.textWrappingMode = TextWrappingModes.NoWrap;
            value.overflowMode = TextOverflowModes.Ellipsis;
            if (centered)
            {
                value.enableAutoSizing = true;
                value.fontSizeMin = 16f;
                value.fontSizeMax = 24f;
            }

            rowObjects.Add(line);
            rowBackgrounds.Add(line.GetComponent<Image>());
            rowValueTexts.Add(value);
            if (selected)
            {
                selectedRowValueText = value;
            }

            if (row.IsAction)
            {
                return;
            }

            var prev = V6Controls.IconButton(line, "chevron-left", new Vector2(300f, 8f),
                new Vector2(40f, 40f), V6Tokens.Key4, V6Tokens.RadiusMedium, 24f, V6Tokens.TextMuted);
            prev.GetComponent<Button>().onClick.AddListener(() =>
            {
                selectedRow = index;
                AdjustSelected(-1);
            });

            var next = V6Controls.IconButton(line, "chevron-right", new Vector2(444f, 8f),
                new Vector2(40f, 40f), V6Tokens.Key4, V6Tokens.RadiusMedium, 24f, V6Tokens.TextMuted);
            next.GetComponent<Button>().onClick.AddListener(() =>
            {
                selectedRow = index;
                AdjustSelected(1);
            });
        }

        // --- プレビュー (design cqQMc: メイン (864,68) 512x688 $k2 r16) ---
        // 4 タブ共通: Head (8,8) / Game Preview (24,60) 464x280 $k0 r8 /
        // Divider (24,544) 464x1 #FFFFFF1F / Desc Title (24,564) fs32 w500 /
        // Desc (24,614) w464 fs24 lineHeight1.4。Play のみキー一覧 (8,356〜) を追加。

        /// <summary>
        /// Issue #415: プレビューパネルは骨格を保持し、見出し (選択中のキー割当種別) と
        /// Desc タイトル/本文、Sound タブのスライダー結線だけを差分更新する。
        /// </summary>
        private void UpdatePreview()
        {
            if (previewPanelGo == null)
            {
                // 骨格が壊れているときは Settings/Preview ごと作り直す。
                RebuildMainPanels();
                return;
            }

            if (previewHeadTitle != null)
            {
                previewHeadTitle.text = PreviewHeadTitle();
            }

            Row row = SelectedRowDesc();
            if (descTitleText != null)
            {
                descTitleText.text = row != null ? row.DescTitle : "";
            }

            if (descBodyText != null)
            {
                descBodyText.text = row != null ? row.Desc : "";
            }

            // Sound タブ: スライダーが結線されている音量対象が変わったら、
            // スライダーだけを作り直す (プレビュー全体は作り直さない)。
            int vi = row != null ? row.VolumeIndex : -1;
            if (vi != volumeSliderRowIndex)
            {
                V6Hierarchy.DetachAndDestroy(previewPanelGo.transform.Find("Volume Slider")?.gameObject);
                volumeSlider = null;
                volumeSliderRowIndex = -1;
                if (vi >= 0)
                {
                    BuildVolumePreview(previewPanelGo);
                }
            }
        }

        private void BuildPreview()
        {
            var main = Zone("Main");
            var panel = V6Controls.Panel(main.gameObject, "Preview",
                new Vector2(PrevX, PrevY), new Vector2(PrevW, PrevH), V6Tokens.Key2, V6Tokens.RadiusLarge);
            previewPanelGo = panel;

            var head = V6Layout.Row(panel, "Head", new Vector2(8f, 8f), new Vector2(496f, 36f), 12f);
            previewHeadTitle = V6Controls.Text(head, "Title", PreviewHeadTitle(),
                new Vector2(12f, 0f), new Vector2(400f, 36f), 18f, V6Tokens.TextDim);

            BuildGamePreview(panel);

            if (selectedTab == 0)
            {
                BuildKeyGrid(panel);
            }
            else if (selectedTab == 1)
            {
                BuildJudgeLegend(panel);
            }
            else if (selectedTab == 2)
            {
                BuildVolumePreview(panel);
            }

            BuildDesc(panel, V6OptionLogic.DescDividerYOf(selectedTab), SelectedRowDesc());
        }

        private string PreviewHeadTitle()
        {
            switch (selectedTab)
            {
                case 0:
                    // design: キー割当 (JIS / 標準)。実際の配列状態を出す。
                    return "キー割当 (" + (IsDefaultKeyLayout() ? "JIS / 標準" : "カスタム") + ")";
                case 1:
                    return "判定表示";
                case 2:
                    return "音量";
                default:
                    return "設定";
            }
        }

        /// <summary>8 レーンの遠近法プレビュー (design 共通: 464x280 / 判定線 y240 / レーン番号)。</summary>
        private void BuildGamePreview(GameObject panel)
        {
            var well = V6Controls.Panel(panel, "Game Preview", new Vector2(GamePrevX, GamePrevY),
                new Vector2(GamePrevW, GamePrevH), V6Tokens.Key0, V6Tokens.RadiusMedium);

            // レーン中心線: 下端 (判定線 y240) で 58px 間隔、上端 (y0) で約 7px 間隔
            // (design プレイフィールドの 150 → 18 px の縮約)。
            // design のレーン番号は (6 + 58i, 246) 40x26 の中心、つまり判定線上の中心は
            // 26 + 58i (中心 229)。464 幅の中央 232 から -3 ずれるが design 実測に合わせる。
            const float judgeY = 240f;
            const float bottomSpacing = 58f;
            const float bottomCenterX = 26f;
            const float topSpacingRatio = 7f / 58f;
            for (int i = 0; i < 8; i++)
            {
                float bottomX = bottomCenterX + bottomSpacing * i;
                float hub = GamePrevW * 0.5f;
                float topX = hub + (bottomX - hub) * topSpacingRatio;
                DrawLine(well, "Lane " + i, bottomX, judgeY, topX, 0f, 1.5f, V5Tokens.FromHex("#FFFFFF24"));
            }

            // サンプルノート (design の Note 0..5)。プレビュー装飾 (実データではない)。
            float[,] notes =
            {
                { 62f, 70f, 33f }, { 205f, 130f, 21f }, { 273f, 96f, 27f },
                { 291f, 186f, 14f }, { 204f, 210f, 14f }, { 327f, 58f, 35f },
            };
            for (int i = 0; i < notes.GetLength(0); i++)
            {
                var note = Rect(well, "Note " + i, notes[i, 2], 10f, V5Tokens.FromHex("#EDEDED"));
                V6Layout.Place(note.GetComponent<RectTransform>(), new Vector2(notes[i, 0], notes[i, 1]),
                    new Vector2(notes[i, 2], 10f));
            }

            var judgeLine = Rect(well, "Judge Line", GamePrevW, 3f, V5Tokens.FromHex("#EDEDED"));
            V6Layout.Place(judgeLine.GetComponent<RectTransform>(), new Vector2(0f, judgeY),
                new Vector2(GamePrevW, 3f));

            for (int i = 0; i < 8; i++)
            {
                // 番号はレーン中心線の真下 (design: (6 + 58i, 246) 40x26 fs18)。
                float bottomX = bottomCenterX + bottomSpacing * i;
                V6Controls.Text(well, "Lane Label " + i, (i + 1).ToString(),
                    new Vector2(bottomX - 20f, 246f), new Vector2(40f, 26f), 18f, V6Tokens.TextMuted,
                    TextAlignmentOptions.Center);
            }
        }

        /// <summary>
        /// Display タブ: 判定色の凡例 (design EzLfb Judge PERFECT..MISS)。
        /// チップ (24,360) 106×64 r8 $k1 / Marker (37,16) 32×4 r2 判定色 /
        /// Label fs18 $text-dim 中央。判定色は HUD と同じ割り当て。
        /// </summary>
        private void BuildJudgeLegend(GameObject panel)
        {
            for (int i = 0; i < V6OptionLogic.JudgeLabels.Length; i++)
            {
                string label = V6OptionLogic.JudgeLabels[i];
                var chip = V6Controls.Panel(panel, "Judge " + label,
                    new Vector2(V6OptionLogic.JudgeChipX + V6OptionLogic.JudgeChipStep * i,
                        V6OptionLogic.JudgeChipY),
                    new Vector2(V6OptionLogic.JudgeChipW, V6OptionLogic.JudgeChipH),
                    V6Tokens.Key1, V6Tokens.RadiusMedium);

                var marker = Rect(chip, "Marker", V6OptionLogic.JudgeMarkerW, V6OptionLogic.JudgeMarkerH,
                    JudgeColor(label));
                V6Layout.Place(marker.GetComponent<RectTransform>(),
                    new Vector2(V6OptionLogic.JudgeMarkerX, V6OptionLogic.JudgeMarkerY),
                    new Vector2(V6OptionLogic.JudgeMarkerW, V6OptionLogic.JudgeMarkerH));

                V6Controls.Text(chip, "Label", label, new Vector2(0f, 22f),
                    new Vector2(V6OptionLogic.JudgeChipW, 26f), 18f, V6Tokens.TextDim,
                    TextAlignmentOptions.Center);
            }
        }

        /// <summary>判定色 (HUD の VerdictColor と同じ割り当て。design の $text/$ok/$warn/$danger)。</summary>
        private static Color JudgeColor(string label)
        {
            switch (label)
            {
                case "PERFECT": return V5Tokens.Text;
                case "GREAT": return V5Tokens.Ok;
                case "BAD": return V5Tokens.Warn;
                default: return V5Tokens.Danger;
            }
        }

        /// <summary>Divider + Desc Title + Desc (design 共通: Divider y544 / Title y564 h46 / Desc y614 w464 h68)。</summary>
        private void BuildDesc(GameObject panel, float dividerY, Row row)
        {
            var divider = Rect(panel, "Divider", 464f, 1f, V5Tokens.FromHex("#FFFFFF1F"));
            V6Layout.Place(divider.GetComponent<RectTransform>(), new Vector2(24f, dividerY), new Vector2(464f, 1f));

            // draw shape は「Title y564 h46」— 高さ 40 だと NotoSansJP の行送り (fs32 で約 46px) を
            // 下回り、TMP の Ellipsis が 1 行も描画しない (実行時に Title が丸ごと消えた原因)。
            // design と同じ h46 にし、Ellipsis は付けない (はみ出す場合も design は自動幅なので
            // 切り落とすより描く。値は短い固定文言のみ)。
            descTitleText = V6Controls.Text(panel, "Desc Title",
                row != null ? row.DescTitle : "",
                new Vector2(24f, dividerY + V6OptionLogic.DescTitleOffsetY),
                new Vector2(464f, 46f), 32f, V6Tokens.Text, TextAlignmentOptions.Left);

            // Desc は design 上で複数行に折り返す段落 (w464 h68 lineHeight1.4)。
            // NoWrap のままだと 1 行で右パネルの外まで伸びて切れていた。
            var desc = V6Controls.Text(panel, "Desc", row != null ? row.Desc : "",
                new Vector2(24f, dividerY + V6OptionLogic.DescOffsetY),
                new Vector2(V6OptionLogic.DescW, V6OptionLogic.DescH),
                24f, V6Tokens.Text, TextAlignmentOptions.TopLeft, FontStyles.Normal,
                TextWrappingModes.Normal);
            descBodyText = desc;
            // design lineHeight 1.4 (fs24 → 33.6px)。TMP の lineSpacing は fs への追加率で、
            // 40 にすると行送りが 34.75 + 24×0.40 = 44.35px になり、design 実測 (Data タブの
            // 2 行目: 457→492 = 35px) より 9px 下へずれて 2 行目がパネル下端 (688) に
            // 食い込んでいた (r24 実機)。0 (フォント本来の 34.75px) が design 実測と一致する。
            desc.lineSpacing = 0f;
        }

        private Row SelectedRowDesc()
        {
            if (currentRows.Count == 0)
            {
                return null;
            }

            return currentRows[selectedRow];
        }
    }
}
