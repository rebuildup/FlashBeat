using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;

namespace FlashBeat.Update
{
    /// <summary>
    /// 起動時に GitHub Releases を非同期で確認し、新版があれば ZIP と .sha256 を
    /// %LOCALAPPDATA%\FlashBeat\update\staging に取得して SHA-256 検証する。
    /// 終了時 (OnApplicationQuit) に配布物内 tools\update\Update.ps1 を
    /// -Apply -WaitPid -Relaunch で分離プロセス起動する (アプリ終了 → 展開 → 検証 →
    /// 一括切替 → 新 exe 再起動)。通信失敗・SHA 不一致では何も変更せず現行版を維持し、
    /// ゲームは止めない。常設ランチャー・独自更新サーバーは使わない (更新元は GitHub Releases のみ)。
    /// </summary>
    public class UpdateChecker : MonoBehaviour
    {
        public const string Repo = "rebuildup/FlashBeat";
        private const string ReleasesApiUrl = "https://api.github.com/repos/" + Repo + "/releases?per_page=10";
        private const string UpdaterScriptRelPath = "tools/update/Update.ps1";

        [Serializable]
        private class ReleaseAsset
        {
            public string name;
            public string browser_download_url;
        }

        [Serializable]
        private class ReleaseInfo
        {
            public string tag_name;
            public bool draft;
            public ReleaseAsset[] assets;
        }

        [Serializable]
        private class ReleaseArray
        {
            public ReleaseInfo[] items;
        }

        private UpdateStaging.StagedUpdate staged;
        private UnityEngine.UI.Button optionApplyButton;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindAnyObjectByType<UpdateChecker>() != null) { return; }
            var go = new GameObject("FlashBeatUpdateChecker");
            DontDestroyOnLoad(go);
            go.AddComponent<UpdateChecker>();
        }

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += OnSceneLoaded;
            StartCoroutine(CheckAndStage());
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private IEnumerator CheckAndStage()
        {
            var current = UpdateVersion.ParseBuildId(Application.version);
            if (!current.Valid)
            {
                Debug.LogWarning("[UpdateChecker] ビルド版識別子を解釈できません: '" + Application.version +
                                 "' — 更新確認を省略します (現行版を維持します)。");
                yield break;
            }

            // ── 1. Releases 一覧を取得 (draft は認証なしでは返らない。prerelease も更新対象) ──
            var listReq = UnityWebRequest.Get(ReleasesApiUrl);
            listReq.SetRequestHeader("Accept", "application/vnd.github+json");
            listReq.timeout = 15;
            yield return listReq.SendWebRequest();
            if (listReq.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning("[UpdateChecker] Releases の確認に失敗しました (" + listReq.error +
                                 ") — 現行版のまま続行します。");
                yield break;
            }

            var latest = ParseLatestRelease(listReq.downloadHandler.text);
            if (latest == null)
            {
                Debug.LogWarning("[UpdateChecker] Releases の応答を解釈できませんでした — 現行版のまま続行します。");
                yield break;
            }

            var latestId = UpdateVersion.ParseTag(latest.tag_name);
            if (!latestId.Valid || !UpdateVersion.IsNewerThan(latestId, current))
            {
                Debug.Log("[UpdateChecker] 最新です (build=" + Application.version + ", latest=" +
                          (latest.tag_name ?? "(無効なタグ)") + ")。");
                yield break;
            }

            // ── 2. ZIP アセットを特定 ──
            ReleaseAsset zip = null;
            if (latest.assets != null)
            {
                foreach (var asset in latest.assets)
                {
                    if (asset != null && !string.IsNullOrEmpty(asset.name) && asset.name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                    {
                        zip = asset;
                        break;
                    }
                }
            }
            if (zip == null)
            {
                Debug.LogWarning("[UpdateChecker] Release " + latest.tag_name + " に ZIP アセットがありません — 現行版のまま続行します。");
                yield break;
            }

            Debug.Log("[UpdateChecker] 新版を確認: " + latest.tag_name + " (現行 " + Application.version + ")");

            // ── 3. 同一タグの staging 済みがあれば再利用 ──
            var existing = UpdateStaging.ReadManifest();
            if (existing != null && existing.tag == latest.tag_name)
            {
                string reuseProblem;
                if (UpdateStaging.VerifyStaged(existing, out reuseProblem))
                {
                    staged = existing;
                    Debug.Log("[UpdateChecker] staging 済みの新版を再利用します: " + existing.zipName);
                    yield break;
                }
                Debug.LogWarning("[UpdateChecker] staging 済み ZIP を再検証できませんでした (" + reuseProblem + ") — 取り直します。");
            }

            // ── 4. ZIP と .sha256 を staging へダウンロード ──
            UpdateStaging.ClearStaging();
            var zipPath = Path.Combine(UpdateStaging.StagingDir, zip.name);

            yield return DownloadToFile(zip.browser_download_url, zipPath);
            if (!File.Exists(zipPath))
            {
                Debug.LogWarning("[UpdateChecker] ZIP のダウンロードに失敗しました — 更新を中止し、現行版を維持します。");
                UpdateStaging.ClearStaging();
                yield break;
            }

            yield return FetchExpectedSha256(zip.browser_download_url + ".sha256");
            var expectedSha = lastFetchedSha256;
            if (string.IsNullOrEmpty(expectedSha))
            {
                Debug.LogWarning("[UpdateChecker] .sha256 を取得できませんでした — 更新を中止し、現行版を維持します。");
                UpdateStaging.ClearStaging();
                yield break;
            }

            // ── 5. SHA-256 検証 (大きい ZIP のためバックグラウンドで計算) ──
            string actualSha = null;
            string computeError = null;
            var task = System.Threading.Tasks.Task.Run(() =>
            {
                try { actualSha = UpdateStaging.ComputeSha256(zipPath); }
                catch (Exception e) { computeError = e.Message; }
            });
            while (!task.IsCompleted) { yield return null; }

            if (computeError != null || !string.Equals(actualSha, expectedSha, System.StringComparison.OrdinalIgnoreCase))
            {
                Debug.LogWarning("[UpdateChecker] SHA-256 検証に失敗しました (actual=" + (actualSha ?? computeError) +
                                 ", expected=" + expectedSha + ") — 更新を中止し、現行版を維持します。");
                UpdateStaging.ClearStaging();
                yield break;
            }

            // ── 6. manifest を書いて終了時の適用を待つ ──
            staged = new UpdateStaging.StagedUpdate
            {
                tag = latest.tag_name,
                zipName = zip.name,
                sha256 = expectedSha,
                stagedAtUtc = DateTime.UtcNow.ToString("o"),
            };
            if (!UpdateStaging.WriteManifest(staged))
            {
                Debug.LogWarning("[UpdateChecker] staging manifest を書けませんでした — 更新を中止し、現行版を維持します。");
                staged = null;
                yield break;
            }

            Debug.Log("[UpdateChecker] 新版の staging が完了しました: " + latest.tag_name +
                      " (sha256=" + expectedSha + ")。終了時に適用されます。");
        }

        private string lastFetchedSha256;

        private IEnumerator FetchExpectedSha256(string url)
        {
            lastFetchedSha256 = null;
            var req = UnityWebRequest.Get(url);
            req.timeout = 15;
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success)
            {
                yield break;
            }
            var text = req.downloadHandler.text;
            if (string.IsNullOrEmpty(text)) { yield break; }
            // .sha256 は "<hex>  <filename>" 形式。先頭トークンが 64 桁 hex なら採用。
            var token = text.Trim().Split(' ')[0].Trim('"');
            if (token.Length == 64 && System.Text.RegularExpressions.Regex.IsMatch(token, "^[0-9a-fA-F]{64}$"))
            {
                lastFetchedSha256 = token.ToLowerInvariant();
            }
        }

        private static IEnumerator DownloadToFile(string url, string destPath)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(destPath));
            }
            catch (Exception)
            {
                yield break;
            }
            var req = new UnityWebRequest(url);
            req.downloadHandler = new DownloadHandlerFile(destPath);
            req.timeout = 0; // 大きい ZIP のためタイムアウトなし (失敗は result で判定)
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning("[UpdateChecker] ダウンロード失敗: " + url + " (" + req.error + ")");
                try { File.Delete(destPath); } catch (Exception) { }
            }
        }

        /// <summary>Releases 一覧 JSON から先頭 (最新) の非 draft リリースを取り出す。</summary>
        private static ReleaseInfo ParseLatestRelease(string json)
        {
            try
            {
                // JsonUtility はトップレベル配列を解釈できないためラップする
                var wrapped = new StringBuilder("{\"items\":").Append(json).Append("}").ToString();
                var parsed = JsonUtility.FromJson<ReleaseArray>(wrapped);
                if (parsed == null || parsed.items == null) { return null; }
                foreach (var release in parsed.items)
                {
                    if (release != null && !release.draft && !string.IsNullOrEmpty(release.tag_name))
                    {
                        return release;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[UpdateChecker] Releases JSON の解析に失敗しました: " + e.Message);
            }
            return null;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != "OptionScene") { return; }
            StartCoroutine(CreateOptionButtonNextFrame());
        }

        private IEnumerator CreateOptionButtonNextFrame()
        {
            yield return null; // OptionScene 側の UI 構築を待つ
            CreateOptionApplyButton();
        }

        /// <summary>Option に「更新を適用して再起動」ボタンを出す (既存 uGUI の TMP フォントを流用)。
        /// staging 済み更新が無いときは出さない。</summary>
        private void CreateOptionApplyButton()
        {
            if (staged == null || optionApplyButton != null) { return; }

            Canvas canvas = null;
            foreach (var candidate in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (candidate.isRootCanvas && candidate.gameObject.activeInHierarchy) { canvas = candidate; break; }
            }
            if (canvas == null) { return; }

            TMPro.TMP_Text fontSource = null;
            foreach (var text in FindObjectsByType<TMPro.TextMeshProUGUI>(FindObjectsSortMode.None))
            {
                if (text.isActiveAndEnabled && text.font != null) { fontSource = text; break; }
            }

            var root = new GameObject("UpdateApplyButton",
                typeof(UnityEngine.UI.Image),
                typeof(UnityEngine.UI.Button),
                typeof(UpdateApplyButtonMarker));
            root.transform.SetParent(canvas.transform, false);
            root.GetComponent<UnityEngine.UI.Image>().color = new Color(0f, 0f, 0f, 0.6f);

            var rect = root.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = new Vector2(-24f, 24f);
            rect.sizeDelta = new Vector2(360f, 56f);

            var labelGo = new GameObject("Label", typeof(TMPro.TextMeshProUGUI));
            labelGo.transform.SetParent(root.transform, false);
            var label = labelGo.GetComponent<TMPro.TextMeshProUGUI>();
            if (fontSource != null) { label.font = fontSource.font; }
            label.text = "更新を適用して再起動 (" + staged.tag + ")";
            label.fontSize = 22;
            label.alignment = TMPro.TextAlignmentOptions.Center;
            label.color = Color.white;
            var labelRect = label.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.sizeDelta = Vector2.zero;

            optionApplyButton = root.GetComponent<UnityEngine.UI.Button>();
            optionApplyButton.onClick.AddListener(ApplyStagedUpdateNow);
            Debug.Log("[UpdateChecker] Option に「更新を適用して再起動」ボタンを追加しました (" + staged.tag + ")。");
        }

        private void OnApplicationQuit()
        {
            // staged が無い (最新 / 取得失敗) なら何もしない → ゲームは普段どおり終了する
            TryLaunchUpdater(waitForThisProcess: true);
        }

        /// <summary>Option の「更新を適用して再起動」から呼ぶ。更新スクリプト起動後、アプリ自身を終了する。</summary>
        public static void ApplyStagedUpdateNow()
        {
            var checker = FindAnyObjectByType<UpdateChecker>();
            if (checker == null || checker.staged == null)
            {
                Debug.LogWarning("[UpdateChecker] 適用できる staging 済み更新がありません。");
                return;
            }
            if (!checker.TryLaunchUpdater(waitForThisProcess: true)) { return; }
            Application.Quit();
        }

        /// <summary>配布物内 tools\update\Update.ps1 を分離プロセスで起動する。
        /// staging 済み ZIP を終了時にもう一度検証し、失敗したら起動しない (現行版を維持)。</summary>
        private bool TryLaunchUpdater(bool waitForThisProcess)
        {
            if (staged == null) { return false; }

            string problem;
            if (!UpdateStaging.VerifyStaged(staged, out problem))
            {
                Debug.LogWarning("[UpdateChecker] staging 済み ZIP の検証に失敗しました (" + problem +
                                 ") — 更新を中止し、現行版を維持します。");
                return false;
            }

            var installDir = Path.GetDirectoryName(Application.dataPath);
            var scriptPath = Path.Combine(installDir, UpdaterScriptRelPath);
            if (!File.Exists(scriptPath))
            {
                Debug.LogError("[UpdateChecker] 更新スクリプトがありません: " + scriptPath);
                return false;
            }

            var args = "-NoProfile -ExecutionPolicy Bypass -File \"" + scriptPath + "\"" +
                       " -InstallDir \"" + installDir + "\"" +
                       " -Apply -Relaunch" +
                       " -ZipPath \"" + Path.Combine(UpdateStaging.StagingDir, staged.zipName) + "\"" +
                       " -ExpectedSha256 " + staged.sha256;
            if (waitForThisProcess)
            {
                args += " -WaitPid " + Process.GetCurrentProcess().Id;
            }

            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = args,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            try
            {
                Process.Start(psi);
                Debug.Log("[UpdateChecker] 更新スクリプトを起動しました: " + staged.tag +
                          " (アプリ終了後に適用されます)。");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError("[UpdateChecker] 更新スクリプトの起動に失敗しました: " + e.Message);
                return false;
            }
        }
    }

    /// <summary>Option に作った適用ボタンの重複生成防止マーカー。</summary>
    public class UpdateApplyButtonMarker : MonoBehaviour
    {
    }
}
