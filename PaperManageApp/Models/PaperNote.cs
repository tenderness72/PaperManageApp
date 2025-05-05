using System;
using System.ComponentModel.DataAnnotations;

namespace PaperManagementApp.Models
{
    public class PaperNote
    {
        [Key]
        public int Id { get; set; }

        public int PaperId { get; set; }

        [Required]
        public string Title { get; set; }

        [Required]
        public string Content { get; set; }

        public string Category { get; set; }  // "問題と目的", "方法", "結果", "考察", "その他" など

        public int PageNumber { get; set; }  // 関連ページ

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }

        // ナビゲーションプロパティ
        public virtual Paper Paper { get; set; }

        // コンストラクタ
        public PaperNote()
        {
            CreatedAt = DateTime.Now;
            UpdatedAt = DateTime.Now;
        }
    }
}