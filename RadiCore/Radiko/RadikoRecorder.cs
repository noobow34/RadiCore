using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;

namespace RadiCore.Radiko
{
    /// <summary>
    /// タイムフリー番組の録音処理（旧 Tools/rec_radiko_ts.sh の C# 実装）。
    /// 実際のHLSセグメント取得・チャンク結合はffmpeg/ffprobeへ委譲する。
    /// </summary>
    public static class RadikoRecorder
    {
        /// <summary>
        /// 指定区間のタイムフリー番組を録音し、m4aファイルとして出力する
        /// </summary>
        /// <returns>成功した場合true</returns>
        public static async Task<bool> RecordAsync(
            string stationId,
            DateTime fromTime,
            DateTime toTime,
            string outputPath,
            string mail,
            string password,
            Action<string>? log = null)
        {
            log ??= _ => { };

            using var httpClient = new HttpClient();

            string radikoSession = "";
            bool isAreaFree = false;

            if (!string.IsNullOrEmpty(mail))
            {
                try
                {
                    (radikoSession, isAreaFree) = await RetryAsync(
                        () => RadikoClient.LoginPremiumAsync(mail, password, httpClient),
                        log, "Radikoプレミアムログイン");
                }
                catch (RadikoException ex)
                {
                    log($"録音失敗: {ex.Message}");
                    return false;
                }
            }

            string authToken;
            string areaId;
            try
            {
                (authToken, areaId) = await RetryAsync(
                    () => RadikoClient.AuthAsync(radikoSession, httpClient),
                    log, "Radiko認証");
            }
            catch (RadikoException ex)
            {
                log($"録音失敗: {ex.Message}");
                await RadikoClient.LogoutPremiumAsync(radikoSession, httpClient);
                return false;
            }

            List<string> hlsUrls;
            try
            {
                hlsUrls = await RadikoClient.GetHlsPlaylistUrlsAsync(stationId, isAreaFree, httpClient);
            }
            catch (Exception ex)
            {
                log($"録音失敗: HLSプレイリストURLの取得に失敗しました ({ex.Message})");
                await RadikoClient.LogoutPremiumAsync(radikoSession, httpClient);
                return false;
            }

            if (hlsUrls.Count == 0)
            {
                log("録音失敗: HLSプレイリストURLが取得できません");
                await RadikoClient.LogoutPremiumAsync(radikoSession, httpClient);
                return false;
            }

            string tmpDir = Path.GetTempPath();
            string tmpFileBase = $"recradikots_{Convert.ToHexString(RandomNumberGenerator.GetBytes(4)).ToLowerInvariant()}";
            string lsid = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
            string ffmpegHeader = $"X-Radiko-Authtoken: {authToken}\r\nX-Radiko-AreaId: {areaId}";
            string fromTimeStr = fromTime.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);

            var chunkFiles = new List<string>();
            bool recordSuccess = false;

            try
            {
                long leftSec = (long)(toTime - fromTime).TotalSeconds;
                DateTime seekTime = fromTime;
                int chunkNo = 0;

                foreach (var hlsUrl in hlsUrls)
                {
                    recordSuccess = true;

                    while (leftSec > 0)
                    {
                        // チャンクは最大300秒、300秒未満は5秒単位に切り上げ
                        long l = 300;
                        if (leftSec < 300)
                            l = (leftSec % 5 == 0) ? leftSec : ((leftSec / 5) + 1) * 5;

                        string chunkFile = Path.Combine(tmpDir, $"{tmpFileBase}_chunk{chunkNo}.m4a");
                        string seekStr = seekTime.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
                        string endAtStr = seekTime.AddSeconds(l).ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);

                        string inputUrl = $"{hlsUrl}?station_id={stationId}&start_at={fromTimeStr}&ft={fromTimeStr}" +
                                          $"&seek={seekStr}&end_at={endAtStr}&to={endAtStr}&l={l}&lsid={lsid}&type=c";

                        var ffmpegArgs = new List<string>
                        {
                            "-nostdin",
                            "-loglevel", "quiet",
                            "-fflags", "+discardcorrupt",
                            "-headers", ffmpegHeader,
                            "-http_seekable", "0",
                            "-seekable", "0",
                            "-i", inputUrl,
                            "-acodec", "copy",
                            "-vn",
                            "-bsf:a", "aac_adtstoasc",
                            "-y", chunkFile
                        };

                        int exitCode = await RunProcessAsync("ffmpeg", ffmpegArgs, log,
                            $"チャンク{chunkNo}取得 (seek={seekStr}, end_at={endAtStr}, l={l}s)");
                        if (exitCode != 0)
                        {
                            log($"チャンク{chunkNo}のダウンロードに失敗しました (exit={exitCode})");
                            recordSuccess = false;
                            break;
                        }

                        chunkFiles.Add(chunkFile);

                        double durationSec = await GetDurationAsync(chunkFile, log, $"チャンク{chunkNo}");
                        long chunkSec = (long)Math.Floor(durationSec + 0.5);
                        if (chunkSec <= 0)
                            chunkSec = l;

                        log($"チャンク{chunkNo}取得完了 (duration={chunkSec}s, 残り={leftSec - chunkSec}s)");

                        leftSec -= chunkSec;
                        seekTime = seekTime.AddSeconds(chunkSec);
                        chunkNo++;
                    }

                    if (recordSuccess)
                        break;
                }

                if (recordSuccess)
                {
                    string fileListPath = Path.Combine(tmpDir, $"{tmpFileBase}_filelist.txt");
                    await File.WriteAllLinesAsync(fileListPath, chunkFiles.Select(f => $"file '{f}'"));

                    var concatArgs = new List<string>
                    {
                        "-loglevel", "error",
                        "-f", "concat",
                        "-safe", "0",
                        "-i", fileListPath,
                        "-c", "copy",
                        "-y", outputPath
                    };

                    int concatExit = await RunProcessAsync("ffmpeg", concatArgs, log,
                        $"チャンク結合 (chunk数={chunkFiles.Count}, output={outputPath})");
                    if (concatExit != 0)
                    {
                        log($"チャンク結合に失敗しました (exit={concatExit})");
                        recordSuccess = false;
                    }
                }
            }
            finally
            {
                foreach (var f in Directory.EnumerateFiles(tmpDir, $"{tmpFileBase}_*"))
                {
                    try { File.Delete(f); } catch { /* 削除失敗は無視する */ }
                }

                await RadikoClient.LogoutPremiumAsync(radikoSession, httpClient);
            }

            if (!recordSuccess)
                log("録音失敗");

            return recordSuccess;
        }

        private static async Task<T> RetryAsync<T>(Func<Task<T>> action, Action<string> log, string operationName, int maxAttempts = 3, int delayMs = 5000)
        {
            Exception? lastEx = null;
            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    return await action();
                }
                catch (Exception ex)
                {
                    lastEx = ex;
                    log($"{operationName}に失敗しました（{attempt}/{maxAttempts}回目）: {ex.Message}");
                    if (attempt < maxAttempts)
                        await Task.Delay(delayMs);
                }
            }

            throw new RadikoException($"{operationName}に失敗しました", lastEx);
        }

        private static async Task<int> RunProcessAsync(string fileName, List<string> args, Action<string> log, string description)
        {
            log($"{fileName}実行: {description}");

            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                UseShellExecute = false
            };
            foreach (var a in args)
                psi.ArgumentList.Add(a);

            using var process = new Process { StartInfo = psi };
            process.Start();
            await process.WaitForExitAsync();

            log($"{fileName}終了: {description} (exit={process.ExitCode})");
            return process.ExitCode;
        }

        private static async Task<double> GetDurationAsync(string filePath, Action<string> log, string description)
        {
            log($"ffprobe実行: {description}");

            var psi = new ProcessStartInfo
            {
                FileName = "ffprobe",
                RedirectStandardOutput = true,
                UseShellExecute = false
            };
            psi.ArgumentList.Add("-v");
            psi.ArgumentList.Add("error");
            psi.ArgumentList.Add("-show_entries");
            psi.ArgumentList.Add("format=duration");
            psi.ArgumentList.Add("-of");
            psi.ArgumentList.Add("default=noprint_wrappers=1:nokey=1");
            psi.ArgumentList.Add(filePath);

            using var process = new Process { StartInfo = psi };
            process.Start();
            string output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            double duration = double.TryParse(output.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : 0;
            log($"ffprobe終了: {description} (exit={process.ExitCode}, duration={duration}s)");
            return duration;
        }
    }
}
