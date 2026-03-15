using Microsoft.Office.Interop.Word;
using PaperManagementApp.Models;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Diagnostics;
using Word = Microsoft.Office.Interop.Word;
using System.Windows;

namespace PaperManagementApp.Services
{
    public class WordService
    {
        private Word.Application _wordApp;
        private Word.Document _currentDocument;

        // .NET 8 では Marshal.GetActiveObject が削除されたため P/Invoke で代替
        // GetActiveObject(oleaut32) は CLSID を要求するため、先に ProgID → CLSID 変換が必要
        [DllImport("ole32.dll")]
        private static extern int CLSIDFromProgID(
            [MarshalAs(UnmanagedType.LPWStr)] string lpszProgID,
            out Guid lpclsid);

        [DllImport("oleaut32.dll")]
        private static extern int GetActiveObject(
            ref Guid rclsid,
            IntPtr pvReserved,
            [MarshalAs(UnmanagedType.IUnknown)] out object ppunk);

        // 既存の Word インスタンスを ROT から取得（取得できなければ null）
        private static Word.Application? TryAttachToRunningWord()
        {
            try
            {
                if (CLSIDFromProgID("Word.Application", out Guid clsid) != 0) return null;
                if (GetActiveObject(ref clsid, IntPtr.Zero, out object obj) != 0) return null;
                return obj as Word.Application;
            }
            catch
            {
                return null;
            }
        }

        // Wordが起動中かチェックし、起動中なら既存プロセスにアタッチ
        public bool IsWordRunning()
        {
            try
            {
                Process[] processes = Process.GetProcessesByName("WINWORD");
                if (processes.Length == 0) return false;

                var app = TryAttachToRunningWord();
                if (app == null) return false;

                _wordApp = app;
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Wordプロセスの検出に失敗しました: {ex.Message}",
                    "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        // Wordに接続（既存プロセス優先、未起動なら新規起動）
        public bool StartWord()
        {
            try
            {
                // ① 既に起動中の Word にアタッチ
                var existing = TryAttachToRunningWord();
                if (existing != null)
                {
                    _wordApp = existing;
                    _wordApp.Visible = true;
                    return true;
                }

                // ② Word が起動していない場合は新規起動
                _wordApp = new Word.Application();
                _wordApp.Visible = true;
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Wordの起動に失敗しました: {ex.Message}",
                    "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        // 現在開いているWord文書を取得
        public bool GetActiveDocument()
        {
            try
            {
                if (_wordApp == null && !IsWordRunning() && !StartWord())
                {
                    return false;
                }

                if (_wordApp.Documents.Count > 0)
                {
                    _currentDocument = _wordApp.ActiveDocument;
                    return true;
                }
                else
                {
                    _currentDocument = _wordApp.Documents.Add();
                    return true;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Word文書の取得に失敗しました: {ex.Message}");
                return false;
            }
        }

        // 文中引用（例：牧村(2006)）を挿入
        public bool InsertInTextCitation(Paper paper)
        {
            Word.Selection? selection = null;
            try
            {
                if (_wordApp == null || _currentDocument == null)
                {
                    if (!GetActiveDocument())
                    {
                        return false;
                    }
                }

                // カーソル位置に引用を挿入
                selection = _wordApp.Selection;
                selection.TypeText(paper.GetInTextCitation());

                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"引用の挿入に失敗しました: {ex.Message}");
                return false;
            }
            finally
            {
                if (selection != null) Marshal.ReleaseComObject(selection);
            }
        }

        // 参考文献リストに完全な引用情報を追加
        public bool InsertFullCitation(Paper paper)
        {
            Word.Selection? selection = null;
            Word.Paragraph? para = null;
            try
            {
                if (_wordApp == null || _currentDocument == null)
                {
                    if (!GetActiveDocument())
                    {
                        return false;
                    }
                }

                selection = _wordApp.Selection;

                // 書式付きセグメントを順に挿入（誌名・巻数をイタリック体に: JPA 3.10.2/3.10.3）
                InsertCitationSegments(selection, paper);
                selection.Font.Italic = 0;
                selection.TypeParagraph();

                // ハンギングインデント: 2行目以降を全角2文字（≈21pt）字下げ (JPA 3.10.1(1))
                para = selection.Paragraphs.Last;
                para.LeftIndent = 21f;
                para.FirstLineIndent = -21f;

                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"引用の挿入に失敗しました: {ex.Message}");
                return false;
            }
            finally
            {
                if (para != null) Marshal.ReleaseComObject(para);
                if (selection != null) Marshal.ReleaseComObject(selection);
            }
        }

        // 参考文献リストを生成して挿入
        public bool InsertReferenceList(List<Paper> papers)
        {
            Word.Selection? selection = null;
            try
            {
                if (_wordApp == null || _currentDocument == null)
                {
                    if (!GetActiveDocument())
                    {
                        return false;
                    }
                }

                selection = _wordApp.Selection;

                // 見出しを挿入
                selection.Font.Bold = 1;
                selection.TypeText("引用文献");
                selection.TypeParagraph();
                selection.Font.Bold = 0;

                // すべての論文の引用情報を挿入（ソートは呼び出し元で実施済み）
                foreach (var paper in papers)
                {
                    // 書式付きセグメントを順に挿入（誌名・巻数をイタリック体に: JPA 3.10.2/3.10.3）
                    InsertCitationSegments(selection, paper);
                    selection.Font.Italic = 0;
                    selection.TypeParagraph();

                    // ハンギングインデント: 2行目以降を全角2文字（≈21pt）字下げ (JPA 3.10.1(1))
                    Word.Paragraph? para = null;
                    try
                    {
                        para = selection.Paragraphs.Last;
                        para.LeftIndent = 21f;
                        para.FirstLineIndent = -21f;
                    }
                    finally
                    {
                        if (para != null) Marshal.ReleaseComObject(para);
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"参考文献リストの挿入に失敗しました: {ex.Message}");
                return false;
            }
            finally
            {
                if (selection != null) Marshal.ReleaseComObject(selection);
            }
        }

        // 書式付きセグメントをWordに挿入（イタリック区間を適用）
        private void InsertCitationSegments(Word.Selection selection, Paper paper)
        {
            foreach (var (text, italic) in paper.GetCitationSegments())
            {
                selection.Font.Italic = italic ? 1 : 0;
                selection.TypeText(text);
            }
        }

        // クリーンアップ
        public void Cleanup()
        {
            try
            {
                if (_wordApp != null)
                {
                    Marshal.ReleaseComObject(_wordApp);
                    _wordApp = null;
                }

                if (_currentDocument != null)
                {
                    Marshal.ReleaseComObject(_currentDocument);
                    _currentDocument = null;
                }

                // GC実行
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"クリーンアップに失敗しました: {ex.Message}");
            }
        }
    }
}