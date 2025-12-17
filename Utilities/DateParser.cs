using System;
using System.Text.RegularExpressions;

namespace LuceneSearchWPFApp.Utilities
{
    /// <summary>
    /// 日期解析工具
    /// </summary>
    public static class DateParser
    {
        // 使用靜態編譯的 Regex 以提升性能（避免重複創建對象）
        // 支援兩種格式：.txt20251214 (無點) 和 .log20251214
        private static readonly Regex FileNameDateRegex = new Regex(@"\.(txt|log)(\d{8})$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // 時間戳記正則表達式（靜態編譯）
        private static readonly Regex TimestampRegex1 = new Regex(@"^(\d{4}-\d{2}-\d{2}\s+\d{2}:\d{2}:\d{2})", RegexOptions.Compiled);
        private static readonly Regex TimestampRegex2 = new Regex(@"^(\d{4}/\d{2}/\d{2}\s+\d{2}:\d{2}:\d{2})", RegexOptions.Compiled);
        private static readonly Regex TimestampRegex3 = new Regex(@"^(\d{8}\s+\d{2}:\d{2}:\d{2})", RegexOptions.Compiled);
        private static readonly Regex TimestampRegex4 = new Regex(@"^(\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2})", RegexOptions.Compiled);

        /// <summary>
        /// 從檔名解析日期
        /// 支援格式：
        /// - AB1B-LAWLIET_Escalator.txt20251214 → 2025-12-14 (無點)
        /// - AB1B-LAWLIET_Escalator.log20251214 → 2025-12-14 (無點)
        /// - AB1B-LAWLIET_Escalator.txt → DateTime.Today (當天檔案)
        /// </summary>
        public static DateTime? ParseDateFromFileName(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                return null;

            // 解析歷史檔案日期：檔名.txt20251214 或 檔名.log20251214
            var match = FileNameDateRegex.Match(fileName);
            if (match.Success)
            {
                string dateStr = match.Groups[2].Value; // 例如：20251214

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

            // 如果沒有匹配到日期，檢查是否為當天檔案（以 .txt 或 .log 結尾，後面沒有數字）
            if (fileName.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) ||
                fileName.EndsWith(".log", StringComparison.OrdinalIgnoreCase))
            {
                // 當天檔案，使用今天日期
                return DateTime.Today;
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

            // 快速檢查：如果行首不是數字，直接返回 null（避免不必要的正則匹配）
            if (logLine.Length < 4 || !char.IsDigit(logLine[0]))
                return null;

            // 使用靜態編譯的 Regex（按最常見的格式順序檢查）
            Match match;

            match = TimestampRegex1.Match(logLine);
            if (match.Success) return match.Groups[1].Value;

            match = TimestampRegex2.Match(logLine);
            if (match.Success) return match.Groups[1].Value;

            match = TimestampRegex3.Match(logLine);
            if (match.Success) return match.Groups[1].Value;

            match = TimestampRegex4.Match(logLine);
            if (match.Success) return match.Groups[1].Value;

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
