using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Video;
using YoutubePlayer.Components;
using FlashBeat.Gameplay;

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
    float count = 0;

    bool streamUrlResolved;
    string resolvedStreamUrl;


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
        for(int i = 0; i < GManager.Songs.Length; i++)
        {
            YPlayer[i].SetActive(false);
        }
        YPlayer[GManager.songID].SetActive(true);

        StartCoroutine(PrepareVideoCoroutine(GManager.songID));

        load.SetActive(true);
    }

    IEnumerator PrepareVideoCoroutine(int songIdx)
    {
        var ygo = YPlayer[songIdx];
        var vp = ygo.GetComponent<UnityEngine.Video.VideoPlayer>();
        var ivp = ygo.GetComponent<InvidiousVideoPlayer>();
        var videoId = ivp != null ? ivp.VideoId : null;

        if (vp != null && !string.IsNullOrEmpty(videoId))
        {
            var resolveTask = YouTubeStreamResolver.GetStreamUrlAsync(videoId);
            while (!resolveTask.IsCompleted) yield return null;

            if (resolveTask.Status == System.Threading.Tasks.TaskStatus.RanToCompletion
                && !string.IsNullOrEmpty(resolveTask.Result))
            {
                vp.source = VideoSource.Url;
                vp.url = resolveTask.Result;
                streamUrlResolved = true;
                resolvedStreamUrl = resolveTask.Result;
                Debug.Log($"[MusicManager] yt-dlp resolved videoId={videoId} for '{GManager.Songs[songIdx].title}' → {TruncateUrl(resolveTask.Result)}");

                if (ivp != null) ivp.enabled = false;
                vp.Prepare();
                float waited = 0f;
                while (!vp.isPrepared && waited < 30f) { yield return null; waited += Time.deltaTime; }
                if (!vp.isPrepared)
                {
                    Debug.LogError($"[MusicManager] VideoPlayer failed to prepare within 30s for videoId={videoId}.");
                }
                yield break;
            }

            var ex = resolveTask.Exception?.GetBaseException() ?? new System.Exception("yt-dlp resolver returned no URL");
            Debug.LogWarning($"[MusicManager] yt-dlp resolver failed for '{GManager.Songs[songIdx].title}': {ex.Message}. Falling back to InvidiousVideoPlayer.");
        }

        if (ivp != null)
        {
            var task = ivp.PrepareVideoAsync();
            while (!task.IsCompleted) yield return null;

            if (task.IsFaulted)
            {
                var inner = task.Exception?.GetBaseException() ?? new System.Exception("PrepareVideoAsync faulted");
                var instUrl = ivp.InvidiousInstance != null ? ivp.InvidiousInstance.InstanceUrl : "<none>";
                Debug.LogError($"[MusicManager] YouTube prepare failed for '{GManager.Songs[songIdx].title}' (videoId={ivp.VideoId}). " +
                               $"Instance='{instUrl}'. {inner.GetType().Name}: {inner.Message}");
            }
            else if (vp != null && string.IsNullOrEmpty(vp.url))
            {
                Debug.LogError($"[MusicManager] InvidiousVideoPlayer completed but VideoPlayer.url is empty (videoId={ivp.VideoId}).");
            }
        }
        else
        {
            VPlayer[songIdx].Stop();
        }
    }

    bool bb = false;
    void Update()
    {
        if (!bb)
        {
            var vp = VPlayer[GManager.songID];
            if (vp != null && vp.isPlaying) vp.Pause();
        }
        count += Time.deltaTime;
        if (count > 8f)
        {
            waitback.SetActive(false);
            load.SetActive(false);
            if (Input.GetKeyDown(KeyCode.Space) && !GManager.played)
            {
                waitback.SetActive(false);
                bb = true;
                GManager.Start = true;
                GManager.StartTime = Time.time;
                GManager.played = true;

                StartCoroutine(PlayVideoCoroutine(GManager.songID));
                StartText.SetActive(false);
                kakusi.SetActive(false);
            }
        }

        if (GManager.played)
        {
            GManager.step_time += Time.deltaTime;


            if (GManager.step_time >= GManager.Songs[GManager.songID].duration-1.0f)
            {
                audioS.volume -= 0.001f;
                Initiate.Fade("ResultScene", Color.black, 1.0f);

            }
        }

    }

    IEnumerator PlayVideoCoroutine(int songIdx)
    {
        if (streamUrlResolved)
        {
            var vp = YPlayer[songIdx].GetComponent<UnityEngine.Video.VideoPlayer>();
            if (vp != null && vp.isPrepared) vp.Play();
        }
        else
        {
            var ivp = YPlayer[songIdx].GetComponent<InvidiousVideoPlayer>();
            if (ivp != null)
            {
                var task = ivp.PlayVideoAsync();
                while (!task.IsCompleted) yield return null;
            }
            else
            {
                var vp = VPlayer[songIdx];
                if (vp != null) vp.Play();
            }
        }
    }

    static string TruncateUrl(string url)
    {
        if (string.IsNullOrEmpty(url)) return "";
        return url.Length <= 80 ? url : url.Substring(0, 80) + "...";
    }
}