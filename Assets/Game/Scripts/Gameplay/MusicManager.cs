using System;
using System.Collections;
using System.IO;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Video;
using YoutubePlayer.Components;
using FlashBeat.Gameplay;

/// <summary>
/// Owns the media pipeline of the GameScene: it asks yt-dlp (with deno as the
/// JavaScript runtime) for the YouTube stream, caches the media locally and plays
/// it with Unity's VideoPlayer.
///
/// Why the media is cached locally instead of streamed by URL:
///   Media Foundation cannot read googlevideo.com URLs (MFCreateSourceReaderFromURL
///   fails with WindowsVideoMedia error 0x80070005) because the request needs the
///   same User-Agent / po-token context yt-dlp used and Unity's VideoPlayer cannot
///   send custom HTTP headers. yt-dlp downloads the very same bytes, and the local
///   file plays back without any network dependency.
///
/// The judgement clock follows the media clock (see MediaTimeline), so the notes,
/// the picture and the sound stay together even when a frame is dropped.
/// </summary>
public class MusicManager : MonoBehaviour
{
    AudioSource audioS;
    [SerializeField] AudioClip Music;
    string songName;
    [SerializeField] GameObject StartText;

    [SerializeField] GameObject kakusi;
    [SerializeField] GameObject[] YPlayer;
    [SerializeField] VideoPlayer[] VPlayer;
    [SerializeField] GameObject load;
    [SerializeField] GameObject waitback;

    // --- media state ------------------------------------------------------------

    const float DirectUrlProbeTimeout = 10f;
    const float PrepareTimeout = 20f;
    const float FallbackStartTimeout = 30f;
    // Issue #226: メディア時計がこの秒数進まなければ破綻 (デコード停止) として
    // 曲を Failed にする。バッファ待ちの一時的な停止を誤検出しないよう、
    // 十分長くする (メディアは再生前にローカルへダウンロード済み)。
    const float StallFailSeconds = 30f;
    const string DirectUrlProbeKeyPrefix = "FlashBeat.DirectUrlProbed.";
    const string GameSceneName = "GameScene";
    const string SelectSceneName = "SelectScene";

    VideoPlayer songVideo;

    /// <summary>
    /// 映像プレーンを有効化したか (Issue #233)。
    /// 映像が 1 フレームも出ていない間は板を隠しておくので、この状態を持つ。
    /// </summary>
    bool picturePlaneActive;
    GameObject streamAudioObject;
    VideoPlayer streamAudioPlayer;
    AudioSource streamAudioSource;
    AudioLevelProbe levelProbe;

    /// <summary>
    /// Cancels an in-flight download when the scene goes away (Retry reloads the
    /// GameScene): the yt-dlp process is killed and the half written file is
    /// removed, so a broken leftover cannot survive into the next attempt
    /// (issue #184 review B1).
    /// </summary>
    CancellationTokenSource mediaCancellation;

    bool mediaReady;
    bool playbackStarted;
    bool pictureStarted;
    bool resultTriggered;

    // --- song end (issue #226) -------------------------------------------------
    // 曲終了の状態機械。duration だけでは (a) 末尾ノーツの確定前に Result へ進み、
    // (b) 動画がメタデータより先に終わると Result に進めない (issue #226)。
    readonly SongEndMachine songEnd = new SongEndMachine();
    /// <summary>メディアの終端 (loop point または再生停止) に達した。以後 judge の
    /// 時計は固定された StartTime から壁時計で進み、末尾ノーツを確定させる。</summary>
    bool mediaEnded;
    bool videoSeenPlaying;
    bool streamAudioSeenPlaying;
    /// <summary>メディアの時刻が最後に進んだ壁時計 (StallFailSeconds の判定用)。</summary>
    float lastMediaProgressWallTime;
    /// <summary>最後のノーツが確定する譜面上の時刻 (SettleDeadline のキャッシュ)。</summary>
    float settleDeadlineCache;
    bool settleDeadlineKnown;

    bool fallbackChecked;
    bool videoPrepared;
    bool streamAudioPrepared;
    PlaybackSource audioMode = PlaybackSource.None;
    MediaStage stage = MediaStage.Idle;
    MediaFailure failure = MediaFailure.None;
    float mediaTime;
    float sceneStartTime;
    float lastStatusLogTime;
    float lastMediaTime;
    float stallSeconds;
    NotesManager notesManager;
    ResourceRequest musicRequest;

    /// <summary>
    /// Wall clock of the moment the song was started. It is the reference for
    /// the fallback clock, so the song ends even when no media could be played.
    /// </summary>
    float playbackStartWallTime;

    /// <summary>
    /// Where the sound of the last started song came from. The evidence of a
    /// real playthrough has to show a YouTube source: the release build ships
    /// no third party media, so a bundled wave or no sound at all means the
    /// song was never really played.
    /// </summary>
    public static PlaybackSource LastPlaybackSource { get; private set; } = PlaybackSource.None;

    /// <summary>
    /// The failure of the last media pipeline run (None while it works). The HUD
    /// (see HudPlayState) shows it and offers the way back to the song list, so
    /// a failed song does not leave the player in front of a dead screen.
    /// </summary>
    public static MediaFailure LastFailure { get; private set; } = MediaFailure.None;

    // --- public state (verification / UI) ---------------------------------------

    /// <summary>メディアの準備が完了しているか (ダウンロード完了・VideoPlayer 準備済み)。</summary>
    public bool MediaReady { get { return mediaReady; } }

    /// <summary>再生が始まったか。</summary>
    public bool PlaybackStarted { get { return playbackStarted; } }

    /// <summary>映像播放器が再生中か。</summary>
    public bool VideoPlaying { get { return songVideo != null && songVideo.isPlaying; } }

    /// <summary>映像播放器の準備完了状態。</summary>
    public bool VideoPrepared { get { return songVideo != null && songVideo.isPrepared; } }

    /// <summary>現在 Playing _URI_ の再生位置 (秒)。判定の基準時刻になる。</summary>
    public float MediaTimeNow { get { return CurrentMediaTime(); } }

    /// <summary>音声の供給元の説明 (デバッグ表示・検証用)。</summary>
    public string AudioModeName { get { return audioMode.ToString(); } }

    /// <summary>
    /// True while the pipeline has decided that the song cannot be played and
    /// the automatic return to the song list is pending. The HUD (HudPlayState)
    /// reads it so the failure is shown at once instead of when its own timeout
    /// runs out.
    /// </summary>
    public static bool SelectReturnPending { get; private set; }

    void Start()
    {
        // Issue #224: GameScene の読み直し (リトライ / 再プレイ) でゲートを閉じる。
        // ポーズ中にリトライ / リタイアしても次のプレイが止まったままにならない。
        PauseGate.HardReset();

        waitback.SetActive(true);
        GameSceneManager.Menu = true;
        GManager.Start = false;
        GManager.step_time = 0;
        // Issue #183: 遷移の待ち時間に動いたかもしれない選択を、固定した
        // プレイセッションへ戻してから曲情報を読む。
        PlaySessionSnapshot session = PlaySessionSnapshot.Current;
        if (session != null) session.RestoreSongId();
        SongData sessionSong = PlaySessionSnapshot.ResolveSong(GManager.songID);
        songName = sessionSong != null ? sessionSong.title : "";
        audioS = GetComponent<AudioSource>();
        // 同期 Resources.Load は最初のフレームを止めていたため非同期にする。
        // 決定 (audioMode) の前に PrepareMediaCoroutine 内で完了を待つ。
        musicRequest = Resources.LoadAsync<AudioClip>("Musics/" + songName);
        notesManager = FindFirstObjectByType<NotesManager>();
        GManager.played = false;
        GManager.nextNotes = 1;
        audioS.volume = GManager.mainVolume;
        StartText.SetActive(true);

        kakusi.SetActive(true);
        for (int i = 0; i < YPlayer.Length; i++)
        {
            YPlayer[i].SetActive(false);
        }
        if (GManager.songID < YPlayer.Length)
        {
            YPlayer[GManager.songID].SetActive(true);

            songVideo = VPlayer[GManager.songID];
            if (songVideo == null)
            {
                songVideo = YPlayer[GManager.songID].GetComponent<VideoPlayer>();
            }
        }
        else
        {
            // Cloud Catalog song (#17): no per-song object exists in the scene,
            // so one dynamic VideoPlayer is created for the remote stream
            // instead of one object per song.
            songVideo = CreateRemoteVideoPlayer();
        }

        sceneStartTime = Time.time;
        AttachLevelProbe();

        load.SetActive(true);
        mediaCancellation = new CancellationTokenSource();
        var mediaToken = mediaCancellation.Token;
        StartCoroutine(PrepareMediaCoroutine(mediaToken));
    }

    void OnDestroy()
    {
        // A Retry (scene reload) or teardown cancels an in-flight download: the
        // yt-dlp process is killed (YouTubeStreamResolver registers the kill on
        // the token) and the half written file is removed (issue #184 review B1).
        // No Dispose: a continuation that resumes later may still register on
        // the token, and Register on a disposed source throws.
        if (mediaCancellation == null) return;
        mediaCancellation.Cancel();
        mediaCancellation = null;
    }

    VideoPlayer CreateRemoteVideoPlayer()
    {
        // Cloud Catalog (#17): one reusable player for songs that have no
        // per-song object in the scene. Rendering follows the default
        // camera-plane mode; audio goes through the existing AudioSource.
        GameObject playerObject = new GameObject("FlashBeatRemoteVideo");
        VideoPlayer player = playerObject.AddComponent<VideoPlayer>();
        player.playOnAwake = false;
        player.renderMode = VideoRenderMode.CameraNearPlane;
        return player;
    }

    void AttachLevelProbe()
    {
        var listener = FindFirstObjectByType<AudioListener>();
        if (listener == null)
        {
            Debug.LogWarning("[MusicManager] no AudioListener found - the audio level cannot be measured.");
            return;
        }

        levelProbe = listener.GetComponent<AudioLevelProbe>();
        if (levelProbe == null)
        {
            levelProbe = listener.gameObject.AddComponent<AudioLevelProbe>();
        }
    }

    IEnumerator PrepareMediaCoroutine(CancellationToken mediaToken)
    {
        // Issue #183: 動画の id もプレイセッションで固定した曲から読む。
        SongData sessionSong = PlaySessionSnapshot.ResolveSong(GManager.songID);
        string videoId = sessionSong != null ? sessionSong.videoId : "";

        // A new run starts without a failure: the HUD shows the state of the
        // current pipeline.
        LastFailure = MediaFailure.None;
        SelectReturnPending = false;
        failure = MediaFailure.None;

        // The Invidious player of the package cannot resolve anything today
        // (0/13 public instances answer) and its error handler would fight with
        // our own player, so it is removed from the song object.
        DisableInvidiousPlayer();

        // 読み込み中表示 (動画のダウンロード・準備中)。失敗時は理由を表示してから自動で隠す。
        // v6 有効時は V6LoadingView が単独で表示するため、MediaLoadingOverlay は出さない。
        MediaLoadingOverlay loading = null;
        bool showLegacyMediaOverlay = GameLoadingRoute.UsesLegacyOverlay;
        if (showLegacyMediaOverlay)
        {
            try { loading = MediaLoadingOverlay.Ensure(); } catch (Exception e) { Debug.LogWarning("[MusicManager] loading overlay unavailable: " + e.Message); }
            if (loading != null) { loading.Show("曲情報を取得中…"); }
        }

        // 1. What does YouTube offer for this video?
        stage = MediaStage.ResolvingStream;
        var formatsTask = YouTubeStreamResolver.ListFormatsAsync(videoId, mediaToken);
        while (!formatsTask.IsCompleted) yield return null;

        var formatsOut = formatsTask.Result;
        var formats = YouTubeStreamResolver.ParseFormats(formatsOut.Ok ? formatsOut.Stdout : "");
        bool streamResolved = formatsOut.Ok || formats.Count > 0;
        Debug.Log($"[MusicManager] yt-dlp -F videoId={videoId} formats={formats.Count} {(formatsOut.Ok ? "ok" : formatsOut.ErrorSummary)}");
        for (int i = 0; i < formats.Count; i++)
        {
            Debug.Log($"[MusicManager]   format {formats[i]}");
        }

        var canMerge = !string.IsNullOrEmpty(YouTubeStreamResolver.ResolveFfmpegPath());
        var plan = YouTubeMediaCache.SelectPlan(formats, canMerge);
        Debug.Log($"[MusicManager] media plan '{songName}': {plan.Summary} (merge available: {canMerge})");

        // 2. Direct URL probe (once per video): documents why the URL cannot be
        //    handed to Media Foundation.
        if (ShouldProbeDirectUrl(videoId))
        {
            yield return ProbeDirectUrl(videoId, plan.VideoSelector);
        }

        // 3. Download the media into the local cache. The merged file has its own
        //    prefix, so a picture-only file of an earlier revision is not reused.
        //    The cache lives in the player environment (persistentDataPath), the
        //    release build ships no third party media at all.
        stage = MediaStage.DownloadingMedia;
        var videoPrefix = YouTubeMediaCache.PrefixForPlan(plan);
        if (showLegacyMediaOverlay && loading != null) { loading.Show("動画をダウンロード中…"); }
        var videoTask = YouTubeMediaCache.EnsureFileAsync(videoId, plan.VideoSelector, videoPrefix, mediaToken);
        while (!videoTask.IsCompleted) yield return null;
        var videoFile = videoTask.Result;
        Debug.Log($"[MusicManager] video file: {(videoFile.Ok ? videoFile.Path + (videoFile.FromCache ? " (cache)" : " (downloaded)") : videoFile.Detail)}");
        if (!videoFile.Ok && showLegacyMediaOverlay && loading != null) { loading.ShowError("動画をダウンロードできませんでした: " + videoFile.Detail); }

        // The merged file (video + YouTube sound in one mp4) is the wanted state.
        // If the merge failed (no ffmpeg, download error) the picture still has to
        // play, so the video-only stream is fetched as the fallback.
        bool combined = plan.Combined;
        if (!videoFile.Ok && plan.Combined && !string.IsNullOrEmpty(plan.FallbackVideoSelector))
        {
            Debug.LogWarning($"[MusicManager] merged download failed ({videoFile.Detail}) - falling back to the video-only stream");
            YouTubeMediaCache.RemoveProducedFiles(YouTubeMediaCache.CacheDirectoryFor(videoId), YouTubeMediaCache.MergedFilePrefix);
            var fallbackTask = YouTubeMediaCache.EnsureFileAsync(videoId, plan.FallbackVideoSelector, YouTubeMediaCache.VideoFilePrefix, mediaToken);
            while (!fallbackTask.IsCompleted) yield return null;
            videoFile = fallbackTask.Result;
            combined = false;
            Debug.Log($"[MusicManager] fallback video file: {(videoFile.Ok ? videoFile.Path : videoFile.Detail)}");
        }

        MediaFileResult audioFile = null;
        if (plan.HasAudioFile && !combined)
        {
            var audioTask = YouTubeMediaCache.EnsureFileAsync(videoId, plan.AudioSelector, YouTubeMediaCache.AudioFilePrefix, mediaToken);
            while (!audioTask.IsCompleted) yield return null;
            audioFile = audioTask.Result;
            Debug.Log($"[MusicManager] audio file: {(audioFile.Ok ? audioFile.Path + (audioFile.FromCache ? " (cache)" : " (downloaded)") : audioFile.Detail)}");
        }

        // 4. Prepare the picture player.
        bool videoReady = false;
        if (videoFile.Ok && songVideo != null)
        {
            stage = MediaStage.PreparingVideo;
            if (showLegacyMediaOverlay && loading != null) { loading.Show("動画を準備中…"); }
            yield return PrepareLocalVideo(videoFile.Path, combined);
            videoReady = videoPrepared;

            if (!videoReady)
            {
                // Issue #184 (review B2): ファイルは「完了」マーク付きでも開けないなら
                // 壊れている。マーカーを剥がさないと EnsureFileAsync は二度と
                // ダウンロードせず、Option でキャッシュを消すまで回復しない。
                // そのためマーカー剥離とカウントは上限に関係なく行う。上限に
                // 達した後の再取得は EnsureFileAsync 側が拒否し、Option 画面の
                // キャッシュ削除 (カウンタをリセットする) が明示的な回復手段。
                YouTubeMediaCache.InvalidateProducedFile(videoFile.Path);
                MediaRefetchPolicy.CountRefetch(videoId);
                Debug.Log($"[MusicManager] the player cannot open the cached file ({Path.GetFileName(videoFile.Path)}) - " +
                          $"cache entry dropped, refetch budget used {MediaRefetchPolicy.CountFor(videoId)}/{MediaRefetchPolicy.MaxAttemptsPerVideo}" +
                          (MediaRefetchPolicy.MayRefetch(videoId) ? "" : " - limit reached, delete the media cache in the Option screen"));
            }
        }

        // 5. Prepare the sound player for the split (video only + audio only) case.
        bool audioReady = false;
        if (!combined && audioFile != null && audioFile.Ok)
        {
            stage = MediaStage.PreparingAudio;
            if (showLegacyMediaOverlay && loading != null) { loading.Show("音声を準備中…"); }
            yield return PrepareStreamAudio(audioFile.Path);
            audioReady = streamAudioPrepared;

            if (!audioReady)
            {
                // 映像と同じ扱い (issue #184 review B2): 開けないキャッシュ音声は
                // 上限に関係なくマーカーを剥がして捨てる。再取得そのものは
                // EnsureFileAsync 側の予算で制限される。
                YouTubeMediaCache.InvalidateProducedFile(audioFile.Path);
                MediaRefetchPolicy.CountRefetch(videoId);
                Debug.Log($"[MusicManager] the player cannot open the cached audio ({Path.GetFileName(audioFile.Path)}) - " +
                          $"cache entry dropped, refetch budget used {MediaRefetchPolicy.CountFor(videoId)}/{MediaRefetchPolicy.MaxAttemptsPerVideo}");
            }
        }

        // 6. Decide where the sound comes from (a missing wav yields None
        //    instead of throwing - the recovery then takes over).
        // The bundled wav is loaded asynchronously (Start); the decision needs
        // to know whether it exists, so wait for the request here.
        if (musicRequest != null)
        {
            while (!musicRequest.isDone) yield return null;
            Music = musicRequest.asset as AudioClip;
            if (Music == null)
            {
                // The release build ships no third party media: the build moves
                // Assets/Game/Resources/Musics out of the build, so the wav that
                // belongs to the chart is not there. The YouTube media is then the
                // only sound source - that is the wanted state, it is only logged so
                // that the player log shows where the sound comes from.
                Debug.Log($"[MusicManager] no bundled wav for '{songName}' - the YouTube media is the only sound source");
            }
        }
        audioMode = PlaybackRecovery.DecideAudioSource(combined, videoReady, audioReady, Music != null);

        mediaReady = true;
        if (showLegacyMediaOverlay && loading != null) { loading.Hide(); }
        load.SetActive(false);
        waitback.SetActive(false);
        Debug.Log($"[Perf] media ready {(Time.time - sceneStartTime) * 1000f:F0}ms");

        // 7. Which stage broke? The most specific reason wins.
        if (!streamResolved)
        {
            failure = MediaFailure.StreamUnresolved;
        }
        else if (!videoFile.Ok)
        {
            failure = MediaFailure.MediaUnavailable;
        }
        else if (!videoReady)
        {
            failure = MediaFailure.VideoPrepareFailed;
        }
        else if (!combined && audioFile != null && !audioFile.Ok)
        {
            failure = MediaFailure.MediaUnavailable;
        }
        else if (!combined && audioFile != null && audioFile.Ok && !audioReady)
        {
            failure = MediaFailure.AudioUnavailable;
        }

        bool hasSound = audioMode != PlaybackSource.None;

        if (failure != MediaFailure.None)
        {
            if (PlaybackRecovery.NeedsSelectReturn(failure, hasSound))
            {
                // Without a sound track the song cannot be played. The player is
                // taken back to the song list instead of sitting in front of a
                // song he cannot play.
                HandlePlaybackFailure(failure, videoReady);
            }
            else
            {
                // The sound is there, so the song is playable: the failure is
                // only reported (with the stage it happened in).
                Debug.Log(PlaybackRecovery.FailureLog(stage, failure, videoReady));
                stage = MediaStage.Ready;
            }
        }
        else
        {
            stage = MediaStage.Ready;
        }

        if (failure == MediaFailure.None && hasSound)
        {
            // The song is playable: an old refetch count from earlier broken
            // files is stale, so the next real incident gets its full budget of
            // attempts again (issue #184).
            MediaRefetchPolicy.Reset(videoId);
        }

        Debug.Log($"[MusicManager] media ready: song='{songName}' audioMode={audioMode} video={(videoReady ? "ok" : "missing")} " +
                  $"(bundled wav {(Music != null ? "present" : "missing")}) - press Space to start");
    }

    /// <summary>
    /// The media pipeline failed and the song cannot be played. The failure is
    /// logged with its stage, the player gets a message and - after a short
    /// grace period - the game goes back to the song list, where another song
    /// can be picked directly.
    /// </summary>
    void HandlePlaybackFailure(MediaFailure reason, bool hasPicture)
    {
        // The log names the stage the pipeline was in when it broke - the stage
        // itself only becomes "failed" afterwards.
        Debug.Log(PlaybackRecovery.FailureLog(stage, reason, hasPicture));
        stage = MediaStage.Failed;
        failure = reason;
        SelectReturnPending = true;

        SetStartMessage(PlaybackRecovery.StartHint(reason));

        StartCoroutine(ReturnToSelectScene());
    }

    /// <summary>
    /// Takes the player back to the song list. A fade that is still running is
    /// skipped by the fade system, so the attempt is repeated a few times.
    /// </summary>
    IEnumerator ReturnToSelectScene()
    {
        yield return new WaitForSeconds(PlaybackRecovery.SelectReturnDelaySeconds);

        for (int attempt = 1; PlaybackRecovery.ShouldRetrySelectReturn(attempt); attempt++)
        {
            if (playbackStarted)
            {
                // The player started the song anyway. The song then ends on the
                // wall clock (Issue #48/#31) and the result screen offers the
                // way back to the song list - the song is not interrupted.
                Debug.Log("[MusicManager] the song is already running - it ends on the wall clock, Esc opens the menu to go back to the song list");
                yield break;
            }

            Debug.Log(PlaybackRecovery.SelectReturnLog(attempt));
            Initiate.Fade(SelectSceneName, Color.black, 1.0f);

            float waited = 0f;
            while (waited < PlaybackRecovery.SelectReturnRetrySeconds && SceneManager.GetActiveScene().name == GameSceneName)
            {
                waited += Time.deltaTime;
                yield return null;
            }
            if (SceneManager.GetActiveScene().name != GameSceneName) yield break;
        }

        Debug.Log("[MusicManager] the automatic return to the select screen failed - Esc opens the menu, Backspace goes back to the song list");
    }

    /// <summary>
    /// Writes a message into the label of the "press Space" hint. The scene is
    /// not modified: the label is looked up at runtime.
    /// </summary>
    void SetStartMessage(string message)
    {
        if (StartText == null) return;

        var label = StartText.GetComponentInChildren<TextMeshProUGUI>(true);
        if (label != null)
        {
            label.text = message;
            return;
        }
        Debug.LogWarning($"[MusicManager] no label under StartText - the message stays in the log: {message}");
    }

    void DisableInvidiousPlayer()
    {
        if (GManager.songID >= YPlayer.Length) return;

        var invidious = YPlayer[GManager.songID].GetComponent<InvidiousVideoPlayer>();
        if (invidious == null) return;

        invidious.enabled = false;
        Destroy(invidious);
        Debug.Log($"[MusicManager] InvidiousVideoPlayer removed for '{songName}' (yt-dlp takes over)");
    }

    bool ShouldProbeDirectUrl(string videoId)
    {
        if (Environment.GetEnvironmentVariable("FLASHBEAT_SKIP_DIRECT_PROBE") == "1") return false;
        return PlayerPrefs.GetInt(DirectUrlProbeKeyPrefix + videoId, 0) == 0;
    }

    IEnumerator ProbeDirectUrl(string videoId, string formatSelector)
    {
        Debug.Log($"[MusicManager] direct URL probe for videoId={videoId} (runs once per video)");

        var urlTask = YouTubeStreamResolver.GetStreamUrlAsync(videoId, formatSelector);
        while (!urlTask.IsCompleted) yield return null;

        var urlOut = urlTask.Result;
        if (!urlOut.Ok)
        {
            Debug.Log($"[MusicManager] direct URL probe: yt-dlp failed ({urlOut.ErrorSummary})");
        }
        else
        {
            var url = FirstUrl(urlOut.Stdout);
            if (string.IsNullOrEmpty(url) || songVideo == null)
            {
                Debug.Log($"[MusicManager] direct URL probe: no usable URL");
            }
            else
            {
                songVideo.errorReceived += OnVideoError;
                songVideo.source = VideoSource.Url;
                songVideo.url = url;
                songVideo.Prepare();

                float waited = 0f;
                while (!songVideo.isPrepared && waited < DirectUrlProbeTimeout)
                {
                    yield return null;
                    waited += Time.deltaTime;
                }

                Debug.Log($"[MusicManager] direct URL probe: prepared={songVideo.isPrepared} after {waited:F1}s url={TruncateUrl(url)}");

                songVideo.Stop();
                songVideo.url = "";
                songVideo.errorReceived -= OnVideoError;
            }
        }

        PlayerPrefs.SetInt(DirectUrlProbeKeyPrefix + videoId, 1);
        PlayerPrefs.Save();
    }

    IEnumerator PrepareLocalVideo(string path, bool combined)
    {
        videoPrepared = false;
        songVideo.errorReceived += OnVideoError;
        songVideo.loopPointReached += OnSongVideoLoopReached;
        songVideo.source = VideoSource.Url;
        songVideo.audioOutputMode = VideoAudioOutputMode.AudioSource;
        songVideo.SetTargetAudioSource(0, audioS);
        songVideo.url = path;
        songVideo.Prepare();

        float waited = 0f;
        while (!songVideo.isPrepared && waited < PrepareTimeout)
        {
            yield return null;
            waited += Time.deltaTime;
        }

        if (!songVideo.isPrepared)
        {
            // Some Unity versions insist on an explicit file:// URL.
            songVideo.url = "file://" + path.Replace('\\', '/');
            songVideo.Prepare();
            waited = 0f;
            while (!songVideo.isPrepared && waited < 5f)
            {
                yield return null;
                waited += Time.deltaTime;
            }
        }

        if (songVideo.isPrepared && combined)
        {
            // The scene already enables audio track 0; make sure the sound of a
            // progressive file really reaches the mixer.
            try { songVideo.EnableAudioTrack(0, true); } catch { }
        }

        if (songVideo.isPrepared)
        {
            WireVideoPicture();
            // Issue #233: 映像プレーンはシーンで非アクティブ (m_IsActive: 0)
            // なので、準備できたここで有効化する (シーンは編集しない)。
            picturePlaneActive = VideoPicture.Activate(songVideo);
        }

        Debug.Log($"[MusicManager] VideoPlayer prepared={songVideo.isPrepared} file={Path.GetFileName(path)} after {waited:F1}s");
        videoPrepared = songVideo.isPrepared;
    }

    /// <summary>
    /// The song object of the scene carries the picture on a quad, but its
    /// VideoPlayer has no target renderer (the Invidious path never worked, so
    /// nobody noticed). The renderer is wired at runtime - the scene itself is
    /// not modified.
    /// </summary>
    void WireVideoPicture()
    {
        if (songVideo == null) return;
        if (songVideo.targetMaterialRenderer != null) return;

        var renderer = songVideo.GetComponent<Renderer>();
        if (renderer == null)
        {
            Debug.LogWarning("[MusicManager] the song object has no renderer - the picture stays invisible");
            return;
        }

        // renderer.material creates an instance, the shared built-in material is
        // not touched.
        var material = renderer.material;
        const string textureProperty = "_MainTex";
        if (!material.HasProperty(textureProperty))
        {
            Debug.LogWarning($"[MusicManager] material '{material.name}' has no {textureProperty} - the picture stays invisible");
            return;
        }

        songVideo.targetMaterialRenderer = renderer;
        songVideo.targetMaterialProperty = textureProperty;

        // The render mode that draws the picture into a material is resolved by
        // name, so the code does not depend on the enum member of the Unity
        // version in use.
        if (TryGetMaterialRenderMode(out var materialMode))
        {
            songVideo.renderMode = materialMode;
        }
        else
        {
            Debug.LogWarning($"[MusicManager] no material render mode found (modes: {string.Join(", ", Enum.GetNames(typeof(VideoRenderMode)))})");
        }

        Debug.Log($"[MusicManager] picture wired to the renderer of '{songVideo.gameObject.name}' " +
                  $"({textureProperty}, renderMode={songVideo.renderMode})");
    }

    static bool TryGetMaterialRenderMode(out VideoRenderMode mode)
    {
        mode = default(VideoRenderMode);
        foreach (var name in Enum.GetNames(typeof(VideoRenderMode)))
        {
            if (name.IndexOf("Material", StringComparison.OrdinalIgnoreCase) < 0) continue;
            mode = (VideoRenderMode)Enum.Parse(typeof(VideoRenderMode), name);
            return true;
        }
        return false;
    }

    IEnumerator PrepareStreamAudio(string path)
    {
        streamAudioPrepared = false;
        streamAudioObject = new GameObject("FlashBeatStreamAudio");
        streamAudioPlayer = streamAudioObject.AddComponent<VideoPlayer>();
        streamAudioSource = streamAudioObject.AddComponent<AudioSource>();
        streamAudioSource.volume = GManager.mainVolume;

        streamAudioPlayer.audioOutputMode = VideoAudioOutputMode.AudioSource;
        streamAudioPlayer.SetTargetAudioSource(0, streamAudioSource);
        streamAudioPlayer.source = VideoSource.Url;
        streamAudioPlayer.errorReceived += OnVideoError;
        streamAudioPlayer.url = path;
        streamAudioPlayer.Prepare();

        float waited = 0f;
        while (!streamAudioPlayer.isPrepared && waited < PrepareTimeout)
        {
            yield return null;
            waited += Time.deltaTime;
        }

        Debug.Log($"[MusicManager] stream audio prepared={streamAudioPlayer.isPrepared} file={Path.GetFileName(path)} after {waited:F1}s");

        if (!streamAudioPlayer.isPrepared)
        {
            Destroy(streamAudioObject);
            streamAudioObject = null;
            streamAudioPlayer = null;
            streamAudioSource = null;
        }

        streamAudioPrepared = streamAudioPlayer != null && streamAudioPlayer.isPrepared;
    }

    void OnVideoError(VideoPlayer source, string message)
    {
        Debug.LogWarning($"[MusicManager] VideoPlayer error: {message}");
        if (playbackStarted)
        {
            FailSongEnd(SongEndFailure.MediaError,
                "[MusicManager] media error during playback: the song is marked failed and will not be completed as a success (issue #226)");
        }
    }

    /// <summary>
    /// Issue #226: 映像の終端 (loop point) を実メディアの終端とみなす。
    /// メタデータの duration が実動画より長くても、ここで曲の終端条件が成立する。
    /// </summary>
    void OnSongVideoLoopReached(VideoPlayer source)
    {
        if (source != songVideo || !playbackStarted)
        {
            return;
        }
        if (!mediaEnded)
        {
            mediaEnded = true;
            Debug.Log("[MusicManager] media reached its end (loop point)");
        }
    }

    /// <summary>
    /// Issue #226: 曲終了の破綻 (再生中のエラー、長時間のストール)。
    /// 状態機械を Failed にして Result への自動遷移を止める。
    /// 表示と回復は既存の経路に流す: VideoPlayer のエラーは GameHudRuntime が
    /// errorReceived を購読して GameErrorView を出す。エラーイベントのない破綻
    /// (ストール) では Esc のポーズメニューから選曲画面へ戻れる。
    /// </summary>
    void FailSongEnd(SongEndFailure reason, string log)
    {
        if (songEnd.State == SongEndState.Completed || songEnd.State == SongEndState.Failed)
        {
            return;
        }
        Debug.LogWarning(log);
        songEnd.Fail(reason);
        stage = MediaStage.Failed;
    }

    // --- Issue #224: ポーズゲートのメディア停止 / 再開 --------------------------------

    /// <summary>ポーズゲート側でメディアを止めた (再開待ち)。</summary>
    bool pausedByGate;

    /// <summary>
    /// ポーズ中にメディア (映像 / 音声) を止める。毎フレーム呼ばれて冪等。
    /// 実メンバー名はこのクラスの所有するもの (songVideo / streamAudioPlayer /
    /// audioS) をそのまま使う。
    /// </summary>
    void PauseMediaForGate()
    {
        if (songVideo != null && songVideo.isPlaying) songVideo.Pause();
        if (audioMode == PlaybackSource.StreamSeparate && streamAudioPlayer != null && streamAudioPlayer.isPlaying)
        {
            streamAudioPlayer.Pause();
        }
        if (audioMode == PlaybackSource.BundledWave && audioS != null && audioS.isPlaying) audioS.Pause();
        pausedByGate = true;
    }

    /// <summary>
    /// 再開 (ポーズ解除) の最初のフレームにメディアを再開し、時計の簿記を
    /// リセットする。判定基準時刻の補正 (壁時計の詰め) は PauseGate.Exit が行う。
    /// 再生開始前 (Space 前) のポーズでは何も再生しない (StartPlayback だけが
    /// 再生を始める)。メディア終端後のポーズでも再再生しない (終端で止まっているのが正常)。
    /// </summary>
    void ResumeMediaFromGate()
    {
        pausedByGate = false;

        if (playbackStarted && !mediaEnded)
        {
            if (songVideo != null && songVideo.isPrepared && !songVideo.isPlaying) songVideo.Play();
            if (audioMode == PlaybackSource.StreamSeparate && streamAudioPlayer != null &&
                streamAudioPlayer.isPrepared && !streamAudioPlayer.isPlaying)
            {
                streamAudioPlayer.Play();
            }
            if (audioMode == PlaybackSource.BundledWave && audioS != null) audioS.UnPause();
        }

        // ストール誤検出の防止: メディアはポーズ中の間止まっていたので、
        // 直前の進行时刻はもう意味を持たない。固定中の譜面時刻からやり直す。
        lastMediaTime = PauseGate.FrozenMediaTime;
        mediaTime = PauseGate.FrozenMediaTime;
        stallSeconds = 0f;
        lastMediaProgressWallTime = Time.time;
    }

    void Update()
    {
        // Issue #224: ポーズ中はメディアも時計も止める。ここより先の処理
        // (メディア時計の追従 / stall 判定 / 曲終了の状態機械) はすべて
        // 時計が動いている前提なので、ポーズ中は丸ごとスキップする。
        if (PauseGate.IsPaused)
        {
            PauseMediaForGate();
            return;
        }
        if (pausedByGate)
        {
            ResumeMediaFromGate();
        }

        if (!playbackStarted)
        {
            // Nothing may play before the player starts the song.
            if (songVideo != null && songVideo.isPlaying) songVideo.Pause();

            // Space may only start the song when the media is ready AND the
            // chart (notes) finished loading - otherwise the judge would miss
            // the first notes. ポーズメニューが開いている間は開始しない。
            bool notesReady = notesManager == null || notesManager.IsReady;
            bool canStart = (mediaReady || Time.time - sceneStartTime > FallbackStartTimeout) && notesReady;
            if (canStart && KeyActions.IsDown(KeyActions.KeyAction.Play))
            {
                StartPlayback();
            }
            return;
        }

        mediaTime = CurrentMediaTime();

        // Issue #233: 映像プレーンは有効化しても、映像のフレームが 1 枚も
        // 出ていない間は板 (白) を隠す。フレームが出たら板を見せる。
        if (picturePlaneActive && songVideo != null)
        {
            VideoPicture.ShowWhenFrameReady(songVideo, songVideo.frameCount > 0);
        }

        if (!mediaEnded)
        {
            DetectMediaEnd();
        }

        if (mediaEnded)
        {
            // Issue #226: メディア (映像) が終端で止まっても judge の時計は進める。
            // StartTime を固定したままにすると Time.time - StartTime が終端時刻から
            // 続き、末尾ノーツが既定の Miss 窓で確定する (HOLD は中断 / 期限切れ)。
            GManager.step_time = Time.time - GManager.StartTime;
        }
        else
        {
            // The judgement clock follows the media clock, so a dropped frame or
            // a buffering stall cannot make the notes drift away from the song.
            GManager.StartTime = MediaTimeline.JudgeStartTime(Time.time, mediaTime);
            GManager.step_time = mediaTime;
        }

        if (mediaEnded)
        {
            // メディア終端後は時計が止まっているのが正常なので、ストール扱いにしない。
            lastMediaTime = mediaTime;
        }
        else if (Mathf.Abs(mediaTime - lastMediaTime) < 0.0005f)
        {
            stallSeconds += Time.deltaTime;
            if (stallSeconds > 1f)
            {
                Debug.LogWarning($"[MusicManager] media stalled for {stallSeconds:F1}s at mediaTime={mediaTime:F2}");
                stallSeconds = 0f;
            }
            // Issue #226: 時計が長時間止まったままならデコード破綻として Failed にする
            // (成功偽装の完了遷移をしない)。時計が進めば lastMediaProgressWallTime が
            // 更新され、この判定は解除される。
            if (HasRealMediaClock() && Time.time - lastMediaProgressWallTime > StallFailSeconds)
            {
                FailSongEnd(SongEndFailure.MediaStalled,
                    $"[MusicManager] media clock stuck at {mediaTime:F2}s for over {StallFailSeconds}s (issue #226): marking the song failed");
            }
        }
        else
        {
            stallSeconds = 0f;
            lastMediaProgressWallTime = Time.time;
            lastMediaTime = mediaTime;
        }

        // The split stream runs picture and sound in two players, so they can
        // drift apart. The picture is the reference (the chart was authored
        // against the video), the sound is pulled back to it.
        if (audioMode == PlaybackSource.StreamSeparate &&
            pictureStarted && songVideo != null && songVideo.isPrepared &&
            streamAudioPlayer != null && streamAudioPlayer.isPrepared)
        {
            float pictureTime = (float)songVideo.time;
            float soundTime = (float)streamAudioPlayer.time;
            if (MediaTimeline.ShouldResync(pictureTime, soundTime))
            {
                streamAudioPlayer.time = pictureTime;
                Debug.LogWarning($"[MusicManager] A/V drift {Mathf.Abs(pictureTime - soundTime):F3}s - the sound was re-seeked to the picture");
            }
        }

        // The split stream can end up silent (Unity's Windows decoder does not
        // accept every audio only container). Measure the mix and fall back to the
        // wav that ships with the chart when it stays quiet.
        if (audioMode == PlaybackSource.StreamSeparate && !fallbackChecked && mediaTime > 3f)
        {
            fallbackChecked = true;
            float rms = levelProbe != null ? levelProbe.LastRms : 0f;
            if (rms < 0.001f && Music != null)
            {
                Debug.LogWarning($"[MusicManager] stream audio is silent (rms={rms:F5}) - falling back to the bundled wav");
                SwitchToBundledAudio(mediaTime);
            }
            else if (rms < 0.001f)
            {
                // There is no wav to fall back to: the song would run silent,
                // which is not playable. The song ends on the wall clock and
                // the result screen offers the way back to the song list.
                Debug.Log(PlaybackRecovery.FailureLog(stage, MediaFailure.AudioSilent, songVideo != null && songVideo.isPrepared));
                failure = MediaFailure.AudioSilent;
            }
        }

        if (Time.time - lastStatusLogTime >= 2f)
        {
            lastStatusLogTime = Time.time;
            float rms = levelProbe != null ? levelProbe.LastRms : 0f;
            float peak = levelProbe != null ? levelProbe.PeakRms : 0f;
            string videoState = "-";
            if (songVideo != null)
            {
                videoState = $"playing={songVideo.isPlaying} frame={songVideo.frameCount} prepared={songVideo.isPrepared}";
            }
            Debug.Log($"[MusicManager] STATUS mediaTime={mediaTime:F2} audioMode={audioMode} rms={rms:F4} peak={peak:F4} " +
                      $"video[{videoState}] perfect={GManager.perfect} great={GManager.great} bad={GManager.bad} miss={GManager.miss} " +
                      $"failure={failure}");
        }

        // Issue #226: 曲終了は状態機械 (SongEndMachine) で裁定する。
        // duration だけの判定は (a) 末尾ノーツの確定前に Result へ進み、
        // (b) メタデータより先に動画が終わると Result に進めなかった。
        SongData sessionSong = PlaySessionSnapshot.ResolveSong(GManager.songID);
        float songDuration = sessionSong != null ? sessionSong.duration : 0f;

        if (!resultTriggered)
        {
            bool mediaClockIsReal = HasRealMediaClock();
            bool durationEnded = mediaClockIsReal
                ? MediaTimeline.HasSongEnded(mediaTime, songDuration)
                : mediaTime >= MediaTimeline.FallbackSongEnd(songDuration);

            int totalNotes = notesManager != null ? notesManager.noteNum : 0;
            int settledNotes = GManager.nextNotes - 1;
            if (settledNotes < 0)
            {
                settledNotes = 0;
            }
            int pendingNotes = totalNotes - settledNotes;
            if (pendingNotes < 0)
            {
                pendingNotes = 0;
            }

            var endTick = new SongEndTick
            {
                EndReached = mediaEnded || durationEnded,
                JudgeTime = Time.time - GManager.StartTime,
                SettleDeadline = SettleDeadline(),
                PendingNotes = pendingNotes,
                TotalNotes = totalNotes
            };
            if (songEnd.Update(in endTick))
            {
                resultTriggered = true;
                if (songEnd.ForcedByDeadline)
                {
                    Debug.LogWarning(
                        $"[MusicManager] song end forced with {pendingNotes} unsettled note(s) " +
                        "(judge accounting drift or skipped notes) - the result total may not match the note count");
                }
                Debug.Log($"[MusicManager] song completed: judged {settledNotes}/{totalNotes}, mediaEnded={mediaEnded}, state={songEnd.State}");
                audioS.volume -= 0.001f;
                Initiate.Fade("ResultScene", Color.black, 1.0f);
            }
        }
    }

    void StartPlayback()
    {
        playbackStarted = true;
        // The player started the song anyway: the pending return is cancelled
        // (its coroutine yields break on playbackStarted) and the HUD goes
        // back to the play view.
        SelectReturnPending = false;
        GManager.Start = true;
        GManager.played = true;
        playbackStartWallTime = Time.time;
        lastMediaProgressWallTime = Time.time;
        LastPlaybackSource = audioMode;
        stage = MediaStage.Playing;

        if (songVideo != null && songVideo.isPrepared)
        {
            // Issue #233: 映像プレーンはシーンで非アクティブなままなので、
            // 再生の直前に有効化しておく。準備済みの経路が
            // PrepareLocalVideo 以外 (シーンの VideoClip) でも同じになる。
            if (!picturePlaneActive)
            {
                WireVideoPicture();
                picturePlaneActive = VideoPicture.Activate(songVideo);
            }

            songVideo.Play();
            pictureStarted = true;
        }

        switch (audioMode)
        {
            case PlaybackSource.StreamCombined:
                // The VideoPlayer renders the sound of the progressive file into audioS.
                break;

            case PlaybackSource.StreamSeparate:
                if (streamAudioPlayer != null && streamAudioPlayer.isPrepared) streamAudioPlayer.Play();
                break;

            case PlaybackSource.BundledWave:
                audioS.clip = Music;
                audioS.time = 0f;
                audioS.Play();
                break;
        }

        mediaTime = CurrentMediaTime();
        GManager.StartTime = MediaTimeline.JudgeStartTime(Time.time, mediaTime);

        if (levelProbe != null) levelProbe.ResetPeak();

        StartText.SetActive(false);
        kakusi.SetActive(false);

        SongData playbackSong = PlaySessionSnapshot.ResolveSong(GManager.songID);
        Debug.Log($"[MusicManager] PLAYBACK_START song='{songName}' videoId='{(playbackSong != null ? playbackSong.videoId : "")}' " +
                  $"source={audioMode} mediaTime={mediaTime:F3} wallClock={DateTime.Now:yyyy-MM-ddTHH:mm:ss.fff} gameTime={Time.time:F3}");
    }

    void SwitchToBundledAudio(float atMediaTime)
    {
        if (streamAudioPlayer != null) streamAudioPlayer.Stop();
        if (streamAudioObject != null)
        {
            Destroy(streamAudioObject);
            streamAudioObject = null;
            streamAudioPlayer = null;
            streamAudioSource = null;
        }

        audioMode = PlaybackSource.BundledWave;
        audioS.clip = Music;
        audioS.time = Mathf.Max(0f, atMediaTime);
        audioS.volume = GManager.mainVolume;
        audioS.Play();

        Debug.Log($"[MusicManager] switched to the bundled wav at mediaTime={atMediaTime:F2}");
    }

    /// <summary>
    /// Issue #226: 実際のメディア時計 (壁時計フォールバックではない) が動いているか。
    /// duration を終了条件に使ってよいかの判定に使う。映像 (マスター) があれば真。
    /// </summary>
    bool HasRealMediaClock()
    {
        if (pictureStarted)
        {
            return true;
        }
        switch (audioMode)
        {
            case PlaybackSource.StreamSeparate:
                return streamAudioPlayer != null;
            case PlaybackSource.BundledWave:
                return audioS != null && audioS.clip != null;
            default:
                return false;
        }
    }

    /// <summary>
    /// Issue #226: メディアが終端に達したかを検出する。
    /// loop point イベント (OnSongVideoLoopReached) が一次情報で、
    /// ここは isPlaying の立ち下がりを拾う補完経路。メディアがなければ何もしない、
    /// (終端判定は FallbackSongEnd の壁時計側で行う)。
    /// </summary>
    void DetectMediaEnd()
    {
        if (pictureStarted && songVideo != null && songVideo.isPrepared)
        {
            if (songVideo.isPlaying)
            {
                videoSeenPlaying = true;
            }
            else if (videoSeenPlaying)
            {
                mediaEnded = true;
                Debug.Log("[MusicManager] the picture player stopped - treating it as the media end (issue #226)");
            }
            return;
        }

        if (audioMode == PlaybackSource.BundledWave && audioS != null && audioS.clip != null)
        {
            if (!audioS.isPlaying && audioS.time >= audioS.clip.length - 0.05f)
            {
                mediaEnded = true;
                Debug.Log("[MusicManager] the bundled wave reached its end (issue #226)");
            }
            return;
        }

        if (audioMode == PlaybackSource.StreamSeparate && streamAudioPlayer != null && streamAudioPlayer.isPrepared)
        {
            if (streamAudioPlayer.isPlaying)
            {
                streamAudioSeenPlaying = true;
            }
            else if (streamAudioSeenPlaying)
            {
                mediaEnded = true;
                Debug.Log("[MusicManager] the audio stream stopped - treating it as the media end (issue #226)");
            }
        }
    }

    /// <summary>
    /// Issue #226: 譜面上で最後のノーツが確定する時刻。
    /// タップは時刻 + BAD 窓、HOLD は終端 + GREAT 猶予 (押し続けた場合の期限)。
    /// 末尾の判定猶予を保証するための期限で、judge の時計 (JudgeTime) と比較する。
    /// </summary>
    float SettleDeadline()
    {
        if (!settleDeadlineKnown)
        {
            settleDeadlineKnown = true;
            settleDeadlineCache = ComputeSettleDeadline();
        }
        return settleDeadlineCache;
    }

    float ComputeSettleDeadline()
    {
        if (notesManager == null)
        {
            return 0f;
        }
        int count = Mathf.Min(notesManager.noteNum, notesManager.NoteType.Count);
        float last = 0f;
        for (int i = 0; i < count; i++)
        {
            float settle;
            if (notesManager.NoteType[i] == LongNote.TYPE_HOLD && i < notesManager.NotesEndTime.Count)
            {
                settle = notesManager.NotesEndTime[i] + LongNote.GREAT_WINDOW;
            }
            else
            {
                settle = notesManager.NotesTime[i] + LongNote.BAD_WINDOW;
            }
            if (settle > last)
            {
                last = settle;
            }
        }
        return last;
    }

    float CurrentMediaTime()
    {
        // The picture is the timing master: the chart was authored against the
        // YouTube recording, and in the merged file the sound comes out of the
        // very same file, so picture and sound cannot drift apart.
        // Only after the picture was started: a file that is prepared while the
        // song is already running must not throw the clock back to zero.
        if (pictureStarted && songVideo != null && songVideo.isPrepared) return (float)songVideo.time;

        switch (audioMode)
        {
            case PlaybackSource.StreamSeparate:
                if (streamAudioPlayer != null && streamAudioPlayer.isPrepared) return (float)streamAudioPlayer.time;
                break;

            case PlaybackSource.BundledWave:
                if (audioS != null && audioS.clip != null) return audioS.time;
                break;
        }

        // No media at all (the download failed, the file could not be opened):
        // the wall clock keeps the song running, so the game still reaches the
        // result screen and the player can retry or go back to the song list.
        return MediaTimeline.FallbackMediaTime(Time.time, playbackStartWallTime);
    }

    static string FirstUrl(string stdout)
    {
        if (string.IsNullOrEmpty(stdout)) return "";
        foreach (var line in stdout.Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("http", StringComparison.OrdinalIgnoreCase)) return trimmed;
        }
        return "";
    }

    static string TruncateUrl(string url)
    {
        if (string.IsNullOrEmpty(url)) return "";
        return url.Length <= 80 ? url : url.Substring(0, 80) + "...";
    }
}