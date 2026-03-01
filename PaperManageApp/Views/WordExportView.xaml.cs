using PaperManagementApp.Models;
using PaperManagementApp.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace PaperManagementApp.Views
{
    public partial class WordExportView : Page
    {
        private PaperService _paperService;
        private WordService _wordService;
        private List<Paper> _allPapers;
        private List<Paper> _displayedPapers;
        private bool _isWordConnected = false;

        public WordExportView()
        {
            InitializeComponent();

            _paperService = new PaperService();
            _wordService = new WordService();

            CheckWordStatus();
            this.Unloaded += WordExportView_Unloaded;

            Loaded += async (s, e) => await LoadPapersAsync();
        }

        // ページがアンロードされるときのイベントハンドラー
        private void WordExportView_Unloaded(object sender, RoutedEventArgs e)
        {
            // Word接続の解除
            if (_isWordConnected)
            {
                _wordService.Cleanup();
            }
        }

        // 論文データの読み込み
        private async Task LoadPapersAsync()
        {
            try
            {
                _allPapers = await _paperService.GetAllPapersAsync();
                _displayedPapers = new List<Paper>(_allPapers);
                PapersDataGrid.ItemsSource = _displayedPapers;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"論文データの読み込み中にエラーが発生しました: {ex.Message}",
                    "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Wordの状態チェック
        private void CheckWordStatus()
        {
            try
            {
                if (_wordService.IsWordRunning())
                {
                    _isWordConnected = true;
                    WordStatusTextBlock.Text = "Word接続中";
                    ConnectWordButton.Content = "接続解除";
                }
                else
                {
                    _isWordConnected = false;
                    WordStatusTextBlock.Text = "Word未接続";
                    ConnectWordButton.Content = "Wordに接続";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Word状態の確認中にエラーが発生しました: {ex.Message}",
                    "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Wordに接続/切断
        private void ConnectWordButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_isWordConnected)
                {
                    // 切断
                    _wordService.Cleanup();
                    _isWordConnected = false;
                    WordStatusTextBlock.Text = "Word未接続";
                    ConnectWordButton.Content = "Wordに接続";
                }
                else
                {
                    // 接続
                    if (_wordService.StartWord())
                    {
                        _isWordConnected = true;
                        WordStatusTextBlock.Text = "Word接続中";
                        ConnectWordButton.Content = "接続解除";
                    }
                    else
                    {
                        MessageBox.Show("Wordの起動に失敗しました。Wordがインストールされていることを確認してください。",
                            "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Word接続の切り替え中にエラーが発生しました: {ex.Message}",
                    "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // 検索テキスト変更
        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            // 入力中は検索しない（検索ボタンクリック時に実行）
        }

        // 検索ボタンクリック
        private async void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            string searchQuery = SearchTextBox.Text.Trim();

            try
            {
                if (string.IsNullOrWhiteSpace(searchQuery))
                {
                    // 検索クエリが空の場合は全ての論文を表示
                    _displayedPapers = new List<Paper>(_allPapers);
                }
                else
                {
                    // 検索実行
                    _displayedPapers = await _paperService.SearchPapersAsync(searchQuery);
                }

                PapersDataGrid.ItemsSource = null;
                PapersDataGrid.ItemsSource = _displayedPapers;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"検索中にエラーが発生しました: {ex.Message}",
                    "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // 本文引用（例：牧村(2006)）の挿入
        private async void InsertInTextCitationButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button != null && button.Tag != null)
            {
                int paperId = Convert.ToInt32(button.Tag);
                await InsertInTextCitationAsync(paperId);
            }
        }

        // 文献リスト用引用の挿入
        private async void InsertFullCitationButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button != null && button.Tag != null)
            {
                int paperId = Convert.ToInt32(button.Tag);
                await InsertFullCitationAsync(paperId);
            }
        }

        // 本文引用の挿入処理
        private async Task InsertInTextCitationAsync(int paperId)
        {
            try
            {
                if (!EnsureWordConnection())
                {
                    return;
                }

                var paper = await _paperService.GetPaperByIdAsync(paperId);

                if (paper == null)
                {
                    MessageBox.Show("論文が見つかりませんでした。", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (_wordService.InsertInTextCitation(paper))
                {
                    MessageBox.Show($"引用「{paper.GetInTextCitation()}」を挿入しました。",
                        "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("引用の挿入に失敗しました。", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Word連携中にエラーが発生しました: {ex.Message}",
                    "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // 文献リスト用引用の挿入処理
        private async Task InsertFullCitationAsync(int paperId)
        {
            try
            {
                if (!EnsureWordConnection())
                {
                    return;
                }

                var paper = await _paperService.GetPaperByIdAsync(paperId);

                if (paper == null)
                {
                    MessageBox.Show("論文が見つかりませんでした。", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (_wordService.InsertFullCitation(paper))
                {
                    MessageBox.Show("引用を挿入しました。", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("引用の挿入に失敗しました。", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Word連携中にエラーが発生しました: {ex.Message}",
                    "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // すべての論文を使った参考文献リストの生成
        private void InsertAllReferencesButton_Click(object sender, RoutedEventArgs e)
        {
            InsertReferencesList(_allPapers);
        }

        // 選択した論文のみでの参考文献リストの生成
        private void InsertSelectedReferencesButton_Click(object sender, RoutedEventArgs e)
        {
            InsertReferencesList(_displayedPapers);
        }

        // 参考文献リストの挿入処理
        private void InsertReferencesList(List<Paper> papers)
        {
            try
            {
                if (!EnsureWordConnection())
                {
                    return;
                }

                if (papers == null || papers.Count == 0)
                {
                    MessageBox.Show("参考文献リストに含める論文がありません。",
                        "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 著者名でソート
                var sortedPapers = papers.OrderBy(p => p.Authors).ToList();

                if (_wordService.InsertReferenceList(sortedPapers))
                {
                    MessageBox.Show($"{sortedPapers.Count}件の論文を含む参考文献リストを挿入しました。",
                        "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("参考文献リストの挿入に失敗しました。",
                        "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Word連携中にエラーが発生しました: {ex.Message}",
                    "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Word接続の確認
        private bool EnsureWordConnection()
        {
            if (_isWordConnected)
            {
                // 既に接続済み
                return _wordService.GetActiveDocument();
            }
            else
            {
                // Wordを起動して接続
                if (_wordService.StartWord() && _wordService.GetActiveDocument())
                {
                    _isWordConnected = true;
                    WordStatusTextBlock.Text = "Word接続中";
                    ConnectWordButton.Content = "接続解除";
                    return true;
                }
                else
                {
                    MessageBox.Show("Wordとの接続に失敗しました。Wordがインストールされていることを確認してください。",
                        "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                }
            }
        }
    }
}
