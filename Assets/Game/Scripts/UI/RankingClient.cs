using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Festival ranking client (#134). Talks to the FlashBeat web API:
/// POST /api/v1/scores and GET /api/v1/leaderboards/{songId}?version=.
///
/// Scene independent and dependency free (no trunk-only classes): the API base
/// URL is read from Resources/CloudCatalogConfig.json — the same file the cloud
/// catalog uses — falling back to the production origin. Every failure
/// (offline, HTTP error, malformed body, invalid input) is reported through a
/// Result object; nothing throws and coroutines always call onDone exactly once.
/// </summary>
public static class RankingClient
{
    public const string DefaultOrigin = "https://flashbeat.rebuildup.dev";
    public const string ConfigResourceName = "CloudCatalogConfig";
    public const int RequestTimeoutSeconds = 5;
    public const int NicknameMinLength = 1;
    public const int NicknameMaxLength = 16;
    public const string NicknamePrefsKey = "RankingNickname";
    /// <summary>error set on transport failure (offline, DNS, timeout).</summary>
    public const string UnavailableError = "unavailable";

    [Serializable]
    public class ScoreRequest
    {
        public string songId;
        public int chartVersion;
        public string nickname;
        public int score;
        public int perfect;
        public int great;
        public int bad;
        public int miss;
        public int maxCombo;
    }

    [Serializable]
    public class ScoreResponse
    {
        public int rank;
    }

    [Serializable]
    public class LeaderboardEntry
    {
        public int rank;
        public string nickname;
        public int score;
    }

    [Serializable]
    public class LeaderboardResponse
    {
        public LeaderboardEntry[] entries;
    }

    [Serializable]
    class Config
    {
        public bool enabled = true;
        public string baseUrl = "";
    }

    /// <summary>Outcome of one request. ok=false means the caller must show the failure path.</summary>
    public class Result
    {
        public bool ok;
        public int httpCode;
        public string error;
        public string body;
        public ScoreResponse score;
        public LeaderboardResponse leaderboard;
    }

    // --- pure helpers (unit tested) -----------------------------------------

    /// <summary>Resolves the API origin: enabled + non-empty baseUrl from the config resource, else the production origin.</summary>
    public static string ResolveOrigin(bool enabled, string baseUrl)
    {
        if (enabled && !string.IsNullOrWhiteSpace(baseUrl))
        {
            return baseUrl.Trim().TrimEnd('/');
        }
        return DefaultOrigin;
    }

    /// <summary>Reads Resources/CloudCatalogConfig.json the same way the cloud catalog does.</summary>
    public static string Origin()
    {
        Config config = null;
        TextAsset asset = Resources.Load<TextAsset>(ConfigResourceName);
        if (asset != null && !string.IsNullOrWhiteSpace(asset.text))
        {
            try
            {
                config = JsonUtility.FromJson<Config>(asset.text);
            }
            catch (Exception)
            {
                config = null;
            }
        }
        return ResolveOrigin(config != null && config.enabled, config != null ? config.baseUrl : null);
    }

    public static string ScoreUrl()
    {
        return Origin() + "/api/v1/scores";
    }

    public static string LeaderboardUrl(string songId, int chartVersion)
    {
        if (!IsValidSongId(songId)) return null;
        return Origin() + "/api/v1/leaderboards/" + songId + "?version=" + chartVersion;
    }

    /// <summary>Server-side songId pattern (contracts/openapi.v1.yaml).</summary>
    public static bool IsValidSongId(string songId)
    {
        if (string.IsNullOrEmpty(songId) || songId.Length < 3 || songId.Length > 64) return false;
        for (int i = 0; i < songId.Length; i++)
        {
            char c = songId[i];
            if (!((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '-')) return false;
        }
        return true;
    }

    /// <summary>Trims and validates a nickname (1..NicknameMaxLength chars). Returns null when unusable.</summary>
    public static string ValidateNickname(string raw)
    {
        if (raw == null) return null;
        string trimmed = raw.Trim();
        if (trimmed.Length < NicknameMinLength || trimmed.Length > NicknameMaxLength) return null;
        return trimmed;
    }

    /// <summary>Builds the POST /api/v1/scores body. Returns null when the nickname is invalid.</summary>
    public static string BuildScoreRequestJson(string songId, int chartVersion, string nickname,
        int score, int perfect, int great, int bad, int miss, int maxCombo)
    {
        string name = ValidateNickname(nickname);
        if (name == null || !IsValidSongId(songId)) return null;
        var request = new ScoreRequest
        {
            songId = songId,
            chartVersion = chartVersion,
            nickname = name,
            score = score,
            perfect = perfect,
            great = great,
            bad = bad,
            miss = miss,
            maxCombo = maxCombo,
        };
        return JsonUtility.ToJson(request);
    }

    /// <summary>Parses a POST /api/v1/scores response ({rank, ...}). Returns null when unusable.</summary>
    public static ScoreResponse ParseScoreResponse(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            ScoreResponse response = JsonUtility.FromJson<ScoreResponse>(json);
            if (response == null || response.rank <= 0) return null;
            return response;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>Parses a GET /api/v1/leaderboards response. Returns the entries or null when unusable.</summary>
    public static LeaderboardEntry[] ParseLeaderboard(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            LeaderboardResponse response = JsonUtility.FromJson<LeaderboardResponse>(json);
            if (response == null) return null;
            // スコアイ合作的曲 (entries が空) は「取得成功・0 件」であり失敗ではない。
            // 空配列を返さないと v6 選曲画面が「取得失敗 (HTTP 200)」と表示してしまう。
            if (response.entries == null) return null;
            for (int i = 0; i < response.entries.Length; i++)
            {
                if (response.entries[i] == null) return null;
            }
            return response.entries;
        }
        catch (Exception)
        {
            return null;
        }
    }

    // --- nickname persistence ------------------------------------------------

    public static void SaveNickname(string nickname)
    {
        try
        {
            if (string.IsNullOrEmpty(nickname))
            {
                PlayerPrefs.DeleteKey(NicknamePrefsKey);
            }
            else
            {
                PlayerPrefs.SetString(NicknamePrefsKey, nickname);
                PlayerPrefs.Save();
            }
        }
        catch (Exception)
        {
            // PlayerPrefs can fail on restricted platforms; ranking still works without persistence.
        }
    }

    public static string LoadNickname()
    {
        try
        {
            return PlayerPrefs.GetString(NicknamePrefsKey, string.Empty);
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }

    // --- requests ------------------------------------------------------------

    /// <summary>
    /// POSTs a score. onDone fires exactly once; on success result.score holds
    /// the server rank. Never throws.
    /// </summary>
    public static IEnumerator PostScore(string songId, int chartVersion, string nickname,
        int score, int perfect, int great, int bad, int miss, int maxCombo, Action<Result> onDone)
    {
        Result result = new Result();
        string body = null;
        try
        {
            body = BuildScoreRequestJson(songId, chartVersion, nickname, score, perfect, great, bad, miss, maxCombo);
        }
        catch (Exception e)
        {
            result.error = e.Message;
        }
        if (body == null)
        {
            if (string.IsNullOrEmpty(result.error)) result.error = "nickname";
            if (onDone != null) onDone(result);
            yield break;
        }

        yield return Send(ScoreUrl(), body, result);
        if (result.body != null)
        {
            result.score = ParseScoreResponse(result.body);
            result.ok = result.score != null;
            if (!result.ok) result.error = "response";
        }
        if (onDone != null) onDone(result);
    }

    /// <summary>
    /// GETs the top-10 leaderboard for one chart version. onDone fires exactly
    /// once; on success result.leaderboard holds the entries. Never throws.
    /// </summary>
    public static IEnumerator GetLeaderboard(string songId, int chartVersion, Action<Result> onDone)
    {
        Result result = new Result();
        string url = null;
        try
        {
            url = LeaderboardUrl(songId, chartVersion);
        }
        catch (Exception e)
        {
            result.error = e.Message;
        }
        if (url == null)
        {
            if (string.IsNullOrEmpty(result.error)) result.error = "songId";
            if (onDone != null) onDone(result);
            yield break;
        }

        yield return Send(url, null, result);
        if (result.body != null)
        {
            LeaderboardEntry[] entries = ParseLeaderboard(result.body);
            result.leaderboard = entries != null ? new LeaderboardResponse { entries = entries } : null;
            result.ok = result.leaderboard != null;
            if (!result.ok) result.error = "response";
        }
        if (onDone != null) onDone(result);
    }

    /// <summary>
    /// One UnityWebRequest round trip. result.body is set only on success;
    /// transport failures (result == 0) report the "unavailable" error.
    /// </summary>
    static IEnumerator Send(string url, string body, Result result)
    {
        UnityWebRequest request;
        try
        {
            if (body != null)
            {
                request = new UnityWebRequest(url, "POST");
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
                request.SetRequestHeader("Content-Type", "application/json");
            }
            else
            {
                request = UnityWebRequest.Get(url);
            }
            request.downloadHandler = new DownloadHandlerBuffer();
            request.timeout = RequestTimeoutSeconds;
        }
        catch (Exception e)
        {
            result.error = e.Message;
            yield break;
        }

        using (request)
        {
            yield return request.SendWebRequest();

            result.httpCode = (int)request.responseCode;
            if (request.result == UnityWebRequest.Result.Success)
            {
                result.ok = false; // set by the caller after parsing
                result.body = request.downloadHandler.text;
            }
            else
            {
                result.body = null;
                result.error = result.httpCode == 0 ? UnavailableError : request.error;
            }
        }
    }
}
