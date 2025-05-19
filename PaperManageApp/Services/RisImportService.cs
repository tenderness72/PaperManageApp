using PaperManagementApp.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
namespace PaperManagementApp.Services
{
    public class RisImportService
    {
        private readonly PaperService _paperService;
        public RisImportService()
        {
            _paperService = new PaperService();
        }
        /// <summary>
        /// RISファイルから論文情報を読み込む
        /// </summary>
        /// <param name="filePath">RISファイルのパス</param>
        /// <returns>読み込まれた論文リスト</returns>
        public List<Paper> ImportFromRisFile(string filePath)
        {
            List<Paper> papers = new List<Paper>();
            string[] lines = File.ReadAllLines(filePath, Encoding.UTF8);
            Dictionary<string, string> currentEntry = new Dictionary<string, string>();
            string lastTag = string.Empty;
            foreach (string line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;
                // エントリーの終了を検出
                if (line.StartsWith("ER  -"))
                {
                    if (currentEntry.Count > 0)
                    {
                        Paper paper = ConvertRisToPaper(currentEntry);
                        papers.Add(paper);
                        currentEntry = new Dictionary<string, string>();
                    }
                    continue;
                }
                // タグと値のペアを抽出
                Match tagMatch = Regex.Match(line, @"^([A-Z][A-Z0-9])  - (.*)$");
                if (tagMatch.Success)
                {
                    string tag = tagMatch.Groups[1].Value;
                    string value = tagMatch.Groups[2].Value.Trim();
                    lastTag = tag;
                    if (currentEntry.ContainsKey(tag))
                    {
                        // 同じタグが既に存在する場合は値を追加（複数著者など）
                        // andで連結
                        currentEntry[tag] += " and " + value;
                    }
                    else
                    {
                        currentEntry[tag] = value;
                    }
                }
                else if (line.StartsWith("  "))
                {
                    // インデントされた行は前のタグの続きと見なす
                    string value = line.Trim();
                    if (!string.IsNullOrEmpty(lastTag) && !string.IsNullOrEmpty(value))
                    {
                        currentEntry[lastTag] += " " + value;
                    }
                }
            }
            // 最後のエントリがER終了タグなしで終わる場合
            if (currentEntry.Count > 0)
            {
                Paper paper = ConvertRisToPaper(currentEntry);
                papers.Add(paper);
            }
            return papers;
        }
        /// <summary>
        /// RISデータをPaperオブジェクトに変換
        /// </summary>
        private Paper ConvertRisToPaper(Dictionary<string, string> risData)
        {
            Paper paper = new Paper();
            // 基本情報の設定
            if (risData.ContainsKey("TI"))
                paper.Title = risData["TI"];
            // 著者情報の処理
            if (risData.ContainsKey("AU"))
            {
                // 複数の区切り文字で分割
                string[] authors = risData["AU"].Split(new[] { " and ", ";" }, StringSplitOptions.RemoveEmptyEntries);

                // 各著者の名前を処理し、最後にピリオドをつける
                List<string> formattedAuthors = new List<string>();
                foreach (var author in authors)
                {
                    string trimmedAuthor = author.Trim();
                    // ピリオドをつける処理
                    if (!trimmedAuthor.EndsWith("."))
                    {
                        formattedAuthors.Add(trimmedAuthor + ".");
                    }
                    else
                    {
                        formattedAuthors.Add(trimmedAuthor);
                    }
                }

                // スペースで区切って連結（カンマはつけない）
                paper.Authors = string.Join(" ", formattedAuthors);
            }
            // 出版年
            if (risData.ContainsKey("PY"))
            {
                if (int.TryParse(risData["PY"], out int year))
                {
                    paper.Year = year;
                }
            }
            // 雑誌名（複数のタグから取得可能）
            if (risData.ContainsKey("JO"))
                paper.Journal = risData["JO"];
            else if (risData.ContainsKey("T2"))
                paper.Journal = risData["T2"];
            else if (risData.ContainsKey("JA"))
                paper.Journal = risData["JA"];
            else if (risData.ContainsKey("J1"))
                paper.Journal = risData["J1"];
            // 巻・号
            string volume = string.Empty;
            string issue = string.Empty;
            if (risData.ContainsKey("VL"))
                volume = risData["VL"];
            if (risData.ContainsKey("IS"))
                issue = risData["IS"];
            if (!string.IsNullOrEmpty(volume) && !string.IsNullOrEmpty(issue))
                paper.Volume = $"{volume}({issue})";
            else if (!string.IsNullOrEmpty(volume))
                paper.Volume = volume;
            else if (!string.IsNullOrEmpty(issue))
                paper.Volume = $"({issue})";
            // ページ
            string startPage = string.Empty;
            string endPage = string.Empty;
            if (risData.ContainsKey("SP"))
                startPage = risData["SP"];
            if (risData.ContainsKey("EP"))
                endPage = risData["EP"];
            if (!string.IsNullOrEmpty(startPage) && !string.IsNullOrEmpty(endPage))
                paper.Pages = $"{startPage}-{endPage}";
            else if (!string.IsNullOrEmpty(startPage))
                paper.Pages = startPage;
            // DOI
            if (risData.ContainsKey("DO"))
                paper.DOI = risData["DO"];
            // URL
            if (risData.ContainsKey("UR"))
            {
                // URLはメモに保存
                string url = risData["UR"];
                if (!string.IsNullOrEmpty(url))
                {
                    paper.AdditionalNotes = $"URL: {url}";
                }
            }
            // 論文タイプの判定
            if (risData.ContainsKey("TY"))
            {
                switch (risData["TY"])
                {
                    case "JOUR":
                        paper.PaperType = "研究論文";
                        break;
                    case "CASE":
                        paper.PaperType = "症例報告";
                        break;
                    case "REVW":
                        paper.PaperType = "レビュー";
                        break;
                    case "MGZN":
                        paper.PaperType = "解説";
                        break;
                    default:
                        paper.PaperType = "その他";
                        break;
                }
            }
            // キーワード
            if (risData.ContainsKey("KW"))
            {
                paper.Keywords = risData["KW"];
            }
            // 作成日時の設定
            paper.CreatedAt = DateTime.Now;
            paper.UpdatedAt = DateTime.Now;
            // ファイルパスを空にする（NOT NULL制約対応）
            paper.FilePath = "";
            return paper;
        }
        /// <summary>
        /// 論文をデータベースに追加
        /// </summary>
        public void SaveImportedPaper(Paper paper)
        {
            _paperService.AddPaper(paper);
        }
        /// <summary>
        /// 複数の論文をデータベースに追加
        /// </summary>
        public void SaveImportedPapers(List<Paper> papers)
        {
            foreach (var paper in papers)
            {
                _paperService.AddPaper(paper);
            }
        }
    }
}