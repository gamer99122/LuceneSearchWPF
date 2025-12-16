using System;
using System.Text.RegularExpressions;

namespace LuceneSearchWPFApp.Utilities
{
    /// <summary>
    /// 日期解析工具
    /// </summary>
    public static class DateParser
    {
        /// <summary>
        /// 從檔名解析日期
        /// 支援格式：
        /// - AB1B-LAWLIET_Escalator.txt.20251214 → 2025-12-14
        /// - AB1B-LAWLIET_Escalator.txt → DateTime.Today
        /// </summary>
        public static DateTime? ParseDateFromFileName(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                return null;

            // 檢查是否為當天檔案（沒有日期後綴）
            if (!fileName.Contains(".txt."))
            {
                // 當天檔案，使用今天日期
                return DateTime.Today;
            }

            // 解析歷史檔案日期：檔名.txt.YYYYMMDD
            // 使用正則表達式匹配 8 位數字日期
            var match = Regex.Match(fileName, @"\.txt\.(\d{8})$");
            if (match.Success)
            {
                string dateStr = match.Groups[1].Value; // 例如：20251214

                if (dateStr.Length == 8 &&
                    int.TryParse(dateStr.Substring(0, 4), out int year) &&
                    int.TryParse(dateStr.Substring(4, 2), out int month) &&
                    int.TryParse(dateStr.Substring(6, 2), out int day))
                {
                    try
                    {
                        return new DateTime(year, month, day);
                    }
                    catch
                    {
                        // 日期無效
                        return null;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// 從 log 內容解析時間戳記
        /// 支援常見格式：
        /// - 2025-01-15 10:30:45
        /// - 2025/01/15 10:30:45
        /// - 20250115 10:30:45
        /// </summary>
        public static string ExtractTimestampFromLog(string logLine)
        {
            if (string.IsNullOrEmpty(logLine))
                return null;

            // 嘗試匹配常見的時間戳記格式
            var patterns = new[]
            {
                @"^(\d{4}-\d{2}-\d{2}\s+\d{2}:\d{2}:\d{2})",  // 2025-01-15 10:30:45
                @"^(\d{4}/\d{2}/\d{2}\s+\d{2}:\d{2}:\d{2})",  // 2025/01/15 10:30:45
                @"^(\d{8}\s+\d{2}:\d{2}:\d{2})",              // 20250115 10:30:45
                @"^(\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2})",    // 2025-01-15T10:30:45 (ISO)
            };

            foreach (var pattern in patterns)
            {
                var match = Regex.Match(logLine, pattern);
                if (match.Success)
                {
                    return match.Groups[1].Value;
                }
            }

            return null;
        }

        /// <summary>
        /// 將日期格式化為顯示字串
        /// </summary>
        public static string FormatDate(DateTime? date)
        {
            if (!date.HasValue)
                return "日期未知";

            if (date.Value.Date == DateTime.Today)
                return "今天";

            if (date.Value.Date == DateTime.Today.AddDays(-1))
                return "昨天";

            return date.Value.ToString("yyyy-MM-dd");
        }
    }
}
