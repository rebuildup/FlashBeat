using System.Linq;
using FlashBeat.UI.V5;
using FlashBeat.UI.V6.Game.Hud;
using NUnit.Framework;
using TMPro;
using UnityEngine;

namespace FlashBeat.Tests
{
    /// <summary>
    /// v6 ゲーム HUD の実構築を検証する (Issue #233)。
    ///
    /// HUD はコードから uGUI を生成するため、
    /// 「生成した要素の参照が 1 つ欠けていた」型の不具合が静かに残る。
    /// 実際に作って <see cref="V6GameHudView.Refresh"/> を 1 回呼んで、
    /// 例外が出ないこと (更新経路が最後まで通ること) を確認する。
    /// </summary>
    public class V6GameHudViewTests
    {
        private GameObject canvasGo;
        private V6GameHudView view;
        private int originalScore;
        private int originalCombo;
        private int originalMiss;
        private int originalComboMilestones;
        private int originalComboBonus;
        private int originalMilestoneBonus;
        private int originalChartNoteCount;
        private int originalSettledNoteCount;
        private float originalMaxBonusRatio;
        private bool originalLyricsEnabled;

        [SetUp]
        public void SetUp()
        {
            originalScore = GManager.score;
            originalCombo = GManager.combo;
            originalMiss = GManager.miss;
            originalComboMilestones = GManager.comboMilestones;
            originalComboBonus = GManager.comboBonus;
            originalMilestoneBonus = GManager.milestoneBonus;
            originalChartNoteCount = GManager.chartNoteCount;
            originalSettledNoteCount = GManager.settledNoteCount;
            originalMaxBonusRatio = GManager.maxBonusRatio;
            originalLyricsEnabled = PlaySession.LyricsEnabled;

            canvasGo = new GameObject("V6GameHudViewTests Canvas", typeof(Canvas));
            var canvasRect = (RectTransform)canvasGo.transform;
            // ScreenSpaceOverlay の Canvas は実機では画面サイズに追従するため
            // sizeDelta は書き換わらない (EditMode では保持される)。
            // このテストはレイアウトの寸法に依存しない (要素の構造と参照だけを確かめる)。
            canvasRect.sizeDelta = new Vector2(1920f, 1080f);
            view = V6GameHudView.Create(canvasRect);
        }

        [TearDown]
        public void TearDown()
        {
            GManager.score = originalScore;
            GManager.combo = originalCombo;
            GManager.miss = originalMiss;
            GManager.comboMilestones = originalComboMilestones;
            GManager.comboBonus = originalComboBonus;
            GManager.milestoneBonus = originalMilestoneBonus;
            GManager.chartNoteCount = originalChartNoteCount;
            GManager.settledNoteCount = originalSettledNoteCount;
            GManager.maxBonusRatio = originalMaxBonusRatio;
            PlaySession.SetLyricsEnabled(originalLyricsEnabled);

            if (canvasGo != null)
            {
                Object.DestroyImmediate(canvasGo);
            }

            view = null;
            canvasGo = null;
        }

        /// <summary>
        /// HUD を更新しても例外が出ない (更新経路が最後まで通る)。
        /// 以前はコンボボーナスの「値」側の Text への参照が欠けており、
        /// 毎フレーム NullReferenceException で更新が途切れていた。
        ///
        /// Refresh() は非表示なら何もしないため、例外が出ないだけでは
        /// 「更新経路が通った」証明にならない。ここでは score を既知の値に
        /// して更新し、表示が実際に書き換わったこと (= 早期 return していない
        /// こと) を併せて確認する。
        /// </summary>
        [Test]
        public void Refresh_UpdatesWithoutThrowing()
        {
            GManager.score = 123456;
            GManager.combo = 7;

            Assert.DoesNotThrow(() => view.Refresh(), "Refresh が例外を投げた");

            // Issue #445: 値は桁別ストリップ (1 文字 = 1 スロット) で表示する。
            // 監査用に連結した表示文字列を検証する。
            Assert.AreEqual(
                GameHudLogic.FormatScore(GManager.score), view.ScoreTextForAudit,
                "Refresh が早期 return してスコアの表示が更新されていない");

            // コンボの値も更新されている (= 更新経路が最後まで通った)。
            Assert.AreEqual("7", view.ComboTextForAudit);
        }

        /// <summary>
        /// Issue #445: コンボ 0 はコンボではないので値の表示自体を隠す
        /// ("0" を出さない)。1 以上で再び表示される。
        /// </summary>
        [Test]
        public void Combo_Zero_HidesTheValue()
        {
            GManager.combo = 0;
            view.Refresh();
            Assert.AreEqual("", view.ComboTextForAudit, "コンボ 0 では値を表示しない");

            GManager.combo = 3;
            view.Refresh();
            Assert.AreEqual("3", view.ComboTextForAudit, "コンボ 1 以上で値が戻る");

            GManager.combo = 0;
            view.Refresh();
            Assert.AreEqual("", view.ComboTextForAudit, "ミスで 0 に戻ると再び隠れる");
        }

        /// <summary>
        /// Issue #788: スコアゲージの目盛り (Seg *) はグレード閾値の正本
        /// (<see cref="FlashBeat.UI.V6.Common.V6GradeLogic.GradeBandsOf"/>)
        /// から導かれ、塗りと同じ ScoreFractionOf 写像に乗る。
        /// 1920×1080 canvas → FHD metrics (GaugeW=600) で、実際の
        /// RectTransform 配置が帯境界と一致することを検証する。
        /// </summary>
        [Test]
        public void ScoreGauge_Segments_FollowGradeBands()
        {
            var track = view.transform.Find("Score Gauge/Track");
            Assert.IsNotNull(track, "Score Gauge/Track が無い");

            // v4+ 帯: C[0,.5) B[.5,.7) A[.7,.95) S[.95,1) SS=満点の点。
            // GaugeW=600 への写像で x = 0/300/420/570。
            AssertSegment(track, "Seg C", 0f, 300f);
            AssertSegment(track, "Seg B", 300f, 120f);
            AssertSegment(track, "Seg A", 420f, 150f);
            AssertSegment(track, "Seg S", 570f, 30f);

            // SS は幅を持たない点帯 → トラック右端に接するキャップ。
            var ss = track.Find("Seg SS") as RectTransform;
            Assert.IsNotNull(ss, "Seg SS (満点点帯のキャップ) が無い");
            Assert.AreEqual(600f - ss.sizeDelta.x, ss.anchoredPosition.x, 0.01f,
                "SS キャップの右端がトラック右端に一致しない");
        }

        /// <summary>
        /// Issue #788 の再現ケース: 1,227,055 点 (B 評価、61.35%) の
        /// 塗り終端が B 帯 [50%,70%) の内側にあり、A 帯へはみ出さない
        /// ことを実際の Fill の sizeDelta で検証する。
        /// </summary>
        [Test]
        public void ScoreGauge_BGradeScore_FillStaysInsideBBand()
        {
            GManager.score = 1227055;
            view.Refresh();

            var fill = view.transform.Find("Score Gauge/Track/Fill") as RectTransform;
            Assert.IsNotNull(fill, "Score Gauge/Track/Fill が無い");

            // 初回 Refresh はゲージ tween を目標値へ即合わせる。
            float frac = fill.sizeDelta.x / 600f;
            Assert.GreaterOrEqual(frac, 0.5f, "B 評価の塗りが B 帯の左端より左");
            Assert.Less(frac, 0.7f, "B 評価の塗りが A 帯へはみ出している");
        }

        private static void AssertSegment(Transform track, string name, float x, float w)
        {
            var rt = track.Find(name) as RectTransform;
            Assert.IsNotNull(rt, name + " が無い");
            Assert.AreEqual(x, rt.anchoredPosition.x, 0.5f, name + " の x が帯境界と一致しない");
            Assert.AreEqual(w, rt.sizeDelta.x, 0.5f, name + " の幅が帯境界と一致しない");
        }

        /// <summary>
        /// Issue #445: スコア値は 1 文字 = 1 スロットの桁別ストリップ。
        /// 変わらない桁のスロットは文字もアニメーション状態も変えない。
        /// </summary>
        [Test]
        public void ScoreValue_UsesPerDigitSlots()
        {
            GManager.score = 123456;
            view.Refresh();

            Transform strip = view.transform.Find("Score Value");
            Assert.IsNotNull(strip, "桁別ストリップ 'Score Value' が無い");
            // "123,456" は 7 スロット (',' を含む)。
            int activeSlots = strip.Cast<Transform>()
                .Count(t => t.gameObject.activeSelf);
            Assert.AreEqual(7, activeSlots, "表示中の桁スロット数が表示文字数と一致しない");

            // 値を変えてもスロット総数は使い回す (増えた分だけ追加生成)。
            GManager.score = 123457;
            view.Refresh();
            Assert.AreEqual("123,457", view.ScoreTextForAudit);
            activeSlots = strip.Cast<Transform>().Count(t => t.gameObject.activeSelf);
            Assert.AreEqual(7, activeSlots);
        }

        /// <summary>
        /// Issue #673: miss > 0 で Combo Status ブロックが非表示になっても、
        /// 節目ボーナスの獲得ポップアップは右コラムに表示され続ける。
        /// milestoneBonusText の親が comboStatusBlock ではないことも固定する。
        /// </summary>
        [Test]
        public void MilestoneBonus_StaysVisibleAfterAMiss()
        {
            GManager.miss = 1; // ShowComboStatus は miss>0 で false → ブロック非表示
            GManager.comboMilestones = 1; // ビュー初期値 (0) からの増分で表示を起こす

            view.Refresh();

            var status = view.ComboStatusBlockForAudit;
            Assert.IsNotNull(status, "Combo Status ブロックが無い");
            Assert.IsFalse(status.activeSelf, "miss > 0 では Combo Status は非表示");

            var bonus = view.MilestoneBonusForAudit;
            Assert.IsNotNull(bonus, "節目ボーナス表示が無い");
            Assert.IsTrue(bonus.activeInHierarchy,
                "miss > 0 でも節目ボーナスは表示される (Issue #673)");
            Assert.AreNotSame(status.transform, bonus.transform.parent,
                "節目ボーナスの親は Combo Status ブロックではない");
            Assert.AreEqual("Right Column", bonus.transform.parent.name,
                "節目ボーナスは常時アクティブな Right Column の子");
        }

        /// <summary>
        /// Issue #784 / ADR-0035: コンボボーナスは右コラムに実スコア点で
        /// 常設表示され、miss > 0 で Combo Status が隠れても残る。v5 では
        /// 連続加点が無く、獲得済みは節目到達の累計
        /// GManager.milestoneBonus (comboMilestones * 250 ratio points)
        /// を最新バージョンの ComponentScore で換算した実値。
        /// </summary>
        [Test]
        public void ComboBonus_ShowsConvertedScorePoints_AndSurvivesAMiss()
        {
            // 節目 2 回到達 → milestoneBonus = 500 ratio points。
            // MaxBonusRatioV5(100) = 1100 → ComponentScoreV5(500,1100) = 454,545。
            GManager.milestoneBonus = 500;
            GManager.maxBonusRatio = FlashBeat.Gameplay.Scoring.MaxBonusRatioV5(100);
            GManager.miss = 1;   // NO MISS は消えるが、獲得済みボーナスは残る
            GManager.combo = 0;  // Miss でコンボは 0 に戻る

            view.Refresh();

            var status = view.ComboStatusBlockForAudit;
            Assert.IsNotNull(status, "Combo Status ブロックが無い");
            Assert.IsFalse(status.activeSelf, "miss > 0 では Combo Status は非表示");

            var bonus = view.ComboBonusBlockForAudit;
            Assert.IsNotNull(bonus, "Combo Bonus ブロックが無い");
            Assert.IsTrue(bonus.activeInHierarchy,
                "miss > 0 でもコンボボーナスは表示される (Issue #784)");
            Assert.AreEqual("Right Column", bonus.transform.parent.name,
                "コンボボーナスは常時アクティブな Right Column の子");
            Assert.IsNotNull(bonus.transform.Find("Next Bonus Track"),
                "次の節目までのゲージ (Next Bonus Track) が無い");

            int expected = V6HudLogic.ComboBonusScorePoints(
                V6HudLogic.EarnedComboBonusRatioPoints(), GManager.maxBonusRatio);
            Assert.AreEqual(
                "+" + expected.ToString("N0", System.Globalization.CultureInfo.InvariantCulture),
                view.ComboBonusValueForAudit,
                "表示値は ComponentScoreV5 で換算した実スコア点");
        }

        /// <summary>
        /// Issue #784 / ADR-0035: ボーナス未獲得 (節目未到達) の間は
        /// ブロックごと隠し、1 回でも節目に達すれば表示する (miss 後も
        /// 獲得済みは残る)。
        /// </summary>
        [Test]
        public void ComboBonus_HiddenUntilBonusIsEarned()
        {
            GManager.milestoneBonus = 0;
            view.Refresh();
            Assert.IsFalse(view.ComboBonusBlockForAudit.activeSelf,
                "節目未到達ではブロックを出さない");

            GManager.milestoneBonus = 250; // 節目 1 回分 (ADR-0035)
            view.Refresh();
            Assert.IsTrue(view.ComboBonusBlockForAudit.activeSelf,
                "節目到達でブロックが出る");
        }

        /// <summary>
        /// ADR-0035: 到達前のゲージは次の節目までの進捗を示す契約。
        /// v5 では獲得済みが 0 でも、残りノーツで次の節目へ届く間は
        /// ブロックを出し「NEXT +N」を表示する。終盤で届かなくなれば
        /// 獲得予定は消え、獲得済みも無ければブロックごと隠れる。
        /// </summary>
        [Test]
        public void ComboBonus_ShowsNextMilestoneProgress_BeforeFirstCrossing()
        {
            GManager.milestoneBonus = 0;
            GManager.chartNoteCount = 139;
            GManager.settledNoteCount = 10;
            GManager.combo = 10;
            GManager.maxBonusRatio = FlashBeat.Gameplay.Scoring.MaxBonusRatioV5(139);
            view.Refresh();

            var bonus = view.ComboBonusBlockForAudit;
            Assert.IsTrue(bonus.activeSelf,
                "節目未到達でも次の節目へ届く間は進捗ブロックを出す");
            StringAssert.StartsWith("NEXT +",
                view.NextBonusTextForAudit ?? string.Empty,
                "次の節目の獲得予定点が表示される");

            // 終盤: 残りノーツでは次の節目へ届かない → 獲得予定は消える。
            GManager.settledNoteCount = 135; // 残り 4 < 必要 40 (combo10 → 必要40)
            view.Refresh();
            Assert.IsFalse(bonus.activeSelf,
                "到達不能かつ未獲得ではブロックを隠す");
            Assert.AreEqual("", view.NextBonusTextForAudit ?? string.Empty,
                "到達不能では獲得予定を出さない");
        }

        // --- 歌詞 (issue #59) ------------------------------------------------

        private int originalSongId;
        private bool originalPlayed;
        private float originalStepTime;

        /// <summary>3 行の歌詞フィクスチャ。0-2s / 3-5s / 6-8s で間奏を挟む。</summary>
        private static TextData ThreeLineLyrics()
        {
            return new TextData
            {
                StartTime = new float[] { 0f, 3f, 6f },
                EndTime = new float[] { 2f, 5f, 8f },
                Mondai = new string[] { "一", "二", "三" },
                Furigana = new string[] { "いち", "に", "さん" },
                romaji = new string[] { "ichi", "ni", "san" },
            };
        }

        /// <summary>歌詞の供給源。UpdateLyrics は FindFirstObjectByType で探す。</summary>
        private GameObject CreateLyricsSource(TextData lyrics)
        {
            var go = new GameObject("V6GameHudViewTests LyricsSource", typeof(GameSceneManager));
            var field = typeof(GameSceneManager).GetField("inputJson",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(field, "GameSceneManager.inputJson が見つからない");
            field.SetValue(go.GetComponent<GameSceneManager>(), lyrics);
            return go;
        }

        private static TextMeshProUGUI LyricTextOf(V6GameHudView target)
        {
            var field = typeof(V6GameHudView).GetField("lyricText",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(field, "V6GameHudView.lyricText が見つからない");
            return (TextMeshProUGUI)field.GetValue(target);
        }

        private static GameObject LyricBoxOf(V6GameHudView target)
        {
            var field = typeof(V6GameHudView).GetField("lyricBox",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(field, "V6GameHudView.lyricBox が見つからない");
            return (GameObject)field.GetValue(target);
        }

        private static GameObject HudFieldOf(V6GameHudView target, string name)
        {
            var field = typeof(V6GameHudView).GetField(name,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"V6GameHudView.{name} が見つからない");
            return (GameObject)field.GetValue(target);
        }

        /// <summary>
        /// 共有クロック (GManager.step_time = メディア時刻) が指す行だけが出る。
        /// 行の切れ目は歌詞データ側で管理されており、HUD は今の行を出すだけ。
        /// </summary>
        [Test]
        public void Lyrics_ShowTheLineThatIsOnAir()
        {
            originalSongId = GManager.songID;
            originalPlayed = GManager.played;
            originalStepTime = GManager.step_time;
            GameObject sourceGo = null;
            try
            {
                sourceGo = CreateLyricsSource(ThreeLineLyrics());
                GManager.played = true;

                GManager.step_time = 1f;
                view.Refresh();
                Assert.AreEqual("一", LyricTextOf(view).text, "1 行目の区間で 1 行目が出る");
                // Issue #433: 振り仮名・ローマ字は表示しない (フィールド自体を持たない)。

                GManager.step_time = 4f;
                view.Refresh();
                Assert.AreEqual("二", LyricTextOf(view).text, "2 行目の区間で 2 行目に切り替わる");

                GManager.step_time = 7f;
                view.Refresh();
                Assert.AreEqual("三", LyricTextOf(view).text, "3 行目の区間で 3 行目に切り替わる");
            }
            finally
            {
                if (sourceGo != null) Object.DestroyImmediate(sourceGo);
                GManager.songID = originalSongId;
                GManager.played = originalPlayed;
                GManager.step_time = originalStepTime;
            }
        }

        /// <summary>
        /// Issue #433: パネルは行間で消さない。譜面に歌詞データがある間は
        /// 枠を出し続け、本文が無い区間は文字だけ空にする。
        /// </summary>
        [Test]
        public void Lyrics_KeepTheBoxWhileTheChartHasLyrics()
        {
            originalSongId = GManager.songID;
            originalPlayed = GManager.played;
            originalStepTime = GManager.step_time;
            GameObject sourceGo = null;
            try
            {
                sourceGo = CreateLyricsSource(ThreeLineLyrics());
                GManager.played = true;

                GManager.step_time = 2.5f;
                view.Refresh();
                Assert.AreEqual("", LyricTextOf(view).text, "行の間は本文が出ない");
                Assert.IsTrue(LyricBoxOf(view).activeSelf, "行の間でも枠は出す (Issue #433)");

                GManager.step_time = 9f;
                view.Refresh();
                Assert.AreEqual("", LyricTextOf(view).text, "最終行の後は本文が出ない");
                Assert.IsTrue(LyricBoxOf(view).activeSelf, "最終行の後も枠は出す");

                // 再び区間に入れば本文が出る。
                GManager.step_time = 4f;
                view.Refresh();
                Assert.AreEqual("二", LyricTextOf(view).text);
                Assert.IsTrue(LyricBoxOf(view).activeSelf);
            }
            finally
            {
                if (sourceGo != null) Object.DestroyImmediate(sourceGo);
                GManager.songID = originalSongId;
                GManager.played = originalPlayed;
                GManager.step_time = originalStepTime;
            }
        }

        /// <summary>
        /// Issue #734 (v0.2.1 実機レビューで再修正): 枠を出さないのは
        /// 「譜面に歌詞データが無い」場合だけ。歌詞データが無い譜面では
        /// 設定 ON でも曲中は枠ごと隠し、本文は空のままにする。
        /// 旧仕様 (歌詞データが無くても枠を出しっぱなし) へ戻ると
        /// このテストは落ちる。
        /// </summary>
        [Test]
        public void Lyrics_HideTheBoxWhenTheChartHasNoLyrics()
        {
            originalSongId = GManager.songID;
            originalPlayed = GManager.played;
            originalStepTime = GManager.step_time;
            GameObject sourceGo = null;
            try
            {
                sourceGo = CreateLyricsSource(new TextData
                {
                    StartTime = new float[0],
                    EndTime = new float[0],
                    Mondai = new string[0],
                });
                GManager.played = true;

                GManager.step_time = 1f;
                view.Refresh();
                Assert.AreEqual("", LyricTextOf(view).text, "歌詞の無い譜面では本文は空");
                Assert.IsFalse(LyricBoxOf(view).activeSelf,
                    "歌詞の無い譜面では設定 ON でも枠は出さない (Issue #734)");

                // どの時刻でも枠は変わらない (本文が無い区間・終端を含む)。
                GManager.step_time = 60f;
                view.Refresh();
                Assert.AreEqual("", LyricTextOf(view).text);
                Assert.IsFalse(LyricBoxOf(view).activeSelf, "歌詞終端後も枠は出さない");
            }
            finally
            {
                if (sourceGo != null) Object.DestroyImmediate(sourceGo);
                GManager.songID = originalSongId;
                GManager.played = originalPlayed;
                GManager.step_time = originalStepTime;
            }
        }

        /// <summary>
        /// Issue #734: 歌詞データ (GameSceneManager.InputJson) がまだ届いて
        /// いない間も枠は出す。かつてはデータ未到着 = 歌詞なし扱いで枠が消え、
        /// 到着時に出るフリッカーが起きていた。枠の可視性はデータの
        /// 読み込みタイミングに依存しない (本文だけが空になる)。
        /// </summary>
        [Test]
        public void Lyrics_BoxStaysVisibleBeforeLyricDataArrives()
        {
            originalSongId = GManager.songID;
            originalPlayed = GManager.played;
            originalStepTime = GManager.step_time;
            try
            {
                // 歌詞ソースを作らない = InputJson == null のまま Refresh する。
                GManager.played = true;
                GManager.step_time = 1f;

                view.Refresh();

                Assert.IsTrue(LyricBoxOf(view).activeSelf,
                    "歌詞データ未到着でも設定 ON なら枠は出す (Issue #734)");
                Assert.AreEqual("", LyricTextOf(view).text,
                    "データ未到着の間は本文は空");
            }
            finally
            {
                GManager.songID = originalSongId;
                GManager.played = originalPlayed;
                GManager.step_time = originalStepTime;
            }
        }

        /// <summary>
        /// Issue #734: 設定 OFF なら歌詞データの有無・本文の切り替わりに
        /// 関わらず曲中は枠を出さない。行の区間・行間・歌詞終端のどの時刻でも
        /// 同じで、Pause の Display 設定で ON/OFF を切り替えても
        /// 次の Refresh で設定値どおりに追従する (フラップしない)。
        /// </summary>
        [Test]
        public void Lyrics_BoxFollowsOnlyTheSettingDuringPlayback()
        {
            originalSongId = GManager.songID;
            originalPlayed = GManager.played;
            originalStepTime = GManager.step_time;
            GameObject sourceGo = null;
            try
            {
                sourceGo = CreateLyricsSource(ThreeLineLyrics());
                GManager.played = true;

                // ON: 歌詞あり譜面では本文の有無に関わらず枠は出る。
                PlaySession.SetLyricsEnabled(true);
                GManager.step_time = 1f;
                view.Refresh();
                Assert.IsTrue(LyricBoxOf(view).activeSelf);
                Assert.AreEqual("一", LyricTextOf(view).text);

                GManager.step_time = 2.5f; // 行間
                view.Refresh();
                Assert.IsTrue(LyricBoxOf(view).activeSelf, "行間でも枠は出す");
                Assert.AreEqual("", LyricTextOf(view).text);

                // OFF (Pause の Display 設定と同じ経路): どの時刻でも枠は出ない。
                PlaySession.SetLyricsEnabled(false);
                foreach (float t in new[] { 1f, 2.5f, 4f, 9f })
                {
                    GManager.step_time = t;
                    view.Refresh();
                    Assert.IsFalse(LyricBoxOf(view).activeSelf,
                        $"設定 OFF では t={t} でも枠を出さない");
                    Assert.AreEqual("", LyricTextOf(view).text);
                }

                // ON へ戻すと即座に枠が戻る (Pause → Resume の往復でフラップしない)。
                PlaySession.SetLyricsEnabled(true);
                GManager.step_time = 4f;
                view.Refresh();
                Assert.IsTrue(LyricBoxOf(view).activeSelf, "設定を戻せば枠も戻る");
                Assert.AreEqual("二", LyricTextOf(view).text);
            }
            finally
            {
                if (sourceGo != null) Object.DestroyImmediate(sourceGo);
                PlaySession.SetLyricsEnabled(originalLyricsEnabled);
                GManager.songID = originalSongId;
                GManager.played = originalPlayed;
                GManager.step_time = originalStepTime;
            }
        }

        /// <summary>
        /// Issue #734: Space 待ち (再生前) の Start Prompt は歌詞ではなく
        /// 開始操作の案内なので、歌詞設定 OFF でも枠と一緒に出す。
        /// (ポリシー: 設定値が固定するのは演奏中の枠。カウントダウン /
        /// START / 曲終了オーバーレイと同じく、進行状態の表示は設定に従わない。)
        /// </summary>
        [Test]
        public void Lyrics_StartPromptShowsEvenWhenLyricsSettingIsOff()
        {
            originalSongId = GManager.songID;
            originalPlayed = GManager.played;
            originalStepTime = GManager.step_time;
            try
            {
                PlaySession.SetLyricsEnabled(false);
                GManager.played = false;

                view.Refresh();

                Transform box = LyricBoxOf(view).transform;
                Transform prompt = box.Find("Start Prompt");
                Assert.IsNotNull(prompt, "Start Prompt が無い");
                Assert.IsTrue(LyricBoxOf(view).activeSelf,
                    "再生前は歌詞設定 OFF でも枠を出す (Start Prompt の置き場)");
                Assert.IsTrue(prompt.gameObject.activeSelf,
                    "再生前は歌詞設定 OFF でも Start Prompt を出す");
            }
            finally
            {
                PlaySession.SetLyricsEnabled(originalLyricsEnabled);
                GManager.songID = originalSongId;
                GManager.played = originalPlayed;
                GManager.step_time = originalStepTime;
            }
        }

        /// <summary>
        /// Issue #674: 歌詞・準備 (Start Prompt)・カウントダウン・曲終了の
        /// 全状態で歌詞スロットの RectTransform サイズ・位置が同一であること。
        /// 各解像度プロファイル (FHD / 2K / Mac) で実構築して検証する。
        /// </summary>
        [Test]
        public void Lyrics_SlotRect_IsIdenticalAcrossAllStates()
        {
            var cases = new (float w, float h)[]
            {
                (1920f, 1080f),
                (2560f, 1440f),
                (2560f, 1664f),
            };

            foreach (var (w, h) in cases)
            {
                var canvas = new GameObject($"V6GameHudViewTests Canvas {w}x{h}", typeof(Canvas));
                var canvasRect = (RectTransform)canvas.transform;
                canvasRect.sizeDelta = new Vector2(w, h);
                V6GameHudView stateView = null;
                try
                {
                    stateView = V6GameHudView.Create(canvasRect);
                    Assert.AreEqual(w, stateView.CanvasSizeForAudit.x, 1f,
                        $"{w}x{h}: Canvas サイズがプロファイルに反映されていない");
                    Assert.AreEqual(h, stateView.CanvasSizeForAudit.y, 1f);

                    // 歌詞ボックス / 再開カウントダウン / 曲終了オーバーレイの
                    // 3 状態は同じスロット RectTransform を共有する。
                    var lyricRt = HudFieldOf(stateView, "lyricBox").GetComponent<RectTransform>();
                    var countdownRt = HudFieldOf(stateView, "countdownOverlay").GetComponent<RectTransform>();
                    var endRt = HudFieldOf(stateView, "endOverlay").GetComponent<RectTransform>();

                    Assert.AreEqual(lyricRt.sizeDelta, countdownRt.sizeDelta,
                        $"{w}x{h}: カウントダウンのスロットサイズが歌詞と違う");
                    Assert.AreEqual(lyricRt.sizeDelta, endRt.sizeDelta,
                        $"{w}x{h}: 曲終了のスロットサイズが歌詞と違う");
                    Assert.AreEqual(lyricRt.anchoredPosition, countdownRt.anchoredPosition,
                        $"{w}x{h}: カウントダウンのスロット位置が歌詞と違う");
                    Assert.AreEqual(lyricRt.anchoredPosition, endRt.anchoredPosition,
                        $"{w}x{h}: 曲終了のスロット位置が歌詞と違う");
                }
                finally
                {
                    if (stateView != null) Object.DestroyImmediate(stateView.gameObject);
                    Object.DestroyImmediate(canvas);
                }
            }
        }

        /// <summary>
        /// Issue #790 (v0.2.1 実機受入): Start Prompt の KeyCap「Space」と
        /// ラベル「to start」は独立した横並び要素で、矩形が重ならない。
        /// かつては非アクティブ階層での TMP 実測が退化し、cap が文字より
        /// 狭くなって「Space」が cap をはみ出し「to start」と重なって見えた。
        /// ここでは実レイアウト (Canvas.ForceUpdateCanvases) 後の
        /// ワールド矩形で非重なりと cap 最小幅を検証する。
        /// </summary>
        [Test]
        public void StartPrompt_KeyCapAndLabel_DoNotOverlap()
        {
            var box = LyricBoxOf(view);
            var prompt = box.transform.Find("Start Prompt");
            Assert.IsNotNull(prompt, "Start Prompt が無い");

            // 非アクティブなままでは LayoutGroup が走らないので一時的に出す。
            bool boxWasActive = box.activeSelf;
            bool promptWasActive = prompt.gameObject.activeSelf;
            box.SetActive(true);
            prompt.gameObject.SetActive(true);
            try
            {
                Canvas.ForceUpdateCanvases();

                var capRt = (RectTransform)prompt.Find("KeyCap Space");
                var labelRt = (RectTransform)prompt.Find("Label");
                Assert.IsNotNull(capRt, "KeyCap Space が無い");
                Assert.IsNotNull(labelRt, "Label が無い");

                Vector3[] capCorners = new Vector3[4];
                Vector3[] labelCorners = new Vector3[4];
                capRt.GetWorldCorners(capCorners);
                labelRt.GetWorldCorners(labelCorners);

                // cap の右端が label の左端を越えない (重なり・潜り込みなし)。
                Assert.LessOrEqual(capCorners[2].x, labelCorners[0].x + 0.5f,
                    "KeyCap の右端が Label の左端を越えている (重なり)");

                // 高さは共有スロット内で揃っている (cap 高さ = spec.CapHeight)。
                float capH = capCorners[1].y - capCorners[0].y;
                float labelH = labelCorners[1].y - labelCorners[0].y;
                Assert.AreEqual(capH, labelH, 1f,
                    "KeyCap と Label の高さが揃っていない");

                // cap の内容幅 (cap 幅 - padding) が「Space」の文字幅として
                // 退化していないこと。実測が 0 に潰れると capW = padding だけになり、
                // 文字が cap をはみ出して重なって見える (Issue #790 の実機症状)。
                var spec = V6HudLogic.ReadyPrompt(LyricTextOf(view).fontSize);
                float capW = capCorners[2].x - capCorners[0].x;
                float innerW = capW - spec.PaddingX * 2f;
                float emEstimate = FlashBeat.UI.V6.V6Ui.LabelAdvanceEm("Space") * spec.FontSize;
                Assert.Greater(innerW, emEstimate * 0.3f,
                    $"KeyCap の内容幅 {innerW:F1}px が 'Space' の想定幅 {emEstimate:F1}px に対して退化している");
            }
            finally
            {
                prompt.gameObject.SetActive(promptWasActive);
                box.SetActive(boxWasActive);
            }
        }

        /// <summary>再生前 (Space 待ち) は歌詞位置に Ready 案内を出す。</summary>
        [Test]
        public void Lyrics_ShowTheReadyPromptBeforeThePlaybackStarts()
        {
            originalSongId = GManager.songID;
            originalPlayed = GManager.played;
            originalStepTime = GManager.step_time;
            GameObject sourceGo = null;
            try
            {
                sourceGo = CreateLyricsSource(ThreeLineLyrics());
                GManager.played = false;

                view.Refresh();

                // design HcP8N の Start Prompt (W1ppF): 「Space to Start」の一文ではなく、
                // KeyCap「Space」+ ラベル「to start」の行を歌詞枠の中に出す。
                Transform box = LyricBoxOf(view).transform;
                Transform prompt = box.Find("Start Prompt");
                Assert.IsNotNull(prompt, "Start Prompt が無い");
                Assert.IsTrue(prompt.gameObject.activeSelf, "再生前は Start Prompt を出す");
                Assert.AreEqual("to start", prompt.Find("Label").GetComponent<TextMeshProUGUI>().text);
                Assert.AreEqual("Space",
                    prompt.Find("KeyCap Space/Key").GetComponent<TextMeshProUGUI>().text);
                // 歌詞 3 行は隠れている (排他)。
                Assert.AreEqual("", LyricTextOf(view).text);
                Assert.IsFalse(box.Find("Lyrics").gameObject.activeSelf);
                Assert.IsTrue(LyricBoxOf(view).activeSelf);
            }
            finally
            {
                if (sourceGo != null) Object.DestroyImmediate(sourceGo);
                GManager.songID = originalSongId;
                GManager.played = originalPlayed;
                GManager.step_time = originalStepTime;
            }
        }

    }
}
