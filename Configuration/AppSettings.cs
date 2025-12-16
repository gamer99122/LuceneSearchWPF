using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace LuceneSearchWPFApp.Configuration
{
    /// <summary>
    /// 應用程式設定
    /// </summary>
    public class AppSettings
    {
        public LuceneSettings Lucene { get; set; }
        public LogFileSettings LogFiles { get; set; }
        public UISettings UI { get; set; }
        public EditorSettings Editor { get; set; }

        private static AppSettings _instance;
        private static readonly object _lock = new object();

        /// <summary>
        /// 取得設定單例
        /// </summary>
        public static AppSettings Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = LoadSettings();
                        }
                    }
                }
                return _instance;
            }
        }

        /// <summary>
        /// 從 appsettings.json 載入設定
        /// </summary>
        private static AppSettings LoadSettings()
        {
            try
            {
                string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");

                if (!File.Exists(configPath))
                {
                    Console.WriteLine($"設定檔不存在: {configPath}，使用預設值");
                    return GetDefaultSettings();
                }

                string jsonContent = File.ReadAllText(configPath);
                var settings = JsonSerializer.Deserialize<AppSettings>(jsonContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                Console.WriteLine("成功載入設定檔");
                return settings ?? GetDefaultSettings();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"載入設定檔失敗: {ex.Message}，使用預設值");
                return GetDefaultSettings();
            }
        }

        /// <summary>
        /// 取得預設設定
        /// </summary>
        private static AppSettings GetDefaultSettings()
        {
            return new AppSettings
            {
                Lucene = new LuceneSettings
                {
                    IndexPath = "LuceneIndex",
                    MaxSearchResults = 500
                },
                LogFiles = new LogFileSettings
                {
                    DefaultEncoding = "big5",
                    EncodingCodePage = 950,
                    AutoDetectEncoding = false
                },
                UI = new UISettings
                {
                    DefaultLogPath = "C:\\HIS2\\log",
                    DefaultFileFilter = "ICUploadXML"
                },
                Editor = new EditorSettings()
            };
        }
    }

    /// <summary>
    /// Lucene 相關設定
    /// </summary>
    public class LuceneSettings
    {
        public string IndexPath { get; set; }
        public int MaxSearchResults { get; set; }

        /// <summary>
        /// 取得完整索引路徑
        /// </summary>
        public string GetFullIndexPath()
        {
            if (Path.IsPathRooted(IndexPath))
                return IndexPath;

            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, IndexPath);
        }
    }

    /// <summary>
    /// Log 檔案相關設定
    /// </summary>
    public class LogFileSettings
    {
        public string DefaultEncoding { get; set; }
        public int EncodingCodePage { get; set; }
        public bool AutoDetectEncoding { get; set; }
    }

    /// <summary>
    /// UI 相關設定
    /// </summary>
    public class UISettings
    {
        public string DefaultLogPath { get; set; }
        public string DefaultFileFilter { get; set; }
        public List<string> FileFilterOptions { get; set; }

        public UISettings()
        {
            // 預設 Filter 選項
            FileFilterOptions = new List<string>
            {
                "全部",
                "Escalator",
                "ICUploadXML",
                "Error",
                "Warning"
            };
        }
    }

    /// <summary>
    /// 編輯器相關設定
    /// </summary>
    public class EditorSettings
    {
        public string Type { get; set; }
        public string ExecutablePath { get; set; }
        public string CommandLineFormat { get; set; }
        public bool FallbackToNotepad { get; set; }

        public EditorSettings()
        {
            Type = "notepad++";
            ExecutablePath = @"C:\Program Files\Notepad++\notepad++.exe";
            CommandLineFormat = "\"{ExecutablePath}\" -n{LineNumber} \"{FilePath}\"";
            FallbackToNotepad = true;
        }
    }
}
