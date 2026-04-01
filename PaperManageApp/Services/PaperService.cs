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
                    (p.AdditionalNotes != null && p.AdditionalNotes.ToLower().Contains(searchText))
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

        // セクションのみ更新
        public async Task<Paper> UpdatePaperSectionAsync(int paperId, string sectionName, string content)
        {
            var paper = await _dbContext.Papers.FindAsync(paperId);
            if (paper == null) throw new KeyNotFoundException($"ID: {paperId} の論文が見つかりません");

            switch (sectionName.ToLower())
            {
                case "abstract":      paper.Abstract = content; break;
                case "additionalnotes": paper.AdditionalNotes = content; break;
                default: throw new ArgumentException($"不明なセクション名: {sectionName}");
            }

            paper.UpdatedAt = DateTime.Now;
            await _dbContext.SaveChangesAsync();
            return paper;
        }

        // お気に入り状態を指定値にセット
        public async Task SetFavoriteAsync(int paperId, bool isFavorite)
        {
            var paper = await _dbContext.Papers.FindAsync(paperId);
            if (paper == null) throw new KeyNotFoundException($"ID: {paperId} の論文が見つかりません");
            paper.IsFavorite = isFavorite;
            paper.UpdatedAt = DateTime.Now;
            await _dbContext.SaveChangesAsync();
        }

        // 複数論文のお気に入り状態を指定値にセット
        public async Task SetFavoritesAsync(IEnumerable<int> paperIds, bool isFavorite)
        {
            var ids = paperIds.ToList();
            var papers = await _dbContext.Papers
                .Where(p => ids.Contains(p.Id))
                .ToListAsync();
            var now = DateTime.Now;
            foreach (var paper in papers)
            {
                paper.IsFavorite = isFavorite;
                paper.UpdatedAt = now;
            }
            await _dbContext.SaveChangesAsync();
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
            var ids = paperIds.ToList();
            var papers = await _dbContext.Papers
                .Where(p => ids.Contains(p.Id))
                .ToListAsync();
            var now = DateTime.Now;
            foreach (var paper in papers)
            {
                paper.IsFavorite = !paper.IsFavorite;
                paper.UpdatedAt = now;
            }
            await _dbContext.SaveChangesAsync();
        }

        // 複数論文の一括削除
        public async Task<int> DeletePapersAsync(IEnumerable<int> paperIds)
        {
            var ids = paperIds.ToList();
            var papers = await _dbContext.Papers
                .Where(p => ids.Contains(p.Id))
                .ToListAsync();

            foreach (var paper in papers)
            {
                if (!string.IsNullOrEmpty(paper.FilePath) && File.Exists(paper.FilePath))
                {
                    try { File.Delete(paper.FilePath); }
                    catch (Exception ex) { Console.WriteLine($"PDFファイルの削除に失敗しました: {ex.Message}"); }
                }
                _dbContext.Papers.Remove(paper);
            }

            if (papers.Count > 0)
                await _dbContext.SaveChangesAsync();

            return papers.Count;
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

        public Task<List<string>> GetClinicalAreasListAsync()
            => GetDistinctCommaSeparatedValuesAsync(p => p.ClinicalArea);

        public Task<List<string>> GetApproachesListAsync()
            => GetDistinctCommaSeparatedValuesAsync(p => p.Approach);

        public Task<List<string>> GetTagsListAsync()
            => GetDistinctCommaSeparatedValuesAsync(p => p.Tags);

        // カンマ区切りフィールドから一意の値リストを取得する共通処理
        private async Task<List<string>> GetDistinctCommaSeparatedValuesAsync(
            System.Linq.Expressions.Expression<Func<Paper, string>> selector)
        {
            var values = await _dbContext.Papers
                .Select(selector)
                .Where(v => !string.IsNullOrEmpty(v))
                .ToListAsync();

            return values
                .SelectMany(v => v.Split(','))
                .Select(v => v.Trim())
                .Where(v => !string.IsNullOrEmpty(v))
                .Distinct()
                .OrderBy(v => v)
                .ToList();
        }
    }
}
