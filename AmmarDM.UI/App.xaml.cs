using System.Linq;
using System.Reflection;
using System.Windows;
using AmmarDM.Core.Data;
using AmmarDM.UI.Services;
using AmmarDM.UI.ViewModels;

namespace AmmarDM.UI
{
    public partial class App : System.Windows.Application
    {
        public static AppSettings Settings { get; private set; } = new();
        public static DownloadRepository Repository { get; private set; } = null!;

        protected override void OnStartup(StartupEventArgs e)
        {
            // إذا تم تشغيل البرنامج من المتصفح كـ Native Messaging Host
            if (e.Args.Length > 0 && e.Args.Any(a => a.Contains("chrome-extension") || a.Contains("native-messaging")))
            {
                NativeMessagingService.RunAsNativeHost();
                Shutdown();
                return;
            }

            base.OnStartup(e);

            var appData = System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData);
            Repository = new DownloadRepository(appData);
            Settings = Repository.LoadSettings();

            // تسجيل الـ Native Messaging تلقائياً في النظام
            var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
            if (!string.IsNullOrEmpty(exePath))
            {
                NativeMessagingService.RegisterHost(exePath);
            }

            DispatcherUnhandledException += (s, ex) =>
            {
                MessageBox.Show($"خطأ: {ex.Exception.Message}", "Ammar DM", MessageBoxButton.OK, MessageBoxImage.Error);
                ex.Handled = true;
            };
        }

        protected override void OnExit(ExitEventArgs e)
        {
            Repository.SaveSettings(Settings);
            base.OnExit(e);
        }
    }
}
