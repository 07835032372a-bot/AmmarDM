using System.Windows;

namespace AmmarDM.UI.Views
{
    public enum DuplicateAction
    {
        Resume,
        Overwrite,
        Cancel
    }

    public partial class DuplicateDownloadDialog : Window
    {
        public DuplicateAction Action { get; private set; } = DuplicateAction.Cancel;

        public DuplicateDownloadDialog(string fileName)
        {
            InitializeComponent();
            FileNameText.Text = fileName;
        }

        private void ResumeBtn_Click(object sender, RoutedEventArgs e)
        {
            Action = DuplicateAction.Resume;
            DialogResult = true;
        }

        private void OverwriteBtn_Click(object sender, RoutedEventArgs e)
        {
            Action = DuplicateAction.Overwrite;
            DialogResult = true;
        }

        private void CancelBtn_Click(object sender, RoutedEventArgs e)
        {
            Action = DuplicateAction.Cancel;
            DialogResult = false;
        }
    }
}
