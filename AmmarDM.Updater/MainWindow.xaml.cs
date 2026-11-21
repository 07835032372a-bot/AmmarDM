using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using System.Windows;

namespace AmmarDM.Updater
{
    public partial class MainWindow : Window
    {
        private readonly string? _zipPath;
        private readonly string? _targetDir;
        private readonly int _mainProcessId;

        public MainWindow()
        {
            InitializeComponent();

            var args = Environment.GetCommandLineArgs();
            // المتوقع تمرير: AmmarDM.Updater.exe <zipPath> <targetDir> <mainProcessId>
            if (args.Length >= 4)
            {
                _zipPath = args[1];
                _targetDir = args[2];
                int.TryParse(args[3], out _mainProcessId);
            }

            Loaded += MainWindow_Loaded;
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await Task.Delay(500);

            if (string.IsNullOrEmpty(_zipPath) || string.IsNullOrEmpty(_targetDir) || !File.Exists(_zipPath))
            {
                MessageBox.Show("معلمات التحديث غير صالحة أو ملف التحديث غير موجود.", "خطأ في التحديث", MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
                return;
            }

            try
            {
                // 1. انتظار إغلاق البرنامج الرئيسي بالكامل
                StatusLabel.Text = "انتظار إغلاق Ammar DM...";
                UpdateProgressBar.Value = 20;

                if (_mainProcessId > 0)
                {
                    try
                    {
                        var proc = Process.GetProcessById(_mainProcessId);
                        if (!proc.HasExited)
                        {
                            proc.WaitForExit(7000);
                        }
                    }
                    catch { }
                }

                // التأكد الإضافي من إغلاق أي عملية AmmarDM
                foreach (var p in Process.GetProcessesByName("AmmarDM"))
                {
                    try
                    {
                        p.Kill();
                        p.WaitForExit(3000);
                    }
                    catch { }
                }

                await Task.Delay(1000);

                // 2. فك ضغط ملفات التحديث فوق المجلد الهدف
                StatusLabel.Text = "تثبيت الملفات الجديدة...";
                UpdateProgressBar.Value = 60;

                using (var archive = ZipFile.OpenRead(_zipPath))
                {
                    foreach (var entry in archive.Entries)
                    {
                        // تخطي ملف المحدث نفسه إن وجد لكي لا يتعارض مع العملية الجارية
                        if (entry.FullName.Equals("AmmarDM.Updater.exe", StringComparison.OrdinalIgnoreCase) ||
                            entry.FullName.Equals("AmmarDM.Updater.dll", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        var destinationPath = Path.GetFullPath(Path.Combine(_targetDir, entry.FullName));
                        // حماية أمنية من ثغرة Zip Slip: التأكد أن المسار لا يخرج خارج المجلد المستهدف
                        if (!destinationPath.StartsWith(Path.GetFullPath(_targetDir), StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        var dir = Path.GetDirectoryName(destinationPath);
                        if (!string.IsNullOrEmpty(dir))
                        {
                            Directory.CreateDirectory(dir);
                        }

                        if (!string.IsNullOrEmpty(entry.Name))
                        {
                            entry.ExtractToFile(destinationPath, overwrite: true);
                        }
                    }
                }

                UpdateProgressBar.Value = 90;
                StatusLabel.Text = "اكتمل التحديث! إعادة تشغيل Ammar DM...";
                await Task.Delay(1000);

                // 3. حذف ملف التحديث المؤقت
                try { File.Delete(_zipPath); } catch { }

                // 4. إعادة تشغيل AmmarDM الجديد
                var exePath = Path.Combine(_targetDir, "AmmarDM.exe");
                if (File.Exists(exePath))
                {
                    Process.Start(new ProcessStartInfo(exePath) { UseShellExecute = true });
                }

                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"حدث خطأ أثناء تطبيق التحديث:\n{ex.Message}", "خطأ في التحديث", MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
            }
        }
    }
}
