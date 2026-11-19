using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using FlashBeat.Gameplay;
using FlashBeat.UI.V5;
using FlashBeat.UI.V6.Game.Hud;
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
        private bool savedFlash;
        private int savedFlashBg;
        private int savedFlashT;

        [SetUp]
        public void SetUp()
        {
            canvasGo = new GameObject("V6OptionPreviewExperienceTests Canvas",
                typeof(RectTransform), typeof(Canvas));
            savedSongId = GManager.songID;
            savedLyricsEnabled = PlaySession.LyricsEnabled;
            savedFlash = GManager.Flash;
            savedFlashBg = GManager.FlashBG;
            savedFlashT = GManager.FlashT;
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
            GManager.Flash = savedFlash;
            GManager.FlashBG = savedFlashBg;
            GManager.FlashT = savedFlashT;

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

        /// <summary>
        /// 指定レーンの最初のタップノーツについて、rig と同じチャート
        /// (<see cref="V6OptionPreviewChart.BuildForCurrentSong"/>: 実チャート
        /// 優先・無ければ疑似チャート) から判定時刻を返す (Issue #677)。
        /// 戻り値は rig の timedStart と同じ NoteTimeWithTiming 適用後の時刻。
        /// </summary>
        private static float FirstTapTimeInLane(int lane, out int timing, out float loopSeconds)
        {
            timing = GManager.noteTiming;
            var chart = V6OptionPreviewChart.BuildForCurrentSong(out loopSeconds);
            float best = float.MaxValue;
            foreach (var note in chart)
            {
                if (note.Lane == lane && note.HoldSeconds <= 0f)
                {
                    float timed = NotesManager.NoteTimeWithTiming(note.Time, timing);
                    // テストは mediaTime >= 0 で駆動するので、窓に収まる
                    // 最初の出現 (loop 0) だけを対象にする。
                    if (timed < loopSeconds && timed < best)
                    {
                        best = timed;
                    }
                }
            }
            if (best == float.MaxValue || best < 0.5f)
            {
                Assert.Fail("lane " + lane + " にテスト可能なタップノーツが無い");
            }
            return best;
        }

        private static float FirstTapTimeInLane(int lane)
        {
            return FirstTapTimeInLane(lane, out _, out _);
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
            // Issue #732: オートプレイの押下もレーンを点けるので、
            // 実キー注入だけを見るこのテストでは切る。
            view.PreviewAutoPlayForAudit = false;

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
            // Issue #732: オートプレイが同じキューを消費するので、
            // 実キー注入の判定だけを見るこのテストでは切る。
            view.PreviewAutoPlayForAudit = false;
            int lane = 2;
            float timed = FirstTapTimeInLane(lane);

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
            // Issue #732: オートプレイ OFF なら「押さない」= 全て MISS の
            // 従来契約を検証できる (ON の網羅性は別テスト)。
            view.PreviewAutoPlayForAudit = false;
            int lane = 4;
            float timed = FirstTapTimeInLane(lane);

            // ノーツをキューへ積んでから、押さずに BAD 窓を越えさせる。
            view.TickPreviewForAudit(timed - 0.5f, 0.016f);
            view.TickPreviewForAudit(timed + LongNote.BAD_WINDOW + 0.1f, 0.016f);

            Assert.AreEqual("MISS", view.PreviewVerdictForAudit(lane),
                "時刻切れノーツが MISS として出ていない");
        }

        // --- 拍フラッシュ (BGFlash 相当、Issue #677) ---

        /// <summary>
        /// 拍フラッシュが選択曲の BPM と FlashBG 倍率で点灯すること。
        /// 本編 BGFlash.Start/Update と同じ規則 (60/BPM × FlashBG 倍率、
        /// α=0.3、lightSpeed=3)。Flash Off では点灯しない。
        /// </summary>
        [Test]
        public void BeatFlash_FollowsGameBpmAndFlashOptions()
        {
            SongData song = PlaySessionSnapshot.ResolveSong(GManager.songID);
            if (song == null || song.bpm <= 0f)
            {
                Assert.Ignore("選択曲の BPM が解決できない環境");
            }

            GManager.Flash = true;
            GManager.FlashBG = 8; // 倍率 ×1 → 拍間隔 = 60/BPM
            GManager.FlashT = 0;

            var view = CreateView();
            float beat = 60f / song.bpm;

            // 拍境界の直前まで進めてから越えさせる → α=0.3 へ点灯。
            view.TickPreviewForAudit(beat - 0.01f, 0.016f);
            Assert.LessOrEqual(view.PreviewBeatFlashAlphaForAudit, 0.0001f,
                "拍前にフラッシュが点いている");
            view.TickPreviewForAudit(beat + 0.005f, 0.016f);
            Assert.AreEqual(0.3f, view.PreviewBeatFlashAlphaForAudit, 0.001f,
                "拍で BGFlash 相当の α=0.3 が点灯しない");

            // Flash Off では点灯しない。
            GManager.Flash = false;
            view.TickPreviewForAudit(beat * 2f + 0.005f, 0.016f);
            Assert.LessOrEqual(view.PreviewBeatFlashAlphaForAudit, 0.0001f,
                "Flash Off でも拍フラッシュが点いている");
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
            // Issue #677: 状態分離の対象を PlaySession / PlaySessionSnapshot /
            // ScorePersistence へも広げる (読み取り専用のはず)。
            bool lyricsEnabled = PlaySession.LyricsEnabled;
            var sessionSnapshot = PlaySessionSnapshot.Current;
            var scoreStore = FlashBeat.Persistence.ScorePersistence.Current;

            // 押下判定・MISS・レーンフラッシュ・歌詞を一通り起こす。
            int lane = 6;
            float timed = FirstTapTimeInLane(lane);
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
            Assert.AreEqual(lyricsEnabled, PlaySession.LyricsEnabled,
                "PlaySession.LyricsEnabled が変わった");
            Assert.AreSame(sessionSnapshot, PlaySessionSnapshot.Current,
                "PlaySessionSnapshot.Current が差し替わった");
            Assert.AreSame(scoreStore, FlashBeat.Persistence.ScorePersistence.Current,
                "ScorePersistence.Current が差し替わった");
        }

        // --- 本編 HUD 写像 (Issue #677) ---

        /// <summary>
        /// 判定ポップのフォントサイズ・箱サイズが本編 V6HudVerdict と同じ
        /// m.VerdictFs / VerdictW / VerdictH を well/Screen 高比の
        /// uniform scale で縮めた値であること (Issue #706: 軸別スケールの
        /// 横歪みを廃止。位置は従来どおり画面フラグメント写像)。
        /// </summary>
        [Test]
        public void Verdict_UsesTheGameHudMetrics()
        {
            var view = CreateView();
            view.TickPreviewForAudit(1.5f, 0.016f); // レイアウトを適用させる

            var metrics = V6HudMetrics.ForResolution(Screen.width, Screen.height);
            var overlay = view.PreviewVerdictRectForAudit(0);
            Assert.IsNotNull(overlay, "判定ポップの RectTransform が無い");
            var wellRt = (RectTransform)overlay.parent;
            Vector2 wellSize = wellRt.rect.size;
            float s = wellSize.y / Screen.height;

            var tmp = view.PreviewVerdictTextForAudit(0);
            Assert.AreEqual(metrics.VerdictFs * s, tmp.fontSize, 0.01f,
                "判定ポップのフォントサイズが本編 m.VerdictFs の写像でない");
            Assert.AreEqual(metrics.VerdictW * s, overlay.sizeDelta.x, 0.05f,
                "判定ポップ幅が本編 m.VerdictW の写像でない");
            Assert.AreEqual(metrics.VerdictH * s, overlay.sizeDelta.y, 0.05f,
                "判定ポップ高さが本編 m.VerdictH の写像でない");
        }

        /// <summary>
        /// 歌詞ボックスが本編 V6HudLyrics と同じスロット
        /// (LyricSlotOrigin / LyricSlotSize / LyricFs) の写像であること。
        /// 高さは Issue #674 の共有スロット高 (LyricSlotHeight)、寸法は
        /// Issue #706 の uniform scale (well/Screen 高比)。固定 px の
        /// 近似値を使わない (Issue #677)。
        /// </summary>
        [Test]
        public void LyricBox_UsesTheGameHudSlotMapping()
        {
            var view = CreateView();
            view.TickPreviewForAudit(1.5f, 0.016f);

            var boxRt = view.PreviewLyricBoxRectForAudit;
            Assert.IsNotNull(boxRt, "歌詞ボックスが無い");
            var wellRt = (RectTransform)view.Root.Find("Main/Preview/Game Preview/Preview Overlay");
            Assert.IsNotNull(wellRt);
            Vector2 wellSize = wellRt.rect.size;
            float sx = wellSize.x / Screen.width;
            float sy = wellSize.y / Screen.height;
            float s = wellSize.y / Screen.height; // 寸法の uniform scale

            var metrics = V6HudMetrics.ForResolution(Screen.width, Screen.height);
            Vector2 slotOrigin = V6HudLogic.LyricSlotOrigin(Screen.width, Screen.height);
            Vector2 slotSize = V6HudLogic.LyricSlotSize(metrics.LyricFs,
                Screen.width, Screen.height);

            Assert.AreEqual(slotSize.x * s, boxRt.sizeDelta.x, 0.05f,
                "歌詞ボックス幅が本編スロット幅の写像でない");
            Assert.AreEqual(slotSize.y * s, boxRt.sizeDelta.y, 0.05f,
                "歌詞ボックス高さが本編 LyricSlotHeight の写像でない");
            Assert.AreEqual(
                (slotOrigin.x + slotSize.x * 0.5f) * sx - wellSize.x * 0.5f,
                boxRt.anchoredPosition.x, 0.05f,
                "歌詞ボックスの x が本編スロット中心の写像でない");
            Assert.AreEqual(-slotOrigin.y * sy, boxRt.anchoredPosition.y, 0.05f,
                "歌詞ボックスの y が本編スロット上端の写像でない");
            Assert.AreEqual(metrics.LyricFs * s, view.PreviewLyricTextForAudit.fontSize, 0.01f,
                "歌詞フォントサイズが本編 m.LyricFs の写像でない");
        }
    }
}
