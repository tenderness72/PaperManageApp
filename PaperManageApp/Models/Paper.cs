using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

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
            get { return Authors?.Split(',') ?? new string[0]; }
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
            string firstAuthor = AuthorArray.Length > 0 ? AuthorArray[0].Trim() : "著者不明";

            if (AuthorArray.Length == 1)
            {
                return $"{firstAuthor}({Year})";
            }
            else if (AuthorArray.Length == 2)
            {
                string secondAuthor = AuthorArray[1].Trim();
                return $"{firstAuthor}・{secondAuthor}({Year})";
            }
            else
            {
                return $"{firstAuthor}ら({Year})";
            }
        }

        // 参考文献リスト用の完全な引用情報を生成
        public string GetFullCitation()
        {
            string authorText = string.Join("・", AuthorArray);
            return $"{authorText} ({Year}). {Title} {Journal}, {Volume}, {Pages}";
        }
    }
}