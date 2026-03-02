using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace PaperManagementApp.Services
{
    public class PdfMetadataExtractionService
    {
        // DOI pattern based on Crossref recommendation
        private static readonly Regex DoiRegex = new Regex(
            @"10\.\d{4,9}/[-._;()/:A-Za-z0-9]+",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public string? TryExtractDoiFromPdf(string pdfPath)
        {
            if (string.IsNullOrWhiteSpace(pdfPath) || !File.Exists(pdfPath))
            {
                return null;
            }

            try
            {
                // Best-effort extraction: scan PDF bytes as Latin1 text.
                byte[] bytes = File.ReadAllBytes(pdfPath);
                string rawText = Encoding.Latin1.GetString(bytes);
                string normalized = Regex.Replace(rawText, @"\s+", " ");

                var match = DoiRegex.Match(normalized);
                if (!match.Success)
                {
                    return null;
                }

                string doi = CleanupDoi(match.Value);
                return string.IsNullOrWhiteSpace(doi) ? null : doi;
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

            string[] noisyTokens = { "preprint", "accepted", "final", "version", "v1", "v2", "pdf" };
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
