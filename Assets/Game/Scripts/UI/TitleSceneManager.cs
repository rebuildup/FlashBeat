using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Video;

public class TitleSceneManager : MonoBehaviour
{
    [SerializeField] private AudioClip sound;
    AudioSource sounds;

    [SerializeField] private TextMeshProUGUI StartT;
    [SerializeField] private TextMeshProUGUI OptionT;
    [SerializeField] private TextMeshProUGUI ExitT;

    [SerializeField] private VideoPlayer Screen;
    

    void Start()
    {
        AudioListener.volume = GManager.mainVolume;
        sounds = GetComponent<AudioSource>();
        GManager.noteTiming--;
        OptionSceneManager.TimingUp();
        sounds.volume = GManager.effectVolume;
        //this.Screen.SetDirectAudioVolume = true;
        RefreshKeyHints();
        KeyActions.Changed += RefreshKeyHints;
        CreateKeyGuide();
    }

    /// <summary>タイトル画面でもキーガイド (Tab) を開けるようにする。</summary>
    private void CreateKeyGuide()
    {
        KeyGuideUI.Create(null);
        var canvas = FindObjectOfType<Canvas>();
        if (canvas == null) { return; }

        var labelGO = new GameObject("KeyGuideHint", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelGO.transform.SetParent(canvas.transform, false);
        var rect = labelGO.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(0f, 16f);
        rect.sizeDelta = new Vector2(700f, 36f);

        var tmp = labelGO.GetComponent<TextMeshProUGUI>();
        tmp.text = "Tab : キーガイド (キーの一覧)";
        tmp.fontSize = 22f;
        tmp.color = new Color(1f, 1f, 1f, 0.85f);
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
        var font = JapaneseFontFinder.FindJapaneseFont();
        if (font != null) { tmp.font = font; }
    }

    void OnDestroy()
    {
        KeyActions.Changed -= RefreshKeyHints;
    }

    /// <summary>シーン内の『push space』などのキー名ヘルプを現在の割り当てに合わせて更新する。</summary>
    private void RefreshKeyHints()
    {
        var hints = FindObjectsOfType<TextMeshProUGUI>();
        for (int i = 0; i < hints.Length; i++)
        {
            string text = hints[i].text;
            if (string.IsNullOrEmpty(text)) continue;
            string lower = text.ToLower();
            KeyActions.KeyAction action;
            if (lower.Contains("space")) { action = KeyActions.KeyAction.Start; }
            else if (lower == "push o") { action = KeyActions.KeyAction.Option; }
            else if (lower == "push e") { action = KeyActions.KeyAction.Exit; }
            else continue;
            hints[i].text = "push " + KeyConfigUI.GetKeysText(KeyActions.Get(action));
        }
    }

    void Update()
    {
        if (KeyActions.IsDown(KeyActions.KeyAction.Start))
        {
            playSound();
            SelectScene();
        }
        if (KeyActions.IsDown(KeyActions.KeyAction.Option))
        {
            playSound();
            OptionScene();
        }
        if (KeyActions.IsDown(KeyActions.KeyAction.Exit))
        {
            playSound();
            EndGame();
        }
    }
    public void EndGame()
    {
        
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;//ゲームプレイ終了
#else
    Application.Quit();//ゲームプレイ終了
#endif
    }

    public void SelectScene()
    {
        
        //SceneManager.LoadScene("SelectScene");
        Initiate.Fade("SelectScene", Color.black, 1.0f);

    }

    public void OptionScene()
    {

        
        //SceneManager.LoadScene("OptionScene");
        Initiate.Fade("OptionScene", Color.black, 1.0f);

    }
    public void playSound()
    {
        sounds = GetComponent<AudioSource>();
        if (sound != null)
        {
            sounds.PlayOneShot(sound);
        }
        else
        {
            Debug.LogWarning("[TitleSceneManager] sound AudioClip is not assigned in Inspector.");
        }
    }
    public void StartB()
    {
        StartT.fontSize = 13;
    }
    public void StartS()
    {
        StartT.fontSize = 12;
    }
    public void OptionB()
    {
        OptionT.fontSize = 13;
    }
    public void OptionS()
    {
        OptionT.fontSize = 12;
    }
    public void ExitB()
    {
        ExitT.fontSize = 13;
    }
    public void ExitS()
    {
        ExitT.fontSize = 12;
    }
   
}
