using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Networking;
using UnityEngine.UI;

public class ResultSceneManager : MonoBehaviour
{
    [SerializeField] private AudioClip sound;
    AudioSource sounds;

    [SerializeField] private TextMeshProUGUI scoreText;
    [SerializeField] private TextMeshProUGUI perfectText;
    [SerializeField] private TextMeshProUGUI greatText;
    [SerializeField] private TextMeshProUGUI badText;
    [SerializeField] private TextMeshProUGUI missText;

    [SerializeField] private TextMeshProUGUI Assessment;

    [SerializeField] private TextMeshProUGUI SongName;
    [SerializeField] private TextMeshProUGUI Musician;
    [SerializeField] private TextMeshProUGUI BPM;
    [SerializeField] private TextMeshProUGUI TotalHit;
    [SerializeField] private TextMeshProUGUI Songlevel;
    [SerializeField] private TextMeshProUGUI SongN;
    [SerializeField] private TextMeshProUGUI Hiscore;

    

    [SerializeField] private TextMeshProUGUI OKT;
    [SerializeField] private TextMeshProUGUI RetryT;
    
    public string[] songURLs;
    public Image[] imageObjects;

    void Start()
    {
        GManager.BGMtime = 0f;   
        SongName.text = GManager.SongName[GManager.songID];
        Musician.text = GManager.Musician[GManager.songID];
        BPM.text = GManager.SBPM[GManager.songID].ToString();
        
       
        TotalHit.text = GManager.Shit[GManager.songID].ToString();
        Songlevel.text = GManager.Slevel[GManager.songID].ToString();
        SongN.text=GManager.songID.ToString();
        
        

        sounds = GetComponent<AudioSource>();
        sounds.volume = GManager.effectVolume;
        scoreText.text = GManager.score.ToString();
        perfectText.text = GManager.perfect.ToString();
        greatText.text = GManager.great.ToString();
        badText.text = GManager.bad.ToString();
        missText.text = GManager.miss.ToString();
        if(GManager.score == 1000000)
        {
            Assessment.text = "SS";
        }else if(GManager.score <1000000&&GManager.score >= 950000)
        {
            Assessment.text = "S";
        }else if(GManager.score <950000&&GManager.score >= 900000)
        {
            Assessment.text = "A";
        }else if(GManager.score <900000&&GManager.score >= 700000)
        {
            Assessment.text = "B";
        }else if(GManager.score < 700000)
        {
            Assessment.text = "C";
        }
        if (GManager.score >= GManager.Hiscore[GManager.songID])
        {
            GManager.Hiscore[GManager.songID] = GManager.score;
        }
        Hiscore.text = GManager.Hiscore[GManager.songID].ToString();
        StartCoroutine(FetchThumbnail(GManager.SongURL[GManager.songID], 0));
        
    }
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            SelectScene();
        }
        if (Input.GetKeyDown(KeyCode.R))
        {
            Retry();
        }
    }
    public void Retry()
    {
        GManager.perfect = 0;
        GManager.great = 0;
        GManager.bad = 0;
        GManager.miss = 0;
        GManager.score = 0;
        GManager.combo = 0;
        GManager.maxScore = 0;
        GManager.ratioScore = 0;
        
        Initiate.Fade("GameScene", Color.black, 1.0f);
       

    }
    public void SelectScene()
    {

        Initiate.Fade("SelectScene", Color.black, 1.0f);

    }
    public void playSound()
    {
        sounds.PlayOneShot(sound);
    }
    public void OKTUp()
    {
        OKT.fontSize = 55;
    }
    public void OKTDown()
    {
        OKT.fontSize = 50;
        
    }
    public void RetryTUp()
    {
        RetryT.fontSize = 55;
    }
    public void RetryTDown()
    {
        RetryT.fontSize = 50;
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
