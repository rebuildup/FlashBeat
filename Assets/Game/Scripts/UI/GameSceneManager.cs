using System;
using System.Collections;
using System.Collections.Generic;
using FlashBeat.UI;
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

    /// <summary>歌詞の表示区間 (メディアタイムライン上)。</summary>
    private List<LyricLine> lyricLines = new List<LyricLine>();

    /// <summary>いま表示している歌詞の行 (LyricsTimeline.IndexAt の結果)。</summary>
    private int currentLyricIndex = -1;

    /// <summary>ポーズメニューを開いている間、歌詞パネルを出さない。</summary>
    private bool menuVisible;

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
        // v6 の HUD にはキーガイド行があるので、旧パネルは作らない (v5 のときは従来どおり)。
        if (FlashBeat.UI.V6.V6UiMode.GameHud)
        {
            FlashBeat.UI.V6.V6UiMode.HideLegacyKeyGuide();
        }
        else
        {
            KeyGuideUI.Create(transform);
        }
        Flash.SetActive(true);
        gauge.fillAmount = 0;
        sounds = GetComponent<AudioSource>();
        sounds.volume = GManager.effectVolume;
        MenuPanel.SetActive(false);
        MenuButton.SetActive(true);
        ClearLyricText();
        ApplyTextPanelVisibility();
        Flash.SetActive(false);
        songName = GManager.ActiveSongs[GManager.songID].title;
        Load(songName);

    }
    private void Load(string SongName)
    {
        this.inputJson = LoadTextData(SongName);
        // 譜面の歌詞を「動画と同じタイムライン上の表示区間」へ変換する
        lyricLines = LyricsTimeline.Build(
            inputJson.StartTime,
            inputJson.EndTime,
            inputJson.Mondai,
            inputJson.Furigana);
    }

    TextData LoadTextData(string songName)
    {
        var mergedAsset = Resources.Load<TextAsset>(songName);
        if (mergedAsset != null)
        {
            var chart = JsonUtility.FromJson<NoteEditor.DTO.MusicDTO.EditData>(mergedAsset.text);
            if (chart != null && chart.lyrics != null && chart.lyrics.mondai != null && chart.lyrics.mondai.Length > 0)
            {
                return new TextData
                {
                    StartTime = chart.lyrics.startTime ?? new float[0],
                    Furigana  = chart.lyrics.furigana  ?? new string[0],
                    Mondai    = chart.lyrics.mondai    ?? new string[0],
                    romaji    = chart.lyrics.romaji    ?? new string[0],
                    EndTime   = chart.lyrics.endTime   ?? new float[0],
                };
            }
        }

        var legacyAsset = Resources.Load<TextAsset>(songName + "_text");
        if (legacyAsset != null)
        {
            return JsonUtility.FromJson<TextData>(legacyAsset.text);
        }

        // Cloud Catalog songs (#17) have no local lyrics resource; that is
        // expected, so only a warning. Fixed-catalog songs keep the error.
        SongData currentSong = GManager.ActiveSongs[GManager.songID];
        if (currentSong != null && currentSong.isRemote)
        {
            Debug.LogWarning($"[GameSceneManager] no text data for remote song '{currentSong.remoteId}' - lyrics are empty");
        }
        else
        {
            Debug.LogError($"[GameSceneManager] No text data for {songName}");
        }
        return new TextData
        {
            StartTime = new float[0],
            Furigana  = new string[0],
            Mondai    = new string[0],
            romaji    = new string[0],
            EndTime   = new float[0],
        };
    }
    public void OpenMenu()
    {
        MenuPanel.SetActive(true);


        MenuButton.SetActive(false);
        // メニューを出している間は歌詞パネルを出さない (閉じたら元へ戻す)
        menuVisible = true;
        ApplyTextPanelVisibility();
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

        if (!IsOpen || IsTransition) return;

        // IsOpenフラグをクリア
        _animator.SetBool(ParamIsOpen, false);


        // アニメーション待機し、終わったらパネル自体を非アクティブにして歌詞を戻す

        StartCoroutine(WaitAnimation("Hidden", () =>
        {
            MenuPanel.SetActive(false);
            menuVisible = false;
            ApplyTextPanelVisibility();
        }));
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

    /// <summary>
    /// v5 のポーズメニューが有効かどうか (Issue #167)。
    /// true のときは Esc を PauseMenuView だけが担う。
    ///
    /// Issue #400: v6 のポーズが有効な GameScene では v5 の PauseMenuView を
    /// **生成しない** (GameHudController.Start)。生成しないものを毎フレーム
    /// シーン全体から探すのは無駄なので、その構成では探索せず false を返す。
    /// それ以外では従来どおり「見つかるまで探し、見つかったら保持」する。
    /// </summary>
    private static FlashBeat.UI.V5.PauseMenuView cachedPauseMenu;

    private static bool HasV5PauseMenu()
    {
        if (cachedPauseMenu != null)
        {
            return true;
        }

        // v6 がゲーム UI とポーズを所有しているなら v5 ポーズは存在しない。
        if (FlashBeat.UI.V6.V6UiMode.GameHud
            && FlashBeat.UI.V6.Game.Pause.V6PauseView.Enabled)
        {
            return false;
        }

        if (!v5PauseLookupExhausted)
        {
            if (v5PauseLookupStartFrame < 0)
            {
                v5PauseLookupStartFrame = Time.frameCount;
            }

            cachedPauseMenu = FindFirstObjectByType<FlashBeat.UI.V5.PauseMenuView>();
            if (cachedPauseMenu != null)
            {
                return true;
            }

            // GameHudController は [DefaultExecutionOrder(100)] の Start で作る。
            // 1 秒見つからなければ、このシーンには存在しないと判断して探索を止める
            // (毎フレームの全走査を続けない)。
            if (Time.frameCount - v5PauseLookupStartFrame > V5PauseLookupFrames)
            {
                v5PauseLookupExhausted = true;
            }
        }

        return false;
    }

    /// <summary>v5 ポーズの探索を打ち切るまでのフレーム数 (約 1 秒)。</summary>
    private const int V5PauseLookupFrames = 60;

    private static bool v5PauseLookupExhausted;
    private static int v5PauseLookupStartFrame = -1;

    /// <summary>テスト用: 探索状態を戻す (シーン再ロード相当)。</summary>
    public static void ResetPauseLookupForAudit()
    {
        cachedPauseMenu = null;
        v5PauseLookupExhausted = false;
        v5PauseLookupStartFrame = -1;
    }

    /// <summary>
    /// レガシー MenuPanel が Esc (Back) を担うべきか (Issue #167 / #224)。
    /// v5 Pause (PauseMenuView) と v6 ポーズ (V6PauseView) が無いときだけレガシーが
    /// Esc を処理する (二重 handling の防止)。Update の条件を EditMode テストから
    /// 直接検証できるよう純粋関数に抽出した。
    /// </summary>
    public static bool LegacyBackOpensMenu(bool hasV5Pause, bool hasV6Pause)
    {
        return !hasV5Pause && !hasV6Pause;
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

            // 歌詞は動画と同じタイムライン (メディアクロック) で出し入れする。
            // GManager.step_time はノーツ位置と同じクロックなので、
            // フレーム落ちやバッファリングでも歌詞とノーツがずれない。
            UpdateLyrics(GManager.step_time);
        }
        else
        {
            HideLyrics();
        }

        if (MenuPanel.activeSelf)
        {
            if (KeyActions.IsDown(KeyActions.KeyAction.MenuRetry))
            {
                RetryGame();
            }
            if (KeyActions.IsDown(KeyActions.KeyAction.MenuEnd))
            {
                GameEnd();
            }
            if (KeyActions.IsDown(KeyActions.KeyAction.MenuRetire))
            {
                RetireGame();
            }
            if (KeyActions.IsDown(KeyActions.KeyAction.Back))
            {
                CloseMenu();
            }
            if (KeyActions.IsDown(KeyActions.KeyAction.MenuSwitch))
            {
                SwitchPanel();
            }
        }
        else if (LegacyBackOpensMenu(HasV5PauseMenu(), FlashBeat.UI.V6.Game.Pause.V6PauseView.Enabled))
        {
            // Issue #167: v5 のポーズメニューがあるときは PauseMenuView が Esc を担う。
            // ここでレガシーの MenuPanel を開くと両方が反応して二重になる。
            // Issue #224: v6 ポーズ (V6PauseView.Enabled) が有効なときも Esc は v6 が担う
            // (V6GameRuntime.UpdatePause)。レガシー MenuPanel を開くと v6 ポーズと二重になる。
            if (KeyActions.IsDown(KeyActions.KeyAction.Back))
            {
                OpenMenu();
            }
        }
        
        

    }

    /// <summary>
    /// いまの時刻に表示されている歌詞を反映する。
    /// 歌詞が無い (データが無い・空行の) ときはパネルごと消す。
    /// </summary>
    void UpdateLyrics(float mediaTime)
    {
        int index = LyricsTimeline.IndexAt(lyricLines, mediaTime);
        if (index != currentLyricIndex)
        {
            currentLyricIndex = index;
            ApplyLyricLine(index);
        }
        ApplyTextPanelVisibility();
    }

    /// <summary>再生前など、歌詞を出さない状態に戻す。</summary>
    void HideLyrics()
    {
        if (currentLyricIndex != -1)
        {
            currentLyricIndex = -1;
            ApplyLyricLine(-1);
        }
        ApplyTextPanelVisibility();
    }

    void ApplyLyricLine(int index)
    {
        if (index < 0)
        {
            ClearLyricText();
            return;
        }

        LyricLine line = lyricLines[index];
        if (!line.HasText)
        {
            // 空行 (全角スペース等) は「ここで歌詞を消す」合図として扱う
            ClearLyricText();
            return;
        }

        mondaiText.text = line.Text;
        furiganaText.text = line.Furigana ?? "";
        romajiText.text = "";
        display = true; // 表示フラグを立てる
    }

    void ClearLyricText()
    {
        mondaiText.text = "";
        furiganaText.text = "";
        romajiText.text = "";
        display = false;
    }

    /// <summary>
    /// 歌詞パネルは「歌詞表示がON」かつ「いま歌詞の行がある」ときだけ出す。
    /// 曲に歌詞が無い場合や間奏中は、空のパネルを残さない。
    /// Issue #167: Select の Ready 画面で Lyrics = Off にした場合は
    /// 演奏中も歌詞を出さない (PlaySession が実値を持つ)。
    /// </summary>
    void ApplyTextPanelVisibility()
    {
        // v6 HUD (V6UiMode.GameHud) は歌詞を自分のレイヤーで描く (Issue #233)。
        // レガシー TextPanel も出すと歌詞が二重表示になるので隠す。
        bool show = Menu && display && !menuVisible
            && FlashBeat.UI.V5.PlaySession.LyricsEnabled
            && !FlashBeat.UI.V6.V6UiMode.GameHud;
        if (TextPanel.activeSelf != show) TextPanel.SetActive(show);
    }

    /// <summary>歌詞の表示可否を反映する (Pause の Display タブから呼ばれる)。</summary>
    public void ApplyLyricsVisibility()
    {
        ApplyTextPanelVisibility();
    }

    public void TextPanelOn()
    {
        OnText.SetActive(true);
        OffText.SetActive(false);
        Menu = true;
        ApplyTextPanelVisibility();
    }

    public void TextPanelOff()
    {
        OnText.SetActive(false);
        OffText.SetActive(true);
        Menu = false;
        ApplyTextPanelVisibility();
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
