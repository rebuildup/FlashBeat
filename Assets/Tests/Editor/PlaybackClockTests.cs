using System.IO;
using System.Text.RegularExpressions;
using FlashBeat.Gameplay;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Playback clock contract of a real playthrough (issue #48/#31):
///
///   * the song has to end even when no media could be played, otherwise the
///     game never reaches the result screen and the player is stuck;
///   * the notes have to follow the media clock, so they cannot run ahead of
///     the music while the stream buffers;
///   * a picture that drifts away from the separate sound track has to be
///     corrected.
/// </summary>
public class PlaybackClockTests
{
    [Test]
    public void TheSongEndsWithoutAnyMediaSoTheResultSceneIsReached()
    {
        const float duration = 89f;
        const float playbackStart = 1000f;

        // 87 seconds after the start the song is not over yet ...
        float beforeEnd = MediaTimeline.FallbackMediaTime(playbackStart + 87f, playbackStart);
        Assert.IsFalse(MediaTimeline.HasSongEnded(beforeEnd, duration));

        // ... one second before the duration it is (the lead out of one second),
        // so the game leaves the game scene and the player can retry or go back
        // to the song list.
        float atEnd = MediaTimeline.FallbackMediaTime(playbackStart + duration - 1f, playbackStart);
        Assert.IsTrue(MediaTimeline.HasSongEnded(atEnd, duration));
    }

    [Test]
    public void TheFallbackClockNeverRunsBackwards()
    {
        Assert.AreEqual(0f, MediaTimeline.FallbackMediaTime(10f, 20f));
    }

    [Test]
    public void TheJudgementStaysOnTheChartTimelineWhileTheFallbackClockRuns()
    {
        const float playbackStart = 500f;
        float wallClock = playbackStart + 12f;

        float mediaTime = MediaTimeline.FallbackMediaTime(wallClock, playbackStart);
        float judgeStart = MediaTimeline.JudgeStartTime(wallClock, mediaTime);

        // The note at 12s of the song has to be hit exactly now.
        Assert.AreEqual(0f, MediaTimeline.JudgeLag(wallClock, judgeStart, 12f), 0.0001f);
    }

    [Test]
    public void ANoteReachesTheHitLineExactlyWhenTheMediaReachesItsTime()
    {
        const float noteTime = 4.5f;
        float zScale = MediaTimeline.ZScaleFor(10f);

        Assert.AreEqual(0f, MediaTimeline.NotePositionZ(noteTime, noteTime, zScale), 0.0001f);
        Assert.Greater(MediaTimeline.NotePositionZ(noteTime, noteTime - 1f, zScale), 0f);
        Assert.Less(MediaTimeline.NotePositionZ(noteTime, noteTime + 1f, zScale), 0f);
    }

    [Test]
    public void TheNotesDoNotJumpWhenTheSongStarts()
    {
        const float noteTime = 4.5f;
        float zScale = MediaTimeline.ZScaleFor(10f);

        // The NotesManager creates the note at z = noteTime * zScale.
        float initialZ = noteTime * zScale;
        Assert.AreEqual(initialZ, MediaTimeline.NotePositionZ(noteTime, 0f, zScale), 0.0001f);

        // The note time is recovered from that position again.
        Assert.AreEqual(noteTime, MediaTimeline.NoteTimeFromPositionZ(initialZ, zScale), 0.0001f);
    }

    [Test]
    public void AStalledMediaClockHoldsTheNotesInsteadOfLettingThemRunAhead()
    {
        const float noteTime = 4.5f;
        float zScale = MediaTimeline.ZScaleFor(10f);

        float whileBuffering = MediaTimeline.NotePositionZ(noteTime, 2f, zScale);
        float whileBufferingLater = MediaTimeline.NotePositionZ(noteTime, 2f, zScale);

        Assert.AreEqual(whileBuffering, whileBufferingLater, 0.0001f);
        Assert.Greater(whileBuffering, 0f);
    }

    /// <summary>
    /// Issue #233: z スケールの決め方の契約。
    ///
    /// MediaTimeline の第 3 引数は「chart time 1 秒あたりのワールド z 移動量」
    /// (zScale) であって、素の noteSpeed オプション **ではない**。
    /// 変換は <see cref="MediaTimeline.ZScaleFor"/> が持ち、
    /// 実運用の 3 箇所 (生成 / 時刻復元 / 移動) は
    /// <see cref="MediaTimeline.CurrentZScale"/> 1 箇所から読む。
    ///
    /// 旧来の意味 (noteSpeed をそのまま z スケールにする) を保つ wrapper は
    /// **残さない**: その解釈だとカメラ可視範囲 8.61 / 10 = 0.86 秒しか
    /// 譜面が見えず、Issue #233 の直接原因そのものである。
    /// </summary>
    [Test]
    public void TheZScaleComesFromTheCameraPinnedConversion()
    {
        float zScale = MediaTimeline.ZScaleFor(10f);

        // ノーツ速度 10 のとき 1.6 秒 (設計意図) が画面に入る。
        Assert.AreEqual(MediaTimeline.ApproachSecondsAtSpeed10,
            MediaTimeline.VisibleDepthZ / zScale, 0.0001f);

        // 速度に比例する (速い = 同じ画面距離を速く移動)。
        Assert.AreEqual(zScale * 2f, MediaTimeline.ZScaleFor(20f), 0.0001f);
        Assert.AreEqual(zScale * 0.5f, MediaTimeline.ZScaleFor(5f), 0.0001f);

        // 0 や負の速度でも破綻しない (軸が反転しない)。
        Assert.Greater(MediaTimeline.ZScaleFor(0f), 0f);
        Assert.Greater(MediaTimeline.ZScaleFor(-3f), 0f);
    }

    /// <summary>
    /// 実運用が使うスケールは <see cref="MediaTimeline.CurrentZScale"/> 1 箇所から
    /// 来る (生成・復元・移動で同じ値)。
    /// </summary>
    [Test]
    public void TheCurrentZScaleFollowsTheConfiguredNoteSpeed()
    {
        float saved = GManager.noteSpeed;
        try
        {
            GManager.noteSpeed = 10f;
            Assert.AreEqual(MediaTimeline.ZScaleFor(10f), MediaTimeline.CurrentZScale, 0.0001f);

            GManager.noteSpeed = 25f;
            Assert.AreEqual(MediaTimeline.ZScaleFor(25f), MediaTimeline.CurrentZScale, 0.0001f);
            Assert.AreEqual(MediaTimeline.ZScaleFor(10f) * 2.5f, MediaTimeline.CurrentZScale, 0.0001f);
        }
        finally
        {
            GManager.noteSpeed = saved;
        }
    }

    /// <summary>
    /// z スケールの **復元** は、どんなスケールでも厳密である (S7)。
    ///
    /// ノーツ時刻は生成位置 z = noteTime * zScale から復元する。復元が
    /// 「生成した位置」ではなく「その時のスケール」で計算されると、曲中の
    /// 速度変更で判定する時刻と描画される時刻がずれる。
    /// </summary>
    [TestCase(0.538125f)]
    [TestCase(5.38125f)]
    [TestCase(13.453125f)]
    [TestCase(0.1f)]
    public void TheNoteTimeIsRecoveredExactlyForEveryScale(float zScale)
    {
        const float noteTime = 7.25f;

        float created = noteTime * zScale;
        float recovered = MediaTimeline.NoteTimeFromPositionZ(created, zScale);

        Assert.AreEqual(noteTime, recovered, 0.0001f,
            "The note time recovery must be exact for the scale " + zScale.ToString("F4") + ".");
        Assert.AreEqual(0f, MediaTimeline.NotePositionZ(noteTime, noteTime, zScale), 0.0001f);
    }

    /// <summary>
    /// セッションごとに z スケールは 1 回だけ確定する (PR #263 レビュー S7)。
    ///
    /// 曲中に速度を変更しても、生成・復元・移動はbegin_session の値を使う
    /// ので、ノーツは不連続に飛ばない。変更は次の曲から効く。
    /// </summary>
    [Test]
    public void TheSessionZScaleIsFixedWhenTheSessionBegins()
    {
        float saved = GManager.noteSpeed;
        try
        {
            MediaTimeline.ResetSession();
            GManager.noteSpeed = 10f;
            MediaTimeline.BeginSession(GManager.noteSpeed);

            float sessionScale = MediaTimeline.SessionZScale;
            Assert.AreEqual(MediaTimeline.ZScaleFor(10f), sessionScale, 0.0001f);

            // 曲中に速度を変更してもセッションの値は変わらない。
            GManager.noteSpeed = 25f;
            Assert.AreEqual(sessionScale, MediaTimeline.SessionZScale, 0.0001f);
            Assert.AreEqual(MediaTimeline.ZScaleFor(25f), MediaTimeline.CurrentZScale, 0.0001f);

            // 次のセッションでは新しい速度が使われる。
            MediaTimeline.BeginSession(GManager.noteSpeed);
            Assert.AreEqual(MediaTimeline.ZScaleFor(25f), MediaTimeline.SessionZScale, 0.0001f);
        }
        finally
        {
            GManager.noteSpeed = saved;
            MediaTimeline.ResetSession();
        }
    }

    /// <summary>
    /// 3D レーンの 3 箇所 (生成 / 時刻復元 / 移動) が同じスケールを使うことを
    /// **ソースレベル** で固定する。
    ///
    /// 片方だけ直すと「1 ノーツ = 1 判定」「判定線に着く時刻 = メディア時刻」が
    /// 崩れるので、数値テストだけでは守れない。
    /// </summary>
    [Test]
    public void TheThreeLaneSitesUseTheSameZScale()
    {
        string notes = File.ReadAllText(SourcePath("Assets/Game/Scripts/Gameplay/Notes.cs"));
        string manager = File.ReadAllText(SourcePath("Assets/Game/Scripts/Gameplay/NotesManager.cs"));
        string timeline = File.ReadAllText(SourcePath("Assets/Game/Scripts/Gameplay/MediaTimeline.cs"));

        // 1. 生成 (NotesManager)
        StringAssert.Contains("positionsZ.Add(time * FlashBeat.Gameplay.MediaTimeline.SessionZScale)", manager,
            "The note generation must use the shared z scale.");

        // 2. 時刻復元 (Notes.Start) と 3. 移動 (Notes.Update)
        StringAssert.Contains("MediaTimeline.NoteTimeFromPositionZ(transform.position.z, ZScale)", notes,
            "The note time recovery must use the shared z scale.");
        StringAssert.Contains("MediaTimeline.NotePositionZ(noteTime, GManager.step_time, ZScale)", notes,
            "The note movement must use the shared z scale.");

        // どれか 1 箇所でも生の noteSpeed を z スケールを渡してはいけない。
        Assert.IsFalse(Regex.IsMatch(notes, @"Note(PositionZ|TimeFromPositionZ)\([^)]*GManager\.noteSpeed"),
            "A note position call passes the raw note speed instead of the shared z scale.");
        Assert.IsFalse(Regex.IsMatch(manager, @"Note(PositionZ|TimeFromPositionZ)\([^)]*GManager\.noteSpeed"),
            "A note position call passes the raw note speed instead of the shared z scale.");

        // Notes はセッション確定済みのスケールだけを読む。
        StringAssert.Contains("MediaTimeline.SessionZScale", notes,
            "Notes must read the session z scale.");

        // CurrentZScale は ZScaleFor(GManager.noteSpeed) そのもの。
        StringAssert.Contains("ZScaleFor(GManager.noteSpeed)", timeline,
            "MediaTimeline.CurrentZScale must be the camera pinned conversion of the note speed.");
    }

    private static string SourcePath(string relative)
    {
        string absolute = Path.Combine(Application.dataPath, relative.Substring("Assets/".Length));
        if (File.Exists(absolute)) return absolute;
        return relative;
    }

    [Test]
    public void APictureSoundDriftAboveTheThresholdIsCorrected()
    {
        Assert.IsFalse(MediaTimeline.ShouldResync(10f, 10.05f));
        Assert.IsTrue(MediaTimeline.ShouldResync(10f, 10.5f));
        Assert.IsTrue(MediaTimeline.ShouldResync(10f, 9.5f));
    }

    // --- issue #58 / regression 86ad62d: メディアの Ready と再生開始の順番 ----
    //
    // CurrentMediaTime の「どの時計を正とするか」の決定は
    // MediaTimeline.SelectMediaTime に抽出した (VideoPlayer.isPrepared は
    // EditMode で偽装できないため、実 VideoPlayer を使う直接テストは不可能)。
    // 抽出は振る舞いを変えない (同じ節・同じ順序)。

    /// <summary>
    /// メディアの準備が済んでも **まだ再生していない** なら、クロックは 0。
    /// 曲はまだ始まっていないので、譜面も動いてはいけない。
    /// </summary>
    [Test]
    public void PreparedButNotStartedMediaKeepsTheClockAtZero()
    {
        // 映像は準備済みだが未再生 (pictureStarted = false): 映像の時刻ではなく
        // 動いている時計 (ここではフォールバック 0) を使う。
        Assert.AreEqual(0f, MediaTimeline.SelectMediaTime(false, true, 0f, false, 0f, 0f), 0.0001f);

        // 音声だけ準備済みで未再生 (BundledWave 相当): audioS.time = 0。
        Assert.AreEqual(0f, MediaTimeline.SelectMediaTime(false, false, 0f, true, 0f, 0f), 0.0001f);
    }

    /// <summary>
    /// 再生開始前にメディアが Ready になった場合、再生開始の瞬間のクロックは 0
    /// (映像の時刻 0 がそのまま正になる)。後から来た別ストリームの Ready
    /// (時刻 0) に引っ張られても、開始直後のクロックは 0 のままであること。
    /// </summary>
    [Test]
    public void ReadyBeforePlaybackStartsTheClockAtZero()
    {
        // 映像を再生し始めた瞬間: 映像の時刻はまだ 0。
        Assert.AreEqual(0f, MediaTimeline.SelectMediaTime(true, true, 0f, true, 0f, 0f), 0.0001f);
    }

    /// <summary>
    /// 再生中 (pictureStarted 済み) に別のストリームが Ready になっても、
    /// クロックは 0 に戻らない (単調)。86ad62d の回帰: 準備が再生開始より
    /// 遅いと、Ready の瞬間に映像の時刻 (0) がクロックを掴んでノーツが
    /// 曲の先頭へ戻っていた。映像を再生し始めた後は映像の時刻が正であり、
    /// 準備できたばかりの音声 (時刻 0) やフォールバックが上書きしない。
    /// </summary>
    [Test]
    public void AMidPlayReadyEventDoesNotResetTheClockAfterThePictureStarted()
    {
        // 映像は 5 秒目で再生中。その後に音声ストリームが Ready (時刻 0) に
        // なっても、クロックは映像の 5 秒のまま。
        Assert.AreEqual(5f, MediaTimeline.SelectMediaTime(true, true, 5f, true, 0f, 12f), 0.0001f);

        // 音声が Ready でなくても同じ (フォールバックにも戻らない)。
        Assert.AreEqual(5f, MediaTimeline.SelectMediaTime(true, true, 5f, false, 0f, 12f), 0.0001f);

        // 逆に、まだ映像を再生し始めていないなら (86ad62d 前の状態)、
        // 準備済み映像の時刻 0 がクロックを掴んではいけない。
        Assert.AreEqual(12f, MediaTimeline.SelectMediaTime(false, true, 0f, false, 0f, 12f), 0.0001f);
    }

    /// <summary>
    /// 実コンポーネントの経路でも同じ契約を確かめる: 音声 (BundledWave) が
    /// 準備済みでも再生していないなら <see cref="MusicManager.MediaTimeNow"/>
    /// は 0。StartPlayback が走っていない EditMode では Start は呼ばれないので、
    /// 反映は private フィールド経由で行う (実機の StartPlayback は
    /// pictureStarted を立てて映像の時刻へ切り替える = 上の pure テスト)。
    /// </summary>
    [Test]
    public void TheComponentsClockIsZeroUntilThePlaybackStarts()
    {
        GameObject go = null;
        AudioClip clip = null;
        try
        {
            go = new GameObject("PlaybackClockTests.MusicManager");
            MusicManager manager = go.AddComponent<MusicManager>();
            clip = AudioClip.Create("PlaybackClockTests.Silent", 44100, 1, 44100, false);
            AudioSource audio = go.AddComponent<AudioSource>();
            audio.clip = clip;

            SetPrivateField(manager, "audioMode", FlashBeat.Gameplay.PlaybackSource.BundledWave);
            SetPrivateField(manager, "audioS", audio);
            SetPrivateField(manager, "playbackStartWallTime", 0f);

            // 準備済み (clip あり) だが未再生: audioS.time == 0 がクロックになる。
            Assert.AreEqual(0f, manager.MediaTimeNow, 0.0001f);
        }
        finally
        {
            if (go != null) Object.DestroyImmediate(go);
            if (clip != null) Object.DestroyImmediate(clip);
        }
    }

    static void SetPrivateField(object target, string name, object value)
    {
        System.Reflection.FieldInfo field = target.GetType()
            .GetField(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.IsNotNull(field, "private field not found: " + name);
        field.SetValue(target, value);
    }
}
