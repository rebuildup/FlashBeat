using UnityEngine;
using System.Collections;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class Fader : MonoBehaviour
{
    [HideInInspector]
    public bool start = false;
    [HideInInspector]
    public float fadeDamp = 0.0f;
    [HideInInspector]
    public string fadeScene;
    [HideInInspector]
    public float alpha = 0.0f;
    [HideInInspector]
    public Color fadeColor;
    [HideInInspector]
    public bool isFadeIn = false;
    CanvasGroup myCanvas;
    Image bg;
    float lastTime = 0;
    bool startedLoading = false;
    bool loadRoutineRunning = false;
    //Set callback
    void OnEnable()
    {
        SceneManager.sceneLoaded += OnLevelFinishedLoading;
    }
    //Remove callback
    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnLevelFinishedLoading;
    }

    public void InitiateFader()
    {

        DontDestroyOnLoad(gameObject);

        //Getting the visual elements
        if (transform.GetComponent<CanvasGroup>())
            myCanvas = transform.GetComponent<CanvasGroup>();

        if (transform.GetComponentInChildren<Image>())
        {
            bg = transform.GetComponent<Image>();
            bg.color = fadeColor;
        }
        //Checking and starting the coroutine
        if (myCanvas && bg)
        {
            myCanvas.alpha = 0.0f;
            StartCoroutine(FadeIt());
        }
        else
            Debug.LogWarning("Something is missing please reimport the package.");
    }

    IEnumerator FadeIt()
    {

        while (!start)
        {
            //waiting to start
            yield return null;
        }
        lastTime = Time.time;
        float coDelta = lastTime;
        bool hasFadedIn = false;

        while (!hasFadedIn)
        {
            coDelta = Time.time - lastTime;
            if (!isFadeIn)
            {
                //Fade in
                alpha = newAlpha(coDelta, 1, alpha);
                if (alpha == 1 && !startedLoading)
                {
                    startedLoading = true;
                    StartCoroutine(LoadSceneRoutine());
                }

            }
            else
            {
                //Fade out
                alpha = newAlpha(coDelta, 0, alpha);
                if (alpha == 0)
                {
                    hasFadedIn = true;
                }


            }
            lastTime = Time.time;
            myCanvas.alpha = alpha;
            yield return null;
        }

        Initiate.DoneFading();

        Debug.Log("Your scene has been loaded , and fading in has just ended");

        Destroy(gameObject);

        yield return null;
    }


    /// <summary>
    /// シーンを非同期で読み込む (旧来の同期 SceneManager.LoadScene はフレームを
    /// 完全に止めて画面が固まって見えた)。allowSceneActivation=false で 90% まで
    /// 読み、暗転が完了している間にアクティベートする。進捗は LoadingOverlay
    /// (Initiate.LoadingFeedback 経由) に出す。二重起動は startedLoading で防ぐ。
    /// </summary>
    IEnumerator LoadSceneRoutine()
    {
        // 二重起動の防御: すでに読み込みが走っているなら何もしない
        if (loadRoutineRunning) yield break;
        loadRoutineRunning = true;

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        Debug.Log($"[Perf] scene load start {fadeScene}");
        Initiate.LoadingFeedback.Begin?.Invoke(fadeScene);

        AsyncOperation operation = SceneManager.LoadSceneAsync(fadeScene);
        if (operation == null)
        {
            Debug.LogError($"[Fader] LoadSceneAsync failed: {fadeScene}");
            loadRoutineRunning = false;
            yield break;
        }
        operation.allowSceneActivation = false;
        while (operation.progress < 0.9f)
        {
            Initiate.LoadingFeedback.Progress?.Invoke(operation.progress);
            yield return null;
        }
        Initiate.LoadingFeedback.Progress?.Invoke(1f);
        operation.allowSceneActivation = true;
        yield return operation;

        Debug.Log($"[Perf] scene load end {fadeScene} {stopwatch.ElapsedMilliseconds}ms");
        Initiate.LoadingFeedback.End?.Invoke();
        loadRoutineRunning = false;
    }

    float newAlpha(float delta, int to, float currAlpha)
    {

        switch (to)
        {
            case 0:
                currAlpha -= fadeDamp * delta;
                if (currAlpha <= 0)
                    currAlpha = 0;

                break;
            case 1:
                currAlpha += fadeDamp * delta;
                if (currAlpha >= 1)
                    currAlpha = 1;

                break;
        }

        return currAlpha;
    }

    void OnLevelFinishedLoading(Scene scene, LoadSceneMode mode)
    {
        StartCoroutine(FadeIt());
        //We can now fade in
        isFadeIn = true;
    }
}
