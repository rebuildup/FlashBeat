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

    private AudioSource sounds;

    void Start()
    {
        // 保存済みの設定 (音量 / ノーツ速度 / 判定幅) を復元する (Issue #167)。
        // 以前は GManager.noteTiming-- と OptionSceneManager.TimingUp() で
        // 判定幅を既定値へ戻していたため、Option 画面での変更が失われていた。
        OptionSettings.Load();
        AudioListener.volume = GManager.mainVolume;
        sounds = GetComponent<AudioSource>();
        if (sounds != null) sounds.volume = GManager.effectVolume;

        // v5 UI (Issue #120): レガシー Canvas を隠し、コード生成の v5 画面に置き換える。
        // 入力は TitleView が処理し、このクラスの public メソッドをコールバックで呼ぶ。
        // キー割り当て (0.1.9) は TitleView が KeyActions 経由で参照する。
        var legacy = GameObject.Find("Canvas");
        if (legacy != null) legacy.SetActive(false);
        FlashBeat.UI.V5.TitleView.Build(SelectScene, OptionScene, EndGame, playSound);

        // タイトル画面でもキーガイド (Tab) を開けるようにする。
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
}
