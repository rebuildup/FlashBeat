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
    private FlashBeat.UI.V6.Title.V6TitleView titleView;

    void Start()
    {
        // 保存済みの設定 (音量 / ノーツ速度 / 判定幅) を復元する (Issue #167)。
        // 以前は GManager.noteTiming-- と OptionSceneManager.TimingUp() で
        // 判定幅を既定値へ戻していたため、Option 画面での変更が失われていた。
        OptionSettings.Load();
        AudioListener.volume = GManager.mainVolume;
        sounds = GetComponent<AudioSource>();
        if (sounds != null) sounds.volume = GManager.effectVolume;

        // タイトルは v5 のコード生成 UI も、シーン配置の旧 UI も使わない。
        // (オーナー指示 2026-11-08: v5 を撤回して元の動画を再生するだけにする。)
        // 旧 UI は画面サイズに追従せず、左下に F2/F5 の案内が常時残るので、
        // Canvas を隠して動画 (VideoPlayer "Screen") だけを見せる。
        // 遷移は下の Update (Start / Option / Exit) が受け持つ。
        // タイトルは v6 のコード生成 UI を使う (オーナー指示 2026-11-08 の「動画を再生するだけ」から
        // 全シーン v6 化へ方針変更)。動画 (VideoPlayer "Screen") は消さない — v6 の UI はその上に重なる。
        var legacy = GameObject.Find("Canvas");
        if (legacy != null)
        {
            legacy.SetActive(true);
            if (legacy.GetComponent<UnityEngine.UI.GraphicRaycaster>() == null)
            {
                legacy.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            }
        }

        var canvasRect = FlashBeat.UI.V5.V5GameUi.NewScreenCanvas("V6 Title Canvas").GetComponent<RectTransform>();
        titleView = FlashBeat.UI.V6.Title.V6TitleView.Create(canvasRect, SelectScene, OptionScene, EndGame);
        FlashBeat.UI.V5.V5GameUi.ApplySingleInputModel(canvasRect.GetComponentInParent<Canvas>());

        // 旧 UI の残骸 (左下の F5/F2 案内) を隠す。更新ピル (UpdateNoticeCanvas) は残す。
        HideLegacyTitleDecorations();
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

    /// <summary>タイトルの旧装飾 (左下の F5/F2 案内) を隠す。更新ピルは残す。</summary>
    private void HideLegacyTitleDecorations()
    {
        var graphics = FindObjectsOfType<UnityEngine.UI.Graphic>();
        for (int i = 0; i < graphics.Length; i++)
        {
            var graphic = graphics[i];
            if (graphic == null) continue;
            var canvas = graphic.GetComponentInParent<Canvas>();
            if (canvas == null) continue;
            if (canvas.name == "UpdateNoticeCanvas") continue;
            if (canvas.name.StartsWith("V6")) continue;
            graphic.enabled = false;
        }

        V6UiMode.HideLegacyKeyGuide();
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
