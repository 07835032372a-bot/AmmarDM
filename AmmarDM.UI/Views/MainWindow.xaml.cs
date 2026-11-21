using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using AmmarDM.Core.Downloading;
using AmmarDM.Core.Models;
using AmmarDM.UI.Services;
using AmmarDM.UI.ViewModels;

namespace AmmarDM.UI.Views
{
    public partial class MainWindow : Window
    {
        private MainViewModel ViewModel => (MainViewModel)DataContext;
        private readonly CancellationTokenSource _ipcTokenSource = new();
        private bool _isExplicitExit = false;
        private NativeMethods.NOTIFYICONDATA _notifyIconData;
        private bool _isTrayCreated = false;
        private ContextMenu? _trayContextMenu;

        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainViewModel();

            ViewModel.RequestAddDownload += ShowAddDownloadDialog;
            ViewModel.RequestSettings += ShowSettingsDialog;
            ViewModel.RequestShowDetail += item => ShowDownloadDetail(item);

            Loaded += MainWindow_Loaded;

            // التحقق من وسيط التشغيل الصامت (--minimized أو /silent)
            if (Environment.GetCommandLineArgs().Any(a => a.Equals("--minimized", StringComparison.OrdinalIgnoreCase) || a.Equals("/silent", StringComparison.OrdinalIgnoreCase)))
            {
                WindowState = WindowState.Minimized;
                Hide();
            }

            // بدء الاستماع لطلبات التحميل القادمة من إضافة المتصفح تلقائياً (Named Pipes + HTTP Server المباشر)
            NativeMessagingService.StartIpcServer(OnBrowserDownloadReceived, _ipcTokenSource.Token);
            LocalHttpServer.Start(OnBrowserDownloadReceived);

            // فحص التحديثات تلقائياً في الخلفية عند بدء التشغيل بدون إزعاج المستخدم إن لم يتوفر تحديث
            _ = CheckForUpdatesSilentlyAsync();
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            InitSystemTray();
        }

        private void InitSystemTray()
        {
            if (_isTrayCreated) return;

            try
            {
                var handle = new WindowInteropHelper(this).Handle;
                var source = HwndSource.FromHwnd(handle);
                source?.AddHook(WndProc);

                // إنشاء قائمة كليك يمين بجانب الساعة
                _trayContextMenu = new ContextMenu { FlowDirection = FlowDirection.RightToLeft };
                
                var openItem = new MenuItem { Header = "فتح Ammar DM" };
                openItem.Click += (s, e) => RestoreMainWindow();
                _trayContextMenu.Items.Add(openItem);

                var addItem = new MenuItem { Header = "إضافة رابط جديد..." };
                addItem.Click += (s, e) => ShowAddDownloadDialog();
                _trayContextMenu.Items.Add(addItem);

                _trayContextMenu.Items.Add(new Separator());

                var resumeItem = new MenuItem { Header = "استئناف الكل" };
                resumeItem.Click += (s, e) => ViewModel.ResumeAllCommand.Execute(null);
                _trayContextMenu.Items.Add(resumeItem);

                var pauseItem = new MenuItem { Header = "إيقاف الكل" };
                pauseItem.Click += (s, e) => ViewModel.PauseAllCommand.Execute(null);
                _trayContextMenu.Items.Add(pauseItem);

                _trayContextMenu.Items.Add(new Separator());

                var exitItem = new MenuItem { Header = "خروج نهائي" };
                exitItem.Click += (s, e) => ExitApplication();
                _trayContextMenu.Items.Add(exitItem);

                // استخراج مقبض الأيقونة من التطبيق
                var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                var icoPath = Path.Combine(baseDir, "Resources", "app_icon.ico");
                IntPtr hIcon = IntPtr.Zero;

                if (File.Exists(icoPath))
                {
                    hIcon = NativeMethods.LoadImage(IntPtr.Zero, icoPath, NativeMethods.IMAGE_ICON, 16, 16, NativeMethods.LR_LOADFROMFILE);
                }

                _notifyIconData = new NativeMethods.NOTIFYICONDATA
                {
                    cbSize = Marshal.SizeOf(typeof(NativeMethods.NOTIFYICONDATA)),
                    hWnd = handle,
                    uID = 1001,
                    uFlags = NativeMethods.NIF_MESSAGE | NativeMethods.NIF_ICON | NativeMethods.NIF_TIP,
                    uCallbackMessage = NativeMethods.WM_TRAYICON,
                    hIcon = hIcon,
                    szTip = "Ammar DM - مدير التحميل"
                };

                NativeMethods.Shell_NotifyIcon(NativeMethods.NIM_ADD, ref _notifyIconData);
                _isTrayCreated = true;
            }
            catch { }
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == NativeMethods.WM_TRAYICON)
            {
                int eventMsg = lParam.ToInt32();
                if (eventMsg == NativeMethods.WM_LBUTTONDBLCLK)
                {
                    RestoreMainWindow();
                    handled = true;
                }
                else if (eventMsg == NativeMethods.WM_RBUTTONUP)
                {
                    if (_trayContextMenu != null)
                    {
                        _trayContextMenu.IsOpen = true;
                    }
                    handled = true;
                }
            }
            return IntPtr.Zero;
        }

        private void RestoreMainWindow()
        {
            Show();
            WindowState = WindowState.Normal;
            Activate();
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (!_isExplicitExit)
            {
                e.Cancel = true;
                Hide();
            }
            else
            {
                if (_isTrayCreated)
                {
                    NativeMethods.Shell_NotifyIcon(NativeMethods.NIM_DELETE, ref _notifyIconData);
                }
                base.OnClosing(e);
            }
        }

        private void ExitApplication()
        {
            _isExplicitExit = true;
            App.Repository.SaveAll(ViewModel.Downloads);
            if (_isTrayCreated)
            {
                NativeMethods.Shell_NotifyIcon(NativeMethods.NIM_DELETE, ref _notifyIconData);
            }
            Application.Current.Shutdown();
        }

        private async Task CheckForUpdatesSilentlyAsync()
        {
            await Task.Delay(3000); // تأخير بسيط حتى تكتمل جاهزية الواجهة
            try
            {
                var update = await AmmarDM.Core.Services.UpdateService.CheckForUpdatesAsync();
                if (update != null)
                {
                    Dispatcher.Invoke(() =>
                    {
                        var win = new UpdateWindow(update) { Owner = this };
                        win.ShowDialog();
                    });
                }
            }
            catch { }
        }

        private void OnBrowserDownloadReceived(NativeHostMessage msg)
        {
            if (string.IsNullOrEmpty(msg.Url)) return;

            // تنفيذ استخراج اسم الملف في الخلفية لتجنب تجميد خيط الـ UI
            Task.Run(() =>
            {
                var fileName = !string.IsNullOrEmpty(msg.FileName) 
                    ? msg.FileName 
                    : DownloadEngine.GetFileNameFromUrl(msg.Url, null);

                var item = new DownloadItem
                {
                    Url = msg.Url,
                    Referrer = msg.Referrer ?? string.Empty,
                    FileName = fileName,
                    SavePath = App.Settings.DefaultDownloadPath,
                    TotalBytes = msg.TotalBytes,
                    MaxConnections = App.Settings.MaxConnections,
                    Category = DownloadEngine.DetectCategory(fileName)
                };

                Dispatcher.BeginInvoke(new System.Action(() =>
                {
                    try
                    {
                        // إظهار نافذة تأكيد التحميل للمستخدم مثل IDM تماماً
                        var dialog = new AddDownloadWindow(msg.Url, fileName)
                        {
                            Owner = this
                        };

                        if (dialog.ShowDialog() == true && dialog.ResultItem != null)
                        {
                            var finalItem = dialog.ResultItem;
                            if (msg.TotalBytes > 0) finalItem.TotalBytes = msg.TotalBytes;
                            finalItem.Referrer = msg.Referrer ?? string.Empty;

                            if (finalItem.Status == DownloadStatus.Paused)
                            {
                                ViewModel.StartDownload(finalItem);
                                ViewModel.PauseItem(finalItem);
                            }
                            else
                            {
                                ViewModel.StartDownload(finalItem);
                                ShowDownloadDetail(finalItem);
                            }
                        }
                    }
                    catch { }
                }));
            });
        }

        private void ToolBar_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is ToolBar toolBar)
            {
                if (toolBar.Template.FindName("OverflowGrid", toolBar) is FrameworkElement overflowGrid)
                    overflowGrid.Visibility = Visibility.Collapsed;
                if (toolBar.Template.FindName("MainPanelBorder", toolBar) is FrameworkElement mainPanelBorder)
                    mainPanelBorder.Margin = new Thickness(0);
            }
        }

        private void ShowDownloadDetail(AmmarDM.Core.Models.DownloadItem item)
        {
            // التحقق إذا كانت نافذة التفاصيل لهذا التحميل مفتوحة بالفعل لنجعلها في المقدمة
            foreach (Window win in Application.Current.Windows)
            {
                if (win is DownloadDetailWindow detailWin && detailWin.Tag as string == item.Id)
                {
                    detailWin.Activate();
                    if (detailWin.WindowState == WindowState.Minimized) detailWin.WindowState = WindowState.Normal;
                    return;
                }
            }

            var progressDialog = new DownloadDetailWindow(item, ViewModel)
            {
                Owner = this,
                Tag = item.Id
            };
            progressDialog.Show();
        }

        private void ShowAddDownloadDialog()
        {
            var dialog = new AddDownloadWindow { Owner = this };
            if (dialog.ShowDialog() == true && dialog.ResultItem != null)
            {
                var item = dialog.ResultItem;

                // فحص إذا كان الرابط موجوداً بالفعل مسبقاً في القائمة كما في IDM
                var existing = ViewModel.Downloads.FirstOrDefault(d => 
                    string.Equals(d.Url?.Trim(), item.Url?.Trim(), System.StringComparison.OrdinalIgnoreCase));

                if (existing != null)
                {
                    var dupDialog = new DuplicateDownloadDialog(existing.FileName) { Owner = this };
                    if (dupDialog.ShowDialog() == true)
                    {
                        if (dupDialog.Action == DuplicateAction.Resume)
                        {
                            ViewModel.ResumeItem(existing);
                            return;
                        }
                        else if (dupDialog.Action == DuplicateAction.Overwrite)
                        {
                            ViewModel.RestartItem(existing);
                            return;
                        }
                    }
                    return;
                }

                ViewModel.StartDownload(item);
                ShowDownloadDetail(item);
            }
        }

        private void ShowSettingsDialog()
        {
            var dialog = new SettingsWindow { Owner = this };
            dialog.ShowDialog();
        }

        private void DataGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (ViewModel.SelectedItem != null)
            {
                // فتح نافذة خصائص وتفاصيل الملف وميزة تحديث الرابط مثل IDM
                var propDialog = new FilePropertiesWindow(ViewModel.SelectedItem, ViewModel) { Owner = this };
                propDialog.ShowDialog();
            }
        }

        private void UpdateUrlMenu_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.SelectedItem != null)
            {
                var dialog = new UpdateUrlDialog(ViewModel.SelectedItem, ViewModel) { Owner = this };
                if (dialog.ShowDialog() == true)
                {
                    MessageBox.Show("تم تحديث رابط التحميل بنجاح!\nيمكنك الآن الضغط على زر استئناف لإكمال التحميل.", "Ammar DM", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }

        private void PropertiesMenu_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.SelectedItem != null)
            {
                var propDialog = new FilePropertiesWindow(ViewModel.SelectedItem, ViewModel) { Owner = this };
                propDialog.ShowDialog();
            }
        }

        private async void CheckUpdatesMenu_Click(object sender, RoutedEventArgs e)
        {
            var updateTask = AmmarDM.Core.Services.UpdateService.CheckForUpdatesAsync();

            // فحص يدوي مع إظهار النتيجة للمستخدم
            try
            {
                var update = await updateTask;
                if (update != null)
                {
                    var win = new UpdateWindow(update) { Owner = this };
                    win.ShowDialog();
                }
                else
                {
                    var current = AmmarDM.Core.Services.UpdateService.CurrentVersion;
                    MessageBox.Show($"أنت تستخدم أحدث إصدار من Ammar DM ({current.Major}.{current.Minor}.{Math.Max(0, current.Build)}).\nلا تتوفر تحديثات جديدة حالياً.", "التحقق من التحديثات", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"تعذر التحقق من وجود تحديثات:\n{ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void CloseMenu_Click(object sender, RoutedEventArgs e)
        {
            ExitApplication();
        }

        private void InstallExtension_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                // مجلد الإضافة في بيئة العمل أو التثبيت
                var extPath = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "AmmarDM.Extension"));
                if (!Directory.Exists(extPath))
                {
                    extPath = Path.Combine(baseDir, "AmmarDM.Extension");
                }

                // تسجيل Native Host في Registry لـ Chrome و Edge
                var exePath = Environment.ProcessPath ?? Path.Combine(baseDir, "AmmarDM.exe");
                NativeMessagingService.RegisterHost(exePath);

                var msg = "لتثبيت إضافة المتصفح الخاصة بـ Ammar DM:\n\n" +
                          "1. افتح صفحة الإضافات في متصفحك:\n" +
                          "   - Chrome: chrome://extensions\n" +
                          "   - Edge: edge://extensions\n" +
                          "   - Brave: brave://extensions\n\n" +
                          "2. فعّل (وضع المطور / Developer Mode) في الزاوية العلوية.\n\n" +
                          "3. اضغط على زر (تحميل إضافة لم يتم حزمها / Load unpacked) ثم اختر مجلد الإضافة الذي سنفتحه لك الآن.\n\n" +
                          "هل تريد فتح مجلد الإضافة الآن؟";

                if (MessageBox.Show(msg, "تثبيت إضافة المتصفح لـ Ammar DM", MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes)
                {
                    if (Directory.Exists(extPath))
                    {
                        System.Diagnostics.Process.Start("explorer.exe", extPath);
                    }
                    else
                    {
                        System.Diagnostics.Process.Start("explorer.exe", baseDir);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"حدث خطأ أثناء إعداد الإضافة: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OpenExtensionFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                var extPath = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "AmmarDM.Extension"));
                if (Directory.Exists(extPath))
                {
                    System.Diagnostics.Process.Start("explorer.exe", extPath);
                }
                else
                {
                    System.Diagnostics.Process.Start("explorer.exe", baseDir);
                }
            }
            catch { }
        }

        private void AboutMenu_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("برنامج Ammar DM (Ammar Download Manager)\nبرنامج تحميل متطور متعدد الخيوط\nالمطور: عمار الصميدعي", "حول Ammar DM", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
