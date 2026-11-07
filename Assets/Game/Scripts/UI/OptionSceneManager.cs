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
/// <summary>
/// ゲーム設定 (音量 / ノーツ速度 / 判定幅 / フラッシュ) の保存と復元 (Issue #167)。
///
/// 従来は設定値が GManager の静的変数にだけ存在し、プロセス終了で消えていた。
/// ここでは PlayerPrefs に保存し、起動時に Title 画面から復元する。
/// キーの保存キー名は他 Issue と衝突しないよう "FlashBeat." 接頭辞を付ける。
/// </summary>
public static class OptionSettings
{
    public const string MainVolumeKey = "FlashBeat.MainVolume";
    public const string EffectVolumeKey = "FlashBeat.EffectVolume";
    public const string BgmVolumeKey = "FlashBeat.BgmVolume";
    public const string NoteSpeedKey = "FlashBeat.NoteSpeed";
    public const string NoteTimingKey = "FlashBeat.NoteTiming";
    public const string FlashKey = "FlashBeat.Flash";
    public const string FlashTimingKey = "FlashBeat.FlashTiming";
    public const string FlashSpeedKey = "FlashBeat.FlashSpeed";

    /// <summary>ノーツ速度の範囲 (0 以下だとノーツが逆走する)。</summary>
    public const float NoteSpeedMin = 1f;

    /// <summary>ノーツ速度の上限。</summary>
    public const float NoteSpeedMax = 50f;

    /// <summary>判定幅 (ms) の下限。</summary>
    public const int NoteTimingMin = -10;

    /// <summary>判定幅 (ms) の上限。</summary>
    public const int NoteTimingMax = 10;

    /// <summary>現在の GManager の値を全部保存する。</summary>
    public static void Save()
    {
        PlayerPrefs.SetFloat(MainVolumeKey, GManager.mainVolume);
        PlayerPrefs.SetFloat(EffectVolumeKey, GManager.effectVolume);
        PlayerPrefs.SetFloat(BgmVolumeKey, GManager.BGMVolume);
        PlayerPrefs.SetFloat(NoteSpeedKey, GManager.noteSpeed);
        PlayerPrefs.SetInt(NoteTimingKey, GManager.noteTiming);
        PlayerPrefs.SetInt(FlashKey, GManager.Flash ? 1 : 0);
        PlayerPrefs.SetInt(FlashTimingKey, GManager.FlashT);
        PlayerPrefs.SetInt(FlashSpeedKey, GManager.FlashBG);
        PlayerPrefs.Save();
    }

    /// <summary>保存済みの値を GManager へ復元する (未保存なら何もしない)。</summary>
    public static void Load()
    {
        if (!PlayerPrefs.HasKey(MainVolumeKey)) { return; }

        GManager.mainVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(MainVolumeKey, GManager.mainVolume));
        GManager.effectVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(EffectVolumeKey, GManager.effectVolume));
        GManager.BGMVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(BgmVolumeKey, GManager.BGMVolume));
        GManager.noteSpeed = Mathf.Clamp(PlayerPrefs.GetFloat(NoteSpeedKey, GManager.noteSpeed),
            NoteSpeedMin, NoteSpeedMax);
        GManager.noteTiming = Mathf.Clamp(PlayerPrefs.GetInt(NoteTimingKey, GManager.noteTiming),
            NoteTimingMin, NoteTimingMax);
        GManager.Flash = PlayerPrefs.GetInt(FlashKey, GManager.Flash ? 1 : 0) != 0;
        GManager.FlashT = PlayerPrefs.GetInt(FlashTimingKey, GManager.FlashT);
        GManager.FlashBG = PlayerPrefs.GetInt(FlashSpeedKey, GManager.FlashBG);
    }
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
        // 保存済みの設定を復元してから UI へ反映する (Issue #167)。
        // 復元しないと Option 画面の値が毎回既定値になり、変更が反映されない。
        OptionSettings.Load();
        ApplyVolumes();

        if (volumeSlider != null)
        {
            volumeSlider.value = GManager.effectVolume;
            volumeSlider.onValueChanged.AddListener(value =>
            {
                if (audioMixer != null) audioMixer.volume = value;
                GManager.effectVolume = value;
                OptionSettings.Save();
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
                OptionSettings.Save();
            });
        }

        if (BGMSlider != null)
        {
            BGMSlider.value = GManager.BGMVolume;
            BGMSlider.onValueChanged.AddListener(value =>
            {
                if (audioBGM != null) audioBGM.volume = value;
                GManager.BGMVolume = value;
                OptionSettings.Save();
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

    /// <summary>
    /// 音量を実際の出力へ即時反映する (Issue #167)。
    /// 各所有者は起動時にしか音量を読まないため、値を変えた瞬間に反映する。
    /// </summary>
    public static void ApplyVolumes()
    {
        AudioListener.volume = GManager.mainVolume;
        var option = FindAnyObjectByType<OptionSceneManager>();
        if (option == null) { return; }

        if (option.audioMixer != null) { option.audioMixer.volume = GManager.effectVolume; }
        if (option.audioMixerMain != null) { option.audioMixerMain.volume = GManager.mainVolume; }
        if (option.audioBGM != null) { option.audioBGM.volume = GManager.BGMVolume; }
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
        GManager.noteTiming = Mathf.Clamp(GManager.noteTiming + 1, OptionSettings.NoteTimingMin, OptionSettings.NoteTimingMax);
        OptionSettings.Save();
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
        GManager.noteTiming = Mathf.Clamp(GManager.noteTiming - 1, OptionSettings.NoteTimingMin, OptionSettings.NoteTimingMax);
        OptionSettings.Save();
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
        // 0 以下だとノーツが逆に流れるため範囲を制限する (Issue #167)。
        GManager.noteSpeed = Mathf.Clamp(GManager.noteSpeed + 0.1f,
            OptionSettings.NoteSpeedMin, OptionSettings.NoteSpeedMax);
        OptionSettings.Save();
        Debug.Log("設定完了 noteSpeed=" + GManager.noteSpeed);
    }
    public void SpeedDown()
    {
        GManager.noteSpeed = Mathf.Clamp(GManager.noteSpeed - 0.1f,
            OptionSettings.NoteSpeedMin, OptionSettings.NoteSpeedMax);
        OptionSettings.Save();
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
