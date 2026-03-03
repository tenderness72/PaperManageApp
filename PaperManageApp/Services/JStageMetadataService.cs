using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;
using PaperManagementApp.Models;

namespace PaperManagementApp.Services
{
    public class JStageMetadataService
    {
        private static readonly HttpClient HttpClient = CreateHttpClient();

        // J-STAGE DOI 形式: 10.prefix/journal.vol.no_page
        private static readonly Regex JStageDoiRegex = new Regex(
            @"^10\.\d+/([a-zA-Z][a-zA-Z0-9]*)\.(\d+)\.(\d+)[_-](\d+)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly XNamespace Atom = "http://www.w3.org/2005/Atom";
        private static readonly XNamespace Prism = "http://prismstandard.org/namespaces/basic/2.0/";
        private static readonly XNamespace Dc = "http://purl.org/dc/elements/1.1/";

        private static HttpClient CreateHttpClient()
        {
            var client = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(20)
            };
            client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Acedia", "1.0"));
            return client;
        }

        public async Task<Paper?> FetchPaperByDoiAsync(string doi)
        {
            string normalizedDoi = DoiMetadataService.NormalizeDoi(doi);
            var match = JStageDoiRegex.Match(normalizedDoi);
            if (!match.Success)
            {
                return null;
            }

            string cdjournal = match.Groups[1].Value;
            string vol = match.Groups[2].Value;
            string no = match.Groups[3].Value;
            string page = match.Groups[4].Value;

            string url = $"https://api.jstage.jst.go.jp/searchapi/do?service=3" +
                         $"&cdjournal={Uri.EscapeDataString(cdjournal)}" +
                         $"&vol={vol}&no={no}&result=100";

            using var response = await HttpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            string xml = await response.Content.ReadAsStringAsync();
            XDocument doc;
            try
            {
                doc = XDocument.Parse(xml);
            }
            catch
            {
                return null;
            }

            foreach (var entry in doc.Descendants(Atom + "entry"))
            {
                string startPage = entry.Element(Prism + "startingPage")?.Value ?? string.Empty;
                if (startPage == page)
                {
                    return MapEntryToPaper(entry);
                }
            }

            return null;
        }

        public async Task<Paper?> SearchAsync(string title, string authors, int? year, string journal)
        {
            if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(journal))
            {
                return null;
            }

            var queryParams = new List<string> { "service=3", "result=5" };

            string searchText = !string.IsNullOrWhiteSpace(title) ? title.Trim() : journal.Trim();
            queryParams.Add($"text={Uri.EscapeDataString(searchText)}");

            if (year.HasValue && year.Value > 0)
            {
                queryParams.Add($"pubyearfrom={year.Value}");
                queryParams.Add($"pubyearto={year.Value}");
            }

            string url = "https://api.jstage.jst.go.jp/searchapi/do?" + string.Join("&", queryParams);

            using var response = await HttpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            string xml = await response.Content.ReadAsStringAsync();
            XDocument doc;
            try
            {
                doc = XDocument.Parse(xml);
            }
            catch
            {
                return null;
            }

            foreach (var entry in doc.Descendants(Atom + "entry"))
            {
                var candidate = MapEntryToPaper(entry);
                if (candidate == null)
                {
                    continue;
                }

                bool titleMatch = IsLikelyMatch(title, candidate.Title);
                bool yearMatch = !year.HasValue || year.Value <= 0 || year.Value == candidate.Year;

                if (titleMatch && yearMatch)
                {
                    return candidate;
                }
            }

            return null;
        }

        private static Paper? MapEntryToPaper(XElement entry)
        {
            // デフォルト名前空間が Atom のため、J-STAGE 独自要素も Atom + で取得する
            string title = entry.Element(Atom + "article_title")?.Element(Atom + "ja")?.Value
                        ?? entry.Element(Atom + "article_title")?.Element(Atom + "en")?.Value
                        ?? entry.Element(Atom + "title")?.Value
                        ?? string.Empty;

            if (string.IsNullOrWhiteSpace(title))
            {
                return null;
            }

            string doi = entry.Element(Prism + "doi")?.Value ?? string.Empty;
            string authors = BuildAuthors(entry);
            int year = ParseYear(entry.Element(Atom + "pubyear")?.Value ?? string.Empty);
            string journal = entry.Element(Atom + "material_title")?.Element(Atom + "ja")?.Value
                          ?? entry.Element(Atom + "material_title")?.Element(Atom + "en")?.Value
                          ?? string.Empty;
            string volume = entry.Element(Prism + "volume")?.Value ?? string.Empty;
            string startPage = entry.Element(Prism + "startingPage")?.Value ?? string.Empty;
            string endPage = entry.Element(Prism + "endingPage")?.Value ?? string.Empty;
            string description = entry.Element(Dc + "description")?.Value ?? string.Empty;

            return new Paper
            {
                DOI = string.IsNullOrWhiteSpace(doi) ? string.Empty : DoiMetadataService.NormalizeDoi(doi),
                Title = title,
                Authors = authors,
                Year = year,
                Journal = journal,
                Volume = volume,
                Pages = BuildPages(startPage, endPage),
                Abstract = description,
                PaperType = "研究論文"
            };
        }

        private static string BuildAuthors(XElement entry)
        {
            var authorElement = entry.Element(Atom + "author");
            if (authorElement == null)
            {
                return string.Empty;
            }

            // 日本語名を優先、なければ英語名
            var jaNames = authorElement.Element(Atom + "ja")?.Elements(Atom + "name")
                .Select(n => n.Value)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .ToList();

            if (jaNames != null && jaNames.Count > 0)
            {
                return string.Join("|", jaNames);
            }

            var enNames = authorElement.Element(Atom + "en")?.Elements(Atom + "name")
                .Select(n => n.Value)
                .Where(n => !string.IsNullOrWhiteSpace(n));

            return enNames != null ? string.Join("|", enNames) : string.Empty;
        }

        private static int ParseYear(string pubyear)
        {
            return int.TryParse(pubyear.Trim(), out int year) ? year : 0;
        }

        private static string BuildPages(string startPage, string endPage)
        {
            if (string.IsNullOrWhiteSpace(startPage))
            {
                return string.Empty;
            }

            return string.IsNullOrWhiteSpace(endPage) ? startPage : $"{startPage}-{endPage}";
        }

        private static bool IsLikelyMatch(string expected, string candidate)
        {
            if (string.IsNullOrWhiteSpace(expected))
            {
                return true;
            }

            string a = NormalizeText(expected);
            string b = NormalizeText(candidate);

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
