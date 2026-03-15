using Microsoft.Win32;
using PaperManagementApp.Models;
using PaperManagementApp.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Navigation;

namespace PaperManagementApp.Views
{
    public partial class PaperEditView : Page
    {
        private PaperService _paperService;
        private RisImportService _risImportService;
        private DoiMetadataService _doiMetadataService;
        private JStageMetadataService _jStageMetadataService;
        private OpenAlexMetadataService _openAlexMetadataService;
        private PdfMetadataExtractionService _pdfMetadataExtractionService;
        private Paper _currentPaper;
        private bool _isEditMode = false;
        private string _pdfFilePath = null;

        // 新規モードかどうかを示すプロパティ（ボタン表示切替用）
        public bool IsNewMode
        {
            get { return !_isEditMode; }
        }

        // コンストラクタ（新規追加モード）
        public PaperEditView()
        {
            InitializeComponent();

            _paperService = new PaperService();
            _risImportService = new RisImportService();
            _doiMetadataService = new DoiMetadataService();
            _jStageMetadataService = new JStageMetadataService();
            _openAlexMetadataService = new OpenAlexMetadataService();
            _pdfMetadataExtractionService = new PdfMetadataExtractionService();
            _currentPaper = new Paper();
            _isEditMode = false;

            HeaderTextBlock.Text = "新規論文の追加";
            PaperTypeComboBox.SelectedIndex = 0; // デフォルトで「研究論文」を選択

            // データコンテキストを設定（ボタン表示のための）
            this.DataContext = this;

            // RISインポートボタンを表示
            ImportRisButton.Visibility = Visibility.Visible;
        }

        // コンストラクタ（編集モード）
        public PaperEditView(int paperId)
        {
            InitializeComponent();

            _paperService = new PaperService();
            _risImportService = new RisImportService();
            _doiMetadataService = new DoiMetadataService();
            _jStageMetadataService = new JStageMetadataService();
            _openAlexMetadataService = new OpenAlexMetadataService();
            _pdfMetadataExtractionService = new PdfMetadataExtractionService();
            _isEditMode = true;

            HeaderTextBlock.Text = "論文の編集";

            // データコンテキストを設定（ボタン表示のための）
            this.DataContext = this;

            // 編集モードではRISインポートボタンを非表示
            ImportRisButton.Visibility = Visibility.Collapsed;

            Loaded += async (s, e) => await LoadPaperAsync(paperId);
        }

        // コンストラクタ（PDFドロップモード）
        public PaperEditView(string pdfFilePath)
        {
            InitializeComponent();

            _paperService = new PaperService();
            _risImportService = new RisImportService();
            _doiMetadataService = new DoiMetadataService();
            _jStageMetadataService = new JStageMetadataService();
            _openAlexMetadataService = new OpenAlexMetadataService();
            _pdfMetadataExtractionService = new PdfMetadataExtractionService();
            _currentPaper = new Paper();
            _isEditMode = false;
            _pdfFilePath = pdfFilePath;

            HeaderTextBlock.Text = "新規論文の追加";
            PaperTypeComboBox.SelectedIndex = 0;

            this.DataContext = this;
            ImportRisButton.Visibility = Visibility.Visible;

            Loaded += async (s, e) =>
            {
                PdfPathTextBox.Text = _pdfFilePath;
                await TryAutoFillFromPdfAsync(showNotFoundMessage: false, showSuccessMessage: false);
            };
        }

        // コンストラクタ（RISインポートモード）
        public PaperEditView(Paper importedPaper)
        {
            InitializeComponent();

            _paperService = new PaperService();
            _risImportService = new RisImportService();
            _doiMetadataService = new DoiMetadataService();
            _jStageMetadataService = new JStageMetadataService();
            _openAlexMetadataService = new OpenAlexMetadataService();
            _pdfMetadataExtractionService = new PdfMetadataExtractionService();
            _currentPaper = importedPaper;
            _isEditMode = false;

            HeaderTextBlock.Text = "RISから論文を追加";

            // データコンテキストを設定（ボタン表示のための）
            this.DataContext = this;

            // RISインポートボタンを非表示（既にインポート済みのため）
            ImportRisButton.Visibility = Visibility.Collapsed;

            // フォームに値を設定
            Loaded += (s, e) => PopulateFormFromPaper(importedPaper);
        }

        // RISインポートボタンクリック
        private void ImportRisButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var openFileDialog = new OpenFileDialog
                {
                    Filter = "RISファイル (*.ris)|*.ris|すべてのファイル (*.*)|*.*",
                    Title = "RISファイルを選択"
                };

                if (openFileDialog.ShowDialog() == true)
                {
                    // RISファイルを読み込み
                    List<Paper> importedPapers = _risImportService.ImportFromRisFile(openFileDialog.FileName);

                    if (importedPapers.Count == 0)
                    {
                        MessageBox.Show("RISファイルから論文情報を読み込めませんでした。",
                            "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    // 複数の論文がある場合は選択ダイアログを表示
                    if (importedPapers.Count > 1)
                    {
                        // 論文選択ダイアログを表示
                        var selectedPaper = ShowPaperSelectionDialog(importedPapers);
                        if (selectedPaper != null)
                        {
                            // 現在の画面に選択した論文情報を表示
                            PopulateFormFromPaper(selectedPaper);
                        }
                    }
                    else
                    {
                        // 1件のみの場合は直接表示
                        PopulateFormFromPaper(importedPapers[0]);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"RISファイルの読み込み中にエラーが発生しました: {ex.Message}",
                    "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ShowLoading(string message)
        {
            LoadingStatusText.Text = message;
            LoadingOverlay.Visibility = Visibility.Visible;
        }

        private void HideLoading()
        {
            LoadingOverlay.Visibility = Visibility.Collapsed;
        }

        // DOI取得ボタンクリック
        private async void FetchDoiButton_Click(object sender, RoutedEventArgs e)
        {
            string doi = DoiTextBox.Text?.Trim() ?? string.Empty;

            try
            {
                FetchDoiButton.IsEnabled = false;
                FetchDoiButton.Content = "取得中...";

                int? inputYear = int.TryParse(YearTextBox.Text?.Trim(), out var parsedYear) ? parsedYear : null;
                string titleText = TitleTextBox.Text ?? string.Empty;
                string authorsText = AuthorsTextBox.Text ?? string.Empty;
                string journalText = JournalTextBox.Text ?? string.Empty;

                Paper? fetchedPaper;
                if (!string.IsNullOrWhiteSpace(doi))
                {
                    // 日本語論文に対応するため J-STAGE を最初に試みる
                    ShowLoading("J-STAGEを検索中...");
                    fetchedPaper = await _jStageMetadataService.FetchPaperByDoiAsync(doi);

                    if (fetchedPaper == null)
                    {
                        ShowLoading("CrossRefを検索中...");
                        fetchedPaper = await _doiMetadataService.FetchPaperByDoiAsync(doi);
                    }

                    if (fetchedPaper == null)
                    {
                        ShowLoading("OpenAlexを検索中...");
                        fetchedPaper = await _openAlexMetadataService.FetchPaperByDoiAsync(doi);
                    }
                }
                else
                {
                    ShowLoading("CrossRefを検索中...");
                    fetchedPaper = await _doiMetadataService.SearchPaperByMetadataAsync(
                        titleText, authorsText, inputYear, journalText);
                }

                if (fetchedPaper == null)
                {
                    ShowLoading("J-STAGEを検索中...");
                    fetchedPaper = await _jStageMetadataService.SearchAsync(
                        titleText, authorsText, inputYear, journalText);
                }

                if (fetchedPaper == null)
                {
                    ShowLoading("OpenAlexを検索中...");
                    fetchedPaper = await _openAlexMetadataService.SearchAsync(
                        titleText, authorsText, inputYear, journalText);
                }

                if (fetchedPaper == null)
                {
                    MessageBox.Show("DOIを特定できませんでした。DOIを直接入力するか、タイトル・著者・年・雑誌名を補って再試行してください。",
                        "取得失敗", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                bool hasExistingInput = !string.IsNullOrWhiteSpace(TitleTextBox.Text) ||
                                        !string.IsNullOrWhiteSpace(AuthorsTextBox.Text) ||
                                        !string.IsNullOrWhiteSpace(JournalTextBox.Text) ||
                                        !string.IsNullOrWhiteSpace(AbstractTextBox.Text) ||
                                        !string.IsNullOrWhiteSpace(KeywordsTextBox.Text);

                if (hasExistingInput)
                {
                    var overwrite = MessageBox.Show(
                        "現在入力中のタイトル・著者・雑誌名などを取得結果で上書きしますか？",
                        "上書き確認",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (overwrite != MessageBoxResult.Yes)
                    {
                        return;
                    }
                }

                ApplyFetchedMetadataToForm(fetchedPaper);
                MessageBox.Show("DOIとメタデータを取得しました。必要に応じて内容を確認してください。",
                    "取得完了", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"DOI取得中にエラーが発生しました: {ex.Message}",
                    "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                HideLoading();
                FetchDoiButton.IsEnabled = true;
                FetchDoiButton.Content = "DOIから取得";
            }
        }

        private void ApplyFetchedMetadataToForm(Paper fetchedPaper)
        {
            DoiTextBox.Text = DoiMetadataService.NormalizeDoi(fetchedPaper.DOI);

            if (!string.IsNullOrWhiteSpace(fetchedPaper.Title))
            {
                TitleTextBox.Text = fetchedPaper.Title;
            }

            if (!string.IsNullOrWhiteSpace(fetchedPaper.Authors))
            {
                AuthorsTextBox.Text = fetchedPaper.Authors;
            }

            if (fetchedPaper.Year > 0)
            {
                YearTextBox.Text = fetchedPaper.Year.ToString();
            }

            if (!string.IsNullOrWhiteSpace(fetchedPaper.Journal))
            {
                JournalTextBox.Text = fetchedPaper.Journal;
            }

            if (!string.IsNullOrWhiteSpace(fetchedPaper.Volume))
            {
                VolumeTextBox.Text = fetchedPaper.Volume;
            }

            if (!string.IsNullOrWhiteSpace(fetchedPaper.Issue))
            {
                IssueTextBox.Text = fetchedPaper.Issue;
            }

            if (!string.IsNullOrWhiteSpace(fetchedPaper.Pages))
            {
                PagesTextBox.Text = fetchedPaper.Pages;
            }

            if (!string.IsNullOrWhiteSpace(fetchedPaper.Keywords))
            {
                KeywordsTextBox.Text = fetchedPaper.Keywords;
            }

            if (!string.IsNullOrWhiteSpace(fetchedPaper.Abstract))
            {
                AbstractTextBox.Text = fetchedPaper.Abstract;
            }

            SelectPaperType(fetchedPaper.PaperType);
        }

        private void SelectPaperType(string? paperType)
        {
            if (string.IsNullOrWhiteSpace(paperType))
            {
                return;
            }

            for (int i = 0; i < PaperTypeComboBox.Items.Count; i++)
            {
                if (PaperTypeComboBox.Items[i] is ComboBoxItem item &&
                    string.Equals(item.Content?.ToString(), paperType, StringComparison.Ordinal))
                {
                    PaperTypeComboBox.SelectedIndex = i;
                    return;
                }
            }
        }

        private async void AutoFetchFromPdfButton_Click(object sender, RoutedEventArgs e)
        {
            await TryAutoFillFromPdfAsync(showNotFoundMessage: true);
        }

        private async Task TryAutoFillFromPdfAsync(bool showNotFoundMessage, bool showSuccessMessage = true)
        {
            if (string.IsNullOrWhiteSpace(_pdfFilePath) || !File.Exists(_pdfFilePath))
            {
                MessageBox.Show("先にPDFファイルを選択してください。", "情報", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                AutoFetchFromPdfButton.IsEnabled = false;
                AutoFetchFromPdfButton.Content = "取得中...";

                Paper? fetchedPaper = null;

                ShowLoading("PDFからDOIを抽出中...");
                string? extractedDoi = _pdfMetadataExtractionService.TryExtractDoiFromPdf(_pdfFilePath);

                if (!string.IsNullOrWhiteSpace(extractedDoi))
                {
                    // 日本語論文に対応するため J-STAGE を最初に試みる
                    ShowLoading("J-STAGEを検索中...");
                    fetchedPaper = await _jStageMetadataService.FetchPaperByDoiAsync(extractedDoi);

                    if (fetchedPaper == null)
                    {
                        ShowLoading("CrossRefを検索中...");
                        fetchedPaper = await _doiMetadataService.FetchPaperByDoiAsync(extractedDoi);
                    }

                    if (fetchedPaper == null)
                    {
                        ShowLoading("OpenAlexを検索中...");
                        fetchedPaper = await _openAlexMetadataService.FetchPaperByDoiAsync(extractedDoi);
                    }
                }

                if (fetchedPaper == null)
                {
                    int? inputYear = int.TryParse(YearTextBox.Text?.Trim(), out var parsedYear) ? parsedYear : null;
                    string authorsHint = AuthorsTextBox.Text ?? string.Empty;
                    string journalHint = JournalTextBox.Text ?? string.Empty;

                    var titleCandidates = new List<string>();
                    if (!string.IsNullOrWhiteSpace(TitleTextBox.Text))
                        titleCandidates.Add(TitleTextBox.Text);
                    ShowLoading("PDFからタイトルを抽出中...");
                    string? pdfTitle = _pdfMetadataExtractionService.TryExtractTitleFromPdf(_pdfFilePath);
                    if (!string.IsNullOrWhiteSpace(pdfTitle))
                        titleCandidates.Add(pdfTitle);
                    string? fileTitle = _pdfMetadataExtractionService.TryExtractTitleFromFileName(_pdfFilePath);
                    if (!string.IsNullOrWhiteSpace(fileTitle))
                        titleCandidates.Add(fileTitle);

                    foreach (string titleHint in titleCandidates)
                    {
                        ShowLoading("CrossRefを検索中...");
                        fetchedPaper = await _doiMetadataService.SearchPaperByMetadataAsync(
                            titleHint, authorsHint, inputYear, journalHint);

                        if (fetchedPaper == null)
                        {
                            ShowLoading("J-STAGEを検索中...");
                            fetchedPaper = await _jStageMetadataService.SearchAsync(
                                titleHint, authorsHint, inputYear, journalHint);
                        }

                        if (fetchedPaper == null)
                        {
                            ShowLoading("OpenAlexを検索中...");
                            fetchedPaper = await _openAlexMetadataService.SearchAsync(
                                titleHint, authorsHint, inputYear, journalHint);
                        }

                        if (fetchedPaper != null) break;
                    }
                }

                if (fetchedPaper == null)
                {
                    if (showNotFoundMessage)
                    {
                        MessageBox.Show("PDFから DOI/メタデータを特定できませんでした。DOIの手入力または項目補足後に再実行してください。",
                            "取得失敗", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                    return;
                }

                ApplyFetchedMetadataToForm(fetchedPaper);
                if (showSuccessMessage)
                {
                    MessageBox.Show("PDFから DOI とメタデータを取得しました。", "取得完了", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"PDF自動取得中にエラーが発生しました: {ex.Message}",
                    "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                HideLoading();
                AutoFetchFromPdfButton.IsEnabled = true;
                AutoFetchFromPdfButton.Content = "PDFから自動取得";
            }
        }

        private Paper ShowPaperSelectionDialog(List<Paper> papers)
        {
            // 論文選択ダイアログを表示
            var dialog = new RisImportSelectDialog(papers);
            dialog.Owner = Application.Current.MainWindow;
            if (dialog.ShowDialog() == true)
            {
                return dialog.SelectedPaper;
            }

            return null;
        }

        // 論文データの読み込み
        private async Task LoadPaperAsync(int paperId)
        {
            try
            {
                _currentPaper = await _paperService.GetPaperByIdAsync(paperId);

                if (_currentPaper == null)
                {
                    MessageBox.Show("論文が見つかりませんでした。", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                    NavigationService.GoBack();
                    return;
                }

                // フォームに値を設定
                PopulateFormFromPaper(_currentPaper);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"論文の読み込み中にエラーが発生しました: {ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Paperオブジェクトからフォームに値を設定
        private void PopulateFormFromPaper(Paper paper)
        {
            TitleTextBox.Text = paper.Title;
            AuthorsTextBox.Text = paper.Authors;
            AuthorsKanaTextBox.Text = paper.AuthorsKana;
            YearTextBox.Text = paper.Year.ToString();
            JournalTextBox.Text = paper.Journal;
            VolumeTextBox.Text = paper.Volume;
            IssueTextBox.Text = paper.Issue;
            PagesTextBox.Text = paper.Pages;
            DoiTextBox.Text = paper.DOI;
            FavoriteCheckBox.IsChecked = paper.IsFavorite;

            // PDFファイルパス
            if (!string.IsNullOrEmpty(paper.FilePath))
            {
                PdfPathTextBox.Text = paper.FilePath;
                _pdfFilePath = paper.FilePath;
            }

            // 論文タイプの選択
            for (int i = 0; i < PaperTypeComboBox.Items.Count; i++)
            {
                var item = PaperTypeComboBox.Items[i] as ComboBoxItem;
                if (item != null && item.Content.ToString() == paper.PaperType)
                {
                    PaperTypeComboBox.SelectedIndex = i;
                    break;
                }
            }

            if (PaperTypeComboBox.SelectedIndex < 0)
            {
                PaperTypeComboBox.SelectedIndex = 0; // デフォルト選択
            }

            ClinicalAreaTextBox.Text = paper.ClinicalArea;
            ApproachTextBox.Text = paper.Approach;
            KeywordsTextBox.Text = paper.Keywords;
            TagsWrapPanel.Children.Clear();
            TagsWrapPanel.Children.Add(TagInputTextBox);
            foreach (var tag in paper.TagArray.Where(t => !string.IsNullOrEmpty(t)))
                AddTagChip(tag);

            // 論文セクションの内容
            AbstractTextBox.Text = paper.Abstract;
            ProblemAndPurposeTextBox.Text = paper.ProblemAndPurpose;
            MethodTextBox.Text = paper.Method;
            ResultsTextBox.Text = paper.Results;
            DiscussionTextBox.Text = paper.Discussion;
            AdditionalNotesTextBox.Text = paper.AdditionalNotes;
        }

        // PDFファイル参照ボタンクリック
        private async void BrowsePdfButton_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "PDFファイル (*.pdf)|*.pdf",
                Title = "PDFファイルを選択"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                _pdfFilePath = openFileDialog.FileName;
                PdfPathTextBox.Text = _pdfFilePath;
                await TryAutoFillFromPdfAsync(showNotFoundMessage: false, showSuccessMessage: false);
            }
        }

        // キャンセルボタンクリック
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            NavigationService.GoBack();
        }

        // 保存ボタンクリック
        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (ValidateForm())
            {
                await SavePaperAsync();
            }
        }

        // フォームの検証
        private bool ValidateForm()
        {
            // 必須項目の確認
            if (string.IsNullOrWhiteSpace(TitleTextBox.Text))
            {
                MessageBox.Show("タイトルを入力してください。", "入力エラー", MessageBoxButton.OK, MessageBoxImage.Warning);
                TitleTextBox.Focus();
                return false;
            }

            if (string.IsNullOrWhiteSpace(AuthorsTextBox.Text))
            {
                MessageBox.Show("著者を入力してください。", "入力エラー", MessageBoxButton.OK, MessageBoxImage.Warning);
                AuthorsTextBox.Focus();
                return false;
            }

            if (string.IsNullOrWhiteSpace(YearTextBox.Text) || !int.TryParse(YearTextBox.Text, out int year))
            {
                MessageBox.Show("有効な出版年を入力してください。", "入力エラー", MessageBoxButton.OK, MessageBoxImage.Warning);
                YearTextBox.Focus();
                return false;
            }

            if (string.IsNullOrWhiteSpace(JournalTextBox.Text))
            {
                MessageBox.Show("雑誌名を入力してください。", "入力エラー", MessageBoxButton.OK, MessageBoxImage.Warning);
                JournalTextBox.Focus();
                return false;
            }

            return true;
        }

        // 論文データの保存
        private async Task SavePaperAsync()
        {
            try
            {
                // 現在の論文データを更新
                _currentPaper.Title = TitleTextBox.Text.Trim();
                _currentPaper.Authors = AuthorsTextBox.Text.Trim();
                _currentPaper.AuthorsKana = string.IsNullOrWhiteSpace(AuthorsKanaTextBox.Text)
                    ? null : AuthorsKanaTextBox.Text.Trim();
                _currentPaper.Year = int.Parse(YearTextBox.Text.Trim());
                _currentPaper.Journal = JournalTextBox.Text.Trim();
                _currentPaper.Volume = VolumeTextBox.Text.Trim();
                _currentPaper.Issue = IssueTextBox.Text.Trim();
                _currentPaper.Pages = PagesTextBox.Text.Trim();
                _currentPaper.DOI = DoiTextBox.Text.Trim();
                _currentPaper.IsFavorite = FavoriteCheckBox.IsChecked ?? false;

                // PDFファイルの処理
                if (!string.IsNullOrEmpty(_pdfFilePath))
                {
                    // 設定からPDF保存先フォルダを取得
                    string appDataPath = Services.SettingsService.Load().PdfSavePath;

                    // フォルダが存在しない場合は作成
                    if (!Directory.Exists(appDataPath))
                    {
                        Directory.CreateDirectory(appDataPath);
                    }

                    // ファイル名（論文IDまたは一意のファイル名）
                    string fileName = _isEditMode ?
                        $"paper_{_currentPaper.Id}.pdf" :
                        $"paper_{DateTime.Now.Ticks}.pdf";

                    string destPath = Path.Combine(appDataPath, fileName);

                    // ファイルが既存のものと異なる場合のみコピー
                    if (_pdfFilePath != _currentPaper.FilePath)
                    {
                        // 既存のファイルがあれば削除
                        if (!string.IsNullOrEmpty(_currentPaper.FilePath) && File.Exists(_currentPaper.FilePath))
                        {
                            try
                            {
                                File.Delete(_currentPaper.FilePath);
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"既存PDFファイルの削除に失敗しました: {ex.Message}");
                                // エラーが発生しても処理は続行
                            }
                        }

                        // 新しいファイルをコピー
                        File.Copy(_pdfFilePath, destPath, true);
                        _currentPaper.FilePath = destPath;
                    }
                }
                else
                {
                    // PDFファイルがない場合は空の文字列を設定（NOT NULL制約対策）
                    _currentPaper.FilePath = "";
                }

                // 分類情報
                _currentPaper.PaperType = (PaperTypeComboBox.SelectedItem as ComboBoxItem)?.Content.ToString();
                _currentPaper.ClinicalArea = ClinicalAreaTextBox.Text.Trim();
                _currentPaper.Approach = ApproachTextBox.Text.Trim();
                _currentPaper.Keywords = KeywordsTextBox.Text.Trim();
                string uncommitted = TagInputTextBox.Text.Trim();
                if (!string.IsNullOrEmpty(uncommitted)) AddTagChip(uncommitted);
                _currentPaper.Tags = string.Join(",", GetCurrentTags());

                // 論文セクション
                _currentPaper.Abstract = AbstractTextBox.Text.Trim();
                _currentPaper.ProblemAndPurpose = ProblemAndPurposeTextBox.Text.Trim();
                _currentPaper.Method = MethodTextBox.Text.Trim();
                _currentPaper.Results = ResultsTextBox.Text.Trim();
                _currentPaper.Discussion = DiscussionTextBox.Text.Trim();
                _currentPaper.AdditionalNotes = AdditionalNotesTextBox.Text.Trim();

                // Notesコレクションを一時的にnullに設定（問題解決のためのテスト）
                _currentPaper.Notes = null;

                // データベースに保存
                string saveMessage = _isEditMode ? "論文情報を更新しました。" : "新しい論文を追加しました。";
                MessageBoxImage saveIcon = MessageBoxImage.Information;

                if (_currentPaper.HasJapaneseAuthors && string.IsNullOrWhiteSpace(_currentPaper.AuthorsKana))
                {
                    saveMessage += "\n\n💡 著者名に日本語が含まれています。\n" +
                                   "「著者よみがな」を入力すると参考文献リストを\n" +
                                   "正確に五十音順でソートできます。";
                    saveIcon = MessageBoxImage.Information;
                }

                if (_isEditMode)
                    await _paperService.UpdatePaperAsync(_currentPaper);
                else
                    await _paperService.AddPaperAsync(_currentPaper);

                MessageBox.Show(saveMessage, "保存完了", MessageBoxButton.OK, saveIcon);

                // 一覧画面に戻る
                NavigationService.GoBack();
            }
            catch (Microsoft.Data.Sqlite.SqliteException sqlEx)
            {
                // SQLite固有のエラー情報を表示
                MessageBox.Show($"データベースエラー: {sqlEx.Message}\nエラーコード: {sqlEx.SqliteErrorCode}",
                    "SQLiteエラー", MessageBoxButton.OK, MessageBoxImage.Error);

                // デバッグ用にコンソールにも出力
                Console.WriteLine($"SQLiteエラー: {sqlEx.Message}, コード: {sqlEx.SqliteErrorCode}");
            }
            catch (Exception ex)
            {
                // 内部例外の詳細を表示
                string innerMessage = ex.InnerException != null ?
                    $"内部例外: {ex.InnerException.Message}" : "詳細なエラー情報はありません";

                MessageBox.Show($"保存中にエラーが発生しました:\n{ex.Message}\n\n{innerMessage}",
                    "エラー", MessageBoxButton.OK, MessageBoxImage.Error);

                // デバッグ用にコンソールにも出力
                Console.WriteLine($"一般エラー: {ex.Message}");
                Console.WriteLine($"内部例外: {ex.InnerException?.Message}");
            }
        }

        // ---- タグチップ入力 ----

        // Enterキーでタグを確定
        private void TagInputTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                string tag = TagInputTextBox.Text.Trim();
                if (!string.IsNullOrEmpty(tag))
                {
                    AddTagChip(tag);
                    TagInputTextBox.Clear();
                }
                e.Handled = true;
            }
        }

        // タグチップを WrapPanel に追加
        private void AddTagChip(string tag)
        {
            if (GetCurrentTags().Contains(tag)) return;

            var chip = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0x34, 0x98, 0xdb)),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(8, 3, 4, 3),
                Margin = new Thickness(2),
                Tag = tag
            };

            var sp = new StackPanel { Orientation = Orientation.Horizontal };
            sp.Children.Add(new TextBlock
            {
                Text = tag,
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center
            });

            var removeBtn = new Button
            {
                Content = "×",
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Foreground = Brushes.White,
                Padding = new Thickness(4, 0, 0, 0),
                Cursor = Cursors.Hand
            };
            removeBtn.Click += (s, e) => TagsWrapPanel.Children.Remove(chip);

            sp.Children.Add(removeBtn);
            chip.Child = sp;

            int inputIndex = TagsWrapPanel.Children.IndexOf(TagInputTextBox);
            TagsWrapPanel.Children.Insert(inputIndex, chip);
        }

        // 現在のチップからタグ一覧を取得
        private List<string> GetCurrentTags()
        {
            return TagsWrapPanel.Children
                .OfType<Border>()
                .Select(b => b.Tag?.ToString() ?? string.Empty)
                .Where(t => !string.IsNullOrEmpty(t))
                .ToList();
        }
    }
}
