#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using YoutubePlayer.Components;

namespace FlashBeat.EditorTools
{
    // The youtube-player package's auto-fetched public Invidious instance list
    // (https://api.invidious.io/instances.json?sort_by=type,users) only has 1
    // entry that satisfies its `cors=true` filter today, and that entry
    // (inv.zoomerville.com) returns 403 for /api/v1/videos/<id> from many
    // egress IPs. This tool probes a curated candidate list and reports
    // which URLs return a parseable VideoInfo JSON.
    //
    // Menu: Tools → FlashBeat → Probe Invidious Instances
    public static class InvidiousInstanceProber
    {
        // Candidate list. The package's public list narrows to ~1 today; these
        // are community-known hosts that historically exposed /api/v1/videos.
        // Add/remove freely — this list is for diagnosis, not for runtime use.
        static readonly string[] Candidates = {
            "https://inv.zoomerville.com",
            "https://inv.nadeko.net",
            "https://yewtu.be",
            "https://invidious.materialio.us",
            "https://invidious.flokinet.to",
            "https://invidious.adminforge.de",
            "https://invidious.f5.si",
            "https://yt.chocolatemoo53.com",
            "https://invidious.tiekoetter.com",
            "https://invidious.nerdvpn.de",
            "https://invidious.privacyredirect.com",
            "https://iv.nboeck.de",
        };

        // Use a song that exists in GManager.Songs. Default to id=1 (ロストアンブレラ).
        const string ProbeVideoId = "DeKLpgzh-qQ";

        [MenuItem("Tools/FlashBeat/Probe Invidious Instances")]
        public static void Probe()
        {
            var results = new List<(string url, long code, bool ok, string detail)>();
            foreach (var baseUrl in Candidates)
            {
                var url = $"{baseUrl}/api/v1/videos/{ProbeVideoId}";
                var (code, body) = HttpGet(url, timeoutSec: 8);
                bool ok = code == 200
                    && !string.IsNullOrEmpty(body)
                    && body.Length > 2 && body[0] == '{'
                    && body.Contains("\"formatStreams\"");
                string detail = code != 200
                    ? $"HTTP {code}"
                    : (ok ? "JSON ok" : $"body not JSON (head={Truncate(body, 60)})");
                results.Add((baseUrl, code, ok, detail));
            }

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Invidious instance probe results:");
            int working = 0;
            foreach (var r in results)
            {
                sb.AppendLine($"  {(r.ok ? "OK  " : "FAIL")}  HTTP {r.code,4}  {r.url,-42}  {r.detail}");
                if (r.ok) working++;
            }
            sb.AppendLine();
            sb.AppendLine($"Working: {working}/{results.Count}");

            if (working > 0)
            {
                var first = results.Find(r => r.ok);
                sb.AppendLine($"Recommended Custom URL: {first.url}");
                sb.AppendLine("To apply: select the 'InvidiousInstance' GameObject in GameScene, " +
                              "set 'Instance Type' to 'Custom', paste the URL into 'Custom Instance Url'.");
            }
            else
            {
                sb.AppendLine("No candidate returned valid JSON. Either your egress IP is blocked from " +
                              "all known Invidious hosts, or the ecosystem is fully offline. " +
                              "Consider self-hosting Invidious or switching to a different YouTube-extraction library.");
            }

            Debug.Log(sb.ToString());
        }

        [MenuItem("Tools/FlashBeat/Apply Recommended Invidious URL")]
        public static void ApplyRecommended()
        {
            foreach (var baseUrl in Candidates)
            {
                var url = $"{baseUrl}/api/v1/videos/{ProbeVideoId}";
                var (code, body) = HttpGet(url, timeoutSec: 8);
                if (code == 200 && !string.IsNullOrEmpty(body) && body.Contains("\"formatStreams\""))
                {
                    ApplyCustomUrl(baseUrl);
                    Debug.Log($"[InvidiousProber] Applied Custom URL: {baseUrl}");
                    return;
                }
            }
            Debug.LogWarning("[InvidiousProber] No working candidate found; nothing applied.");
        }

        static void ApplyCustomUrl(string url)
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
            if (!scene.path.EndsWith("GameScene.unity"))
            {
                Debug.LogWarning("[InvidiousProber] Active scene is not GameScene. Open GameScene first.");
                return;
            }

            var roots = scene.GetRootGameObjects();
            InvidiousInstance target = null;
            foreach (var root in roots)
            {
                target = root.GetComponentInChildren<InvidiousInstance>(true);
                if (target != null) break;
            }
            if (target == null)
            {
                Debug.LogWarning("[InvidiousProber] No InvidiousInstance component found in GameScene.");
                return;
            }

            Undo.RecordObject(target, "Apply Invidious Custom URL");
            target.InstanceType = InvidiousInstance.InvidiousInstanceType.Custom;
            target.CustomInstanceUrl = url;
            UnityEditor.EditorUtility.SetDirty(target);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject = target.gameObject;
        }

        static (long code, string body) HttpGet(string url, int timeoutSec)
        {
            var req = UnityEngine.Networking.UnityWebRequest.Get(url);
            req.timeout = timeoutSec;
            var op = req.SendWebRequest();
            var start = System.DateTime.UtcNow;
            while (!op.isDone)
            {
                System.Threading.Thread.Sleep(20);
                if ((System.DateTime.UtcNow - start).TotalSeconds > timeoutSec + 1)
                {
                    req.Abort();
                    break;
                }
            }
            var code = req.responseCode;
            var body = req.downloadHandler != null ? req.downloadHandler.text : "";
            req.Dispose();
            return (code, body ?? "");
        }

        static string Truncate(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return "";
            s = s.Replace("\n", " ").Replace("\r", " ");
            return s.Length <= max ? s : s.Substring(0, max) + "...";
        }
    }
}
#endif
