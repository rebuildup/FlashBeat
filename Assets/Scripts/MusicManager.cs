using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Video;
using YoutubePlayer;

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
    

    void Start()
    {
        waitback.SetActive(true);
        GameSceneManager.Menu = true;
        GManager.Start = false;
        GManager.step_time = 0;
        songName = GManager.SongName[GManager.songID];
        audioS = GetComponent<AudioSource>();
        Music = (AudioClip)Resources.Load("Musics/" + songName);
        GManager.played = false;
        GManager.nextNotes = 1;
        audioS.volume = GManager.mainVolume;
        StartText.SetActive(true);
        
        
        kakusi.SetActive(true);
        for(int i = 0; i < GManager.SongURL.Length; i++)
        {
            YPlayer[i].SetActive(false);
        }
        YPlayer[GManager.songID].SetActive(true);
        VPlayer[GManager.songID].Stop();
        load.SetActive(true);
    }

    bool bb = false;
    void Update()
    {
        if (!bb)
        {
            VPlayer[GManager.songID].Pause();
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

                VPlayer[GManager.songID].Play();
                StartText.SetActive(false);
                kakusi.SetActive(false);
            }
        }
        
        if (GManager.played)
        {
            GManager.step_time += Time.deltaTime;

            
            if (GManager.step_time >= GManager.SongLong[GManager.songID]-1.0f)
            {
                audioS.volume -= 0.001f;
                Initiate.Fade("ResultScene", Color.black, 1.0f);

            }
        }
        
    }
}