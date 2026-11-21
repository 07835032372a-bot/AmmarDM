using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using AmmarDM.Core.Downloading;
using AmmarDM.Core.Models;
using AmmarDM.Core.Queue;

namespace AmmarDM.UI.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly DownloadQueue _queue;
        private DownloadItem? _selectedItem;
        private string _searchText = string.Empty;
        private string _selectedCategory = "الكل";
        private string _statusText = "جاهز";
        private double _totalSpeed;
        private int _activeCount;

        public ObservableCollection<DownloadItem> Downloads => _queue.Items;
        public ObservableCollection<string> Categories { get; } = new()
        {
            "الكل", "جارٍ التحميل", "مكتمل", "متوقف", "مجدولة",
            "مقاطع الفيديو", "الموسيقى", "المستندات", "البرامج", "الأرشيفات", "الصور"
        };

        public DownloadItem? SelectedItem
        {
            get => _selectedItem;
            set { _selectedItem = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasSelection)); }
        }

        public bool HasSelection => SelectedItem != null;

        public string SearchText
        {
            get => _searchText;
            set { _searchText = value; OnPropertyChanged(); }
        }

        public string SelectedCategory
        {
            get => _selectedCategory;
            set { _selectedCategory = value; OnPropertyChanged(); }
        }

        public string StatusText
        {
            get => _statusText;
            set { _statusText = value; OnPropertyChanged(); }
        }

        public double TotalSpeed
        {
            get => _totalSpeed;
            set { _totalSpeed = value; OnPropertyChanged(); OnPropertyChanged(nameof(TotalSpeedDisplay)); }
        }

        public int ActiveCount
        {
            get => _activeCount;
            set { _activeCount = value; OnPropertyChanged(); }
        }

        public string TotalSpeedDisplay => DownloadItem.FormatSpeed(TotalSpeed);

        // Commands
        public ICommand AddDownloadCommand { get; }
        public ICommand PauseCommand { get; }
        public ICommand ResumeCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand DeleteCommand { get; }
        public ICommand OpenFileCommand { get; }
        public ICommand OpenFolderCommand { get; }
        public ICommand PauseAllCommand { get; }
        public ICommand ResumeAllCommand { get; }
        public ICommand ClearCompletedCommand { get; }
        public ICommand SettingsCommand { get; }

        public event Action? RequestAddDownload;
        public event Action? RequestSettings;
        public event Action<DownloadItem>? RequestShowDetail;

        public MainViewModel()
        {
            _queue = new DownloadQueue(App.Settings.MaxConcurrentDownloads);

            _queue.ItemProgressChanged += OnItemProgress;
            _queue.ItemCompleted += OnItemCompleted;
            _queue.ItemFailed += OnItemFailed;

            var saved = App.Repository.LoadAll();
            foreach (var item in saved.Where(i => i.Status != DownloadStatus.Completed))
            {
                item.Status = DownloadStatus.Paused;
                _queue.Items.Add(item);
            }
            foreach (var item in saved.Where(i => i.Status == DownloadStatus.Completed))
            {
                _queue.Items.Add(item);
            }

            AddDownloadCommand = new RelayCommand(_ => RequestAddDownload?.Invoke());
            PauseCommand = new RelayCommand(_ => PauseSelected(), _ => SelectedItem?.Status == DownloadStatus.Downloading);
            ResumeCommand = new RelayCommand(_ => ResumeSelected(), _ =>
                SelectedItem?.Status == DownloadStatus.Paused || SelectedItem?.Status == DownloadStatus.Error || SelectedItem?.Status == DownloadStatus.Cancelled);
            CancelCommand = new RelayCommand(_ => CancelSelected(), _ => SelectedItem != null &&
                SelectedItem.Status != DownloadStatus.Completed);
            DeleteCommand = new RelayCommand(_ => DeleteSelected(), _ => SelectedItem != null);
            OpenFileCommand = new RelayCommand(_ => OpenFile(), _ => SelectedItem?.Status == DownloadStatus.Completed);
            OpenFolderCommand = new RelayCommand(_ => OpenFolder(), _ => SelectedItem != null);
            PauseAllCommand = new RelayCommand(_ => _queue.PauseAll());
            ResumeAllCommand = new RelayCommand(_ => ResumeAll());
            ClearCompletedCommand = new RelayCommand(_ => ClearCompleted());
            SettingsCommand = new RelayCommand(_ => RequestSettings?.Invoke());
        }

        public void StartDownload(DownloadItem item)
        {
            _queue.Enqueue(item);
            SaveAll();
        }

        public void PauseItem(DownloadItem item)
        {
            _queue.PauseItem(item);
            SaveAll();
        }

        public void RestartItem(DownloadItem item)
        {
            _queue.RestartItem(item);
            SaveAll();
            RequestShowDetail?.Invoke(item);
        }

        public void ResumeItem(DownloadItem item)
        {
            _queue.ResumeItem(item);
            SaveAll();
            RequestShowDetail?.Invoke(item);
        }

        public void CancelItem(DownloadItem item)
        {
            _queue.CancelItem(item);
            SaveAll();
        }

        private void PauseSelected()
        {
            if (SelectedItem == null) return;
            PauseItem(SelectedItem);
        }

        private void ResumeSelected()
        {
            if (SelectedItem == null) return;
            ResumeItem(SelectedItem);
        }

        private void CancelSelected()
        {
            if (SelectedItem == null) return;
            CancelItem(SelectedItem);
        }

        private void DeleteSelected()
        {
            if (SelectedItem == null) return;
            _queue.RemoveItem(SelectedItem);
            SaveAll();
        }

        private void ResumeAll()
        {
            foreach (var item in Downloads.Where(i => i.Status == DownloadStatus.Paused || i.Status == DownloadStatus.Error || i.Status == DownloadStatus.Cancelled).ToList())
                _queue.ResumeItem(item);
            SaveAll();
        }

        private void ClearCompleted()
        {
            var completed = Downloads.Where(i => i.Status == DownloadStatus.Completed).ToList();
            foreach (var item in completed)
                Downloads.Remove(item);
            SaveAll();
        }

        private void OpenFile()
        {
            if (SelectedItem == null) return;
            if (File.Exists(SelectedItem.FullPath))
            {
                try
                {
                    Process.Start(new ProcessStartInfo(SelectedItem.FullPath) { UseShellExecute = true });
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"تعذر فتح الملف: {ex.Message}", "Ammar DM");
                }
            }
            else
            {
                MessageBox.Show("الملف غير موجود على القرص!", "Ammar DM");
            }
        }

        private void OpenFolder()
        {
            if (SelectedItem == null) return;
            if (Directory.Exists(SelectedItem.SavePath))
            {
                try
                {
                    Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{SelectedItem.FullPath}\"") { UseShellExecute = true });
                }
                catch
                {
                    Process.Start(new ProcessStartInfo(SelectedItem.SavePath) { UseShellExecute = true });
                }
            }
        }

        private bool _isUpdatingOverallStats = false;

        private void OnItemProgress(DownloadItem item)
        {
            if (_isUpdatingOverallStats) return;
            _isUpdatingOverallStats = true;

            Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
            {
                try { UpdateOverallStats(); }
                finally { _isUpdatingOverallStats = false; }
            }), System.Windows.Threading.DispatcherPriority.Background);
        }

        private void OnItemCompleted(DownloadItem item)
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                UpdateOverallStats();
                SaveAll();
                StatusText = $"اكتمل تحميل: {item.FileName}";
            });
        }

        private void OnItemFailed(DownloadItem item, Exception ex)
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                UpdateOverallStats();
                SaveAll();
                StatusText = $"فشل التحميل: {ex.Message}";
            });
        }

        private void UpdateOverallStats()
        {
            ActiveCount = Downloads.Count(i => i.Status == DownloadStatus.Downloading);
            TotalSpeed = Downloads.Where(i => i.Status == DownloadStatus.Downloading).Sum(i => i.Speed);
            if (ActiveCount > 0)
                StatusText = $"جارٍ تحميل {ActiveCount} ملفات بسرعة {TotalSpeedDisplay}";
            else
                StatusText = "جاهز";
        }

        private DateTime _lastSaveTime = DateTime.MinValue;

        public void SaveAll()
        {
            Task.Run(() =>
            {
                lock (this)
                {
                    if ((DateTime.Now - _lastSaveTime).TotalSeconds < 1) return;
                    _lastSaveTime = DateTime.Now;
                    try
                    {
                        var list = Downloads.ToList();
                        App.Repository.SaveAll(list);
                    }
                    catch { }
                }
            });
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class RelayCommand : ICommand
    {
        private readonly Action<object?> _execute;
        private readonly Predicate<object?>? _canExecute;

        public RelayCommand(Action<object?> execute, Predicate<object?>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;
        public void Execute(object? parameter) => _execute(parameter);
        public event EventHandler? CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }
    }
}
