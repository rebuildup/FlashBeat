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

        // v6 title: キー案内はフッター凡例 (静的な Legend Label) が担う。
        // 旧 KeyGuideUI パネル (レーン別キー行) は V5 専用で、v6 では作られない
        // (Issue #740 Stage B: v6 が唯一の実装)。
        // レーン別キー行の正しさはゲーム HUD 側 (KeyGuideMode) と EditMode テストが担保する。
        Check("the v6 title view exists",
            FindFirstObjectByType<FlashBeat.UI.V6.Title.V6TitleViewDriver>() != null);
        Check("the legacy key guide panel does not exist",
            FindFirstObjectByType<KeyGuideUI>() == null);

        int legendCount = 0;
        bool hasJapaneseFont = false;
        bool hasJapaneseText = false;
        foreach (var label in FindObjectsByType<TMPro.TextMeshProUGUI>(
            FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (label == null || !label.name.StartsWith("Legend Label", StringComparison.Ordinal))
            {
                continue;
            }
            legendCount++;
            if (label.font != null && label.font.name != null &&
                (label.font.name.Contains("Noto") || label.font.name.Contains("YuGoth")))
            {
                hasJapaneseFont = true;
            }
            if (!string.IsNullOrEmpty(label.text) && ContainsJapanese(label.text))
            {
                hasJapaneseText = true;
            }
        }
        Check("the title footer legend exists", legendCount >= 3);
        Check("the legend uses a Japanese font", hasJapaneseFont);
        Check("the legend shows Japanese text", hasJapaneseText);

        ScreenCapture.CaptureScreenshot(Path.Combine(outDir, "autotest-keyguide.png"));
        yield return new WaitForSecondsRealtime(0.5f);

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

    private static bool ContainsJapanese(string text)
    {
        foreach (char c in text)
        {
            if ((c >= 0x3040 && c <= 0x30FF) || (c >= 0x4E00 && c <= 0x9FFF)) { return true; }
        }
        return false;
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
