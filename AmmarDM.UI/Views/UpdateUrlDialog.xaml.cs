using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using AmmarDM.Core.Downloading;
using AmmarDM.Core.Models;
using AmmarDM.UI.ViewModels;

namespace AmmarDM.UI.Views
{
    public partial class UpdateUrlDialog : Window
    {
        private readonly DownloadItem _item;
        private readonly MainViewModel _viewModel;
        private readonly DispatcherTimer _clipboardMonitorTimer;
        private readonly DownloadEngine _tempEngine = new();
        private string _lastClipboardText = string.Empty;
        private bool _isCheckingUrl = false;

        public UpdateUrlDialog(DownloadItem item, MainViewModel viewModel)
        {
            InitializeComponent();
            _item = item;
            _viewModel = viewModel;

            FileNameTarget.Text = _item.FileName;
            NewUrlTextBox.Text = _item.Url;

            // إعداد مؤقت مراقبة الحافظة والروابط التلقائي (Clipboard & Link Catcher)
            _clipboardMonitorTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(500)
            };
            _clipboardMonitorTimer.Tick += ClipboardMonitorTimer_Tick;

            try
            {
                if (Clipboard.ContainsText())
                {
                    _lastClipboardText = Clipboard.GetText().Trim();
                }
            }
            catch { }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            _clipboardMonitorTimer.Start();
        }

        private async void ClipboardMonitorTimer_Tick(object? sender, EventArgs e)
        {
            if (_isCheckingUrl) return;

            try
            {
                if (!Clipboard.ContainsText()) return;
                var current = Clipboard.GetText().Trim();
                if (string.IsNullOrEmpty(current) || current == _lastClipboardText || current == _item.Url) return;

                if (Uri.TryCreate(current, UriKind.Absolute, out var uri) &&
                    (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
                {
                    _lastClipboardText = current;
                    _isCheckingUrl = true;

                    MonitoringText.Text = "🔍 تم اقتناص رابط، جاري فحص هوية الملف...";
                    MonitoringIndicator.Text = "🟡";

                    // فحص الرابط المقتنص واستخراج اسم الملف وحجمه للمقارنة
                    string detectedFileName = DownloadEngine.GetFileNameFromUrl(current, null);
                    string detectedSizeStr = "غير معروف";

                    try
                    {
                        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                        var info = await _tempEngine.GetFileInfoAsync(current, cts.Token);
                        if (info.TotalBytes > 0)
                            detectedSizeStr = DownloadItem.FormatSize(info.TotalBytes);
                        if (!string.IsNullOrEmpty(info.ContentDisposition))
                            detectedFileName = DownloadEngine.GetFileNameFromUrl(current, info.ContentDisposition);
                    }
                    catch { }

                    // عرض نافذة تأكيد تطابق الملف للمستخدم كما في IDM تماماً
                    var confirmDialog = new ConfirmRefreshUrlDialog(
                        _item.FileName,
                        detectedFileName,
                        _item.FileSizeDisplay,
                        detectedSizeStr,
                        current
                    )
                    {
                        Owner = this
                    };

                    if (confirmDialog.ShowDialog() == true && confirmDialog.IsConfirmed)
                    {
                        NewUrlTextBox.Text = current;
                        MonitoringText.Text = "✅ تم تأكيد تطابق الملف بنجاح!";
                        MonitoringIndicator.Text = "🟢";
                        StatusLabel.Foreground = System.Windows.Media.Brushes.Green;
                        StatusLabel.Text = "تم تحديث الرابط والتأكد من الملف! جاري الاستئناف...";

                        // تطبيق التحديث فوراً كما في IDM
                        _clipboardMonitorTimer.Stop();
                        _item.Url = current;
                        _viewModel.SaveAll();
                        _viewModel.ResumeItem(_item);
                        DialogResult = true;
                        return;
                    }
                    else
                    {
                        MonitoringText.Text = "متابعة المراقبة لاقتناص الرابط الصحيح...";
                        MonitoringIndicator.Text = "🟢";
                        _isCheckingUrl = false;
                    }
                }
            }
            catch
            {
                _isCheckingUrl = false;
            }
        }

        private void OpenInBrowserBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // فتح صفحة الإحالة الأصلية Referrer أو الرابط الأصلي
                var targetUrl = !string.IsNullOrEmpty(_item.Referrer) ? _item.Referrer : _item.Url;
                Process.Start(new ProcessStartInfo(targetUrl) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"تعذر فتح المتصفح: {ex.Message}", "Ammar DM");
            }
        }

        private void PasteBtn_Click(object sender, RoutedEventArgs e)
        {
            if (Clipboard.ContainsText())
            {
                NewUrlTextBox.Text = Clipboard.GetText().Trim();
            }
        }

        private void OkBtn_Click(object sender, RoutedEventArgs e)
        {
            var newUrl = NewUrlTextBox.Text.Trim();
            if (string.IsNullOrEmpty(newUrl) || !Uri.TryCreate(newUrl, UriKind.Absolute, out _))
            {
                StatusLabel.Foreground = System.Windows.Media.Brushes.Red;
                StatusLabel.Text = "الرجاء إدخال رابط تحميل صالح يبدأ بـ http أو https";
                return;
            }

            _clipboardMonitorTimer.Stop();
            _item.Url = newUrl;
            _viewModel.SaveAll();
            _viewModel.ResumeItem(_item);
            DialogResult = true;
        }

        private void CancelBtn_Click(object sender, RoutedEventArgs e)
        {
            _clipboardMonitorTimer.Stop();
            DialogResult = false;
        }

        protected override void OnClosed(EventArgs e)
        {
            _clipboardMonitorTimer.Stop();
            base.OnClosed(e);
        }
    }
}
