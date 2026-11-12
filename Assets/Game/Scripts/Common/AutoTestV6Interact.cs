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
///   - #384 Result の段階表示: 表示が時間とともに進む / 2 秒で登録欄が自動で開く /
///     Space で残りを打ち切る (スキップ)。
///   - #382 再開カウントダウン: 3 → 2 → 1 が出る / その間ポーズゲートは閉じた
///     まま / 3 秒で再開する / Esc でポーズへ戻る / 開始フレームの Esc は無視する。
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

        yield return RunVolumeSlider();
        yield return RunResultReveal();
        yield return RunResumeCountdown();
        yield return RunContentCache();

        WriteResult();
        Application.Quit();
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
        slider.OnPointerDown(Pointer(slider, 0.20f));
        yield return null;
        readouts.Append("down=").Append(Percent(slider.Value)).Append(' ');

        float[] dragTo = { 0.35f, 0.55f, 0.70f };
        for (int i = 0; i < dragTo.Length; i++)
        {
            slider.OnDrag(Pointer(slider, dragTo[i]));
            yield return null;
            readouts.Append("drag=").Append(Percent(slider.Value)).Append(' ');
        }

        yield return Screenshot("375-slider-drag");

        slider.OnPointerUp(Pointer(slider, 0.80f));
        yield return null;
        readouts.Append("up=").Append(Percent(slider.Value));
        Debug.Log(Tag + " slider " + readouts);

        Check("the drag changes the value to where the pointer is", Mathf.Abs(slider.Value - 0.80f) <= 0.02f);
        Check("the value owner follows the drag", Mathf.Abs(GManager.mainVolume - 0.80f) <= 0.02f);
        Check("the value label follows the drag", slider.ValueLabelForAudit == "80%");
        Check("the knob follows the drag",
            Mathf.Abs(slider.KnobCenterForAudit - V6VolumeSlider.KnobCenter(0.80f)) <= 1.5f);

        float saved = PlayerPrefs.GetFloat(OptionSettings.MainVolumeKey, -1f);
        Check("releasing the pointer saves the volume", Mathf.Abs(saved - 0.80f) <= 0.02f);

        // 猶予 (0.5 秒) の途中で画面を離れる: 最後の値が保存されること。
        slider.OnPointerDown(Pointer(slider, 0.30f));
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
        float firstTrophyAlpha = -1f;
        float lastTrophyAlpha = -1f;
        bool completed = false;

        while (view.RevealElapsedForAudit < 2.6f)
        {
            float elapsed = view.RevealElapsedForAudit;
            float alpha = view.TrophyAlphaForAudit;
            if (elapsed < 1.9f && !view.RevealCompleteForAudit)
            {
                sawBeforeComplete = true;
            }

            if (firstTrophyAlpha < 0f) { firstTrophyAlpha = alpha; }
            lastTrophyAlpha = alpha;
            if (alpha > 0f && alpha < 1f) { sawTrophyFadeIn = true; }
            if (view.ModalOpenForAudit) { autoRegisterOpened = true; }
            if (view.RevealCompleteForAudit) { completed = true; }

            if (autoRegisterOpened && completed) { break; }
            yield return null;
        }

        Debug.Log(Tag + " reveal elapsed=" + view.RevealElapsedForAudit.ToString("F2")
            + " complete=" + view.RevealCompleteForAudit
            + " modal=" + view.ModalOpenForAudit
            + " trophyAlpha=" + view.TrophyAlphaForAudit.ToString("F2"));

        Check("the reveal is staged (not everything at once)", sawBeforeComplete);
        Check("the trophy fades in gradually", sawTrophyFadeIn || firstTrophyAlpha < lastTrophyAlpha);
        Check("the register field opens automatically at 2s", autoRegisterOpened);
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

        // 段階表示の途中 (1.0 秒未満) になるまで待ってから押す。
        while (view.RevealElapsedForAudit < 0.8f) { yield return null; }

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
        float waited = 0f;
        while (waited < 15f && (pause == null || hud == null))
        {
            pause = V6PauseView.Current;
            hud = FindFirstObjectByType<V6GameHudView>();
            waited += Time.unscaledDeltaTime;
            yield return null;
        }

        if (!Check("pause view exists", pause != null)) { yield break; }
        if (!Check("game hud exists", hud != null)) { yield break; }

        // 1. 通常の再開: 3 → 2 → 1 の後にゲートが開く。
        pause.Open();
        yield return null;
        Check("opening the pause closes the gate", PauseGate.IsPaused);

        pause.RequestResume();
        Check("resume starts the countdown", V6ResumeCountdown.IsRunning);

        var digits = new List<string>();
        bool gateStayedClosed = true;
        bool shotTaken = false;
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

            if (V6ResumeCountdown.IsRunning && !PauseGate.IsPaused) { gateStayedClosed = false; }
            // 判定ゲートも閉じたままであること (#382): ゲートはポーズ中だけでなく
            // 曲終了の表示中も閉じるので、判定側の合成 (CanJudge) で確かめる。
            if (V6ResumeCountdown.IsRunning
                && SongEndGate.CanJudge(PauseGate.IsPaused, SongEndGate.BlocksJudging))
            {
                gateStayedClosed = false;
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
        Check("the countdown shows 3-2-1", sequence.Contains("3") && sequence.Contains("2")
            && sequence.Contains("1") && digits.Count >= 3);
        Check("the gate stays closed for the whole countdown", gateStayedClosed);
        Check("the countdown ends", !V6ResumeCountdown.IsRunning);
        Check("the gate opens when the countdown ends", !PauseGate.IsPaused);

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
