using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AmmarDM.Core.Models;
using AmmarDM.Core.Downloading;

namespace AmmarDM.Core.Queue
{
    public class DownloadQueue
    {
        private readonly DownloadEngine _engine;
        private readonly SemaphoreSlim _semaphore;
        private int _maxConcurrent;

        public ObservableCollection<DownloadItem> Items { get; } = new();

        public int MaxConcurrentDownloads
        {
            get => _maxConcurrent;
            set => _maxConcurrent = Math.Max(1, value);
        }

        public event Action<DownloadItem>? ItemProgressChanged;
        public event Action<DownloadItem>? ItemCompleted;
        public event Action<DownloadItem, Exception>? ItemFailed;

        public DownloadQueue(int maxConcurrent = 3)
        {
            _maxConcurrent = maxConcurrent;
            _semaphore = new SemaphoreSlim(maxConcurrent, 32);
            _engine = new DownloadEngine();
            _engine.ProgressChanged += item => ItemProgressChanged?.Invoke(item);
            _engine.DownloadCompleted += item => ItemCompleted?.Invoke(item);
            _engine.DownloadFailed += (item, ex) => ItemFailed?.Invoke(item, ex);
        }

        public void Enqueue(DownloadItem item)
        {
            item.Status = DownloadStatus.Queued;
            Items.Add(item);
            _ = ProcessQueueAsync(item);
        }

        private async Task ProcessQueueAsync(DownloadItem item)
        {
            await _semaphore.WaitAsync();
            try
            {
                if (item.Status == DownloadStatus.Cancelled || item.Status == DownloadStatus.Paused)
                    return;

                if (item.ScheduledAt.HasValue && item.ScheduledAt.Value > DateTime.Now)
                {
                    item.Status = DownloadStatus.Scheduled;
                    var delay = item.ScheduledAt.Value - DateTime.Now;
                    await Task.Delay(delay);
                }

                await _engine.StartDownloadAsync(item);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public void PauseItem(DownloadItem item)
        {
            _engine.Pause(item);
        }

        public void PauseAll()
        {
            foreach (var item in Items.Where(i => i.Status == DownloadStatus.Downloading))
                _engine.Pause(item);
        }

        public void ResumeItem(DownloadItem item)
        {
            if (item.Status == DownloadStatus.Paused || item.Status == DownloadStatus.Error || item.Status == DownloadStatus.Cancelled)
            {
                item.Status = DownloadStatus.Queued;
                _ = ProcessQueueAsync(item);
            }
        }

        public void RestartItem(DownloadItem item)
        {
            _engine.CleanTempFiles(item);
            item.DownloadedBytes = 0;
            item.Status = DownloadStatus.Queued;
            item.Segments.Clear();
            _ = ProcessQueueAsync(item);
        }

        public void CancelItem(DownloadItem item)
        {
            _engine.Cancel(item);
        }

        public void RemoveItem(DownloadItem item)
        {
            if (item.Status == DownloadStatus.Downloading)
                _engine.Cancel(item);
            Items.Remove(item);
        }
    }
}
