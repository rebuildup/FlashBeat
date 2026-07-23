using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;
using UnityEngine.Networking;

public class YoutubePlayy : MonoBehaviour
{
    public string youtubeUrl;
    public VideoPlayer videoPlayer;
    // Start is called before the first frame update
    void Start()
    {
        StartCoroutine(PlayYouTubeVideo(youtubeUrl));
    }

    IEnumerator PlayYouTubeVideo(string url)
    {
        UnityWebRequest request = UnityWebRequest.Get(url);
        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError(request.error);
        }
        else
        {
            videoPlayer.url = url;
            videoPlayer.Play();
        }
    }
}
