using Microsoft.Win32;
using PaperManagementApp.Models;
using PaperManagementApp.Services;
using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;

namespace PaperManagementApp.Views
{
    public partial class PaperEditView : Page
    {
        private PaperService _paperService;
        private Paper _currentPaper;
        private bool _isEditMode = false;
        private string _pdfFilePath = null;

        // コンストラクタ（新規追加モード）
        public PaperEditView()
        {
            InitializeComponent();

            _paperService = new PaperService();
            _currentPaper = new Paper();
            _isEditMode = false;

            HeaderTextBlock.Text = "新規論文の追加";
            PaperTypeComboBox.SelectedIndex = 0; // デフォルトで「研究論文」を選択
        }

        // コンストラクタ（編集モード）
        public PaperEditView(int paperId)
        {
            InitializeComponent();

            _paperService = new PaperService();
            _isEditMode = true;

            LoadPaper(paperId);

            HeaderTextBlock.Text = "論文の編集";
        }

        // 論文データの読み込み
        private void LoadPaper(int paperId)
        {
            try
            {
                _currentPaper = _paperService.GetPaperById(paperId);

                if (_currentPaper == null)
                {
                    MessageBox.Show("論文が見つかりませんでした。", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                    NavigationService.GoBack();
                    return;
                }

                // フォームに値を設定
                TitleTextBox.Text = _currentPaper.Title;
                AuthorsTextBox.Text = _currentPaper.Authors;
                YearTextBox.Text = _currentPaper.Year.ToString();
                JournalTextBox.Text = _currentPaper.Journal;
                VolumeTextBox.Text = _currentPaper.Volume;
                PagesTextBox.Text = _currentPaper.Pages;
                DoiTextBox.Text = _currentPaper.DOI;
                FavoriteCheckBox.IsChecked = _currentPaper.IsFavorite;

                // PDFファイルパス
                if (!string.IsNullOrEmpty(_currentPaper.FilePath))
                {
                    PdfPathTextBox.Text = _currentPaper.FilePath;
                    _pdfFilePath = _currentPaper.FilePath;
                }

                // 論文タイプの選択
                for (int i = 0; i < PaperTypeComboBox.Items.Count; i++)
                {
                    var item = PaperTypeComboBox.Items[i] as ComboBoxItem;
                    if (item != null && item.Content.ToString() == _currentPaper.PaperType)
                    {
                        PaperTypeComboBox.SelectedIndex = i;
                        break;
                    }
                }

                if (PaperTypeComboBox.SelectedIndex < 0)
                {
                    PaperTypeComboBox.SelectedIndex = 0; // デフォルト選択
                }

                ClinicalAreaTextBox.Text = _currentPaper.ClinicalArea;
                ApproachTextBox.Text = _currentPaper.Approach;
                KeywordsTextBox.Text = _currentPaper.Keywords;
                TagsTextBox.Text = _currentPaper.Tags;

                // 論文セクションの内容
                AbstractTextBox.Text = _currentPaper.Abstract;
                ProblemAndPurposeTextBox.Text = _currentPaper.ProblemAndPurpose;
                MethodTextBox.Text = _currentPaper.Method;
                ResultsTextBox.Text = _currentPaper.Results;
                DiscussionTextBox.Text = _currentPaper.Discussion;
                AdditionalNotesTextBox.Text = _currentPaper.AdditionalNotes;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"論文の読み込み中にエラーが発生しました: {ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // PDFファイル参照ボタンクリック
        private void BrowsePdfButton_Click(object sender, RoutedEventArgs e)
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
            }
        }

        // キャンセルボタンクリック
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            NavigationService.GoBack();
        }

        // 保存ボタンクリック
        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (ValidateForm())
            {
                SavePaper();
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
        private void SavePaper()
        {
            try
            {
                // 現在の論文データを更新
                _currentPaper.Title = TitleTextBox.Text.Trim();
                _currentPaper.Authors = AuthorsTextBox.Text.Trim();
                _currentPaper.Year = int.Parse(YearTextBox.Text.Trim());
                _currentPaper.Journal = JournalTextBox.Text.Trim();
                _currentPaper.Volume = VolumeTextBox.Text.Trim();
                _currentPaper.Pages = PagesTextBox.Text.Trim();
                _currentPaper.DOI = DoiTextBox.Text.Trim();
                _currentPaper.IsFavorite = FavoriteCheckBox.IsChecked ?? false;

                // PDFファイルの処理
                if (!string.IsNullOrEmpty(_pdfFilePath))
                {
                    // PDFファイルをアプリのデータフォルダにコピー（任意）
                    string appDataPath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        "PaperManagementApp",
                        "PDFs"
                    );

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
                _currentPaper.Tags = TagsTextBox.Text.Trim();

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
                if (_isEditMode)
                {
                    _paperService.UpdatePaper(_currentPaper);
                    MessageBox.Show("論文情報を更新しました。", "保存完了", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    _paperService.AddPaper(_currentPaper);
                    MessageBox.Show("新しい論文を追加しました。", "保存完了", MessageBoxButton.OK, MessageBoxImage.Information);
                }

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
    }
}