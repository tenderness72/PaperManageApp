using PaperManagementApp.Models;
using PaperManagementApp.Services;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace PaperManagementApp.Views
{
    public partial class PaperListView : Page
    {
        private PaperService _paperService;
        private List<Paper> _allPapers;
        private List<Paper> _displayedPapers;
        private bool _showFavoritesOnly;
        private string _currentSearchQuery = "";
        private int? _selectedYear = null;
        private string _selectedJournal = null;
        private string _selectedClinicalArea = null;

        // コンストラクタ（標準）
        public PaperListView()
        {
            InitializeComponent();

            _paperService = new PaperService();
            _showFavoritesOnly = false;

            LoadPapers();
        }

        // コンストラクタ（お気に入りのみ表示）
        public PaperListView(bool favoritesOnly)
        {
            InitializeComponent();

            _paperService = new PaperService();
            _showFavoritesOnly = favoritesOnly;

            LoadPapers();
        }

        // 論文データの読み込み
        private void LoadPapers()
        {
            try
            {
                if (_showFavoritesOnly)
                {
                    _allPapers = _paperService.GetFavoritePapers();
                }
                else
                {
                    _allPapers = _paperService.GetAllPapers();
                }

                _displayedPapers = new List<Paper>(_allPapers);
                PapersDataGrid.ItemsSource = _displayedPapers;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"論文データの読み込み中にエラーが発生しました: {ex.Message}",
                    "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // 検索クエリの更新
        public void UpdateSearch(string searchQuery)
        {
            _currentSearchQuery = searchQuery;
            ApplyFiltersAndSearch();
        }

        // フィルターの適用
        public void ApplyFilter(int? year, string journal, string clinicalArea)
        {
            _selectedYear = year;
            _selectedJournal = journal;
            _selectedClinicalArea = clinicalArea;

            ApplyFiltersAndSearch();
        }

        // 検索とフィルターの適用
        private void ApplyFiltersAndSearch()
        {
            try
            {
                // 基本リストの取得（お気に入りのみまたは全て）
                if (_showFavoritesOnly)
                {
                    _allPapers = _paperService.GetFavoritePapers();
                }
                else
                {
                    _allPapers = _paperService.GetAllPapers();
                }

                // 検索条件があれば絞り込み
                if (!string.IsNullOrWhiteSpace(_currentSearchQuery))
                {
                    var searchResults = _paperService.SearchPapers(_currentSearchQuery);

                    // お気に入りのみモードなら、検索結果からお気に入りだけを抽出
                    if (_showFavoritesOnly)
                    {
                        var favoriteSearchResults = new List<Paper>();
                        foreach (var paper in searchResults)
                        {
                            if (paper.IsFavorite)
                            {
                                favoriteSearchResults.Add(paper);
                            }
                        }
                        _allPapers = favoriteSearchResults;
                    }
                    else
                    {
                        _allPapers = searchResults;
                    }
                }

                // フィルターの適用
                _displayedPapers = new List<Paper>();

                foreach (var paper in _allPapers)
                {
                    bool matchesYear = _selectedYear == null || paper.Year == _selectedYear;
                    bool matchesJournal = string.IsNullOrEmpty(_selectedJournal) || paper.Journal.Contains(_selectedJournal);
                    bool matchesClinicalArea = string.IsNullOrEmpty(_selectedClinicalArea) || paper.ClinicalArea.Contains(_selectedClinicalArea);

                    if (matchesYear && matchesJournal && matchesClinicalArea)
                    {
                        _displayedPapers.Add(paper);
                    }
                }

                PapersDataGrid.ItemsSource = null;
                PapersDataGrid.ItemsSource = _displayedPapers;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"検索/フィルター適用中にエラーが発生しました: {ex.Message}",
                    "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // 新規追加ボタンクリック
        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            NavigationService.Navigate(new PaperEditView());
        }

        // 編集ボタンクリック
        private void EditButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button != null && button.Tag != null)
            {
                int paperId = Convert.ToInt32(button.Tag);
                NavigationService.Navigate(new PaperEditView(paperId));
            }
        }

        // 削除ボタンクリック
        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button != null && button.Tag != null)
            {
                int paperId = Convert.ToInt32(button.Tag);
                DeletePaper(paperId);
            }
        }

        // 削除処理
        private void DeletePaper(int paperId)
        {
            var result = MessageBox.Show("この論文を削除してもよろしいですか？\nこの操作は取り消せません。",
                "削除の確認", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    bool success = _paperService.DeletePaper(paperId);

                    if (success)
                    {
                        MessageBox.Show("論文を削除しました。", "削除完了", MessageBoxButton.OK, MessageBoxImage.Information);

                        // リストを更新
                        LoadPapers();
                        ApplyFiltersAndSearch();
                    }
                    else
                    {
                        MessageBox.Show("論文の削除に失敗しました。", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"削除中にエラーが発生しました: {ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        // お気に入りチェックボックス変更
        private void FavoriteCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            var checkbox = sender as CheckBox;
            if (checkbox != null && checkbox.Tag != null)
            {
                int paperId = Convert.ToInt32(checkbox.Tag);
                UpdateFavoriteStatus(paperId, true);
            }
        }

        private void FavoriteCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            var checkbox = sender as CheckBox;
            if (checkbox != null && checkbox.Tag != null)
            {
                int paperId = Convert.ToInt32(checkbox.Tag);
                UpdateFavoriteStatus(paperId, false);
            }
        }

        // お気に入り状態の更新
        private void UpdateFavoriteStatus(int paperId, bool isFavorite)
        {
            try
            {
                var paper = _paperService.GetPaperById(paperId);

                if (paper != null)
                {
                    paper.IsFavorite = isFavorite;
                    _paperService.UpdatePaper(paper);

                    // お気に入りのみ表示モードでお気に入りを解除した場合はリストから削除
                    if (_showFavoritesOnly && !isFavorite)
                    {
                        LoadPapers();
                        ApplyFiltersAndSearch();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"お気に入り状態の更新中にエラーが発生しました: {ex.Message}",
                    "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // データグリッド選択変更
        private void PapersDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // コンテキストメニュー用に選択状態を保持
        }

        // データグリッドダブルクリック
        private void PapersDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var selectedPaper = PapersDataGrid.SelectedItem as Paper;

            if (selectedPaper != null)
            {
                NavigationService.Navigate(new PaperDetailView(selectedPaper.Id));
            }
        }

        // コンテキストメニュー - 詳細表示
        private void ViewDetailMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var selectedPaper = PapersDataGrid.SelectedItem as Paper;

            if (selectedPaper != null)
            {
                NavigationService.Navigate(new PaperDetailView(selectedPaper.Id));
            }
        }

        // コンテキストメニュー - 編集
        private void EditMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var selectedPaper = PapersDataGrid.SelectedItem as Paper;

            if (selectedPaper != null)
            {
                NavigationService.Navigate(new PaperEditView(selectedPaper.Id));
            }
        }

        // コンテキストメニュー - 削除
        private void DeleteMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var selectedPaper = PapersDataGrid.SelectedItem as Paper;

            if (selectedPaper != null)
            {
                DeletePaper(selectedPaper.Id);
            }
        }

        // コンテキストメニュー - Wordに引用を挿入
        private void InsertCitationMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var selectedPaper = PapersDataGrid.SelectedItem as Paper;

            if (selectedPaper != null)
            {
                try
                {
                    var wordService = new WordService();

                    bool wordConnected = false;

                    // Wordが起動しているかチェック
                    if (wordService.IsWordRunning())
                    {
                        wordConnected = true;
                    }
                    else
                    {
                        // Wordを起動
                        wordConnected = wordService.StartWord();
                    }

                    if (!wordConnected)
                    {
                        MessageBox.Show("Wordとの接続に失敗しました。Wordがインストールされていることを確認してください。",
                            "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    // アクティブな文書を取得
                    if (!wordService.GetActiveDocument())
                    {
                        MessageBox.Show("Word文書の取得に失敗しました。", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    // 引用の挿入方法を選択
                    var result = MessageBox.Show(
                        "どの形式で引用を挿入しますか？\n\n「はい」：本文引用 (例: 牧村(2006))\n「いいえ」：文献リスト用 (例: 牧村 雅(2006). 論文タイトル...)",
                        "引用の挿入", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);

                    if (result == MessageBoxResult.Yes)
                    {
                        // 本文引用
                        if (wordService.InsertInTextCitation(selectedPaper))
                        {
                            MessageBox.Show("引用を挿入しました。", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                        else
                        {
                            MessageBox.Show("引用の挿入に失敗しました。", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                    else if (result == MessageBoxResult.No)
                    {
                        // 完全な引用情報
                        if (wordService.InsertFullCitation(selectedPaper))
                        {
                            MessageBox.Show("引用を挿入しました。", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                        else
                        {
                            MessageBox.Show("引用の挿入に失敗しました。", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Word連携中にエラーが発生しました: {ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}