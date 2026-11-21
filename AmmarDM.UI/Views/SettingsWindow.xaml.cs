using System.Windows;
using Microsoft.Win32;

namespace AmmarDM.UI.Views
{
    public partial class SettingsWindow : Window
    {
        public SettingsWindow()
        {
            InitializeComponent();
            LoadSettings();
        }

        private void LoadSettings()
        {
            var s = App.Settings;
            DefaultSavePathTextBox.Text = s.DefaultDownloadPath;
            StartWithWindowsCheckBox.IsChecked = s.StartWithWindows;
            ShowNotifyCheckBox.IsChecked = s.ShowNotifications;
            PlaySoundCheckBox.IsChecked = s.SoundOnComplete;

            DefaultConnectionsComboBox.SelectedIndex = s.MaxConnections switch
            {
                4 => 0, 8 => 1, 16 => 2, 32 => 3, _ => 2
            };
            MaxConcurrentComboBox.SelectedIndex = s.MaxConcurrentDownloads switch
            {
                1 => 0, 2 => 1, 3 => 2, 5 => 3, _ => 2
            };

            CaptureDownloadsCheckBox.IsChecked = s.CaptureBrowserDownloads;
            ShowVideoPanelCheckBox.IsChecked = s.ShowVideoDownloadPanel;
            ExtensionsTextBox.Text = s.CapturedExtensions;
            IgnoredSitesTextBox.Text = s.IgnoredSites;
        }

        private void SaveBtn_Click(object sender, RoutedEventArgs e)
        {
            var s = App.Settings;
            s.DefaultDownloadPath = DefaultSavePathTextBox.Text;
            s.MaxConnections = DefaultConnectionsComboBox.SelectedIndex switch
            {
                0 => 4, 1 => 8, 2 => 16, 3 => 32, _ => 16
            };
            s.MaxConcurrentDownloads = MaxConcurrentComboBox.SelectedIndex switch
            {
                0 => 1, 1 => 2, 2 => 3, 3 => 5, _ => 2
            };
            s.StartWithWindows = StartWithWindowsCheckBox.IsChecked == true;
            s.ShowNotifications = ShowNotifyCheckBox.IsChecked == true;
            s.SoundOnComplete = PlaySoundCheckBox.IsChecked == true;

            s.CaptureBrowserDownloads = CaptureDownloadsCheckBox.IsChecked == true;
            s.ShowVideoDownloadPanel = ShowVideoPanelCheckBox.IsChecked == true;
            s.CapturedExtensions = ExtensionsTextBox.Text.Trim();
            s.IgnoredSites = IgnoredSitesTextBox.Text.Trim();

            App.Repository.SaveSettings(s);
            DialogResult = true;
        }

        private void BrowseBtn_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFolderDialog
            {
                Title = "اختر المجلد الافتراضي",
                InitialDirectory = DefaultSavePathTextBox.Text
            };
            if (dialog.ShowDialog() == true)
                DefaultSavePathTextBox.Text = dialog.FolderName;
        }

        private void CancelBtn_Click(object sender, RoutedEventArgs e) => DialogResult = false;
    }
}
