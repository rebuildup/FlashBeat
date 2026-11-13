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

        [SetUp]
        public void SetUp()
        {
            originalScore = GManager.score;
            originalCombo = GManager.combo;

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

            TextMeshProUGUI score = view.GetComponentsInChildren<TextMeshProUGUI>(true)
                .FirstOrDefault(t => t.name == "Score Value");
            Assert.IsNotNull(score, "スコアの値 Text が見つからない");
            Assert.AreEqual(
                GameHudLogic.FormatScore(GManager.score), score.text,
                "Refresh が早期 return してスコアの表示が更新されていない");

            // コンボの値 Text も更新されている (=": 更新経路が最後まで通った")。
            TextMeshProUGUI combo = comboTextOf(view);
            Assert.IsNotNull(combo, "コンボの値 Text が見つからない");
            Assert.AreEqual("7", combo.text);
        }

        private static TextMeshProUGUI comboTextOf(V6GameHudView target)
        {
            var field = typeof(V6GameHudView).GetField("comboText",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            return (TextMeshProUGUI)field.GetValue(target);
        }

        /// <summary>
        /// 「+ COMBO BONUS」「次のボーナス」の block は構築しない (issue-162 B-4 再照合)。
        /// 表示値 (BonusTotal = combo*50 の近似 / NextBonusReward = milestone*10 の仮式)
        /// は JudgeScoreboard の配点に存在しない加点で、実スコアと異なる偽値だった。
        /// </summary>
        [Test]
        public void FakeComboBonusBlock_IsNotBuilt()
        {
            GManager.combo = 7;
            view.Refresh();

            Transform bonus = view.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(t => t.name == "Combo Bonus");
            Assert.IsNull(bonus, "実在しないコンボボーナスの表示が戻っています (issue-162 B-4)");
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
        /// Issue #433: 譜面に歌詞データが無いときはパネル自体を隠す。
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

                view.Refresh();

                Assert.IsFalse(LyricBoxOf(view).activeSelf, "歌詞の無い譜面では枠を出さない");
            }
            finally
            {
                if (sourceGo != null) Object.DestroyImmediate(sourceGo);
                GManager.songID = originalSongId;
                GManager.played = originalPlayed;
                GManager.step_time = originalStepTime;
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
