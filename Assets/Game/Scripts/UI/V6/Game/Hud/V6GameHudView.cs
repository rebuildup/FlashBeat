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
    /// v6 Game HUD (Issue #220, design/flashbeat-ui.pen の Game · HUD フレーム)。
    ///
    /// - すべてコードから uGUI を生成する (v5 GameHudView と同じ方針)。
    /// - 表示値はすべて実データ: GManager (score/combo/perfect/great/bad/miss/step_time)、
    ///   KeyBindings (キーガイド, Changed 購読で即時更新)、
    ///   PlaySessionSnapshot (曲の長さ = 進捗バー)、GameSceneManager.InputJson (歌詞)、
    ///   Judge がレーンに Instantiate する判定クローン (レーン別判定行)。
    /// - 判定クローンは v5 と同じく読み取った後に非表示にする (3D プレイフィールドには
    ///   判定ポップアップを出さず、画面最下部の判定行に一本化)。
    /// - 「準備中 / 失敗」の表示は既存 HudPlayStatus (v5) の責務。二重に出さない。
    /// - 寸法は <see cref="V6HudMetrics"/> の解像度プロファイル (契約 §8)。
    ///   design px を実ピクセルのまま使い、等比拡大 (引き伸ばし) はしない
    ///   (Owner 指示 2026-11-08)。ルートは 1:1 でスクリーン左上に置く。
    ///
    /// MusicManager / Judge / NotesManager は所有外。変更しない。
    ///
    /// Issue #385 Phase 2.2: 領域ごとに partial class へ分割した
    /// (スコア/コンボ = V6HudScore.cs、歌詞/カウントダウン/終了 = V6HudLyrics.cs、
    /// 判定行/レーン格子 = V6HudVerdict.cs、枠/シェード/進捗/メニュー/キーガイド =
    /// V6HudChrome.cs)。このファイルはライフサイクル・Refresh 駆動・共通ヘルパを持つ。
    /// </summary>
    public sealed partial class V6GameHudView : MonoBehaviour
    {
        public RectTransform Root { get; private set; }

        /// <summary>メニューボタン (統合側が PauseMenuView 等に接続できるように公開)。</summary>
        public Button MenuButton { get; private set; }

        private V6HudMetrics m;

        // --- 毎フレームの状態 ---
        /// <summary>このビューが Refresh を実行した総回数。</summary>
        private int refreshCount;
        /// <summary>直近に Refresh を実行したフレーム番号。</summary>
        private int lastRefreshFrame = -1;
        /// <summary>同じフレームで 2 回以上 Refresh されたフレーム数 (0 が正常)。</summary>
        private int duplicateRefreshFrames;

        /// <summary>
        /// Canvas 配下に自分自身を作る。親は Canvas の RectTransform
        /// (Constant Pixel Size x1 前提 = v5 CanvasConfigurator 方式)。
        /// </summary>
        public static V6GameHudView Create(RectTransform parent)
        {
            var go = V5Ui.Node(parent.gameObject, "V6GameHud");
            var view = go.AddComponent<V6GameHudView>();
            view.Build();
            return view;
        }

        /// <summary>
        /// 更新の所有者はこのビュー自身 (<c>Update</c>)。統合側はこれを呼ぶだけでよく、
        /// <see cref="Refresh"/> を直接呼ばない (1 フレーム 2 回の更新を作らない)。
        ///
        /// 同じフレームで複数回呼ばれても <see cref="Refresh"/> は 1 回だけ走る
        /// (このフレームで既に更新済みなら何もしない)。統合側が毎フレーム呼んでも
        /// 二重更新にならないので、呼び出し順に依存しない。
        /// </summary>
        public void RequestRefresh()
        {
            if (lastRefreshFrame == Time.frameCount)
            {
                return;
            }

            Refresh();
        }

        // --- Issue #393: 更新回数の観測点 (テストと計測ハーネス用) ---

        /// <summary>このビューが Refresh を実行した総回数。</summary>
        public int RefreshCountForAudit
        {
            get { return refreshCount; }
        }

        /// <summary>同じフレームで 2 回以上 Refresh されたフレーム数 (0 が正常)。</summary>
        public int DuplicateRefreshFramesForAudit
        {
            get { return duplicateRefreshFrames; }
        }

        /// <summary>
        /// 検証ハーネス用: スコア値の表示文字列 (桁スロットの連結)。
        /// コンボ 0 のとき <see cref="ComboTextForAudit"/> は空文字。
        /// </summary>
        public string ScoreTextForAudit
        {
            get { return scoreStrip != null ? scoreStrip.Text : null; }
        }

        /// <summary>検証ハーネス用: コンボ値の表示文字列 (0 なら空)。</summary>
        public string ComboTextForAudit
        {
            get { return comboStrip != null ? comboStrip.Text : null; }
        }

        // --- Issue #379: HUD 受入ハーネス用 ---

        /// <summary>上部シェード (design D-379: 一様な帯ではなく縦グラデーション)。</summary>
        public Image TopShadeForAudit
        {
            get { return topShadeImage; }
        }

        /// <summary>上部シェードの design 高さ (m.TopShadeH)。</summary>
        public int TopShadeHeightForAudit
        {
            get { return m != null ? m.TopShadeH : -1; }
        }

        /// <summary>歌詞スロットの RectTransform (design i30vM4 の位置検証用)。</summary>
        public RectTransform LyricBoxForAudit
        {
            get { return lyricBox != null ? lyricBox.GetComponent<RectTransform>() : null; }
        }

        /// <summary>歌詞本文に実際に適用されているフォントサイズ。</summary>
        public float LyricFontSizeForAudit
        {
            get { return lyricText != null ? lyricText.fontSize : -1f; }
        }

        /// <summary>HUD canvas の実ピクセル寸法。</summary>
        public Vector2 CanvasSizeForAudit
        {
            get { return CanvasSize(); }
        }

        /// <summary>スコア表示の使用中桁数 (カンマを含む)。</summary>
        public int ScoreActiveDigitsForAudit
        {
            get { return scoreStrip == null ? -1 : scoreStrip.ActiveCount(); }
        }

        /// <summary>スコアが 1 行に収まっているか (使用中の全スロットが同じ y)。</summary>
        public bool ScoreSingleLineForAudit
        {
            get { return scoreStrip != null && scoreStrip.IsSingleLine(); }
        }

        /// <summary>スコアの桁ポップが再生中か。</summary>
        public bool ScorePopActiveForAudit
        {
            get { return scoreStrip != null && scoreStrip.AnyAnimating(Time.unscaledTime); }
        }

        /// <summary>判定行テキストに適用されているフォントサイズ。</summary>
        public float VerdictFontSizeForAudit
        {
            get
            {
                return verdictTexts.Count > 0 && verdictTexts[0] != null
                    ? verdictTexts[0].fontSize : -1f;
            }
        }

        /// <summary>判定行が design のフォントサイズ (m.VerdictFs) を使っているか。</summary>
        public bool VerdictUsesDesignFontSizeForAudit
        {
            get
            {
                return m != null && verdictTexts.Count > 0 && verdictTexts[0] != null
                    && Mathf.Approximately(verdictTexts[0].fontSize, m.VerdictFs);
            }
        }

        /// <summary>判定行 lane の表示文字列 (非表示なら空)。</summary>
        public string VerdictTextForAudit(int lane)
        {
            return lane >= 0 && lane < verdictTexts.Count && verdictTexts[lane] != null
                ? verdictTexts[lane].text : null;
        }

        /// <summary>表示/非表示。非表示中は Refresh も何もしない。</summary>
        public void SetVisible(bool visible)        {
            if (Root != null)
            {
                Root.gameObject.SetActive(visible);
            }
        }

        /// <summary>
        /// 実データを UI へ反映する。冪等で、呼ぶ側は 1 フレーム 1 回に限る。
        ///
        /// Issue #393: 以前は自身の <c>Update</c> と <c>V6GameRuntime.Update</c> の
        /// 両方が呼び、同じフレームで 2 回走っていた。更新の所有者はこのビュー自身に
        /// 一本化したので、統合側は <see cref="RequestRefresh"/> を使う。
        /// </summary>
        public void Refresh()
        {
            if (Root == null || !Root.gameObject.activeInHierarchy)
            {
                return;
            }

            refreshCount++;
            if (lastRefreshFrame == Time.frameCount)
            {
                duplicateRefreshFrames++;
            }

            lastRefreshFrame = Time.frameCount;

            int score = GManager.score;
            // TextMeshPro の文字列設定は値が変わるまで呼ばない (issue #381:
            // 毎フレームの書式化は GC を呼び、それがカクつきになる)。
            // Issue #445: 値は桁別ストリップへ。文字が変わった桁だけがポップする。
            if (score != lastRenderedScore)
            {
                gradeText.text = GameHudLogic.RankText(score);
                scoreStrip.SetText(GameHudLogic.FormatScore(score), Time.unscaledTime);
                lastRenderedScore = score;
            }

            // スコアゲージ (塗りは + セグメントの手前まで, Best は保存スコア)
            // Issue #434: 値を瞬時に切り替えず、表示側を目標へ指数 tween させる。
            // 初回 (Rebuild 直後) は目標値へ即合わせて前回プレイの残像を出さない。
            float gaugeTargetW = V6HudLogic.GaugeFillWidth(score, m.GaugeFillMax);
            int hi = GManager.songID >= 0 && GManager.songID < GManager.Hiscore.Length
                ? GManager.Hiscore[GManager.songID]
                : GManager.BestScoreOf(GManager.songID); // 固定テーブル外は安定キー (issue-162 A-6)
            float gaugeTargetX = V6HudLogic.GaugeBestX(hi, m.GaugeFillMax);
            if (!gaugeShownInit)
            {
                gaugeShownWidth = gaugeTargetW;
                gaugeBestShownX = gaugeTargetX;
                gaugeShownInit = true;
            }
            else
            {
                float dt = Time.unscaledDeltaTime;
                gaugeShownWidth = V6HudLogic.Approach(gaugeShownWidth, gaugeTargetW, dt, 10f);
                gaugeBestShownX = V6HudLogic.Approach(gaugeBestShownX, gaugeTargetX, dt, 10f);
            }

            // Issue #402: 変化しないフレームで sizeDelta / anchoredPosition を
            // 書き戻さない (uGUI を不必要に dirty にしない)。
            V6LayoutWrites.SizeDeltaIfChanged(gaugeFill, gaugeShownWidth, m.GaugeFillH);
            V6LayoutWrites.AnchoredPositionIfChanged(gaugeBest, gaugeBestShownX, 0f);

            // コンボ (表示は値が変わったときだけ更新する)
            // Issue #445: 0 はコンボではないので値自体を隠す (ComboValueText が
            // 空文字を返す → スロットが全て非表示になる)。復帰時は全桁が
            // 「新しい文字」としてポップする。
            if (GManager.combo != lastRenderedCombo)
            {
                comboStrip.SetText(V6HudLogic.ComboValueText(GManager.combo), Time.unscaledTime);
                lastRenderedCombo = GManager.combo;
            }

            int judged = GManager.perfect + GManager.great + GManager.bad + GManager.miss;
            bool showStatus = V6HudLogic.ShowComboStatus(GManager.miss, judged);
            // 契約 §12: この座標 (FHD 1512,460) は Combo Status が使う
            // (旧 Combo Bonus block は廃止 — issue-162 B-4)。
            // Issue #402: 表示状態が変わらないフレームで SetActive を呼ばない。
            V6LayoutWrites.ActiveIfChanged(comboStatusBlock, showStatus);

            // FULL COMBO は曲完走 (全ノーツ判定済み) かつノーミスのときだけ出す。
            // 演奏中ずっと出しっぱなしにすると内容を反映しない表示になる (Issue #233)。
            var currentSong = GManager.GetSong(GManager.songID);
            bool songComplete = GManager.played && currentSong != null && currentSong.totalHits > 0
                && judged >= currentSong.totalHits;
            if (fullComboChip != null && fullComboChip.activeSelf != (songComplete && GManager.miss == 0))
            {
                fullComboChip.SetActive(songComplete && GManager.miss == 0);
            }

            int milestone = GameHudLogic.NextBonusMilestone(GManager.combo);
            // Issue #434: 塗りが伸びた瞬間に色をワンショットで発光させ、
            // 「いま増えた」が分かるようにする (色は Update で戻す)。
            float comboFillW = V6HudLogic.ComboFillFraction(GManager.combo, milestone) * m.StatusTrackW;
            if (!Mathf.Approximately(comboFillW, lastComboFillWidth))
            {
                lastComboFillWidth = comboFillW;
                comboBonusPulseStart = Time.unscaledTime;
            }

            // Issue #402: コンボが変わらないフレームでは塗り幅を書き戻さない。
            V6LayoutWrites.SizeDeltaIfChanged(comboFill, comboFillW, m.StatusTrackH);

            // issue-162 B-4 再照合 (2026-11-11): 正本 Uc9v6 には Gx3kq "Combo Bonus" が
            // BIKrJ "Combo Status" と同じ (1512,460) に重ねて置かれている (design 側の
            // 状態の重複)。Gx3kq の「+1,200」「次のボーナス 300 / +3,000」は
            // JudgeScoreboard にコンボボーナス加点が無いため実在しない値で、
            // BonusTotal / NextBonusReward も近似式にすぎない。加点そのものは
            // 新しいゲーム仕様 (今回のスコープ外) なので、数値を捏造せず
            // 同じ座標を Combo Status (NO MISS / FULL COMBO) が使う。

            // 進捗バー + 歌詞 (メディアタイム = GManager.step_time)
            float duration = CurrentSongDuration();
            // Issue #402: 曲が進んでいない (ポーズ中・待機中) フレームでは
            // 進捗バーを書き戻さない。時間が進めば値が変わるので更新される。
            V6LayoutWrites.SizeDeltaIfChanged(progressFill,
                V6HudLogic.ProgressFillWidth(GManager.step_time, duration, progressTrack.rect.width),
                progressFill.rect.height);

            UpdateLyrics();
            UpdateLaneVerdicts();
            UpdateLaneGrids();
        }

        private void Update()
        {
            ApplyRootTransform();

            // 解像度変更 (プロファイル境界をまたぐ) で HUD を作り直す。
            // 寸法は design px を実ピクセルで使うので、アンカー追従だけでは対応できない。
            Vector2 canvasSize = CanvasSize();
            if (m == null || !m.MatchesResolution(canvasSize.x, canvasSize.y))
            {
                Rebuild();
            }

            // データ反映の唯一の入口 (Issue #393)。統合側が先に RequestRefresh を
            // 呼んでいても、このフレームでは二重に走らない。
            RequestRefresh();
            UpdateValueStrips();
            UpdateComboBonusPulse();
        }

        /// <summary>
        /// 描画面 (Canvas) の実ピクセル寸法。v6 の Canvas は
        /// CanvasConfigurator (Constant Pixel Size x1) の ScreenSpaceOverlay なので
        /// Canvas の rect サイズ = 実スクリーン解像度。
        /// </summary>
        private Vector2 CanvasSize()
        {
            if (cachedCanvas == null)
            {
                cachedCanvas = GetComponentInParent<Canvas>();
            }

            var rt = cachedCanvas != null ? cachedCanvas.transform as RectTransform : null;
            if (rt != null && rt.rect.width > 0f && rt.rect.height > 0f)
            {
                return rt.rect.size;
            }

            return new Vector2(Screen.width, Screen.height);
        }

        private Canvas cachedCanvas;

        /// <summary>
        /// HUD ルートを実スクリーン解像度の <b>1:1</b> (等倍) で置く。
        ///
        /// 親フレーム (V6GameRuntime.hudFrame) は Canvas (= 実スクリーン解像度) 全体を
        /// 覆い、scale は常に 1 (引き伸ばしなし / Owner 指示 2026-11-08)。
        /// かつて親の等比拡大を 1/parentScale で打ち消す逆倍率を毎フレーム掛けていたが、
        /// 親が等倍になったので廃止。ルートは左上アンカーで親フレームいっぱいに置き、
        /// 子は design px の絶対座標 (左上原点) と端アンカーの両方で置ける。
        /// 解像度変更で親が変わるので毎フレーム同期する (変化があればのみ dirty)。
        /// </summary>
        private void ApplyRootTransform()
        {
            var parent = transform.parent as RectTransform;
            if (parent == null)
            {
                return;
            }

            var size = parent.rect.size;
            if (size.x <= 0f || size.y <= 0f)
            {
                return;
            }

            var rt = V6Layout.RectOf(gameObject);
            if (rt.anchorMin == new Vector2(0f, 1f) && rt.anchorMax == new Vector2(0f, 1f)
                && rt.pivot == new Vector2(0f, 1f) && rt.anchoredPosition == Vector2.zero
                && rt.sizeDelta == size && rt.localScale == Vector3.one)
            {
                return;
            }

            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = size;
            rt.localScale = Vector3.one;
        }

        /// <summary>
        /// 解像度プロファイルが変わったとき、HUD を作り直す。
        /// 子は全てコード生成なので消して <see cref="Build"/> し直すだけでよい。
        /// V5GameUi.DestroyNow は Play モードではフレーム末破棄なので、旧子が
        /// 1 フレームだけ残る (解像度変更の瞬間のみの二重描画で、実害はない)。
        /// </summary>
        private void Rebuild()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                V5GameUi.DestroyNow(transform.GetChild(i).gameObject);
            }

            keyCaps.Clear();
            verdictTexts.Clear();
            seenJudgeIds.Clear();
            // Issue #402: レーン格子のメモを捨てる (子を作り直したので、
            // 前回の座標を「適用済み」と思い込むと位置が更新されない)。
            lastLaneGridCanvas = new Vector2(float.NaN, float.NaN);
            lastLaneGridScale = float.NaN;
            lastLaneGridCamera = null;
            lastLaneGridPlayfield = false;
            lastJudged = -1;
            songIdCached = int.MinValue;
            lyricData = null;
            lyricLines = null;
            lyricIndex = -2;
            cachedCanvas = null;
            progressTrack = null;
            progressFill = null;
            gaugeFill = null;
            gaugeBest = null;
            comboFill = null;
            comboStatusBlock = null;
            comboStatusRow = null;
            fullComboChip = null;
            keyGuideRow = null;
            gradeText = null;
            scoreStrip = null;
            comboStrip = null;
            lyricText = null;
            lyricBox = null;
            lyricPrompt = null;
            MenuButton = null;
            comboFillImage = null;
            // Issue #434: tween / パルスの状態もリセット (別曲・別解像度で
            // 前回の途中経過が残らないように)。
            gaugeShownInit = false;
            gaugeShownWidth = -1f;
            gaugeBestShownX = 0f;
            lastComboFillWidth = -1f;
            comboBonusPulseStart = -1f;
            verdictBaseY = 0f;
            verdictStartDrop = V6HudLogic.VerdictRestGapAboveLine;
            // 新しい TextMeshPro は空文字から始まる。前の部品に出していた値を
            // 「表示済み」のまま残すと、値が変わらない限り Refresh が書き込まず
            // スコア / コンボが次に増えるまで空のままになる。
            lastRenderedScore = int.MinValue;
            lastRenderedCombo = int.MinValue;

            Build();
        }

        private void OnEnable()
        {
            KeyBindings.Changed += RebuildKeyGuide;
        }

        private void OnDestroy()
        {
            KeyBindings.Changed -= RebuildKeyGuide;
        }

        // --- 構築 ---

        private void Build()
        {
            // 契約 §8: HUD は解像度ごとの固有フレーム (1920×1080 / 2560×1440 / 2560×1664)。
            // design px を実ピクセルのまま使い、等比拡大 (引き伸ばし) はしない
            // (Owner 指示 2026-11-08)。
            Vector2 canvasSize = CanvasSize();
            m = V6HudMetrics.ForResolution(canvasSize.x, canvasSize.y);
            // Root は自分自身。未代入だと Refresh のガードが常に早期 return し、
            // スコア / コンボ / 歌詞 / 判定行 / レーン格子の更新が一切走らない。
            Root = V6Layout.RectOf(gameObject);
            ApplyRootTransform();

            BuildDim();
            BuildTopShade();
            BuildProgressBar();
            BuildLyric();
            BuildCountdown();
            BuildGrade();
            BuildScoreGauge();
            BuildScoreTexts();
            BuildMenuButton();
            BuildComboBlock();
            BuildRightColumn();
            BuildVerdictRow();
            BuildKeyGuide();
            BuildEndOverlay();
        }

        private float CurrentSongDuration()
        {
            if (songIdCached != GManager.songID)
            {
                songIdCached = GManager.songID;
                var song = PlaySessionSnapshot.ResolveSong(GManager.songID);
                songDurationCached = song != null ? song.duration : 0f;
            }

            return songDurationCached;
        }

        private int songIdCached = int.MinValue;
        private float songDurationCached;

        private static void PlaceTopLeft(RectTransform rt, float x, float y)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, y);
        }

        /// <summary>
        /// TMP の実測送り幅 (design px)。design の自動幅 KeyCap (padding [0,10]) を
        /// 再現するために、推定 em ではなく実フォントで測る。
        /// フォント未設定などで測れないときは em 推定に落ちる。
        /// </summary>
        private static float MeasureTextWidth(TMP_Text text, string content, float fontSize)
        {
            float fallback = V6Ui.LabelAdvanceEm(content) * fontSize;
            if (text == null)
            {
                return Mathf.Ceil(fallback);
            }

            text.text = content;
            text.fontSize = fontSize;
            text.ForceMeshUpdate();
            float width = text.GetPreferredValues(content).x;
            return Mathf.Ceil(width > 0f ? width : fallback);
        }

        private float Scale
        {
            get { return m == null ? 1f : m.Scale; }
        }
    }
}
