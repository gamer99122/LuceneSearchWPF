using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using LuceneSearchWPFApp.Services.Interfaces; // 引入介面

namespace LuceneSearchWPFApp.Services
{
    // 公開的設定類別，可以在 ConfigurationService 內部使用，也可以在外部需要時直接訪問其結構
    // 但載入和實例化應由 ConfigurationService 處理
    public class LuceneSettings
    {
        public string IndexPath { get; set; }
        public int MaxSearchResults { get; set; }

        public string GetFullIndexPath()
        {
            if (Path.IsPathRooted(IndexPath))
                return IndexPath;

            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, IndexPath);
        }
    }

    public class LogFileSettings
    {
        public string DefaultEncoding { get; set; }
        public int EncodingCodePage { get; set; }
        public bool AutoDetectEncoding { get; set; }
    }

    public class UISettings
    {
        public string DefaultLogPath { get; set; }
        public string DefaultFileFilter { get; set; }
        public List<string> FileFilterOptions { get; set; }

        public UISettings()
        {
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

    public class EditorSettings
    {
        public string Type { get; set; }
        public string ExecutablePath { get; set; }
        public string CommandLineFormat { get; set; }
        public bool FallbackToNotepad { get; set; }

        public EditorSettings()
        {
            Type = "notepad++";
            ExecutablePath = @"V:\Program Files\Notepad++\notepad++.exe";
            CommandLineFormat = "\"{ExecutablePath}\" -n{LineNumber} \"{FilePath}\"";
            FallbackToNotepad = true;
        }
    }

    // 專門用於載入 appsettings.json 的內部結構
    internal class AppSettingsModel
    {
        public LuceneSettings Lucene { get; set; }
        public LogFileSettings LogFiles { get; set; }
        public UISettings UI { get; set; }
        public EditorSettings Editor { get; set; }
    }

    public class ConfigurationService : IConfigurationService
    {
        private AppSettingsModel _appSettings;

        public ConfigurationService()
        {
            _appSettings = LoadSettings();
        }

        private AppSettingsModel LoadSettings()
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
                var settings = JsonSerializer.Deserialize<AppSettingsModel>(jsonContent, new JsonSerializerOptions
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

        private AppSettingsModel GetDefaultSettings()
        {
            return new AppSettingsModel
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
                    DefaultLogPath = AppDomain.CurrentDomain.BaseDirectory, // 預設為程式執行目錄，具體路徑請由 appsettings.json 設定
                    //DefaultFileFilter = "ICUploadXML"
                },
                Editor = new EditorSettings()
            };
        }

        // IConfigurationService 介面實作
        public string GetFullIndexPath() => _appSettings.Lucene.GetFullIndexPath();
        public int GetMaxSearchResults() => _appSettings.Lucene.MaxSearchResults;
        public int GetLogFileEncodingCodePage() => _appSettings.LogFiles.EncodingCodePage;

        // 添加其他需要的配置獲取方法
        public UISettings GetUISettings() => _appSettings.UI;
        public EditorSettings GetEditorSettings() => _appSettings.Editor;
        public LogFileSettings GetLogFileSettings() => _appSettings.LogFiles;
    }
}
