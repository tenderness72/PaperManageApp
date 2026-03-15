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
            // 新規カラムのマイグレーション（EnsureCreated では自動追加されないため）
            MigrateColumns();
        }

        // 既存DBに不足カラムを追加 / NULL値を修正する簡易マイグレーション
        private void MigrateColumns()
        {
            // Issue 列の追加（既存の場合は duplicate column name エラーを無視）
            try { Database.ExecuteSqlRaw("ALTER TABLE Papers ADD COLUMN Issue TEXT"); } catch { }

            // AuthorsKana 列の追加（著者よみがな）
            try { Database.ExecuteSqlRaw("ALTER TABLE Papers ADD COLUMN AuthorsKana TEXT"); } catch { }

            // bool 型カラムが NULL だと EF Core が GetBoolean() 時に例外を投げるため 0 で埋める
            try { Database.ExecuteSqlRaw("UPDATE Papers SET IsFavorite = 0 WHERE IsFavorite IS NULL"); } catch { }

            // DateTime 型カラムも同様に NULL を修正
            try { Database.ExecuteSqlRaw("UPDATE Papers SET CreatedAt = datetime('now') WHERE CreatedAt IS NULL"); } catch { }
            try { Database.ExecuteSqlRaw("UPDATE Papers SET UpdatedAt = datetime('now') WHERE UpdatedAt IS NULL"); } catch { }
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