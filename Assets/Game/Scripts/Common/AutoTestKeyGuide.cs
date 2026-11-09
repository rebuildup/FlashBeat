using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Verification aid (Mac, no OS input permission needed): with "-autotest keyguide" the player
/// opens the title screen, finds the key guide, shows it and checks that all 8 lanes are listed
/// with their keys and that a Japanese font is used (no tofu boxes).
/// Reproduces the owner's request: "キーガイドの追加".
/// </summary>
public class AutoTestKeyGuide : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (GetArg("-autotest") != "keyguide") { return; }
        var go = new GameObject("AutoTestKeyGuide");
        DontDestroyOnLoad(go);
        go.AddComponent<AutoTestKeyGuide>();
    }

    private int pass;
    private int fail;
    private string outDir;

    private IEnumerator Start()
    {
        outDir = GetArg("-autotest-out");
        if (string.IsNullOrEmpty(outDir)) { outDir = Application.persistentDataPath; }
        try { Directory.CreateDirectory(outDir); } catch (Exception) { }

        SceneManager.LoadScene("TitleScene");
        yield return new WaitForSecondsRealtime(4f);
        Check("on TitleScene", SceneManager.GetActiveScene().name == "TitleScene");

        KeyGuideUI guide = UnityEngine.Object.FindFirstObjectByType<KeyGuideUI>();
        Check("the key guide was created on the title screen", guide != null);

        if (guide != null)
        {
            for (int lane = 0; lane < KeyBindings.DefaultKeys.Length; lane++)
            {
                string row = guide.GetRowLabel(lane);
                bool ok = !string.IsNullOrEmpty(row);
                if (ok)
                {
                    foreach (KeyCode key in Judge.LaneKeys[lane])
                    {
                        if (!row.Contains(KeyGuideUI.GetKeyDisplayName(key))) { ok = false; break; }
                    }
                }
                Check("lane " + (lane + 1) + " lists its keys", ok);
            }

            guide.SetVisible(true);
            yield return new WaitForEndOfFrame();
            Check("the guide can be shown", guide.IsVisible);

            var labels = guide.GetComponentsInChildren<TMPro.TextMeshProUGUI>(true);
            bool hasJapaneseFont = false;
            bool hasJapaneseText = false;
            for (int i = 0; i < labels.Length; i++)
            {
                if (labels[i] == null) { continue; }
                if (labels[i].font != null && labels[i].font.name != null &&
                    (labels[i].font.name.Contains("Noto") || labels[i].font.name.Contains("YuGoth")))
                {
                    hasJapaneseFont = true;
                }
                if (!string.IsNullOrEmpty(labels[i].text) && labels[i].text.Contains("キーガイド"))
                {
                    hasJapaneseText = true;
                }
            }
            Check("the guide uses a Japanese font", hasJapaneseFont);
            Check("the guide shows a Japanese title", hasJapaneseText);

            ScreenCapture.CaptureScreenshot(Path.Combine(outDir, "autotest-keyguide.png"));
            yield return new WaitForSecondsRealtime(0.5f);
        }

        Debug.Log("[AutoTest] RESULT keyguide pass=" + pass + " fail=" + fail);
        try
        {
            File.WriteAllText(Path.Combine(outDir, "autotest-result.txt"),
                "[AutoTest] RESULT keyguide pass=" + pass + " fail=" + fail + "\n");
        }
        catch (Exception e) { Debug.LogWarning("[AutoTest] could not write the result file: " + e.Message); }

        yield return new WaitForSecondsRealtime(0.5f);
        Application.Quit();
    }

    private void Check(string label, bool ok)
    {
        if (ok) { pass++; Debug.Log("[AutoTest] PASS " + label); }
        else { fail++; Debug.Log("[AutoTest] FAIL " + label); }
    }

    private static string GetArg(string key)
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == key) return args[i + 1];
        }
        return null;
    }
}
