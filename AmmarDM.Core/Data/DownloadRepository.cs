using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using AmmarDM.Core.Models;

namespace AmmarDM.Core.Data
{
    public class DownloadRepository
    {
        private readonly string _filePath;

        public DownloadRepository(string appDataPath)
        {
            var dir = Path.Combine(appDataPath, "AmmarDM");
            Directory.CreateDirectory(dir);
            _filePath = Path.Combine(dir, "downloads.json");
        }

        public List<DownloadItem> LoadAll()
        {
            try
            {
                if (!File.Exists(_filePath)) return new();
                var json = File.ReadAllText(_filePath);
                return JsonSerializer.Deserialize<List<DownloadItem>>(json) ?? new();
            }
            catch { return new(); }
        }

        public void SaveAll(IEnumerable<DownloadItem> items)
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(items, options);
                File.WriteAllText(_filePath, json);
            }
            catch { }
        }

        public AppSettings LoadSettings()
        {
            var path = Path.Combine(Path.GetDirectoryName(_filePath)!, "settings.json");
            try
            {
                if (!File.Exists(path)) return new();
                var json = File.ReadAllText(path);
                return JsonSerializer.Deserialize<AppSettings>(json) ?? new();
            }
            catch { return new(); }
        }

        public void SaveSettings(AppSettings settings)
        {
            var path = Path.Combine(Path.GetDirectoryName(_filePath)!, "settings.json");
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                File.WriteAllText(path, JsonSerializer.Serialize(settings, options));
            }
            catch { }
        }
    }

    public class AppSettings
    {
        public string DefaultDownloadPath { get; set; } =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        public int MaxConnections { get; set; } = 16;
        public int MaxConcurrentDownloads { get; set; } = 3;
        public double MaxSpeedKbps { get; set; } = 0;
        public bool StartWithWindows { get; set; } = false;
        public bool MinimizeToTray { get; set; } = true;
        public bool ShowNotifications { get; set; } = true;
        public bool SoundOnComplete { get; set; } = true;
        public string Theme { get; set; } = "System";

        // إعدادات المتصفح وامتدادات الاقتناص وقائمة التجاهل مثل IDM
        public string CapturedExtensions { get; set; } = "ZIP,RAR,7Z,EXE,ISO,MSI,TAR,GZ,MP4,MKV,WEBM,FLV,AVI,MOV,MP3,WAV,AAC,PDF,DOCX,XLSX";
        public string IgnoredSites { get; set; } = "update.microsoft.com,windowsupdate.microsoft.com";
        public bool CaptureBrowserDownloads { get; set; } = true;
        public bool ShowVideoDownloadPanel { get; set; } = true;
    }
}
