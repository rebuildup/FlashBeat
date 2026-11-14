using System.Collections.Generic;
using FlashBeat.UI.V5;
using FlashBeat.UI.V6.Common;
using FlashBeat.UI.V6.Game.Playfield;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace FlashBeat.UI.V6.Game.Hud
{
    /// <summary>
    /// V6GameHudView のスコア / グレード / スコアゲージ / コンボブロック /
    /// コンボステータス (右コラム) 部分 (Issue #385 Phase 2.2 で分割)。
    /// partial class のためフィールド・メソッドは V6GameHudView 本体と共有する。
    /// </summary>
    public sealed partial class V6GameHudView
    {
        // --- 生成した部品 (スコア系) ---
        private TextMeshProUGUI gradeText;
        /// <summary>スコア値の桁別ストリップ (Issue #445: 1 文字 = 1 スロット)。</summary>
        private ValueStrip scoreStrip;
        /// <summary>コンボ値の桁別ストリップ (0 のときは値自体を隠す)。</summary>
        private ValueStrip comboStrip;
        private RectTransform gaugeFill;
        private RectTransform gaugeBest;
        private RectTransform comboFill;
        private GameObject comboStatusBlock;
        private GameObject comboStatusRow;
        private GameObject fullComboChip;

        // --- 毎フレームの状態 (スコア系) ---
        /// <summary>最後に文字として出した値 (同じ値なら TextMeshPro を触らない。#381)。</summary>
        private int lastRenderedScore = int.MinValue;
        private int lastRenderedCombo = int.MinValue;

        // --- Issue #434: フィードバック強化 ---
        /// <summary>コンボボーナス塗りの Image (変化時に色をワンショットで発光させる)。</summary>
        private Image comboFillImage;
        /// <summary>コンボボーナス塗りの発光開始時刻 (-1 = 停止中)。</summary>
        private float comboBonusPulseStart = -1f;
        /// <summary>前フレームのコンボボーナス塗り幅 (変化検出用)。</summary>
        private float lastComboFillWidth = -1f;
        /// <summary>スコアゲージ塗りの表示幅 (tween 中の値)。</summary>
        private float gaugeShownWidth = -1f;
        /// <summary>スコアゲージ Best マーカーの表示 x (tween 中の値)。</summary>
        private float gaugeBestShownX;
        /// <summary>ゲージ tween の初期化済みフラグ (初回は目標値へ即合わせる)。</summary>
        private bool gaugeShownInit;

        private void BuildGrade()
        {
            gradeText = V5Ui.Text(gameObject, "Grade", "C", m.GradeFs, V5Tokens.Text,
                TextAlignmentOptions.TopLeft, FontStyles.Bold);
            var rt = gradeText.rectTransform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            // Issue #434: グレードが一段低く見えるので、スコアラベル半行分だけ上へ
            // (上端アンカーなので y を 0 に近づける = 画面上方向)。
            rt.anchoredPosition = new Vector2(m.Margin, -m.Margin + m.ScoreLabelFs * 0.5f);
            rt.sizeDelta = new Vector2(m.GaugeX - m.Margin, m.GradeFs * 1.2f);
        }

        private void BuildScoreGauge()
        {
            var gauge = V5Ui.Node(gameObject, "Score Gauge");
            V5Ui.Place(V6Layout.RectOf(gauge), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(m.GaugeX, -m.GaugeY), new Vector2(m.GaugeW, m.GaugeH));

            // Track (k0, 角丸 2)
            var track = V5Ui.Panel(gauge, "Track", new Vector2(m.GaugeW, m.GaugeTrackH),
                V5Tokens.Key0, 2f);
            var trackRt = V6Layout.RectOf(track);
            trackRt.anchorMin = new Vector2(0f, 1f);
            trackRt.anchorMax = new Vector2(0f, 1f);
            trackRt.pivot = new Vector2(0f, 1f);
            trackRt.anchoredPosition = Vector2.zero;

            // セグメント C/B/A/S/SS/+ (design は track と同色。+ だけ契約 §8 の #3A3220)。
            for (int i = 0; i < m.GaugeSegX.Length; i++)
            {
                var seg = V5Ui.Panel(track, "Seg " + i,
                    new Vector2(m.GaugeSegW[i], m.GaugeSegH),
                    i == m.GaugeSegX.Length - 1 ? V6HudLogic.AccentBase : V5Tokens.Key0, 2f);
                var segRt = V6Layout.RectOf(seg);
                segRt.anchorMin = new Vector2(0f, 0f);
                segRt.anchorMax = new Vector2(0f, 0f);
                segRt.pivot = new Vector2(0f, 0.5f);
                segRt.anchoredPosition = new Vector2(m.GaugeSegX[i], m.GaugeSegY);
            }

            // 塗り (Fill)
            var fill = V5Ui.Panel(track, "Fill",
                new Vector2(0f, m.GaugeFillH), V5Tokens.Text, 2f);
            var fillRt = V6Layout.RectOf(fill);
            fillRt.anchorMin = new Vector2(0f, 1f);
            fillRt.anchorMax = new Vector2(0f, 1f);
            fillRt.pivot = new Vector2(0f, 1f);
            fillRt.anchoredPosition = new Vector2(0f, 0f);
            gaugeFill = fillRt;

            // Best マーカー
            var best = V5Ui.Panel(track, "Best",
                new Vector2(m.GaugeBestW, m.GaugeBestH), V5Tokens.Warn, 2f);
            var bestRt = V6Layout.RectOf(best);
            bestRt.anchorMin = new Vector2(0f, 0.5f);
            bestRt.anchorMax = new Vector2(0f, 0.5f);
            bestRt.pivot = new Vector2(0f, 0.5f);
            bestRt.anchoredPosition = Vector2.zero;
            gaugeBest = bestRt;

            // ラベル行 (C B A S SS +)
            string[] labels = { "C", "B", "A", "S", "SS", "+" };
            for (int i = 0; i < labels.Length; i++)
            {
                var label = V5Ui.Text(gauge, "Label " + labels[i], labels[i], m.GaugeLabelFs,
                    V5Tokens.TextMuted, TextAlignmentOptions.Center);
                var labelRt = label.rectTransform;
                labelRt.anchorMin = new Vector2(0f, 1f);
                labelRt.anchorMax = new Vector2(0f, 1f);
                labelRt.pivot = new Vector2(0f, 0f);
                labelRt.anchoredPosition = new Vector2(
                    m.GaugeSegX[i] + m.GaugeSegW[i] * 0.5f, -m.GaugeLabelY);
                labelRt.sizeDelta = new Vector2(m.GaugeSegW[i], m.GaugeLabelFs * 1.4f);
            }
        }

        private void BuildScoreTexts()
        {
            var label = V5Ui.Text(gameObject, "SCORE", "SCORE", m.ScoreLabelFs, V5Tokens.TextMuted,
                TextAlignmentOptions.TopLeft);
            PlaceTopLeft(label.rectTransform, m.GaugeX, -m.ScoreLabelY);

            // Issue #445: スコア値は 1 文字 = 1 スロットの桁別ストリップ。
            // 文字が変わった桁だけがポップ + 発光し、変わらない桁は動かない。
            // ',' (3 桁区切り) は静的な区切りとして表示だけする (アニメーション無し)。
            // 旧来の全体 autosize は廃止: スロット幅をフォントの ASCII 送り幅
            // (0.62em) で固定するので "1,000,000" (9 文字) でも
            // FHD で約 213px / 2K で約 279px とゲージ幅に十分収まる。
            scoreStrip = CreateValueStrip(gameObject, "Score Value",
                new Vector2(m.GaugeX, -m.ScoreValueY),
                new Vector2(m.GaugeW, m.ScoreFs * 1.4f),
                m.ScoreFs, V5Tokens.Text, V5Tokens.WarnLight);
        }

        private void BuildComboBlock()
        {
            // 契約 §8: Combo 左上は design px 絶対座標 (FHD (48,430) / 2K (64,573) / Mac (64,685))。
            // 1:1 固定配置なので中央アンカー + オフセット計算は不要 (左上アンカーでそのまま置く)。
            var block = V5Ui.Node(gameObject, "Combo Block");
            var rt = V6Layout.RectOf(block);
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(m.ComboX, -m.ComboY);
            rt.sizeDelta = new Vector2(m.ComboW, m.ComboH);

            var label = V5Ui.Text(block, "COMBO", "COMBO", m.ComboLabelFs, V5Tokens.TextMuted,
                TextAlignmentOptions.TopLeft);
            PlaceTopLeft(label.rectTransform, 0f, 0f);

            // Issue #445: コンボ値も桁別ストリップ。0 のときは値自体を隠し、
            // 変わった桁だけがポップ + 発光する (全体ポップは廃止)。
            comboStrip = CreateValueStrip(block, "Value",
                new Vector2(0f, -m.ComboValueY),
                new Vector2(m.ComboW, m.ComboValueFs * 1.4f),
                m.ComboValueFs, V5Tokens.Text, V5Tokens.AccentLight);
        }

        private void BuildRightColumn()
        {
            // 両 block は同座標 (排他) なので、コラムの高さは高い方 = ステータス block。
            // 契約 §8: 右コラム左上は design px (FHD (1512,460) / 2K・Mac (2016,613/725))。
            // 右端アンカーで置く (右端 = design 右マージン: FHD 48 / 2K・Mac 64) ので、
            // 未知解像度でプロファイルを流用したときも画面外へはみ出さない。
            float columnH = m.StatusBlockH;
            var column = V5Ui.Node(gameObject, "Right Column");
            var rt = V6Layout.RectOf(column);
            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-m.Margin, -m.RightColY);
            rt.sizeDelta = new Vector2(m.RightColW, columnH);

            // --- コンボステータス ---
            // issue-162 B-4 再照合: ここにあった「+ COMBO BONUS」「次のボーナス」の
            // 2 行は実在しない加点の表示 (近似値 / 仮の式) なので廃止した。
            // 契約 §12: Combo Status はこの座標 (FHD 1512,460) を使う。
            var status = V5Ui.Node(column, "Combo Status");
            var statusRt = V6Layout.RectOf(status);
            statusRt.anchorMin = new Vector2(1f, 1f);
            statusRt.anchorMax = new Vector2(1f, 1f);
            statusRt.pivot = new Vector2(1f, 1f);
            statusRt.anchoredPosition = Vector2.zero;
            statusRt.sizeDelta = new Vector2(m.RightColW, m.StatusBlockH);
            comboStatusBlock = status;

            var track = V5Ui.Panel(status, "Track",
                new Vector2(m.StatusTrackW, m.StatusTrackH), V5Tokens.Key0, 4f);
            var trackRt = V6Layout.RectOf(track);
            trackRt.anchorMin = new Vector2(0f, 1f);
            trackRt.anchorMax = new Vector2(0f, 1f);
            trackRt.pivot = new Vector2(0f, 1f);
            trackRt.anchoredPosition = new Vector2(0f, -m.StatusTrackY);

            var fill = V5Ui.Panel(track, "Fill",
                new Vector2(0f, m.StatusTrackH), V5Tokens.Text, 4f);
            var fillRt = V6Layout.RectOf(fill);
            fillRt.anchorMin = new Vector2(0f, 0.5f);
            fillRt.anchorMax = new Vector2(0f, 0.5f);
            fillRt.pivot = new Vector2(0f, 0.5f);
            fillRt.anchoredPosition = Vector2.zero;
            comboFill = fillRt;
            comboFillImage = fill.GetComponent<Image>();

            comboStatusRow = V5Ui.Node(status, "Status Row");
            var rowRt = V6Layout.RectOf(comboStatusRow);
            rowRt.anchorMin = new Vector2(0f, 1f);
            rowRt.anchorMax = new Vector2(0f, 1f);
            rowRt.pivot = new Vector2(0f, 1f);
            rowRt.anchoredPosition = new Vector2(0f, -m.StatusRowY);
            rowRt.sizeDelta = new Vector2(m.StatusTrackW, m.StatusRowH);

            var rowLayout = comboStatusRow.AddComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = 10f * Scale;
            rowLayout.childAlignment = TextAnchor.UpperLeft;
            rowLayout.childControlWidth = false;
            rowLayout.childControlHeight = false;
            rowLayout.childForceExpandWidth = false;
            rowLayout.childForceExpandHeight = false;

            var noMiss = V5Ui.Text(comboStatusRow, "No Miss", "NO MISS", m.StatusFs,
                V5Tokens.AccentLight);
            var noMissLe = noMiss.gameObject.AddComponent<LayoutElement>();
            noMissLe.minWidth = m.FcChipW * 0.6f;
            noMissLe.preferredHeight = m.StatusRowH;

            // 契約 §8: FC チップ 150×40 #3A3220 r4 (fs22 $warn-light)。
            var chip = V5Ui.Panel(comboStatusRow, "Full Combo",
                new Vector2(m.FcChipW, m.FcChipH), V6HudLogic.AccentBase, 4f);
            fullComboChip = chip;
            var chipLe = chip.AddComponent<LayoutElement>();
            chipLe.minWidth = m.FcChipW;
            chipLe.preferredHeight = m.FcChipH;
            V5Ui.Text(chip, "Text", "FULL COMBO", m.StatusFs, V5Tokens.WarnLight,
                TextAlignmentOptions.Center);
        }

        /// <summary>
        /// コンボボーナス塗りの発光を戻す (Issue #434)。
        /// 変化の瞬間に WarnLight へ発光させ、短時間で通常色へ戻す。
        /// </summary>
        private void UpdateComboBonusPulse()
        {
            if (comboFillImage == null)
            {
                return;
            }

            if (comboBonusPulseStart < 0f)
            {
                return;
            }

            float elapsed = Time.unscaledTime - comboBonusPulseStart;
            if (elapsed >= V6HudLogic.ComboBonusPulseSeconds)
            {
                comboBonusPulseStart = -1f;
                comboFillImage.color = V5Tokens.Text;
                return;
            }

            float t = V6HudLogic.EaseOutCubic(elapsed / V6HudLogic.ComboBonusPulseSeconds);
            comboFillImage.color = Color.Lerp(V5Tokens.WarnLight, V5Tokens.Text, t);
        }

        /// <summary>
        /// 桁別ストリップのポップを進める (Issue #445)。
        /// 文字が変わったスロットだけが拡縮 + 発光し、終わったスロットは
        /// 以後一切書き込まない (静止した桁を dirty にしない)。
        /// </summary>
        private void UpdateValueStrips()
        {
            UpdateStripAnim(scoreStrip, Time.unscaledTime);
            UpdateStripAnim(comboStrip, Time.unscaledTime);
        }

        private void UpdateStripAnim(ValueStrip strip, float now)
        {
            if (strip == null)
            {
                return;
            }

            for (int i = 0; i < strip.Slots.Count; i++)
            {
                ValueStrip.Slot slot = strip.Slots[i];
                if (slot.AnimStart < 0f || !slot.Go.activeSelf)
                {
                    continue;
                }

                float elapsed = now - slot.AnimStart;
                if (elapsed >= V6HudLogic.DigitPopTotal)
                {
                    slot.AnimStart = -1f;
                    slot.Rt.localScale = Vector3.one;
                    slot.Label.color = strip.BaseColor;
                    continue;
                }

                float scale = V6HudLogic.PunchScale(elapsed,
                    V6HudLogic.DigitPopPeak, V6HudLogic.DigitPopAttack, V6HudLogic.DigitPopTotal);
                slot.Rt.localScale = new Vector3(scale, scale, 1f);
                float t = V6HudLogic.EaseOutCubic(
                    Mathf.Clamp01(elapsed / V6HudLogic.DigitPopTotal));
                slot.Label.color = Color.Lerp(strip.FlashColor, strip.BaseColor, t);
            }
        }

        /// <summary>
        /// 桁別値ストリップを作る (Issue #445)。コンテナは左上アンカーで
        /// <paramref name="pos"/> に置き、中に 1 文字 = 1 スロットを必要数だけ作る
        /// (スロットは値の長さに合わせて遅延生成)。スロット幅はフォントの
        /// ASCII 送り幅 (0.62em) で固定し、桁の位置が値の変化でずれないようにする。
        /// </summary>
        private ValueStrip CreateValueStrip(GameObject parent, string name, Vector2 pos, Vector2 size,
            float fontSize, Color baseColor, Color flashColor)
        {
            var container = V5Ui.Node(parent, name);
            var rt = container.GetComponent<RectTransform>();
            PlaceTopLeft(rt, pos.x, pos.y);
            rt.sizeDelta = size;

            return new ValueStrip
            {
                Container = rt,
                SlotWidth = fontSize * V6Ui.AsciiAdvanceEm,
                FontSize = fontSize,
                BaseColor = baseColor,
                FlashColor = flashColor,
            };
        }

        /// <summary>
        /// 桁別表示ストリップ (Issue #445)。1 文字 = 1 スロット (固定幅・中央揃え)
        /// で、値が変わったとき <b>文字が変わった桁だけ</b>をポップ + 発光させる。
        /// 全体をまとめて拡縮・発光する旧方式は「安っぽい」との指摘で廃止した。
        /// </summary>
        private sealed class ValueStrip
        {
            /// <summary>1 桁スロット。</summary>
            public sealed class Slot
            {
                public GameObject Go;
                public RectTransform Rt;
                public TextMeshProUGUI Label;
                /// <summary>最後にスロットへ書いた文字 (同じ文字なら TMP を触らない)。</summary>
                public char LastChar = '\0';
                /// <summary>ポップ開始時刻 (-1 = 静止中。以後は一切書き込まない)。</summary>
                public float AnimStart = -1f;
            }

            public RectTransform Container;
            public readonly List<Slot> Slots = new List<Slot>(16);
            /// <summary>1 スロットの幅 (px)。</summary>
            public float SlotWidth;
            public float FontSize;
            public Color BaseColor;
            /// <summary>ポップ中に発光させる色 (終わると BaseColor へ戻る)。</summary>
            public Color FlashColor;
            /// <summary>
            /// 最後に表示した文字列 (差分の基準)。初期値は実文字列と一致しない番兵
            /// なので、最初の SetText では全桁が「新しい文字」としてポップする。
            /// </summary>
            public string Text = "\u0001";

            /// <summary>
            /// 表示文字列を差し替える。文字が変わった桁だけポップを開始し、
            /// はみ出たスロットは隠す。文字列が同じなら何もしない (issue #381 と同じ)。
            /// </summary>
            public void SetText(string value, float now)
            {
                value = value ?? string.Empty;
                if (value == Text)
                {
                    return;
                }

                bool[] changed = V6HudLogic.ChangedCharFlags(Text, value);
                Text = value;
                EnsureSlots(value.Length);

                for (int i = 0; i < Slots.Count; i++)
                {
                    Slot slot = Slots[i];
                    bool used = i < value.Length;
                    if (slot.Go.activeSelf != used)
                    {
                        slot.Go.SetActive(used);
                    }

                    if (!used)
                    {
                        slot.AnimStart = -1f;
                        slot.LastChar = '\0';
                        continue;
                    }

                    // 文字が変わった桁だけ TMP を更新する (変わらない桁は
                    // text 代入も dirty にしない)。
                    char c = value[i];
                    if (slot.LastChar != c)
                    {
                        slot.Label.text = c.ToString();
                        slot.LastChar = c;
                    }

                    if (i < changed.Length && changed[i])
                    {
                        slot.AnimStart = now;
                    }
                }
            }

            private void EnsureSlots(int count)
            {
                while (Slots.Count < count)
                {
                    Slots.Add(CreateSlot(Slots.Count));
                }
            }

            private Slot CreateSlot(int index)
            {
                var go = V5Ui.Node(Container.gameObject, Container.name + " Digit " + index);
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                float h = Container.rect.height;
                rt.anchoredPosition = new Vector2((index + 0.5f) * SlotWidth, -h * 0.5f);
                rt.sizeDelta = new Vector2(SlotWidth, h);

                var label = V5Ui.Text(go, "Char", "", FontSize, BaseColor,
                    TextAlignmentOptions.Center, FontStyles.Bold);
                label.textWrappingMode = TextWrappingModes.NoWrap;
                V6Layout.StretchFill(label.rectTransform);

                // 使うまで非表示 (空スロットの TMP を毎フレーム走らせない)。
                go.SetActive(false);
                return new Slot { Go = go, Rt = rt, Label = label };
            }
        }
    }
}
