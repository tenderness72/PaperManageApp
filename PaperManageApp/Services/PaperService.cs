using Microsoft.EntityFrameworkCore;
using PaperManagementApp.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace PaperManagementApp.Services
{
    public class PaperService
    {
        private readonly DatabaseContext _dbContext;

        public PaperService()
        {
            _dbContext = new DatabaseContext();
        }

        // すべての論文を取得
        public List<Paper> GetAllPapers()
        {
            return _dbContext.Papers
                .Include(p => p.Notes)
                .OrderByDescending(p => p.UpdatedAt)
                .ToList();
        }

        // ID指定で論文を取得
        public Paper GetPaperById(int id)
        {
            return _dbContext.Papers
                .Include(p => p.Notes)
                .FirstOrDefault(p => p.Id == id);
        }

        // お気に入りの論文を取得
        public List<Paper> GetFavoritePapers()
        {
            return _dbContext.Papers
                .Include(p => p.Notes)
                .Where(p => p.IsFavorite)
                .OrderByDescending(p => p.UpdatedAt)
                .ToList();
        }

        // 検索条件に一致する論文を取得
        public List<Paper> SearchPapers(string searchText)
        {
            if (string.IsNullOrWhiteSpace(searchText))
            {
                return GetAllPapers();
            }

            searchText = searchText.ToLower();

            return _dbContext.Papers
                .Include(p => p.Notes)
                .Where(p =>
                    p.Title.ToLower().Contains(searchText) ||
                    p.Authors.ToLower().Contains(searchText) ||
                    p.Journal.ToLower().Contains(searchText) ||
                    p.Abstract.ToLower().Contains(searchText) ||
                    p.Keywords.ToLower().Contains(searchText) ||
                    p.Tags.ToLower().Contains(searchText) ||
                    p.ProblemAndPurpose.ToLower().Contains(searchText) ||
                    p.Method.ToLower().Contains(searchText) ||
                    p.Results.ToLower().Contains(searchText) ||
                    p.Discussion.ToLower().Contains(searchText)
                )
                .OrderByDescending(p => p.UpdatedAt)
                .ToList();
        }

        // フィルタリング（出版年、ジャーナル、臨床領域など）
        public List<Paper> FilterPapers(int? year = null, string journal = null, string clinicalArea = null)
        {
            var query = _dbContext.Papers.Include(p => p.Notes).AsQueryable();

            if (year.HasValue)
            {
                query = query.Where(p => p.Year == year.Value);
            }

            if (!string.IsNullOrWhiteSpace(journal))
            {
                query = query.Where(p => p.Journal.Contains(journal));
            }

            if (!string.IsNullOrWhiteSpace(clinicalArea))
            {
                query = query.Where(p => p.ClinicalArea.Contains(clinicalArea));
            }

            return query.OrderByDescending(p => p.UpdatedAt).ToList();
        }

        // 論文の追加
        public Paper AddPaper(Paper paper)
        {
            paper.CreatedAt = DateTime.Now;
            paper.UpdatedAt = DateTime.Now;

            _dbContext.Papers.Add(paper);
            _dbContext.SaveChanges();

            return paper;
        }

        // 論文の更新
        public Paper UpdatePaper(Paper paper)
        {
            var existingPaper = _dbContext.Papers.Find(paper.Id);

            if (existingPaper == null)
            {
                throw new KeyNotFoundException($"ID: {paper.Id} の論文が見つかりません");
            }

            // 作成日時は保持
            paper.CreatedAt = existingPaper.CreatedAt;
            paper.UpdatedAt = DateTime.Now;

            _dbContext.Entry(existingPaper).CurrentValues.SetValues(paper);
            _dbContext.SaveChanges();

            return paper;
        }

        // セクションのみ更新（概要、方法、結果など）
        public Paper UpdatePaperSection(int paperId, string sectionName, string content)
        {
            var paper = _dbContext.Papers.Find(paperId);

            if (paper == null)
            {
                throw new KeyNotFoundException($"ID: {paperId} の論文が見つかりません");
            }

            switch (sectionName.ToLower())
            {
                case "abstract":
                    paper.Abstract = content;
                    break;
                case "problemandpurpose":
                    paper.ProblemAndPurpose = content;
                    break;
                case "method":
                    paper.Method = content;
                    break;
                case "results":
                    paper.Results = content;
                    break;
                case "discussion":
                    paper.Discussion = content;
                    break;
                case "additionalnotes":
                    paper.AdditionalNotes = content;
                    break;
                default:
                    throw new ArgumentException($"不明なセクション名: {sectionName}");
            }

            paper.UpdatedAt = DateTime.Now;
            _dbContext.SaveChanges();

            return paper;
        }

        // お気に入り状態の切り替え
        public Paper ToggleFavorite(int paperId)
        {
            var paper = _dbContext.Papers.Find(paperId);

            if (paper == null)
            {
                throw new KeyNotFoundException($"ID: {paperId} の論文が見つかりません");
            }

            paper.IsFavorite = !paper.IsFavorite;
            paper.UpdatedAt = DateTime.Now;

            _dbContext.SaveChanges();

            return paper;
        }

        // 論文の削除
        public bool DeletePaper(int paperId)
        {
            var paper = _dbContext.Papers.Find(paperId);

            if (paper == null)
            {
                return false;
            }

            // 関連するPDFファイルがあれば削除
            if (!string.IsNullOrEmpty(paper.FilePath) && File.Exists(paper.FilePath))
            {
                try
                {
                    File.Delete(paper.FilePath);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"PDFファイルの削除に失敗しました: {ex.Message}");
                    // ファイル削除に失敗しても論文データは削除する
                }
            }

            _dbContext.Papers.Remove(paper);
            _dbContext.SaveChanges();

            return true;
        }

        // 論文メモの追加
        public PaperNote AddPaperNote(PaperNote note)
        {
            note.CreatedAt = DateTime.Now;
            note.UpdatedAt = DateTime.Now;

            _dbContext.PaperNotes.Add(note);
            _dbContext.SaveChanges();

            return note;
        }

        // 論文メモの更新
        public PaperNote UpdatePaperNote(PaperNote note)
        {
            var existingNote = _dbContext.PaperNotes.Find(note.Id);

            if (existingNote == null)
            {
                throw new KeyNotFoundException($"ID: {note.Id} のメモが見つかりません");
            }

            note.CreatedAt = existingNote.CreatedAt;
            note.UpdatedAt = DateTime.Now;

            _dbContext.Entry(existingNote).CurrentValues.SetValues(note);
            _dbContext.SaveChanges();

            return note;
        }

        // 論文メモの削除
        public bool DeletePaperNote(int noteId)
        {
            var note = _dbContext.PaperNotes.Find(noteId);

            if (note == null)
            {
                return false;
            }

            _dbContext.PaperNotes.Remove(note);
            _dbContext.SaveChanges();

            return true;
        }

        // 個別値のリストを取得（フィルター選択肢用）
        public List<int> GetYearsList()
        {
            return _dbContext.Papers
                .Select(p => p.Year)
                .Distinct()
                .OrderByDescending(y => y)
                .ToList();
        }

        public List<string> GetJournalsList()
        {
            return _dbContext.Papers
                .Select(p => p.Journal)
                .Where(j => !string.IsNullOrEmpty(j))
                .Distinct()
                .OrderBy(j => j)
                .ToList();
        }

        public List<string> GetClinicalAreasList()
        {
            // カンマ区切りの値を分割して一意のリストを取得
            var areas = new List<string>();

            foreach (var paper in _dbContext.Papers)
            {
                if (!string.IsNullOrEmpty(paper.ClinicalArea))
                {
                    var paperAreas = paper.ClinicalAreaArray;
                    foreach (var area in paperAreas)
                    {
                        string trimmedArea = area.Trim();
                        if (!string.IsNullOrEmpty(trimmedArea) && !areas.Contains(trimmedArea))
                        {
                            areas.Add(trimmedArea);
                        }
                    }
                }
            }

            return areas.OrderBy(a => a).ToList();
        }

        public List<string> GetApproachesList()
        {
            // カンマ区切りの値を分割して一意のリストを取得
            var approaches = new List<string>();

            foreach (var paper in _dbContext.Papers)
            {
                if (!string.IsNullOrEmpty(paper.Approach))
                {
                    var paperApproaches = paper.ApproachArray;
                    foreach (var approach in paperApproaches)
                    {
                        string trimmedApproach = approach.Trim();
                        if (!string.IsNullOrEmpty(trimmedApproach) && !approaches.Contains(trimmedApproach))
                        {
                            approaches.Add(trimmedApproach);
                        }
                    }
                }
            }

            return approaches.OrderBy(a => a).ToList();
        }

        public List<string> GetTagsList()
        {
            // カンマ区切りの値を分割して一意のリストを取得
            var tags = new List<string>();

            foreach (var paper in _dbContext.Papers)
            {
                if (!string.IsNullOrEmpty(paper.Tags))
                {
                    var paperTags = paper.TagArray;
                    foreach (var tag in paperTags)
                    {
                        string trimmedTag = tag.Trim();
                        if (!string.IsNullOrEmpty(trimmedTag) && !tags.Contains(trimmedTag))
                        {
                            tags.Add(trimmedTag);
                        }
                    }
                }
            }

            return tags.OrderBy(t => t).ToList();
        }
    }
}