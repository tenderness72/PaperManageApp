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

        // Wordが起動しているかチェック - 修正版
        public bool IsWordRunning()
        {
            try
            {
                // システムプロセスからWordが実行中かチェック
                Process[] processes = Process.GetProcessesByName("WINWORD");
                if (processes.Length > 0)
                {
                    try
                    {
                        // 既存のWORDプロセスが見つかったので、新しいインスタンスを作成
                        _wordApp = new Word.Application();
                        _wordApp.Visible = true;
                        return true;
                    }
                    catch
                    {
                        return false;
                    }
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        // Wordを起動
        public bool StartWord()
        {
            try
            {
                var assembly = typeof(Microsoft.Office.Interop.Word.Application).Assembly;
                MessageBox.Show($"Word Interop Assembly: {assembly.FullName}");
                _wordApp = new Word.Application();
                _wordApp.Visible = true;

                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Wordの起動に失敗しました: {ex.Message}\n\n{ex.StackTrace}");
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
                Console.WriteLine($"Word文書の取得に失敗しました: {ex.Message}");
                return false;
            }
        }

        // 文中引用（例：牧村(2006)）を挿入
        public bool InsertInTextCitation(Paper paper)
        {
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
                Word.Selection selection = _wordApp.Selection;
                selection.TypeText(paper.GetInTextCitation());

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"引用の挿入に失敗しました: {ex.Message}");
                return false;
            }
        }

        // 参考文献リストに完全な引用情報を追加
        public bool InsertFullCitation(Paper paper)
        {
            try
            {
                if (_wordApp == null || _currentDocument == null)
                {
                    if (!GetActiveDocument())
                    {
                        return false;
                    }
                }

                // カーソル位置に完全な引用情報を挿入
                Word.Selection selection = _wordApp.Selection;
                selection.TypeText(paper.GetFullCitation());
                selection.TypeParagraph(); // 改行を挿入

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"引用の挿入に失敗しました: {ex.Message}");
                return false;
            }
        }

        // 参考文献リストを生成して挿入
        public bool InsertReferenceList(List<Paper> papers)
        {
            try
            {
                if (_wordApp == null || _currentDocument == null)
                {
                    if (!GetActiveDocument())
                    {
                        return false;
                    }
                }

                Word.Selection selection = _wordApp.Selection;

                // 見出しを挿入
                selection.Font.Bold = 1;
                selection.TypeText("引用文献");
                selection.TypeParagraph();
                selection.Font.Bold = 0;

                // 論文リストを著者名でソート
                papers.Sort((a, b) => string.Compare(a.Authors, b.Authors));

                // すべての論文の引用情報を挿入
                foreach (var paper in papers)
                {
                    selection.TypeText(paper.GetFullCitation());
                    selection.TypeParagraph();
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"参考文献リストの挿入に失敗しました: {ex.Message}");
                return false;
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
                Console.WriteLine($"クリーンアップに失敗しました: {ex.Message}");
            }
        }
    }
}