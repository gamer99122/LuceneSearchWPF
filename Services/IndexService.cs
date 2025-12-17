using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Lucene.Net.Analysis.Cn.Smart;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.Store;
using Lucene.Net.Util;
using LuceneSearchWPFApp.Services.Interfaces; // 引入介面
//using LuceneSearchWPFApp.Configuration; // 移除對舊 AppSettings 的引用
using LuceneSearchWPFApp.Utilities;

namespace LuceneSearchWPFApp.Services
{
    public class IndexService : IIndexService // 實現 IIndexService 介面
    {
        private readonly string _indexPath;
        private readonly LuceneVersion _luceneVersion = LuceneVersion.LUCENE_48;
        private readonly IConfigurationService _configurationService; // 注入配置服務

        // 透過建構子注入 IConfigurationService
        public IndexService(IConfigurationService configurationService)
        {
            _configurationService = configurationService;
            _indexPath = _configurationService.GetFullIndexPath(); // 從配置服務獲取路徑
        }

        public async Task CreateIndexAsync(string folderPath, string fileFilterKeyword, DateTime startDate, DateTime endDate, IProgress<string> progress)
        {
            if (!System.IO.Directory.Exists(folderPath))
            {
                throw new DirectoryNotFoundException($"Directory not found: {folderPath}");
            }

            await Task.Run(() =>
            {
                var dirInfo = new DirectoryInfo(_indexPath);
                if (!dirInfo.Exists) dirInfo.Create();

                using (var directory = FSDirectory.Open(dirInfo))
                using (var analyzer = new SmartChineseAnalyzer(_luceneVersion))
                {
                    var config = new IndexWriterConfig(_luceneVersion, analyzer)
                    {
                        OpenMode = OpenMode.CREATE // 每次重建索引都清空舊資料，避免重複
                    };

                    using (var writer = new IndexWriter(directory, config))
                    {
                        // 這是核心修改：不再使用 Directory.GetFiles
                        // 而是根據日期範圍主動生成路徑
                        var targetFiles = new System.Collections.Generic.List<string>();
                        var currentDate = startDate.Date;
                        var today = DateTime.Today;

                        // 定義可能的副檔名
                        var extensions = new[] { ".txt", ".log" };

                        while (currentDate <= endDate.Date)
                        {
                            string dateSuffix = currentDate.ToString("yyyyMMdd");
                            
                            // 如果 fileFilterKeyword 為空，我們無法猜測檔名，這時可能還是得退回到 GetFiles，
                            // 但為了效能，我們假設使用者一定會選 Filter。如果沒選，就只抓標準的 Log 檔名 (視需求而定)
                            // 這裡假設 fileFilterKeyword 是必填的或是主要的檔名前綴
                            
                            if (string.IsNullOrEmpty(fileFilterKeyword))
                            {
                                // 如果沒有關鍵字，這個優化策略會失效，因為我們不知道要檢查什麼檔名。
                                // 為了安全起見，如果沒有 keyword，我們還是退回到原本的 GetFiles 邏輯，或者拋出警告。
                                // 這裡我們先保留一個簡單的 fallback：不優化，直接掃描 (只針對這一天? 很難)。
                                // 鑑於效能考量，我們強烈建議使用者選擇 Filter。
                                // 在此實作中，若無 Keyword，則無法進行「預測」，只能略過或抓全部 (極慢)。
                                // 我們選擇：如果沒有 Keyword，則執行舊邏輯 (為了相容性)，但只做一次。
                                var allFiles = System.IO.Directory.GetFiles(folderPath);
                                targetFiles.AddRange(allFiles);
                                break; // 既然全抓了，就不用跑日期迴圈了
                            }
                            else
                            {
                                foreach (var ext in extensions)
                                {
                                    // 1. 檢查歷史檔名格式: Name.extYYYYMMDD
                                    string historyFileName = $"{fileFilterKeyword}{ext}{dateSuffix}";
                                    string historyPath = Path.Combine(folderPath, historyFileName);
                                    if (File.Exists(historyPath))
                                    {
                                        targetFiles.Add(historyPath);
                                    }

                                    // 2. 如果這一天是「今天」，也要檢查當日檔名: Name.ext (無日期後綴)
                                    if (currentDate == today)
                                    {
                                        string currentFileName = $"{fileFilterKeyword}{ext}";
                                        string currentPath = Path.Combine(folderPath, currentFileName);
                                        // 避免重複加入 (雖然邏輯上不太會重疊，除非日期後綴就是空)
                                        if (File.Exists(currentPath) && !targetFiles.Contains(currentPath))
                                        {
                                            targetFiles.Add(currentPath);
                                        }
                                    }
                                }
                            }

                            currentDate = currentDate.AddDays(1);
                        }

                        // 移除重複 (保險起見)
                        targetFiles = targetFiles.Distinct().ToList();

                        foreach (var filePath in targetFiles)
                        {
                            string fileName = Path.GetFileName(filePath);
                            progress?.Report(fileName);

                            try
                            {
                                int lineIndex = 0;

                                var fileDate = DateParser.ParseDateFromFileName(fileName);
                                // 如果檔名解析不出日期 (例如當日檔)，則使用檔案的最後修改時間或創建時間作為備案，
                                // 或者根據我們剛剛的迴圈邏輯，其實我們知道它是哪一天的。
                                // 但為了保持一致性，如果 Parse 失敗，我們試著用檔案系統時間。
                                if (fileDate == null)
                                {
                                    fileDate = File.GetLastWriteTime(filePath);
                                }
                                
                                string fileDateStr = fileDate?.ToString("yyyyMMdd") ?? "";

                                var encodingCodePage = _configurationService.GetLogFileEncodingCodePage(); // 從配置服務獲取編碼頁
                                var encoding = Encoding.GetEncoding(encodingCodePage,
                                    new EncoderReplacementFallback("?"),
                                    new DecoderReplacementFallback("?"));

                                foreach (var lineContent in File.ReadLines(filePath, encoding))
                                {
                                    if (string.IsNullOrWhiteSpace(lineContent))
                                    {
                                        lineIndex++;
                                        continue;
                                    }

                                    var timestamp = DateParser.ExtractTimestampFromLog(lineContent);

                                    var doc = new Document
                                    {
                                        new StringField("FileName", fileName, Field.Store.YES),
                                        new StringField("FilePath", filePath, Field.Store.YES),
                                        new StringField("LineNumber", lineIndex.ToString(), Field.Store.YES),
                                        new StoredField("Content", lineContent),
                                        new StringField("FileDate", fileDateStr, Field.Store.YES),
                                        new StringField("LogTimestamp", timestamp ?? "", Field.Store.YES),
                                        new TextField("TokenizedContent", lineContent, Field.Store.NO)
                                    };

                                    writer.AddDocument(doc);
                                    lineIndex++;
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Error indexing {fileName}: {ex.Message}");
                            }
                        }
                        
                        writer.Commit();
                    }
                }
            });
        }
    }
}