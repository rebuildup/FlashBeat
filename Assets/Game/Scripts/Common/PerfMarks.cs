using UnityEngine;

/// <summary>
/// スパイクフレームの帰属を絞るための「最後に通過した地点」マーカー (issue #395)。
/// <see cref="FrameTimeProbe"/> が spike を記録するとき、Update 内で最後に
/// <see cref="Hit"/> された名前とその経過時間を一緒に出す。12 秒級の停止が
/// 「譜面生成中なのか、VideoPlayer 準備中なのか、描画側なのか」を、Profiler を
/// 繋がずに切り分けるための最小限の仕組み。
///
/// コストは static フィールド 2 つへの書き込みだけなので常時有効でよい。
/// マーカーの呼び出しは区間の入口/出口に限定し、ループ内では閾値超過時だけ
/// 打つ (毎フレーム Hit すると lastMark が常に同じ名前になり意味を失う)。
/// </summary>
public static class PerfMarks
{
    static string lastName = "(none)";
    static float lastTime = -1f;

    /// <summary>地点を記録する。frame 内のどこまで進んだかの目印。</summary>
    public static void Hit(string name)
    {
        lastName = name;
        lastTime = Time.unscaledTime;
    }

    /// <summary>最後のマーカー名。</summary>
    public static string LastName { get { return lastName; } }

    /// <summary>最後のマーカーからの経過秒。未記録なら -1。</summary>
    public static float AgeSeconds
    {
        get { return lastTime < 0f ? -1f : Time.unscaledTime - lastTime; }
    }

    /// <summary>spike ログ用の 1 行表現。</summary>
    public static string Describe()
    {
        if (lastTime < 0f) return lastName;
        return lastName + "@" + (Time.unscaledTime - lastTime).ToString("F2");
    }
}
