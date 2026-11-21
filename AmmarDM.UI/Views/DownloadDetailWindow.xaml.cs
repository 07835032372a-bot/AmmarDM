using System.Windows;
using Microsoft.Win32;
using AmmarDM.Core.Models;
using AmmarDM.UI.ViewModels;

namespace AmmarDM.UI.Views
{
    public partial class DownloadDetailWindow : Window
    {
        private readonly DownloadItem _item;
        private readonly MainViewModel _viewModel;

        private bool _isUpdatingUI = false;

        public DownloadDetailWindow(DownloadItem item, MainViewModel viewModel)
        {
            InitializeComponent();
            _item = item;
            _viewModel = viewModel;
            _item.PropertyChanged += (s, e) =>
            {
                if (_isUpdatingUI) return;
                _isUpdatingUI = true;
                Dispatcher.BeginInvoke(new System.Action(() =>
                {
                    try { UpdateUI(); }
                    finally { _isUpdatingUI = false; }
                }), System.Windows.Threading.DispatcherPriority.Background);
            };
            UpdateUI();
        }

        private void UpdateUI()
        {
            FileNameLabel.Text = _item.FileName;
            ProgressLabel.Text = $"{_item.Progress:F1}%";
            DetailProgressBar.Value = _item.Progress;
            SpeedLabel.Text = _item.SpeedDisplay;
            EtaLabel.Text = _item.EtaDisplay;
            DownloadedLabel.Text = $"{_item.DownloadedDisplay} من {_item.FileSizeDisplay}";
            TotalLabel.Text = _item.FileSizeDisplay;
            ActiveSegmentsLabel.Text = $"{System.Math.Max(1, _item.ActiveSegments)} خيط";
            StatusLabel.Text = _item.StatusDisplay;

            if (SegmentsGrid.ItemsSource != _item.Segments)
            {
                SegmentsGrid.ItemsSource = _item.Segments;
            }

            if (_item.Status == DownloadStatus.Downloading)
            {
                PauseResumeBtn.Content = "إيقاف مؤقت";
                PauseResumeBtn.IsEnabled = true;
            }
            else if (_item.Status == DownloadStatus.Paused || _item.Status == DownloadStatus.Error || _item.Status == DownloadStatus.Cancelled)
            {
                PauseResumeBtn.Content = "استئناف";
                PauseResumeBtn.IsEnabled = true;
            }
            else if (_item.Status == DownloadStatus.Completed)
            {
                PauseResumeBtn.Content = "اكتمل";
                PauseResumeBtn.IsEnabled = false;
            }
        }

        private void PauseResumeBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_item.Status == DownloadStatus.Downloading)
            {
                _viewModel.PauseItem(_item);
            }
            else
            {
                _viewModel.ResumeItem(_item);
            }
        }

        private void CancelBtn_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.CancelItem(_item);
            Close();
        }

        private void CloseBtn_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
