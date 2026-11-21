using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AmmarDM.Core.Models;

namespace AmmarDM.Core.Downloading
{
    public class DownloadEngine : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly ConcurrentDictionary<string, CancellationTokenSource> _downloadCts = new();
        private readonly string _tempDir;
        private bool _disposed;

        public event Action<DownloadItem>? ProgressChanged;
        public event Action<DownloadItem>? DownloadCompleted;
        public event Action<DownloadItem, Exception>? DownloadFailed;

        public DownloadEngine()
        {
            var handler = new HttpClientHandler
            {
                AllowAutoRedirect = true,
                MaxAutomaticRedirections = 15,
                AutomaticDecompression = DecompressionMethods.All,
                ServerCertificateCustomValidationCallback = (_, _, _, _) => true,
                UseCookies = true,
                CookieContainer = new CookieContainer()
            };

            _httpClient = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromHours(24)
            };

            // وضع هيدرز مطابقة تماماً لـ IDM والمتصفح
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");
            _httpClient.DefaultRequestHeaders.Add("Accept",
                "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8");
            _httpClient.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.9,ar;q=0.8");

            _tempDir = Path.Combine(Path.GetTempPath(), "AmmarDM_Temp");
            Directory.CreateDirectory(_tempDir);
        }

        public async Task StartDownloadAsync(DownloadItem item)
        {
            var cts = new CancellationTokenSource();
            _downloadCts[item.Id] = cts;
            var token = cts.Token;

            long lastMeasuredBytes = item.DownloadedBytes;
            var lastMeasuredTime = DateTime.Now;

            try
            {
                item.Status = DownloadStatus.Downloading;
                item.ErrorMessage = string.Empty;
                ProgressChanged?.Invoke(item);

                // حل الروابط الوسيطة (مثل صفحات Mediafire و Google Drive) لاستخراج الرابط المباشر
                string directUrl = await ResolveDirectDownloadUrlAsync(item.Url, token);
                item.Url = directUrl;

                var info = await GetFileInfoAsync(directUrl, token);

                if (info.TotalBytes > 0)
                    item.TotalBytes = info.TotalBytes;

                if (string.IsNullOrEmpty(item.FileName) || item.FileName.StartsWith("download_"))
                {
                    item.FileName = GetFileNameFromUrl(directUrl, info.ContentDisposition);
                    item.Category = DetectCategory(item.FileName);
                }

                Directory.CreateDirectory(item.SavePath);

                // إذا كان الملف المتوقع حجمه أكبر أو يدعم التجزئة
                if (info.AcceptsRanges && item.TotalBytes > 0 && item.MaxConnections > 1)
                {
                    await MultiSegmentDownloadAsync(item, lastMeasuredBytes, lastMeasuredTime, token);
                }
                else
                {
                    await SingleStreamDownloadAsync(item, lastMeasuredBytes, lastMeasuredTime, token);
                }

                item.Status = DownloadStatus.Completed;
                item.CompletedAt = DateTime.Now;
                item.Speed = 0;
                item.TimeRemaining = TimeSpan.Zero;
                ProgressChanged?.Invoke(item);
                DownloadCompleted?.Invoke(item);
            }
            catch (OperationCanceledException)
            {
                if (item.Status != DownloadStatus.Paused)
                    item.Status = DownloadStatus.Cancelled;
                item.Speed = 0;
                ProgressChanged?.Invoke(item);
            }
            catch (Exception ex)
            {
                item.Status = DownloadStatus.Error;
                item.ErrorMessage = ex.Message;
                item.Speed = 0;
                ProgressChanged?.Invoke(item);
                DownloadFailed?.Invoke(item, ex);
            }
            finally
            {
                _downloadCts.TryRemove(item.Id, out _);
                cts.Dispose();
            }
        }

        /// <summary>
        /// استخراج الرابط المباشر في حال كان الرابط لصفحة وسيطة (Mediafire / YouTube / غيرها)
        /// </summary>
        private async Task<string> ResolveDirectDownloadUrlAsync(string url, CancellationToken token)
        {
            try
            {
                // إذا كان الرابط هو صفحة يوتيوب نفسها
                if (MediaExtractor.IsYouTubeUrl(url))
                {
                    var (title, formats) = await MediaExtractor.ExtractYouTubeFormatsAsync(url);
                    if (formats.Count > 0)
                    {
                        var best = formats.FirstOrDefault(f => !f.IsAudioOnly) ?? formats[0];
                        return best.DirectUrl;
                    }
                }

                // فحص Mediafire
                if (url.Contains("mediafire.com") && !url.Contains("download"))
                {
                    using var req = new HttpRequestMessage(HttpMethod.Get, url);
                    using var resp = await _httpClient.SendAsync(req, token);
                    if (resp.IsSuccessStatusCode)
                    {
                        var html = await resp.Content.ReadAsStringAsync(token);
                        // البحث عن الرابط المباشر داخل زر التنزيل
                        var match = Regex.Match(html, @"href=""(https?://download\d+\.mediafire\.com/[^""]+)""", RegexOptions.IgnoreCase);
                        if (match.Success)
                        {
                            return match.Groups[1].Value;
                        }
                    }
                }
            }
            catch { }

            return url;
        }

        private async Task MultiSegmentDownloadAsync(DownloadItem item, long lastMeasuredBytes, DateTime lastMeasuredTime, CancellationToken token)
        {
            int segCount = Math.Min(item.MaxConnections, 32);
            long segSize = item.TotalBytes / segCount;
            var tempFiles = new string[segCount];
            var tasks = new Task[segCount];
            var segProgress = new long[segCount];

            string progressFile = GetProgressFilePath(item);
            long[] savedProgress = LoadSegmentProgress(item, progressFile, segCount);

            // تهيئة عناصر الأجزاء للعرض الحي
            var segList = new System.Collections.Generic.List<SegmentInfo>();
            for (int i = 0; i < segCount; i++)
            {
                long startByte = i * segSize;
                long endByte = (i == segCount - 1) ? item.TotalBytes - 1 : (i + 1) * segSize - 1;
                segList.Add(new SegmentInfo
                {
                    Index = i + 1,
                    StartPosition = startByte,
                    EndPosition = endByte,
                    DownloadedBytes = savedProgress[i],
                    IsActive = true
                });
            }

            // تحديث قائمة السجمنتات في الكائن
            item.Segments = new System.Collections.ObjectModel.ObservableCollection<SegmentInfo>(segList);

            for (int i = 0; i < segCount; i++)
            {
                int idx = i;
                long segStart = idx * segSize;
                long end = (idx == segCount - 1) ? item.TotalBytes - 1 : (idx + 1) * segSize - 1;
                long segExpectedBytes = end - segStart + 1;
                tempFiles[idx] = GetTempFilePath(item, idx);

                long existingLength = File.Exists(tempFiles[idx]) ? new FileInfo(tempFiles[idx]).Length : savedProgress[idx];
                segProgress[idx] = existingLength;

                if (existingLength >= segExpectedBytes)
                {
                    segProgress[idx] = segExpectedBytes;
                    if (idx < item.Segments.Count)
                    {
                        item.Segments[idx].DownloadedBytes = item.Segments[idx].TotalBytes;
                        item.Segments[idx].IsActive = false;
                    }
                    tasks[idx] = Task.CompletedTask;
                    continue;
                }

                tasks[idx] = DownloadSegmentAsync(item.Url, tempFiles[idx], segStart, end, bytes =>
                {
                    long currentSegDownloaded = Interlocked.Add(ref segProgress[idx], bytes);
                    if (idx < item.Segments.Count)
                    {
                        item.Segments[idx].DownloadedBytes = currentSegDownloaded;
                    }

                    long total = 0;
                    for (int s = 0; s < segCount; s++) total += Interlocked.Read(ref segProgress[s]);

                    item.DownloadedBytes = total;
                    SaveSegmentProgress(progressFile, segProgress);
                    UpdateSpeedAndEta(item, ref lastMeasuredBytes, ref lastMeasuredTime);
                    ProgressChanged?.Invoke(item);
                }, token);
            }

            item.ActiveSegments = segCount;
            await Task.WhenAll(tasks);
            item.ActiveSegments = 0;

            // التحقق الدقيق من اكتمال كل جزء قبل الدمج
            for (int i = 0; i < segCount; i++)
            {
                long segStart = i * segSize;
                long end = (i == segCount - 1) ? item.TotalBytes - 1 : (i + 1) * segSize - 1;
                long expectedLen = end - segStart + 1;
                long actualLen = File.Exists(tempFiles[i]) ? new FileInfo(tempFiles[i]).Length : 0;

                if (actualLen < expectedLen)
                {
                    throw new IOException($"فشل اكتمال تنزيل الخيط رقم {i + 1}: تم تنزيل {actualLen} بايت فقط من أصل {expectedLen} بايت.");
                }

                if (i < item.Segments.Count)
                {
                    item.Segments[i].IsActive = false;
                    item.Segments[i].DownloadedBytes = item.Segments[i].TotalBytes;
                }
            }

            await MergeSegmentsAsync(tempFiles, item.FullPath, token);

            foreach (var f in tempFiles)
            {
                try { if (File.Exists(f)) File.Delete(f); } catch { }
            }
            try { if (File.Exists(progressFile)) File.Delete(progressFile); } catch { }
        }

        private async Task DownloadSegmentAsync(string url, string tempFile, long start, long end, Action<long> onProgress, CancellationToken token)
        {
            int maxRetries = 5;
            int attempt = 0;

            while (true)
            {
                attempt++;
                token.ThrowIfCancellationRequested();

                // قراءة ما تم حفظه بالفعل على القرص لاستئناف الجزء بالضبط
                long existingLength = File.Exists(tempFile) ? new FileInfo(tempFile).Length : 0;
                long currentStart = start + existingLength;

                if (currentStart > end)
                {
                    // اكتمل تحميل هذا الجزء بالكامل
                    return;
                }

                try
                {
                    using var request = new HttpRequestMessage(HttpMethod.Get, url);
                    request.Headers.Range = new RangeHeaderValue(currentStart, end);
                    AddCommonHeaders(request, url);

                    using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
                    response.EnsureSuccessStatusCode();

                    using var stream = await response.Content.ReadAsStreamAsync(token);
                    var mode = existingLength > 0 ? FileMode.Append : FileMode.Create;
                    using var fs = new FileStream(tempFile, mode, FileAccess.Write, FileShare.None, 81920, true);

                    var buffer = new byte[81920];
                    int bytesRead;
                    while ((bytesRead = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), token)) > 0)
                    {
                        token.ThrowIfCancellationRequested();
                        await fs.WriteAsync(buffer.AsMemory(0, bytesRead), token);
                        onProgress(bytesRead);
                    }

                    // إذا وصلنا هنا بنجاح نخرج من حلقة المحاولات
                    return;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception)
                {
                    if (attempt >= maxRetries)
                    {
                        throw;
                    }

                    // انتظار تصاعدي بسيط قبل إعادة المحاولة (تجاوز الإغلاق المؤقت من الخادم)
                    await Task.Delay(1000 * attempt, token);
                }
            }
        }

        private async Task SingleStreamDownloadAsync(DownloadItem item, long lastMeasuredBytes, DateTime lastMeasuredTime, CancellationToken token)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, item.Url);
            AddCommonHeaders(request, item.Url);

            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
            response.EnsureSuccessStatusCode();

            // فحص هل المحتوى المعار هو صفحة ويب HTML بدلاً من ملف
            var contentType = response.Content.Headers.ContentType?.MediaType?.ToLower();
            if (contentType != null && contentType.Contains("text/html") && !item.FileName.EndsWith(".html") && !item.FileName.EndsWith(".htm"))
            {
                // نقرأ الصفحة لنرى هل هي صفحة خطأ أو صفحة تحميل Mediafire
                var html = await response.Content.ReadAsStringAsync(token);
                var match = Regex.Match(html, @"href=""(https?://download\d+\.mediafire\.com/[^""]+)""", RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    item.Url = match.Groups[1].Value;
                    await SingleStreamDownloadAsync(item, lastMeasuredBytes, lastMeasuredTime, token);
                    return;
                }
                else
                {
                    throw new Exception("الرابط المباشر منتهي الصلاحية أو محمي بصفحة أمان.");
                }
            }

            if (item.TotalBytes == 0 && response.Content.Headers.ContentLength.HasValue)
                item.TotalBytes = response.Content.Headers.ContentLength.Value;

            using var stream = await response.Content.ReadAsStreamAsync(token);
            using var fs = new FileStream(item.FullPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);

            var buffer = new byte[81920];
            int bytesRead;
            item.DownloadedBytes = 0;

            while ((bytesRead = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), token)) > 0)
            {
                token.ThrowIfCancellationRequested();
                await fs.WriteAsync(buffer.AsMemory(0, bytesRead), token);
                item.DownloadedBytes += bytesRead;
                UpdateSpeedAndEta(item, ref lastMeasuredBytes, ref lastMeasuredTime);
                ProgressChanged?.Invoke(item);
            }
        }

        private void AddCommonHeaders(HttpRequestMessage request, string url)
        {
            try
            {
                var uri = new Uri(url);
                if (uri.Host.Contains("googlevideo.com") || uri.Host.Contains("youtube.com"))
                {
                    request.Headers.Referrer = new Uri("https://www.youtube.com/");
                }
                else
                {
                    request.Headers.Referrer = new Uri($"{uri.Scheme}://{uri.Host}/");
                }
            }
            catch { }
        }

        private void UpdateSpeedAndEta(DownloadItem item, ref long lastBytes, ref DateTime lastTime)
        {
            var now = DateTime.Now;
            var elapsed = (now - lastTime).TotalSeconds;
            if (elapsed >= 0.5)
            {
                var bytesDiff = item.DownloadedBytes - lastBytes;
                if (bytesDiff >= 0)
                {
                    item.Speed = bytesDiff / elapsed;
                    lastBytes = item.DownloadedBytes;
                    lastTime = now;

                    if (item.Speed > 0 && item.TotalBytes > item.DownloadedBytes)
                    {
                        var remaining = item.TotalBytes - item.DownloadedBytes;
                        item.TimeRemaining = TimeSpan.FromSeconds(remaining / item.Speed);
                    }
                }
            }
        }

        private async Task MergeSegmentsAsync(string[] tempFiles, string outputPath, CancellationToken token)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            using var outputStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);

            foreach (var tempFile in tempFiles)
            {
                token.ThrowIfCancellationRequested();
                if (!File.Exists(tempFile)) continue;
                using var inputStream = new FileStream(tempFile, FileMode.Open, FileAccess.Read, FileShare.None, 81920, true);
                await inputStream.CopyToAsync(outputStream, token);
            }
        }

        public async Task<(long TotalBytes, bool AcceptsRanges, string? ContentDisposition)> GetFileInfoAsync(string url, CancellationToken token)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Head, url);
                AddCommonHeaders(request, url);
                using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);

                if (response.IsSuccessStatusCode)
                {
                    long size = response.Content.Headers.ContentLength ?? 0;
                    bool acceptsRange = response.Headers.AcceptRanges?.Contains("bytes") == true;
                    string? cd = response.Content.Headers.ContentDisposition?.ToString();
                    return (size, acceptsRange, cd);
                }
            }
            catch { }

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Range = new RangeHeaderValue(0, 0);
                AddCommonHeaders(request, url);
                using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);

                if (response.IsSuccessStatusCode)
                {
                    long size = response.Content.Headers.ContentRange?.Length ?? response.Content.Headers.ContentLength ?? 0;
                    bool acceptsRange = response.Content.Headers.ContentRange != null;
                    string? cd = response.Content.Headers.ContentDisposition?.ToString();
                    return (size, acceptsRange, cd);
                }
            }
            catch { }

            return (0, false, null);
        }

        private void SaveSegmentProgress(string path, long[] progress)
        {
            try
            {
                File.WriteAllText(path, string.Join(",", progress));
            }
            catch { }
        }

        public void CleanTempFiles(DownloadItem item)
        {
            try
            {
                for (int i = 0; i < 32; i++)
                {
                    var f = GetTempFilePath(item, i);
                    if (File.Exists(f)) File.Delete(f);
                }
                var p = GetProgressFilePath(item);
                if (File.Exists(p)) File.Delete(p);
            }
            catch { }
        }

        public void Pause(DownloadItem item)
        {
            item.Status = DownloadStatus.Paused;
            if (_downloadCts.TryGetValue(item.Id, out var cts))
            {
                try { cts.Cancel(); } catch { }
            }
        }

        public void Cancel(DownloadItem item)
        {
            item.Status = DownloadStatus.Cancelled;
            if (_downloadCts.TryGetValue(item.Id, out var cts))
            {
                try { cts.Cancel(); } catch { }
            }
        }

        private string GetTempFilePath(DownloadItem item, int segIndex)
            => Path.Combine(_tempDir, $"{item.Id}_seg{segIndex}.tmp");

        private string GetProgressFilePath(DownloadItem item)
            => Path.Combine(_tempDir, $"{item.Id}.progress");

        private long[] LoadSegmentProgress(DownloadItem item, string path, int count)
        {
            try
            {
                if (File.Exists(path))
                {
                    var parts = File.ReadAllText(path).Split(',');
                    if (parts.Length == count)
                        return parts.Select(p => long.TryParse(p, out var v) ? v : 0).ToArray();
                }

                // فحص حجم الملفات المؤقتة الموجودة بالفعل على القرص كنسخة احتياطية للاستئناف
                var diskProgress = new long[count];
                bool foundAny = false;
                for (int i = 0; i < count; i++)
                {
                    var segFile = GetTempFilePath(item, i);
                    if (File.Exists(segFile))
                    {
                        diskProgress[i] = new FileInfo(segFile).Length;
                        foundAny = true;
                    }
                }
                if (foundAny)
                {
                    SaveSegmentProgress(path, diskProgress);
                    return diskProgress;
                }
            }
            catch { }
            return new long[count];
        }

        public static string SanitizeFileName(string fileName)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            var cleaned = string.Join("_", fileName.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries)).Trim();
            // منع ثغرات Path Traversal مثل .. أو المسارات النسبية
            cleaned = cleaned.Replace("..", "").Trim('.', ' ', '/', '\\');
            return string.IsNullOrEmpty(cleaned) ? $"download_{DateTime.Now:yyyyMMdd_HHmmss}.bin" : cleaned;
        }

        public static string GetFileNameFromUrl(string url, string? contentDisposition)
        {
            if (!string.IsNullOrEmpty(contentDisposition))
            {
                var parts = contentDisposition.Split(';');
                foreach (var p in parts)
                {
                    var t = p.Trim();
                    if (t.StartsWith("filename*=", StringComparison.OrdinalIgnoreCase))
                    {
                        var val = t.Substring(10);
                        var idx = val.IndexOf("''");
                        if (idx != -1) val = val.Substring(idx + 2);
                        return SanitizeFileName(Uri.UnescapeDataString(val.Trim('"')));
                    }
                    if (t.StartsWith("filename=", StringComparison.OrdinalIgnoreCase))
                    {
                        return SanitizeFileName(t.Substring(9).Trim('"'));
                    }
                }
            }

            try
            {
                var uri = new Uri(url);
                var name = Path.GetFileName(uri.LocalPath);
                if (!string.IsNullOrEmpty(name) && name.Contains('.'))
                    return SanitizeFileName(Uri.UnescapeDataString(name));
            }
            catch { }

            return $"download_{DateTime.Now:yyyyMMdd_HHmmss}.bin";
        }

        public static DownloadCategory DetectCategory(string fileName)
        {
            var ext = Path.GetExtension(fileName).ToLower();
            return ext switch
            {
                ".mp4" or ".mkv" or ".avi" or ".mov" or ".wmv" or ".flv" => DownloadCategory.Videos,
                ".mp3" or ".flac" or ".aac" or ".wav" or ".ogg" => DownloadCategory.Music,
                ".pdf" or ".doc" or ".docx" or ".xls" or ".xlsx" or ".ppt" => DownloadCategory.Documents,
                ".exe" or ".msi" or ".dmg" or ".deb" or ".apk" => DownloadCategory.Programs,
                ".zip" or ".rar" or ".7z" or ".tar" or ".gz" => DownloadCategory.Archives,
                ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" or ".webp" => DownloadCategory.Images,
                _ => DownloadCategory.General
            };
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                foreach (var cts in _downloadCts.Values)
                {
                    try { cts.Cancel(); cts.Dispose(); } catch { }
                }
                _httpClient.Dispose();
                _disposed = true;
            }
        }
    }
}
