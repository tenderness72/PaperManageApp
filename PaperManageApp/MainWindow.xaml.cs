using PaperManagementApp.Models;
using PaperManagementApp.Services;
using PaperManagementApp.Views;
using System;
using System.Windows;
using System.Windows.Controls;

namespace PaperManagementApp
{
    public partial class MainWindow : Window
    {
        private PaperService _paperService;
        private string _currentSearchQuery = "";

        public MainWindow()
        {
            InitializeComponent();

            _paperService = new PaperService();

            // 初期画面として論文一覧を表示
            MainFrame.Navigate(new PaperListView());

            // フィルター選択肢の読み込み（非同期）
            Loaded += async (s, e) => await LoadFilterOptionsAsync();
        }

        private async System.Threading.Tasks.Task LoadFilterOptionsAsync()
        {
            try
            {
                // 年のフィルター選択肢を読み込み
                YearFilterComboBox.Items.Clear();
                YearFilterComboBox.Items.Add(new ComboBoxItem { Content = "すべての年" });
                foreach (var year in await _paperService.GetYearsListAsync())
                {
                    YearFilterComboBox.Items.Add(new ComboBoxItem { Content = year.ToString() });
                }
                YearFilterComboBox.SelectedIndex = 0;

                // ジャーナルのフィルター選択肢を読み込み
                JournalFilterComboBox.Items.Clear();
                JournalFilterComboBox.Items.Add(new ComboBoxItem { Content = "すべてのジャーナル" });
                foreach (var journal in await _paperService.GetJournalsListAsync())
                {
                    JournalFilterComboBox.Items.Add(new ComboBoxItem { Content = journal });
                }
                JournalFilterComboBox.SelectedIndex = 0;

                // 臨床領域のフィルター選択肢を読み込み
                ClinicalAreaFilterComboBox.Items.Clear();
                ClinicalAreaFilterComboBox.Items.Add(new ComboBoxItem { Content = "すべての臨床領域" });
                foreach (var area in await _paperService.GetClinicalAreasListAsync())
                {
                    ClinicalAreaFilterComboBox.Items.Add(new ComboBoxItem { Content = area });
                }
                ClinicalAreaFilterComboBox.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"フィルター選択肢の読み込み中にエラーが発生しました: {ex.Message}",
                    "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // すべての論文ボタンクリック
        private void AllPapersButton_Click(object sender, RoutedEventArgs e)
        {
            MainFrame.Navigate(new PaperListView());

            // 検索ボックスをクリア
            SearchTextBox.Text = "";
            _currentSearchQuery = "";

            // フィルターをリセット
            YearFilterComboBox.SelectedIndex = 0;
            JournalFilterComboBox.SelectedIndex = 0;
            ClinicalAreaFilterComboBox.SelectedIndex = 0;
        }

        // お気に入りボタンクリック
        private void FavoritesButton_Click(object sender, RoutedEventArgs e)
        {
            // 検索ボックスをクリア
            SearchTextBox.Text = "";
            _currentSearchQuery = "";

            // フィルターをリセット
            YearFilterComboBox.SelectedIndex = 0;
            JournalFilterComboBox.SelectedIndex = 0;
            ClinicalAreaFilterComboBox.SelectedIndex = 0;

            // お気に入り一覧を表示
            MainFrame.Navigate(new PaperListView(true));
        }

        // 新しい論文追加ボタンクリック
        private void AddPaperButton_Click(object sender, RoutedEventArgs e)
        {
            MainFrame.Navigate(new PaperEditView());
        }

        // Word連携ボタンクリック
        private void WordIntegrationButton_Click(object sender, RoutedEventArgs e)
        {
            MainFrame.Navigate(new WordExportView());
        }

        // 設定ボタンクリック
        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Views.SettingsDialog { Owner = this };
            dialog.ShowDialog();
        }

        // 検索テキスト変更
        private async void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            _currentSearchQuery = SearchTextBox.Text;

            // 検索クエリが変わったらリストビューに通知
            if (MainFrame.Content is PaperListView listView)
            {
                await listView.UpdateSearch(_currentSearchQuery);
            }
        }

        // フィルターコンボボックス選択変更
        private async void FilterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (MainFrame.Content is PaperListView listView)
            {
                // 選択された年
                int? selectedYear = null;
                if (YearFilterComboBox.SelectedIndex > 0)
                {
                    var yearItem = YearFilterComboBox.SelectedItem as ComboBoxItem;
                    if (yearItem != null && int.TryParse(yearItem.Content.ToString(), out int year))
                    {
                        selectedYear = year;
                    }
                }

                // 選択されたジャーナル
                string? selectedJournal = null;
                if (JournalFilterComboBox.SelectedIndex > 0)
                {
                    var journalItem = JournalFilterComboBox.SelectedItem as ComboBoxItem;
                    if (journalItem != null)
                    {
                        selectedJournal = journalItem.Content.ToString();
                    }
                }

                // 選択された臨床領域
                string? selectedClinicalArea = null;
                if (ClinicalAreaFilterComboBox.SelectedIndex > 0)
                {
                    var areaItem = ClinicalAreaFilterComboBox.SelectedItem as ComboBoxItem;
                    if (areaItem != null)
                    {
                        selectedClinicalArea = areaItem.Content.ToString();
                    }
                }

                // フィルターを適用
                await listView.ApplyFilter(selectedYear, selectedJournal, selectedClinicalArea);
            }
        }
    }
}
