using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace FlashBeat.Gameplay
{
    /// <summary>
    /// 選曲画面で注目している曲のメディアを先にキャッシュへ落とす (Issue #377)。
    ///
    /// 選曲 → Ready → GameScene の流れでは、MusicManager.PrepareMediaCoroutine が
    /// yt-dlp の形式解決 (~秒) とダウンロード (初回 ~26 秒の実測あり) を
    /// その場で行う。ユーザーが行を行き来している間に同じ解決を裏で進めておけば、
    /// Ready 時には <see cref="YouTubeMediaCache.TryGetReadyFiles"/> がヒットして
    /// 形式解決ごとスキップできる。
    ///
    /// 契約:
    ///   - 同時に走る先読みは 1 本だけ。別の曲が選ばれたら前の先読みは
    ///     キャンセルする (EnsureFileAsync は動画ごとのゲートで直列化され、
    ///     中断ファイルは完了マーカーが付かないのでキャッシュを汚さない)。
    ///   - 取得済み (TryGetReadyFiles) の曲は何もしない。プロセス内で一度
    ///     温めた曲も再要求しない (キャッシュ削除は Option 画面の明示操作で、
    ///     その場合も再生側は通常経路で回復する)。
    ///   - 失敗はカウントするだけで再試行しない。連続して行を行き来しても
    ///     yt-dlp の多重起動にならない。
    /// </summary>
    public static class MediaPrefetcher
    {
        /// <summary>Request の呼び出し回数 (監査用)。</summary>
        public static int RequestCount { get; private set; }

        /// <summary>キャッシュ済みで即座にスキップした回数。</summary>
        public static int CacheHitCount { get; private set; }

        /// <summary>実際に解決/ダウンロードを開始した回数。</summary>
        public static int StartedCount { get; private set; }

        /// <summary>先読みがキャッシュを温めきった回数。</summary>
        public static int CompletedCount { get; private set; }

        /// <summary>別の曲への切替でキャンセルされた回数。</summary>
        public static int CancelledCount { get; private set; }

        /// <summary>解決またはダウンロードに失敗した回数。</summary>
        public static int FailedCount { get; private set; }

        /// <summary>現在先読み中の videoId (無ければ null)。</summary>
        public static string InFlightVideoId { get; private set; }

        /// <summary>
        /// テスト用シーム: 設定時は yt-dlp -F の起動を迂回して形式リストを返す。
        /// <see cref="YouTubeMediaCache.DownloadOverride"/> と同じ約束で、
        /// ゲームは設定しない。テストは TearDown で null に戻す。
        /// </summary>
        public static Func<string, CancellationToken, Task<List<YtDlpFormat>>> FormatsOverride;

        static readonly object gate = new object();
        static readonly HashSet<string> warm = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>
        /// このセッションで先読みに失敗した videoId。選択を行き来するたびに
        /// yt-dlp を再起動しないための抑止集合 (Issue #377)。
        /// キャンセル (別曲への切替) は失敗に数えない: 同じ曲へ戻れば再試行する。
        /// </summary>
        static readonly HashSet<string> failedOnce = new HashSet<string>(StringComparer.Ordinal);
        static CancellationTokenSource current;
        static Task currentRun = Task.CompletedTask;

        /// <summary>
        /// videoId のメディアを先読みする。戻り値はその先読みの完了を待つ Task
        /// (キャッシュ済み・多重要求では即完了の Task)。呼び出し側は通常捨てる。
        /// </summary>
        public static Task Request(string videoId)
        {
            if (string.IsNullOrEmpty(videoId))
            {
                return Task.CompletedTask;
            }

            lock (gate)
            {
                RequestCount++;

                // ffmpeg の有無は MusicManager と同じ判断 (TryGetReadyFiles の
                // canMerge) を使う。PATH の解決はプロセス内で繰り返しても軽い。
                bool canMerge = !string.IsNullOrEmpty(YouTubeStreamResolver.ResolveFfmpegPath());
                ReadyMedia ready;
                if (YouTubeMediaCache.TryGetReadyFiles(videoId, out ready, canMerge: canMerge))
                {
                    warm.Add(videoId);
                    CacheHitCount++;
                    CancelInFlightIfOther(videoId);
                    return Task.CompletedTask;
                }

                if (warm.Contains(videoId) || failedOnce.Contains(videoId))
                {
                    CancelInFlightIfOther(videoId);
                    return Task.CompletedTask;
                }

                if (InFlightVideoId == videoId)
                {
                    // 同じ曲の先読みが走っている: 新しい要求は既存の完了を待つ。
                    return currentRun;
                }

                // 別の曲が選ばれた: 進行中の先読みは中断する。
                // EnsureGame 内の DownloadGate が同一動画の処理を直列化するので、
                // 中断と新規開始が同じキャッシュを壊し合うことはない。
                // CTS は Dispose しない: 実行中の run が token.Register を
                // 非同期に呼ぶ経路 (YouTubeStreamResolver.RunAsync) があり、
                // 破棄済み source への Register は ObjectDisposedException で
                // yt-dlp の kill 自体を失う (MusicManager と同じ約束)。
                if (current != null)
                {
                    current.Cancel();
                    current = null;
                }

                var cts = new CancellationTokenSource();
                current = cts;
                InFlightVideoId = videoId;
                StartedCount++;
                currentRun = RunAsync(videoId, cts);
                return currentRun;
            }
        }

        /// <summary>
        /// 早期 return 経路用: 要求 videoId と異なる先読みが残っていれば中断する。
        /// cache hit / warm / failedOnce で即 return する場合でも
        /// 「同時に走る先読みは 1 本」の契約を守る (#561)。
        /// InFlightVideoId も畳む: 旧 run の finally は current != cts で
        /// 状態をクリアしないので、残すと stale な videoId が残り続ける。
        /// </summary>
        static void CancelInFlightIfOther(string videoId)
        {
            if (current == null || InFlightVideoId == videoId)
            {
                return;
            }

            current.Cancel();
            current = null;
            InFlightVideoId = null;
        }

        static async Task RunAsync(string videoId, CancellationTokenSource cts)
        {
            var token = cts.Token;
            // ConfigureAwait(false) は使わない: 継続がスレッドプールへ逃げると
            // Application.persistentDataPath (CacheDirectoryFor / TryGetReadyFiles)
            // をメインスレッド外から呼んで例外になる (EditMode 実測)。
            // Unity の同期コンテキストで再開させる。
            try
            {
                List<YtDlpFormat> formats;
                if (FormatsOverride != null)
                {
                    formats = await FormatsOverride(videoId, token);
                }
                else
                {
                    var formatsTask = YouTubeStreamResolver.ListFormatsAsync(videoId, token);
                    var formatsOut = await formatsTask;
                    formats = YouTubeStreamResolver.ParseFormats(formatsOut.Ok ? formatsOut.Stdout : "");
                }

                // 本番の形式プローブはキャンセルを例外ではなく非 0 終了で返す
                // (YouTubeStreamResolver.RunAsync が yt-dlp を kill して
                // Ok=false を返し、ParseFormats は空リストになる)。「別曲への
                // 切替」は失敗に数えない契約なので、空リストでも token を見る。
                if (formats == null || formats.Count == 0)
                {
                    if (token.IsCancellationRequested)
                    {
                        CountCancelled();
                    }
                    else
                    {
                        MarkFailed(videoId);
                    }
                    return;
                }

                bool canMerge = !string.IsNullOrEmpty(YouTubeStreamResolver.ResolveFfmpegPath());
                var plan = YouTubeMediaCache.SelectPlan(formats, canMerge);

                // MusicManager.PrepareMediaCoroutine と同じ取得順:
                // 結合ファイル → (失敗時) 映像のみフォールバック → 分離時は音声も。
                var video = await YouTubeMediaCache
                    .EnsureFileAsync(videoId, plan.VideoSelector, YouTubeMediaCache.PrefixForPlan(plan), token);

                bool combined = plan.Combined;
                if (!video.Ok && plan.Combined && !string.IsNullOrEmpty(plan.FallbackVideoSelector))
                {
                    // ゲート外の一括削除は使わない: 再生側 (MusicManager) が
                    // 同じ videoId を並行取得していると、完成直後の結合ファイルを
                    // 消しうる。完了済みファイルを残すゲート付き削除にする。
                    await YouTubeMediaCache
                        .RemoveIncompleteProducedFilesAsync(YouTubeMediaCache.CacheDirectoryFor(videoId), YouTubeMediaCache.MergedFilePrefix, token);
                    video = await YouTubeMediaCache
                        .EnsureFileAsync(videoId, plan.FallbackVideoSelector, YouTubeMediaCache.VideoFilePrefix, token);
                    combined = false;
                }

                if (plan.HasAudioFile && !combined)
                {
                    await YouTubeMediaCache
                        .EnsureFileAsync(videoId, plan.AudioSelector, YouTubeMediaCache.AudioFilePrefix, token);
                }

                ReadyMedia ready;
                if (YouTubeMediaCache.TryGetReadyFiles(videoId, out ready, canMerge: canMerge))
                {
                    lock (gate)
                    {
                        warm.Add(videoId);
                        CompletedCount++;
                    }
                }
                else if (token.IsCancellationRequested)
                {
                    // ダウンロード中のキャンセルも「失敗」にしない
                    // (EnsureFileAsync は中断を !Ok で返しうる)。
                    CountCancelled();
                }
                else
                {
                    MarkFailed(videoId);
                }
            }
            catch (OperationCanceledException)
            {
                CountCancelled();
            }
            catch (Exception e)
            {
                if (token.IsCancellationRequested)
                {
                    CountCancelled();
                }
                else
                {
                    MarkFailed(videoId);
                    Debug.LogWarning("[MediaPrefetcher] prefetch failed for " + videoId + ": " + e.Message);
                }
            }
            finally
            {
                lock (gate)
                {
                    // 自分が現行 run の時だけ状態を畳む。A→B→A の切替では
                    // 古い run の finally が新しい run の InFlightVideoId を
                    // 消してはいけない (videoId 比較では区別できない)。
                    if (ReferenceEquals(current, cts))
                    {
                        current = null;
                        currentRun = Task.CompletedTask;
                        cts.Dispose();
                        InFlightVideoId = null;
                    }
                }
            }
        }

        static void MarkFailed(string videoId)
        {
            lock (gate)
            {
                failedOnce.Add(videoId);
                FailedCount++;
            }
        }

        static void CountCancelled()
        {
            // 他のカウンタと同じく gate 内で更新する (継続スレッドから
            // Request と並行して呼ばれうる)。
            lock (gate)
            {
                CancelledCount++;
            }
        }

        /// <summary>監査・テスト用: warm 集合とカウンタを初期状態へ戻す。</summary>
        public static void ResetForAudit()
        {
            lock (gate)
            {
                if (current != null)
                {
                    current.Cancel();
                    current = null;
                }

                currentRun = Task.CompletedTask;
                warm.Clear();
                failedOnce.Clear();
                InFlightVideoId = null;
                // 実行中 run の CTS は Dispose しない (Request と同じ理由:
                // token.Register が ObjectDisposedException になりうる)。
                RequestCount = 0;
                CacheHitCount = 0;
                StartedCount = 0;
                CompletedCount = 0;
                CancelledCount = 0;
                FailedCount = 0;
            }
        }
    }
}
