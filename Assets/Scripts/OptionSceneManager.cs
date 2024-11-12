using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using UnityEngine.SceneManagement;
using TMPro;
using Unity.VisualScripting;
using UnityEngine.Audio;
using UnityEngine.UI;


[Serializable]
public class SettingItemData //ノーツファイル概要のデータ
{
    public string name;
    public int maxBlock;
    public int BPM;
    public int offset;
    public Note[] notes;

}
public class OptionSceneManager : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI timingUp;
    [SerializeField] private TextMeshProUGUI timingDown;
    [SerializeField] private TextMeshProUGUI speedUp;
    [SerializeField] private TextMeshProUGUI speedDown;
    [SerializeField] private TextMeshProUGUI flashBool;
    [SerializeField] private TextMeshProUGUI flashBoolS;
    [SerializeField] private TextMeshProUGUI fspeedUp;
    [SerializeField] private TextMeshProUGUI fspeedDown;
    [SerializeField] private TextMeshProUGUI ftimingUp;
    [SerializeField] private TextMeshProUGUI ftimingDown;
    [SerializeField] private TextMeshProUGUI backT;

    [SerializeField] public TextMeshProUGUI speedNum;
    [SerializeField] public TextMeshProUGUI TimingText;
    [SerializeField] public TextMeshProUGUI flashSText;
    [SerializeField] public TextMeshProUGUI flashTText;
    [SerializeField] public GameObject FlashON;
    [SerializeField] public GameObject FlashOFF;

    [SerializeField] private AudioSource audioMixer;
    [SerializeField] public Slider volumeSlider;
    [SerializeField] private AudioSource audioMixerMain;
    [SerializeField] private Slider MainSlider;
    [SerializeField] private AudioSource audioBGM;
    [SerializeField] private Slider BGMSlider;
    private static string jsonPath;

    private static string jsonfileName;

    [SerializeField] private AudioClip sound;
    AudioSource sounds;

    void Start()
    {
        audioMixer = audioMixer.GetComponent<AudioSource>();
        audioMixerMain = audioMixerMain.GetComponent<AudioSource>();
        audioBGM = audioBGM.GetComponent<AudioSource>();
        if (GManager.Flash)
        {
            FlashON.SetActive(true);
            FlashOFF.SetActive(false);
        }
        else
        {
            FlashON.SetActive(false);
            FlashOFF.SetActive(true);
        }
        flashSText.text = (GManager.FlashBG).ToString();
        flashTText.text = (GManager.FlashT).ToString();
        volumeSlider.value = GManager.effectVolume;
        MainSlider.value = GManager.mainVolume;
        BGMSlider.value = GManager.BGMVolume;
        
    }
    /*
     [SerializeField] private AudioSource audioMixer;
    [SerializeField] public Slider volumeSlider;

    audioMixer = gameObject.GetComponent<AudioSource>();

    volumeSlider.onValueChanged.AddListener(value => this.audioMixer.volume = value);
     */
    void Update()
    {
        volumeSlider.onValueChanged.AddListener(value => this.audioMixer.volume = value);
        MainSlider.onValueChanged.AddListener(value=>this.audioMixerMain.volume = value);
        BGMSlider.onValueChanged.AddListener(value => this.audioBGM.volume = value);
        if (SceneManager.GetActiveScene().name == "OptionScene")
        {
            speedNum.text = GManager.noteSpeed.ToString("F1");
            TimingText.text = GManager.noteTiming.ToString();
        }
        GManager.mainVolume = MainSlider.value;

        GManager.effectVolume = volumeSlider.value;
        GManager.BGMVolume = BGMSlider.value;
        AudioListener.volume = GManager.mainVolume;
        audioBGM.volume = GManager.BGMVolume;
        // 音量を読み込む

    }
    private static OptionSceneManager instance;

    public static OptionSceneManager Instance
    {
        get{
            if (instance == null)
            {
                instance = FindObjectOfType<OptionSceneManager>();
            }
            return instance;
        }
    }

    public static void saveSettings(int I,SettingItemData newData)
    {
        jsonPath = "Assets/Resources/" + GManager.SongName[I] + ".json";
        jsonfileName = GManager.SongName[I].ToString();

        string jsonstr = JsonUtility.ToJson(newData);

        StreamWriter writer = new StreamWriter(jsonPath, false);

        writer.Write(jsonstr);

        writer.Flush();
        writer.Close();
    }

    public static SettingItemData LoadSettings(int I)
    {
        jsonPath = "Assets/Resources/" + GManager.SongName[I] + ".json";
        jsonfileName = GManager.SongName[I].ToString(); ;
        SettingItemData settingData = null;

        try
        {
            string json = Resources.Load<TextAsset>(jsonfileName).ToString();
            settingData = JsonUtility.FromJson<SettingItemData>(json);

        }
        catch(Exception e)
        {
            Debug.LogError("Error loading settings" + e.Message);
        }
        return settingData;
    }
    public void TitleScene()
    {
        
        //SceneManager.LoadScene("TitleScene");
        Initiate.Fade("TitleScene", Color.black, 1.0f);

    }

    public static void TimingUp()
    {
        GManager.noteTiming++;
        for (int i = 1; i <= GManager.totalSong; i++)
        {
            SettingItemData settingData = new SettingItemData(); // settingDataを宣言して初期化
            settingData = LoadSettings(i); // 既存のJSONデータを読み込む
            settingData.offset = GManager.noteTiming;
            saveSettings(i, settingData);
            Debug.Log("設定完了 offset=" + settingData.offset);
        }
    }
    public void TimingDown()
    {
        GManager.noteTiming--;
        for (int i = 1; i <= GManager.totalSong; i++)
        {
            SettingItemData settingData = new SettingItemData(); // settingDataを宣言して初期化
            settingData = LoadSettings(i); // 既存のJSONデータを読み込む
            settingData.offset = GManager.noteTiming;
            saveSettings(i, settingData);
            Debug.Log("設定完了 offset=" + settingData.offset);
        }
    }
    public void FlashTUp()
    {
        GManager.FlashT++;
        flashTText.text = (GManager.FlashT).ToString();
    }
    public void FlashTDown()
    {
        GManager.FlashT--;
        flashTText.text = (GManager.FlashT).ToString();
    }

    public void SpeedUp()
    {
        GManager.noteSpeed += 0.1f;
        Debug.Log("設定完了 noteSpeed=" + GManager.noteSpeed);
    }
    public void SpeedDown()
    {
        GManager.noteSpeed -= 0.1f;
        Debug.Log("設定完了 noteSpeed=" + GManager.noteSpeed);
    }
    public void playSound()
    {
        sounds = GetComponent<AudioSource>();
        sounds.PlayOneShot(sound);
    }

    public void FlashSwitch()
    {
        if (GManager.Flash)
        {
            FlashON.SetActive(false);
            FlashOFF.SetActive(true);
            GManager.Flash = false;
        }
        else
        {
            FlashON.SetActive(true);
            FlashOFF.SetActive(false);
            GManager.Flash = true;
        }
    }
    public void FlashSUp()
    {
        switch (GManager.FlashBG)
        {
            case 1:
                GManager.FlashBG = 2;
                break;
            case 2:
                GManager.FlashBG = 4;
                break;
            case 4:
                GManager.FlashBG = 8;
                break;
            case 8:
                GManager.FlashBG = 1;
                break;
            default:
                GManager.FlashBG = 0;
                break;
        }
        flashSText.text = (GManager.FlashBG).ToString();
    }
    public void FlashSDown()
    {
        switch (GManager.FlashBG)
        {
            case 1:
                GManager.FlashBG = 8;
                break;
            case 2:
                GManager.FlashBG = 1;
                break;
            case 4:
                GManager.FlashBG = 2;
                break;
            case 8:
                GManager.FlashBG = 4;
                break;
            default:
                GManager.FlashBG = 0;
                break;
        }
        flashSText.text = (GManager.FlashBG).ToString();
    }
    
    public void timingUpUp()
    {
        timingUp.fontSize = 9;
    }
    public void timingUpDown()
    {
        timingUp.fontSize = 7;
    }
    public void timingDownUp()
    {
        timingDown.fontSize = 9;
    }
    public void timingDownDown()
    {
        timingDown.fontSize = 7;
    }
    public void speedUpUp()
    {
        speedUp.fontSize = 9;
    }
    public void speedUpDown()
    {
        speedUp.fontSize = 7;
    }
    public void speedDownUp()
    {
        speedDown.fontSize = 9;
    }
    public void speedDownDown()
    {
        speedDown.fontSize = 7;
    }
    public void flashBoolUp()
    {
        flashBool.fontSize = 40;
    }
    public void flashBoolDown()
    {
        flashBool.fontSize = 36;
    }
    public void flashBoolSUp()
    {
        flashBoolS.fontSize = 40;
    }
    public void flashBoolSDown()
    {
        flashBoolS.fontSize = 36;
    }
    public void fspeedUpUp()
    {
        fspeedUp.fontSize = 9;
    }
    public void fspeedUpDown()
    {
        fspeedUp.fontSize = 7;
    }
    public void fspeedDownUp()
    {
        fspeedDown.fontSize = 9;
    }
    public void fspeedDownDown()
    {
        fspeedDown.fontSize = 7;
    }
    public void ftimingUpUp()
    {
        ftimingUp.fontSize = 9;
    }
    public void ftimingUpDown()
    {
        ftimingUp.fontSize = 7;
    }
    public void ftimingDownUp()
    {
        ftimingDown.fontSize = 9;
    }
    public void ftimingDownDown()
    {
        ftimingDown.fontSize = 7;
    }
    public void backTUp()
    {
        backT.fontSize = 17;
    }
    public void backTDown()
    {
        backT.fontSize = 15;
    }

}
