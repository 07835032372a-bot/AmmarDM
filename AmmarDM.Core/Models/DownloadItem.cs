using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace AmmarDM.Core.Models
{
    public enum DownloadStatus
    {
        Queued,
        Downloading,
        Paused,
        Completed,
        Error,
        Cancelled,
        Scheduled
    }

    public enum DownloadCategory
    {
        General,
        Videos,
        Music,
        Documents,
        Programs,
        Archives,
        Images
    }

    public class DownloadItem : INotifyPropertyChanged
    {
        private string _id = Guid.NewGuid().ToString("N");
        private string _url = string.Empty;
        private string _fileName = string.Empty;
        private string _savePath = string.Empty;
        private long _totalBytes;
        private long _downloadedBytes;
        private DownloadStatus _status = DownloadStatus.Queued;
        private DownloadCategory _category = DownloadCategory.General;
        private double _speed;
        private TimeSpan _timeRemaining;
        private int _maxConnections = 16;
        private int _activeSegments;
        private string _errorMessage = string.Empty;
        private DateTime _createdAt = DateTime.Now;
        private DateTime? _completedAt;
        private DateTime? _scheduledAt;
        private string _comment = string.Empty;
        private string _referrer = string.Empty;
        private int _priority;

        public string Id { get => _id; set => SetField(ref _id, value); }
        public string Url { get => _url; set => SetField(ref _url, value); }
        public string Referrer { get => _referrer; set => SetField(ref _referrer, value); }
        public string FileName { get => _fileName; set => SetField(ref _fileName, value); }
        public string SavePath { get => _savePath; set => SetField(ref _savePath, value); }
        public string FullPath => Path.Combine(SavePath, FileName);

        public long TotalBytes
        {
            get => _totalBytes;
            set
            {
                SetField(ref _totalBytes, value);
                OnPropertyChanged(nameof(FileSizeDisplay));
                OnPropertyChanged(nameof(Progress));
            }
        }

        public long DownloadedBytes
        {
            get => _downloadedBytes;
            set
            {
                SetField(ref _downloadedBytes, value);
                OnPropertyChanged(nameof(DownloadedDisplay));
                OnPropertyChanged(nameof(Progress));
            }
        }

        public double Progress => TotalBytes > 0 ? (double)DownloadedBytes / TotalBytes * 100.0 : 0.0;

        public DownloadStatus Status
        {
            get => _status;
            set
            {
                SetField(ref _status, value);
                OnPropertyChanged(nameof(StatusDisplay));
            }
        }

        public DownloadCategory Category { get => _category; set => SetField(ref _category, value); }
        public double Speed { get => _speed; set { SetField(ref _speed, value); OnPropertyChanged(nameof(SpeedDisplay)); } }
        public TimeSpan TimeRemaining { get => _timeRemaining; set { SetField(ref _timeRemaining, value); OnPropertyChanged(nameof(EtaDisplay)); } }
        public int MaxConnections { get => _maxConnections; set => SetField(ref _maxConnections, value); }
        public int ActiveSegments { get => _activeSegments; set => SetField(ref _activeSegments, value); }
        public string ErrorMessage { get => _errorMessage; set => SetField(ref _errorMessage, value); }
        public DateTime CreatedAt { get => _createdAt; set => SetField(ref _createdAt, value); }
        public DateTime? CompletedAt { get => _completedAt; set => SetField(ref _completedAt, value); }
        public DateTime? ScheduledAt { get => _scheduledAt; set => SetField(ref _scheduledAt, value); }
        public string Comment { get => _comment; set => SetField(ref _comment, value); }
        public int Priority { get => _priority; set => SetField(ref _priority, value); }

        [JsonIgnore]
        public ObservableCollection<SegmentInfo> Segments { get; set; } = new();

        public string FileSizeDisplay => FormatSize(TotalBytes);
        public string DownloadedDisplay => FormatSize(DownloadedBytes);
        public string SpeedDisplay => FormatSpeed(Speed);
        public string EtaDisplay => FormatEta(TimeRemaining);

        public string StatusDisplay => Status switch
        {
            DownloadStatus.Queued => "في الانتظار",
            DownloadStatus.Downloading => "جارٍ التحميل",
            DownloadStatus.Paused => "متوقف مؤقتاً",
            DownloadStatus.Completed => "مكتمل",
            DownloadStatus.Error => "خطأ",
            DownloadStatus.Cancelled => "ملغى",
            DownloadStatus.Scheduled => "مجدول",
            _ => "غير معروف"
        };

        public static string FormatSize(long bytes)
        {
            if (bytes <= 0) return "غير معروف";
            string[] units = { "B", "KB", "MB", "GB", "TB" };
            double size = bytes;
            int unit = 0;
            while (size >= 1024 && unit < units.Length - 1)
            {
                size /= 1024;
                unit++;
            }
            return $"{size:F2} {units[unit]}";
        }

        public static string FormatSpeed(double bytesPerSec)
        {
            if (bytesPerSec <= 0) return "0 KB/s";
            if (bytesPerSec < 1024) return $"{bytesPerSec:F0} B/s";
            if (bytesPerSec < 1024 * 1024) return $"{bytesPerSec / 1024:F1} KB/s";
            return $"{bytesPerSec / (1024 * 1024):F2} MB/s";
        }

        public static string FormatEta(TimeSpan ts)
        {
            if (ts == TimeSpan.Zero || ts.TotalSeconds <= 0) return "--:--";
            if (ts.TotalHours >= 1) return $"{(int)ts.TotalHours}س {ts.Minutes}د";
            if (ts.TotalMinutes >= 1) return $"{ts.Minutes}د {ts.Seconds}ث";
            return $"{ts.Seconds} ثانية";
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
        {
            if (Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(name);
            return true;
        }
    }
}
