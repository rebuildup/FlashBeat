using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using FlashBeat.Gameplay;
using FlashBeat.UI.V5;
using FlashBeat.UI.V6.Option;

namespace FlashBeat.V6CanvasTests
{
    /// <summary>
    /// Option 画面 Game Preview のゲーム体験拡張 (Issue #607) の検証。
    ///
    /// - well の中に歌詞行と 8 レーン分の判定ポップ用テキストがあること。
    /// - レーンキー押下でレーンフラッシュの α が点灯・減衰すること
    ///   (本編 LaneFlash / V6PlayfieldView と同じ 0.3 / 3 per second)。
    /// - 疑似チャートのノーツが JudgeQueue の判定窓で判定され、
    ///   判定ポップ (PERFECT/GREAT/BAD/MISS) が出ること。
    /// - 歌詞が選択中の曲の歌詞データと同期して出ること。
    /// - プレビュー操作が GManager のスコア・セッション状態を
    ///   一切変更しないこと (状態分離)。
    /// </summary>
    public sealed class V6OptionPreviewExperienceTests
    {
        private GameObject canvasGo;
        private int savedSongId;
        private bool savedLyricsEnabled;

        [SetUp]
        public void SetUp()
        {
            canvasGo = new GameObject("V6OptionPreviewExperienceTests Canvas",
                typeof(RectTransform), typeof(Canvas));
            savedSongId = GManager.songID;
            savedLyricsEnabled = PlaySession.LyricsEnabled;
            GManager.songID = 1; // ロストアンブレラ (歌詞データあり)
            PlaySession.SetLyricsEnabled(true);
        }

        [TearDown]
        public void TearDown()
        {
            KeyBindings.ClearInjectedLaneKeyDowns();
            KeyBindings.ClearInjectedKeyHolds();
            PlaySession.SetLyricsEnabled(savedLyricsEnabled);
            GManager.songID = savedSongId;

            var view = V6OptionView.Current;
            if (view != null)
            {
                view.Dispose();
            }

            if (canvasGo != null)
            {
                Object.DestroyImmediate(canvasGo);
            }
            canvasGo = null;
        }

        private V6OptionView CreateView()
        {
            return V6OptionView.Create(canvasGo.GetComponent<RectTransform>(), () => { });
        }

        /// <summary>指定レーンの最初のタップノーツについて、判定窓内のメディア時刻を返す。</summary>
        private static float FirstTapTimeInLane(int lane, out int timing)
        {
            timing = GManager.noteTiming;
            foreach (var note in V6OptionPreviewChart.Build())
            {
                if (note.Lane == lane && note.HoldSeconds <= 0f)
                {
                    return NotesManager.NoteTimeWithTiming(note.Time, timing);
                }
            }
            Assert.Fail("lane " + lane + " にタップノーツが無い");
            return 0f;
        }

        // --- オーバーレイ構造 ---

        [Test]
        public void GamePreview_HasLyricBox_AndVerdictTextsPerLane()
        {
            var view = CreateView();

            var overlay = (RectTransform)view.Root.Find("Main/Preview/Game Preview/Preview Overlay");
            Assert.IsNotNull(overlay, "Game Preview well に Preview Overlay が無い");

            Assert.IsNotNull(overlay.Find("Preview Lyric Box"), "歌詞ボックスが無い");
            for (int lane = 0; lane < Judge.LaneCount; lane++)
            {
                Assert.IsNotNull(overlay.Find("Verdict " + lane),
                    "判定ポップ用テキスト Verdict " + lane + " が無い");
            }
        }

        // --- レーンフラッシュ ---

        [Test]
        public void LaneFlash_LightsOnLaneKey_AndDecaysLikeTheGame()
        {
            var view = CreateView();

            // 押下フレーム: 本編と同じ α = 0.3。
            KeyBindings.InjectLaneKeyDown(3);
            view.TickPreviewForAudit(5f, 0.016f);
            Assert.AreEqual(0.3f, view.PreviewLaneFlashAlphaForAudit(3), 0.0001f,
                "レーンキー押下でレーンが点灯しない");
            Assert.AreEqual(0f, view.PreviewLaneFlashAlphaForAudit(0), 0.0001f,
                "押していないレーンまで点灯している");

            // 離すと 3/秒で減衰 (LaneFlash.lightSpeed = 3 と同じ)。
            KeyBindings.ClearInjectedLaneKeyDowns();
            view.TickPreviewForAudit(5.016f, 0.05f);
            Assert.AreEqual(0.15f, view.PreviewLaneFlashAlphaForAudit(3), 0.02f,
                "減衰速度が本編 (3/秒) と違う");
        }

        // --- 判定ポップ ---

        [Test]
        public void Judgement_KeyPressWithinWindow_ShowsGreatVerdict()
        {
            var view = CreateView();
            int lane = 2;
            float timed = FirstTapTimeInLane(lane, out _);

            // まず判定窓の手前まで進めてキューへ積ませる。
            view.TickPreviewForAudit(timed - 0.5f, 0.016f);
            Assert.AreEqual(0, view.PreviewJudgementCountForAudit, "まだ判定は無い");

            // 判定時刻の 0.12 秒前に押す → lag 0.12 は GREAT 窓 (<= 0.15)。
            KeyBindings.InjectLaneKeyDown(lane);
            view.TickPreviewForAudit(timed - 0.12f, 0.016f);

            Assert.AreEqual("GREAT", view.PreviewVerdictForAudit(lane),
                "押下判定が GREAT として出ていない");
            Assert.GreaterOrEqual(view.PreviewJudgementCountForAudit, 1);
        }

        [Test]
        public void Judgement_UnpressedNote_ShowsMissVerdict()
        {
            var view = CreateView();
            int lane = 4;
            float timed = FirstTapTimeInLane(lane, out _);

            // ノーツをキューへ積んでから、押さずに BAD 窓を越えさせる。
            view.TickPreviewForAudit(timed - 0.5f, 0.016f);
            view.TickPreviewForAudit(timed + LongNote.BAD_WINDOW + 0.1f, 0.016f);

            Assert.AreEqual("MISS", view.PreviewVerdictForAudit(lane),
                "時刻切れノーツが MISS として出ていない");
        }

        // --- 歌詞同期 ---

        [Test]
        public void Lyrics_CurrentSongLine_FollowsThePreviewClock()
        {
            // ロストアンブレラの先頭行: startTime 0.946 / endTime 2.3 / 「僕を連れてって」。
            Assert.AreEqual("僕を連れてって", V6OptionPreviewLyrics.CurrentLine(1.5f),
                "選択中の曲の歌詞がメディア時刻と同期していない");
            Assert.IsNull(V6OptionPreviewLyrics.CurrentLine(30f),
                "歌詞区間の外で行が出ている");

            PlaySession.SetLyricsEnabled(false);
            Assert.IsNull(V6OptionPreviewLyrics.CurrentLine(1.5f),
                "歌詞 OFF でも行が出ている");
            PlaySession.SetLyricsEnabled(true);
        }

        [Test]
        public void Lyrics_PreviewShowsTheCurrentLine()
        {
            var view = CreateView();
            view.TickPreviewForAudit(1.5f, 0.016f);
            Assert.AreEqual("僕を連れてって", view.PreviewLyricForAudit,
                "プレビューに歌詞が出ていない");
        }

        // --- 状態分離 ---

        [Test]
        public void Preview_DoesNotTouchGameSessionState()
        {
            var view = CreateView();

            // 判定・スコア系の観測点を保存。
            int score = GManager.score;
            int combo = GManager.combo;
            int maxCombo = GManager.maxCombo;
            int perfect = GManager.perfect;
            int great = GManager.great;
            int bad = GManager.bad;
            int miss = GManager.miss;
            float ratio = GManager.ratioScore;
            int settled = GManager.settledNoteCount;
            int nextNotes = GManager.nextNotes;
            float stepTime = GManager.step_time;
            bool played = GManager.played;
            bool start = GManager.Start;

            // 押下判定・MISS・レーンフラッシュ・歌詞を一通り起こす。
            int lane = 6;
            float timed = FirstTapTimeInLane(lane, out _);
            view.TickPreviewForAudit(timed - 0.5f, 0.016f);
            KeyBindings.InjectLaneKeyDown(lane);
            view.TickPreviewForAudit(timed - 0.12f, 0.016f);
            view.TickPreviewForAudit(timed + 1f, 0.016f);
            view.TickPreviewForAudit(1.5f, 0.016f); // 歌詞区間

            Assert.Greater(view.PreviewJudgementCountForAudit, 0,
                "前提: プレビュー内で判定が起きていない");

            Assert.AreEqual(score, GManager.score, "score が変わった");
            Assert.AreEqual(combo, GManager.combo, "combo が変わった");
            Assert.AreEqual(maxCombo, GManager.maxCombo, "maxCombo が変わった");
            Assert.AreEqual(perfect, GManager.perfect, "perfect が変わった");
            Assert.AreEqual(great, GManager.great, "great が変わった");
            Assert.AreEqual(bad, GManager.bad, "bad が変わった");
            Assert.AreEqual(miss, GManager.miss, "miss が変わった");
            Assert.AreEqual(ratio, GManager.ratioScore, 0.0001f, "ratioScore が変わった");
            Assert.AreEqual(settled, GManager.settledNoteCount, "settledNoteCount が変わった");
            Assert.AreEqual(nextNotes, GManager.nextNotes, "nextNotes が変わった");
            Assert.AreEqual(stepTime, GManager.step_time, 0.0001f, "step_time が変わった");
            Assert.AreEqual(played, GManager.played, "played が変わった");
            Assert.AreEqual(start, GManager.Start, "Start が変わった");
        }
    }
}
