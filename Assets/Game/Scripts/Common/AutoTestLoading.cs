using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using FlashBeat.UI.V6.Game.Loading;

/// <summary>
/// Verification aid (Mac, no OS input permission needed): with "-autotest loading" the player
/// shows the v6 loading view (V6LoadingView) and checks that it is really on screen,
/// that the loading / error states are recorded, and that SetVisible(false) hides it.
/// Reproduces the owner's request: "動画のダウンロードを読み込み中表示にしたい".
/// Issue #740 Stage B: 旧 MediaLoadingOverlay は製品経路から外れたため削除済み。
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
    private GameObject canvasGo;
    private V6LoadingView view;

    private IEnumerator Start()
    {
        outDir = GetArg("-autotest-out");
        if (string.IsNullOrEmpty(outDir)) { outDir = Application.persistentDataPath; }
        try { Directory.CreateDirectory(outDir); } catch (Exception) { }

        SceneManager.LoadScene("TitleScene");
        yield return new WaitForSecondsRealtime(4f);
        Check("on TitleScene", SceneManager.GetActiveScene().name == "TitleScene");

        // 検証用に v6 ローディング画面を単独で生成する (製品では V6GameRuntime が
        // GameScene で生成する)。親は ScreenSpaceOverlay の Canvas。
        canvasGo = new GameObject("AutoTestLoadingCanvas", typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler));
        DontDestroyOnLoad(canvasGo);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 40000;
        view = V6LoadingView.Create(canvasGo.GetComponent<RectTransform>());
        Check("the v6 loading view was created", view != null);

        if (view != null)
        {
            Check("the view is hidden before the download starts", !view.IsShownForAudit);

            view.SetLoading("テスト曲", "テストアーティスト", null, null);
            view.SetVisible(true);
            yield return null;
            Check("the view is visible while downloading", view.IsShownForAudit);
            Check("the view root is active", view.Root != null && view.Root.gameObject.activeInHierarchy);

            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(outDir, "autotest-loading.png"));

            view.SetLoading("テスト曲", "テストアーティスト", null, 0.5f);
            yield return null;
            Check("the progress is recorded", view.ProgressForAudit.HasValue
                && Mathf.Approximately(view.ProgressForAudit.Value, 0.5f));

            view.SetError();
            yield return null;
            Check("the error state is recorded", view.ErrorShownForAudit);

            view.SetVisible(false);
            yield return null;
            Check("the view is hidden after the media is ready", !view.IsShownForAudit);
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
