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
    public class OpenAlexMetadataService
    {
        private static readonly HttpClient HttpClient = CreateHttpClient();

        private static HttpClient CreateHttpClient()
        {
            var client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
            client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Acedia", "1.0"));
            client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("(mailto:acedia-app@example.com)"));
            return client;
        }

        // DOI から直接取得
        public async Task<Paper?> FetchPaperByDoiAsync(string doi)
        {
            if (string.IsNullOrWhiteSpace(doi)) return null;

            string normalizedDoi = DoiMetadataService.NormalizeDoi(doi);
            // OpenAlex は "https://doi.org/{doi}" 形式でも "/works/{doi}" 形式でも受け付ける
            string url = $"https://api.openalex.org/works/https://doi.org/{Uri.EscapeDataString(normalizedDoi)}" +
                         "?mailto=acedia-app@example.com";

            try
            {
                using var response = await HttpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode) return null;

                using var stream = await response.Content.ReadAsStreamAsync();
                using var doc = await JsonDocument.ParseAsync(stream);
                return MapToPaper(doc.RootElement);
            }
            catch { return null; }
        }

        // タイトル・著者・年・雑誌名で検索
        public async Task<Paper?> SearchAsync(string title, string authors, int? year, string journal)
        {
            if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(journal)) return null;

            string query = !string.IsNullOrWhiteSpace(title) ? title.Trim() : journal.Trim();
            string url = $"https://api.openalex.org/works?search={Uri.EscapeDataString(query)}" +
                         "&per_page=5&mailto=acedia-app@example.com";

            try
            {
                using var response = await HttpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode) return null;

                using var stream = await response.Content.ReadAsStreamAsync();
                using var doc = await JsonDocument.ParseAsync(stream);

                if (!doc.RootElement.TryGetProperty("results", out JsonElement results)) return null;

                foreach (var item in results.EnumerateArray())
                {
                    var candidate = MapToPaper(item);
                    if (candidate == null) continue;

                    bool titleMatch = IsLikelyMatch(title, candidate.Title);
                    bool yearMatch = !year.HasValue || year.Value <= 0 || year.Value == candidate.Year;

                    if (titleMatch && yearMatch) return candidate;
                }

                return null;
            }
            catch { return null; }
        }

        private static Paper? MapToPaper(JsonElement work)
        {
            string title = work.TryGetProperty("title", out var t) ? t.GetString() ?? string.Empty : string.Empty;
            if (string.IsNullOrWhiteSpace(title)) return null;

            string doi = string.Empty;
            if (work.TryGetProperty("doi", out var doiEl) && doiEl.ValueKind == JsonValueKind.String)
                doi = DoiMetadataService.NormalizeDoi(doiEl.GetString() ?? string.Empty);

            int year = 0;
            if (work.TryGetProperty("publication_year", out var yearEl) && yearEl.ValueKind == JsonValueKind.Number)
                yearEl.TryGetInt32(out year);

            string authors = BuildAuthors(work);
            string journal = BuildJournal(work);
            string volume = BuildStringField(work, "biblio", "volume");
            string issue = BuildStringField(work, "biblio", "issue");
            string firstPage = BuildStringField(work, "biblio", "first_page");
            string lastPage = BuildStringField(work, "biblio", "last_page");
            string pages = string.IsNullOrWhiteSpace(firstPage) ? string.Empty
                         : string.IsNullOrWhiteSpace(lastPage) ? firstPage
                         : $"{firstPage}-{lastPage}";
            string abstrakt = BuildAbstract(work);
            string keywords = BuildKeywords(work);
            string paperType = MapType(work);

            return new Paper
            {
                DOI = doi,
                Title = title,
                Authors = authors,
                Year = year,
                Journal = journal,
                Volume = volume,
                Issue = issue,
                Pages = pages,
                Abstract = abstrakt,
                Keywords = keywords,
                PaperType = paperType
            };
        }

        private static string BuildAuthors(JsonElement work)
        {
            if (!work.TryGetProperty("authorships", out var authorships)) return string.Empty;

            var names = new List<string>();
            foreach (var authorship in authorships.EnumerateArray())
            {
                if (!authorship.TryGetProperty("author", out var author)) continue;
                string name = author.TryGetProperty("display_name", out var n) ? n.GetString() ?? string.Empty : string.Empty;
                if (!string.IsNullOrWhiteSpace(name)) names.Add(name);
            }
            return string.Join("|", names);
        }

        private static string BuildJournal(JsonElement work)
        {
            if (!work.TryGetProperty("primary_location", out var loc)) return string.Empty;
            if (loc.ValueKind != JsonValueKind.Object) return string.Empty;
            if (!loc.TryGetProperty("source", out var source)) return string.Empty;
            if (source.ValueKind != JsonValueKind.Object) return string.Empty;
            return source.TryGetProperty("display_name", out var n) ? n.GetString() ?? string.Empty : string.Empty;
        }

        private static string BuildStringField(JsonElement work, string outer, string inner)
        {
            if (!work.TryGetProperty(outer, out var outerEl)) return string.Empty;
            if (!outerEl.TryGetProperty(inner, out var innerEl)) return string.Empty;
            return innerEl.ValueKind == JsonValueKind.String ? innerEl.GetString() ?? string.Empty : string.Empty;
        }

        // OpenAlex の abstract は inverted index 形式（単語→位置リスト）なので再構築する
        private static string BuildAbstract(JsonElement work)
        {
            if (!work.TryGetProperty("abstract_inverted_index", out var index)) return string.Empty;
            if (index.ValueKind != JsonValueKind.Object) return string.Empty;

            var posWord = new SortedDictionary<int, string>();
            foreach (var prop in index.EnumerateObject())
            {
                foreach (var pos in prop.Value.EnumerateArray())
                {
                    if (pos.TryGetInt32(out int p))
                        posWord[p] = prop.Name;
                }
            }
            return string.Join(" ", posWord.Values);
        }

        private static string BuildKeywords(JsonElement work)
        {
            if (!work.TryGetProperty("keywords", out var kws)) return string.Empty;
            var words = kws.EnumerateArray()
                .Where(k => k.TryGetProperty("display_name", out _))
                .Select(k => k.GetProperty("display_name").GetString() ?? string.Empty)
                .Where(k => !string.IsNullOrWhiteSpace(k));
            return string.Join(",", words);
        }

        private static string MapType(JsonElement work)
        {
            if (!work.TryGetProperty("type", out var t)) return "その他";
            return (t.GetString() ?? string.Empty) switch
            {
                "article" => "研究論文",
                "review" => "レビュー",
                "proceedings-article" => "研究論文",
                _ => "その他"
            };
        }

        private static bool IsLikelyMatch(string expected, string candidate)
        {
            if (string.IsNullOrWhiteSpace(expected)) return true;

            string a = NormalizeText(expected);
            string b = NormalizeText(candidate);
            if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b)) return false;

            return a.Contains(b, StringComparison.OrdinalIgnoreCase) ||
                   b.Contains(a, StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeText(string value)
        {
            return new string(value.ToLowerInvariant()
                .Where(c => char.IsLetterOrDigit(c) || char.IsWhiteSpace(c))
                .ToArray()).Trim();
        }
    }
}
