using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Verification aid (Mac): with "-autotest play" the player walks through the whole
/// game the way an exhibition visitor would - title screen, key guide, song select,
/// media download, playback - and screenshots every step.
///
/// The owner's request: "実際に使いたい。UIなども含めてMac確認に入れて" - the game has
/// to be really playable on the Mac, with the whole UI visible, not just a unit test.
/// </summary>
public class AutoTestPlay : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (GetArg("-autotest") != "play") { return; }
        var go = new GameObject("AutoTestPlay");
        DontDestroyOnLoad(go);
        go.AddComponent<AutoTestPlay>();
    }

    private int pass;
    private int fail;
    private string outDir;
    private int shot;

    private IEnumerator Start()
    {
        outDir = GetArg("-autotest-out");
        if (string.IsNullOrEmpty(outDir)) { outDir = Application.persistentDataPath; }
        try { Directory.CreateDirectory(outDir); } catch (Exception) { }

        // 1. Title screen.
        SceneManager.LoadScene("TitleScene");
        yield return new WaitForSecondsRealtime(4f);
        Check("the title screen is shown", SceneManager.GetActiveScene().name == "TitleScene");
        Check("the title screen has a canvas", FindObjectOfType<Canvas>() != null);
        yield return Screenshot("01-title");

        // 2. The key guide opens on the title screen.
        KeyGuideUI guide = FindFirstObjectByType<KeyGuideUI>();
        Check("the key guide exists on the title screen", guide != null);
        if (guide != null)
        {
            guide.SetVisible(true);
            yield return new WaitForSecondsRealtime(0.4f);
            Check("the key guide opens", guide.IsVisible);
            yield return Screenshot("02-keyguide");
            guide.SetVisible(false);
        }

        // 3. Song select: the guide must exist there too.
        SceneManager.LoadScene("SelectScene");
        yield return new WaitForSecondsRealtime(4f);
        Check("the select screen is shown", SceneManager.GetActiveScene().name == "SelectScene");
        Check("the key guide exists on the select screen", FindFirstObjectByType<KeyGuideUI>() != null);
        yield return Screenshot("03-select");

        // 4. Game scene: the media is downloaded and prepared.
        GManager.songID = 1;
        GManager.ResetSession();
        SceneManager.LoadScene("GameScene");
        yield return new WaitForSecondsRealtime(3f);
        Check("the game scene is shown", SceneManager.GetActiveScene().name == "GameScene");

        MusicManager music = FindFirstObjectByType<MusicManager>();
        Check("the music manager exists", music != null);

        // The download of a YouTube video takes a while; the loading screen has to
        // be on screen while it happens.
        bool sawLoading = false;
        float waited = 0f;
        while (waited < 240f)
        {
            var overlay = FindFirstObjectByType<MediaLoadingOverlay>();
            if (overlay != null && overlay.IsVisible) { sawLoading = true; }
            if (music != null && music.MediaReady) { break; }
            waited += 1f;
            yield return new WaitForSecondsRealtime(1f);
        }
        if (sawLoading) { yield return Screenshot("04-loading"); }
        Check("the loading screen was shown while the media was prepared", sawLoading);
        Check("the media is ready", music != null && music.MediaReady);
        Check("the video player is prepared", music != null && music.VideoPrepared);
        yield return Screenshot("05-med ready");

        // 5. Playback starts and the media clock runs.
        if (music != null && music.MediaReady)
        {
            KeyBindings.InjectKeyDown(KeyCode.Space);
            yield return new WaitForSecondsRealtime(1f);
            Check("the playback started", music.PlaybackStarted);

            float before = music.MediaTimeNow;
            yield return new WaitForSecondsRealtime(3f);
            Check("the media clock advances", music.MediaTimeNow > before);
            Check("the video plays", music.VideoPlaying);
            yield return Screenshot("06-gameplay");
        }

        Debug.Log("[AutoTest] RESULT play pass=" + pass + " fail=" + fail);
        try
        {
            File.WriteAllText(Path.Combine(outDir, "autotest-play-result.txt"),
                "[AutoTest] RESULT play pass=" + pass + " fail=" + fail + "\n");
        }
        catch (Exception e) { Debug.LogWarning("[AutoTest] could not write the result file: " + e.Message); }

        yield return new WaitForSecondsRealtime(0.5f);
        Application.Quit();
    }

    private IEnumerator Screenshot(string name)
    {
        yield return new WaitForEndOfFrame();
        shot++;
        string path = Path.Combine(outDir, string.Format("autotest-play-{0}.png", shot));
        ScreenCapture.CaptureScreenshot(path);
        yield return new WaitForSecondsRealtime(0.6f);
        Check("the screenshot " + name + " was written", File.Exists(path));
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
