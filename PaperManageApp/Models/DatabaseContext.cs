using Microsoft.EntityFrameworkCore;
using PaperManagementApp.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection.Emit;

namespace PaperManagementApp.Models
{
    public class DatabaseContext : DbContext
    {
        // テーブル定義
        public DbSet<Paper> Papers { get; set; }
        public DbSet<PaperNote> PaperNotes { get; set; }

        // コンストラクタ
        public DatabaseContext()
        {
            // データベースファイルが存在しない場合は作成
            Database.EnsureCreated();
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            // アプリケーションデータフォルダにSQLiteデータベースを作成
            string appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "PaperManagementApp"
            );

            // フォルダが存在しない場合は作成
            if (!Directory.Exists(appDataPath))
            {
                Directory.CreateDirectory(appDataPath);
            }

            string dbPath = Path.Combine(appDataPath, "papers.db");
            optionsBuilder.UseSqlite($"Data Source={dbPath}");
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // 論文とメモの関連付け - 修正版
            modelBuilder.Entity<Paper>()
                .HasMany(p => p.Notes)  // 正しい: NotesはICollection<PaperNote>型
                .WithOne(n => n.Paper)
                .HasForeignKey(n => n.PaperId)
                .OnDelete(DeleteBehavior.Cascade);

            // インデックス設定（変更なし）
            modelBuilder.Entity<Paper>()
                .HasIndex(p => p.Title);

            modelBuilder.Entity<Paper>()
                .HasIndex(p => p.Authors);

            modelBuilder.Entity<Paper>()
                .HasIndex(p => p.Year);

            modelBuilder.Entity<Paper>()
                .HasIndex(p => p.Journal);
        }
    }
}