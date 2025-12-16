using System;
using LuceneSearchWPFApp.Utilities;

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

        /// <summary>
        /// 日期顯示文字（用於 DataGrid 顯示）
        /// </summary>
        public string DateDisplay
        {
            get
            {
                return DateParser.FormatDate(FileDate);
            }
        }

        public override string ToString()
        {
            return $"[{FileName}]-行號[{LineNumber}]: {Content}";
        }

        /// <summary>
        /// 格式化顯示（包含日期）
        /// </summary>
        public string ToStringWithDate()
        {
            string datePrefix = FileDate.HasValue
                ? $"[{FileDate.Value:yyyy-MM-dd}]"
                : "[日期未知]";

            return $"{datePrefix}[{FileName}]-行號[{LineNumber}]: {Content}";
        }
    }
}
