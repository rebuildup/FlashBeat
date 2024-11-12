using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Experimental.Video;
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

    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            playSound();
            SelectScene();
        }
        if (Input.GetKeyDown(KeyCode.O))
        {
            playSound();
            OptionScene();
        }
        if (Input.GetKeyDown(KeyCode.E))
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
        sounds.PlayOneShot(sound);
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
