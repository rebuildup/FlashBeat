using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FlashBeat.UI.V5;
using FlashBeat.UI.V6;
using FlashBeat.UI.V6.Common;

namespace FlashBeat.UI.V6.Result
{

    /// <summary>
    /// V6ResultView の結果・スコアゲージ・判定・楽曲情報ブロック部分
    /// (Issue #385 Phase 2.1 で分割)。partial class のためフィールド・
    /// メソッドは V6ResultView 本体と共有する。
    /// </summary>
    public sealed partial class V6ResultView
    {
        // 結果 (列1-3) (0,24) 840x280。
        private const float ResultBlockY = 24f;
        private const float GradeBoxW = 280f, GradeBoxH = 280f;
        private const float ScoreColX = 336f;
        // スコアゲージ (0,336) 840x100 → Gauge (0,16) 824x76。
        private const float GaugeZoneY = 336f;
        private const float GaugeY = 16f;
        private const float GaugeTrackY = 24f, GaugeTrackH = 16f;

        // 判定 (0,448) 840x140。5 セル x=0,168,336,504,672 各 160 幅。
        private const float JudgeY = 448f;
        private static readonly float[] JudgeX = { 0f, 168f, 336f, 504f, 672f };
        private const float JudgeCellW = 160f, JudgeCellH = 140f;

        // 楽曲情報 (0,636) 840x120 の寸法は V6ResultLogic.SongInfo* (テストと共有)。

        // 動的に更新する要素 (Refresh で差し替える)。
        private TextMeshProUGUI gradeLetter;
        private TextMeshProUGUI scoreValue;
        private TextMeshProUGUI bestValue;
        private readonly TextMeshProUGUI[] judgeValues = new TextMeshProUGUI[5];
        private Image gaugeFill;
        private Image gaugeBestMarker;
        private TextMeshProUGUI songTitle;
        private TextMeshProUGUI songArtist;
        private readonly TextMeshProUGUI[] songStatValues = new TextMeshProUGUI[4];
        private Image songThumb;

        // 結果 (0,24) 840x280: グレード文字 + SCORE + 前回最高。

        private void BuildResultBlock(GameObject main)
        {
            var block = V6Layout.Place(V5Ui.Node(main, "Result Block"), 0f, ResultBlockY, 840f, GradeBoxH);

            // design (B7Ko8) は fill なし (背景パネルを敷かない)。以前あった可読性目的の
            // スクリーンは設計に無く、デザインとの差分になっていたので描かない。
            // トロフィーはヘッダーの左上スロットが持つ (BuildHeader)。
            var gradeBox = V6Layout.Place(V5Ui.Node(block, "Grade"), 0f, 0f, GradeBoxW, GradeBoxH);
            gradeLetter = V5GameUi.Text(gradeBox.gameObject, "Letter", "–", 240f, V5Tokens.Text,
                TextAlignmentOptions.Center, FontStyles.Bold);
            V6Layout.StretchCenter(gradeLetter.GetComponent<RectTransform>(), GradeBoxW, GradeBoxH);

            var scoreLabel = V5GameUi.Text(block.gameObject, "Score Label", "SCORE", 20f, V5Tokens.TextDim);
            V6Layout.Place(scoreLabel, ScoreColX, 48f, 400f, 26f, new Vector2(0f, 0.5f));

            scoreValue = OneLine(V5GameUi.Text(block.gameObject, "Score Value", "0", 72f, V5Tokens.Text,
                TextAlignmentOptions.Left, FontStyles.Normal));
            V6Layout.Place(scoreValue, ScoreColX, 80f, 500f, 80f, new Vector2(0f, 0.5f));

            var bestLabel = V5GameUi.Text(block.gameObject, "Best Label", "前回最高", 20f, V5Tokens.TextDim);
            V6Layout.Place(bestLabel, ScoreColX, 206f, 300f, 26f, new Vector2(0f, 0.5f));

            bestValue = OneLine(V5GameUi.Text(block.gameObject, "Best Value", "–", 32f, V5Tokens.Text));
            V6Layout.Place(bestValue, ScoreColX, 232f, 400f, 40f, new Vector2(0f, 0.5f));

            // 段階表示 (#384): 各要素は CanvasGroup のアルファで出す。
            // トロフィーのグループはヘッダー側で作る (BuildHeader)。
            gradeGroup = FadeGroup(gradeBox);
            gradeRect = gradeBox.GetComponent<RectTransform>();
            scoreLabelGroup = FadeGroup(scoreLabel.gameObject);
            scoreValueGroup = FadeGroup(scoreValue.gameObject);
            bestLabelGroup = FadeGroup(bestLabel.gameObject);
            bestValueGroup = FadeGroup(bestValue.gameObject);
        }

        // スコアゲージ (0,336) 840x100 → Gauge (0,16) 824x76。

        private void BuildGauge(GameObject main)
        {
            var zone = V6Layout.Place(V5Ui.Node(main, "Score Gauge Zone"), 0f, GaugeZoneY, 840f, 100f);
            var gauge = V6Layout.Place(V5Ui.Node(zone, "Score Gauge"), 0f, GaugeY, V6ResultLogic.GaugeW, 76f);

            // セグメント (y24 h16): C(0,165) B(168,164) A(335,165) S(503,164) SS(670,55) +(728,96)。
            float[] segX = { 0f, 168f, 335f, 503f, 670f, 728f };
            float[] segW = { 165f, 164f, 165f, 164f, 55f, 96f };
            string[] segNames = { "C", "B", "A", "S", "SS", "+" };
            for (int i = 0; i < segX.Length; i++)
            {
                Color fill = i == 5 ? V5Tokens.FromHex("#3A3220") : V5Tokens.Key0;
                V6Layout.Place(V5Ui.Panel(gauge.gameObject, "Seg " + segNames[i], new Vector2(segW[i], GaugeTrackH), fill, 2f),
                    segX[i], GaugeTrackY);
                var label = V5GameUi.Text(gauge.gameObject, "Label " + segNames[i], segNames[i], 16f,
                    V5Tokens.TextDim, TextAlignmentOptions.Center);
                V6Layout.Place(label, segX[i], 46f, segW[i], 20f, new Vector2(0f, 0.5f));
            }

            gaugeFill = V6Layout.Place(V5Ui.Panel(gauge.gameObject, "Fill",
                new Vector2(0f, GaugeTrackH), V5Tokens.FromHex("#EDEDED"), 2f), 0f, GaugeTrackY).GetComponent<Image>();

            gaugeBestMarker = V6Layout.Place(V5Ui.Panel(gauge.gameObject, "Best Marker", new Vector2(4f, 24f),
                V5Tokens.Text, 0f), 0f, 20f).GetComponent<Image>();
            var bestLabel = V5GameUi.Text(gauge.gameObject, "Best Label", "BEST", 14f, V5Tokens.TextMuted,
                TextAlignmentOptions.Center);
            V6Layout.Place(bestLabel, 734f, 0f, 56f, 18f, new Vector2(0f, 0.5f));

            gaugeGroup = FadeGroup(zone);
        }

        // 判定 (0,448) 840x140: Perfect / Great / Bad / Miss / Combo。

        private void BuildJudgements(GameObject main)
        {
            var block = V6Layout.Place(V5Ui.Node(main, "Judgements"), 0f, JudgeY, 840f, JudgeCellH);

            // design (EpX3e) は fill なし。可読性目的のスクリーンは設計に無いため描かない。
            string[] labels = { "Perfect", "Great", "Bad", "Miss", "Combo" };
            for (int i = 0; i < labels.Length; i++)
            {
                var cell = V6Layout.Place(V5Ui.Node(block, "J " + labels[i]), JudgeX[i], 0f, JudgeCellW, JudgeCellH);

                // design S5n3P: Label (0,0) 66×29 fs20 / Value (0,43) 83×70 fs48、
                // どちらも左上揃え。以前は VerticalLayoutGroup に任せていたため、
                // TMP の中央揃え (TextAlignmentOptions.Left) と子の既定サイズが
                // 混ざり、判定行だけ design より約 11px 下に描かれていた (r13 実機)。
                var judgeLabel = V5GameUi.Text(cell.gameObject, "Label", labels[i], 20f,
                    V5Tokens.TextDim, TextAlignmentOptions.TopLeft);
                V6Layout.Place(judgeLabel, 0f, 0f, JudgeCellW, 29f, new Vector2(0f, 1f));
                judgeValues[i] = OneLine(V5GameUi.Text(cell.gameObject, "Value", "0", 48f, V5Tokens.Text,
                    TextAlignmentOptions.TopLeft, FontStyles.Normal));
                V6Layout.Place(judgeValues[i], 0f, 43f, JudgeCellW, 70f, new Vector2(0f, 1f));
                judgeGroups[i] = FadeGroup(cell);
            }
        }

        // 楽曲情報 (0,636) 840x120: Thumb + Title/Artist + BPM/NOTES/LEVEL/曲長。
        // 寸法は V6ResultLogic.SongInfo* (テストと共有)。

        private void BuildSongInfo(GameObject main)
        {
            var panel = V6Layout.Place(V5Ui.Panel(main.gameObject, "Song Info",
                    new Vector2(V6ResultLogic.SongInfoW, V6ResultLogic.SongInfoH), V5Tokens.Key2, 16f),
                0f, V6ResultLogic.SongInfoY);

            // 曲バッジ (Thumb + Title/Artist、列ノード無し) は共通部品
            // (Issue #385 Phase 1.2)。寸法は V6ResultLogic.SongInfo* / SongThumb*。
            var badge = V6SongBadge.Create(panel.gameObject, new V6SongBadge.Spec
            {
                ThumbPos = new Vector2(V6ResultLogic.SongThumbX, V6ResultLogic.SongThumbY),
                ThumbSize = new Vector2(V6ResultLogic.SongThumbW, V6ResultLogic.SongThumbH),
                ThumbColor = V5Tokens.Key3,
                Title = new V6SongBadge.Line
                {
                    Pos = new Vector2(V6ResultLogic.SongTitleX, V6ResultLogic.SongTitleY),
                    Size = new Vector2(V6ResultLogic.SongTitleW, V6ResultLogic.SongTitleH),
                    FontSize = 34f, Color = V5Tokens.Text,
                    // design は pivot (0,0.5) で配置 (At の pivot 引数と同じ式)。
                    Pivot = new Vector2(0f, 0.5f),
                    // 実データは design のサンプルより長いので 1 行固定 + ellipsis。
                    FitLineHeight = true,
                },
                Sub = new V6SongBadge.Line
                {
                    Name = "Artist",
                    Pos = new Vector2(V6ResultLogic.SongArtistX, V6ResultLogic.SongArtistY),
                    Size = new Vector2(V6ResultLogic.SongArtistW, V6ResultLogic.SongArtistH),
                    FontSize = 23f, Color = V5Tokens.TextMuted,
                    Pivot = new Vector2(0f, 0.5f),
                    FitLineHeight = true,
                },
            });
            songThumb = badge.Thumb.GetComponent<Image>();
            songTitle = badge.Title;
            songArtist = badge.Sub;

            // Stat (BPM/NOTES/LEVEL/曲長) は Label + Value を明示矩形で縦積みする
            // (旧実装は Column レイアウトで TMP の既定サイズにレイアウトが追従できず、
            // 値がバンドの下へはみ出していた)。
            string[] statLabels = { "BPM", "NOTES", "LEVEL", "曲長" };
            for (int i = 0; i < statLabels.Length; i++)
            {
                var cell = V6Layout.Place(V5Ui.Node(panel.gameObject, "Stat " + statLabels[i]),
                    V6ResultLogic.SongStatX[i], V6ResultLogic.SongStatY,
                    V6ResultLogic.SongStatW, V6ResultLogic.SongStatH);
                var label = V5GameUi.Text(cell.gameObject, "Label", statLabels[i], 18f, V5Tokens.TextDim);
                V6Layout.Place(label, 0f, 0f, V6ResultLogic.SongStatW, V6ResultLogic.SongStatLabelH);
                songStatValues[i] = OneLine(V5GameUi.Text(cell.gameObject, "Value", "–", 30f, V5Tokens.Text,
                    TextAlignmentOptions.Left, FontStyles.Normal));
                V6Layout.Place(songStatValues[i], 0f, V6ResultLogic.SongStatValueY,
                    V6ResultLogic.SongStatW, V6ResultLogic.SongStatValueH);
            }

            lateGroups.Add(FadeGroup(panel));
        }

        private void RefreshResult()
        {
            int score = runScore;
            gradeLetter.text = V6ResultLogic.GradeOf(score);
            scoreValue.text = V6ResultLogic.FormatScore(score);
            judgeValues[0].text = runPerfect.ToString();
            judgeValues[1].text = runGreat.ToString();
            judgeValues[2].text = runBad.ToString();
            judgeValues[3].text = runMiss.ToString();
            judgeValues[4].text = runMaxCombo.ToString();

            float fill = V6ResultLogic.GaugeFillWidth(score);
            gaugeFill.rectTransform.sizeDelta = new Vector2(fill, GaugeTrackH);
            int best = BestOf();
            float bestX = V6ResultLogic.GaugeBestX(best);
            gaugeBestMarker.rectTransform.anchoredPosition = new Vector2(bestX, -20f);

            if (prevHiscore >= 0)
            {
                string delta = V6ResultLogic.DeltaLabel(prevHiscore, score);
                bestValue.text = delta.Length > 0 ? delta : V6ResultLogic.FormatScore(prevHiscore);
            }
            else
            {
                bestValue.text = "–";
            }
        }

        private void RefreshSongInfo()
        {
            SongData song = SongOf();
            if (song == null)
            {
                songTitle.text = "";
                songArtist.text = "";
                if (songRankTitle != null)
                {
                    songRankTitle.text = "";
                }

                for (int i = 0; i < songStatValues.Length; i++)
                {
                    songStatValues[i].text = "–";
                }

                return;
            }

            songTitle.text = song.title ?? "";
            songArtist.text = song.musician ?? "";
            if (songRankTitle != null)
            {
                songRankTitle.text = song.title ?? "";
            }
            songStatValues[0].text = song.bpm > 0 ? song.bpm.ToString() : "–";
            songStatValues[1].text = song.totalHits > 0 ? song.totalHits.ToString() : "–";
            songStatValues[2].text = song.level > 0 ? song.level.ToString() : "–";
            songStatValues[3].text = V6ResultLogic.DurationText(SongLongOf());

            SetThumb(songThumb, song);
        }
    }
}
