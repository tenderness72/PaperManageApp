using PaperManagementApp.Models;
using PaperManagementApp.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace PaperManagementApp.Views
{
    public partial class PaperListView : Page
    {
        private Storyboard _pulseStoryboard;

        private PaperService _paperService;
        private List<Paper> _allPapers;
        private List<Paper> _displayedPapers;
        private bool _showFavoritesOnly;
        private string _currentSearchQuery = "";
        private int? _selectedYear = null;
        private string _selectedJournal = null;
        private string _selectedClinicalArea = null;
        private string _selectedTag = null;

        // コンストラクタ（標準）
        public PaperListView()
        {
            InitializeComponent();
            _paperService = new PaperService();
            _showFavoritesOnly = false;
            Loaded += async (s, e) => { await LoadPapersAsync(); InitPulseAnimation(); };
        }

        // コンストラクタ（お気に入りのみ表示）
        public PaperListView(bool favoritesOnly)
        {
            InitializeComponent();
            _paperService = new PaperService();
            _showFavoritesOnly = favoritesOnly;

            Loaded += async (s, e) => await LoadPapersAsync();
        }

        // ドラッグオーバーレイのパルスアニメーションを初期化
        private void InitPulseAnimation()
        {
            _pulseStoryboard = new Storyboard
            {
                RepeatBehavior = RepeatBehavior.Forever,
                AutoReverse = true
            };

            void AddAnim(PropertyPath path, double from, double to)
            {
                var anim = new DoubleAnimation(from, to, TimeSpan.FromSeconds(0.6));
                Storyboard.SetTarget(anim, OverlayIcon);
                Storyboard.SetTargetProperty(anim, path);
                _pulseStoryboard.Children.Add(anim);
            }

            AddAnim(new PropertyPath(UIElement.OpacityProperty), 0.75, 1.0);
            AddAnim(new PropertyPath("RenderTransform.ScaleX"), 1.0, 1.08);
            AddAnim(new PropertyPath("RenderTransform.ScaleY"), 1.0, 1.08);
        }

        // 論文データの読み込み
        private async Task LoadPapersAsync()
        {
            try
            {
                if (_showFavoritesOnly)
                {
                    _allPapers = await _paperService.GetFavoritePapersAsync();
                }
                else
                {
                    _allPapers = await _paperService.GetAllPapersAsync();
                }

                _displayedPapers = new List<Paper>(_allPapers);
                PapersDataGrid.ItemsSource = _displayedPapers;
                UpdateSelectionUI();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"論文データの読み込み中にエラーが発生しました: {ex.Message}",
                    "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // 検索クエリの更新（MainWindow から呼ばれる）
        public async Task UpdateSearch(string searchQuery)
        {
            _currentSearchQuery = searchQuery;
            await ApplyFiltersAndSearchAsync();
        }

        // フィルターの適用（MainWindow から呼ばれる）
        public async Task ApplyFilter(int? year, string journal, string clinicalArea, string tag = null)
        {
            _selectedYear = year;
            _selectedJournal = journal;
            _selectedClinicalArea = clinicalArea;
            _selectedTag = tag;

            await ApplyFiltersAndSearchAsync();
        }

        // 検索とフィルターの適用
        private async Task ApplyFiltersAndSearchAsync()
        {
            try
            {
                // 基本リストの取得（お気に入りのみまたは全て）
                if (_showFavoritesOnly)
                {
                    _allPapers = await _paperService.GetFavoritePapersAsync();
                }
                else
                {
                    _allPapers = await _paperService.GetAllPapersAsync();
                }

                // 検索条件があれば絞り込み
                if (!string.IsNullOrWhiteSpace(_currentSearchQuery))
                {
                    var searchResults = await _paperService.SearchPapersAsync(_currentSearchQuery);

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

                // フィルターの適用（in-memory）
                _displayedPapers = new List<Paper>();

                foreach (var paper in _allPapers)
                {
                    bool matchesYear = _selectedYear == null || paper.Year == _selectedYear;
                    bool matchesJournal = string.IsNullOrEmpty(_selectedJournal) || paper.Journal.Contains(_selectedJournal);
                    bool matchesClinicalArea = string.IsNullOrEmpty(_selectedClinicalArea) || paper.ClinicalArea?.Contains(_selectedClinicalArea) == true;
                    bool matchesTag = string.IsNullOrEmpty(_selectedTag) ||
                                      paper.TagArray.Any(t => t.Trim() == _selectedTag);

                    if (matchesYear && matchesJournal && matchesClinicalArea && matchesTag)
                    {
                        _displayedPapers.Add(paper);
                    }
                }

                PapersDataGrid.ItemsSource = null;
                PapersDataGrid.ItemsSource = _displayedPapers;
                UpdateSelectionUI();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"検索/フィルター適用中にエラーが発生しました: {ex.Message}",
                    "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // PDFドラッグ＆ドロップ — ルートGrid用（オーバーレイ表示）
        private static bool IsPdfDrag(DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return false;
            var files = e.Data.GetData(DataFormats.FileDrop) as string[];
            return files != null && files.Any(f => f.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase));
        }

        private void RootGrid_DragEnter(object sender, DragEventArgs e)
        {
            if (IsPdfDrag(e))
            {
                DragOverlay.Visibility = Visibility.Visible;
                _pulseStoryboard?.Begin();
                e.Effects = DragDropEffects.Copy;
                e.Handled = true;
            }
        }

        private void RootGrid_DragOver(object sender, DragEventArgs e)
        {
            if (IsPdfDrag(e))
            {
                e.Effects = DragDropEffects.Copy;
                e.Handled = true;
            }
            else
            {
                e.Effects = DragDropEffects.None;
                e.Handled = true;
            }
        }

        private void RootGrid_DragLeave(object sender, DragEventArgs e)
        {
            // ウィンドウ外に出たときだけ非表示（子要素への移動は無視）
            var pos = e.GetPosition(DragOverlay);
            if (pos.X < 0 || pos.Y < 0 ||
                pos.X > DragOverlay.ActualWidth || pos.Y > DragOverlay.ActualHeight)
            {
                DragOverlay.Visibility = Visibility.Collapsed;
                _pulseStoryboard?.Stop();
            }
        }

        private void RootGrid_Drop(object sender, DragEventArgs e)
        {
            DragOverlay.Visibility = Visibility.Collapsed;
            _pulseStoryboard?.Stop();

            if (!IsPdfDrag(e)) return;

            var files = (e.Data.GetData(DataFormats.FileDrop) as string[])!
                .Where(f => f.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) && File.Exists(f))
                .ToArray();

            if (files.Length == 0) return;

            if (files.Length > 1)
                MessageBox.Show($"{files.Length} 件のPDFがドロップされました。最初のファイルのみ処理します。",
                    "情報", MessageBoxButton.OK, MessageBoxImage.Information);

            NavigationService.Navigate(new PaperEditView(files[0]));
        }

        // DataGrid側のハンドラーは削除（ルートGridに統合）
        private void PapersDataGrid_DragOver(object sender, DragEventArgs e) { }

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

        // ---- 選択チェックボックス・一括削除 ----

        // 行の選択チェックボックスが変更されたとき
        private void SelectionCheckBox_Click(object sender, RoutedEventArgs e)
        {
            UpdateSelectionUI();
        }

        // 一括操作バーの「すべて選択」チェックボックス
        private void SelectAllCheckBox_Click(object sender, RoutedEventArgs e)
        {
            bool select = SelectAllCheckBox.IsChecked == true;
            if (_displayedPapers == null) return;
            foreach (var p in _displayedPapers)
                p.IsSelected = select;
            PapersDataGrid.Items.Refresh();
            UpdateSelectionUI();
        }

        // 選択状態をUIに反映（カウント・ボタン有効化・SelectAll状態）
        private void UpdateSelectionUI()
        {
            if (_displayedPapers == null) return;
            int count = _displayedPapers.Count(p => p.IsSelected);
            SelectionCountText.Text = $"{count}件選択中";
            BulkDeleteButton.IsEnabled = count > 0;
            BulkDeleteButton.Content = count > 0 ? $"削除（{count}件）" : "削除";
            BulkFavoriteButton.IsEnabled = count > 0;
            BulkFavoriteButton.Content = count > 0 ? $"お気に入り切替（{count}件）" : "お気に入り切替";

            bool allSelected  = count == _displayedPapers.Count && count > 0;
            bool noneSelected = count == 0;
            SelectAllCheckBox.IsChecked = allSelected ? true : noneSelected ? false : null;
        }

        // 一括削除ボタン
        private async void BulkDeleteButton_Click(object sender, RoutedEventArgs e)
        {
            var targets = _displayedPapers?.Where(p => p.IsSelected).ToList();
            if (targets == null || targets.Count == 0) return;

            var result = MessageBox.Show(
                $"{targets.Count}件の論文を削除してもよろしいですか？\nこの操作は取り消せません。",
                "一括削除の確認", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                int deleted = await _paperService.DeletePapersAsync(targets.Select(p => p.Id));
                MessageBox.Show($"{deleted}件の論文を削除しました。", "削除完了",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                await LoadPapersAsync();
                await ApplyFiltersAndSearchAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"削除中にエラーが発生しました: {ex.Message}", "エラー",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // 削除ボタンクリック
        private async void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button != null && button.Tag != null)
            {
                int paperId = Convert.ToInt32(button.Tag);
                await DeletePaperAsync(paperId);
            }
        }

        // 削除処理
        private async Task DeletePaperAsync(int paperId)
        {
            var result = MessageBox.Show("この論文を削除してもよろしいですか？\nこの操作は取り消せません。",
                "削除の確認", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    bool success = await _paperService.DeletePaperAsync(paperId);

                    if (success)
                    {
                        MessageBox.Show("論文を削除しました。", "削除完了", MessageBoxButton.OK, MessageBoxImage.Information);

                        // リストを更新
                        await LoadPapersAsync();
                        await ApplyFiltersAndSearchAsync();
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

        // ★ボタン：個別お気に入り切替（楽観的更新）
        private async void StarButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button?.Tag == null) return;
            int paperId = Convert.ToInt32(button.Tag);

            var paper = _allPapers?.FirstOrDefault(p => p.Id == paperId);
            if (paper == null) return;

            // ① 新しい値を確定してからUIとDBに同じ値を書く（二重トグル防止）
            bool newValue = !paper.IsFavorite;
            paper.IsFavorite = newValue;
            PapersDataGrid.Items.Refresh();

            bool needsReload = _showFavoritesOnly && !newValue;

            try
            {
                await _paperService.SetFavoriteAsync(paperId, newValue);
                if (needsReload)
                {
                    await LoadPapersAsync();
                    await ApplyFiltersAndSearchAsync();
                }
            }
            catch (Exception ex)
            {
                // 失敗したらロールバック
                paper.IsFavorite = !newValue;
                PapersDataGrid.Items.Refresh();
                MessageBox.Show($"お気に入り状態の更新中にエラーが発生しました: {ex.Message}",
                    "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // 一括お気に入り切替ボタン
        private async void BulkFavoriteButton_Click(object sender, RoutedEventArgs e)
        {
            var targets = _displayedPapers?.Where(p => p.IsSelected).ToList();
            if (targets == null || targets.Count == 0) return;

            // 選択中の過半数がお気に入りなら解除、そうでなければ追加
            bool newValue = targets.Count(p => p.IsFavorite) < targets.Count / 2.0 + 1;
            foreach (var p in targets) p.IsFavorite = newValue;
            PapersDataGrid.Items.Refresh();

            try
            {
                await _paperService.SetFavoritesAsync(targets.Select(p => p.Id), newValue);
                if (_showFavoritesOnly)
                {
                    await LoadPapersAsync();
                    await ApplyFiltersAndSearchAsync();
                }
            }
            catch (Exception ex)
            {
                foreach (var p in targets) p.IsFavorite = !newValue;
                PapersDataGrid.Items.Refresh();
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
        private async void DeleteMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var selectedPaper = PapersDataGrid.SelectedItem as Paper;

            if (selectedPaper != null)
            {
                await DeletePaperAsync(selectedPaper.Id);
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
