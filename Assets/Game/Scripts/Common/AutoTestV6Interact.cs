using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using FlashBeat.Gameplay;
using FlashBeat.UI.V6;
using FlashBeat.UI.V6.Common;
using FlashBeat.UI.V6.Game.Hud;
using FlashBeat.UI.V6.Game.Pause;
using FlashBeat.UI.V6.Option;
using FlashBeat.UI.V6.Result;
using FlashBeat.UI.V6.Select.View;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

/// <summary>
/// 操作を伴う受け入れ条件の実行 (<see cref="AutoTestV6Screens"/> の兄弟モード)。
///
/// 使い方 (プレイヤー引数):
///   -autotest v6interact -autotest-out &lt;dir&gt;
///
/// 静止画では確かめられない「操作・状態・永続化」を、実プレイヤーの同じコード
/// 経路 (ポインタ操作 / キー注入 / ビューの公開 API) で動かして PASS / FAIL を
/// 記録する。対象:
///
///   - #375 音量スライダ: ドラッグで値が変わる / ラベルとノブが追従する /
///     指を離すと保存される / 画面を離れると猶予中の保存が確定する。
///   - #384 Result の段階表示: 表示が時間とともに進む / 登録欄が自動で開く
///     (時刻は V6ResultRevealLogic の正本に従う) / Space で残りを打ち切る (スキップ)。
///   - #382 再開カウントダウン: 3 → 2 → 1 が出る / その間ポーズゲートは閉じた
///     まま / 3 秒で再開する / Esc でポーズへ戻る / 開始フレームの Esc は無視する。
///   - #377 キャッシュ: Select を離れて戻ったとき Resources を読み直さない。
///
/// ポインタ操作は EventSystem のレイキャストで当たり先を確かめてから配る
/// (手前に別の UI が被さっていれば当たらない)。OS のマウス注入は行わないため、
/// ポインタの「移動そのもの」は再現していない。
///
/// 結果は &lt;out&gt;/autotest-v6interact-result.txt と Player.log の
/// [AutoTestV6Interact] 行。スクリーンショットも同じディレクトリへ書く。
/// </summary>
public class AutoTestV6Interact : MonoBehaviour
{
    private const string Tag = "[AutoTestV6Interact]";

    /// <summary>シーン ロード後、UI が組み上がるまでの待ち秒。</summary>
    private const float SettleSeconds = 3f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (V6ScreenAudit.GetArg("-autotest") != "v6interact") { return; }

        var go = new GameObject("AutoTestV6Interact");
        DontDestroyOnLoad(go);
        go.AddComponent<AutoTestV6Interact>();
    }

    private string outDir;
    private int pass;
    private int fail;
    private int shot;
    private readonly List<string> lines = new List<string>();

    private IEnumerator Start()
    {
        outDir = V6ScreenAudit.GetArg("-autotest-out");
        if (string.IsNullOrEmpty(outDir)) { outDir = Application.persistentDataPath; }
        try { Directory.CreateDirectory(outDir); }
        catch (Exception) { }

        Debug.Log(Tag + " start out=" + outDir
            + " resolution=" + Screen.width + "x" + Screen.height
            + " fullscreen=" + Screen.fullScreen);

        // どの検証で例外が出ても、結果を残して終了する。無人実行でハングしない。
        try
        {
            yield return RunVolumeSlider();
            yield return RunOptionPlaySettings();
            yield return RunResultReveal();
            yield return RunResumeCountdown();
            yield return RunContentCache();
        }
        finally
        {
            WriteResult();
            Application.Quit();
        }
    }

    // ---------------------------------------------------------------- #375

    /// <summary>
    /// 音量スライダのドラッグ (Issue #375)。実際のポインタ操作と同じ経路
    /// (OnPointerDown / OnDrag / OnPointerUp → 座標変換 → 値) を通す。
    /// </summary>
    private IEnumerator RunVolumeSlider()
    {
        yield return LoadScene("OptionScene");

        var option = V6OptionView.Current;
        if (!Check("option view exists", option != null)) { yield break; }

        option.SelectTabForAudit(2); // Sound
        yield return null;
        yield return null;

        V6VolumeSlider slider = option.VolumeSliderForAudit;
        if (!Check("the sound tab shows the volume slider", slider != null)) { yield break; }

        float original = GManager.mainVolume;
        GManager.mainVolume = 0.5f;
        slider.Refresh();
        yield return null;

        var readouts = new StringBuilder();

        // 押した瞬間 (20%) から 35 / 55 / 70% へドラッグし、80% で離す。
        // 直接ハンドラを呼ばず、EventSystem のレイキャストで当たり先を確かめてから
        // そのオブジェクトへ配る。手前に別の UI が被さっていれば当たらない。
        bool routed = DispatchPointer(slider, 0.20f, ExecuteEvents.pointerDownHandler);
        yield return null;
        readouts.Append("down=").Append(Percent(slider.Value)).Append(' ');

        float[] dragTo = { 0.35f, 0.55f, 0.70f };
        bool valueFollowedEveryStep = true;
        for (int i = 0; i < dragTo.Length; i++)
        {
            routed &= DispatchPointer(slider, dragTo[i], ExecuteEvents.dragHandler);
            yield return null;
            readouts.Append("drag=").Append(Percent(slider.Value)).Append(' ');
            // 途中の各位置で値とラベルが追従していること (離した後の値だけを
            // 見ると、途中で別の値に飛んでいても気づけない)。
            if (Mathf.Abs(slider.Value - dragTo[i]) > 0.02f
                || slider.ValueLabelForAudit != Percent(dragTo[i]))
            {
                valueFollowedEveryStep = false;
            }
        }

        yield return Screenshot("375-slider-drag");

        routed &= DispatchPointer(slider, 0.80f, ExecuteEvents.pointerUpHandler);
        yield return null;
        readouts.Append("up=").Append(Percent(slider.Value));
        Debug.Log(Tag + " slider " + readouts);

        Check("the pointer events reach the slider through the event system", routed);
        Check("the value and the label follow every drag step", valueFollowedEveryStep);

        Check("the drag changes the value to where the pointer is", Mathf.Abs(slider.Value - 0.80f) <= 0.02f);
        Check("the value owner follows the drag", Mathf.Abs(GManager.mainVolume - 0.80f) <= 0.02f);
        Check("the value label follows the drag", slider.ValueLabelForAudit == "80%");
        Check("the knob follows the drag",
            Mathf.Abs(slider.KnobCenterForAudit - V6VolumeSlider.KnobCenter(0.80f)) <= 1.5f);

        float saved = PlayerPrefs.GetFloat(OptionSettings.MainVolumeKey, -1f);
        Check("releasing the pointer saves the volume", Mathf.Abs(saved - 0.80f) <= 0.02f);

        // 猶予 (0.5 秒) の途中で画面を離れる: 最後の値が保存されること。
        DispatchPointer(slider, 0.30f, ExecuteEvents.pointerDownHandler);
        yield return null;
        Check("a drag without release is not saved immediately",
            Mathf.Abs(PlayerPrefs.GetFloat(OptionSettings.MainVolumeKey, -1f) - 0.30f) > 0.02f);

        option.SetVisible(false);
        yield return null;
        Check("leaving the screen saves the pending volume",
            Mathf.Abs(PlayerPrefs.GetFloat(OptionSettings.MainVolumeKey, -1f) - 0.30f) <= 0.02f);

        // 次の検証に影響しないよう元の値へ戻す。
        GManager.mainVolume = original;
        OptionSettings.Save();
        option.SetVisible(true);
    }

    // ---------------------------------------------------------------- #376/#375

    /// <summary>
    /// Option の Play タブ (Issue #376 / #375)。キー設定は画面上の行だけで開ける
    /// (値欄に F2 を出さない)、NoteSpeed / NoteTiming は行から変更できて
    /// PlayerPrefs へ保存されることを、実プレイヤーのビュー経路で確かめる。
    /// </summary>
    private IEnumerator RunOptionPlaySettings()
    {
        yield return LoadScene("OptionScene");

        var option = V6OptionView.Current;
        if (!Check("option view exists (play settings)", option != null)) { yield break; }

        option.SelectTabForAudit(0); // Play
        yield return null;
        yield return null;

        // 行構成: NoteSpeed / NoteTiming / Key Layout / キー設定。
        var labels = new List<string>();
        bool f2Shown = false;
        for (int i = 0; i < option.RowCountForAudit; i++)
        {
            labels.Add(option.RowLabelForAudit(i));
            string value = option.RowValueForAudit(i);
            if (value != null && value.Contains("F2")) { f2Shown = true; }
        }
        Debug.Log(Tag + " option play rows: " + string.Join(" / ", labels));
        Check("the play tab has NoteSpeed / NoteTiming / キー設定",
            labels.Contains("NoteSpeed") && labels.Contains("NoteTiming") && labels.Contains("キー設定"));
        Check("no row shows F2 as its value", !f2Shown);

        int keyRow = labels.IndexOf("キー設定");
        Check("the key config row reads '開く ›'",
            keyRow >= 0 && option.RowValueForAudit(keyRow) == "開く ›");

        // NoteSpeed: ←→ と同じ AdjustSelected 経路で変更し、値・保存値・表示が一致する。
        int speedRow = labels.IndexOf("NoteSpeed");
        int timingRow = labels.IndexOf("NoteTiming");
        float savedSpeed = GManager.noteSpeed;
        int savedTiming = GManager.noteTiming;
        if (speedRow >= 0)
        {
            // クランプ端 (1 / 50) では +1 が効かないので中央の値から始める。
            GManager.noteSpeed = 10f;
            float before = GManager.noteSpeed;
            option.SelectRowForAudit(speedRow);
            option.AdjustSelectedRowForAudit(1);
            yield return null;
            float saved = PlayerPrefs.GetFloat(OptionSettings.NoteSpeedKey, float.NaN);
            Debug.Log(Tag + " notespeed " + before.ToString("F2") + " -> "
                + GManager.noteSpeed.ToString("F2") + " saved=" + saved.ToString("F2")
                + " row=" + option.RowValueForAudit(speedRow));
            Check("NoteSpeed changes from the option row", GManager.noteSpeed != before);
            Check("NoteSpeed is saved to PlayerPrefs",
                !float.IsNaN(saved) && Mathf.Abs(saved - GManager.noteSpeed) < 0.001f);
            Check("the NoteSpeed row shows the new value",
                option.RowValueForAudit(speedRow) == V6OptionLogic.FormatSpeed(GManager.noteSpeed));
        }
        else
        {
            Check("the play tab has a NoteSpeed row", false);
        }

        if (timingRow >= 0)
        {
            GManager.noteTiming = 0;
            int before = GManager.noteTiming;
            option.SelectRowForAudit(timingRow);
            option.AdjustSelectedRowForAudit(1);
            yield return null;
            int saved = PlayerPrefs.GetInt(OptionSettings.NoteTimingKey, int.MinValue);
            Debug.Log(Tag + " notetiming " + before + " -> " + GManager.noteTiming
                + " saved=" + saved + " row=" + option.RowValueForAudit(timingRow));
            Check("NoteTiming changes from the option row", GManager.noteTiming != before);
            Check("NoteTiming is saved to PlayerPrefs", saved == GManager.noteTiming);
            Check("the NoteTiming row shows the new value",
                option.RowValueForAudit(timingRow) == GManager.noteTiming.ToString());
        }
        else
        {
            Check("the play tab has a NoteTiming row", false);
        }

        // キー設定: 行の決定 (Enter と同じ ActivateRow) でモーダルが開く。
        // F2 を押さずに画面内の操作だけで到達できること。
        if (keyRow >= 0)
        {
            option.SelectRowForAudit(keyRow);
            option.ActivateSelectedRowForAudit();
            yield return null;
            yield return null;

            var modal = option.KeyConfigModalForAudit;
            Check("the key config modal opens from the row", modal != null && modal.IsOpen);
            Check("the modal blocks the background input", V6KeyConfigModal.BlocksInput);
            yield return Screenshot("376-keyconfig-modal");
            if (modal != null)
            {
                // Esc の実キー経路は EditMode 側 (V6KeyConfigModalTests) が固定済み。
                // ここでは開いたまま残さないよう製品の閉じる API で畳む。
                modal.Hide();
                yield return null;
            }
            Check("closing the modal releases the background input", !V6KeyConfigModal.BlocksInput);
        }
        else
        {
            Check("the play tab has a キー設定 row", false);
        }

        // 元の値へ戻して保存する (後続の検証・実機状態を汚さない)。
        // NoteTiming は曲ごとの offset 設定へも書き戻されるので、そちらも揃える。
        GManager.noteSpeed = savedSpeed;
        GManager.noteTiming = savedTiming;
        OptionSceneManager.WriteNoteTimingToSongOffsets();
        OptionSettings.Save();
    }

    // ---------------------------------------------------------------- #384

    /// <summary>
    /// Result の段階表示 (Issue #384)。時間で進むこと、2 秒で登録欄が開くこと、
    /// Space で残りを打ち切ることを、実プレイヤーの入力経路で確かめる。
    /// </summary>
    private IEnumerator RunResultReveal()
    {
        // 段階表示はシーン開始と同時に進む。落ち着くまで待つと最初の段を
        // 見逃して「一気に出た」ように観測してしまうので、待たずに取り始める。
        yield return LoadScene("ResultScene", 0f);

        V6ResultView view = FindResultView();
        float waited = 0f;
        while (waited < 10f && view == null)
        {
            view = FindResultView();
            waited += Time.unscaledDeltaTime;
            yield return null;
        }

        if (!Check("result view exists", view != null)) { yield break; }

        bool sawBeforeComplete = false;
        bool sawTrophyFadeIn = false;
        bool autoRegisterOpened = false;
        float modalOpenElapsed = -1f;
        float firstTrophyAlpha = -1f;
        float lastTrophyAlpha = -1f;
        bool completed = false;

        // 期限付き。登録欄が開かない退行でここが止まらないようにする。
        // 登録欄は V6ResultRevealLogic.RegisterStart (3.0s) で開き、
        // リビール全体は TotalSeconds (3.18s) で完了する (#425 で 2s→3s へ遅延)。
        float watchStart = Time.unscaledTime;
        while (Time.unscaledTime - watchStart < 10f
               && view.RevealElapsedForAudit < V6ResultRevealLogic.TotalSeconds + 0.3f)
        {
            float elapsed = view.RevealElapsedForAudit;
            float alpha = view.TrophyAlphaForAudit;
            if (elapsed < V6ResultRevealLogic.RegisterStart - 0.1f && !view.RevealCompleteForAudit)
            {
                sawBeforeComplete = true;
            }

            if (firstTrophyAlpha < 0f) { firstTrophyAlpha = alpha; }
            lastTrophyAlpha = alpha;
            if (alpha > 0f && alpha < 1f) { sawTrophyFadeIn = true; }
            if (view.ModalOpenForAudit && !autoRegisterOpened)
            {
                autoRegisterOpened = true;
                modalOpenElapsed = elapsed;
            }
            if (view.RevealCompleteForAudit) { completed = true; }

            if (autoRegisterOpened && completed) { break; }
            yield return null;
        }

        Debug.Log(Tag + " reveal elapsed=" + view.RevealElapsedForAudit.ToString("F2")
            + " complete=" + view.RevealCompleteForAudit
            + " modal=" + view.ModalOpenForAudit
            + " modalAt=" + modalOpenElapsed.ToString("F2")
            + " trophyAlpha=" + view.TrophyAlphaForAudit.ToString("F2"));

        Check("the reveal is staged (not everything at once)", sawBeforeComplete);
        Check("the trophy fades in gradually",
            lastTrophyAlpha >= 0.99f
            && (sawTrophyFadeIn || (firstTrophyAlpha >= 0f && firstTrophyAlpha < lastTrophyAlpha)));
        Check("the register field opens automatically at the reveal register stage",
            autoRegisterOpened
            && modalOpenElapsed >= V6ResultRevealLogic.RegisterStart - 0.1f
            && modalOpenElapsed <= V6ResultRevealLogic.RegisterStart + V6ResultRevealLogic.RegisterDuration + 0.3f);
        Check("the reveal completes on its own", completed);

        yield return Screenshot("384-result-reveal");

        // 2 回目: Space で残りを打ち切る。自動で開く登録欄も開かない。
        yield return LoadScene("ResultScene", 0f);
        view = null;
        waited = 0f;
        while (waited < 10f && view == null)
        {
            view = FindResultView();
            waited += Time.unscaledDeltaTime;
            yield return null;
        }

        if (!Check("result view exists (skip run)", view != null)) { yield break; }

        // 段階表示の途中 (1.0 秒未満) になるまで待ってから押す。期限付き。
        float waitStarted = Time.unscaledTime;
        while (view.RevealElapsedForAudit < 0.8f && Time.unscaledTime - waitStarted < 10f)
        {
            yield return null;
        }

        bool skipped = false;
        for (int attempt = 0; attempt < 3 && !skipped; attempt++)
        {
            KeyBindings.InjectKeyDown(KeyCode.Space);
            yield return null;
            yield return null;
            skipped = view.RevealCompleteForAudit;
        }

        Debug.Log(Tag + " skip elapsed=" + view.RevealElapsedForAudit.ToString("F2")
            + " complete=" + view.RevealCompleteForAudit
            + " modal=" + view.ModalOpenForAudit);

        Check("Space skips the staged reveal", skipped);
        Check("the skip does not open the register field", !view.ModalOpenForAudit);

        yield return Screenshot("384-result-skip");
    }

    // ---------------------------------------------------------------- #382

    /// <summary>
    /// 再開カウントダウン (Issue #382)。数字の並び、その間ポーズゲートが
    /// 閉じたままであること、3 秒で再開すること、Esc の取り消しを確かめる。
    /// </summary>
    private IEnumerator RunResumeCountdown()
    {
        yield return LoadScene("GameScene");

        V6PauseView pause = null;
        V6GameHudView hud = null;
        MusicManager music = null;
        float waited = 0f;
        while (waited < 15f && (pause == null || hud == null))
        {
            pause = V6PauseView.Current;
            hud = FindFirstObjectByType<V6GameHudView>();
            music = music != null ? music : FindFirstObjectByType<MusicManager>();
            waited += Time.unscaledDeltaTime;
            yield return null;
        }

        if (!Check("pause view exists", pause != null)) { yield break; }
        if (!Check("game hud exists", hud != null)) { yield break; }

        // #382: 静止・判定ゲートの確認は「演奏が実際に始まっている」状態で行う。
        // メディア準備を待って Space を注入する (AutoTestV6SongEnd と同じ経路)。
        // 開始できない環境では開始前ポーズの縮退経路だけを検証する。
        bool playbackStarted = false;
        if (music != null)
        {
            float readyWaited = 0f;
            // AutoTestV6SongEnd と同じ余裕 (YouTube 解決は遅いことがある)。
            while (readyWaited < 180f && !music.MediaReady)
            {
                readyWaited += Time.unscaledDeltaTime;
                yield return null;
            }
            if (music.MediaReady)
            {
                KeyBindings.InjectKeyDown(KeyCode.Space);
                float startWaited = 0f;
                while (startWaited < 10f && !music.PlaybackStarted)
                {
                    startWaited += Time.unscaledDeltaTime;
                    yield return null;
                }
            }
            playbackStarted = music.PlaybackStarted;
        }
        Debug.Log(Tag + " playback started=" + playbackStarted);
        Check("playback started for the pause checks", playbackStarted);

        // 1. 通常の再開: 3 → 2 → 1 の後にゲートが開く。
        pause.Open();
        yield return null;
        Check("opening the pause closes the gate", PauseGate.IsPaused);

        // ポーズ中の基準値: 譜面時計 (ノーツ・判定の基準) と判定カウンタ。
        float frozenStep = GManager.step_time;
        int frozenPerfect = GManager.perfect, frozenGreat = GManager.great;
        int frozenBad = GManager.bad, frozenMiss = GManager.miss, frozenScore = GManager.score;

        // ポーズ中の判定キー入力は判定に影響しない (#382)。
        KeyBindings.InjectKeyDown(KeyBindings.GetKeys(0)[0]);
        yield return null;
        KeyBindings.InjectKeyDown(KeyBindings.GetKeys(3)[0]);
        yield return null;

        // ポーズ中は譜面時計が止まったまま (ノーツが動かない)。
        bool stepFrozeDuringPause = true;
        float pauseWaited = 0f;
        while (pauseWaited < 0.6f)
        {
            if (!Mathf.Approximately(GManager.step_time, frozenStep)) { stepFrozeDuringPause = false; }
            pauseWaited += Time.unscaledDeltaTime;
            yield return null;
        }
        Check("the chart clock is frozen while paused", stepFrozeDuringPause);
        Check("judgement keys during pause change nothing",
            GManager.perfect == frozenPerfect && GManager.great == frozenGreat
            && GManager.bad == frozenBad && GManager.miss == frozenMiss
            && GManager.score == frozenScore);

        pause.RequestResume();
        Check("resume starts the countdown", V6ResumeCountdown.IsRunning);

        var digits = new List<string>();
        bool gateStayedClosed = true;
        bool stepFrozeDuringCountdown = true;
        bool shotTaken = false;
        bool injectedOn3 = false;
        bool injectedOn2 = false;
        // カウントダウン中 (ゲートが閉じている間) の判定カウンタ。
        // ループの各フレームで「まだ実行中」の値を取り直すので、
        // ループを抜けた時点の値は必ずゲート閉鎖中のものになる
        // (再開後の正当な判定を誤って FAIL にしない)。
        int lastPerfect = frozenPerfect, lastGreat = frozenGreat;
        int lastBad = frozenBad, lastMiss = frozenMiss, lastScore = frozenScore;
        // 0.25 秒間隔だと、カウントダウン開始直後の重いフレームで 1 つ飛ばす
        // ことがある (3 を観測できず 2 から始まった)。毎フレーム見る。
        float sinceStart = 0f;
        while (sinceStart < 3.6f)
        {
            string digit = hud.CountdownTextForAudit;
            if (digit.Length > 0 && (digits.Count == 0 || digits[digits.Count - 1] != digit))
            {
                digits.Add(digit);
            }

            // 判定キーが通らないこと (#382)。判定側の合成条件は
            // 「ポーズ中でも曲終了表示中でもない」なので、カウントダウン中は閉じている。
            if (V6ResumeCountdown.IsRunning
                && SongEndGate.CanJudge(PauseGate.IsPaused, SongEndGate.BlocksJudging))
            {
                gateStayedClosed = false;
            }

            if (V6ResumeCountdown.IsRunning)
            {
                // カウントダウン中も譜面時計は固定値のまま (ノーツが動かない)。
                if (!Mathf.Approximately(GManager.step_time, frozenStep))
                {
                    stepFrozeDuringCountdown = false;
                }

                lastPerfect = GManager.perfect; lastGreat = GManager.great;
                lastBad = GManager.bad; lastMiss = GManager.miss; lastScore = GManager.score;

                // カウントダウン中の判定キー入力 (3 と 2 の表示中に 1 回ずつ)。
                // 注入は 1 フレーム有効なので、必ずゲート閉鎖中に失効する。
                if (digit == "3" && !injectedOn3)
                {
                    injectedOn3 = true;
                    KeyBindings.InjectKeyDown(KeyBindings.GetKeys(1)[0]);
                    KeyBindings.InjectKeyDown(KeyBindings.GetKeys(6)[0]);
                }
                else if (digit == "2" && !injectedOn2)
                {
                    injectedOn2 = true;
                    KeyBindings.InjectKeyDown(KeyBindings.GetKeys(4)[0]);
                }
            }

            if (digit == "1" && !shotTaken)
            {
                shotTaken = true;
                yield return Screenshot("382-countdown-1");
            }

            sinceStart += Time.unscaledDeltaTime;
            yield return null;
        }

        string sequence = string.Join("-", digits);
        Debug.Log(Tag + " countdown digits=" + sequence
            + " running=" + V6ResumeCountdown.IsRunning
            + " paused=" + PauseGate.IsPaused);
        // 順序まで見る (集合だけだと 1-2-3 でも通ってしまう)。
        Check("the countdown shows 3-2-1", sequence == "3-2-1");
        Check("the gate stays closed for the whole countdown", gateStayedClosed);
        Check("the chart clock is frozen during the countdown", stepFrozeDuringCountdown);
        Check("judgement keys during the countdown change nothing",
            lastPerfect == frozenPerfect && lastGreat == frozenGreat
            && lastBad == frozenBad && lastMiss == frozenMiss && lastScore == frozenScore);
        Check("the countdown ends", !V6ResumeCountdown.IsRunning);
        Check("the gate opens when the countdown ends", !PauseGate.IsPaused);

        // 再開の連続性: 譜面時計は固定値から跳ねずに再開する
        // (ResumeMediaFromGate が FrozenMediaTime から時計簿記をやり直す)。
        // 再開直後は映像プレイヤーの起動過渡で時刻が不連続に跳ぶことがある
        // (開始 ~0.3 秒でのポーズという端境期。定常状態の連続性は下の
        // steady-state チェックで厳密に確認する)。ここでは壊滅的な跳躍が
        // ないことだけを見る。
        float resumeStep = GManager.step_time;
        Debug.Log(Tag + " resume continuity frozen=" + frozenStep.ToString("F3")
            + " resumed=" + resumeStep.ToString("F3")
            + " mediaNow=" + (music != null ? music.MediaTimeNow.ToString("F3") : "n/a"));
        Check("the chart clock resumes near the frozen position",
            Mathf.Abs(resumeStep - frozenStep) < 1.5f);
        yield return new WaitForSecondsRealtime(0.4f);
        Check("the chart clock advances after the resume",
            GManager.step_time > frozenStep + 0.05f);
        // 追いつき矯正 (ResumeSyncSeconds) の効いたあとの定常状態:
        // 譜面時計は「固定時刻 + 再開からの経過」へ収束しているはず。
        // 起動過渡の残りを見込んで広めに取る (厳密な連続性は steady-state)。
        float settledStep = GManager.step_time;
        float expectedStep = frozenStep + 0.4f;
        Debug.Log(Tag + " resume settled step=" + settledStep.ToString("F3")
            + " expected~" + expectedStep.ToString("F3"));
        Check("the chart clock settles after the resume",
            Mathf.Abs(settledStep - expectedStep) < 1.5f);

        // 1.5 定常状態でのポーズ → 再開: 再生が安定してから (mediaTime > 2s)
        // ポーズして再開の連続性を確認する。開始直後のポーズは映像プレイヤーの
        // 起動過渡で時計が不連続になるため、#382 の受け入れは定常状態で行う。
        float steadyWait = 0f;
        while (steadyWait < 20f && (music == null || music.MediaTimeNow < 2f))
        {
            steadyWait += Time.unscaledDeltaTime;
            yield return null;
        }
        if (music != null && music.MediaTimeNow >= 2f)
        {
            pause.Open();
            yield return null;
            float steadyFrozen = GManager.step_time;
            pause.RequestResume();
            float steadyCdWait = 0f;
            while (steadyCdWait < 6f && (V6ResumeCountdown.IsRunning || PauseGate.IsPaused))
            {
                steadyCdWait += Time.unscaledDeltaTime;
                yield return null;
            }
            Check("the steady-state resume completes the countdown",
                !V6ResumeCountdown.IsRunning && !PauseGate.IsPaused);
            yield return null;
            float steadyResumed = GManager.step_time;
            Debug.Log(Tag + " steady resume frozen=" + steadyFrozen.ToString("F3")
                + " resumed=" + steadyResumed.ToString("F3"));
            Check("the chart clock resumes continuously in steady state",
                Mathf.Abs(steadyResumed - steadyFrozen) < 0.5f);
        }
        else
        {
            Check("media reached steady state for the second pause check", false);
        }

        // 2. Esc でポーズへ戻る (カウントダウンは取り消される)。
        pause.Open();
        yield return null;
        pause.RequestResume();
        yield return new WaitForSecondsRealtime(0.3f);
        KeyBindings.InjectKeyDown(KeyCode.Escape);
        yield return null;
        yield return null;
        Check("Esc during the countdown cancels it", !V6ResumeCountdown.IsRunning);
        Check("Esc during the countdown returns to the pause menu", pause.IsOpen);
        Check("the gate stays closed after the cancel", PauseGate.IsPaused);

        // #376/#375: Pause の Play タブからも NoteSpeed / NoteTiming / キー設定へ
        // 到達でき、値が保存されること (Option と同じ値モデル)。
        pause.SelectRowForAudit(0, 0); // Play タブ
        yield return null;
        int pSpeed = -1, pTiming = -1, pKey = -1;
        for (int i = 0; i < pause.RowCountForAudit(0); i++)
        {
            string label = pause.RowLabelForAudit(0, i);
            if (label == "NoteSpeed") { pSpeed = i; }
            else if (label == "NoteTiming") { pTiming = i; }
            else if (label == "キー設定") { pKey = i; }
        }
        Check("the pause play tab has NoteSpeed / NoteTiming / キー設定",
            pSpeed >= 0 && pTiming >= 0 && pKey >= 0);
        Check("the pause key config row shows no F2",
            pKey >= 0 && (pause.RowValueForAudit(0, pKey) ?? string.Empty).IndexOf("F2") < 0);

        float pauseSavedSpeed = GManager.noteSpeed;
        int pauseSavedTiming = GManager.noteTiming;
        if (pSpeed >= 0)
        {
            GManager.noteSpeed = 10f;
            float before = GManager.noteSpeed;
            pause.SelectRowForAudit(0, pSpeed);
            pause.AdjustSelectedForAudit(1);
            yield return null;
            float saved = PlayerPrefs.GetFloat(OptionSettings.NoteSpeedKey, float.NaN);
            Debug.Log(Tag + " pause notespeed " + before.ToString("F2") + " -> "
                + GManager.noteSpeed.ToString("F2") + " saved=" + saved.ToString("F2"));
            Check("NoteSpeed changes from the pause menu", GManager.noteSpeed != before);
            Check("NoteSpeed is saved from the pause menu",
                !float.IsNaN(saved) && Mathf.Abs(saved - GManager.noteSpeed) < 0.001f);
        }
        if (pTiming >= 0)
        {
            GManager.noteTiming = 0;
            pause.SelectRowForAudit(0, pTiming);
            pause.AdjustSelectedForAudit(1);
            yield return null;
            int saved = PlayerPrefs.GetInt(OptionSettings.NoteTimingKey, int.MinValue);
            Debug.Log(Tag + " pause notetiming -> " + GManager.noteTiming + " saved=" + saved);
            Check("NoteTiming changes from the pause menu", GManager.noteTiming == 1);
            Check("NoteTiming is saved from the pause menu", saved == GManager.noteTiming);
        }
        if (pKey >= 0)
        {
            pause.OpenKeyConfig();
            yield return null;
            var pauseModal = pause.KeyConfigModalForAudit;
            Check("the pause key config modal opens", pauseModal != null && pauseModal.IsOpen);
            Check("the pause modal blocks the background input", V6KeyConfigModal.BlocksInput);
            yield return Screenshot("376-pause-keyconfig");
            if (pauseModal != null)
            {
                pauseModal.Hide();
                yield return null;
            }
            Check("closing the pause modal releases the background input", !V6KeyConfigModal.BlocksInput);
        }
        GManager.noteSpeed = pauseSavedSpeed;
        GManager.noteTiming = pauseSavedTiming;
        OptionSceneManager.WriteNoteTimingToSongOffsets();
        OptionSettings.Save();
        pause.SelectRowForAudit(0, 0);

        // 3. 開始フレームの Esc は無視する (ポーズを Esc で閉じた直後の取り違え)。
        pause.RequestResume();
        KeyBindings.InjectKeyDown(KeyCode.Escape);
        yield return null;
        yield return null;
        Check("Esc on the frame the countdown starts is ignored", V6ResumeCountdown.IsRunning);

        V6ResumeCountdown.Abort();
        pause.Close();
        PauseGate.HardReset();
    }

    // ---------------------------------------------------------------- #377

    /// <summary>
    /// 譜面・ジャケットのキャッシュ (Issue #377)。Select を開き、別シーンへ
    /// 離れて戻ったときに Resources の読み直しが起きないことを、実読込回数で見る。
    /// </summary>
    private IEnumerator RunContentCache()
    {
        // 冷たい状態 (キャッシュ空) と温かい状態で同じ画面を開き、所要時間と
        // Resources の実読込回数を比べる。
        V6ContentCaches.InvalidateAll();
        float cold = 0f;
        yield return TimedSelect("cold", v => cold = v);
        var before = CacheCounts();

        yield return LoadScene("TitleScene");
        float warm = 0f;
        yield return TimedSelect("warm", v => warm = v);
        var after = CacheCounts();

        Debug.Log(Tag + " cache cold=" + cold.ToString("F2") + "s warm=" + warm.ToString("F2") + "s"
            + " first=" + before.Describe() + " second=" + after.Describe());

        Check("the first visit loads charts", before.ChartMetaLoads + before.ChartExistsLoads > 0);
        Check("the second visit reloads no chart meta",
            after.ChartMetaLoads == before.ChartMetaLoads);
        Check("the second visit reloads no chart existence",
            after.ChartExistsLoads == before.ChartExistsLoads);
        Check("the second visit reloads no jacket", after.JacketLoads == before.JacketLoads);
        Check("the jackets stay cached", after.JacketCount > 0 && after.JacketCount >= before.JacketCount);
    }

    /// <summary>Select シーンを開き、シーンが有効になって 2 フレーム経つまでの秒数を返す。</summary>
    private IEnumerator TimedSelect(string label, Action<float> report)
    {
        float start = Time.unscaledTime;
        SceneManager.LoadScene("SelectScene");
        while (SceneManager.GetActiveScene().name != "SelectScene") { yield return null; }
        yield return null;
        yield return null;
        float seconds = Time.unscaledTime - start;

        Check("the select scene is loaded (" + label + ")", V6SelectView.Current != null);
        report(seconds);
    }

    private struct Counts
    {
        public int ChartMetaLoads;
        public int ChartExistsLoads;
        public int JacketLoads;
        public int JacketCount;

        public string Describe()
        {
            return "chartMeta=" + ChartMetaLoads + " chartExists=" + ChartExistsLoads
                + " jacket=" + JacketLoads + "/cached=" + JacketCount;
        }
    }

    private static Counts CacheCounts()
    {
        return new Counts
        {
            ChartMetaLoads = V6ContentCaches.ChartMetaLoadCount,
            ChartExistsLoads = V6ContentCaches.ChartExistsLoadCount,
            JacketLoads = V6ContentCaches.JacketLoadCount,
            JacketCount = V6ContentCaches.JacketCount,
        };
    }

    // ---------------------------------------------------------------- helpers

    private IEnumerator LoadScene(string sceneName, float settleSeconds = SettleSeconds)
    {
        SceneManager.LoadScene(sceneName);
        yield return null;
        if (settleSeconds > 0f) { yield return new WaitForSecondsRealtime(settleSeconds); }
        if (SceneManager.GetActiveScene().name != sceneName)
        {
            Check("the scene " + sceneName + " is loaded", false);
            yield break;
        }

        Check("the scene " + sceneName + " is loaded", true);
    }

    /// <summary>Result の View (ドライバ経由。View は MonoBehaviour ではない)。</summary>
    private static V6ResultView FindResultView()
    {
        var driver = FindFirstObjectByType<V6ResultViewDriver>();
        return driver != null ? driver.View : null;
    }

    private static PointerEventData Pointer(V6VolumeSlider slider, float value)
    {
        // pressEventCamera は読み取り専用 (Enter/押下時に設定される)。
        // Screen Space Overlay のキャンバスでは null のままで正しく変換される。
        return new PointerEventData(EventSystem.current)
        {
            position = slider.ScreenPointForValue(value),
        };
    }

    /// <summary>
    /// 実ポインタと同じ順序で配る。EventSystem のレイキャストで最前面の当たり先を取り、
    /// スライダの内側ならそのオブジェクトから親へ向かってハンドラを実行する。
    /// 手前に別の UI が被さっていれば当たり先がスライダの外になり、何も起きない。
    /// </summary>
    private static bool DispatchPointer<T>(V6VolumeSlider slider, float value,
        ExecuteEvents.EventFunction<T> handler) where T : IEventSystemHandler
    {
        if (slider == null || EventSystem.current == null)
        {
            return false;
        }

        var pointer = Pointer(slider, value);
        var results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointer, results);
        if (results.Count == 0)
        {
            return false;
        }

        GameObject hit = results[0].gameObject;
        if (hit == null)
        {
            return false;
        }

        if (hit != slider.gameObject && !hit.transform.IsChildOf(slider.transform))
        {
            return false;
        }

        ExecuteEvents.ExecuteHierarchy(hit, pointer, handler);
        return true;
    }

    private static string Percent(float value)
    {
        return Mathf.RoundToInt(value * 100f) + "%";
    }

    private IEnumerator Screenshot(string label)
    {
        yield return new WaitForEndOfFrame();
        shot++;
        string path = Path.Combine(outDir, "v6interact-" + label + ".png");
        ScreenCapture.CaptureScreenshot(path);

        // 書き出しは非同期。0.6 秒の固定待ちだと、直後にシーンを切り替えたときに
        // ファイルが現れる前に判定してしまうことがある (実測で 1 回)。
        float waited = 0f;
        while (!File.Exists(path) && waited < 5f)
        {
            waited += Time.unscaledDeltaTime;
            yield return null;
        }

        if (!File.Exists(path))
        {
            // 1 度だけ撮り直す (フレーム境界をまたいだ取りこぼし対策)。
            ScreenCapture.CaptureScreenshot(path);
            yield return new WaitForEndOfFrame();
            waited = 0f;
            while (!File.Exists(path) && waited < 5f)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        Check("the screenshot " + label + " was written", File.Exists(path));
    }

    private bool Check(string label, bool ok)
    {
        if (ok) { pass++; }
        else { fail++; }

        string line = (ok ? "PASS " : "FAIL ") + label;
        lines.Add(line);
        Debug.Log(Tag + " " + line);
        return ok;
    }

    private void WriteResult()
    {
        string result = Tag + " RESULT pass=" + pass + " fail=" + fail
            + " screenshots=" + shot
            + " resolution=" + Screen.width + "x" + Screen.height;
        lines.Add(result);
        Debug.Log(result);

        try
        {
            File.WriteAllText(Path.Combine(outDir, "autotest-v6interact-result.txt"),
                string.Join(Environment.NewLine, lines.ToArray()) + Environment.NewLine);
        }
        catch (Exception e)
        {
            Debug.LogWarning(Tag + " could not write the result file: " + e.Message);
        }
    }
}
