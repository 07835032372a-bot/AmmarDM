using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using AmmarDM.Core.Models;
using AmmarDM.Core.Services;

namespace AmmarDM.UI.Views
{
    public partial class UpdateWindow : Window
    {
        private readonly UpdateInfo _updateInfo;
        private CancellationTokenSource? _downloadCts;

        public UpdateWindow(UpdateInfo updateInfo)
        {
            InitializeComponent();
            _updateInfo = updateInfo;

            var current = UpdateService.CurrentVersion;
            VersionText.Text = $"الإصدار الجديد: {_updateInfo.Version} (الإصدار الحالي: {current.Major}.{current.Minor}.{Math.Max(0, current.Build)})";

            if (_updateInfo.Changelog != null && _updateInfo.Changelog.Count > 0)
            {
                ChangelogList.ItemsSource = _updateInfo.Changelog;
            }
            else
            {
                ChangelogList.ItemsSource = new[] { "تحسينات عامة في الأداء وإصلاح الأخطاء." };
            }

            if (_updateInfo.IsMandatory)
            {
                LaterBtn.Visibility = Visibility.Collapsed;
            }
        }

        private async void UpdateNowBtn_Click(object sender, RoutedEventArgs e)
        {
            UpdateNowBtn.IsEnabled = false;
            LaterBtn.IsEnabled = false;
            ProgressPanel.Visibility = Visibility.Visible;

            _downloadCts = new CancellationTokenSource();
            var tempZip = Path.Combine(Path.GetTempPath(), $"AmmarDM_Update_{Guid.NewGuid():N}.zip");

            try
            {
                ProgressStatusText.Text = "جاري تحميل حزمة التحديث...";
                await UpdateService.DownloadUpdateFileAsync(_updateInfo.DownloadUrl, tempZip, pct =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        DownloadProgressBar.Value = pct;
                        ProgressStatusText.Text = $"جاري تحميل حزمة التحديث... ({pct:F0}%)";
                    });
                }, _downloadCts.Token);

                ProgressStatusText.Text = "اكتمل التحميل! جاري تشغيل أداة التحديث...";
                DownloadProgressBar.Value = 100;

                await Task.Delay(800);

                // إطلاق AmmarDM.Updater.exe
                var appDir = AppDomain.CurrentDomain.BaseDirectory;
                var updaterExe = Path.Combine(appDir, "AmmarDM.Updater.exe");

                if (!File.Exists(updaterExe))
                {
                    MessageBox.Show("لم يتم العثور على أداة التحديث AmmarDM.Updater.exe في مجلد البرنامج.", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                    UpdateNowBtn.IsEnabled = true;
                    LaterBtn.IsEnabled = true;
                    return;
                }

                var currentPid = Process.GetCurrentProcess().Id;
                var startInfo = new ProcessStartInfo
                {
                    FileName = updaterExe,
                    Arguments = $"\"{tempZip}\" \"{appDir.TrimEnd('\\')}\" {currentPid}",
                    UseShellExecute = true
                };

                Process.Start(startInfo);

                // إغلاق البرنامج الحالي فوراً لإتاحة استبدال الملفات
                Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"تعذر إكمال التحديث:\n{ex.Message}", "خطأ في التحديث", MessageBoxButton.OK, MessageBoxImage.Error);
                UpdateNowBtn.IsEnabled = true;
                LaterBtn.IsEnabled = true;
                ProgressPanel.Visibility = Visibility.Collapsed;
            }
        }

        private void LaterBtn_Click(object sender, RoutedEventArgs e)
        {
            _downloadCts?.Cancel();
            Close();
        }

        protected override void OnClosed(EventArgs e)
        {
            _downloadCts?.Cancel();
            base.OnClosed(e);
        }
    }
}
