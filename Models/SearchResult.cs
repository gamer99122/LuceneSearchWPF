using System;
// Removed LuceneSearchWPFApp.Utilities as it's no longer needed for DateDisplay

namespace LuceneSearchWPFApp.Models
{
    public class SearchResult
    {
        public string FileName { get; set; }
        public string LineNumber { get; set; }
        public string Content { get; set; }

        /// <summary>
        /// 從檔名解析出的日期（例如：20251214 → 2025-12-14）
        /// </summary>
        public DateTime? FileDate { get; set; }

        /// <summary>
        /// 從 log 內容解析出的時間戳記（如果有的話）
        /// </summary>
        public string LogTimestamp { get; set; }

        /// <summary>
        /// 完整檔案路徑（用於開啟檔案）
        /// </summary>
        public string FilePath { get; set; }

        public override string ToString()
        {
            return $"[{FileName}]-行號[{LineNumber}]: {Content}";
        }
    }
}
