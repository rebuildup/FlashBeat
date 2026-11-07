using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Verification aid (Mac, no OS input permission needed): with "-autotest loading" the player
/// shows the media loading overlay ("読み込み中") and checks that it is really on screen,
/// that the status line follows, that the spinner animates, and that Hide() takes it away.
/// Reproduces the owner's request: "動画のダウンロードを読み込み中表示にしたい".
/// </summary>
public class AutoTestLoading : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (GetArg("-autotest") != "loading") { return; }
        var go = new GameObject("AutoTestLoading");
        DontDestroyOnLoad(go);
        go.AddComponent<AutoTestLoading>();
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

        MediaLoadingOverlay overlay = MediaLoadingOverlay.Ensure();
        Check("the overlay was created", overlay != null);

        if (overlay != null)
        {
            Check("the overlay is hidden before the download starts", !overlay.IsVisible);

            overlay.Show("動画をダウンロード中…");
            yield return null;
            Check("the overlay is visible while downloading", overlay.IsVisible);
            Check("the title says 読み込み中", overlay.TitleText.Contains("読み込み中"));
            Check("the status line shows the phase", overlay.StatusText.Contains("ダウンロード"));
            Check("the overlay does not block input", !BlocksRaycasts());

            float angle0 = overlay.SpinnerAngle;
            for (int i = 0; i < 8; i++) { yield return null; }
            Check("the spinner animates", overlay.SpinnerAngle != angle0);

            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(outDir, "autotest-loading.png"));

            overlay.Show("音声を準備中…");
            yield return null;
            Check("the status line follows the phase", overlay.StatusText.Contains("音声"));

            overlay.Hide();
            yield return null;
            Check("the overlay is hidden after the media is ready", !overlay.IsVisible);
        }

        Debug.Log("[AutoTest] RESULT loading pass=" + pass + " fail=" + fail);
        try
        {
            File.WriteAllText(Path.Combine(outDir, "autotest-result.txt"),
                "[AutoTest] RESULT loading pass=" + pass + " fail=" + fail + "\n");
        }
        catch (Exception e) { Debug.LogWarning("[AutoTest] could not write the result file: " + e.Message); }

        yield return new WaitForSecondsRealtime(0.5f);
        Application.Quit();
    }

    /// <summary>オーバーレイがクリックを奪っていないか (Graphic の raycastTarget を確認)。</summary>
    private static bool BlocksRaycasts()
    {
        var images = Resources.FindObjectsOfTypeAll<UnityEngine.UI.Graphic>();
        for (int i = 0; i < images.Length; i++)
        {
            var graphic = images[i];
            if (graphic == null) continue;
            if (!graphic.gameObject.activeInHierarchy) continue;
            if (graphic.transform.root == null) continue;
            if (graphic.transform.root.name != "MediaLoadingOverlay") continue;
            if (graphic.raycastTarget) return true;
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
