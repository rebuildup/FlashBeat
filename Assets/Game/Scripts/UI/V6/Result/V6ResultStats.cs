using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FlashBeat.Gameplay;
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
        // 結果 (列1-3) (0,24) 840x280。グレード矩形の寸法は
        // V6ResultLogic.GradeBoxW/H (テストと共有)。
        private const float ResultBlockY = 24f;
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
        private readonly TextMeshProUGUI[] bonusValues = new TextMeshProUGUI[4];
        private TextMeshProUGUI songTitle;
        private TextMeshProUGUI songArtist;
        private readonly TextMeshProUGUI[] songStatValues = new TextMeshProUGUI[4];
        private Image songThumb;

        // 結果 (0,24) 840x280: グレード文字 + SCORE + 前回最高。

        private void BuildResultBlock(GameObject main)
        {
            var block = V6Layout.Place(V5Ui.Node(main, "Result Block"), 0f, ResultBlockY, 840f, V6ResultLogic.GradeBoxH);

            // design (B7Ko8) は fill なし (背景パネルを敷かない)。以前あった可読性目的の
            // スクリーンは設計に無く、デザインとの差分になっていたので描かない。
            // トロフィーはヘッダーの左上スロットが持つ (BuildHeader)。
            var gradeBox = V6Layout.Place(V5Ui.Node(block, "Grade"), 0f, 0f,
                V6ResultLogic.GradeBoxW, V6ResultLogic.GradeBoxH);
            // "SS" のような複数グリフは fs240 では矩形幅を超えて折り返し・
            // SCORE 列へのはみ出しを起こす (Issue #737)。1 行固定にし、
            // 実際のフォントサイズは RefreshResult の FitGradeFont が
            // インク実測で収まる値へ決める。
            gradeLetter = OneLine(V5GameUi.Text(gradeBox.gameObject, "Letter", "–",
                V6ResultLogic.GradeMaxFontSize, V5Tokens.Text,
                TextAlignmentOptions.Center, FontStyles.Bold));
            V6Layout.StretchCenter(gradeLetter.GetComponent<RectTransform>(),
                V6ResultLogic.GradeBoxW, V6ResultLogic.GradeBoxH);

            var scoreLabel = V5GameUi.Text(block.gameObject, "Score Label", "SCORE", 20f, V5Tokens.TextDim);
            V6Layout.Place(scoreLabel, ScoreColX, 48f, 400f, 26f, new Vector2(0f, 0.5f));

            scoreValue = OneLine(V5GameUi.Text(block.gameObject, "Score Value", "0", 72f, V5Tokens.Text,
                TextAlignmentOptions.Left, FontStyles.Normal));
            // 100 万点超 (scoring v4) やカンマ区切りの桁増しでも矩形から
            // はみ出さないよう、TMP の auto-size で縮小を許す。
            AutoFit(scoreValue, 44f);
            V6Layout.Place(scoreValue, ScoreColX, 80f, 500f, 80f, new Vector2(0f, 0.5f));

            var bestLabel = V5GameUi.Text(block.gameObject, "Best Label", "前回最高", 20f, V5Tokens.TextDim);
            V6Layout.Place(bestLabel, ScoreColX, 206f, 300f, 26f, new Vector2(0f, 0.5f));

            bestValue = OneLine(V5GameUi.Text(block.gameObject, "Best Value", "–", 32f, V5Tokens.Text));
            AutoFit(bestValue, 20f);
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

            // セグメントは V6GradeLogic.GradeBandsOf (グレード閾値の正本、
            // Issue #788) をトラック幅へ写した値。「満点のみ」の SS は
            // 幅を持たない点帯なので、右端に接する細いキャップとして描く。
            // 旧デザインの固定座標 (C 165 / B 164 / A 165 / S 164 / SS 55 / + 96)
            // ではグレード境界と目盛りが一致しなかった。
            var bands = Common.V6GradeLogic.GradeBandsOf(FlashBeat.Gameplay.Scoring.LatestVersion);
            float pointW = V6ResultLogic.GaugeW / 150f;
            foreach (var band in bands)
            {
                if (band.IsPoint)
                {
                    V6Layout.Place(V5Ui.Panel(gauge.gameObject, "Seg " + band.Label,
                        new Vector2(pointW, GaugeTrackH), V5Tokens.FromHex("#3A3220"), 2f),
                        V6ResultLogic.GaugeW - pointW, GaugeTrackY);
                    var capLabel = V5GameUi.Text(gauge.gameObject, "Label " + band.Label, band.Label, 16f,
                        V5Tokens.TextDim, TextAlignmentOptions.Center);
                    V6Layout.Place(capLabel, V6ResultLogic.GaugeW - pointW * 2f, 46f, pointW * 4f, 20f,
                        new Vector2(0f, 0.5f));
                    continue;
                }

                float segX = band.Start * V6ResultLogic.GaugeW;
                float segW = (band.End - band.Start) * V6ResultLogic.GaugeW;
                V6Layout.Place(V5Ui.Panel(gauge.gameObject, "Seg " + band.Label,
                    new Vector2(segW, GaugeTrackH), V5Tokens.Key0, 2f), segX, GaugeTrackY);
                var label = V5GameUi.Text(gauge.gameObject, "Label " + band.Label, band.Label, 16f,
                    V5Tokens.TextDim, TextAlignmentOptions.Center);
                V6Layout.Place(label, segX, 46f, segW, 20f, new Vector2(0f, 0.5f));
            }

            gaugeFill = V6Layout.Place(V5Ui.Panel(gauge.gameObject, "Fill",
                new Vector2(0f, GaugeTrackH), V5Tokens.FromHex("#EDEDED"), 2f), 0f, GaugeTrackY).GetComponent<Image>();

            // SS キャップが塗りで隠れないよう、Fill の後に最前面へ。
            var ssCap = gauge.transform.Find("Seg SS");
            if (ssCap != null)
            {
                ssCap.SetAsLastSibling();
            }

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
                // 判定数・最大コンボは譜面によって桁が増えるため auto-size で縮小を許す。
                AutoFit(judgeValues[i], 30f);
                V6Layout.Place(judgeValues[i], 0f, 43f, JudgeCellW, 70f, new Vector2(0f, 1f));
                judgeGroups[i] = FadeGroup(cell);
            }
        }

        // ボーナス内訳 (ADR-0028 / Issue #577): 判定行 (0,448 h140 → 588 終了)
        // と楽曲情報 (SongInfoY=636) の隙間に置く 1 行。
        private const float BonusLineY = 592f;
        private const float BonusLineH = 40f;

        /// <summary>
        /// scoring v4 (ADR-0033 / Issue #736) の基礎スコア + ボーナス内訳行。
        /// 判定由来の基礎スコア (上限 1,000,000) と、コンボ継続・節目・
        /// クリア (ノーミス + フルコンボ終了) のボーナス群を、実際に
        /// 加算されるスコア点で出す。合計が表示スコア (最大 2,000,000)
        /// に対応する。
        /// </summary>
        private void BuildBonusLine(GameObject main)
        {
            var row = V6Layout.Place(V5Ui.Node(main, "Bonus Line"), 0f, BonusLineY, 840f, BonusLineH);

            var label = V5GameUi.Text(row.gameObject, "Label", "BASE + BONUS", 18f, V5Tokens.TextDim,
                TextAlignmentOptions.Left);
            V6Layout.Place(label, 0f, 8f, 100f, 28f, new Vector2(0f, 0.5f));

            // scoring v6 (ADR-0037) では 2 列目はコンボではなくノーツ品質点
            // (P*1000+G*600+B*200 の絶対点) を表す。
            string[] names = { "BASE", "QUALITY", "MILESTONE", "CLEAR" };
            string[] keys = { "Base Score", "Note Quality", "Milestone Bonus", "Clear Bonus" };
            const float colX = 110f;
            const float colW = (840f - colX) / 4f;
            for (int i = 0; i < 4; i++)
            {
                float x = colX + i * colW;
                var colLabel = V5GameUi.Text(row.gameObject, keys[i] + " Label", names[i], 13f,
                    V5Tokens.TextDim, TextAlignmentOptions.Left);
                V6Layout.Place(colLabel, x, 6f, colW, 14f, new Vector2(0f, 1f));
                bonusValues[i] = OneLine(V5GameUi.Text(row.gameObject, keys[i] + " Value", "–", 24f,
                    V5Tokens.Text, TextAlignmentOptions.Left, FontStyles.Bold));
                AutoFit(bonusValues[i], 16f);
                V6Layout.Place(bonusValues[i], x, 18f, colW, 22f, new Vector2(0f, 1f));
            }

            lateGroups.Add(FadeGroup(row));
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
                // NOTES など桁の大きい値でも Stat セル (116 幅) からはみ出さないよう縮小を許す。
                AutoFit(songStatValues[i], 20f);
                V6Layout.Place(songStatValues[i], 0f, V6ResultLogic.SongStatValueY,
                    V6ResultLogic.SongStatW, V6ResultLogic.SongStatValueH);
            }

            lateGroups.Add(FadeGroup(panel));
        }

        private void RefreshResult()
        {
            int score = runScore;
            // scoring v6 (ADR-0037) のグレード・ゲージは譜面満点
            // ScoreMaxV6(noteCount) で正規化するためノーツ数が必要。
            // Issue #870: HUD と同じ正本 (GManager.chartNoteCount) を使う。
            // 確定済み判定合計だと未判定残りのある終了で分母がずれ、
            // HUD のグレードと Result が一致しなくなる。
            int noteCount = V6ResultLogic.ResultNoteCount(
                GManager.chartNoteCount, runPerfect, runGreat, runBad, runMiss);
            gradeLetter.text = V6ResultLogic.GradeOf(score, Scoring.LatestVersion, noteCount);
            FitGradeFont();
            scoreValue.text = V6ResultLogic.FormatScore(score);
            judgeValues[0].text = runPerfect.ToString();
            judgeValues[1].text = runGreat.ToString();
            judgeValues[2].text = runBad.ToString();
            judgeValues[3].text = runMiss.ToString();
            judgeValues[4].text = runMaxCombo.ToString();

            // scoring v4/v6 の基礎スコア + ボーナス内訳 (ADR-0033/0037,
            // Issue #736, #825)。ノーツ数は確定済みの判定合計 = 譜面の
            // ノーツ数 (完走後なので残りは無い)。
            V6ResultLogic.BonusBreakdownTexts(
                runPerfect, runGreat, runBad, runMiss, runMaxCombo, runComboMilestones, noteCount,
                GManager.judgmentsComplete,
                out string baseScoreText, out string comboBonusText,
                out string milestoneBonusText, out string clearBonusText);
            bonusValues[0].text = baseScoreText;
            bonusValues[1].text = comboBonusText;
            bonusValues[2].text = milestoneBonusText;
            bonusValues[3].text = clearBonusText;

            float fill = V6ResultLogic.GaugeFillWidth(score, noteCount);
            gaugeFill.rectTransform.sizeDelta = new Vector2(fill, GaugeTrackH);
            int best = BestOf();
            float bestX = V6ResultLogic.GaugeBestX(best, noteCount);
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

        // --- テキストの収まり (Issue #737) ---

        /// <summary>
        /// 数値・短い動的テキストに TMP の auto-size を付ける。桁数が増えても
        /// (100 万点超のスコア、判定・コンボの大きな値) 矩形からはみ出さないよう、
        /// fontSizeMin..fontSizeMax (現在の fontSize が上限) の範囲へ縮める。
        /// min は可読性の下限で、これを下回る値は設計上あり得ない想定。
        /// 実装は V6 共通規約 <see cref="V6TextFit.AutoFit"/> (Issue #749 で集約)。
        /// </summary>
        private static void AutoFit(TextMeshProUGUI tmp, float minFontSize)
        {
            V6TextFit.AutoFit(tmp, minFontSize);
        }

        /// <summary>
        /// グレード文字を Grade 箱 (V6ResultLogic.GradeBoxW×GradeBoxH) に収める
        /// フォントサイズへ調整する。"SS" のような複数グリフは design の fs240 では
        /// 箱の幅を超えて SCORE 列へはみ出す (Issue #737) ため、TMP のインク実測
        /// (textInfo のメッシュ頂点の外接矩形) が内側余白に収まる最大 fs へ縮める。
        /// 単一グリフ (S/A/B/C/–) は fs240 のまま変わらない。
        /// </summary>
        private void FitGradeFont()
        {
            gradeLetter.fontSize = V6ResultLogic.GradeMaxFontSize;
            // TMP の文字サイズはレイアウト時にしか確定しないため、収まり判定は
            // 最大 fs で一度組んだ textInfo の頂点を実測して行う
            // (フォントサイズにほぼ比例するので倍率で縮められる)。
            gradeLetter.ForceMeshUpdate(true, true);
            float scale = InkScale(gradeLetter,
                V6ResultLogic.GradeBoxW - V6ResultLogic.GradePadX * 2f,
                V6ResultLogic.GradeBoxH - V6ResultLogic.GradePadY * 2f);
            gradeLetter.fontSize = Mathf.Clamp(
                V6ResultLogic.GradeMaxFontSize * scale,
                V6ResultLogic.GradeMinFontSize, V6ResultLogic.GradeMaxFontSize);
        }

        /// <summary>
        /// TMP textInfo のメッシュ頂点からインクの外接矩形を取り、availW/availH に
        /// 収まる倍率 (最大 1) を返す。実測できないとき (フォント未解決など) は
        /// <see cref="V6Ui.LabelAdvanceEm"/> の推定送り幅で幅だけ縮める。
        /// </summary>
        private static float InkScale(TextMeshProUGUI tmp, float availW, float availH)
        {
            float minX = float.MaxValue, maxX = float.MinValue;
            float minY = float.MaxValue, maxY = float.MinValue;
            TMP_TextInfo info = tmp.textInfo;
            if (info != null && info.meshInfo != null)
            {
                // vertices 配列は容量分まで確保されることがあるため、
                // 実文字数 (4 頂点/文字) までに限定して未使用の退化 quad を除く。
                int quadLimit = info.characterCount * 4;
                for (int m = 0; m < info.meshInfo.Length; m++)
                {
                    Vector3[] verts = info.meshInfo[m].vertices;
                    if (verts == null)
                    {
                        continue;
                    }

                    int limit = Mathf.Min(verts.Length, quadLimit);
                    for (int i = 0; i + 3 < limit; i += 4)
                    {
                        for (int k = 0; k < 4; k++)
                        {
                            Vector3 v = verts[i + k];
                            if (v.x < minX) { minX = v.x; }
                            if (v.x > maxX) { maxX = v.x; }
                            if (v.y < minY) { minY = v.y; }
                            if (v.y > maxY) { maxY = v.y; }
                        }
                    }
                }
            }

            float inkW = maxX - minX;
            float inkH = maxY - minY;
            if (inkW <= 0f || inkH <= 0f)
            {
                // 実測できないときのフォールバック: 推定送り幅 (em) で幅だけ縮める。
                float estW = V6Ui.LabelAdvanceEm(tmp.text) * tmp.fontSize;
                return estW > availW && estW > 0f ? availW / estW : 1f;
            }

            return Mathf.Min(1f, Mathf.Min(availW / inkW, availH / inkH));
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
