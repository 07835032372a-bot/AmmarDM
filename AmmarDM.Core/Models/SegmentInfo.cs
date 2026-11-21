using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AmmarDM.Core.Models
{
    public class SegmentInfo : INotifyPropertyChanged
    {
        private int _index;
        private long _startPosition;
        private long _endPosition;
        private long _downloadedBytes;
        private double _speed;
        private bool _isActive;

        public int Index
        {
            get => _index;
            set => SetField(ref _index, value);
        }

        public long StartPosition
        {
            get => _startPosition;
            set => SetField(ref _startPosition, value);
        }

        public long EndPosition
        {
            get => _endPosition;
            set => SetField(ref _endPosition, value);
        }

        public long TotalBytes => Math.Max(0, EndPosition - StartPosition + 1);

        public long DownloadedBytes
        {
            get => _downloadedBytes;
            set
            {
                if (SetField(ref _downloadedBytes, value))
                {
                    OnPropertyChanged(nameof(Progress));
                    OnPropertyChanged(nameof(ProgressDisplay));
                    OnPropertyChanged(nameof(DownloadedDisplay));
                }
            }
        }

        public double Speed
        {
            get => _speed;
            set
            {
                if (SetField(ref _speed, value))
                    OnPropertyChanged(nameof(SpeedDisplay));
            }
        }

        public bool IsActive
        {
            get => _isActive;
            set => SetField(ref _isActive, value);
        }

        public double Progress => TotalBytes > 0 ? Math.Min(100.0, (double)DownloadedBytes / TotalBytes * 100.0) : 0;
        public string ProgressDisplay => $"{Progress:F1}%";
        public string DownloadedDisplay => $"{DownloadItem.FormatSize(DownloadedBytes)} / {DownloadItem.FormatSize(TotalBytes)}";
        public string SpeedDisplay => DownloadItem.FormatSpeed(Speed);

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
