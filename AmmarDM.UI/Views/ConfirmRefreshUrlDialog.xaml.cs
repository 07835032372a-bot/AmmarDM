using System.Windows;

namespace AmmarDM.UI.Views
{
    public partial class ConfirmRefreshUrlDialog : Window
    {
        public bool IsConfirmed { get; private set; }

        public ConfirmRefreshUrlDialog(string originalName, string detectedName, string originalSize, string detectedSize, string capturedUrl)
        {
            InitializeComponent();
            OriginalFileNameText.Text = originalName;
            DetectedFileNameText.Text = string.IsNullOrEmpty(detectedName) ? originalName : detectedName;
            SizeComparisonText.Text = $"{detectedSize} (الأصلي: {originalSize})";
            CapturedUrlText.Text = capturedUrl;
        }

        private void YesBtn_Click(object sender, RoutedEventArgs e)
        {
            IsConfirmed = true;
            DialogResult = true;
        }

        private void NoBtn_Click(object sender, RoutedEventArgs e)
        {
            IsConfirmed = false;
            DialogResult = false;
        }

        private void CancelBtn_Click(object sender, RoutedEventArgs e)
        {
            IsConfirmed = false;
            DialogResult = false;
        }
    }
}
