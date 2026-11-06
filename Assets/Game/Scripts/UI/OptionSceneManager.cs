using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using UnityEngine.SceneManagement;
using TMPro;
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
        if (volumeSlider != null)
        {
            volumeSlider.value = GManager.effectVolume;
            volumeSlider.onValueChanged.AddListener(value =>
            {
                if (audioMixer != null) audioMixer.volume = value;
                GManager.effectVolume = value;
            });
        }

        if (MainSlider != null)
        {
            MainSlider.value = GManager.mainVolume;
            MainSlider.onValueChanged.AddListener(value =>
            {
                if (audioMixerMain != null) audioMixerMain.volume = value;
                GManager.mainVolume = value;
                AudioListener.volume = value;
            });
        }

        if (BGMSlider != null)
        {
            BGMSlider.value = GManager.BGMVolume;
            BGMSlider.onValueChanged.AddListener(value =>
            {
                if (audioBGM != null) audioBGM.volume = value;
                GManager.BGMVolume = value;
            });
        }

        if (GManager.Flash)
        {
            if (FlashON != null) FlashON.SetActive(true);
            if (FlashOFF != null) FlashOFF.SetActive(false);
        }
        else
        {
            if (FlashON != null) FlashON.SetActive(false);
            if (FlashOFF != null) FlashOFF.SetActive(true);
        }

        if (flashSText != null) flashSText.text = GManager.FlashBG.ToString();
        if (flashTText != null) flashTText.text = GManager.FlashT.ToString();

        // v5 UI (Issue #120): レガシー Canvas を隠し、コード生成の v5 画面に置き換える。
        // 入力は OptionView が処理し、このクラスの既存メソッドをコールバックで呼ぶ。
        var legacy = GameObject.Find("Canvas");
        if (legacy != null) legacy.SetActive(false);
        FlashBeat.UI.V5.OptionView.Build(
            TitleScene, SpeedUp, SpeedDown, OptionSceneManager.TimingUp, TimingDown);
    }

    void Update()
    {
        if (SceneManager.GetActiveScene().name == "OptionScene")
        {
            if (speedNum != null) speedNum.text = GManager.noteSpeed.ToString("F1");
            if (TimingText != null) TimingText.text = GManager.noteTiming.ToString();
        }
    }
    private static OptionSceneManager instance;

    public static OptionSceneManager Instance
    {
        get{
            if (instance == null)
            {
                instance = FindAnyObjectByType<OptionSceneManager>();
            }
            return instance;
        }
    }

    /// <summary>設定を書き込む先。
    /// 編集環境 (プロジェクト直下で起動) は従来どおり Assets/Resources へ書き出す。
    /// ビルド版の実行ディレクトリには Assets が無く、書き出しが例外となって
    /// タイトル画面の処理が中断していたため、ユーザー領域 (persistentDataPath) を使う。</summary>
    public static string SettingsDirectory()
    {
        const string editorDirectory = "Assets/Resources";
        try
        {
            if (Directory.Exists(editorDirectory)) { return editorDirectory; }
        }
        catch (Exception) { }
        return Path.Combine(Application.persistentDataPath, "settings");
    }

    /// <summary>設定ファイル名 (曲名から作出的安全な名前)。</summary>
    public static string SettingsFileName(string title)
    {
        var name = string.IsNullOrEmpty(title) ? "song" : title;
        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(invalid, '_');
        }
        return name + ".json";
    }

    public static void saveSettings(int I,SettingItemData newData)
    {
        if (newData == null) { return; }
        if (I < 0 || I >= GManager.ActiveSongs.Length) { return; }

        jsonfileName = GManager.ActiveSongs[I].title.ToString();
        try
        {
            var directory = SettingsDirectory();
            if (!Directory.Exists(directory)) { Directory.CreateDirectory(directory); }
            jsonPath = Path.Combine(directory, SettingsFileName(jsonfileName));

            string jsonstr = JsonUtility.ToJson(newData);

            using (var writer = new StreamWriter(jsonPath, false))
            {
                writer.Write(jsonstr);
                writer.Flush();
            }
        }
        catch (Exception e)
        {
            // 設定の保存に失敗してもゲームは止めない (音量や速度の設定だけ失われる)。
            Debug.LogWarning("Error saving settings: " + e.Message);
        }
    }

    public static SettingItemData LoadSettings(int I)
    {
        if (I < 0 || I >= GManager.ActiveSongs.Length) { return new SettingItemData(); }

        jsonfileName = GManager.ActiveSongs[I].title.ToString(); ;
        SettingItemData settingData = null;

        try
        {
            // 1. ビルド版が保存した設定 (ユーザー領域) を優先する。
            var savedPath = Path.Combine(SettingsDirectory(), SettingsFileName(jsonfileName));
            if (File.Exists(savedPath))
            {
                settingData = JsonUtility.FromJson<SettingItemData>(File.ReadAllText(savedPath));
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("Error loading saved settings: " + e.Message);
        }

        if (settingData != null) { return settingData; }

        try
        {
            // 2. 編集環境の譜面ファイル (Resources) から読む。
            var asset = Resources.Load<TextAsset>(jsonfileName);
            if (asset != null)
            {
                settingData = JsonUtility.FromJson<SettingItemData>(asset.ToString());
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("Error loading settings: " + e.Message);
        }

        // 3. どちらにも無ければ既定値を返す (null を返すと呼び出し側で NullReference になる)。
        return settingData ?? new SettingItemData { name = jsonfileName };
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
