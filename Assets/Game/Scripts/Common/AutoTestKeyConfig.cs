using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

/// <summary>
/// Verification aid (Mac, no OS input permission needed): with "-autotest keyconfig" the player drives the key config
/// overlay itself - injected keys + a real GraphicRaycaster hit test + a click event - and logs [AutoTest] PASS/FAIL
/// lines, then quits. Inert without the flag. Reproduces the owner's report: "cannot move to another lane while
/// assigning; arrows and clicks do nothing".
/// </summary>
public class AutoTestKeyConfig : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (GetArg("-autotest") != "keyconfig") { return; }
        var go = new GameObject("AutoTestKeyConfig");
        DontDestroyOnLoad(go);
        go.AddComponent<AutoTestKeyConfig>();
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

        KeyBindings.ResetToDefaults();
        KeyConfigUI.InjectKeyDown(KeyCode.F2);
        yield return Frames();
        Check("F2 opens the key config", KeyConfigUI.IsOpen);
        Check("scene manager is suspended while open (E/O/Space must not act)", !AnySceneManagerEnabled());
        yield return Shot("01-open");

        KeyConfigUI.InjectKeyDown(KeyCode.DownArrow);
        yield return Frames();
        Check("Down selects lane 2 (not listening)", KeyConfigUI.TestSelectedLane == 1 && !KeyConfigUI.TestListening);

        KeyConfigUI.InjectKeyDown(KeyCode.Return);
        yield return Frames();
        Check("Enter starts listening", KeyConfigUI.TestListening);
        yield return Shot("02-listening");

        // the owner's bug: while listening, arrows did nothing
        KeyConfigUI.InjectKeyDown(KeyCode.DownArrow);
        yield return Frames();
        Check("Down WHILE LISTENING moves to lane 3 and stops listening", KeyConfigUI.TestSelectedLane == 2 && !KeyConfigUI.TestListening);

        KeyConfigUI.InjectKeyDown(KeyCode.UpArrow);
        yield return Frames();
        Check("Up moves back to lane 2", KeyConfigUI.TestSelectedLane == 1);

        // click on lane 6: real raycast at the row centre, then the click event
        GameObject row = KeyConfigUI.TestRowObject(5);
        Check("lane 6 row exists", row != null);
        if (row != null && EventSystem.current != null)
        {
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(null, row.transform.position);
            var pointer = new PointerEventData(EventSystem.current) { position = screen };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            bool hitRow = false;
            for (int i = 0; i < hits.Count; i++)
            {
                if (hits[i].gameObject != null && (hits[i].gameObject == row || hits[i].gameObject.transform.IsChildOf(row.transform))) { hitRow = true; break; }
            }
            Check("a pointer at the centre of lane 6 hits the row (raycaster + EventSystem present)", hitRow);
            ExecuteEvents.Execute(row, pointer, ExecuteEvents.pointerClickHandler);
            yield return Frames();
            Check("click selects lane 6 and starts listening", KeyConfigUI.TestSelectedLane == 5 && KeyConfigUI.TestListening);
        }
        else
        {
            Check("EventSystem exists for clicks", EventSystem.current != null);
        }
        yield return Shot("03-clicked-lane6");

        KeyConfigUI.InjectKeyDown(KeyCode.E);
        yield return Frames();
        Check("E assigned to lane 6 while the app keeps running", Contains(KeyBindings.GetKeys(5), KeyCode.E));

        KeyConfigUI.InjectKeyDown(KeyCode.Escape);
        yield return Frames();
        Check("Esc stops listening", !KeyConfigUI.TestListening);
        KeyBindings.ResetToDefaults();
        KeyConfigUI.InjectKeyDown(KeyCode.F2);
        yield return Frames();
        Check("F2 closes", !KeyConfigUI.IsOpen);
        Check("scene manager resumed after closing", AnySceneManagerEnabled());
        yield return Shot("04-closed");

        // The lane flash must follow the CUSTOM bindings (owner: "the lit lane is wrong").
        KeyBindings.ResetToDefaults();
        KeyBindings.ToggleKey(5, KeyCode.E); // E moves from lane 3 to lane 6
        SceneManager.LoadScene("GameScene");
        yield return new WaitForSecondsRealtime(4f);
        LaneFlash[] flashes = FindObjectsOfType<LaneFlash>();
        Check("GameScene has the 8 lane flashes", flashes.Length >= 8);
        KeyBindings.InjectKeyDown(KeyCode.E);
        yield return Frames();
        float alpha3 = 0f;
        float alpha6 = 0f;
        for (int i = 0; i < flashes.Length; i++)
        {
            Renderer r = flashes[i].GetComponent<Renderer>();
            if (r == null) { continue; }
            if (flashes[i].LaneNumber == 3) { alpha3 = r.material.color.a; }
            if (flashes[i].LaneNumber == 6) { alpha6 = r.material.color.a; }
        }
        Check("E (bound to lane 6) lights lane 6", alpha6 > 0.1f);
        Check("E no longer lights lane 3", alpha3 < 0.05f);
        yield return Shot("05-game-flash");
        KeyBindings.ResetToDefaults();

        string line = "[AutoTest] RESULT keyconfig pass=" + pass + " fail=" + fail;
        Debug.Log(line);
        try { File.WriteAllText(Path.Combine(outDir, "autotest-result.txt"), line + "\n"); } catch (Exception) { }
        Application.Quit();
    }

    private static IEnumerator Frames()
    {
        yield return null;
        yield return null;
        yield return null;
    }

    private IEnumerator Shot(string name)
    {
        yield return new WaitForEndOfFrame();
        ScreenCapture.CaptureScreenshot(Path.Combine(outDir, "autotest-" + name + ".png"));
    }

    private void Check(string name, bool ok)
    {
        if (ok) { pass++; } else { fail++; }
        Debug.Log("[AutoTest] " + (ok ? "PASS " : "FAIL ") + name);
    }

    private static bool AnySceneManagerEnabled()
    {
        MonoBehaviour[] all = FindObjectsOfType<MonoBehaviour>();
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i].enabled && all[i].GetType().Name.EndsWith("SceneManager")) { return true; }
        }
        return false;
    }

    private static bool Contains(KeyCode[] keys, KeyCode key)
    {
        for (int i = 0; i < keys.Length; i++)
        {
            if (keys[i] == key) { return true; }
        }
        return false;
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
}
