using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[Serializable]
public class TextData
{
    public float[] StartTime;
    public string[] Furigana;
    public string[] Mondai;
    public string[] romaji;
    public float[] EndTime;
    
}

public class GameSceneManager : MonoBehaviour
{
    [SerializeField] private GameObject MenuPanel;
    [SerializeField] private GameObject MenuButton;
    [SerializeField] private GameObject TextPanel;

    [SerializeField] private TextMeshProUGUI menuT;

    [SerializeField] private AudioClip sound;
    AudioSource sounds;
    [SerializeField] public Image gauge;

    public static List<float> SaveNotesTime = new List<float>();

    [SerializeField] private Animator _animator;

    // アニメーターコントローラーのレイヤー(通常は0)
    [SerializeField] private int _layer;

    // IsOpenフラグ(アニメーターコントローラー内で定義したフラグ)
    private static readonly int ParamIsOpen = Animator.StringToHash("IsOpen");

    // ダイアログは開いているかどうか
    public bool IsOpen => gameObject.activeSelf;

    // アニメーション中かどうか
    public bool IsTransition { get; private set; }

    public bool display;

    

    [SerializeField] private TextMeshProUGUI furiganaText;
    [SerializeField] private TextMeshProUGUI mondaiText;
    [SerializeField] private TextMeshProUGUI romajiText;

    private string songName;

    private TextData inputJson; // メンバ変数として TextData オブジェクトを宣言

    private int charNum;

    private string romajiT;

    public float timeElapsed = 0.0f;

   

    public static bool Menu;
    [SerializeField] private GameObject OnText;
    [SerializeField] private GameObject OffText;

    [SerializeField] public GameObject Flash;

    // 他の関数で使用するためのプロパティ
    public TextData InputJson
    {
        get { return inputJson; }
    }


    private void Start()
    {


        Flash.SetActive(true);
        gauge.fillAmount = 0;
        sounds = GetComponent<AudioSource>();
        sounds.volume = GManager.effectVolume;
        MenuPanel.SetActive(false);
        MenuButton.SetActive(true);
        if (Menu)
        {
            TextPanel.SetActive(true);
        }
        else
        {
            TextPanel.SetActive(false);
        }
        furiganaText.text = "";
        mondaiText.text = "";
        romajiText.text = "";
        timeElapsed = 0.0f;
        Flash.SetActive(false);
        songName = GManager.Songs[GManager.songID].title;
        Load(songName);

    }
    private void Load(string SongName)
    {
        string inputString = Resources.Load<TextAsset>(SongName + "_text").ToString();
        this.inputJson = JsonUtility.FromJson<TextData>(inputString);
    }
    public void OpenMenu()
    {
        MenuPanel.SetActive(true);
        
        
        MenuButton.SetActive(false);
        TextPanel.SetActive(false);
        if (Menu)
        {
            OnText.SetActive(true);
            OffText.SetActive(false);
        }
        else
        {
            OnText.SetActive(false);
            OffText.SetActive(true);
        }
        _animator.SetBool(ParamIsOpen, true);

        // アニメーション待機
        StartCoroutine(WaitAnimation("Shown"));

    }
    public void CloseMenu()
    {
        if (!IsOpen || IsTransition) return;
        MenuButton.SetActive(true);
        
        if (Menu)
        {
            TextPanel.SetActive(true);
        }
        else
        {
            TextPanel.SetActive(false);
        }
        if (!IsOpen || IsTransition) return;

        // IsOpenフラグをクリア
        _animator.SetBool(ParamIsOpen, false);
        

        // アニメーション待機し、終わったらパネル自体を非アクティブにする
        
        //StartCoroutine(WaitAnimation("Hidden", () => gameObject.SetActive(false)));
        StartCoroutine(WaitAnimation("Hidden", () =>MenuPanel.SetActive(false)));
    }
    public void playSound()
    {
        sounds.PlayOneShot(sound);
    }
    public void RetireGame()
    {
        Initiate.Fade("SelectScene", Color.black, 1.0f);
        
    }
    public void RetryGame()
    {
        GManager.ResetSession();
        Initiate.Fade("GameScene", Color.black, 1.0f);
    }
    public void GameEnd()
    {
        Initiate.Fade("ResultScene", Color.black, 1.0f);
    }


    void Update()
    {
        
        if (GManager.played)
        {
            if (GManager.Flash)
            {
                Flash.SetActive(true);
            }
            else
            {
                Flash.SetActive(false);
            }



            gauge.fillAmount = (GManager.score / 1000000f);
            
            timeElapsed += Time.deltaTime;
            
            
            for (int i = 0; i < inputJson.StartTime.Length; i++)
            {
                if (Mathf.Abs(timeElapsed - (inputJson.StartTime[i] + (GManager.FlashT * 0.01f))) < 0.05f)
                {
                    mondaiText.text = inputJson.Mondai[i];
                    display = true; // 表示フラグを立てる
                    charNum = 0;
                }

            }

        }
        if (romajiT != null && charNum < romajiT.Length)
        {
            if (Input.GetKeyDown(romajiT[charNum].ToString()))
            {
                Correct();
                if(charNum >= romajiT.Length)
                {
                    TextEnd();
                }
            }
            else if (Input.anyKeyDown)
            {
                UnCorrect();
            }
        }
        
        if (MenuPanel.activeSelf)
        {
            if (Input.GetKeyDown(KeyCode.R))
            {
                RetryGame();
            }
            if(Input.GetKeyDown(KeyCode.E))
            {
                GameEnd();
            }
            if (Input.GetKeyDown(KeyCode.Backspace))
            {
                RetireGame();
            }
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                CloseMenu();
            }
            if (Input.GetKeyDown(KeyCode.T))
            {
                SwitchPanel();
            }
        }
        else
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                OpenMenu();
            }
        }
        
        

    }
    private void Correct()
    {
        charNum++;
    }
    private void UnCorrect()
    {
    }
    public void TextEnd()
    {
        // テキストを緑色に変更
        romajiText.text = "<color=#41FF00>" + romajiT + "</color>";
    }

    public void TextPanelOn()
    {
        OnText.SetActive(true);
        OffText.SetActive(false);
        Menu = true;
    }

    public void TextPanelOff()
    {
        OnText.SetActive(false);
        OffText.SetActive(true);
        Menu = false;
    }
    public void SwitchPanel()
    {
        if (Menu)
        {
            TextPanelOff();
        }
        else
        {
            TextPanelOn();
        }
    }
    public void MenuTUp()
    {
        menuT.fontSize = 89;
        
    }
    public void MenuTDown()
    {
        menuT.fontSize = 86;
    }
    private IEnumerator WaitAnimation(string stateName, UnityAction onCompleted = null)
    {
        IsTransition = true;

        yield return new WaitUntil(() =>
        {
            // ステートが変化し、アニメーションが終了するまでループ
            var state = _animator.GetCurrentAnimatorStateInfo(_layer);
            return state.IsName(stateName) && state.normalizedTime >= 1;
        });

        IsTransition = false;

        onCompleted?.Invoke();
    }
}
