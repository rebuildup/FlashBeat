using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace FlashBeat.Gameplay
{
    /// <summary>
    /// ダウンロードしたメディアファイルの正規化 (issue #796)。
    ///
    /// YouTube の progressive ストリーム (itag 18/22) や、元ストリームの
    /// timescale を引き継いだ ffmpeg マージ結果には、映像トラックの
    /// time_base が 1/11988 のような Unity VideoPlayer
    /// (AVFoundationVideoMedia) が解釈できない値を持つものがある。
    /// そのファイルは prepare には成功するが、再生時計が実時間の約 1/6 で
    /// しか進まない・音声が出ない・時刻が大きく跳ぶ、といった破綻を起こす
    /// (実機で確認済み: 同一映像を timescale 29970 へ再エンコードすると
    /// 正常に再生された)。
    ///
    /// 対処: ffmpeg が使える環境では、映像トラックだけを再エンコードして
    /// 「フレームレートと整合する timescale」へ正規化する。音声はコピーする。
    /// ffmpeg / ffprobe が無い環境では何もしない (従来どおりの挙動)。
    /// </summary>
    public static class MediaNormalizer
    {
        /// <summary>
        /// 正規化が必要とみなす映像 time_base の分母の上限 (排他)。
        ///
        /// 実測で正常に再生できたファイルの映像 timescale は 15360 / 30000 /
        /// 29970 で、破綻したのは 11988。閾値を既知の正常値の最小値に置き、
        /// それ未満だけを対象にする保守的な判定にしている (根本原因は
        /// Unity 側のパーサなので、実害が確認された値だけを直す)。
        /// </summary>
        public const long UnsafeTimeBaseDenominatorBelow = 15360;

        /// <summary>映像ストリームの time_base 分母が閾値未満なら正規化対象。</summary>
        public static bool NeedsNormalization(long timeBaseDenominator)
        {
            return timeBaseDenominator > 0 && timeBaseDenominator < UnsafeTimeBaseDenominatorBelow;
        }

        /// <summary>
        /// 指定フレームレート (num/den) に対して、全フレームの duration が
        /// 整数 tick になる最小の「素直な」timescale を返す。
        ///
        /// 1 フレームの長さは den/num 秒なので、timescale T での tick 数は
        /// T*den/num。T を num の倍数にすれば必ず整数になる。
        /// T = num*m とすると tick/フレーム = m*den なので、m を
        /// 「m*den ≒ 1000」になるように選ぶと、実績のある timescale
        /// (30fps→30000, 29.97fps→29970/30000) と同程度の粒度になる。
        /// 不正な入力には 0 を返す。
        /// </summary>
        public static long ComputeVideoTrackTimescale(int num, int den)
        {
            if (num <= 0 || den <= 0) return 0;
            // num/den を既約にする (T が num の倍数であることが条件なので、
            // 約分しても T*den/num の整数性は変わらない)。
            int g = Gcd(num, den);
            num /= g;
            den /= g;

            // m*den ≒ 1000 となる m (1 以上)。delta が小さすぎる/大きすぎる
            // timescale は避けたいので 1..10000 に丸める。
            long m = (1000 + den - 1) / den; // ceil(1000/den)
            if (m < 1) m = 1;
            if (m > 10000) m = 10000;

            long t = (long)num * m;
            // 念のため delta (= m*den) が正の整数であることを確認する。
            if (t <= 0 || m * den <= 0) return 0;
            return t;
        }

        static int Gcd(int a, int b)
        {
            while (b != 0)
            {
                int t = a % b;
                a = b;
                b = t;
            }
            return a == 0 ? 1 : a;
        }

        /// <summary>
        /// ffprobe の "key=value" 行から time_base=NUM/DEN の DEN を読む。
        /// 読めなければ 0。
        /// </summary>
        public static long ParseTimeBaseDenominator(string probeOutput)
        {
            if (string.IsNullOrEmpty(probeOutput)) return 0;
            foreach (var rawLine in probeOutput.Split('\n'))
            {
                var line = rawLine.Trim();
                const string prefix = "time_base=";
                if (!line.StartsWith(prefix, StringComparison.Ordinal)) continue;
                var parts = line.Substring(prefix.Length).Split('/');
                long den;
                if (parts.Length == 2 && long.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out den))
                {
                    return den;
                }
            }
            return 0;
        }

        /// <summary>
        /// ffprobe 出力から映像フレームレート (avg_frame_rate、無ければ
        /// r_frame_rate) を "num/den" または "num" として読む。
        /// 読めなければ false。
        /// </summary>
        public static bool TryParseFrameRate(string probeOutput, out int num, out int den)
        {
            num = 0;
            den = 0;
            if (string.IsNullOrEmpty(probeOutput)) return false;

            // avg_frame_rate を優先し、無効 (0/0) なら r_frame_rate を使う。
            string avg = null, rate = null;
            foreach (var rawLine in probeOutput.Split('\n'))
            {
                var line = rawLine.Trim();
                const string avgPrefix = "avg_frame_rate=";
                const string ratePrefix = "r_frame_rate=";
                if (line.StartsWith(avgPrefix, StringComparison.Ordinal)) avg = line.Substring(avgPrefix.Length);
                else if (line.StartsWith(ratePrefix, StringComparison.Ordinal)) rate = line.Substring(ratePrefix.Length);
            }

            return TryParseRate(avg, out num, out den) || TryParseRate(rate, out num, out den);
        }

        static bool TryParseRate(string text, out int num, out int den)
        {
            num = 0;
            den = 0;
            if (string.IsNullOrEmpty(text)) return false;
            var parts = text.Split('/');
            if (parts.Length == 1)
            {
                return int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out num) && num > 0
                    ? (den = 1) > 0
                    : false;
            }
            if (parts.Length != 2) return false;
            return int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out num)
                && int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out den)
                && num > 0 && den > 0;
        }

        /// <summary>
        /// ファイルの映像トラックが Unity で再生できない timescale を持つ場合、
        /// ffmpeg で映像を再エンコードして正規化する。音声はストリームコピー。
        ///
        /// 失敗しても例外は投げず false を返す: 正規化できないファイルは
        /// 従来どおりそのまま使われる (ダウンロード自体を失敗させない)。
        /// </summary>
        /// <returns>ファイルを置き換えたら true。</returns>
        public static async Task<bool> NormalizeVideoAsync(string path, CancellationToken cancellationToken = default)
        {
            try
            {
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) return false;

                // ffprobe で映像ストリームの time_base / frame rate を調べる。
                // 映像が無いファイル (音声のみ) はここで対象外になる。
                var ffprobe = await YouTubeStreamResolver.ResolveFfprobePathAsync().ConfigureAwait(false);
                if (string.IsNullOrEmpty(ffprobe)) return false;

                var probeOut = await RunToolAsync(ffprobe,
                    "-v error -select_streams v:0 -show_entries stream=time_base,avg_frame_rate,r_frame_rate -of default=noprint_wrappers=1 " +
                    YouTubeStreamResolver.Quote(path), 20000, cancellationToken).ConfigureAwait(false);
                if (probeOut == null) return false;

                long denominator = ParseTimeBaseDenominator(probeOut);
                if (!NeedsNormalization(denominator)) return false;

                int num, den;
                if (!TryParseFrameRate(probeOut, out num, out den))
                {
                    Debug.LogWarning($"[MediaNormalizer] cannot read frame rate of '{Path.GetFileName(path)}' - skip normalization");
                    return false;
                }
                long timescale = ComputeVideoTrackTimescale(num, den);
                if (timescale <= 0)
                {
                    Debug.LogWarning($"[MediaNormalizer] cannot compute a safe timescale for '{Path.GetFileName(path)}' ({num}/{den}) - skip normalization");
                    return false;
                }

                var ffmpeg = await YouTubeStreamResolver.ResolveFfmpegPathAsync().ConfigureAwait(false);
                if (string.IsNullOrEmpty(ffmpeg))
                {
                    Debug.LogWarning($"[MediaNormalizer] ffmpeg not found - cannot normalize '{Path.GetFileName(path)}' (time_base=1/{denominator})");
                    return false;
                }

                // 一時ファイルへ書いてから置き換える (途中失敗で元ファイルを壊さない)。
                var dir = Path.GetDirectoryName(path) ?? ".";
                var tmp = Path.Combine(dir, Path.GetFileNameWithoutExtension(path) + ".norm" + Path.GetExtension(path));
                try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }

                var args = new StringBuilder();
                args.Append("-hide_banner -loglevel error -y -i ").Append(YouTubeStreamResolver.Quote(path));
                args.Append(" -map 0:v:0 -map 0:a? ");
                // 短い動画の背景用途なので veryfast/crf20 で十分 (issue #796 で
                // 実機検証したレシピと同じ)。timescale は fps と整数関係になる値。
                args.Append("-c:v libx264 -preset veryfast -crf 20 -profile:v main -pix_fmt yuv420p");
                args.Append(" -video_track_timescale ").Append(timescale.ToString(CultureInfo.InvariantCulture));
                args.Append(" -c:a copy -movflags +faststart ").Append(YouTubeStreamResolver.Quote(tmp));

                var sw = Stopwatch.StartNew();
                var output = await RunToolAsync(ffmpeg, args.ToString(), 600000, cancellationToken).ConfigureAwait(false);
                sw.Stop();

                if (output == null || !File.Exists(tmp) || new FileInfo(tmp).Length <= 0)
                {
                    DeleteBestEffort(tmp);
                    Debug.LogWarning($"[MediaNormalizer] ffmpeg normalize failed for '{Path.GetFileName(path)}' (time_base=1/{denominator}): {output}");
                    return false;
                }

                File.Copy(tmp, path, overwrite: true);
                DeleteBestEffort(tmp);
                // 置き換えでファイルサイズが変わるため、完了マーカーの記録値を
                // 更新する (更新しないと次回のキャッシュ判定でサイズ不一致に
                // なり、正常化したファイルが再ダウンロード対象になる)。
                YouTubeMediaCache.MarkCompleted(path);
                Debug.Log($"[MediaNormalizer] normalized '{Path.GetFileName(path)}' video time_base 1/{denominator} -> 1/{timescale} " +
                          $"({num}/{den} fps) in {sw.ElapsedMilliseconds}ms");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[MediaNormalizer] normalization of '{path}' failed: {e.Message}");
                return false;
            }
        }

        /// <summary>ffmpeg/ffprobe を実行して stdout を返す。失敗時は null。</summary>
        static async Task<string> RunToolAsync(string executable, string arguments, int timeoutMs, CancellationToken cancellationToken)
        {
            var psi = new ProcessStartInfo
            {
                FileName = executable,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            try
            {
                using (var proc = new Process { StartInfo = psi })
                {
                    proc.Start();
                    var stdoutTask = proc.StandardOutput.ReadToEndAsync();
                    var stderrTask = proc.StandardError.ReadToEndAsync();
                    using (cancellationToken.Register(() => { try { if (!proc.HasExited) proc.Kill(); } catch { } }))
                    {
                        try { proc.WaitForExit(timeoutMs); }
                        catch { }
                    }
                    if (!proc.HasExited)
                    {
                        try { proc.Kill(); } catch { }
                        return null;
                    }
                    // ffprobe は stdout、ffmpeg は stderr に情報を出す。
                    // 失敗時に備えて両方を結合して返す (呼び出し側は行頭一致で読む)。
                    var stdout = await SafeResult(stdoutTask).ConfigureAwait(false);
                    var stderr = await SafeResult(stderrTask).ConfigureAwait(false);
                    if (proc.ExitCode != 0) return null;
                    return stdout + "\n" + stderr;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[MediaNormalizer] failed to run '{executable}': {e.Message}");
                return null;
            }
        }

        static async Task<string> SafeResult(Task<string> task)
        {
            try { return await task.ConfigureAwait(false) ?? ""; }
            catch { return ""; }
        }

        static void DeleteBestEffort(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { }
        }
    }
}
