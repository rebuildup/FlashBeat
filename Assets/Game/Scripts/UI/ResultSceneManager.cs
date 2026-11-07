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

    private FlashBeat.UI.V5.ResultView view;
    private int prevHiscore;

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
        SongName.text = GManager.ActiveSongs[GManager.songID].title;
        Musician.text = GManager.ActiveSongs[GManager.songID].musician;
        BPM.text = GManager.ActiveSongs[GManager.songID].bpm.ToString();
        
       
        TotalHit.text = GManager.ActiveSongs[GManager.songID].totalHits.ToString();
        Songlevel.text = GManager.ActiveSongs[GManager.songID].level.ToString();
        SongN.text=GManager.songID.ToString();
        
        

        sounds = GetComponent<AudioSource>();
        sounds.volume = GManager.effectVolume;
        scoreText.text = GManager.score.ToString();
        perfectText.text = GManager.perfect.ToString();
        greatText.text = GManager.great.ToString();
        badText.text = GManager.bad.ToString();
        missText.text = GManager.miss.ToString();
        Assessment.text = FlashBeat.UI.V5.ResultView.RankFor(GManager.score);
        int hiIndex = GManager.songID;
        if (hiIndex >= GManager.Hiscore.Length) hiIndex = 0; // remote songs beyond the fixed table keep slot 0 unused
        prevHiscore = GManager.Hiscore[hiIndex];
        if (GManager.score >= GManager.Hiscore[hiIndex])
        {
            GManager.Hiscore[hiIndex] = GManager.score;
        }
        Hiscore.text = GManager.Hiscore[hiIndex].ToString();
        StartCoroutine(FetchThumbnail(GManager.ActiveSongs[GManager.songID].videoId, 0));
        BuildV5View();

    }

    private void BuildV5View()
    {
        var legacy = GameObject.Find("Canvas");
        if (legacy != null)
        {
            legacy.SetActive(false);
        }

        var song = GManager.ActiveSongs[GManager.songID];
        var snapshot = new FlashBeat.UI.V5.ResultView.Snapshot
        {
            Score = GManager.score,
            Perfect = GManager.perfect,
            Great = GManager.great,
            Bad = GManager.bad,
            Miss = GManager.miss,
            Combo = GManager.combo,
            PrevHiscore = prevHiscore,
            Hiscore = GManager.Hiscore[GManager.songID < GManager.Hiscore.Length ? GManager.songID : 0],
            SongTitle = song.title,
            Musician = song.musician,
            Bpm = song.bpm,
            Level = song.level,
            TotalHits = song.totalHits
        };
        view = FlashBeat.UI.V5.ResultView.Build(snapshot, Retry, SelectScene);
    }

    void Update()
    {
        if (view != null)
        {
            return; // 入力は v5 ResultView が処理する (二重処理防止)
        }
        if (KeyActions.IsDown(KeyActions.KeyAction.Confirm))
        {
            SelectScene();
        }
        if (KeyActions.IsDown(KeyActions.KeyAction.Retry))
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
        GManager.maxCombo = 0;
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
            if (view != null)
            {
                view.SetBackdrop(texture);
            }
        }
        else
        {
            Debug.Log(request.error);
        }
    }

}
