using PaperManagementApp.Models;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace PaperManagementApp.Views
{
    /// <summary>
    /// RisImportSelectDialog.xaml の相互作用ロジック
    /// </summary>
    public partial class RisImportSelectDialog : Window
    {
        // 選択された論文
        public Paper SelectedPaper { get; private set; }

        public RisImportSelectDialog(List<Paper> papers)
        {
            InitializeComponent();

            // 論文リストを設定
            PapersListView.ItemsSource = papers;

            // デフォルトで最初の項目を選択
            if (papers.Count > 0)
            {
                PapersListView.SelectedIndex = 0;
            }
        }

        // 選択ボタンクリック
        private void SelectButton_Click(object sender, RoutedEventArgs e)
        {
            if (PapersListView.SelectedItem is Paper selectedPaper)
            {
                SelectedPaper = selectedPaper;
                DialogResult = true;
            }
        }

        // リスト選択変更
        private void PapersListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // 論文が選択されている場合のみ選択ボタンを有効化
            SelectButton.IsEnabled = PapersListView.SelectedItem != null;
        }
    }
}