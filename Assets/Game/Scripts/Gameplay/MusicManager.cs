using System;
using System.Collections;
using System.IO;
using UnityEngine;
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

    enum AudioMode
    {
        None,           // no audio source at all
        StreamCombined, // one progressive file: the VideoPlayer renders sound and picture
        StreamSeparate, // picture from the video file, sound from a second (audio only) file
        Bundled,        // sound from the wav that ships with the chart
    }

    const float DirectUrlProbeTimeout = 10f;
    const float PrepareTimeout = 20f;
    const float FallbackStartTimeout = 30f;
    const string DirectUrlProbeKeyPrefix = "FlashBeat.DirectUrlProbed.";

    VideoPlayer songVideo;
    GameObject streamAudioObject;
    VideoPlayer streamAudioPlayer;
    AudioSource streamAudioSource;
    AudioLevelProbe levelProbe;

    bool mediaReady;
    bool playbackStarted;
    bool resultTriggered;
    bool fallbackChecked;
    bool videoPrepared;
    bool streamAudioPrepared;
    AudioMode audioMode = AudioMode.None;
    float mediaTime;
    float sceneStartTime;
    float lastStatusLogTime;
    float lastMediaTime;
    float stallSeconds;

    void Start()
    {
        waitback.SetActive(true);
        GameSceneManager.Menu = true;
        GManager.Start = false;
        GManager.step_time = 0;
        songName = GManager.Songs[GManager.songID].title;
        audioS = GetComponent<AudioSource>();
        Music = (AudioClip)Resources.Load("Musics/" + songName);
        GManager.played = false;
        GManager.nextNotes = 1;
        audioS.volume = GManager.mainVolume;
        StartText.SetActive(true);

        kakusi.SetActive(true);
        for (int i = 0; i < YPlayer.Length; i++)
        {
            YPlayer[i].SetActive(false);
        }
        YPlayer[GManager.songID].SetActive(true);

        songVideo = VPlayer[GManager.songID];
        if (songVideo == null && GManager.songID < YPlayer.Length)
        {
            songVideo = YPlayer[GManager.songID].GetComponent<VideoPlayer>();
        }

        sceneStartTime = Time.time;
        AttachLevelProbe();

        load.SetActive(true);
        StartCoroutine(PrepareMediaCoroutine());
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

    IEnumerator PrepareMediaCoroutine()
    {
        var song = GManager.Songs[GManager.songID];
        string videoId = song.videoId;

        // The Invidious player of the package cannot resolve anything today
        // (0/13 public instances answer) and its error handler would fight with
        // our own player, so it is removed from the song object.
        DisableInvidiousPlayer();

        // 1. What does YouTube offer for this video?
        var formatsTask = YouTubeStreamResolver.ListFormatsAsync(videoId);
        while (!formatsTask.IsCompleted) yield return null;

        var formatsOut = formatsTask.Result;
        var formats = YouTubeStreamResolver.ParseFormats(formatsOut.Ok ? formatsOut.Stdout : "");
        Debug.Log($"[MusicManager] yt-dlp -F videoId={videoId} formats={formats.Count} {(formatsOut.Ok ? "ok" : formatsOut.ErrorSummary)}");
        for (int i = 0; i < formats.Count; i++)
        {
            Debug.Log($"[MusicManager]   format {formats[i]}");
        }

        var plan = YouTubeMediaCache.SelectPlan(formats);
        Debug.Log($"[MusicManager] media plan '{songName}': {plan.Summary}");

        // 2. Direct URL probe (once per video): documents why the URL cannot be
        //    handed to Media Foundation.
        if (ShouldProbeDirectUrl(videoId))
        {
            yield return ProbeDirectUrl(videoId, plan.VideoSelector);
        }

        // 3. Download the media into the local cache.
        var videoTask = YouTubeMediaCache.EnsureFileAsync(videoId, plan.VideoSelector, YouTubeMediaCache.VideoFilePrefix);
        while (!videoTask.IsCompleted) yield return null;
        var videoFile = videoTask.Result;
        Debug.Log($"[MusicManager] video file: {(videoFile.Ok ? videoFile.Path + (videoFile.FromCache ? " (cache)" : " (downloaded)") : videoFile.Detail)}");

        MediaFileResult audioFile = null;
        if (plan.HasAudioFile)
        {
            var audioTask = YouTubeMediaCache.EnsureFileAsync(videoId, plan.AudioSelector, YouTubeMediaCache.AudioFilePrefix);
            while (!audioTask.IsCompleted) yield return null;
            audioFile = audioTask.Result;
            Debug.Log($"[MusicManager] audio file: {(audioFile.Ok ? audioFile.Path + (audioFile.FromCache ? " (cache)" : " (downloaded)") : audioFile.Detail)}");
        }

        // 4. Prepare the picture player.
        bool videoReady = false;
        if (videoFile.Ok && songVideo != null)
        {
            yield return PrepareLocalVideo(videoFile.Path, plan.Combined);
            videoReady = videoPrepared;
        }

        // 5. Prepare the sound player for the split (video only + audio only) case.
        bool audioReady = false;
        if (!plan.Combined && audioFile != null && audioFile.Ok)
        {
            yield return PrepareStreamAudio(audioFile.Path);
            audioReady = streamAudioPrepared;
        }

        // 6. Decide where the sound comes from.
        if (plan.Combined && videoReady)
        {
            audioMode = AudioMode.StreamCombined;
        }
        else if (audioReady)
        {
            audioMode = AudioMode.StreamSeparate;
        }
        else if (Music != null)
        {
            audioMode = AudioMode.Bundled;
        }
        else
        {
            audioMode = AudioMode.None;
        }

        mediaReady = true;
        load.SetActive(false);
        waitback.SetActive(false);

        Debug.Log($"[MusicManager] media ready: song='{songName}' audioMode={audioMode} video={(videoReady ? "ok" : "missing")} " +
                  $"(bundled wav {(Music != null ? "present" : "missing")}) - press Space to start");
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

        Debug.Log($"[MusicManager] VideoPlayer prepared={songVideo.isPrepared} file={Path.GetFileName(path)} after {waited:F1}s");
        videoPrepared = songVideo.isPrepared;
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
    }

    void Update()
    {
        if (!playbackStarted)
        {
            // Nothing may play before the player starts the song.
            if (songVideo != null && songVideo.isPlaying) songVideo.Pause();

            bool canStart = mediaReady || Time.time - sceneStartTime > FallbackStartTimeout;
            if (canStart && Input.GetKeyDown(KeyCode.Space))
            {
                StartPlayback();
            }
            return;
        }

        mediaTime = CurrentMediaTime();

        // The judgement clock follows the media clock, so a dropped frame or a
        // buffering stall cannot make the notes drift away from the song.
        GManager.StartTime = MediaTimeline.JudgeStartTime(Time.time, mediaTime);
        GManager.step_time = mediaTime;

        if (Mathf.Abs(mediaTime - lastMediaTime) < 0.0005f)
        {
            stallSeconds += Time.deltaTime;
            if (stallSeconds > 1f)
            {
                Debug.LogWarning($"[MusicManager] media stalled for {stallSeconds:F1}s at mediaTime={mediaTime:F2}");
                stallSeconds = 0f;
            }
        }
        else
        {
            stallSeconds = 0f;
        }
        lastMediaTime = mediaTime;

        // The split stream can end up silent (Unity's Windows decoder does not
        // accept every audio only container). Measure the mix and fall back to the
        // wav that ships with the chart when it stays quiet.
        if (audioMode == AudioMode.StreamSeparate && !fallbackChecked && mediaTime > 3f)
        {
            fallbackChecked = true;
            float rms = levelProbe != null ? levelProbe.LastRms : 0f;
            if (rms < 0.001f && Music != null)
            {
                Debug.LogWarning($"[MusicManager] stream audio is silent (rms={rms:F5}) - falling back to the bundled wav");
                SwitchToBundledAudio(mediaTime);
            }
        }

        if (Time.time - lastStatusLogTime >= 2f)
        {
            lastStatusLogTime = Time.time;
            float rms = levelProbe != null ? levelProbe.LastRms : 0f;
            float peak = levelProbe != null ? levelProbe.PeakRms : 0f;
            Debug.Log($"[MusicManager] STATUS mediaTime={mediaTime:F2} audioMode={audioMode} rms={rms:F4} peak={peak:F4} " +
                      $"perfect={GManager.perfect} great={GManager.great} bad={GManager.bad} miss={GManager.miss}");
        }

        if (!resultTriggered && MediaTimeline.HasSongEnded(mediaTime, GManager.Songs[GManager.songID].duration))
        {
            resultTriggered = true;
            audioS.volume -= 0.001f;
            Initiate.Fade("ResultScene", Color.black, 1.0f);
        }
    }

    void StartPlayback()
    {
        playbackStarted = true;
        GManager.Start = true;
        GManager.played = true;

        if (songVideo != null && songVideo.isPrepared) songVideo.Play();

        switch (audioMode)
        {
            case AudioMode.StreamCombined:
                // The VideoPlayer renders the sound of the progressive file into audioS.
                break;

            case AudioMode.StreamSeparate:
                if (streamAudioPlayer != null && streamAudioPlayer.isPrepared) streamAudioPlayer.Play();
                break;

            case AudioMode.Bundled:
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

        Debug.Log($"[MusicManager] PLAYBACK_START song='{songName}' videoId='{GManager.Songs[GManager.songID].videoId}' " +
                  $"audioMode={audioMode} mediaTime={mediaTime:F3} wallClock={DateTime.Now:yyyy-MM-ddTHH:mm:ss.fff} gameTime={Time.time:F3}");
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

        audioMode = AudioMode.Bundled;
        audioS.clip = Music;
        audioS.time = Mathf.Max(0f, atMediaTime);
        audioS.volume = GManager.mainVolume;
        audioS.Play();

        Debug.Log($"[MusicManager] switched to the bundled wav at mediaTime={atMediaTime:F2}");
    }

    float CurrentMediaTime()
    {
        switch (audioMode)
        {
            case AudioMode.StreamCombined:
                if (songVideo != null && songVideo.isPrepared) return (float)songVideo.time;
                break;

            case AudioMode.StreamSeparate:
                if (streamAudioPlayer != null && streamAudioPlayer.isPrepared) return (float)streamAudioPlayer.time;
                break;

            case AudioMode.Bundled:
                if (audioS != null && audioS.clip != null) return audioS.time;
                break;
        }

        if (songVideo != null && songVideo.isPrepared) return (float)songVideo.time;
        return GManager.step_time;
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
