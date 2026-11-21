using System;
using System.Windows;
using Microsoft.Win32;
using AmmarDM.Core.Downloading;
using AmmarDM.Core.Models;

namespace AmmarDM.UI.Views
{
    public partial class AddDownloadWindow : Window
    {
        public DownloadItem? ResultItem { get; private set; }

        public AddDownloadWindow(string? initialUrl = null, string? initialFileName = null)
        {
            InitializeComponent();
            SavePathTextBox.Text = App.Settings.DefaultDownloadPath;

            if (!string.IsNullOrEmpty(initialUrl))
            {
                UrlTextBox.Text = initialUrl;
                if (!string.IsNullOrEmpty(initialFileName))
                {
                    FileNameTextBox.Text = initialFileName;
                }
            }
            else if (Clipboard.ContainsText())
            {
                var text = Clipboard.GetText().Trim();
                if (Uri.TryCreate(text, UriKind.Absolute, out var uri) &&
                    (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
                {
                    UrlTextBox.Text = text;
                }
            }
        }

        private void CloseBtn_Click(object sender, RoutedEventArgs e) => DialogResult = false;
        private void CancelBtn_Click(object sender, RoutedEventArgs e) => DialogResult = false;

        private void PasteBtn_Click(object sender, RoutedEventArgs e)
        {
            if (Clipboard.ContainsText())
                UrlTextBox.Text = Clipboard.GetText().Trim();
        }

        private void BrowseBtn_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFolderDialog
            {
                Title = "اختر مجلد الحفظ",
                InitialDirectory = SavePathTextBox.Text
            };
            if (dialog.ShowDialog() == true)
                SavePathTextBox.Text = dialog.FolderName;
        }

        private void UrlTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            var url = UrlTextBox.Text.Trim();
            if (Uri.TryCreate(url, UriKind.Absolute, out _))
            {
                if (string.IsNullOrEmpty(FileNameTextBox.Text))
                {
                    FileNameTextBox.Text = DownloadEngine.GetFileNameFromUrl(url, null);
                }
            }
        }

        private void StartBtn_Click(object sender, RoutedEventArgs e)
        {
            if (CreateDownloadItem())
                DialogResult = true;
        }

        private void DownloadLaterBtn_Click(object sender, RoutedEventArgs e)
        {
            if (CreateDownloadItem())
            {
                if (ResultItem != null)
                    ResultItem.Status = DownloadStatus.Paused;
                DialogResult = true;
            }
        }

        private bool CreateDownloadItem()
        {
            var url = UrlTextBox.Text.Trim();
            if (string.IsNullOrEmpty(url))
            {
                StatusLabel.Text = "الرجاء إدخال رابط التحميل";
                return false;
            }

            if (!Uri.TryCreate(url, UriKind.Absolute, out _))
            {
                StatusLabel.Text = "الرابط غير صالح";
                return false;
            }

            var savePath = SavePathTextBox.Text.Trim();
            if (string.IsNullOrEmpty(savePath))
                savePath = App.Settings.DefaultDownloadPath;

            var fileName = FileNameTextBox.Text.Trim();
            if (string.IsNullOrEmpty(fileName))
                fileName = DownloadEngine.GetFileNameFromUrl(url, null);

            ResultItem = new DownloadItem
            {
                Url = url,
                FileName = fileName,
                SavePath = savePath,
                MaxConnections = App.Settings.MaxConnections,
                Category = DownloadEngine.DetectCategory(fileName)
            };

            return true;
        }
    }
}
