using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Verification aid: when the player is started with "-autoshot &lt;dir&gt;" it saves a PNG of the game view
/// every few seconds ("-autoshot-interval &lt;sec&gt;", default 8; "-autoshot-count &lt;n&gt;", default 8).
/// Needs no OS screen-recording permission (the app captures itself) and is completely inert without the flag.
/// Used by script/mac-verify run and the Windows drive scripts.
/// </summary>
public class AutoShot : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        string dir = GetArg("-autoshot");
        if (string.IsNullOrEmpty(dir)) { return; }
        var go = new GameObject("AutoShot");
        DontDestroyOnLoad(go);
        var shot = go.AddComponent<AutoShot>();
        shot.directory = dir;
        shot.interval = ParseFloat(GetArg("-autoshot-interval"), 8f);
        shot.count = Mathf.Max(1, (int)ParseFloat(GetArg("-autoshot-count"), 8f));
    }

    private string directory;
    private float interval = 8f;
    private int count = 8;

    private IEnumerator Start()
    {
        try { Directory.CreateDirectory(directory); }
        catch (Exception e) { Debug.LogWarning("[AutoShot] cannot create " + directory + ": " + e.Message); yield break; }
        for (int i = 0; i < count; i++)
        {
            yield return new WaitForSecondsRealtime(interval);
            yield return new WaitForEndOfFrame();
            string scene = SceneManager.GetActiveScene().name;
            string path = Path.Combine(directory, "shot-" + i.ToString("00") + "-" + scene + ".png");
            ScreenCapture.CaptureScreenshot(path);
            Debug.Log("[AutoShot] " + path + " (" + Screen.width + "x" + Screen.height + ", fullscreen=" + Screen.fullScreen + ")");
        }
    }

    private static string GetArg(string name)
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == name) { return args[i + 1]; }
        }
        return null;
    }

    private static float ParseFloat(string text, float fallback)
    {
        float value;
        return float.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out value) ? value : fallback;
    }
}
