using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using PaperManagementApp.Models;

namespace PaperManagementApp.Services
{
    public class DoiMetadataService
    {
        private static readonly HttpClient HttpClient = CreateHttpClient();

        private static HttpClient CreateHttpClient()
        {
            var client = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(20)
            };
            client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Acedia", "1.0"));
            // CrossRef のポライトプール利用のために mailto を付与
            client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("(mailto:acedia-app@example.com)"));
            return client;
        }

        public async Task<Paper?> FetchPaperByDoiAsync(string doi)
        {
            if (string.IsNullOrWhiteSpace(doi))
            {
                return null;
            }

            string normalizedDoi = NormalizeDoi(doi);
            string url = $"https://api.crossref.org/works/{Uri.EscapeDataString(normalizedDoi)}";

            using var response = await HttpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            using var stream = await response.Content.ReadAsStreamAsync();
            using var document = await JsonDocument.ParseAsync(stream);

            if (!document.RootElement.TryGetProperty("message", out JsonElement message))
            {
                return null;
            }

            var paper = new Paper
            {
                DOI = normalizedDoi,
                Title = GetPreferredTitle(message),
                Authors = BuildAuthors(message),
                Year = ExtractYear(message),
                Journal = GetFirstString(message, "container-title"),
                Volume = GetStringOrDefault(message, "volume"),
                Issue = GetStringOrDefault(message, "issue"),
                Pages = GetStringOrDefault(message, "page"),
                Abstract = GetStringOrDefault(message, "abstract"),
                Keywords = BuildKeywords(message),
                PaperType = MapPaperType(GetStringOrDefault(message, "type"))
            };

            return paper;
        }

        public async Task<Paper?> SearchPaperByMetadataAsync(string title, string authors, int? year, string journal)
        {
            var queryParts = new List<string>();

            if (!string.IsNullOrWhiteSpace(title))
            {
                queryParts.Add(title.Trim());
            }

            if (!string.IsNullOrWhiteSpace(authors))
            {
                queryParts.Add(authors.Trim().Replace("・", " "));
            }

            if (year.HasValue && year.Value > 0)
            {
                queryParts.Add(year.Value.ToString());
            }

            if (!string.IsNullOrWhiteSpace(journal))
            {
                queryParts.Add(journal.Trim());
            }

            if (queryParts.Count == 0)
            {
                return null;
            }

            string bibliographic = string.Join(" ", queryParts);
            string url = $"https://api.crossref.org/works?rows=10&query.bibliographic={Uri.EscapeDataString(bibliographic)}";

            using var response = await HttpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            using var stream = await response.Content.ReadAsStreamAsync();
            using var document = await JsonDocument.ParseAsync(stream);

            if (!document.RootElement.TryGetProperty("message", out JsonElement message))
            {
                return null;
            }

            if (!message.TryGetProperty("items", out JsonElement items) || items.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            foreach (var item in items.EnumerateArray())
            {
                var candidate = MapMessageToPaper(item);
                if (candidate == null || string.IsNullOrWhiteSpace(candidate.DOI))
                {
                    continue;
                }

                bool titleMatch = IsLikelyTitleMatch(title, candidate.Title);
                bool yearMatch = !year.HasValue || year.Value <= 0 || year.Value == candidate.Year;
                bool journalMatch = IsLikelyJournalMatch(journal, candidate.Journal);

                if (titleMatch && yearMatch && journalMatch)
                {
                    return candidate;
                }
            }

            return null;
        }

        public static string NormalizeDoi(string doi)
        {
            string value = doi.Trim();
            value = value.Replace("https://doi.org/", "", StringComparison.OrdinalIgnoreCase);
            value = value.Replace("http://doi.org/", "", StringComparison.OrdinalIgnoreCase);
            value = value.Replace("https://dx.doi.org/", "", StringComparison.OrdinalIgnoreCase);
            value = value.Replace("http://dx.doi.org/", "", StringComparison.OrdinalIgnoreCase);
            value = value.Replace("doi:", "", StringComparison.OrdinalIgnoreCase);
            return value.Trim();
        }

        private static string GetStringOrDefault(JsonElement element, string propertyName)
        {
            if (!element.TryGetProperty(propertyName, out JsonElement value))
            {
                return string.Empty;
            }

            return value.ValueKind switch
            {
                JsonValueKind.String => value.GetString() ?? string.Empty,
                JsonValueKind.Number => value.ToString(),
                _ => string.Empty
            };
        }

        // CrossRef は original-title に元言語（日本語等）のタイトルを含めることがある。
        // original-title が存在すればそちらを優先する。
        private static string GetPreferredTitle(JsonElement element)
        {
            string originalTitle = GetFirstString(element, "original-title");
            if (!string.IsNullOrWhiteSpace(originalTitle))
            {
                return originalTitle;
            }
            return GetFirstString(element, "title");
        }

        private static string GetFirstString(JsonElement element, string propertyName)
        {
            if (!element.TryGetProperty(propertyName, out JsonElement value))
            {
                return string.Empty;
            }

            if (value.ValueKind == JsonValueKind.Array && value.GetArrayLength() > 0)
            {
                var first = value[0];
                if (first.ValueKind == JsonValueKind.String)
                {
                    return first.GetString() ?? string.Empty;
                }
            }

            return string.Empty;
        }

        private static string BuildAuthors(JsonElement message)
        {
            if (!message.TryGetProperty("author", out JsonElement authorArray) || authorArray.ValueKind != JsonValueKind.Array)
            {
                return string.Empty;
            }

            var authors = new List<string>();
            foreach (JsonElement author in authorArray.EnumerateArray())
            {
                string family = GetStringOrDefault(author, "family");
                string given = GetStringOrDefault(author, "given");
                string fullName = $"{family} {given}".Trim();

                if (!string.IsNullOrWhiteSpace(fullName))
                {
                    authors.Add(fullName);
                }
            }

            return string.Join("・", authors);
        }

        private static int ExtractYear(JsonElement message)
        {
            int year = TryExtractYear(message, "published-print");
            if (year > 0) return year;

            year = TryExtractYear(message, "published-online");
            if (year > 0) return year;

            year = TryExtractYear(message, "issued");
            if (year > 0) return year;

            return 0;
        }

        private static int TryExtractYear(JsonElement message, string propertyName)
        {
            if (!message.TryGetProperty(propertyName, out JsonElement published))
            {
                return 0;
            }

            if (!published.TryGetProperty("date-parts", out JsonElement dateParts) ||
                dateParts.ValueKind != JsonValueKind.Array ||
                dateParts.GetArrayLength() == 0)
            {
                return 0;
            }

            var firstPart = dateParts[0];
            if (firstPart.ValueKind != JsonValueKind.Array || firstPart.GetArrayLength() == 0)
            {
                return 0;
            }

            if (firstPart[0].ValueKind == JsonValueKind.Number && firstPart[0].TryGetInt32(out int year))
            {
                return year;
            }

            return 0;
        }

        private static string BuildKeywords(JsonElement message)
        {
            if (!message.TryGetProperty("subject", out JsonElement subjects) || subjects.ValueKind != JsonValueKind.Array)
            {
                return string.Empty;
            }

            return string.Join(",",
                subjects.EnumerateArray()
                    .Where(s => s.ValueKind == JsonValueKind.String)
                    .Select(s => s.GetString())
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Distinct());
        }

        private static string MapPaperType(string crossrefType)
        {
            return crossrefType switch
            {
                "journal-article" => "研究論文",
                "proceedings-article" => "研究論文",
                "review-article" => "レビュー",
                "book-chapter" => "その他",
                "book" => "その他",
                "reference-entry" => "その他",
                "report" => "その他",
                _ => "その他"
            };
        }

        private static Paper? MapMessageToPaper(JsonElement message)
        {
            if (!message.TryGetProperty("DOI", out JsonElement doiElement))
            {
                return null;
            }

            string doi = doiElement.GetString() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(doi))
            {
                return null;
            }

            return new Paper
            {
                DOI = NormalizeDoi(doi),
                Title = GetPreferredTitle(message),
                Authors = BuildAuthors(message),
                Year = ExtractYear(message),
                Journal = GetFirstString(message, "container-title"),
                Volume = GetStringOrDefault(message, "volume"),
                Issue = GetStringOrDefault(message, "issue"),
                Pages = GetStringOrDefault(message, "page"),
                Abstract = GetStringOrDefault(message, "abstract"),
                Keywords = BuildKeywords(message),
                PaperType = MapPaperType(GetStringOrDefault(message, "type"))
            };
        }

        private static bool IsLikelyTitleMatch(string expectedTitle, string candidateTitle)
        {
            if (string.IsNullOrWhiteSpace(expectedTitle))
            {
                return true;
            }

            string a = NormalizeText(expectedTitle);
            string b = NormalizeText(candidateTitle);

            if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b))
            {
                return false;
            }

            return a.Contains(b, StringComparison.OrdinalIgnoreCase) ||
                   b.Contains(a, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsLikelyJournalMatch(string expectedJournal, string candidateJournal)
        {
            if (string.IsNullOrWhiteSpace(expectedJournal))
            {
                return true;
            }

            string a = NormalizeText(expectedJournal);
            string b = NormalizeText(candidateJournal);

            if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b))
            {
                return false;
            }

            return a.Contains(b, StringComparison.OrdinalIgnoreCase) ||
                   b.Contains(a, StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeText(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            return new string(value
                .ToLowerInvariant()
                .Where(c => char.IsLetterOrDigit(c) || char.IsWhiteSpace(c))
                .ToArray())
                .Trim();
        }
    }
}
