using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Verification aid (Mac, no OS input permission needed): with "-autotest overlay" the player
/// checks that no dimming overlay is covering the screen when nothing is open.
/// Reproduces the owner's report: "after updating, the screen is dark; only the ranking and the key
/// config are drawn on top and the controls behind them are mostly hidden".
/// Cause: the ranking overlays built a full-screen black backdrop as the PARENT of the board panel
/// and only hid the inner panel, so the backdrop stayed active from the first frame.
/// </summary>
public class AutoTestOverlay : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (GetArg("-autotest") != "overlay") { return; }
        var go = new GameObject("AutoTestOverlay");
        DontDestroyOnLoad(go);
        go.AddComponent<AutoTestOverlay>();
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

        Check("no ranking board backdrop is covering the screen", !IsActive("RankingBoard"));
        Check("no ranking modal backdrop is covering the screen", !IsActive("RankingSubmitModal"));
        Check("no key config modal is covering the screen", !IsActive("KeyConfigModal"));

        // スクリーンショットはフレーム末で撮る必要がある。
        yield return new WaitForEndOfFrame();
        float brightness = MeasureBrightness();
        Debug.Log("[AutoTest] INFO average screen luminance = " + brightness.ToString("F1"));
        Check("the screen is not dimmed (luminance >= 25)", brightness >= 25f);
        SaveShot("00-title");

        Debug.Log("[AutoTest] RESULT overlay pass=" + pass + " fail=" + fail);
        try
        {
            File.WriteAllText(Path.Combine(outDir, "autotest-overlay-result.txt"),
                "pass=" + pass + " fail=" + fail + "\nluminance=" + brightness.ToString("F1") + "\n");
        }
        catch (Exception e) { Debug.LogWarning("[AutoTest] could not write the result file: " + e.Message); }

        yield return new WaitForSecondsRealtime(0.5f);
        Application.Quit();
    }

    /// <summary>指定名の GameObject が有効かどうか (どの親の下でも探す)。</summary>
    private static bool IsActive(string objectName)
    {
        GameObject[] all = Resources.FindObjectsOfTypeAll<GameObject>();
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] == null) continue;
            if (all[i].name != objectName) continue;
            if (all[i].activeInHierarchy) return true;
        }
        return false;
    }

    /// <summary>画面全体の平均輝度 (0-255)。16 ピクセルごとにサンプルする。</summary>
    private float MeasureBrightness()
    {
        Texture2D texture = ScreenCapture.CaptureScreenshotAsTexture();
        if (texture == null) return 0f;
        Color[] pixels = texture.GetPixels();
        float sum = 0f;
        int count = 0;
        for (int i = 0; i < pixels.Length; i += 16)
        {
            Color c = pixels[i];
            sum += 0.299f * c.r + 0.587f * c.g + 0.114f * c.b;
            count++;
        }
        Destroy(texture);
        if (count == 0) return 0f;
        return (sum / count) * 255f;
    }

    private void SaveShot(string label)
    {
        try
        {
            ScreenCapture.CaptureScreenshot(Path.Combine(outDir, label + ".png"));
        }
        catch (Exception e) { Debug.LogWarning("[AutoTest] screenshot failed: " + e.Message); }
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
