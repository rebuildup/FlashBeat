using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;
using FlashBeat.Gameplay;
using FlashBeat.UI.V6;
using FlashBeat.UI.V6.Select;
using FlashBeat.UI.V6.Select.View;

/// <summary>
/// #416 Native 資源の寿命・再入場メモリ計測ハーネス (実機プレイヤー用)。
///
/// 使い方 (プレイヤー引数):
///   -autotest v6mem -autotest-out &lt;dir&gt;
///   [-autotest-rounds &lt;UI 巡回数, 既定 3&gt;]
///   [-autotest-games &lt;プレイ回数, 既定 2&gt;]
///   [-autotest-songs &lt;カンマ区切り曲名, 既定 "ツイッターランド,まにまに"&gt;]
///
/// 計測内容 (Issue #416 の実行項目に対応):
///   1. UI 巡回: Title → Select → Option → Select を rounds 回まわし、
///      各シーンで資源センサスを取る (再入場で単調増加しないか)。
///   2. プレイ再入場: games 回、選曲 → Ready → GameScene → 再生 →
///      ResultScene を通し、Game 突入直後 / 演奏中 / Result の各点で
///      センサスを取る (VideoPlayer / RenderTexture / AudioClip の寿命)。
///   3. SelectScene 内の History/Ranking モード切替 (キー注入で L/R/Esc を
///      往復) と Result→Retry の再入場を rounds 回まわす。
///   4. 遠隔ジャケットキャッシュ: SeedRemoteEntryForAudit で
///      RemoteCacheCapacity を超えるエントリを注入し、LRU 退避で
///      Texture2D が実際に解放されるか、DestroyAll で全解放できるかを
///      センサスの前後差で確かめる。あわせて失敗・重複要求・
///      シーン退出をまたぐコールバックを実 HTTP で確認する。
///   5. MediaCacheManager.GetCacheSizeBytes を 10/100/1000 ファイルの
///      一時ディレクトリでコールド/ウォーム計測する (TTL 失効の再スキャンは
///      EditMode テスト側で保証)。
///
/// センサス項目:
///   - GC.GetTotalMemory / Profiler.GetMonoUsedSizeLong (マネージド)
///   - Profiler.GetTotalAllocatedMemoryLong / GetTotalReservedMemoryLong
///     (プレイヤー全体の確保/予約。Native 側の増加はこちらに出る)
///   - Resources.FindObjectsOfTypeAll による Texture2D / Sprite /
///     Material / RenderTexture / AudioClip / Mesh / GameObject の個数と
///     Profiler.GetRuntimeMemorySizeLong の合計 (ロード済み資産の内訳)
///   - V6JacketProvider / V6AssetOwner / V6Sprites / MediaCacheManager の
///     監査カウンタ
///
/// 「ロード済みで残っているもの」と「解放されないもの」の区別は、
/// 同一操作の周回で個数・バイト数が頭打ちになるか (収束) で判断する。
/// 結果は Debug.Log ([V6Mem] ...) と &lt;out&gt;/autotest-v6mem-result.txt。
/// Memory Profiler パッケージの GUI スナップショットは自動化できないため、
/// 本ハーネスは Profiler API + オブジェクト列挙で同等の before/after を取る。
/// </summary>
public class AutoTestV6Mem : MonoBehaviour
{
    const string Tag = "[V6Mem]";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (V6ScreenAudit.GetArg("-autotest") != "v6mem") { return; }
        var go = new GameObject("AutoTestV6Mem");
        DontDestroyOnLoad(go);
        go.AddComponent<AutoTestV6Mem>();
    }

    const float SettleSeconds = 4f;
    const float SelectSceneWait = 6f;
    const float MediaReadyTimeout = 300f;
    const float SongEndTimeout = 300f;
    const float HitWindowSeconds = 0.12f;

    string outDir;
    readonly StringBuilder result = new StringBuilder();
    int uiRounds = 3;
    int gameCount = 2;
    string[] songTitles = { "ツイッターランド", "まにまに" };
    string[] uiScenes = { "TitleScene", "SelectScene", "OptionScene", "SelectScene" };

    // AutoHit 用 (AutoTestV6SongEnd と同じ方式: 実時計に合わせてキー注入)。
    readonly HashSet<int> hitSent = new HashSet<int>();
    readonly bool[] laneInjectedThisFrame = new bool[8];
    int hitsSent;

    IEnumerator Start()
    {
        outDir = V6ScreenAudit.GetArg("-autotest-out");
        if (string.IsNullOrEmpty(outDir)) { outDir = Application.persistentDataPath; }
        string rounds = V6ScreenAudit.GetArg("-autotest-rounds");
        int parsed;
        if (!string.IsNullOrEmpty(rounds) && int.TryParse(rounds, out parsed) && parsed > 0) { uiRounds = parsed; }
        string games = V6ScreenAudit.GetArg("-autotest-games");
        if (!string.IsNullOrEmpty(games) && int.TryParse(games, out parsed) && parsed >= 0) { gameCount = parsed; }
        string songs = V6ScreenAudit.GetArg("-autotest-songs");
        if (!string.IsNullOrEmpty(songs))
        {
            songTitles = songs.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
        }
        string ui = V6ScreenAudit.GetArg("-autotest-ui");
        if (!string.IsNullOrEmpty(ui))
        {
            uiScenes = ui.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
        }
        try { Directory.CreateDirectory(outDir); }
        catch (Exception) { }

        Log("start out=" + outDir + " uiRounds=" + uiRounds + " games=" + gameCount
            + " ui=" + string.Join(">", uiScenes)
            + " songs=" + string.Join("|", songTitles)
            + " resolution=" + Screen.width + "x" + Screen.height);

        yield return RunAll();
    }

    IEnumerator RunAll()
    {
        Census("boot");

        // --- 1. UI 巡回 (シーン再入場)。-autotest-ui で対象を絞れる
        //     (リークするシーンの切り分け用)。既定は Title→Select→Option→Select。
        for (int r = 0; r < uiRounds; r++)
        {
            for (int s = 0; s < uiScenes.Length; s++)
            {
                string scene = uiScenes[s];
                yield return LoadAndSettle(scene, scene.ToLowerInvariant() + "-" + r);
            }
        }

        // --- 1b. SelectScene 内の History/Ranking モード切替 ---
        //     L/R キー注入でモードを往復し、ビューの表示切替を繰り返す。
        //     uiScenes に SelectScene が無い (bisect 用) ときは飛ばす。
        if (Array.IndexOf(uiScenes, "SelectScene") >= 0)
        {
            yield return SubModeTour();
        }

        // --- 2. プレイ再入場 (多曲アクセスを兼ねる) ---
        for (int g = 0; g < gameCount; g++)
        {
            string title = songTitles[Math.Min(g, songTitles.Length - 1)];
            yield return PlayOnce(g, title);
        }

        // --- 2b. Result → Retry の再入場 ---
        if (gameCount > 0)
        {
            yield return RetryProbe();
        }

        // --- 3. 遠隔ジャケットキャッシュの上限・退避・全解放 ---
        yield return CacheEvictionTest();

        // --- 4. メディアキャッシュ容量スキャンのコールド/ウォーム計測 ---
        yield return MediaCacheProbe();

        // --- 5. 遠隔画像の失敗・重複要求・シーン退出をまたぐコールバック ---
        yield return RemoteLifecycleProbe();

        Log("done");
        Finish();
    }

    IEnumerator LoadAndSettle(string scene, string label)
    {
        SceneManager.LoadScene(scene);
        yield return new WaitForSecondsRealtime(SettleSeconds);
        Census(label + " scene=" + SceneManager.GetActiveScene().name);
    }

    /// <summary>1 曲を選択 → Ready → GameScene → 再生 → ResultScene まで通す。</summary>
    IEnumerator PlayOnce(int index, string title)
    {
        string p = "g" + index + "-" + title;

        SceneManager.LoadScene("SelectScene");
        yield return new WaitForSecondsRealtime(SelectSceneWait);
        Census(p + " select");

        V6SelectView view = V6SelectView.Current;
        if (view == null || view.Data == null)
        {
            Log("FAIL " + p + " select view/data missing");
            yield break;
        }
        IV6SelectDataSource data = view.Data;
        V6SongEntry target = FindSong(data.AllSongs, title);
        if (target == null)
        {
            Log("FAIL " + p + " song not in catalog: " + title);
            yield break;
        }

        data.Select(target.SongId);
        yield return new WaitForSecondsRealtime(0.4f);
        GManager.songID = target.SongId;
        GManager.ResetSession();
        hitSent.Clear();
        hitsSent = 0;

        view.TriggerReady();
        float waited = 0f;
        while (waited < 20f && SceneManager.GetActiveScene().name != "GameScene")
        {
            waited += 0.5f;
            yield return new WaitForSecondsRealtime(0.5f);
        }
        if (SceneManager.GetActiveScene().name != "GameScene")
        {
            Log("FAIL " + p + " GameScene not reached");
            yield break;
        }

        // GameScene 突入直後 (メディア準備前) のセンサス: シーン資産の山。
        yield return new WaitForSecondsRealtime(3f);
        Census(p + " game-enter");

        MusicManager music = FindFirstObjectByType<MusicManager>();
        if (music == null)
        {
            Log("FAIL " + p + " MusicManager missing");
            yield break;
        }
        float mediaWaited = 0f;
        while (mediaWaited < MediaReadyTimeout && !music.MediaReady)
        {
            mediaWaited += 1f;
            yield return new WaitForSecondsRealtime(1f);
        }
        Log(p + " media ready=" + music.MediaReady + " audioMode=" + music.AudioModeName
            + " videoPrepared=" + music.VideoPrepared + " failure=" + MusicManager.LastFailure);
        if (!music.MediaReady) { yield break; }

        KeyBindings.InjectKeyDown(KeyCode.Space);
        yield return new WaitForSecondsRealtime(1.5f);
        if (!music.PlaybackStarted) { Log("FAIL " + p + " playback did not start"); yield break; }

        // 演奏中 (定常状態) のセンサス。
        yield return new WaitForSecondsRealtime(10f);
        Census(p + " gameplay");

        NotesManager notes = FindFirstObjectByType<NotesManager>();
        float loopStart = Time.realtimeSinceStartup;
        SongEndState endState = SongEndState.Playing;
        while (Time.realtimeSinceStartup - loopStart < SongEndTimeout)
        {
            if (SceneManager.GetActiveScene().name == "ResultScene") { break; }
            endState = music.SongEndState;
            if (endState == SongEndState.Failed) { break; }
            AutoHit(notes);
            yield return null;
        }
        Log(p + " end state=" + endState + " scene=" + SceneManager.GetActiveScene().name
            + " judged=" + (GManager.perfect + GManager.great + GManager.bad + GManager.miss)
            + " hitsSent=" + hitsSent);

        yield return new WaitForSecondsRealtime(2f);
        Census(p + " result scene=" + SceneManager.GetActiveScene().name);
    }

    /// <summary>
    /// SelectScene 内で History / Ranking モードをキー注入で往復する。
    /// シーンを跨がない表示切替なので、資源がモード往復で増えないかを見る
    /// (#415 の差分更新と #416 の資源寿命の両方に関係)。
    /// </summary>
    IEnumerator SubModeTour()
    {
        SceneManager.LoadScene("SelectScene");
        yield return new WaitForSecondsRealtime(SelectSceneWait);
        Census("submode select-entry scene=" + SceneManager.GetActiveScene().name);

        for (int r = 0; r < uiRounds; r++)
        {
            // L → History
            InjectKey(KeyCode.L);
            yield return new WaitForSecondsRealtime(SettleSeconds);
            Census("submode history-" + r);

            // Esc (Back) → Select に戻る
            InjectAction(KeyActions.KeyAction.Back);
            yield return new WaitForSecondsRealtime(SettleSeconds);
            Census("submode selectback-" + r);

            // R → Ranking
            InjectKey(KeyCode.R);
            yield return new WaitForSecondsRealtime(SettleSeconds);
            Census("submode ranking-" + r);

            InjectAction(KeyActions.KeyAction.Back);
            yield return new WaitForSecondsRealtime(SettleSeconds);
            Census("submode selectback2-" + r);
        }
    }

    /// <summary>
    /// ResultScene から Retry で GameScene へ再入場する計測。
    /// RetryGame 経路 (GManager.ResetSession + シーン再読込) の資源寿命を見る。
    /// </summary>
    IEnumerator RetryProbe()
    {
        var mgr = FindFirstObjectByType<ResultSceneManager>();
        if (mgr == null)
        {
            Log("FAIL retry: ResultSceneManager missing scene=" + SceneManager.GetActiveScene().name);
            yield break;
        }

        // Initiate.Fade は前のフェードが終わるまで "Already Fading" で拒否する。
        // Result 突入直後は fade-in が残っていることがあるので、
        // GameScene に着くまで Retry() を繰り返し呼ぶ (拒否されるだけで副作用なし)。
        bool reached = false;
        for (int attempt = 0; attempt < 20 && !reached; attempt++)
        {
            mgr.Retry();
            float waited = 0f;
            while (waited < 3f && SceneManager.GetActiveScene().name != "GameScene")
            {
                waited += 0.25f;
                yield return new WaitForSecondsRealtime(0.25f);
            }
            reached = SceneManager.GetActiveScene().name == "GameScene";
        }
        if (!reached)
        {
            Log("FAIL retry: GameScene not reached");
            yield break;
        }

        yield return new WaitForSecondsRealtime(3f);
        Census("retry game-enter");

        MusicManager music = FindFirstObjectByType<MusicManager>();
        float mediaWaited = 0f;
        while (music != null && mediaWaited < MediaReadyTimeout && !music.MediaReady)
        {
            mediaWaited += 1f;
            yield return new WaitForSecondsRealtime(1f);
        }
        Log("retry media ready=" + (music != null && music.MediaReady)
            + " videoPrepared=" + (music != null && music.VideoPrepared));

        yield return new WaitForSecondsRealtime(10f);
        Census("retry gameplay");

        // 演奏を打ち切って SelectScene へ戻り、後続プローブのセンサスを
        // ゲーム中オブジェクトで汚さない。
        SceneManager.LoadScene("SelectScene");
        yield return new WaitForSecondsRealtime(SelectSceneWait);
        Census("post-retry select scene=" + SceneManager.GetActiveScene().name);
    }

    /// <summary>
    /// MediaCacheManager.GetCacheSizeBytes のコールド/ウォームを
    /// 10 / 100 / 1000 ファイルで計測する。TTL 失効後の再スキャンは
    /// EditMode (MediaCacheManagerTests.SnapshotExpiresAfterTheTtl) で
    /// 保証済みなので、ここでは実ディスク I/O の壁時計だけを見る。
    /// </summary>
    IEnumerator MediaCacheProbe()
    {
        int[] counts = { 10, 100, 1000 };
        string baseDir = Path.Combine(Path.GetTempPath(), "v6mem-mediacache");
        try { if (Directory.Exists(baseDir)) { Directory.Delete(baseDir, true); } }
        catch (Exception) { }
        Directory.CreateDirectory(baseDir);

        var payload = new byte[64 * 1024];
        new System.Random(1).NextBytes(payload);

        foreach (int n in counts)
        {
            string dir = Path.Combine(baseDir, "case-" + n);
            Directory.CreateDirectory(dir);
            for (int i = 0; i < n; i++)
            {
                File.WriteAllBytes(Path.Combine(dir, "f" + i + ".bin"), payload);
            }

            int scansBefore = MediaCacheManager.SizeScanCount;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            long bytes = MediaCacheManager.GetCacheSizeBytes(dir);
            sw.Stop();
            long coldMs = sw.ElapsedMilliseconds;

            sw.Restart();
            long bytesWarm = MediaCacheManager.GetCacheSizeBytes(dir);
            sw.Stop();
            long warmMs = sw.ElapsedMilliseconds;

            int scansDelta = MediaCacheManager.SizeScanCount - scansBefore;
            Log(string.Format(CultureInfo.InvariantCulture,
                "mediacache files={0} bytes={1} coldMs={2} warmMs={3} scansDelta={4} warmSame={5}",
                n, bytes, coldMs, warmMs, scansDelta, bytes == bytesWarm));

            try { Directory.Delete(dir, true); } catch (Exception) { }
        }

        try { Directory.Delete(baseDir, true); } catch (Exception) { }
        yield break;
    }

    /// <summary>
    /// 遠隔画像取得の失敗系を実機で確認する:
    ///   - 同一 videoId の連続要求は HTTP を増やさない (dedup)
    ///   - 失敗は負キャッシュされ、即時の再要求は HTTP を出さない
    ///   - in-flight の要求はシーンを跨いでも生存し、コールバックが届く
    ///     (呼び出し側の生存確認は各 View の this==null ガードが担う)
    /// </summary>
    IEnumerator RemoteLifecycleProbe()
    {
        int fetch0 = V6JacketProvider.RemoteFetchCount;
        int dedup0 = V6JacketProvider.RemoteDedupCount;
        int fail0 = V6JacketProvider.RemoteFailureCount;
        int neg0 = V6JacketProvider.NegativeCacheHitCount;
        int deliveredSprites = 0;
        int deliveredTextures = 0;

        string bogus = "v6mem-no-such-video-000";
        V6JacketProvider.RequestRemote(bogus, s => { deliveredSprites++; });
        V6JacketProvider.RequestRemote(bogus, s => { deliveredSprites++; });

        // 失敗 (404) を待つ。timeout は provider 側 30 秒が上限。
        float waited = 0f;
        float limit = V6JacketProvider.RemoteRequestTimeoutSeconds + 10f;
        while (waited < limit && V6JacketProvider.RemoteFailureCount == fail0)
        {
            waited += 0.5f;
            yield return new WaitForSecondsRealtime(0.5f);
        }
        Log(string.Format(CultureInfo.InvariantCulture,
            "remoteprobe failwait={0:F0}s fetch={1} dedup={2} fail={3} delivered={4}",
            waited,
            V6JacketProvider.RemoteFetchCount - fetch0,
            V6JacketProvider.RemoteDedupCount - dedup0,
            V6JacketProvider.RemoteFailureCount - fail0,
            deliveredSprites));

        // 負キャッシュ中の再要求: HTTP は増えず null が返る。
        V6JacketProvider.RequestRemote(bogus, s => { deliveredSprites++; });
        yield return new WaitForSecondsRealtime(0.5f);
        Log(string.Format(CultureInfo.InvariantCulture,
            "remoteprobe neg={0} fetchTotal={1} delivered={2}",
            V6JacketProvider.NegativeCacheHitCount - neg0,
            V6JacketProvider.RemoteFetchCount - fetch0,
            deliveredSprites));

        // in-flight の要求を残したままシーンを抜ける。RemoteHost は
        // DontDestroyOnLoad なので取得は継続し、応答時にコールバックが届く。
        V6JacketProvider.RequestRemoteTexture("v6mem-no-such-video-001",
            t => { deliveredTextures++; });
        SceneManager.LoadScene("TitleScene");
        yield return new WaitForSecondsRealtime(SettleSeconds + V6JacketProvider.RemoteRequestTimeoutSeconds + 5f);

        Log(string.Format(CultureInfo.InvariantCulture,
            "remoteprobe postexit scene={0} fetchTotal={1} deliveredTex={2} inFlightEntries(census)",
            SceneManager.GetActiveScene().name,
            V6JacketProvider.RemoteFetchCount - fetch0,
            deliveredTextures));
        Census("remote-exit scene=" + SceneManager.GetActiveScene().name);
    }

    /// <summary>遠隔キャッシュ上限・LRU 退避・DestroyAll の実機確認。</summary>
    IEnumerator CacheEvictionTest()
    {
        Census("pre-seed");

        int capacity = V6JacketProvider.RemoteCacheCapacity;
        int extra = 10;
        int total = capacity + extra;
        var seeds = new List<Texture2D>(total);
        int evictedBefore = V6JacketProvider.RemoteFetchEvictedCount;

        for (int i = 0; i < total; i++)
        {
            // 小さい Texture2D を注入 (実画像は不要: 退避と寿命だけを見る)。
            var tex = new Texture2D(8, 8, TextureFormat.RGBA32, false);
            tex.name = "v6mem-seed-" + i;
            seeds.Add(tex);
            V6JacketProvider.SeedRemoteEntryForAudit("v6mem-seed-" + i, tex, i);
        }
        yield return null;

        int evicted = V6JacketProvider.RemoteFetchEvictedCount - evictedBefore;
        int destroyed = 0;
        for (int i = 0; i < seeds.Count; i++)
        {
            // 退避されたエントリの Texture2D は Unity の null 判定で消えている。
            if (seeds[i] == null) { destroyed++; }
        }
        Log(string.Format(CultureInfo.InvariantCulture,
            "seed capacity={0} seeded={1} evictedCounter={2} texturesDestroyed={3}",
            capacity, total, evicted, destroyed));
        Census("post-seed");

        // 残ったシード分も含めて全解放し、センサスがベースラインへ戻るか見る。
        V6JacketProvider.DestroyAll();
        yield return null;
        Census("post-destroyall");
    }

    static V6SongEntry FindSong(IReadOnlyList<V6SongEntry> songs, string title)
    {
        if (songs == null) { return null; }
        for (int i = 0; i < songs.Count; i++)
        {
            V6SongEntry entry = songs[i];
            if (entry != null && entry.Title == title) { return entry; }
        }
        return null;
    }

    /// <summary>判定時刻に合わせたキー注入 (AutoTestV6SongEnd と同じ方式)。</summary>
    void AutoHit(NotesManager notes)
    {
        if (notes == null || !notes.IsReady) { return; }
        if (notes.NotesTime == null || notes.LaneNum == null) { return; }

        float mediaTime = GManager.step_time;
        int count = Mathf.Min(notes.NotesTime.Count, notes.LaneNum.Count);

        for (int lane = 0; lane < laneInjectedThisFrame.Length; lane++)
        {
            laneInjectedThisFrame[lane] = false;
        }

        for (int i = 0; i < count; i++)
        {
            if (hitSent.Contains(i)) { continue; }
            float time = notes.NotesTime[i];
            if (time > 900f) { hitSent.Add(i); continue; }

            int lane = notes.LaneNum[i];
            if (lane < 0 || lane > 7) { hitSent.Add(i); continue; }
            if (laneInjectedThisFrame[lane]) { continue; }

            float delta = time - mediaTime;
            if (delta > HitWindowSeconds) { continue; }
            if (delta < -0.25f) { hitSent.Add(i); continue; }

            KeyCode[] keys = KeyBindings.GetKeys(lane);
            if (keys == null || keys.Length == 0) { hitSent.Add(i); continue; }
            KeyBindings.InjectKeyDown(keys[0]);
            hitSent.Add(i);
            laneInjectedThisFrame[lane] = true;
            hitsSent++;
        }
    }

    /// <summary>資源センサス。1 行のログ + result ファイル行として記録する。</summary>
    void Census(string label)
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        long monoUsed = Profiler.GetMonoUsedSizeLong();
        long monoHeap = GC.GetTotalMemory(true);
        long allocated = Profiler.GetTotalAllocatedMemoryLong();
        long reserved = Profiler.GetTotalReservedMemoryLong();

        string objLine = ObjectCensus();
        string auditLine = AuditCounters();
        string typeLine = TypeCensus();

        string line = string.Format(CultureInfo.InvariantCulture,
            "census label={0} scene={1} monoUsedMB={2:F1} monoHeapMB={3:F1} allocMB={4:F1} reservedMB={5:F1} | {6} | {7}",
            label, SceneManager.GetActiveScene().name,
            monoUsed / 1048576.0, monoHeap / 1048576.0,
            allocated / 1048576.0, reserved / 1048576.0,
            objLine, auditLine);
        Log(line);
        Log("types label=" + label + " " + typeLine);
    }

    /// <summary>
    /// ロード済み全 UnityEngine.Object を型名で集計する。
    /// monoUsed が増え続けるのに個数系が頭打ちの場合、残る犯人は
    /// この表 (ネイティブは破棄済みだがマネージド側だけ残る型) か、
    /// 純粋な C# オブジェクト (文字列 / リスト / デリゲート) のどちらか。
    /// </summary>
    static string TypeCensus()
    {
        var all = Resources.FindObjectsOfTypeAll<UnityEngine.Object>();
        var groups = new Dictionary<string, TypeStat>();
        for (int i = 0; i < all.Length; i++)
        {
            UnityEngine.Object obj = all[i];
            if (obj == null) { continue; }
            string name = obj.GetType().Name;
            TypeStat stat;
            if (!groups.TryGetValue(name, out stat))
            {
                stat = new TypeStat();
                groups[name] = stat;
            }
            stat.Count++;
            stat.Bytes += Profiler.GetRuntimeMemorySizeLong(obj);
        }

        var sorted = new List<KeyValuePair<string, TypeStat>>(groups);
        sorted.Sort((a, b) => b.Value.Count.CompareTo(a.Value.Count));
        var sb = new StringBuilder();
        for (int i = 0; i < sorted.Count; i++)
        {
            if (i > 0) { sb.Append(" "); }
            sb.Append(sorted[i].Key).Append("=").Append(sorted[i].Value.Count);
        }
        return sb.ToString();
    }

    sealed class TypeStat
    {
        public int Count;
        public long Bytes;
    }

    /// <summary>ロード済み UnityEngine.Object の個数とランタイムメモリ合計。</summary>
    static string ObjectCensus()
    {
        var sb = new StringBuilder();
        AppendType<Texture2D>(sb, "tex2d");
        AppendType<Sprite>(sb, "sprite");
        AppendType<Material>(sb, "mat");
        AppendType<RenderTexture>(sb, "rt");
        AppendType<AudioClip>(sb, "audio");
        AppendType<Mesh>(sb, "mesh");
        AppendType<GameObject>(sb, "go");
        return sb.ToString();
    }

    static void AppendType<T>(StringBuilder sb, string tag) where T : UnityEngine.Object
    {
        var all = Resources.FindObjectsOfTypeAll<T>();
        long bytes = 0;
        // 上位 3 個の名前を残す (リーク源の特定用)。
        var top = new List<KeyValuePair<long, string>>(3);
        for (int i = 0; i < all.Length; i++)
        {
            T obj = all[i];
            if (obj == null) { continue; }
            long size = Profiler.GetRuntimeMemorySizeLong(obj);
            bytes += size;
            if (top.Count < 3 || size > top[top.Count - 1].Key)
            {
                top.Add(new KeyValuePair<long, string>(size, obj.name));
                top.Sort((a, b) => b.Key.CompareTo(a.Key));
                if (top.Count > 3) { top.RemoveAt(top.Count - 1); }
            }
        }
        sb.Append(tag).Append("=").Append(all.Length)
          .Append("(").Append((bytes / 1048576.0).ToString("F1", CultureInfo.InvariantCulture)).Append("MB)");
        if (top.Count > 0)
        {
            sb.Append("{");
            for (int i = 0; i < top.Count; i++)
            {
                if (i > 0) { sb.Append(","); }
                sb.Append(top[i].Value).Append(":")
                  .Append((top[i].Key / 1048576.0).ToString("F1", CultureInfo.InvariantCulture));
            }
            sb.Append("}");
        }
        sb.Append(" ");
    }

    /// <summary>アプリ側の監査カウンタ (実装済みのものだけ)。</summary>
    static string AuditCounters()
    {
        var sb = new StringBuilder();
        sb.Append("jacket[")
          .Append("local=").Append(V6JacketProvider.LocalLoadCount)
          .Append(" remote=").Append(V6JacketProvider.RemoteFetchCount)
          .Append(" dedup=").Append(V6JacketProvider.RemoteDedupCount)
          .Append(" neg=").Append(V6JacketProvider.NegativeCacheHitCount)
          .Append(" fail=").Append(V6JacketProvider.RemoteFailureCount)
          .Append(" crop=").Append(V6JacketProvider.CropCreateCount)
          .Append(" cached=").Append(V6JacketProvider.CachedSpriteCount)
          .Append(" remoteEntries=").Append(V6JacketProvider.RemoteCacheEntryCount)
          .Append(" remoteSprites=").Append(V6JacketProvider.RemoteCachedSpriteCount)
          .Append(" evicted=").Append(V6JacketProvider.RemoteFetchEvictedCount)
          .Append("]");
        sb.Append(" owner[shared=").Append(V6AssetOwner.SharedCount)
          .Append(" sharedCreate=").Append(V6AssetOwner.SharedCreateCount).Append("]");
        sb.Append(" sprites[iconCache=").Append(V6Sprites.IconCacheCount)
          .Append(" shared=").Append(V6Sprites.SharedCount)
          .Append(" create=").Append(V6Sprites.CreateCount).Append("]");
        sb.Append(" mediaCache[bytes=").Append(MediaCacheManager.GetCacheSizeBytes())
          .Append(" scans=").Append(MediaCacheManager.SizeScanCount).Append("]");
        return sb.ToString();
    }

    /// <summary>キーを 1 フレームだけ注入する (次フレームの GetKeyDownOrInjected で見える)。</summary>
    static void InjectKey(KeyCode key)
    {
        KeyBindings.InjectKeyDown(key);
    }

    /// <summary>KeyActions の操作に現在割り当てられている全キーを注入する。</summary>
    static void InjectAction(KeyActions.KeyAction action)
    {
        KeyCode[] keys = KeyActions.Get(action);
        for (int i = 0; i < keys.Length; i++)
        {
            KeyBindings.InjectKeyDown(keys[i]);
        }
    }

    void Log(string line)
    {
        string full = Tag + " " + line;
        Debug.Log(full);
        result.AppendLine(full);
    }

    void Finish()
    {
        string path = Path.Combine(outDir, "autotest-v6mem-result.txt");
        try { File.WriteAllText(path, result.ToString()); }
        catch (Exception e) { Debug.LogWarning(Tag + " write result failed: " + e.Message); }
        Application.Quit();
    }
}
