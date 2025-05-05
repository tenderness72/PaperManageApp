using Microsoft.Win32;
using PaperManagementApp.Models;
using PaperManagementApp.Services;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;

namespace PaperManagementApp.Views
{
    public partial class RisImportView : Page
    {
        private RisImportService _risImportService;
        private List<Paper> _importedPapers;

        public RisImportView()
        {
            InitializeComponent();

            _risImportService = new RisImportService();
            _importedPapers = new List<Paper>();
        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "RISファイル (*.ris)|*.ris|すべてのファイル (*.*)|*.*",
                Title = "RISファイルを選択"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    FilePathTextBox.Text = openFileDialog.FileName;
                    LoadRisFile(openFileDialog.FileName);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"ファイルの読み込み中にエラーが発生しました: {ex.Message}",
                        "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void LoadRisFile(string filePath)
        {
            try
            {
                // RISファイルを読み込み
                _importedPapers = _risImportService.ImportFromRisFile(filePath);

                // DataGridに表示
                PapersDataGrid.ItemsSource = _importedPapers;

                // 読み込み結果を表示
                if (_importedPapers.Count > 0)
                {
                    MessageBox.Show($"{_importedPapers.Count}件の論文情報を読み込みました。",
                        "読み込み完了", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("有効な論文情報が見つかりませんでした。",
                        "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"RISファイルの解析中にエラーが発生しました: {ex.Message}",
                    "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ImportButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_importedPapers == null || _importedPapers.Count == 0)
                {
                    MessageBox.Show("インポートする論文がありません。",
                        "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 選択された論文のみインポート
                var selectedPapers = new List<Paper>();
                foreach (var item in PapersDataGrid.SelectedItems)
                {
                    if (item is Paper paper)
                    {
                        selectedPapers.Add(paper);
                    }
                }

                if (selectedPapers.Count == 0)
                {
                    // 選択されていない場合は全てインポート
                    selectedPapers = _importedPapers;
                }

                // 確認ダイアログを表示
                var result = MessageBox.Show($"{selectedPapers.Count}件の論文をインポートしますか？",
                    "確認", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    // データベースに保存
                    _risImportService.SaveImportedPapers(selectedPapers);

                    MessageBox.Show($"{selectedPapers.Count}件の論文を正常にインポートしました。",
                        "インポート完了", MessageBoxButton.OK, MessageBoxImage.Information);

                    // 一覧画面に戻る
                    NavigationService.GoBack();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"インポート中にエラーが発生しました: {ex.Message}",
                    "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            NavigationService.GoBack();
        }
    }
}