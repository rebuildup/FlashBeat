using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;

public class VideoTime : MonoBehaviour
{
    public VideoPlayer videoPlayer;
    [SerializeField] AudioSource audioBGM;
    // Start is called before the first frame update
    void Start()
    {
        
        videoPlayer.time = GManager.BGMtime;
    }

    // Update is called once per frame
    void Update()
    {
        audioBGM.volume = GManager.BGMVolume;
        GManager.BGMtime = (float)videoPlayer.time;
    }
}
