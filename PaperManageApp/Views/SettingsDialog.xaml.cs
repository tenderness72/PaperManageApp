using Microsoft.Win32;
using PaperManagementApp.Services;
using System.Windows;

namespace PaperManagementApp.Views
{
    public partial class SettingsDialog : Window
    {
        public SettingsDialog()
        {
            InitializeComponent();

            // 現在の設定を読み込んで表示
            var settings = SettingsService.Load();
            PdfPathTextBox.Text = settings.PdfSavePath;
        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFolderDialog
            {
                Title = "PDFの保存先フォルダを選択してください",
                InitialDirectory = PdfPathTextBox.Text
            };

            if (dialog.ShowDialog() == true)
            {
                PdfPathTextBox.Text = dialog.FolderName;
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(PdfPathTextBox.Text))
            {
                MessageBox.Show("フォルダを指定してください。",
                    "入力エラー", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            SettingsService.Save(new AppSettings { PdfSavePath = PdfPathTextBox.Text });
            MessageBox.Show("設定を保存しました。",
                "保存完了", MessageBoxButton.OK, MessageBoxImage.Information);
            DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
