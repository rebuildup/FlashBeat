using TMPro;
using UnityEngine;
using UnityEngine.Video;

/// <summary>
/// Title scene (design Unit 01 "Title").
///
/// The screen itself is built as uGUI objects inside TitleScene.unity
/// (see design/flashbeat-ui.pen); this script only owns the input handling,
/// the scene transitions and the catalog status chip.
/// </summary>
public class TitleSceneManager : MonoBehaviour
{
    [SerializeField] private AudioClip sound;
    [SerializeField] private VideoPlayer Screen;

    [SerializeField] private TextMeshProUGUI catalogStatus;

    private AudioSource sounds;

    void Start()
    {
        AudioListener.volume = GManager.mainVolume;
        sounds = GetComponent<AudioSource>();
        GManager.noteTiming--;
        OptionSceneManager.TimingUp();
        if (sounds != null) sounds.volume = GManager.effectVolume;
        RefreshCatalogStatus();
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
        Initiate.Fade("SelectScene", Color.black, 1.0f);
    }

    public void OptionScene()
    {
        Initiate.Fade("OptionScene", Color.black, 1.0f);
    }

    public void playSound()
    {
        if (sounds == null) sounds = GetComponent<AudioSource>();
        if (sounds == null) return;
        if (sound != null)
        {
            sounds.PlayOneShot(sound);
        }
        else
        {
            Debug.LogWarning("[TitleSceneManager] sound AudioClip is not assigned in Inspector.");
        }
    }

    /// <summary>
    /// Catalog status chip (design "カタログ状態を cloud アイコン付きで表示").
    /// The cloud catalog is fetched on the select screen, so on the title
    /// screen the chip reports the built-in list until a remote catalog has
    /// been applied.
    /// </summary>
    private void RefreshCatalogStatus()
    {
        if (catalogStatus == null) return;
        catalogStatus.text = GManager.HasRemoteCatalog
            ? $"同期済み · {GManager.totalSong}曲"
            : $"ローカル · {GManager.totalSong}曲";
    }
}
