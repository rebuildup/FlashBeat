using System;
using System.Collections;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Cloud Catalog (#17): fetches the song catalog and chart JSON from the
/// FlashBeat web API (public GET endpoints only, no token, no secret in the
/// binary) and converts them into the in-game SongData / chart representations.
///
/// The API base URL comes from Resources/CloudCatalogConfig.json so it can be
/// changed without a rebuild. Every failure (offline, HTTP error, malformed
/// body, empty catalog) is reported to the caller as "unusable"; callers then
/// keep the built-in fixed catalog (GManager.ActiveSongs) and the game keeps
/// running.
/// </summary>
public static class CloudCatalog
{
    public const string ConfigResourceName = "CloudCatalogConfig";
    public const int RequestTimeoutSeconds = 10;

    [Serializable]
    public class Config
    {
        public bool enabled = true;
        public string baseUrl = "";
        public int chartVersion = 1;      // fallback when a catalog entry omits chartVersion
        public float refreshSeconds = 8f; // idle refresh interval on the select screen
    }

    [Serializable]
    class CatalogResponse
    {
        public int catalogVersion;
        public RemoteSong[] songs;
    }

    [Serializable]
    class RemoteSong
    {
        public string id;
        public string title;
        public string artist;
        public string youtubeId;
        public int level;
        public int bpm;
        public float durationSec;
        public int chartVersion;
        public int noteCount;
    }

    [Serializable]
    public class ChartResponse
    {
        public string songId;
        public int chartVersion;
        public Data chart;
    }

    static readonly Regex SongIdPattern = new Regex("^[a-z0-9-]{3,64}$");
    static readonly Regex YouTubeIdPattern = new Regex("^[A-Za-z0-9_-]{11}$");

    /// <summary>Loads Resources/CloudCatalogConfig.json. Returns null when the resource is missing, disabled or has no base URL; the game then stays on the fixed catalog.</summary>
    public static Config LoadConfig()
    {
        TextAsset asset = Resources.Load<TextAsset>(ConfigResourceName);
        if (asset == null) return null;
        return ParseConfig(asset.text);
    }

    public static Config ParseConfig(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        Config config;
        try
        {
            config = JsonUtility.FromJson<Config>(json);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[CloudCatalog] config parse failed: {e.Message}");
            return null;
        }
        if (config == null || !config.enabled || string.IsNullOrWhiteSpace(config.baseUrl)) return null;
        return config;
    }

    public static string CatalogUrl(string baseUrl)
    {
        return TrimBase(baseUrl) + "/api/v1/catalog";
    }

    public static string ChartUrl(string baseUrl, string songId, int chartVersion)
    {
        return $"{TrimBase(baseUrl)}/api/v1/charts/{songId}?version={chartVersion}";
    }

    static string TrimBase(string baseUrl)
    {
        return (baseUrl ?? "").Trim().TrimEnd('/');
    }

    /// <summary>
    /// Parses a GET /api/v1/catalog body into the runtime song list
    /// (index 0 = the noSong placeholder, remote songs from index 1).
    /// Returns null when the response is unusable (empty catalog, malformed
    /// JSON, no valid entry); callers then keep the fixed catalog.
    /// </summary>
    public static SongData[] ParseCatalog(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;

        CatalogResponse response;
        try
        {
            response = JsonUtility.FromJson<CatalogResponse>(json);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[CloudCatalog] catalog parse failed: {e.Message}");
            return null;
        }
        if (response == null || response.songs == null || response.songs.Length == 0) return null;

        SongData[] songs = new SongData[response.songs.Length + 1];
        songs[0] = new SongData(0, "noSong", "no", "", 0, 0, 0, 0f);
        int count = 1;
        for (int i = 0; i < response.songs.Length; i++)
        {
            RemoteSong remote = response.songs[i];
            if (remote == null || !SongIdPattern.IsMatch(remote.id ?? "")) continue;
            if (string.IsNullOrWhiteSpace(remote.title)) continue;
            if (!YouTubeIdPattern.IsMatch(remote.youtubeId ?? "")) continue;

            songs[count] = new SongData(count, remote.title, remote.artist ?? "", remote.youtubeId, remote.bpm, remote.level, remote.noteCount, remote.durationSec)
            {
                isRemote = true,
                remoteId = remote.id,
                chartVersion = remote.chartVersion > 0 ? remote.chartVersion : 0,
                noteCount = remote.noteCount,
            };
            count++;
        }
        if (count == 1) return null;
        if (count == songs.Length) return songs;

        SongData[] trimmed = new SongData[count];
        Array.Copy(songs, trimmed, count);
        return trimmed;
    }

    /// <summary>
    /// Parses a GET /api/v1/charts/{songId}?version= body ({songId, chartVersion, chart}).
    /// Returns null when unusable (404 body, malformed JSON, missing notes);
    /// callers then warn and stay on the select screen instead of starting a
    /// song that cannot be played.
    /// </summary>
    public static Data ParseChartResponse(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;

        ChartResponse response;
        try
        {
            response = JsonUtility.FromJson<ChartResponse>(json);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[CloudCatalog] chart parse failed: {e.Message}");
            return null;
        }
        if (response == null || response.chart == null || response.chart.notes == null || response.chart.notes.Length == 0) return null;
        return response.chart;
    }

    /// <summary>
    /// GETs a text body over UnityWebRequest. onDone fires exactly once;
    /// httpCode is the HTTP status, or 0 on transport failure, and body is
    /// null whenever the request did not succeed.
    /// </summary>
    public static IEnumerator GetText(string url, Action<int, string> onDone)
    {
        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.timeout = RequestTimeoutSeconds;
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                onDone((int)request.responseCode, request.downloadHandler.text);
            }
            else
            {
                Debug.LogWarning($"[CloudCatalog] GET {url} failed: {request.error} (status={request.responseCode})");
                onDone((int)request.responseCode, null);
            }
        }
    }
}
