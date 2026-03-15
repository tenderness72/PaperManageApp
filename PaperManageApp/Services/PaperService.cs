using Microsoft.EntityFrameworkCore;
using PaperManagementApp.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace PaperManagementApp.Services
{
    public class PaperService : IDisposable
    {
        private readonly DatabaseContext _dbContext;
        private bool _disposed = false;

        public PaperService()
        {
            _dbContext = new DatabaseContext();
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _dbContext?.Dispose();
                _disposed = true;
            }
        }

        // すべての論文を取得
        public async Task<List<Paper>> GetAllPapersAsync()
        {
            return await _dbContext.Papers
                .Include(p => p.Notes)
                .OrderByDescending(p => p.UpdatedAt)
                .ToListAsync();
        }

        // ID指定で論文を取得
        public async Task<Paper> GetPaperByIdAsync(int id)
        {
            return await _dbContext.Papers
                .Include(p => p.Notes)
                .FirstOrDefaultAsync(p => p.Id == id);
        }

        // お気に入りの論文を取得
        public async Task<List<Paper>> GetFavoritePapersAsync()
        {
            return await _dbContext.Papers
                .Include(p => p.Notes)
                .Where(p => p.IsFavorite)
                .OrderByDescending(p => p.UpdatedAt)
                .ToListAsync();
        }

        // 検索条件に一致する論文を取得
        public async Task<List<Paper>> SearchPapersAsync(string searchText)
        {
            if (string.IsNullOrWhiteSpace(searchText))
            {
                return await GetAllPapersAsync();
            }

            searchText = searchText.ToLower();

            return await _dbContext.Papers
                .Include(p => p.Notes)
                .Where(p =>
                    p.Title.ToLower().Contains(searchText) ||
                    p.Authors.ToLower().Contains(searchText) ||
                    p.Journal.ToLower().Contains(searchText) ||
                    (p.Abstract != null && p.Abstract.ToLower().Contains(searchText)) ||
                    (p.Keywords != null && p.Keywords.ToLower().Contains(searchText)) ||
                    (p.Tags != null && p.Tags.ToLower().Contains(searchText)) ||
                    (p.ProblemAndPurpose != null && p.ProblemAndPurpose.ToLower().Contains(searchText)) ||
                    (p.Method != null && p.Method.ToLower().Contains(searchText)) ||
                    (p.Results != null && p.Results.ToLower().Contains(searchText)) ||
                    (p.Discussion != null && p.Discussion.ToLower().Contains(searchText))
                )
                .OrderByDescending(p => p.UpdatedAt)
                .ToListAsync();
        }

        // フィルタリング（出版年、ジャーナル、臨床領域など）
        public async Task<List<Paper>> FilterPapersAsync(int? year = null, string journal = null, string clinicalArea = null)
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

            return await query.OrderByDescending(p => p.UpdatedAt).ToListAsync();
        }

        // 論文の追加
        public async Task<Paper> AddPaperAsync(Paper paper)
        {
            paper.CreatedAt = DateTime.Now;
            paper.UpdatedAt = DateTime.Now;

            _dbContext.Papers.Add(paper);
            await _dbContext.SaveChangesAsync();

            return paper;
        }

        // 論文の更新
        public async Task<Paper> UpdatePaperAsync(Paper paper)
        {
            var existingPaper = await _dbContext.Papers.FindAsync(paper.Id);

            if (existingPaper == null)
            {
                throw new KeyNotFoundException($"ID: {paper.Id} の論文が見つかりません");
            }

            // 作成日時は保持
            paper.CreatedAt = existingPaper.CreatedAt;
            paper.UpdatedAt = DateTime.Now;

            _dbContext.Entry(existingPaper).CurrentValues.SetValues(paper);
            await _dbContext.SaveChangesAsync();

            return paper;
        }

        // セクションのみ更新（概要、方法、結果など）
        public async Task<Paper> UpdatePaperSectionAsync(int paperId, string sectionName, string content)
        {
            var paper = await _dbContext.Papers.FindAsync(paperId);

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
            await _dbContext.SaveChangesAsync();

            return paper;
        }

        // お気に入り状態の切り替え
        public async Task<Paper> ToggleFavoriteAsync(int paperId)
        {
            var paper = await _dbContext.Papers.FindAsync(paperId);

            if (paper == null)
            {
                throw new KeyNotFoundException($"ID: {paperId} の論文が見つかりません");
            }

            paper.IsFavorite = !paper.IsFavorite;
            paper.UpdatedAt = DateTime.Now;

            await _dbContext.SaveChangesAsync();

            return paper;
        }

        // 論文の削除
        public async Task<bool> DeletePaperAsync(int paperId)
        {
            var paper = await _dbContext.Papers.FindAsync(paperId);

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
            await _dbContext.SaveChangesAsync();

            return true;
        }

        // 複数論文の一括お気に入り切替
        public async Task ToggleFavoritesAsync(IEnumerable<int> paperIds)
        {
            foreach (int id in paperIds)
            {
                var paper = await _dbContext.Papers.FindAsync(id);
                if (paper == null) continue;
                paper.IsFavorite = !paper.IsFavorite;
                paper.UpdatedAt = DateTime.Now;
            }
            await _dbContext.SaveChangesAsync();
        }

        // 複数論文の一括削除
        public async Task<int> DeletePapersAsync(IEnumerable<int> paperIds)
        {
            int deleted = 0;
            foreach (int id in paperIds)
            {
                var paper = await _dbContext.Papers.FindAsync(id);
                if (paper == null) continue;

                if (!string.IsNullOrEmpty(paper.FilePath) && File.Exists(paper.FilePath))
                {
                    try { File.Delete(paper.FilePath); }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"PDFファイルの削除に失敗しました: {ex.Message}");
                    }
                }

                _dbContext.Papers.Remove(paper);
                deleted++;
            }
            if (deleted > 0)
                await _dbContext.SaveChangesAsync();
            return deleted;
        }

        // 論文メモの追加
        public async Task<PaperNote> AddPaperNoteAsync(PaperNote note)
        {
            note.CreatedAt = DateTime.Now;
            note.UpdatedAt = DateTime.Now;

            _dbContext.PaperNotes.Add(note);
            await _dbContext.SaveChangesAsync();

            return note;
        }

        // 論文メモの更新
        public async Task<PaperNote> UpdatePaperNoteAsync(PaperNote note)
        {
            var existingNote = await _dbContext.PaperNotes.FindAsync(note.Id);

            if (existingNote == null)
            {
                throw new KeyNotFoundException($"ID: {note.Id} のメモが見つかりません");
            }

            note.CreatedAt = existingNote.CreatedAt;
            note.UpdatedAt = DateTime.Now;

            _dbContext.Entry(existingNote).CurrentValues.SetValues(note);
            await _dbContext.SaveChangesAsync();

            return note;
        }

        // 論文メモの削除
        public async Task<bool> DeletePaperNoteAsync(int noteId)
        {
            var note = await _dbContext.PaperNotes.FindAsync(noteId);

            if (note == null)
            {
                return false;
            }

            _dbContext.PaperNotes.Remove(note);
            await _dbContext.SaveChangesAsync();

            return true;
        }

        // 個別値のリストを取得（フィルター選択肢用）
        public async Task<List<int>> GetYearsListAsync()
        {
            return await _dbContext.Papers
                .Select(p => p.Year)
                .Distinct()
                .OrderByDescending(y => y)
                .ToListAsync();
        }

        public async Task<List<string>> GetJournalsListAsync()
        {
            return await _dbContext.Papers
                .Select(p => p.Journal)
                .Where(j => !string.IsNullOrEmpty(j))
                .Distinct()
                .OrderBy(j => j)
                .ToListAsync();
        }

        public async Task<List<string>> GetClinicalAreasListAsync()
        {
            // カンマ区切りの値を分割して一意のリストを取得
            var papers = await _dbContext.Papers
                .Select(p => p.ClinicalArea)
                .Where(c => !string.IsNullOrEmpty(c))
                .ToListAsync();

            var areas = new HashSet<string>();
            foreach (var clinicalArea in papers)
            {
                foreach (var area in clinicalArea.Split(','))
                {
                    string trimmed = area.Trim();
                    if (!string.IsNullOrEmpty(trimmed))
                        areas.Add(trimmed);
                }
            }

            return areas.OrderBy(a => a).ToList();
        }

        public async Task<List<string>> GetApproachesListAsync()
        {
            // カンマ区切りの値を分割して一意のリストを取得
            var papers = await _dbContext.Papers
                .Select(p => p.Approach)
                .Where(a => !string.IsNullOrEmpty(a))
                .ToListAsync();

            var approaches = new HashSet<string>();
            foreach (var approach in papers)
            {
                foreach (var item in approach.Split(','))
                {
                    string trimmed = item.Trim();
                    if (!string.IsNullOrEmpty(trimmed))
                        approaches.Add(trimmed);
                }
            }

            return approaches.OrderBy(a => a).ToList();
        }

        public async Task<List<string>> GetTagsListAsync()
        {
            // カンマ区切りの値を分割して一意のリストを取得
            var papers = await _dbContext.Papers
                .Select(p => p.Tags)
                .Where(t => !string.IsNullOrEmpty(t))
                .ToListAsync();

            var tags = new HashSet<string>();
            foreach (var tag in papers)
            {
                foreach (var item in tag.Split(','))
                {
                    string trimmed = item.Trim();
                    if (!string.IsNullOrEmpty(trimmed))
                        tags.Add(trimmed);
                }
            }

            return tags.OrderBy(t => t).ToList();
        }
    }
}
