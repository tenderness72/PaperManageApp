using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;

namespace PaperManagementApp.Models
{
    public class Paper
    {
        [Key]
        public int Id { get; set; }

        // 基本情報（必須項目）
        [Required]
        public string Title { get; set; }

        [Required]
        public string Authors { get; set; }

        /// <summary>著者よみがな（姓のみ可）。| 区切りで複数著者に対応。ソートキーとして使用。</summary>
        public string? AuthorsKana { get; set; }

        [Required]
        public int Year { get; set; }

        [Required]
        public string Journal { get; set; }

        public string? Volume { get; set; }

        public string? Issue { get; set; }

        public string? Pages { get; set; }

        // 拡張情報
        public string? DOI { get; set; }

        public string? Keywords { get; set; }

        public string? FilePath { get; set; }

        public bool IsFavorite { get; set; }

        // 追加メタデータ
        public string? PaperType { get; set; }  // 論文タイプ（研究論文/症例報告など）

        public string? ClinicalArea { get; set; }  // 臨床領域（カンマ区切り）

        public string? Approach { get; set; }  // 治療アプローチ（カンマ区切り）

        public string? Tags { get; set; }  // タグ（カンマ区切り）

        // メモ系（指定された論文セクション）
        public string? Abstract { get; set; }  // 概要

        public string? ProblemAndPurpose { get; set; }  // 問題と目的

        public string? Method { get; set; }  // 方法

        public string? Results { get; set; }  // 結果

        public string? Discussion { get; set; }  // 考察

        public string? AdditionalNotes { get; set; }  // その他メモ

        // 管理用
        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }

        // ナビゲーションプロパティ
        public virtual ICollection<PaperNote> Notes { get; set; }

        // 著者を配列として取得
        // 新形式: | 区切り、旧形式: . 区切り（後方互換）
        [NotMapped]
        public string[] AuthorArray
        {
            get
            {
                if (string.IsNullOrEmpty(Authors)) return new string[0];
                char separator = Authors.Contains('・') ? '・'
                               : Authors.Contains('|') ? '|'
                               : '.';
                return Authors.Split(separator)
                    .Select(a => a.Trim())
                    .Where(a => !string.IsNullOrEmpty(a))
                    .ToArray();
            }
        }

        // 第1著者の姓（ソートキー用）
        // よみがなが入力されている場合はよみがなの第1著者姓を使用（五十音順ソート対応）
        [NotMapped]
        public string FirstAuthorSortKey
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(AuthorsKana))
                {
                    char sep = AuthorsKana.Contains('・') ? '・'
                             : AuthorsKana.Contains('|') ? '|'
                             : '.';
                    string firstKana = AuthorsKana.Split(sep)[0].Trim();
                    return ExtractLastName(firstKana);
                }
                return ExtractLastName(AuthorArray.FirstOrDefault() ?? string.Empty);
            }
        }

        // 著者名に日本語文字が含まれるかどうか
        [NotMapped]
        public bool HasJapaneseAuthors => AuthorArray.Any(IsJapaneseName);

        // カンマ区切り文字列を配列に変換する共通ヘルパー
        private static string[] SplitComma(string s) => s?.Split(',') ?? Array.Empty<string>();

        [NotMapped] public string[] TagArray          => SplitComma(Tags);
        [NotMapped] public string[] ClinicalAreaArray => SplitComma(ClinicalArea);
        [NotMapped] public string[] ApproachArray     => SplitComma(Approach);

        // コンストラクタ
        public Paper()
        {
            CreatedAt = DateTime.Now;
            UpdatedAt = DateTime.Now;
            Notes = new List<PaperNote>();
        }

        // JPA形式の本文中引用を生成（3.7.1）
        // 日本語: 山田（2020）/ 山田・鈴木（2020）/ 山田他（2020）
        // 英語:   Smith（2020）/ Smith & Jones（2020）/ Smith et al.（2020）
        public string GetInTextCitation()
        {
            string[] authorList = AuthorArray;
            if (authorList.Length == 0) return "著者不明";

            bool isJapanese = HasJapaneseAuthors;
            List<string> lastNames = authorList.Select(a => ExtractLastName(a)).ToList();

            if (isJapanese)
            {
                if (lastNames.Count == 1)
                    return $"{lastNames[0]}（{Year}）";
                else if (lastNames.Count == 2)
                    return $"{lastNames[0]}・{lastNames[1]}（{Year}）";
                else
                    return $"{lastNames[0]}他（{Year}）";
            }
            else
            {
                if (lastNames.Count == 1)
                    return $"{lastNames[0]}（{Year}）";
                else if (lastNames.Count == 2)
                    return $"{lastNames[0]} & {lastNames[1]}（{Year}）";
                else
                    return $"{lastNames[0]} et al.（{Year}）";
            }
        }

        // 著者名から姓を抽出するヘルパーメソッド
        private string ExtractLastName(string authorName)
        {
            // カンマが含まれている場合（例：「山本,淳一」「Smith,John」）
            if (authorName.Contains(","))
                return authorName.Split(',')[0].Trim();

            // スペースで区切られている場合（例：「山本 淳一」「Smith John」「長谷川 裕」）
            if (authorName.Contains(" "))
                return authorName.Split(' ')[0].Trim();

            // セパレータなし日本語名の場合は先頭2文字を姓と見なす
            if (IsJapaneseName(authorName) && authorName.Length >= 2)
                return authorName.Substring(0, 2);

            return authorName;
        }

        // 日本語名かどうかを判定するヘルパーメソッド
        private bool IsJapaneseName(string name)
        {
            return name.Any(c => (c >= '\u3040' && c <= '\u309F') ||  // ひらがな
                                (c >= '\u30A0' && c <= '\u30FF') ||  // カタカナ
                                (c >= '\u4E00' && c <= '\u9FFF'));   // 漢字
        }

        // JPA形式の参考文献リスト用引用を生成（3.10.2/3.10.3）
        public string GetFullCitation()
        {
            return string.Concat(GetCitationSegments().Select(s => s.Text));
        }

        // Word挿入用: 書式付きセグメントのリストを返す（3.10.2/3.10.3）
        // Item.Italic=true の区間をイタリック体で挿入すること
        //   英語: 誌名＋巻数 がイタリック (3.10.2(3))
        //   日本語: 巻数のみイタリック、誌名は立体 (3.10.3(3))
        public List<(string Text, bool Italic)> GetCitationSegments()
        {
            string[] authorList = AuthorArray;
            bool isJapanese = HasJapaneseAuthors;

            string authorText = authorList.Length == 0
                ? "著者不明"
                : isJapanese
                    ? string.Join("・", authorList.Select(a => FormatAuthorNameJapanese(a)))
                    : FormatEnglishAuthorList(authorList.Select(a => FormatAuthorNameEnglish(a)).ToList());

            string yearStr   = isJapanese ? $"（{Year}）" : $" ({Year})";
            string vol       = Volume ?? "";
            string issueStr  = !string.IsNullOrEmpty(Issue) ? $"({Issue})" : "";
            string pagesStr  = NormalizePageRange(Pages ?? "");
            string? doiStr   = FormatDOI(DOI);

            var segments = new List<(string Text, bool Italic)>();

            if (isJapanese)
            {
                // 日本語: 誌名は立体、巻数のみイタリック
                segments.Add(($"{authorText}{yearStr}. {Title}　{Journal}, ", false));
                if (!string.IsNullOrEmpty(vol))
                    segments.Add((vol, true));                          // 巻数 → イタリック
                segments.Add(($"{issueStr}, {pagesStr}.", false));
            }
            else
            {
                // 英語: 誌名・巻数ともにイタリック
                segments.Add(($"{authorText}{yearStr}. {Title}. ", false));
                segments.Add((Journal, true));                          // 誌名 → イタリック
                segments.Add((", ", false));
                if (!string.IsNullOrEmpty(vol))
                    segments.Add((vol, true));                          // 巻数 → イタリック
                segments.Add(($"{issueStr}, {pagesStr}.", false));
            }

            if (doiStr != null)
                segments.Add((" " + doiStr, false));

            return segments;
        }

        // ページ範囲のハイフンを2分ダッシュに正規化（JPA 3.10.2/3.10.3）
        private static string NormalizePageRange(string pages)
            => pages.Replace("-", "–");

        // 英語著者リストを JPA 形式で結合（20名以下: A, B, & C / 21名以上: A, ..., Z）
        private string FormatEnglishAuthorList(List<string> authors)
        {
            if (authors.Count == 1) return authors[0];
            if (authors.Count <= 20)
                return string.Join(", ", authors.Take(authors.Count - 1)) + ", & " + authors.Last();
            // 21名以上: 第1〜19著者 + ... + 最終著者
            return string.Join(", ", authors.Take(19)) + ", ... " + authors.Last();
        }

        // 参考文献用・日本語著者名のフォーマット（姓 名 形式）
        private string FormatAuthorNameJapanese(string authorName)
        {
            if (authorName.Contains(","))
            {
                var parts = authorName.Split(',');
                return $"{parts[0].Trim()} {parts[1].Trim()}";
            }
            return authorName;
        }

        // 参考文献用・英語著者名のフォーマット（姓, イニシャル. 形式）
        // 例: "Smith, John Michael" → "Smith, J. M."
        private string FormatAuthorNameEnglish(string authorName)
        {
            string surname, givenNames;

            if (authorName.Contains(","))
            {
                var parts = authorName.Split(new[] { ',' }, 2);
                surname = parts[0].Trim();
                givenNames = parts[1].Trim();
            }
            else if (authorName.Contains(" "))
            {
                var parts = authorName.Split(new[] { ' ' }, 2);
                surname = parts[0].Trim();
                givenNames = parts[1].Trim();
            }
            else
            {
                return authorName;
            }

            if (string.IsNullOrEmpty(givenNames)) return surname;

            // イニシャル化（"John Michael" → "J. M."）
            string initials = string.Join(" ", givenNames.Split(' ')
                .Where(n => !string.IsNullOrEmpty(n))
                .Select(n => n[0] + "."));

            return $"{surname}, {initials}";
        }

        // DOIを正規化して https://doi.org/xxx 形式で返す
        private string FormatDOI(string doi)
        {
            if (string.IsNullOrEmpty(doi)) return null;
            string id = doi
                .Replace("https://doi.org/", "")
                .Replace("http://doi.org/", "")
                .Replace("https://dx.doi.org/", "")
                .Trim();
            return $"https://doi.org/{id}";
        }
    }
}