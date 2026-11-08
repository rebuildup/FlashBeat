using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using FlashBeat.UI.V6.Select;
using FlashBeat.UI.V6.Select.View;

/// <summary>
/// v6 選曲→Ready→ゲーム の実データ経路を検証する (Issue #223)。
///
/// 使い方: プレイヤー引数に "-autotest v6" と "-autotest-out &lt;dir&gt;" を渡す。
/// 選曲画面で実カタログの曲を選び、Ready でゲーム画面へ遷移したことを
/// スクリーンショット付きで確認する。
/// </summary>
public class AutoTestV6 : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (GetArg("-autotest") != "v6") { return; }
        var go = new GameObject("AutoTestV6");
        DontDestroyOnLoad(go);
        go.AddComponent<AutoTestV6>();
    }

    private int pass;
    private int fail;
    private int shot;
    private string outDir;

    private IEnumerator Start()
    {
        outDir = GetArg("-autotest-out");
        if (string.IsNullOrEmpty(outDir)) { outDir = Application.persistentDataPath; }
        try { Directory.CreateDirectory(outDir); }
        catch (Exception) { }

        // 1. 選曲画面 (v6 の 3 カラム) が立ち上がる。
        SceneManager.LoadScene("SelectScene");
        yield return new WaitForSecondsRealtime(5f);
        Check("the select scene is loaded", SceneManager.GetActiveScene().name == "SelectScene");

        V6SelectView view = V6SelectView.Current;
        Check("the v6 select view exists", view != null);
        Check("the select screen lists real songs", view != null && view.VisibleSongCount > 0);
        yield return Screenshot("01-select");

        if (view == null)
        {
            Finish();
            yield break;
        }

        IV6SelectDataSource data = view.Data;
        Check("the data source is present", data != null);

        // 2. 実カタログから曲を選ぶ (固定データではないことを確かめる)。
        IReadOnlyList<V6SongEntry> songs = data.AllSongs;
        Check("the catalog has more than one song", songs != null && songs.Count > 1);

        V6SongEntry target = null;
        for (int i = 0; i < songs.Count; i++)
        {
            if (songs[i] != null && !string.IsNullOrEmpty(songs[i].Title)) { target = songs[i]; break; }
        }
        Check("a song with a real title exists", target != null);

        if (target != null)
        {
            data.Select(target.SongId);
            yield return new WaitForSecondsRealtime(0.4f);
            Check("the selection follows the picked song", data.Selected != null && data.Selected.SongId == target.SongId);
            Check("the selected song has a title", data.Selected != null && !string.IsNullOrEmpty(data.Selected.Title));
            yield return Screenshot("02-select-selected");
        }

        // 3. Ready → ゲーム画面 (実セッションとして遷移する)。
        int expectedSongId = data.Selected == null ? -1 : data.Selected.SongId;
        view.TriggerReady();

        float waited = 0f;
        while (waited < 12f)
        {
            if (SceneManager.GetActiveScene().name == "GameScene") { break; }
            waited += 0.5f;
            yield return new WaitForSecondsRealtime(0.5f);
        }
        Check("ready starts the game scene", SceneManager.GetActiveScene().name == "GameScene");
        Check("the started song is the picked one", GManager.songID == expectedSongId);
        yield return new WaitForSecondsRealtime(3f);
        yield return Screenshot("03-game");

        // 4. v6 のプレイフィールドと HUD が scene にある。
        Check("the v6 playfield exists", FlashBeat.UI.V6.Game.Playfield.V6PlayfieldView.Current != null);
        Check("the v6 hud exists", FindFirstObjectByType<FlashBeat.UI.V6.Game.Hud.V6GameHudView>() != null);

        Finish();
    }

    private void Finish()
    {
        Debug.Log("[AutoTestV6] RESULT pass=" + pass + " fail=" + fail);
        try
        {
            File.WriteAllText(Path.Combine(outDir, "autotest-v6-result.txt"),
                "[AutoTestV6] RESULT pass=" + pass + " fail=" + fail + "\n");
        }
        catch (Exception e) { Debug.LogWarning("[AutoTestV6] could not write the result file: " + e.Message); }

        Application.Quit();
    }

    private IEnumerator Screenshot(string name)
    {
        yield return new WaitForEndOfFrame();
        shot++;
        string path = Path.Combine(outDir, string.Format("autotest-v6-{0}.png", shot));
        ScreenCapture.CaptureScreenshot(path);
        yield return new WaitForSecondsRealtime(0.6f);
        Check("the screenshot " + name + " was written", File.Exists(path));
    }

    private void Check(string label, bool ok)
    {
        if (ok) { pass++; Debug.Log("[AutoTestV6] PASS " + label); }
        else { fail++; Debug.Log("[AutoTestV6] FAIL " + label); }
    }

    private static string GetArg(string key)
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == key) { return args[i + 1]; }
        }
        return null;
    }
}
