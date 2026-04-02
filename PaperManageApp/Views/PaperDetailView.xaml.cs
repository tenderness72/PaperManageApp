using PaperManagementApp.Models;
using PaperManagementApp.Services;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;

namespace PaperManagementApp.Views
{
    public partial class PaperDetailView : Page
    {
        private PaperService _paperService;
        private WordService _wordService;
        private Paper _currentPaper;
        private bool _isDataDirty = false;

        public PaperDetailView(int paperId)
        {
            InitializeComponent();

            _paperService = new PaperService();
            _wordService = new WordService();

            Loaded += async (s, e) =>
            {
                if (NavigationService != null)
                {
                    NavigationService.Navigating += NavigationService_Navigating;
                }
                await LoadPaperAsync(paperId);
            };
        }

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

                // 基本情報の表示
                TitleTextBlock.Text = _currentPaper.Title;
                AuthorsTextBlock.Text = _currentPaper.Authors;

                // 出版情報
                JournalRun.Text = _currentPaper.Journal;
                YearRun.Text = _currentPaper.Year.ToString();
                VolumeRun.Text = _currentPaper.Volume;
                PagesRun.Text = _currentPaper.Pages;
                DoiRun.Text = _currentPaper.DOI;

                // 論文情報
                PaperTypeRun.Text = _currentPaper.PaperType;
                ClinicalAreaRun.Text = _currentPaper.ClinicalArea;
                ApproachRun.Text = _currentPaper.Approach;
                KeywordsRun.Text = _currentPaper.Keywords;

                // 引用情報
                InTextCitationTextBox.Text = _currentPaper.GetInTextCitation();
                FullCitationTextBox.Text = _currentPaper.GetFullCitation();

                // 論文内容
                AbstractTextBox.Text = _currentPaper.Abstract;
                AdditionalNotesTextBox.Text = _currentPaper.AdditionalNotes;

                // PDFボタンの表示制御
                OpenPdfButton.IsEnabled = !string.IsNullOrEmpty(_currentPaper.FilePath) && File.Exists(_currentPaper.FilePath);
                if (!OpenPdfButton.IsEnabled && !string.IsNullOrEmpty(_currentPaper.FilePath))
                {
                    OpenPdfButton.Content = "PDFファイルなし";
                }
                else if (OpenPdfButton.IsEnabled)
                {
                    OpenPdfButton.Content = "PDFを開く";
                }
                else
                {
                    OpenPdfButton.Content = "PDFファイルなし";
                }

                // データの変更フラグをリセット
                _isDataDirty = false;
                SaveButton.IsEnabled = false;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"論文の読み込み中にエラーが発生しました: {ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SectionTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            _isDataDirty = true;
            SaveButton.IsEnabled = true;
        }

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            await SavePaperAsync();
        }

        private async Task SavePaperAsync()
        {
            try
            {
                // 論文内容を保存
                _currentPaper.Abstract = AbstractTextBox.Text;
                _currentPaper.AdditionalNotes = AdditionalNotesTextBox.Text;

                // データベースを更新
                await _paperService.UpdatePaperAsync(_currentPaper);

                // 変更フラグをリセット
                _isDataDirty = false;
                SaveButton.IsEnabled = false;

                MessageBox.Show("変更を保存しました。", "保存完了", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存中にエラーが発生しました: {ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void EditButton_Click(object sender, RoutedEventArgs e)
        {
            // 編集前に変更があれば保存を促す
            if (_isDataDirty)
            {
                var result = MessageBox.Show("変更が保存されていません。保存しますか？", "確認",
                    MessageBoxButton.YesNoCancel, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    await SavePaperAsync();
                }
                else if (result == MessageBoxResult.Cancel)
                {
                    return; // 編集画面への遷移をキャンセル
                }
            }

            // 編集画面に遷移
            NavigationService.Navigate(new PaperEditView(_currentPaper.Id));
        }

        private async void BackButton_Click(object sender, RoutedEventArgs e)
        {
            // 戻る前に変更があれば保存を促す
            if (_isDataDirty)
            {
                var result = MessageBox.Show("変更が保存されていません。保存しますか？", "確認",
                    MessageBoxButton.YesNoCancel, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    await SavePaperAsync();
                }
                else if (result == MessageBoxResult.Cancel)
                {
                    return; // 戻るをキャンセル
                }
            }

            NavigationService.GoBack();
        }

        private void WordCitationButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                bool wordConnected = false;

                // Wordが起動しているかチェック
                if (_wordService.IsWordRunning())
                {
                    wordConnected = true;
                }
                else
                {
                    // Wordを起動
                    wordConnected = _wordService.StartWord();
                }

                if (!wordConnected)
                {
                    MessageBox.Show("Wordとの接続に失敗しました。Wordがインストールされていることを確認してください。",
                        "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // アクティブな文書を取得
                if (!_wordService.GetActiveDocument())
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
                    if (_wordService.InsertInTextCitation(_currentPaper))
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
                    if (_wordService.InsertFullCitation(_currentPaper))
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
            finally
            {
                // Word連携後のクリーンアップ
                _wordService.Cleanup();
            }
        }

        private void CopyCitationButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 引用のコピー方法を選択
                var result = MessageBox.Show(
                    "どの形式の引用をコピーしますか？\n\n「はい」：本文引用 (例: 牧村(2006))\n「いいえ」：文献リスト用 (例: 牧村 雅(2006). 論文タイトル...)",
                    "引用のコピー", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    // 本文引用
                    Clipboard.SetText(InTextCitationTextBox.Text);
                    MessageBox.Show("本文引用をクリップボードにコピーしました。", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else if (result == MessageBoxResult.No)
                {
                    // 完全な引用情報
                    Clipboard.SetText(FullCitationTextBox.Text);
                    MessageBox.Show("文献リスト用引用をクリップボードにコピーしました。", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"クリップボードへのコピー中にエラーが発生しました: {ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // NavigatingCancelEventArgsを処理するイベントハンドラー
        // ナビゲーションのキャンセルが必要なため同期で動作する
        private async void NavigationService_Navigating(object sender, NavigatingCancelEventArgs e)
        {
            // 未保存の変更があれば確認
            if (_isDataDirty)
            {
                var result = MessageBox.Show("変更が保存されていません。保存しますか？", "確認",
                    MessageBoxButton.YesNoCancel, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    // ナビゲーションをいったんキャンセルし、保存完了後に戻る
                    e.Cancel = true;
                    NavigationService.Navigating -= NavigationService_Navigating;
                    _isDataDirty = false;
                    await SavePaperAsync();
                    NavigationService?.GoBack();
                    return;
                }
                else if (result == MessageBoxResult.Cancel)
                {
                    e.Cancel = true;
                    return;
                }
            }

            // イベントの登録解除（メモリリーク防止）
            if (NavigationService != null)
            {
                NavigationService.Navigating -= NavigationService_Navigating;
            }
        }

        private void OpenPdfButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_currentPaper == null)
                {
                    MessageBox.Show("論文情報が読み込まれていません。", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (string.IsNullOrEmpty(_currentPaper.FilePath))
                {
                    MessageBox.Show("この論文にはPDFファイルが関連付けられていません。", "情報", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                if (!File.Exists(_currentPaper.FilePath))
                {
                    var result = MessageBox.Show(
                        $"PDFファイルが見つかりませんでした。\n\nパス: {_currentPaper.FilePath}\n\n論文データからファイルパスを削除しますか？",
                        "ファイルが見つかりません",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning
                    );

                    if (result == MessageBoxResult.Yes)
                    {
                        _currentPaper.FilePath = null;
                        // fire-and-forget で更新
                        _ = _paperService.UpdatePaperAsync(_currentPaper);
                        MessageBox.Show("ファイルパスを削除しました。", "削除完了", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    return;
                }

                // PDFファイルを既定のアプリケーションで開く
                Process.Start(new ProcessStartInfo
                {
                    FileName = _currentPaper.FilePath,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"PDFファイルを開く際にエラーが発生しました: {ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
