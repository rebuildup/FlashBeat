using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

/// <summary>
/// Festival ranking overlay (#134), scene independent (KeyConfigUI pattern).
/// Lives for the whole session (DontDestroyOnLoad) and drives RankingPanel:
/// - on the result screen: after the result appears, cloud songs with score > 0
///   get the 「ランキングに登録」 modal (own character buffer from
///   Input.inputString so it works on any keyboard layout without IME),
/// - on Title/Select/Result: F5 opens the read-only Top 10,
/// - while the modal is open the scene's *SceneManager behaviour and the
///   EventSystem navigation are suspended (KeyConfigUI input isolation), so
///   Retry / back-to-select keep working after the modal closes.
/// </summary>
public class ResultRankingOverlay : MonoBehaviour
{
    private const float ResultModalDelaySeconds = 1.2f;
    private const int NicknameMaxLength = 12;

    private enum State
    {
        Idle,
        EnteringName,
        Submitting,
        SubmitFailed,
        Board,
    }

    private static ResultRankingOverlay instance;

    private RankingPanel panel;
    private State state = State.Idle;
    private readonly System.Text.StringBuilder nameBuffer = new System.Text.StringBuilder();
    private string songId;
    private int chartVersion;
    private int myRank;
    private bool submittedThisResult;
    private bool skippedThisResult;
    private float sceneEnteredAt;
    private string lastSceneName;
    private readonly List<MonoBehaviour> suspended = new List<MonoBehaviour>();
    private bool savedSendNavigation = true;
    private bool navigationSuspended;

    /// <summary>シーン非依存で起動する。</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (FindObjectOfType<ResultRankingOverlay>() != null) return;
        var go = new GameObject("ResultRankingOverlay");
        DontDestroyOnLoad(go);
        go.AddComponent<ResultRankingOverlay>();
    }

    private void Awake()
    {
        instance = this;
        panel = gameObject.AddComponent<RankingPanel>();
        panel.EnsureBuilt();
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
        ResumeScene();
    }

    private static bool IsResultScene()
    {
        return SceneManager.GetActiveScene().name == "ResultScene";
    }

    private static bool IsBoardScene()
    {
        string name = SceneManager.GetActiveScene().name;
        return name == "TitleScene" || name == "SelectScene" || name == "ResultScene";
    }

    /// <summary>ランキング対象のクラウド曲 (songId/chartVersion を持つ曲)。なければ null。</summary>
    private static SongData RankingSong()
    {
        SongData song = GManager.GetSong(GManager.songID);
        if (song == null || !song.isRemote || string.IsNullOrEmpty(song.remoteId) || song.chartVersion <= 0)
        {
            return null;
        }
        return song;
    }

    private void Update()
    {
        // v6 UI モードでは旧オーバーレイ (自動登録モーダル / F5 ボード / ヒント
        // 「更新を適用して再起動」等) を出さない。v6 側が自前の登録モーダルと
        // ランキングを持つため。フラグ分岐なので V6ResultView.Enabled = false
        // (v5 へ戻す) で従来どおり動く。
        if (FlashBeat.UI.V6.Result.V6ResultView.Enabled)
        {
            return;
        }

        string sceneName = SceneManager.GetActiveScene().name;
        if (sceneName != lastSceneName)
        {
            lastSceneName = sceneName;
            sceneEnteredAt = Time.unscaledTime;
            OnSceneChanged();
        }

        panel.ShowHint(!IsModalOrBoardOpen() && IsBoardScene());

        if (IsModalOrBoardOpen())
        {
            HandleOpenInput();
            return;
        }

        if (IsBoardScene() && Input.GetKeyDown(KeyCode.F5))
        {
            OpenBoard(null);
            return;
        }

        if (IsResultScene() && !submittedThisResult && !skippedThisResult
            && Time.unscaledTime - sceneEnteredAt >= ResultModalDelaySeconds)
        {
            SongData song = RankingSong();
            if (song != null && GManager.score > 0)
            {
                OpenSubmitModal(song);
            }
        }
    }

    private bool IsModalOrBoardOpen()
    {
        return state != State.Idle;
    }

    private void OnSceneChanged()
    {
        // シーンをまたいだ状態のリセット (提出済みフラグは結果画面ごと)。
        submittedThisResult = false;
        skippedThisResult = false;
        if (IsModalOrBoardOpen())
        {
            CloseAll();
        }
    }

    private void CloseAll()
    {
        panel.HideModal();
        panel.HideBoard();
        state = State.Idle;
        // Issue #167: 閉じたフレームに背面の View が同じキー (Esc など) を
        // 処理して遷移するのを防ぐ。
        FlashBeat.UI.SceneInputGate.BlockThisFrame();
        ResumeScene();
    }

    // --- Top 10 board --------------------------------------------------------

    /// <summary>F5 で開く読み取り専用ボード。myRank は提出応答から (null ならハイライトなし)。</summary>
    private void OpenBoard(RankingClient.Result submitResult)
    {
        SongData song = RankingSong();
        if (song == null)
        {
            return;
        }
        songId = song.remoteId;
        chartVersion = song.chartVersion;
        myRank = submitResult != null && submitResult.score != null ? submitResult.score.rank : 0;
        state = State.Board;
        panel.EnsureBuilt();
        panel.SetBoardLoading();
        panel.ShowBoard(null, 0, null);
        // Issue #167: ボードを開けている間は背面の入力を止める (Title / Select でも)。
        // 止めないと Select では Enter/Space が裏の画面に届き、ボードの裏で
        // 曲が始まってしまう。
        SuspendScene();
        FlashBeat.UI.SceneInputGate.BlockThisFrame();
        StartCoroutine(RankingClient.GetLeaderboard(songId, chartVersion, OnLeaderboard));
    }

    private void OnLeaderboard(RankingClient.Result result)
    {
        if (state != State.Board) return; // 閉じたあとの応答は捨てる
        if (!result.ok || result.leaderboard == null)
        {
            panel.ShowBoard(null, 0, null);
            panel.SetBoardMessage("ランキングを とりにいけませんでした");
            return;
        }
        panel.HideModal();
        var entries = new List<RankingClient.LeaderboardEntry>(result.leaderboard.entries);
        if (entries.Count > 10) entries.RemoveRange(10, entries.Count - 10);
        panel.ShowBoard(entries, myRank, null);
    }

    // --- submit modal --------------------------------------------------------

    private void OpenSubmitModal(SongData song)
    {
        songId = song.remoteId;
        chartVersion = song.chartVersion;
        myRank = 0;
        nameBuffer.Length = 0;
        string saved = RankingClient.LoadNickname();
        if (!string.IsNullOrEmpty(saved)) nameBuffer.Append(saved);
        submittedThisResult = true; // 一度だけ出す (失敗時は Esc で閉じるまで再試行)
        state = State.EnteringName;
        panel.EnsureBuilt();
        panel.ShowModal(nameBuffer.ToString());
        panel.SetModalMessage(string.Empty, Color.white);
        SuspendScene();
        FlashBeat.UI.SceneInputGate.BlockThisFrame();
    }

    private void HandleOpenInput()
    {
        switch (state)
        {
            case State.EnteringName:
                HandleNameInput();
                break;
            case State.Submitting:
                // 応答待ち。Esc で中断はしない (二重送信を防ぐ)。
                break;
            case State.SubmitFailed:
                // 戻る/決定はメニュー操作設定に従う (#142)。ただし名前入力モーダルでは
                // Space が文字入力になるため Confirm (既定 Space) は読まず Enter のみ。
                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                {
                    Submit();
                }
                else if (KeyActions.IsDown(KeyActions.KeyAction.Back))
                {
                    CloseAll();
                }
                break;
            case State.Board:
                if (Input.GetKeyDown(KeyCode.F5) || KeyActions.IsDown(KeyActions.KeyAction.Back))
                {
                    CloseAll();
                }
                break;
        }
    }

    private void HandleNameInput()
    {
        // 戻るはメニュー操作設定に従う (#142、既定 Esc)。文字入力系 (BS/Enter/Space) は
        // モーダルのテキスト編集として固定のまま。
        if (KeyActions.IsDown(KeyActions.KeyAction.Back))
        {
            skippedThisResult = true;
            CloseAll();
            return;
        }
        if (Input.GetKeyDown(KeyCode.Backspace))
        {
            if (nameBuffer.Length > 0) nameBuffer.Length -= 1;
            panel.SetNameText(nameBuffer.ToString());
            return;
        }
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            string nickname = nameBuffer.ToString().Trim();
            if (string.IsNullOrEmpty(nickname))
            {
                panel.SetModalMessage("なまえを いれてください", new Color(1f, 0.85f, 0.3f));
                return;
            }
            if (nickname.Length > NicknameMaxLength)
            {
                panel.SetModalMessage("なまえが ながすぎます", new Color(1f, 0.85f, 0.3f));
                return;
            }
            Submit();
            return;
        }

        // Input.inputString から 1 文字ずつ受け取る (レイアウト非依存・IME 不要)。
        string input = Input.inputString;
        for (int i = 0; i < input.Length; i++)
        {
            char c = input[i];
            if (c < ' ') continue; // 制御文字 (\n, \b) は KeyCode 側で処理済み
            if (nameBuffer.Length >= NicknameMaxLength) break;
            nameBuffer.Append(c);
        }
        if (input.Length > 0)
        {
            panel.SetNameText(nameBuffer.ToString());
        }
    }

    private void Submit()
    {
        string nickname = nameBuffer.ToString().Trim();
        state = State.Submitting;
        panel.SetModalMessage("送信中...", new Color(0.6f, 0.9f, 1f));
        StartCoroutine(RankingClient.PostScore(songId, chartVersion, nickname,
            GManager.score, GManager.perfect, GManager.great, GManager.bad, GManager.miss, GManager.maxCombo,
            OnSubmitResult));
    }

    private void OnSubmitResult(RankingClient.Result result)
    {
        if (state != State.Submitting) return;
        if (result.ok && result.score != null)
        {
            RankingClient.SaveNickname(nameBuffer.ToString().Trim());
            myRank = result.score.rank;
            panel.HideModal();
            state = State.Board;
            panel.EnsureBuilt();
            panel.SetBoardLoading();
            panel.ShowBoard(null, 0, null);
            if (IsResultScene()) SuspendScene(); // 結果画面では Space/R を止める
            StartCoroutine(RankingClient.GetLeaderboard(songId, chartVersion, OnLeaderboard));
        }
        else
        {
            state = State.SubmitFailed;
            string reason = result.error == RankingClient.UnavailableError
                ? "オフラインのようです"
                : "送信できませんでした";
            panel.SetModalMessage(reason + "  再送 [Enter] / スキップ [Esc]", new Color(1f, 0.6f, 0.6f));
        }
    }

    // --- scene suspension (KeyConfigUI pattern) ------------------------------

    /// <summary>
    /// モーダル / ボード表示中は背後のシーンのキー処理と EventSystem 経由の Submit を止める。
    /// v5 の View (ResultView / SelectView / TitleView) とレガシー *SceneManager の両方。
    /// 閉じたら元に戻す。開いた / 閉じたフレームの二重処理は SceneInputGate が担う。
    /// </summary>
    private void SuspendScene()
    {
        ResumeScene();
        if (EventSystem.current != null)
        {
            savedSendNavigation = EventSystem.current.sendNavigationEvents;
            EventSystem.current.sendNavigationEvents = false;
            EventSystem.current.SetSelectedGameObject(null);
            navigationSuspended = true;
        }
        MonoBehaviour[] all = FindObjectsOfType<MonoBehaviour>();
        for (int i = 0; i < all.Length; i++)
        {
            MonoBehaviour behaviour = all[i];
            if (behaviour == null || behaviour == this || !behaviour.enabled) continue;
            if (behaviour is RankingPanel) continue;
            string typeName = behaviour.GetType().Name;
            if (typeName.EndsWith("SceneManager")
                // Issue #167: v5 View の Update も止める。止めないとランキングの
                // 名前入力中に ResultView が R / Space / Esc を処理して
                // リトライや遷移が起きる (ResultSceneManager は v5 では入力を持たない)。
                || typeName == "TitleView" || typeName == "SelectView" || typeName == "ResultView"
                // Tab のキーガイド / F3・F4 の更新通知もモーダル中は止める。
                || typeName == "KeyGuideUI" || typeName == "UpdateNoticeUI"
                // ボード / モーダル中の F2 でキー設定が重ねて開かないようにする。
                || typeName == "KeyConfigUI")
            {
                behaviour.enabled = false;
                suspended.Add(behaviour);
            }
        }
    }

    private void ResumeScene()
    {
        if (navigationSuspended && EventSystem.current != null)
        {
            EventSystem.current.sendNavigationEvents = savedSendNavigation;
        }
        navigationSuspended = false;
        for (int i = 0; i < suspended.Count; i++)
        {
            if (suspended[i] != null)
            {
                suspended[i].enabled = true;
            }
        }
        suspended.Clear();
    }
}
