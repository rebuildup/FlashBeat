/// <summary>
/// Holds the chart JSON snapshot of the song being played (#17).
///
/// The chart version is pinned when the playlist entry starts (select screen
/// -> GameScene transition) and the snapshot is kept until the next Hold, so
/// retries of the same song replay the exact same chart even if the server
/// publishes a new version mid-session. Fixed-catalog songs never consult the
/// holder; they keep loading their chart from Resources.
/// </summary>
public static class CloudChartHolder
{
    static string songId;
    static int chartVersion;
    static string chartJson;

    public static void Hold(string id, int version, string json)
    {
        songId = id;
        chartVersion = version;
        chartJson = json;
    }

    public static bool TryGet(string id, int version, out string json)
    {
        if (!string.IsNullOrEmpty(id) && id == songId && version == chartVersion && !string.IsNullOrEmpty(chartJson))
        {
            json = chartJson;
            return true;
        }
        json = null;
        return false;
    }

    /// <summary>
    /// 選択中の曲 (remoteId) に対応する保持中の譜面を、版を問わず取り出す (Issue #183)。
    ///
    /// 遷移開始の時点でカタログが入れ替わっていることがある (行の chartVersion が
    /// 取得時より新しくなっている)。そのときでも、実際に取得して保持している版を
    /// 返すことで、プレイする譜面と曲情報・版のずれを防ぐ。TryGet と違って版の
    /// 一致を要求しない代わりに、呼び出し側は返された版をそのまま固定すること。
    /// </summary>
    public static bool TryGetForSong(string id, out int version, out string json)
    {
        if (!string.IsNullOrEmpty(id) && id == songId && !string.IsNullOrEmpty(chartJson))
        {
            version = chartVersion;
            json = chartJson;
            return true;
        }
        version = 0;
        json = null;
        return false;
    }

    public static void Clear()
    {
        songId = null;
        chartVersion = 0;
        chartJson = null;
    }
}
