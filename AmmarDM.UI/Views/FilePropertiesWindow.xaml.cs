using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using AmmarDM.Core.Models;
using AmmarDM.UI.ViewModels;

namespace AmmarDM.UI.Views
{
    public partial class FilePropertiesWindow : Window
    {
        private readonly DownloadItem _item;
        private readonly MainViewModel _viewModel;

        public FilePropertiesWindow(DownloadItem item, MainViewModel viewModel)
        {
            InitializeComponent();
            _item = item;
            _viewModel = viewModel;

            FileNameHeader.Text = _item.FileName;
            StatusHeader.Text = $"الحالة: {_item.StatusDisplay} - تم تحميل {_item.DownloadedDisplay} من {_item.FileSizeDisplay}";

            UrlTextBox.Text = _item.Url;
            FileNameTextBox.Text = _item.FileName;
            SavePathTextBox.Text = _item.SavePath;
            FileSizeText.Text = _item.FileSizeDisplay;
            DateAddedText.Text = _item.CreatedAt.ToString("yyyy/MM/dd HH:mm:ss");
            CommentTextBox.Text = _item.Comment;

            OpenFileBtn.IsEnabled = _item.Status == DownloadStatus.Completed && File.Exists(_item.FullPath);
        }

        private void UpdateUrlBtn_Click(object sender, RoutedEventArgs e)
        {
            var newUrl = UrlTextBox.Text.Trim();
            if (string.IsNullOrEmpty(newUrl) || !Uri.TryCreate(newUrl, UriKind.Absolute, out _))
            {
                MessageBox.Show("يرجى إدخال رابط تحميل صالح.", "تحديث الرابط", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _item.Url = newUrl;
            _viewModel.SaveAll();
            MessageBox.Show("تم تحديث رابط التحميل بنجاح!\nيمكنك الآن استئناف التحميل من هذا الرابط الجديد مباشرة.", "تم التحديث", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void OpenFileBtn_Click(object sender, RoutedEventArgs e)
        {
            if (File.Exists(_item.FullPath))
            {
                try
                {
                    Process.Start(new ProcessStartInfo(_item.FullPath) { UseShellExecute = true });
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"تعذر فتح الملف: {ex.Message}", "Ammar DM");
                }
            }
        }

        private void OpenFolderBtn_Click(object sender, RoutedEventArgs e)
        {
            if (Directory.Exists(_item.SavePath))
            {
                try
                {
                    Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{_item.FullPath}\"") { UseShellExecute = true });
                }
                catch
                {
                    Process.Start(new ProcessStartInfo("explorer.exe", _item.SavePath) { UseShellExecute = true });
                }
            }
        }

        private void SaveBtn_Click(object sender, RoutedEventArgs e)
        {
            var newUrl = UrlTextBox.Text.Trim();
            if (!string.IsNullOrEmpty(newUrl) && Uri.TryCreate(newUrl, UriKind.Absolute, out _))
            {
                _item.Url = newUrl;
            }

            var newFileName = FileNameTextBox.Text.Trim();
            if (!string.IsNullOrEmpty(newFileName))
            {
                _item.FileName = newFileName;
            }

            _item.Comment = CommentTextBox.Text;
            _viewModel.SaveAll();
            DialogResult = true;
        }

        private void CloseBtn_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
