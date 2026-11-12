using System;
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
///
/// v6 タイトル (Issue #239) の統合担当:
///  - v6 (V6TitleView.Enabled) のときは v6 のタイトルを新しい Canvas に描き、
///    旧 (シーン配置) の Canvas は非アクティブにして二重表示を止める。
///    動画 (VideoPlayer "Screen") はシーン直下の 3D オブジェクトなので、
///    Canvas を隠しても動画と背景は映り続ける。
///  - v6 を無効にしたときは旧タイトルをそのまま動かす (シーン配置のボタンの
///    onClick は TitleSceneManager.SelectScene / OptionScene / EndGame に結線済み)。
///  - キー入力は TitleSceneManager.Update だけが受け持つ (二重処理防止)。
///    v5 の TitleView (コード生成) はどこからも作られていないので、
///    旧 UI 側の入力ハンドラは存在しない。
/// </summary>
public class TitleSceneManager : MonoBehaviour
{
    [SerializeField] private AudioClip sound;
    [SerializeField] private VideoPlayer Screen;

    private AudioSource sounds;
    private FlashBeat.UI.V6.Title.V6TitleView titleView;

    // History / Ranking タブの操作が設定されているか (描画の切り替えに使う)。
    private bool historyAvailable;
    private bool rankingAvailable;

    void Start()
    {
        // Issue #239: History / Ranking タブの遷移先を設定する。
        // タブは操作 (フック) が設定されているときだけ描かれる。
        // 遷移先は Select シーンなので、要求は PendingEntry 経由で渡す。
        FlashBeat.UI.V6.Title.V6TitleLinks.HistoryRequested = OnHistoryRequested;
        FlashBeat.UI.V6.Title.V6TitleLinks.RankingRequested = OnRankingRequested;

        // 背景映像 (design QJtTv の bg-anim3) を出す (#372)。
        EnsureTitleVideoRenders();

        // 保存済み設定 (音量 / ノーツ速度 / 判定幅) の復元 (Issue #167)。
        // 以前は GManager.noteTiming-- と OptionSceneManager.TimingUp() で
        // 判定幅を既定値へ戻していたため、Option 画面での変更が失われていた。
        OptionSettings.Load();
        // Display 設定 (Key Guide / Lyrics) の復元 (issue-162 D-1)。
        // ここまで静的フィールドだけで、再起動すると選んだ値が消えていた。
        FlashBeat.UI.V5.PlaySession.Load();
        AudioListener.volume = GManager.mainVolume;
        sounds = GetComponent<AudioSource>();
        if (sounds != null) sounds.volume = GManager.effectVolume;

        if (FlashBeat.UI.V6.Title.V6TitleView.Enabled)
        {
            BuildV6Title();
        }
        else
        {
            BuildLegacyTitle();
        }
    }

    /// <summary>
    /// 背景映像を実際に描画させる (#372)。TitleScene の VideoPlayer は
    /// MaterialOverride なのに targetMaterialRenderer が未指定のため、そのままでは
    /// 映像がどこにも描画されず、旧 v5 の Background (ベージュ) が残る。
    /// design QJtTv の背景は bg-anim3 (Resources/Videos/背景アニメーション3.mp4) なので、
    /// OpeningSceneManager と同じく CameraFarPlane に切り替え、手前の旧背景メッシュを隠す。
    /// </summary>
    private void EnsureTitleVideoRenders()
    {
        var video = FindFirstObjectByType<VideoPlayer>();
        if (video == null)
        {
            return;
        }

        // design QJtTv の背景は cover。16:9 の映像を 16:10 のディスプレイへ
        // そのまま出すと上下に帯が残るので、はみ出し側を切って全面に描く。
        video.aspectRatio = VideoAspectRatio.FitOutside;

        bool unusable =
            video.renderMode == VideoRenderMode.APIOnly ||
            (video.renderMode == VideoRenderMode.MaterialOverride &&
             video.targetMaterialRenderer == null) ||
            (video.renderMode == VideoRenderMode.CameraFarPlane &&
             video.targetCamera == null);
        if (!unusable)
        {
            return;
        }

        var camera = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();
        if (camera == null)
        {
            return;
        }

        video.renderMode = VideoRenderMode.CameraFarPlane;
        video.targetCamera = camera;

        // 映像の手前に来る旧スクリーン メッシュと旧背景 (ベージュ) を隠す。
        var screen = video.GetComponentInChildren<MeshRenderer>();
        if (screen != null)
        {
            screen.enabled = false;
        }

        var backdrop = GameObject.Find("BackGround");
        if (backdrop != null)
        {
            var renderer = backdrop.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.enabled = false;
            }
        }

        Debug.Log("[TitleSceneManager] event=video-render-mode renderMode=CameraFarPlane");
    }

    /// <summary>
    /// v6 のタイトルを組み立てる。旧 (シーン配置) の UI と同じ画面には出さない。
    /// </summary>
    private void BuildV6Title()
    {
        // 旧 UI (シーン配置の Canvas) を隠す。動画 (VideoPlayer "Screen") と
        // 背景 (BackGround) は 3D オブジェクトなので影響を受けない。
        var legacy = FindLegacyCanvas();
        if (legacy != null)
        {
            legacy.SetActive(false);
        }

        var canvasRect = FlashBeat.UI.V5.V5GameUi.NewScreenCanvas("V6 Title Canvas").GetComponent<RectTransform>();
        titleView = FlashBeat.UI.V6.Title.V6TitleView.Create(canvasRect, SelectScene, OptionScene, EndGame);
        RefreshPanelHandlers();

        // v6 ビュー自身がキー操作を担うので、Unity の自動ナビゲーションは切る。
        FlashBeat.UI.V5.V5GameUi.ApplySingleInputModel(canvasRect.GetComponentInParent<Canvas>());

        // 旧 UI の残骸 (左下の F5/F2 案内) を隠す。更新ピル (UpdateNoticeCanvas) は残す。
        HideLegacyTitleDecorations();
    }

    /// <summary>v6 を無効にしたときは旧 (シーン配置) のタイトルをそのまま動かす。</summary>
    private void BuildLegacyTitle()
    {
        var legacy = FindLegacyCanvas();
        if (legacy != null)
        {
            legacy.SetActive(true);
        }
    }

    /// <summary>
    /// シーン配置の旧 Canvas ("Canvas") を探す。非アクティブな Canvas も含めて
    /// 探す (隠したあとに GameObject.Find で見つからなくなるため)。
    /// </summary>
    private static GameObject FindLegacyCanvas()
    {
        var canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < canvases.Length; i++)
        {
            if (canvases[i] != null && canvases[i].gameObject.name == "Canvas")
            {
                return canvases[i].gameObject;
            }
        }

        return null;
    }

    /// <summary>
    /// History / Ranking タブの操作を反映する。フック (V6TitleLinks) が
    /// 設定されている間だけタブを描く。設定が変わったら描き直す。
    /// </summary>
    private void RefreshPanelHandlers()
    {
        bool history = FlashBeat.UI.V6.Title.V6TitleLinks.HistoryRequested != null;
        bool ranking = FlashBeat.UI.V6.Title.V6TitleLinks.RankingRequested != null
            || FlashBeat.UI.V5.TitleLinks.RankingRequested != null;
        if (history == historyAvailable && ranking == rankingAvailable)
        {
            return;
        }

        historyAvailable = history;
        rankingAvailable = ranking;
        if (titleView != null)
        {
            titleView.SetPanelHandlers(history ? (Action)InvokeHistory : null,
                ranking ? (Action)InvokeRanking : null);
        }
    }

    private void InvokeHistory()
    {
        var hook = FlashBeat.UI.V6.Title.V6TitleLinks.HistoryRequested;
        if (hook != null)
        {
            hook();
        }
    }

    private void InvokeRanking()
    {
        var hook = FlashBeat.UI.V6.Title.V6TitleLinks.RankingRequested;
        if (hook != null)
        {
            hook();
            return;
        }

        var legacyHook = FlashBeat.UI.V5.TitleLinks.RankingRequested;
        if (legacyHook != null)
        {
            legacyHook();
        }
    }

    // --- History / Ranking タブからの遷移 (Issue #239) ---

    /// <summary>
    /// Title の History タブ / H キー。要求を PendingEntry に積んでから
    /// SelectScene へ遷移する (SelectSceneManager が Start で消費して History を開く)。
    /// </summary>
    private void OnHistoryRequested()
    {
        SetV6Entry(FlashBeat.UI.V6.Title.V6TitleLinks.EntryScreen.History);
    }

    /// <summary>Ranking タブ / R キー。同上 (Ranking を開く)。</summary>
    private void OnRankingRequested()
    {
        SetV6Entry(FlashBeat.UI.V6.Title.V6TitleLinks.EntryScreen.Ranking);
    }

    private void SetV6Entry(FlashBeat.UI.V6.Title.V6TitleLinks.EntryScreen screen)
    {
        FlashBeat.UI.V6.Title.V6TitleLinks.PendingEntry = screen;
        SelectScene();
    }

    void OnDestroy()
    {
        // 静的フックはシーンをまたいで残るので、このシーンのインスタンスの
        // 分だけ解除する (dangling delegate の防止)。
        if (FlashBeat.UI.V6.Title.V6TitleLinks.HistoryRequested == OnHistoryRequested)
        {
            FlashBeat.UI.V6.Title.V6TitleLinks.HistoryRequested = null;
        }
        if (FlashBeat.UI.V6.Title.V6TitleLinks.RankingRequested == OnRankingRequested)
        {
            FlashBeat.UI.V6.Title.V6TitleLinks.RankingRequested = null;
        }
    }

    void Update()
    {
        if (titleView == null)
        {
            HandleLegacyInput();
            return;
        }

        // 履歴 / ランキングのフックが後から設定された場合にタブを出す。
        RefreshPanelHandlers();

        // オーバーレイ (キー設定 / 更新通知) が開いた直後のフレームは
        // 背面(scene) 側の入力を捨てる (二重処理防止)。
        if (!FlashBeat.UI.SceneInputGate.InputAllowed)
        {
            return;
        }

        // タブの選択 (↑↓ / W / S)。
        // GetKeyDownOrInjected: 入力注入 (自動テスト / Web 版) のキーも拾う。
        if (KeyBindings.GetKeyDownOrInjected(KeyCode.UpArrow) || KeyBindings.GetKeyDownOrInjected(KeyCode.W))
        {
            titleView.MoveSelection(-1);
            return;
        }

        if (KeyBindings.GetKeyDownOrInjected(KeyCode.DownArrow) || KeyBindings.GetKeyDownOrInjected(KeyCode.S))
        {
            titleView.MoveSelection(1);
            return;
        }

        // History / Ranking タブ (KeyCap H / R — V6TitleLogic の固定キー)。
        if (historyAvailable && KeyBindings.GetKeyDownOrInjected(FlashBeat.UI.V6.Title.V6TitleLogic.HistoryTabKeyCode))
        {
            InvokeHistory();
            return;
        }
        if (rankingAvailable && KeyBindings.GetKeyDownOrInjected(FlashBeat.UI.V6.Title.V6TitleLogic.RankingTabKeyCode))
        {
            InvokeRanking();
            return;
        }

        // 決定 (実キーの割り当てに従う: KeyActions.Start)。
        if (KeyActions.IsDown(KeyActions.KeyAction.Start))
        {
            titleView.ActivateSelected();
            return;
        }

        if (KeyActions.IsDown(KeyActions.KeyAction.Option))
        {
            OptionScene();
            return;
        }

        if (KeyActions.IsDown(KeyActions.KeyAction.Exit) || KeyBindings.GetKeyDownOrInjected(KeyCode.Escape))
        {
            EndGame();
        }
    }

    /// <summary>
    /// v6 を無効にしたときの入力 (旧タイトル用)。決定は選曲へ、
    /// Option は設定へ、終了 (Exit / Esc) は強制終了。
    /// </summary>
    private void HandleLegacyInput()
    {
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
