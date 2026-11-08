/// <summary>
/// ローディング表示 (LoadingOverlay) の純粋な判定ロジック。
/// Unity API に依存しないので NUnit テストから直接検証できる。
/// </summary>
public static class LoadingOverlayLogic
{
    /// <summary>表示開始までの遅延 (ミリ秒)。短い読み込みで一瞬点滅するのを防ぐ。</summary>
    public const double ShowDelayMs = 250.0;

    /// <summary>一度表示したら最短これだけは見せ続ける (ミリ秒)。</summary>
    public const double MinDisplayMs = 400.0;

    /// <summary>SceneManager.LoadSceneAsync の progress が allowSceneActivation=false で止まる値。</summary>
    public const float MaxAsyncProgress = 0.9f;

    /// <summary>表示要求から ShowDelayMs 以上経過したら表示してよい。</summary>
    public static bool ShouldBecomeVisible(double pendingMs)
    {
        return pendingMs >= ShowDelayMs;
    }

    /// <summary>表示開始から MinDisplayMs 以上経過していれば隠してよい。</summary>
    public static bool CanHide(double shownMs)
    {
        return shownMs >= MinDisplayMs;
    }

    /// <summary>
    /// AsyncOperation.progress (0..0.9 で止まる) を 0..1 のバーの量に写像する。
    /// 0.9 到達で 100% になる (残り 0.1 はアクティベーション待ち)。
    /// </summary>
    public static float MapProgress(float asyncProgress)
    {
        if (asyncProgress <= 0f) return 0f;
        if (asyncProgress >= 1f) return 1f;
        return asyncProgress / MaxAsyncProgress;
    }

    /// <summary>シーン名 → 日本語の画面名 (例: GameScene → ゲーム)。未知の名前はそのまま返す。</summary>
    public static string SceneLabel(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName)) return "";
        string key = sceneName.EndsWith("Scene")
            ? sceneName.Substring(0, sceneName.Length - "Scene".Length)
            : sceneName;
        switch (key)
        {
            case "Opening": return "オープニング";
            case "Title": return "タイトル";
            case "Select": return "選曲";
            case "Game": return "ゲーム";
            case "Result": return "リザルト";
            case "Option": return "オプション";
            case "Typing":
            case "Tiping": return "タイピング";
            case "MakeFile": return "譜面作成";
            case "NoteEditor": return "譜面エディタ";
            default: return sceneName;
        }
    }
}
