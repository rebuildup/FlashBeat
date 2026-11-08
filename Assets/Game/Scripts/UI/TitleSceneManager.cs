using TMPro;
using UnityEngine;
using UnityEngine.Video;
using FlashBeat.UI.V6;

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

        // タイトルは v5 のコード生成 UI を使わず、**シーン配置の元の画面**を使う
        // (オーナー指示 2026-11-08: v5 を撤回して元の動画を再生するだけにする)。
        // TitleScene.unity には動画 (VideoPlayer "Screen") + ロゴ + メニューが
        // 置かれていて、それがそのままタイトルになる。
        var legacy = GameObject.Find("Canvas");
        if (legacy != null)
        {
            legacy.SetActive(true);
            // v5  Obi 之前 のレガシー Canvas には GraphicRaycaster が無く、
            // マウス操作がボタンに届かなかった (Issue #167)。元の画面を使うので足す。
            if (legacy.GetComponent<UnityEngine.UI.GraphicRaycaster>() == null)
            {
                legacy.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            }
        }

        // キーガイド (Tab) はタイトルでは出さない (左下に常時(keycap)が出るのを避ける)。
        V6UiMode.HideLegacyKeyGuide();
    }

    void Update()
    {
        // 元の画面 (ボタン) のキーボード操作。タイトルは動画が流れるだけの画面なので、
        // 最小限の入力だけ受け持つ。
        if (KeyActions.IsDown(KeyActions.KeyAction.Start) || Input.GetKeyDown(KeyCode.Return)
            || Input.GetKeyDown(KeyCode.Space))
        {
            SelectScene();
        }
        else if (KeyActions.IsDown(KeyActions.KeyAction.Option))
        {
            OptionScene();
        }
        else if (KeyActions.IsDown(KeyActions.KeyAction.Exit) || Input.GetKeyDown(KeyCode.Escape))
        {
            EndGame();
        }
    }

    /// <summary>旧キーガイド (タイトルでは使わない)。</summary>
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
