using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace PaperManagementApp.Services
{
    public class PdfMetadataExtractionService
    {
        // 裸の DOI パターン (Crossref 推奨)
        private static readonly Regex DoiRegex = new Regex(
            @"10\.\d{4,9}/[-._;()/:A-Za-z0-9]+",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // doi.org URL パターン（ハイパーリンク・XMP に現れる、偽陽性ほぼゼロ）
        private static readonly Regex DoiUrlRegex = new Regex(
            @"https?://(?:dx\.)?doi\.org/(10\.\d{4,9}/[-._;()/:A-Za-z0-9]+)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // "DOI:" プレフィックス付き表記（本文中の明示的表記）
        private static readonly Regex DoiLabelRegex = new Regex(
            @"(?:doi|DOI)[:\s：]+\s*(10\.\d{4,9}/[-._;()/:A-Za-z0-9]+)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public string? TryExtractDoiFromPdf(string pdfPath)
        {
            if (string.IsNullOrWhiteSpace(pdfPath) || !File.Exists(pdfPath))
                return null;

            // ① 生バイトで doi.org URL を探す（非圧縮のハイパーリンク注釈・XMP に有効、偽陽性ほぼゼロ）
            string? doi = TryExtractDoiUrlFromRawBytes(pdfPath);
            if (!string.IsNullOrWhiteSpace(doi)) return doi;

            // ② PdfPig ワード抽出で "DOI:" プレフィックス後を探す（本文中の明示的表記）
            doi = TryExtractDoiWithLabelFromWords(pdfPath);
            if (!string.IsNullOrWhiteSpace(doi)) return doi;

            // ③ PdfPig ワード抽出で裸の DOI パターンを探す（文字並び替え問題を回避）
            doi = TryExtractDoiFromWords(pdfPath);
            if (!string.IsNullOrWhiteSpace(doi)) return doi;

            // ④ 生バイトで裸の DOI パターン（最終手段、偽陽性リスクあり）
            return TryExtractDoiFromRawBytes(pdfPath);
        }

        // ① 生バイトから doi.org URL を抽出（非圧縮領域対象）
        private string? TryExtractDoiUrlFromRawBytes(string pdfPath)
        {
            try
            {
                byte[] bytes = File.ReadAllBytes(pdfPath);
                string rawText = Encoding.Latin1.GetString(bytes);
                var match = DoiUrlRegex.Match(rawText);
                if (!match.Success) return null;
                return CleanupDoi(match.Groups[1].Value);
            }
            catch { return null; }
        }

        // ② PdfPig ワードから "DOI:" プレフィックス付き表記を抽出
        private string? TryExtractDoiWithLabelFromWords(string pdfPath)
        {
            try
            {
                using var pdf = PdfDocument.Open(pdfPath);
                int pageLimit = Math.Min(pdf.NumberOfPages, 3);

                for (int pageNum = 1; pageNum <= pageLimit; pageNum++)
                {
                    string wordText = BuildWordText(pdf.GetPage(pageNum));
                    var match = DoiLabelRegex.Match(wordText);
                    if (match.Success)
                    {
                        string doi = CleanupDoi(match.Groups[1].Value);
                        if (!string.IsNullOrWhiteSpace(doi)) return doi;
                    }
                }
                return null;
            }
            catch { return null; }
        }

        // ③ PdfPig ワードから裸の DOI パターンを抽出
        private string? TryExtractDoiFromWords(string pdfPath)
        {
            try
            {
                using var pdf = PdfDocument.Open(pdfPath);
                int pageLimit = Math.Min(pdf.NumberOfPages, 3);

                for (int pageNum = 1; pageNum <= pageLimit; pageNum++)
                {
                    string wordText = BuildWordText(pdf.GetPage(pageNum));
                    var match = DoiRegex.Match(wordText);
                    if (match.Success)
                    {
                        string doi = CleanupDoi(match.Value);
                        if (!string.IsNullOrWhiteSpace(doi)) return doi;
                    }
                }
                return null;
            }
            catch { return null; }
        }

        // ページのワードをY/X順で結合してテキストを生成
        private static string BuildWordText(Page page)
        {
            var words = page.GetWords()
                .OrderByDescending(w => w.BoundingBox.Bottom)
                .ThenBy(w => w.BoundingBox.Left);
            return string.Join(" ", words.Select(w => w.Text));
        }

        // ④ 生バイトで裸の DOI パターン（フォールバック）
        private string? TryExtractDoiFromRawBytes(string pdfPath)
        {
            try
            {
                byte[] bytes = File.ReadAllBytes(pdfPath);
                string rawText = Encoding.Latin1.GetString(bytes);
                string normalized = Regex.Replace(rawText, @"\s+", " ");

                var match = DoiRegex.Match(normalized);
                if (!match.Success) return null;

                string doi = CleanupDoi(match.Value);
                return string.IsNullOrWhiteSpace(doi) ? null : doi;
            }
            catch { return null; }
        }

        public string? TryExtractTitleFromPdf(string pdfPath)
        {
            if (string.IsNullOrWhiteSpace(pdfPath) || !File.Exists(pdfPath))
            {
                return null;
            }

            try
            {
                using var pdf = PdfDocument.Open(pdfPath);
                var firstPage = pdf.GetPage(1);

                // 1ページ目の全ワードをフォントサイズ付きで取得
                var words = firstPage.GetWords().ToList();
                if (words.Count == 0)
                {
                    return null;
                }

                // 最大フォントサイズを取得（タイトルは通常最大フォント）
                double maxFontSize = words.Max(w => w.Letters.Max(l => l.FontSize));

                // 最大フォントサイズのワードを上から順に結合
                // ただしフォントサイズが閾値（最大の70%）以上のものに限定
                double threshold = maxFontSize * 0.7;

                // Y座標で降順ソート（PDF座標は下から上なので大きいほど上）
                var titleWords = words
                    .Where(w => w.Letters.Any() && w.Letters.Max(l => l.FontSize) >= threshold)
                    .OrderByDescending(w => w.BoundingBox.Bottom)
                    .ThenBy(w => w.BoundingBox.Left)
                    .ToList();

                if (titleWords.Count == 0)
                {
                    return null;
                }

                // タイトル行を構成（Abstract/Introduction 等が出てきたら打ち切り）
                var stopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "abstract", "introduction", "keywords", "keyword", "summary",
                    "抄録", "要旨", "はじめに", "序論"
                };

                var titleParts = new List<string>();
                foreach (var word in titleWords)
                {
                    if (stopWords.Contains(word.Text))
                    {
                        break;
                    }
                    titleParts.Add(word.Text);
                }

                string title = string.Join(" ", titleParts).Trim();
                return string.IsNullOrWhiteSpace(title) ? null : title;
            }
            catch
            {
                return null;
            }
        }

        public string? TryExtractTitleFromFileName(string pdfPath)
        {
            if (string.IsNullOrWhiteSpace(pdfPath))
            {
                return null;
            }

            string fileName = Path.GetFileNameWithoutExtension(pdfPath);
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return null;
            }

            // Replace separators, strip common noise tokens, and normalize spaces.
            string title = fileName
                .Replace('_', ' ')
                .Replace('-', ' ')
                .Replace('.', ' ');

            string[] noisyTokens = { "preprint", "accepted", "final", "version", "v1", "v2", "pdf",
                                     "unlocked", "decrypted", "ocr", "scan", "copy", "draft",
                                     "submitted", "revised", "published", "postprint" };
            var tokens = title
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Where(t => !noisyTokens.Contains(t, StringComparer.OrdinalIgnoreCase))
                .ToArray();

            if (tokens.Length == 0)
            {
                return null;
            }

            return string.Join(" ", tokens);
        }

        private static string CleanupDoi(string doi)
        {
            if (string.IsNullOrWhiteSpace(doi))
            {
                return string.Empty;
            }

            string cleaned = doi.Trim();

            // Remove trailing punctuation often attached in PDFs.
            while (cleaned.Length > 0 && ".,;:)]}>".Contains(cleaned[^1]))
            {
                cleaned = cleaned[..^1];
            }

            return cleaned.Trim();
        }
    }
}
