using Microsoft.Office.Interop.Word;
using PaperManagementApp.Models;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Diagnostics;
using Word = Microsoft.Office.Interop.Word;
using System.Windows;
using System.Reflection;
using System.IO;

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
                    catch (Exception ex)
                    {
                        MessageBox.Show($"既存のWord接続に失敗しました: {ex.Message}");
                        return false;
                    }
                }
                return false;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Wordプロセスの検出に失敗しました: {ex.Message}");
                return false;
            }
        }

        // Wordを起動（強化版）
        public bool StartWord()
        {
            try
            {
                // Office相互運用アセンブリの情報をログに出力
                try
                {
                    var wordAppType = typeof(Microsoft.Office.Interop.Word.Application);
                    var assembly = wordAppType.Assembly;
                    MessageBox.Show($"Word Interop Assembly: {assembly.FullName}");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Word Interopアセンブリの情報取得に失敗: {ex.Message}");
                }

                // 明示的な Late Binding の利用（PIA依存を減らす）
                try
                {
                    Type officeType = Type.GetTypeFromProgID("Word.Application");
                    if (officeType != null)
                    {
                        object wordObj = Activator.CreateInstance(officeType);
                        _wordApp = (Word.Application)wordObj;
                        _wordApp.Visible = true;
                        return true;
                    }
                    else
                    {
                        // 通常の方法でWordを起動
                        _wordApp = new Word.Application();
                        _wordApp.Visible = true;
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Late Bindingでの起動に失敗しました: {ex.Message}");

                    // 最後の手段：通常の方法でWordを起動
                    try
                    {
                        _wordApp = new Word.Application();
                        _wordApp.Visible = true;
                        return true;
                    }
                    catch (Exception ex2)
                    {
                        MessageBox.Show($"Wordの起動に失敗しました: {ex2.Message}\n\n{ex2.StackTrace}");
                        return false;
                    }
                }
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

                // カーソル位置に完全な引用情報を挿入
                selection = _wordApp.Selection;

                // 引用情報を取得
                string citation = paper.GetFullCitation();

                // 初回の行を挿入
                selection.TypeText(citation);
                selection.TypeParagraph(); // 改行

                // 論文情報が長い場合、2行目以降のインデントのためのフラグを設定
                if (citation.Length > 80)  // 80文字を超える場合は2行になると仮定
                {
                    // 段落設定を取得
                    para = selection.Paragraphs.Last;

                    // 1行目のハンギングインデントを設定（Windows APIのポイント単位）
                    // 全角スペース2つ分（約40ポイント）
                    para.FirstLineIndent = -40f;  // ハンギングインデント
                    para.LeftIndent = 40f;       // 左インデント
                }

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

                // 論文リストを著者名でソート
                papers.Sort((a, b) => string.Compare(a.Authors, b.Authors));

                // すべての論文の引用情報を挿入
                foreach (var paper in papers)
                {
                    // 引用情報を取得
                    string citation = paper.GetFullCitation();

                    // 初回の行を挿入
                    selection.TypeText(citation);
                    selection.TypeParagraph();

                    // 論文情報が長い場合、2行目以降のインデントのためのフラグを設定
                    if (citation.Length > 80)  // 80文字を超える場合は2行になると仮定
                    {
                        Word.Paragraph? para = null;
                        try
                        {
                            // 段落設定を取得
                            para = selection.Paragraphs.Last;

                            // ハンギングインデントを設定（Windows APIのポイント単位）
                            // 全角スペース2つ分（約40ポイント）
                            para.FirstLineIndent = -40f;  // ハンギングインデント
                            para.LeftIndent = 40f;       // 左インデント
                        }
                        finally
                        {
                            if (para != null) Marshal.ReleaseComObject(para);
                        }
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