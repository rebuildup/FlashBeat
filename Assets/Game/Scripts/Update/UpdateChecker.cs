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
    /// 起動時に GitHub Releases を非同期で「確認だけ」行う (issue #118: 更新は明示式・任意)。
    /// 新版があれば <see cref="UpdateFlowModel"/> を Available にし、TitleScene の通知 UI
    /// (<see cref="UpdateNoticeUI"/>) が『アップデートあり』を出す。ダウンロードも適用も
    /// ユーザーが明示的にトリガーしたときだけ行う (<see cref="RequestDownload"/> /
    /// <see cref="RequestApply"/>)。押さなければダウンロードも適用も行わない。
    /// 適用は配布物内 tools\update\Update.ps1 を -Apply -WaitPid -Relaunch で分離プロセス起動
    /// (アプリ終了 → 展開 → 検証 → 一括切替 → 新 exe 再起動)。OnApplicationQuit からの
    /// 自動適用は存在しない。通信失敗・SHA 不一致では何も変更せず現行版を維持し、
    /// ゲームは止めない。常設ランチャー・独自更新サーバーは使わない (更新元は GitHub Releases のみ)。
    /// 起動時には前回の更新結果 (Update.ps1 が書く last-result.json、issue #125) を読み、
    /// Title 通知の文言に反映する (成功 / 失敗とも 1 回だけ表示し mark-as-shown)。
    /// </summary>
    public class UpdateChecker : MonoBehaviour
    {
        public const string Repo = "rebuildup/FlashBeat";
        // prerelease が混ざっても安定版を取りこぼさないよう、取得件数を広めに取る。
        private const string ReleasesApiUrl = "https://api.github.com/repos/" + Repo + "/releases?per_page=30";
        private const string UpdaterScriptRelPath = "tools/update/Update.ps1";

        [Serializable]
        public class ReleaseAsset
        {
            public string name;
            public string browser_download_url;
        }

        [Serializable]
        public class ReleaseInfo
        {
            public string tag_name;
            public bool draft;
            public bool prerelease;
            public string created_at;
            public string published_at;
            public ReleaseAsset[] assets;
        }

        [Serializable]
        private class ReleaseArray
        {
            public ReleaseInfo[] items;
        }

        /// <summary>更新フローの共有状態機械 (UpdateNoticeUI が参照して表示する)。</summary>
        public UpdateFlowModel Model { get; } = new UpdateFlowModel();

        private UpdateStaging.StagedUpdate staged;
        private ReleaseAsset pendingZip;
        private UnityEngine.UI.Button optionApplyButton;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            EnsureExists();
        }

        /// <summary>UpdateChecker がいなければ生成する (UpdateNoticeUI からも呼べるよう public)。</summary>
        public static void EnsureExists()
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
            StartCoroutine(CheckForUpdate());
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        /// <summary>
        /// 起動時の確認。通信は非同期でブロックせず、失敗はログのみで静かに終わる。
        /// ここではダウンロードしない (ユーザーが F3 / クリックしたときだけ <see cref="RequestDownload"/>)。
        /// </summary>
        private IEnumerator CheckForUpdate()
        {
            var current = UpdateVersion.ParseBuildId(Application.version);

            // ── -1. 前回の更新結果を反映 (issue #125) ──────────────────
            // Update.ps1 がすべての終了経路で書く last-result.json を読み、Title 通知の文言を
            // Model に渡す。表示と mark-as-shown (リネーム) は UpdateNoticeUI が Title で行う。
            // ここではブロックしない (ファイル読み取りのみ・失敗しても静かに続行)。
            var lastResult = UpdateResultFile.Read();
            if (lastResult != null)
            {
                var currentVersionText = current.Valid ? current.Version : Application.version;
                var lastNotice = UpdateResultFile.BuildNoticeText(lastResult, currentVersionText);
                if (lastNotice != null)
                {
                    Model.SetLastResultNotice(lastNotice);
                    Debug.Log("[UpdateChecker] 前回の更新結果を検出: result=" + lastResult.result +
                              " exitCode=" + lastResult.exitCode + "。Title で通知します。");
                }
            }

            if (!current.Valid)
            {
                Debug.LogWarning("[UpdateChecker] ビルド版識別子を解釈できません: '" + Application.version +
                                 "' — 更新確認を省略します (現行版を維持します)。");
                yield break;
            }

            // ── 0. チャンネル (stable / preview) を確定する ──
            // 展示用ビルド (stable) は prerelease を更新候補にしない。
            var channel = UpdateChannel.ReadInstalledChannel();
            var includePrerelease = UpdateChannel.IncludesPrerelease(channel);
            Debug.Log("[UpdateChecker] 更新チャンネル: " + channel +
                      " (prerelease を更新候補に含める: " + (includePrerelease ? "yes" : "no") + ")");

            // ── 1. Releases 一覧を取得 (draft は認証なしでは返らない) ──
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

            var latest = ParseLatestRelease(listReq.downloadHandler.text, includePrerelease);
            if (latest == null)
            {
                Debug.LogWarning("[UpdateChecker] Releases の応答を解釈できませんでした — 現行版のまま続行します。");
                yield break;
            }

            var latestId = UpdateVersion.ParseTag(latest.tag_name);
            if (!latestId.Valid)
            {
                Debug.Log("[UpdateChecker] 最新です (build=" + Application.version + ", latest=" +
                          (latest.tag_name ?? "(無効なタグ)") + ")。");
                yield break;
            }

            if (UpdateVersion.IsSameVersionDifferentBuild(latestId, current))
            {
                // 同じバージョンで別のビルドは「新旧」を判定できない (SHA に大小関係がない)。
                // 自動適用すると古いビルドへ戻す可能性があるので、更新候補にしない。
                Debug.Log("[UpdateChecker] 同じバージョン " + latestId.Version + " の別ビルドがあります " +
                          "(" + latest.tag_name + " / 現行 " + current.ShortSha + ")。" +
                          "更新はしません (適用は運営者が Update.ps1 -Tag で行えます)。");
                yield break;
            }

            if (!UpdateVersion.IsNewerThan(latestId, current))
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

            Debug.Log("[UpdateChecker] 新版を確認: " + latest.tag_name + " (現行 " + Application.version + ")。" +
                      "ダウンロードはユーザーが Title 画面で指示したときだけ行います。");
            Model.CheckFoundNewVersion(latest.tag_name, latestId.Version);
            pendingZip = zip;

            // ── 3. 同一タグの staging 済みがあれば再利用 (前回 staging したが適用しなかった場合) ──
            var existing = UpdateStaging.ReadManifest();
            if (existing != null && existing.tag == latest.tag_name)
            {
                string reuseProblem;
                if (UpdateStaging.VerifyStaged(existing, out reuseProblem))
                {
                    staged = existing;
                    Model.MarkStagedReady();
                    Debug.Log("[UpdateChecker] staging 済みの新版を再利用します: " + existing.zipName +
                              " (適用はユーザー確認時のみ)。");
                    yield break;
                }
                Debug.LogWarning("[UpdateChecker] staging 済み ZIP を再検証できませんでした (" + reuseProblem + ") — 再ダウンロードします。");
            }

            // ここで終わる。ダウンロードは RequestDownload() で開始する。
        }

        /// <summary>ユーザー操作 (Title 通知の F3 / クリック) によるダウンロード開始。
        /// Available のときだけ受け付ける。自動では呼ばれない。</summary>
        public void RequestDownload()
        {
            if (Model.Phase != UpdateFlowPhase.Available || pendingZip == null) { return; }
            Model.BeginDownload();
            StartCoroutine(DownloadAndStage(pendingZip));
        }

        /// <summary>ZIP + .sha256 を staging へ取得して SHA-256 検証し、manifest を書く。
        /// 進捗は Model 経由で通知 UI に見せる。失敗時は Available に戻り現行版を維持する。</summary>
        private IEnumerator DownloadAndStage(ReleaseAsset zip)
        {
            // 現行より新しくない ZIP は staging しない (issue #125: 新旧判定できないものは適用しない)。
            if (!IsStagedTagNewerThanCurrent(Model.LatestTag, Application.version))
            {
                Debug.LogWarning("[UpdateChecker] 候補の版が現行より新しくありません (" + Model.LatestTag +
                                 " / 現行 " + Application.version + ") — ダウンロードを中止し、現行版を維持します。");
                Model.DownloadFailed("候補の版が現行より新しくありません。現行のまま使えます");
                yield break;
            }

            UpdateStaging.ClearStaging();
            var zipPath = Path.Combine(UpdateStaging.StagingDir, zip.name);

            yield return DownloadToFile(zip.browser_download_url, zipPath,
                                        progress => Model.SetDownloadProgress(progress * 0.9f));
            if (!File.Exists(zipPath))
            {
                Debug.LogWarning("[UpdateChecker] ZIP のダウンロードに失敗しました — 更新を中止し、現行版を維持します。");
                UpdateStaging.ClearStaging();
                Model.DownloadFailed("ダウンロードに失敗しました。現行のまま使えます");
                yield break;
            }

            yield return FetchExpectedSha256(zip.browser_download_url + ".sha256");
            Model.SetDownloadProgress(0.95f);
            var expectedSha = lastFetchedSha256;
            if (string.IsNullOrEmpty(expectedSha))
            {
                Debug.LogWarning("[UpdateChecker] .sha256 を取得できませんでした — 更新を中止し、現行版を維持します。");
                UpdateStaging.ClearStaging();
                Model.DownloadFailed(".sha256 を取得できませんでした。現行のまま使えます");
                yield break;
            }

            // ── SHA-256 検証 (大きい ZIP のためバックグラウンドで計算) ──
            string actualSha = null;
            string computeError = null;
            var task = System.Threading.Tasks.Task.Run(() =>
            {
                try { actualSha = UpdateStaging.ComputeSha256(zipPath); }
                catch (Exception e) { computeError = e.Message; }
            });
            while (!task.IsCompleted) { yield return null; }
            Model.SetDownloadProgress(1f);

            if (computeError != null || !string.Equals(actualSha, expectedSha, System.StringComparison.OrdinalIgnoreCase))
            {
                Debug.LogWarning("[UpdateChecker] SHA-256 検証に失敗しました (actual=" + (actualSha ?? computeError) +
                                 ", expected=" + expectedSha + ") — 更新を中止し、現行版を維持します。");
                UpdateStaging.ClearStaging();
                Model.DownloadFailed("SHA-256 検証に失敗しました。現行のまま使えます");
                yield break;
            }

            // RequestDownload は Available (LatestTag 設定済み) からしか来ないため tag は必ず非空。
            staged = new UpdateStaging.StagedUpdate
            {
                tag = Model.LatestTag,
                zipName = zip.name,
                sha256 = expectedSha,
                stagedAtUtc = DateTime.UtcNow.ToString("o"),
            };
            if (string.IsNullOrEmpty(staged.tag))
            {
                // LatestTag が無い状態でこのメソッドには到達しない (RequestDownload が Available 限定のため)。
                UpdateStaging.ClearStaging();
                staged = null;
                Model.DownloadFailed("内部エラー: タグが不明です");
                yield break;
            }
            if (!UpdateStaging.WriteManifest(staged))
            {
                Debug.LogWarning("[UpdateChecker] staging manifest を書けませんでした — 更新を中止し、現行版を維持します。");
                staged = null;
                UpdateStaging.ClearStaging();
                Model.DownloadFailed("staging に書き込めませんでした。現行のまま使えます");
                yield break;
            }

            Model.DownloadSucceeded();
            Debug.Log("[UpdateChecker] 新版の staging が完了しました: " + staged.tag +
                      " (sha256=" + expectedSha + ")。適用はユーザーが確認したときだけ行われます。");
        }

        /// <summary>ユーザー操作 (Title 通知の F3 / クリック、Option の適用ボタン) による適用開始。
        /// staging 済み更新が無い・準備ができていないときは何もしない。自動では呼ばれない。</summary>
        public void RequestApply()
        {
            if (staged == null)
            {
                Debug.LogWarning("[UpdateChecker] 適用できる staging 済み更新がありません。");
                return;
            }
            if (Model.Phase == UpdateFlowPhase.Ready)
            {
                Model.BeginApply();
            }
            else if (Model.Phase != UpdateFlowPhase.Applying)
            {
                return;
            }

            if (!TryLaunchUpdater(waitForThisProcess: true))
            {
                Model.ApplyFailed("更新スクリプトを起動できませんでした。現行のまま使えます");
                return;
            }
            // 更新スクリプトは分離プロセスで自分の PID の終了を待つので、即座に終了してよい。
            Application.Quit();
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

        private static IEnumerator DownloadToFile(string url, string destPath, Action<float> onProgress)
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
            var op = req.SendWebRequest();
            while (!op.isDone)
            {
                if (onProgress != null) { onProgress(op.progress); }
                yield return null;
            }
            if (onProgress != null) { onProgress(1f); }
            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning("[UpdateChecker] ダウンロード失敗: " + url + " (" + req.error + ")");
                try { File.Delete(destPath); } catch (Exception) { }
            }
        }

        /// <summary>Releases 一覧 JSON から適用候補 (非 draft・タグ形式が有効) を選ぶ。
        /// includePrerelease が false のとき prerelease は候補から外れる (stable チャンネル)。
        /// API の返却順は作成順と限らない (実測でも非順序) ため、version を主キー、
        /// published_at を副キーに最大のものを選ぶ。public は EditMode テストのため。</summary>
        public static ReleaseInfo ParseLatestRelease(string json, bool includePrerelease)
        {
            try
            {
                // JsonUtility はトップレベル配列を解釈できないためラップする
                var wrapped = new StringBuilder("{\"items\":").Append(json).Append("}").ToString();
                var parsed = JsonUtility.FromJson<ReleaseArray>(wrapped);
                if (parsed == null || parsed.items == null) { return null; }

                ReleaseInfo best = null;
                UpdateVersion.ReleaseId bestId = default;
                var bestPublished = DateTime.MinValue;
                foreach (var release in parsed.items)
                {
                    if (release == null || release.draft || string.IsNullOrEmpty(release.tag_name)) { continue; }
                    if (!includePrerelease && release.prerelease) { continue; }
                    var id = UpdateVersion.ParseTag(release.tag_name);
                    if (!id.Valid) { continue; }
                    var published = ParseTimestamp(release.published_at) ?? ParseTimestamp(release.created_at) ?? DateTime.MinValue;
                    if (best == null || CompareCandidate(id, published, bestId, bestPublished) > 0)
                    {
                        best = release;
                        bestId = id;
                        bestPublished = published;
                    }
                }
                return best;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[UpdateChecker] Releases JSON の解析に失敗しました: " + e.Message);
                return null;
            }
        }

        private static int CompareCandidate(UpdateVersion.ReleaseId a, DateTime aPublished, UpdateVersion.ReleaseId b, DateTime bPublished)
        {
            var byVersion = UpdateVersion.CompareVersions(a.Version, b.Version);
            if (byVersion != 0) { return byVersion; }
            return aPublished.CompareTo(bPublished);
        }

        private static DateTime? ParseTimestamp(string text)
        {
            if (string.IsNullOrEmpty(text)) { return null; }
            DateTime value;
            if (DateTime.TryParse(text, System.Globalization.CultureInfo.InvariantCulture,
                                  System.Globalization.DateTimeStyles.RoundtripKind, out value))
            {
                return value;
            }
            return null;
        }

        /// <summary>staging 対象 / 適用対象のタグが現行ビルドより新しい版か (issue #125)。
        /// 純粋関数 (EditMode テスト対象)。タグまたは現行ビルド識別子が解釈できない、
        /// 同一バージョンの別ビルド、旧版のいずれでも false (適用しない)。</summary>
        public static bool IsStagedTagNewerThanCurrent(string tag, string currentBuildId)
        {
            var tagId = UpdateVersion.ParseTag(tag);
            var current = UpdateVersion.ParseBuildId(currentBuildId);
            return UpdateVersion.IsNewerThan(tagId, current);
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
        /// staging 済み更新が無いときは出さない。押すことは明示的なユーザー同意である。</summary>
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

        /// <summary>Option の「更新を適用して再起動」から呼ぶ。ユーザーの明示的な同意であり、
        /// ここからだけが適用への入口 (OnApplicationQuit からの自動適用は存在しない)。</summary>
        public static void ApplyStagedUpdateNow()
        {
            var checker = FindAnyObjectByType<UpdateChecker>();
            if (checker == null || checker.staged == null)
            {
                Debug.LogWarning("[UpdateChecker] 適用できる staging 済み更新がありません。");
                return;
            }
            checker.RequestApply();
        }

        /// <summary>配布物内 tools\update\Update.ps1 を分離プロセスで起動する。
        /// staging 済み ZIP を起動前にもう一度検証し、失敗したら起動しない (現行版を維持)。</summary>
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

            // staging 済み ZIP が現行より新しい版か (issue #125)。古い / 同一バージョンの別ビルドは適用しない。
            if (!IsStagedTagNewerThanCurrent(staged.tag, Application.version))
            {
                Debug.LogWarning("[UpdateChecker] staging 済みの版が現行より新しくありません (" + staged.tag +
                                 " / 現行 " + Application.version + ") — 適用を中止し、現行版を維持します。");
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
                // issue #125: 起動の成否はこのログでは確定しない (実結果は Update.ps1 が書く
                // last-result.json を次回起動時に検証する)。
                Debug.Log("[UpdateChecker] update script started (result unknown until next start): " + staged.tag + ".");
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
