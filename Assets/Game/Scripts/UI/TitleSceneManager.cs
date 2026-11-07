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
        RefreshKeyHints();
        KeyActions.Changed += RefreshKeyHints;
        CreateKeyGuide();
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

    void OnDestroy()
    {
        KeyActions.Changed -= RefreshKeyHints;
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
