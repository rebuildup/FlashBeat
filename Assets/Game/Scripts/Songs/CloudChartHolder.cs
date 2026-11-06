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

    /// <summary>
    /// The snapshot of the song that is being played. <paramref name="version"/>
    /// is the version the caller expects; 0 means "the catalog entry did not
    /// carry a version" - then the pinned snapshot of that song is used.
    /// A different, known version never matches (the snapshot of a song is
    /// never silently replaced).
    /// </summary>
    public static bool TryGet(string id, int version, out string json)
    {
        if (!string.IsNullOrEmpty(id) && id == songId && !string.IsNullOrEmpty(chartJson)
            && (version <= 0 || version == chartVersion))
        {
            json = chartJson;
            return true;
        }
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
