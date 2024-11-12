using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SelectSceneManager : MonoBehaviour
{

    [SerializeField] private TextMeshProUGUI BackT;
    [SerializeField] private TextMeshProUGUI StartT;
    [SerializeField] private TextMeshProUGUI SongUpT;
    [SerializeField] private TextMeshProUGUI SongDownT;

    [SerializeField] private AudioClip sound;
    AudioSource sounds;
    [SerializeField] private TextMeshProUGUI SongNameOne;
    [SerializeField] private TextMeshProUGUI SongNameTwo;
    [SerializeField] private TextMeshProUGUI SongNameThree;
    [SerializeField] private TextMeshProUGUI SongNameFour;
    [SerializeField] private TextMeshProUGUI SongNameFive;

    [SerializeField] private TextMeshProUGUI MusicianOne;
    [SerializeField] private TextMeshProUGUI MusicianTwo;
    [SerializeField] private TextMeshProUGUI MusicianThree;
    [SerializeField] private TextMeshProUGUI MusicianFour;
    [SerializeField] private TextMeshProUGUI MusicianFive;

    [SerializeField] private TextMeshProUGUI BPMOne;
    [SerializeField] private TextMeshProUGUI BPMTwo;
    [SerializeField] private TextMeshProUGUI BPMThree;
    [SerializeField] private TextMeshProUGUI BPMFour;
    [SerializeField] private TextMeshProUGUI BPMFive;

    [SerializeField] private TextMeshProUGUI HitOne;
    [SerializeField] private TextMeshProUGUI HitTwo;
    [SerializeField] private TextMeshProUGUI HitThree;
    [SerializeField] private TextMeshProUGUI HitFour;
    [SerializeField] private TextMeshProUGUI HitFive;

    [SerializeField] private TextMeshProUGUI LevelOne;
    [SerializeField] private TextMeshProUGUI LevelTwo;
    [SerializeField] private TextMeshProUGUI LevelThree;
    [SerializeField] private TextMeshProUGUI LevelFour;
    [SerializeField] private TextMeshProUGUI LevelFive;

    [SerializeField] private TextMeshProUGUI noOne;
    [SerializeField] private TextMeshProUGUI noTwo;
    [SerializeField] private TextMeshProUGUI noThree;
    [SerializeField] private TextMeshProUGUI noFour;
    [SerializeField] private TextMeshProUGUI noFive;

    [SerializeField] private TextMeshProUGUI HiOne;
    [SerializeField] private TextMeshProUGUI HiTwo;
    [SerializeField] private TextMeshProUGUI HiThree;
    [SerializeField] private TextMeshProUGUI HiFour;
    [SerializeField] private TextMeshProUGUI HiFive;

    [SerializeField] private TextMeshProUGUI dottt;

    [SerializeField] private Image[] samune;
    
    public Image[] imageObjects;
    private string dottT;
    
    void Start()
    {
        sounds = GetComponent<AudioSource>();
        sounds.volume = GManager.effectVolume;
        if (GManager.songID == 1)//Total-1 Total 1 2 3
        {
            StartCoroutine(FetchThumbnail(GManager.SongURL[GManager.totalSong - 1], 0));
            StartCoroutine(FetchThumbnail(GManager.SongURL[GManager.totalSong], 1));
            StartCoroutine(FetchThumbnail(GManager.SongURL[1], 2));
            StartCoroutine(FetchThumbnail(GManager.SongURL[2], 3));
            StartCoroutine(FetchThumbnail(GManager.SongURL[3], 4));
        }
        else if (GManager.songID == 2)//Total 1 2 3 4
        {
            StartCoroutine(FetchThumbnail(GManager.SongURL[GManager.totalSong], 0));
            StartCoroutine(FetchThumbnail(GManager.SongURL[1], 1));
            StartCoroutine(FetchThumbnail(GManager.SongURL[2], 2));
            StartCoroutine(FetchThumbnail(GManager.SongURL[3], 3));
            StartCoroutine(FetchThumbnail(GManager.SongURL[4], 4));
        }
        else if (GManager.songID == GManager.totalSong)//Total-2 Total-1 Total 1 2
        {
            StartCoroutine(FetchThumbnail(GManager.SongURL[GManager.totalSong - 2], 0));
            StartCoroutine(FetchThumbnail(GManager.SongURL[GManager.totalSong - 1], 1));
            StartCoroutine(FetchThumbnail(GManager.SongURL[GManager.totalSong], 2));
            StartCoroutine(FetchThumbnail(GManager.SongURL[1], 3));
            StartCoroutine(FetchThumbnail(GManager.SongURL[2], 4));
        }
        else if (GManager.songID == GManager.totalSong - 1)//Total -3 Total-2 Total-1 Total 1
        {
            StartCoroutine(FetchThumbnail(GManager.SongURL[GManager.totalSong - 3], 0));
            StartCoroutine(FetchThumbnail(GManager.SongURL[GManager.totalSong - 2], 1));
            StartCoroutine(FetchThumbnail(GManager.SongURL[GManager.totalSong - 1], 2));
            StartCoroutine(FetchThumbnail(GManager.SongURL[GManager.totalSong], 3));
            StartCoroutine(FetchThumbnail(GManager.SongURL[1], 4));
        }
        else//Song-2 Song-1 Song Song+1 Song+2
        {
            StartCoroutine(FetchThumbnail(GManager.SongURL[GManager.songID - 2], 0));
            StartCoroutine(FetchThumbnail(GManager.SongURL[GManager.songID - 1], 1));
            StartCoroutine(FetchThumbnail(GManager.SongURL[GManager.songID], 2));
            StartCoroutine(FetchThumbnail(GManager.SongURL[GManager.songID + 1], 3));
            StartCoroutine(FetchThumbnail(GManager.SongURL[GManager.songID + 2], 4));
        }
    }
   
    void Update()
    {
        dottDis(GManager.songID);
        
        if(GManager.songID == 1)//Total-1 Total 1 2 3
        {
            SongNameOne.text = GManager.SongName[GManager.totalSong - 1];
            SongNameTwo.text = GManager.SongName[GManager.totalSong];
            SongNameThree.text = GManager.SongName[1];
            SongNameFour.text = GManager.SongName[2];
            SongNameFive.text = GManager.SongName[3];

            MusicianOne.text = GManager.Musician[GManager.totalSong - 1];
            MusicianTwo.text = GManager.Musician[GManager.totalSong];
            MusicianThree.text = GManager.Musician[1];
            MusicianFour.text = GManager.Musician[2];
            MusicianFive.text = GManager.Musician[3];

            BPMOne.text = GManager.SBPM[GManager.totalSong - 1].ToString();
            BPMTwo.text = GManager.SBPM[GManager.totalSong].ToString();
            BPMThree.text = GManager.SBPM[1].ToString();
            BPMFour.text = GManager.SBPM[2].ToString();
            BPMFive.text = GManager.SBPM[3].ToString();

            HitOne.text = GManager.Shit[GManager.totalSong - 1].ToString();
            HitTwo.text = GManager.Shit[GManager.totalSong].ToString();
            HitThree.text = GManager.Shit[1].ToString();
            HitFour.text = GManager.Shit[2].ToString();
            HitFive.text = GManager.Shit[3].ToString();

            LevelOne.text = GManager.Slevel[GManager.totalSong - 1].ToString();
            LevelTwo.text = GManager.Slevel[GManager.totalSong].ToString();
            LevelThree.text = GManager.Slevel[1].ToString();
            LevelFour.text = GManager.Slevel[2].ToString();
            LevelFive.text = GManager.Slevel[3].ToString();

            noOne.text = (GManager.totalSong - 1).ToString();
            noTwo.text = (GManager.totalSong).ToString();
            noThree.text = 1.ToString();
            noFour.text=2.ToString();
            noFive.text=3.ToString();

            HiOne.text= GManager.Hiscore[GManager.totalSong - 1].ToString();
            HiTwo.text = GManager.Hiscore[GManager.totalSong].ToString();
            HiThree.text = GManager.Hiscore[1].ToString();
            HiFour.text = GManager.Hiscore[2].ToString();
            HiFive.text = GManager.Hiscore[3].ToString();
        }
        else if(GManager.songID == 2)//Total 1 2 3 4
        {
            SongNameOne.text = GManager.SongName[GManager.totalSong];
            SongNameTwo.text = GManager.SongName[1];
            SongNameThree.text = GManager.SongName[2];
            SongNameFour.text = GManager.SongName[3];
            SongNameFive.text = GManager.SongName[4];

            MusicianOne.text = GManager.Musician[GManager.totalSong];
            MusicianTwo.text = GManager.Musician[1];
            MusicianThree.text = GManager.Musician[2];
            MusicianFour.text = GManager.Musician[3];
            MusicianFive.text = GManager.Musician[4];

            BPMOne.text = GManager.SBPM[GManager.totalSong].ToString();
            BPMTwo.text = GManager.SBPM[1].ToString();
            BPMThree.text = GManager.SBPM[2].ToString();
            BPMFour.text = GManager.SBPM[3].ToString();
            BPMFive.text = GManager.SBPM[4].ToString();

            HitOne.text = GManager.Shit[GManager.totalSong].ToString();
            HitTwo.text = GManager.Shit[1].ToString();
            HitThree.text = GManager.Shit[2].ToString();
            HitFour.text = GManager.Shit[3].ToString();
            HitFive.text = GManager.Shit[4].ToString();

            LevelOne.text = GManager.Slevel[GManager.totalSong].ToString();
            LevelTwo.text = GManager.Slevel[1].ToString();
            LevelThree.text = GManager.Slevel[2].ToString();
            LevelFour.text = GManager.Slevel[3].ToString();
            LevelFive.text = GManager.Slevel[4].ToString();

            noOne.text = (GManager.totalSong).ToString();
            noTwo.text = 1.ToString();
            noThree.text=2.ToString();
            noFour.text=3.ToString();
            noFive.text=4.ToString();

            HiOne.text = GManager.Hiscore[GManager.totalSong].ToString();
            HiTwo.text = GManager.Hiscore[1].ToString();
            HiThree.text = GManager.Hiscore[2].ToString();
            HiFour.text = GManager.Hiscore[3].ToString();
            HiFive.text = GManager.Hiscore[4].ToString();

        }
        else if(GManager.songID == GManager.totalSong)//Total-2 Total-1 Total 1 2
        {
            SongNameOne.text = GManager.SongName[GManager.totalSong - 2];
            SongNameTwo.text = GManager.SongName[GManager.totalSong - 1];
            SongNameThree.text = GManager.SongName[GManager.totalSong];
            SongNameFour.text = GManager.SongName[1];
            SongNameFive.text = GManager.SongName[2];

            MusicianOne.text = GManager.Musician[GManager.totalSong - 2];
            MusicianTwo.text = GManager.Musician[GManager.totalSong - 1];
            MusicianThree.text = GManager.Musician[GManager.totalSong];
            MusicianFour.text = GManager.Musician[1];
            MusicianFive.text = GManager.Musician[2];

            BPMOne.text = GManager.SBPM[GManager.totalSong - 2].ToString();
            BPMTwo.text = GManager.SBPM[GManager.totalSong - 1].ToString();
            BPMThree.text = GManager.SBPM[GManager.totalSong].ToString();
            BPMFour.text = GManager.SBPM[1].ToString();
            BPMFive.text = GManager.SBPM[2].ToString();

            HitOne.text = GManager.Shit[GManager.totalSong - 2].ToString();
            HitTwo.text = GManager.Shit[GManager.totalSong - 1].ToString();
            HitThree.text = GManager.Shit[GManager.totalSong].ToString();
            HitFour.text = GManager.Shit[1].ToString();
            HitFive.text = GManager.Shit[2].ToString();

            LevelOne.text = GManager.Slevel[GManager.totalSong - 2].ToString();
            LevelTwo.text = GManager.Slevel[GManager.totalSong - 1].ToString();
            LevelThree.text = GManager.Slevel[GManager.totalSong].ToString();
            LevelFour.text = GManager.Slevel[1].ToString();
            LevelFive.text = GManager.Slevel[2].ToString();

            noOne.text=(GManager.totalSong-2).ToString();
            noTwo.text=(GManager.totalSong-1).ToString();
            noThree.text=(GManager.totalSong).ToString();
            noFour.text=1.ToString();
            noFive.text=2.ToString();

            HiOne.text = GManager.Hiscore[GManager.totalSong - 2].ToString();
            HiTwo.text = GManager.Hiscore[GManager.totalSong - 1].ToString();
            HiThree.text = GManager.Hiscore[GManager.totalSong].ToString();
            HiFour.text = GManager.Hiscore[1].ToString();
            HiFive.text = GManager.Hiscore[2].ToString();


        }
        else if(GManager.songID == GManager.totalSong - 1)//Total -3 Total-2 Total-1 Total 1
        {
            SongNameOne.text = GManager.SongName[GManager.totalSong - 3];
            SongNameTwo.text = GManager.SongName[GManager.totalSong - 2];
            SongNameThree.text = GManager.SongName[GManager.totalSong - 1];
            SongNameFour.text = GManager.SongName[GManager.totalSong];
            SongNameFive.text = GManager.SongName[1];

            MusicianOne.text = GManager.Musician[GManager.totalSong - 3];
            MusicianTwo.text = GManager.Musician[GManager.totalSong - 2];
            MusicianThree.text = GManager.Musician[GManager.totalSong - 1];
            MusicianFour.text = GManager.Musician[GManager.totalSong];
            MusicianFive.text = GManager.Musician[1];

            BPMOne.text = GManager.SBPM[GManager.totalSong - 3].ToString();
            BPMTwo.text = GManager.SBPM[GManager.totalSong - 2].ToString();
            BPMThree.text = GManager.SBPM[GManager.totalSong - 1].ToString();
            BPMFour.text = GManager.SBPM[GManager.totalSong].ToString();
            BPMFive.text = GManager.SBPM[1].ToString();

            HitOne.text = GManager.Shit[GManager.totalSong - 3].ToString();
            HitTwo.text = GManager.Shit[GManager.totalSong - 2].ToString();
            HitThree.text = GManager.Shit[GManager.totalSong - 1].ToString();
            HitFour.text = GManager.Shit[GManager.totalSong].ToString();
            HitFive.text = GManager.Shit[1].ToString();

            LevelOne.text = GManager.Slevel[GManager.totalSong - 3].ToString();
            LevelTwo.text = GManager.Slevel[GManager.totalSong - 2].ToString();
            LevelThree.text = GManager.Slevel[GManager.totalSong - 1].ToString();
            LevelFour.text = GManager.Slevel[GManager.totalSong].ToString();
            LevelFive.text = GManager.Slevel[1].ToString();

            noOne.text=(GManager.totalSong-3).ToString();
            noTwo.text=(GManager.totalSong-2).ToString();
            noThree.text=(GManager.totalSong-1).ToString(); 
            noFour.text=(GManager.totalSong).ToString();
            noFive.text=1.ToString();

            HiOne.text = GManager.Hiscore[GManager.totalSong - 3].ToString();
            HiTwo.text = GManager.Hiscore[GManager.totalSong - 2].ToString();
            HiThree.text = GManager.Hiscore[GManager.totalSong - 1].ToString();
            HiFour.text = GManager.Hiscore[GManager.totalSong].ToString();
            HiFive.text = GManager.Hiscore[1].ToString();


        }
        else//Song-2 Song-1 Song Song+1 Song+2
        {
            SongNameOne.text = GManager.SongName[GManager.songID - 2];
            SongNameTwo.text = GManager.SongName[GManager.songID - 1];
            SongNameThree.text = GManager.SongName[GManager.songID];
            SongNameFour.text = GManager.SongName[GManager.songID + 1];
            SongNameFive.text = GManager.SongName[GManager.songID + 2];

            MusicianOne.text = GManager.Musician[GManager.songID - 2];
            MusicianTwo.text = GManager.Musician[GManager.songID - 1];
            MusicianThree.text = GManager.Musician[GManager.songID];
            MusicianFour.text = GManager.Musician[GManager.songID + 1];
            MusicianFive.text = GManager.Musician[GManager.songID + 2];

            BPMOne.text = GManager.SBPM[GManager.songID - 2].ToString();
            BPMTwo.text = GManager.SBPM[GManager.songID - 1].ToString();
            BPMThree.text = GManager.SBPM[GManager.songID].ToString();
            BPMFour.text = GManager.SBPM[GManager.songID + 1].ToString();
            BPMFive.text = GManager.SBPM[GManager.songID + 2].ToString();

            HitOne.text = GManager.Shit[GManager.songID - 2].ToString();
            HitTwo.text = GManager.Shit[GManager.songID - 1].ToString();
            HitThree.text = GManager.Shit[GManager.songID].ToString();
            HitFour.text = GManager.Shit[GManager.songID + 1].ToString();
            HitFive.text = GManager.Shit[GManager.songID + 2].ToString();

            LevelOne.text = GManager.Slevel[GManager.songID - 2].ToString();
            LevelTwo.text = GManager.Slevel[GManager.songID - 1].ToString();
            LevelThree.text = GManager.Slevel[GManager.songID].ToString();
            LevelFour.text = GManager.Slevel[GManager.songID + 1].ToString();
            LevelFive.text = GManager.Slevel[GManager.songID + 2].ToString();

            noOne.text=(GManager.songID-2).ToString();
            noTwo.text=(GManager.songID-1).ToString();
            noThree.text=(GManager.songID).ToString();
            noFour.text=(GManager.songID+1).ToString();
            noFive.text=(GManager.songID+2).ToString();

            HiOne.text = GManager.Hiscore[GManager.songID - 2].ToString();
            HiTwo.text = GManager.Hiscore[GManager.songID - 1].ToString();
            HiThree.text = GManager.Hiscore[GManager.songID].ToString();
            HiFour.text = GManager.Hiscore[GManager.songID + 1].ToString();
            HiFive.text = GManager.Hiscore[GManager.songID + 2].ToString();


        }
        if (Input.GetKeyDown(KeyCode.Space))
        {
            playSound();
            GameScene();
        }
        if (Input.GetKeyDown(KeyCode.F)||Input.GetKeyDown(KeyCode.D)|| Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow))
        {
            playSound();
            SongDown();
            if (GManager.songID == 1)//Total-1 Total 1 2 3
            {
                StartCoroutine(FetchThumbnail(GManager.SongURL[GManager.totalSong - 1], 0));
                StartCoroutine(FetchThumbnail(GManager.SongURL[GManager.totalSong], 1));
                StartCoroutine(FetchThumbnail(GManager.SongURL[1], 2));
                StartCoroutine(FetchThumbnail(GManager.SongURL[2], 3));
                StartCoroutine(FetchThumbnail(GManager.SongURL[3], 4));
            }
            else if (GManager.songID == 2)//Total 1 2 3 4
            {
                StartCoroutine(FetchThumbnail(GManager.SongURL[GManager.totalSong], 0));
                StartCoroutine(FetchThumbnail(GManager.SongURL[1], 1));
                StartCoroutine(FetchThumbnail(GManager.SongURL[2], 2));
                StartCoroutine(FetchThumbnail(GManager.SongURL[3], 3));
                StartCoroutine(FetchThumbnail(GManager.SongURL[4], 4));
            }
            else if (GManager.songID == GManager.totalSong)//Total-2 Total-1 Total 1 2
            {
                StartCoroutine(FetchThumbnail(GManager.SongURL[GManager.totalSong - 2], 0));
                StartCoroutine(FetchThumbnail(GManager.SongURL[GManager.totalSong - 1], 1));
                StartCoroutine(FetchThumbnail(GManager.SongURL[GManager.totalSong], 2));
                StartCoroutine(FetchThumbnail(GManager.SongURL[1], 3));
                StartCoroutine(FetchThumbnail(GManager.SongURL[2], 4));
            }
            else if (GManager.songID == GManager.totalSong - 1)//Total -3 Total-2 Total-1 Total 1
            {
                StartCoroutine(FetchThumbnail(GManager.SongURL[GManager.totalSong - 3], 0));
                StartCoroutine(FetchThumbnail(GManager.SongURL[GManager.totalSong - 2], 1));
                StartCoroutine(FetchThumbnail(GManager.SongURL[GManager.totalSong - 1], 2));
                StartCoroutine(FetchThumbnail(GManager.SongURL[GManager.totalSong], 3));
                StartCoroutine(FetchThumbnail(GManager.SongURL[1], 4));
            }
            else//Song-2 Song-1 Song Song+1 Song+2
            {
                StartCoroutine(FetchThumbnail(GManager.SongURL[GManager.songID - 2], 0));
                StartCoroutine(FetchThumbnail(GManager.SongURL[GManager.songID - 1], 1));
                StartCoroutine(FetchThumbnail(GManager.SongURL[GManager.songID], 2));
                StartCoroutine(FetchThumbnail(GManager.SongURL[GManager.songID + 1], 3));
                StartCoroutine(FetchThumbnail(GManager.SongURL[GManager.songID + 2], 4));
            }
        }
        if (Input.GetKeyDown(KeyCode.J)||Input.GetKeyDown(KeyCode.K) || Input.GetKeyDown(KeyCode.L) || Input.GetKeyDown(KeyCode.Semicolon)||Input.GetKeyDown(KeyCode.RightArrow))
        {
            playSound();
            SongUp();
            if (GManager.songID == 1)//Total-1 Total 1 2 3
            {
                StartCoroutine(FetchThumbnail(GManager.SongURL[GManager.totalSong - 1], 0));
                StartCoroutine(FetchThumbnail(GManager.SongURL[GManager.totalSong], 1));
                StartCoroutine(FetchThumbnail(GManager.SongURL[1], 2));
                StartCoroutine(FetchThumbnail(GManager.SongURL[2], 3));
                StartCoroutine(FetchThumbnail(GManager.SongURL[3], 4));
            }
            else if (GManager.songID == 2)//Total 1 2 3 4
            {
                StartCoroutine(FetchThumbnail(GManager.SongURL[GManager.totalSong], 0));
                StartCoroutine(FetchThumbnail(GManager.SongURL[1], 1));
                StartCoroutine(FetchThumbnail(GManager.SongURL[2], 2));
                StartCoroutine(FetchThumbnail(GManager.SongURL[3], 3));
                StartCoroutine(FetchThumbnail(GManager.SongURL[4], 4));
            }
            else if (GManager.songID == GManager.totalSong)//Total-2 Total-1 Total 1 2
            {
                StartCoroutine(FetchThumbnail(GManager.SongURL[GManager.totalSong - 2], 0));
                StartCoroutine(FetchThumbnail(GManager.SongURL[GManager.totalSong - 1], 1));
                StartCoroutine(FetchThumbnail(GManager.SongURL[GManager.totalSong], 2));
                StartCoroutine(FetchThumbnail(GManager.SongURL[1], 3));
                StartCoroutine(FetchThumbnail(GManager.SongURL[2], 4));
            }
            else if (GManager.songID == GManager.totalSong - 1)//Total -3 Total-2 Total-1 Total 1
            {
                StartCoroutine(FetchThumbnail(GManager.SongURL[GManager.totalSong - 3], 0));
                StartCoroutine(FetchThumbnail(GManager.SongURL[GManager.totalSong - 2], 1));
                StartCoroutine(FetchThumbnail(GManager.SongURL[GManager.totalSong - 1], 2));
                StartCoroutine(FetchThumbnail(GManager.SongURL[GManager.totalSong], 3));
                StartCoroutine(FetchThumbnail(GManager.SongURL[1], 4));
            }
            else//Song-2 Song-1 Song Song+1 Song+2
            {
                StartCoroutine(FetchThumbnail(GManager.SongURL[GManager.songID - 2], 0));
                StartCoroutine(FetchThumbnail(GManager.SongURL[GManager.songID - 1], 1));
                StartCoroutine(FetchThumbnail(GManager.SongURL[GManager.songID], 2));
                StartCoroutine(FetchThumbnail(GManager.SongURL[GManager.songID + 1], 3));
                StartCoroutine(FetchThumbnail(GManager.SongURL[GManager.songID + 2], 4));
            }
        }
        if(Input.GetKeyDown(KeyCode.Escape))
        {
            playSound();
            TitleScene();
        }
    }

    public void dottDis(int N)
    {
        if (N == 1)
        {
            dottT = "Åú";
            for (int i = 0; i < GManager.totalSong - 1; i++)
            {
                dottT += " Åõ";
            }
        }
        else if (N == GManager.totalSong)
        {
            dottT = "Åõ";
            for(int i = 0; i < GManager.totalSong - 2; i++)
            {
                dottT += " Åõ";
            }
            dottT += " Åú";
        }
        else
        {
            dottT = "Åõ";
            for (int i = 0; i < GManager.songID - 2; i++)
            {
                dottT += " Åõ";
            }
            dottT += " Åú";
            for (int i = 0; i < (GManager.totalSong-GManager.songID); i++)
            {
                dottT += " Åõ";
            }
        }
        dottt.text = dottT;
    }
    public void GameScene()
    {
        GManager.perfect = 0;
        GManager.great = 0;
        GManager.bad = 0;
        GManager.miss = 0;
        GManager.score = 0;
        GManager.combo = 0;
        GManager.maxScore = 0;
        GManager.ratioScore = 0;
      
        //SceneManager.LoadScene("GameScene");
        Initiate.Fade("GameScene", Color.black, 1.0f);

    }

    public void playSound()
    {
        sounds.PlayOneShot(sound);
    }

    public void TitleScene()
    {
        
        //SceneManager.LoadScene("TitleScene");
        Initiate.Fade("TitleScene", Color.black, 1.0f);

    }
    public void SongUp()
    {
        GManager.songID++;
        if (GManager.songID > GManager.totalSong)
        {
            GManager.songID = 1;
        }
        if (GManager.songID == 1)//Total-1 Total 1 2 3
        {
            StartCoroutine(FetchThumbnail(GManager.SongURL[GManager.totalSong - 1], 0));
            StartCoroutine(FetchThumbnail(GManager.SongURL[GManager.totalSong], 1));
            StartCoroutine(FetchThumbnail(GManager.SongURL[1], 2));
            StartCoroutine(FetchThumbnail(GManager.SongURL[2], 3));
            StartCoroutine(FetchThumbnail(GManager.SongURL[3], 4));
        }
        else if (GManager.songID == 2)//Total 1 2 3 4
        {
            StartCoroutine(FetchThumbnail(GManager.SongURL[GManager.totalSong], 0));
            StartCoroutine(FetchThumbnail(GManager.SongURL[1], 1));
            StartCoroutine(FetchThumbnail(GManager.SongURL[2], 2));
            StartCoroutine(FetchThumbnail(GManager.SongURL[3], 3));
            StartCoroutine(FetchThumbnail(GManager.SongURL[4], 4));
        }
        else if (GManager.songID == GManager.totalSong)//Total-2 Total-1 Total 1 2
        {
            StartCoroutine(FetchThumbnail(GManager.SongURL[GManager.totalSong - 2], 0));
            StartCoroutine(FetchThumbnail(GManager.SongURL[GManager.totalSong - 1], 1));
            StartCoroutine(FetchThumbnail(GManager.SongURL[GManager.totalSong], 2));
            StartCoroutine(FetchThumbnail(GManager.SongURL[1], 3));
            StartCoroutine(FetchThumbnail(GManager.SongURL[2], 4));
        }
        else if (GManager.songID == GManager.totalSong - 1)//Total -3 Total-2 Total-1 Total 1
        {
            StartCoroutine(FetchThumbnail(GManager.SongURL[GManager.totalSong - 3], 0));
            StartCoroutine(FetchThumbnail(GManager.SongURL[GManager.totalSong - 2], 1));
            StartCoroutine(FetchThumbnail(GManager.SongURL[GManager.totalSong - 1], 2));
            StartCoroutine(FetchThumbnail(GManager.SongURL[GManager.totalSong], 3));
            StartCoroutine(FetchThumbnail(GManager.SongURL[1], 4));
        }
        else//Song-2 Song-1 Song Song+1 Song+2
        {
            StartCoroutine(FetchThumbnail(GManager.SongURL[GManager.songID - 2], 0));
            StartCoroutine(FetchThumbnail(GManager.SongURL[GManager.songID - 1], 1));
            StartCoroutine(FetchThumbnail(GManager.SongURL[GManager.songID], 2));
            StartCoroutine(FetchThumbnail(GManager.SongURL[GManager.songID + 1], 3));
            StartCoroutine(FetchThumbnail(GManager.SongURL[GManager.songID + 2], 4));
        }
    }

    public void SongDown()
    {
        GManager.songID--;
        if(GManager.songID <= 0)
        {
            GManager.songID = GManager.totalSong;
        }
        if (GManager.songID == 1)//Total-1 Total 1 2 3
        {
            StartCoroutine(FetchThumbnail(GManager.SongURL[GManager.totalSong - 1], 0));
            StartCoroutine(FetchThumbnail(GManager.SongURL[GManager.totalSong], 1));
            StartCoroutine(FetchThumbnail(GManager.SongURL[1], 2));
            StartCoroutine(FetchThumbnail(GManager.SongURL[2], 3));
            StartCoroutine(FetchThumbnail(GManager.SongURL[3], 4));
        }
        else if (GManager.songID == 2)//Total 1 2 3 4
        {
            StartCoroutine(FetchThumbnail(GManager.SongURL[GManager.totalSong], 0));
            StartCoroutine(FetchThumbnail(GManager.SongURL[1], 1));
            StartCoroutine(FetchThumbnail(GManager.SongURL[2], 2));
            StartCoroutine(FetchThumbnail(GManager.SongURL[3], 3));
            StartCoroutine(FetchThumbnail(GManager.SongURL[4], 4));
        }
        else if (GManager.songID == GManager.totalSong)//Total-2 Total-1 Total 1 2
        {
            StartCoroutine(FetchThumbnail(GManager.SongURL[GManager.totalSong - 2], 0));
            StartCoroutine(FetchThumbnail(GManager.SongURL[GManager.totalSong - 1], 1));
            StartCoroutine(FetchThumbnail(GManager.SongURL[GManager.totalSong], 2));
            StartCoroutine(FetchThumbnail(GManager.SongURL[1], 3));
            StartCoroutine(FetchThumbnail(GManager.SongURL[2], 4));
        }
        else if (GManager.songID == GManager.totalSong - 1)//Total -3 Total-2 Total-1 Total 1
        {
            StartCoroutine(FetchThumbnail(GManager.SongURL[GManager.totalSong - 3], 0));
            StartCoroutine(FetchThumbnail(GManager.SongURL[GManager.totalSong - 2], 1));
            StartCoroutine(FetchThumbnail(GManager.SongURL[GManager.totalSong - 1], 2));
            StartCoroutine(FetchThumbnail(GManager.SongURL[GManager.totalSong], 3));
            StartCoroutine(FetchThumbnail(GManager.SongURL[1], 4));
        }
        else//Song-2 Song-1 Song Song+1 Song+2
        {
            StartCoroutine(FetchThumbnail(GManager.SongURL[GManager.songID - 2], 0));
            StartCoroutine(FetchThumbnail(GManager.SongURL[GManager.songID - 1], 1));
            StartCoroutine(FetchThumbnail(GManager.SongURL[GManager.songID], 2));
            StartCoroutine(FetchThumbnail(GManager.SongURL[GManager.songID + 1], 3));
            StartCoroutine(FetchThumbnail(GManager.SongURL[GManager.songID + 2], 4));
        }
    }

    
    public void BackTUp()
    {
        BackT.fontSize = 19;
    }
    public void BackTDown()
    {
        BackT.fontSize = 17;
       
    }
    public void StartTUp()
    {
        StartT.fontSize = 16.5f;
    }
    public void StartTDown()
    {
        StartT.fontSize = 15;
    }
    public void SongUpTUp()
    {
        SongUpT.fontSize = 33;
    }
    public void SongUpTDown()
    {
        SongUpT.fontSize = 30;
    }
    public void SongDownTUp()
    {
        SongDownT.fontSize = 33;
    }
    public void SongDownTDown()
    {
        SongDownT.fontSize = 30;
    }
    public void PanelOne()
    {
        GManager.songID -= 2;
        if (GManager.songID <= 1)
        {
            GManager.songID = GManager.totalSong-1;
        }
    }
    public void PanelTwo()
    {
        GManager.songID--;
        if (GManager.songID <= 0)
        {
            GManager.songID = GManager.totalSong;
        }
    }
    public void PanelFour()
    {
        GManager.songID++;
        if (GManager.songID > GManager.totalSong)
        {
            GManager.songID = 1;
        }
    }
    public void PanelFive()
    {
        GManager.songID += 2;
        if (GManager.songID > GManager.totalSong-1)
        {
            GManager.songID = 2;
        }
    }
    IEnumerator FetchThumbnail(string songURL, int index)
    {
        string url = "https://img.youtube.com/vi/" + songURL + "/0.jpg";
        UnityWebRequest request = UnityWebRequestTexture.GetTexture(url);
        yield return request.SendWebRequest();
        if (request.result == UnityWebRequest.Result.Success)
        {
            Texture2D texture = ((DownloadHandlerTexture)request.downloadHandler).texture;
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
            imageObjects[index].sprite = sprite;
        }
        else
        {
            Debug.Log(request.error);
        }
    }
}
