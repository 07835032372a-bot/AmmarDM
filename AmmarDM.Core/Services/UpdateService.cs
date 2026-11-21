using System;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AmmarDM.Core.Models;

namespace AmmarDM.Core.Services
{
    public static class UpdateService
    {
        private static readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(15) };

        // رابط التحقق من التحديثات الافتراضي عبر GitHub للمشروع
        public const string DefaultUpdateUrl = "https://raw.githubusercontent.com/07835032372a-bot/AmmarDM/main/update.json";

        public static Version CurrentVersion
        {
            get
            {
                var ver = Assembly.GetEntryAssembly()?.GetName().Version;
                return ver ?? new Version(1, 0, 0, 0);
            }
        }

        public static async Task<UpdateInfo?> CheckForUpdatesAsync(string? updateManifestUrl = null, CancellationToken token = default)
        {
            var url = !string.IsNullOrEmpty(updateManifestUrl) ? updateManifestUrl : DefaultUpdateUrl;
            try
            {
                using var response = await _httpClient.GetAsync(url, token);
                if (!response.IsSuccessStatusCode) return null;

                var json = await response.Content.ReadAsStringAsync(token);
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var info = JsonSerializer.Deserialize<UpdateInfo>(json, options);

                if (info != null && Version.TryParse(info.Version, out var remoteVer))
                {
                    // المقارنة: إذا كان الإصدار البعيد أحدث من الحالي
                    var current = CurrentVersion;
                    var remote = new Version(remoteVer.Major, remoteVer.Minor, Math.Max(0, remoteVer.Build));
                    var local = new Version(current.Major, current.Minor, Math.Max(0, current.Build));

                    if (remote > local)
                    {
                        return info;
                    }
                }
            }
            catch { }

            return null;
        }

        public static async Task DownloadUpdateFileAsync(string downloadUrl, string destinationZip, Action<double>? progressCallback, CancellationToken token = default)
        {
            using var response = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, token);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? -1L;
            using var stream = await response.Content.ReadAsStreamAsync(token);
            using var fileStream = new FileStream(destinationZip, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);

            var buffer = new byte[81920];
            long totalRead = 0;
            int bytesRead;

            while ((bytesRead = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), token)) > 0)
            {
                token.ThrowIfCancellationRequested();
                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), token);
                totalRead += bytesRead;

                if (totalBytes > 0 && progressCallback != null)
                {
                    double pct = (double)totalRead / totalBytes * 100.0;
                    progressCallback(pct);
                }
            }
        }
    }
}
