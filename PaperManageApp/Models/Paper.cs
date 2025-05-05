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

        [Required]
        public int Year { get; set; }

        [Required]
        public string Journal { get; set; }

        public string Volume { get; set; }

        public string Pages { get; set; }

        // 拡張情報
        public string DOI { get; set; }

        public string Keywords { get; set; }

        public string FilePath { get; set; }

        public bool IsFavorite { get; set; }

        // 追加メタデータ
        public string PaperType { get; set; }  // 論文タイプ（研究論文/症例報告など）

        public string ClinicalArea { get; set; }  // 臨床領域（カンマ区切り）

        public string Approach { get; set; }  // 治療アプローチ（カンマ区切り）

        public string Tags { get; set; }  // タグ（カンマ区切り）

        // メモ系（指定された論文セクション）
        public string Abstract { get; set; }  // 概要

        public string ProblemAndPurpose { get; set; }  // 問題と目的

        public string Method { get; set; }  // 方法

        public string Results { get; set; }  // 結果

        public string Discussion { get; set; }  // 考察

        public string AdditionalNotes { get; set; }  // その他メモ

        // 管理用
        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }

        // ナビゲーションプロパティ
        public virtual ICollection<PaperNote> Notes { get; set; }

        // 著者を配列として取得
        [NotMapped]
        public string[] AuthorArray
        {
            get { return Authors?.Split('.') ?? new string[0]; }
        }

        // タグを配列として取得
        [NotMapped]
        public string[] TagArray
        {
            get { return Tags?.Split(',') ?? new string[0]; }
        }

        // クリニカルエリアを配列として取得
        [NotMapped]
        public string[] ClinicalAreaArray
        {
            get { return ClinicalArea?.Split(',') ?? new string[0]; }
        }

        // アプローチを配列として取得
        [NotMapped]
        public string[] ApproachArray
        {
            get { return Approach?.Split(',') ?? new string[0]; }
        }

        // コンストラクタ
        public Paper()
        {
            CreatedAt = DateTime.Now;
            UpdatedAt = DateTime.Now;
            Notes = new List<PaperNote>();
        }

        // APA形式の引用を生成（本文中の引用用）
        public string GetInTextCitation()
        {
            // 著者が区切られているか確認
            string[] authorList = Authors?.Split('.') ?? new string[0];

            if (authorList.Length == 0)
            {
                return "著者不明";
            }

            // 各著者から姓のみを抽出
            List<string> lastNames = new List<string>();
            foreach (string authorName in authorList)
            {
                lastNames.Add(ExtractLastName(authorName.Trim()));
            }

            // 引用の生成
            if (lastNames.Count == 1)
            {
                return $"{lastNames[0]}({Year})";
            }
            else if (lastNames.Count == 2)
            {
                return $"{lastNames[0]}・{lastNames[1]}({Year})";
            }
            else
            {
                return $"{lastNames[0]}ら({Year})";
            }
        }

        // 著者名から姓を抽出するヘルパーメソッド
        private string ExtractLastName(string authorName)
        {
            // カンマが含まれている場合（例：「山本,淳一」）
            if (authorName.Contains(","))
            {
                return authorName.Split(',')[0].Trim();
            }

            // カンマがない場合は日本語名かどうかを確認
            if (IsJapaneseName(authorName))
            {
                // 日本語名の場合、より洗練された姓の抽出が必要かもしれませんが、
                // ここでは簡単のため、最初の2文字を姓と見なします
                if (authorName.Length >= 2)
                {
                    return authorName.Substring(0, 2);
                }
            }

            // スペースで区切られている場合（例：「山本 淳一」）
            if (authorName.Contains(" "))
            {
                return authorName.Split(' ')[0].Trim();
            }

            // その他の場合はそのまま返す
            return authorName;
        }

        // 日本語名かどうかを判定するヘルパーメソッド
        private bool IsJapaneseName(string name)
        {
            return name.Any(c => (c >= '\u3040' && c <= '\u309F') ||  // ひらがな
                                (c >= '\u30A0' && c <= '\u30FF') ||  // カタカナ
                                (c >= '\u4E00' && c <= '\u9FFF'));   // 漢字
        }

        // 参考文献リスト用の完全な引用情報を生成
        public string GetFullCitation()
        {
            // 著者が区切られているか確認
            string[] authorList = Authors?.Split('.') ?? new string[0];

            if (authorList.Length == 0)
            {
                return $"著者不明 ({Year}). {Title} {Journal}, {Volume}, {Pages}";
            }

            // 著者名を整形（フルネームを使用）
            List<string> formattedAuthors = new List<string>();
            foreach (string authorName in authorList)
            {
                formattedAuthors.Add(FormatAuthorName(authorName.Trim()));
            }

            string authorText = string.Join("・", formattedAuthors);
            return $"{authorText} ({Year}). {Title} {Journal}, {Volume}, {Pages}";
        }

        // 参考文献用に著者名をフォーマットするヘルパーメソッド
        private string FormatAuthorName(string authorName)
        {
            // カンマが含まれている場合（例：「山本,淳一」）
            if (authorName.Contains(","))
            {
                string[] parts = authorName.Split(',');
                if (parts.Length >= 2)
                {
                    // 日本語形式で「姓 名」として返す
                    return $"{parts[0].Trim()} {parts[1].Trim()}";
                }
            }

            // その他の場合はそのまま返す
            return authorName;
        }
    }
}