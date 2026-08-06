using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class TipingSceneManager : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI furiganaText;
    [SerializeField] private TextMeshProUGUI mondaiText;
    [SerializeField] private TextMeshProUGUI romajiText;

    [SerializeField] private GameObject ResultPanel;

    AudioSource audioS;
    [SerializeField] AudioClip Music;

    private bool MusicPlayed;
    private int charNum;

    private string romajiT;
    private bool TPlayed;
    private bool display;
    private int nowText;
    private float MusicCount;

    private TextData inputJson; // メンバ変数として TextData オブジェクトを宣言

    public TextData InputJson
    {
        get { return inputJson; }
    }
    // Start is called before the first frame update
    void Start()
    {
        ResultPanel.SetActive(false);
        TPlayed = false;
        MusicPlayed = false;
        nowText = 0;
        Load(GManager.Songs[GManager.songID].title);
        furiganaText.text = GManager.Songs[GManager.songID].title;
        mondaiText.text = "Space to Start";
        romajiText.text = "";
        display = false;
        audioS = GetComponent<AudioSource>();
        Music = (AudioClip)Resources.Load("Musics/" + GManager.Songs[GManager.songID].title);
        audioS.volume = GManager.mainVolume;
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            TPlayed = true;
        }
        //音楽が終わったら繰り返し
      
        else
        {
            MusicCount += Time.deltaTime;
            if (MusicCount >= GManager.Songs[GManager.songID].duration)
            {
                MusicPlayed = false;
            }
        }

        if (TPlayed)
        {
            if (!MusicPlayed)
            {
                audioS.PlayOneShot(Music);
                MusicPlayed = true;
            }
            else
            {
                MusicCount += Time.deltaTime;
                if (MusicCount >= GManager.Songs[GManager.songID].duration)
                {
                    MusicPlayed = false;
                }
            }
            if (!display)
            {
                TextTrue();
                display = true;
            }
            else
            {
                if (romajiT != null && charNum < romajiT.Length)
                {
                    if (Input.GetKeyDown(romajiT[charNum].ToString()))
                    {
                        Correct();
                    }
                    else if (Input.anyKeyDown)
                    {
                        UnCorrect();
                    }
                }
            }
        }
    }
    private void Load(string SongName)
    {/*
        string inputString = Resources.Load<TextAsset>(SongName+"_text").ToString();
        TextData inputJson = JsonUtility.FromJson<TextData>(inputString);*/
        string inputString = Resources.Load<TextAsset>(SongName + "_text").ToString();
        this.inputJson = JsonUtility.FromJson<TextData>(inputString);
    }
    private void Correct()
    {
        
        if (charNum >= romajiT.Length-1)
        {
            charNum = 0;
            TextEnd();
        }
        else
        {
            charNum++;
            romajiText.text = "<color=#6A6A6A>" + romajiT.Substring(0, charNum) + "</color>" + romajiT.Substring(charNum);
        }
        
    }
    private void UnCorrect()
    {
        romajiText.text = "<color=#6A6A6A>" + romajiT.Substring(0, charNum) + "</color>" + "<color=#FF0000>" + romajiT[charNum] + "</color>" + "<color=#6A6A6A>" + romajiT.Substring(charNum + 1) + "</color>";
    }
    public void TextEnd()
    {
        nowText++;
        if (nowText> inputJson.EndTime.Length)
        {
            ResultOpen();
        }
        
        display = false;
    }
    private void TextTrue()
    {
        furiganaText.text = inputJson.Furigana[nowText];
        mondaiText.text = inputJson.Mondai[nowText];
        romajiT = inputJson.romaji[nowText];
        romajiText.text = romajiT;
        
    }
    private void ResultOpen()
    {
        ResultPanel.SetActive(true);
    }
}
