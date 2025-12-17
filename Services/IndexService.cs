using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading;
using Lucene.Net.Analysis.Cn.Smart;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.Store;
using Lucene.Net.Util;
using LuceneSearchWPFApp.Services.Interfaces;
using LuceneSearchWPFApp.Utilities;

namespace LuceneSearchWPFApp.Services
{
    public class IndexService : IIndexService
    {
        private readonly string _indexPath;
        private readonly LuceneVersion _luceneVersion = LuceneVersion.LUCENE_48;
        private readonly IConfigurationService _configurationService;
        private Encoding _encoding; // 緩存 Encoding 對象，避免重複創建

        public IndexService(IConfigurationService configurationService)
        {
            _configurationService = configurationService;
            _indexPath = _configurationService.GetFullIndexPath();

            // 初始化 Encoding（只創建一次）
            var encodingCodePage = _configurationService.GetLogFileEncodingCodePage();
            _encoding = Encoding.GetEncoding(encodingCodePage,
                new EncoderReplacementFallback("?"),
                new DecoderReplacementFallback("?"));
        }

        public async Task ClearIndexAsync()
        {
            await Task.Run(() =>
            {
                if (System.IO.Directory.Exists(_indexPath))
                {
                    try
                    {
                        var dirInfo = new DirectoryInfo(_indexPath);
                        foreach (var file in dirInfo.GetFiles())
                        {
                            file.Delete();
                        }
                    }
                    catch (Exception ex)
                    {
                        throw new IOException($"Failed to clear index: {ex.Message}", ex);
                    }
                }
            });
        }

        // 新增：同步遠端檔案到本機
        public async Task<List<string>> SyncRemoteFilesAsync(string remotePath, string localPath, string fileFilterKeyword, DateTime startDate, DateTime endDate, IProgress<string> progress)
        {
            if (!System.IO.Directory.Exists(remotePath))
            {
                throw new DirectoryNotFoundException($"Remote Directory not found: {remotePath}");
            }

            // 確保本機目錄存在
            if (!System.IO.Directory.Exists(localPath))
            {
                System.IO.Directory.CreateDirectory(localPath);
            }

            return await Task.Run(() =>
            {
                var syncedFiles = new List<string>();
                // 1. 找出遠端有哪些檔案需要同步
                var remoteFiles = GetFilesByDateRange(remotePath, fileFilterKeyword, startDate, endDate);

                foreach (var remoteFile in remoteFiles)
                {
                    string fileName = Path.GetFileName(remoteFile);
                    string localDestPath = Path.Combine(localPath, fileName);

                    try
                    {
                        // 檢查是否需要複製
                        // 簡單邏輯：如果本機檔案不存在，或大小不同，或最後修改時間不同(遠端較新)，就複製
                        // 針對 Log 檔，通常寫入後不變，只比對存在與大小可能就夠了
                        bool needCopy = true;
                        if (File.Exists(localDestPath))
                        {
                            var remoteInfo = new FileInfo(remoteFile);
                            var localInfo = new FileInfo(localDestPath);
                            if (remoteInfo.Length == localInfo.Length && remoteInfo.LastWriteTime == localInfo.LastWriteTime)
                            {
                                needCopy = false;
                            }
                        }

                        if (needCopy)
                        {
                            progress?.Report($"Downloading: {fileName}");
                            File.Copy(remoteFile, localDestPath, true); // Overwrite
                        }

                        syncedFiles.Add(localDestPath);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Failed to sync file {fileName}: {ex.Message}");
                    }
                }

                return syncedFiles;
            });
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
                {
                    var existingFilePaths = new HashSet<string>();

                    if (DirectoryReader.IndexExists(directory))
                    {
                        try
                        {
                            progress?.Report("Checking existing index...");

                            using (var reader = DirectoryReader.Open(directory))
                            {
                                int maxDoc = reader.MaxDoc;
                                progress?.Report($"Reading {maxDoc:N0} existing indexed lines...");

                                // 優化：批次讀取並回報進度
                                int reportInterval = Math.Max(1, maxDoc / 20); // 每 5% 回報一次

                                for (int i = 0; i < maxDoc; i++)
                                {
                                    // 只讀取 FilePath 欄位（不讀取完整 document）
                                    var doc = reader.Document(i, new HashSet<string> { "FilePath" });
                                    string path = doc.Get("FilePath");
                                    if (!string.IsNullOrEmpty(path))
                                    {
                                        existingFilePaths.Add(path);
                                    }

                                    // 進度回報（每 5% 或最後一筆）
                                    if (i % reportInterval == 0 || i == maxDoc - 1)
                                    {
                                        int percent = (int)((i + 1) * 100.0 / maxDoc);
                                        progress?.Report($"Reading existing index... {percent}% ({i + 1:N0}/{maxDoc:N0})");
                                    }
                                }

                                // 統計有多少個唯一檔案
                                progress?.Report($"Found {existingFilePaths.Count:N0} already indexed files");
                            }
                        }
                        catch (Exception ex)
                        {
                            progress?.Report($"Warning: Failed to read existing index, will rebuild all.");
                            Console.WriteLine($"Warning: Failed to read existing index: {ex.Message}.");
                        }
                    }

                    using (var analyzer = new SmartChineseAnalyzer(_luceneVersion))
                    {
                        var config = new IndexWriterConfig(_luceneVersion, analyzer)
                        {
                            OpenMode = OpenMode.CREATE_OR_APPEND,
                            // 優化：大幅增加 RAM 緩衝區（您有 64GB RAM，可以用 1GB）
                            RAMBufferSizeMB = 1024.0
                        };

                        using (var writer = new IndexWriter(directory, config))
                        {
                            // 使用重構後的邏輯獲取目標檔案列表
                            var targetFiles = GetFilesByDateRange(folderPath, fileFilterKeyword, startDate, endDate);

                            // 移除重複
                            targetFiles = targetFiles.Distinct().ToList();

                            // 過濾掉已索引的檔案（這裡只做檢查，不執行實際索引）
                            var filesToCheck = targetFiles.Where(f => !existingFilePaths.Contains(f)).ToList();

                            if (filesToCheck.Count == 0)
                            {
                                progress?.Report("No new files to index.");
                                return;
                            }

                            //關閉writer，我們將使用多個並行 writer
                        }
                    }

                    // 突破性優化：使用多個 IndexWriter 並行分詞！
                    var filesToIndex = GetFilesByDateRange(folderPath, fileFilterKeyword, startDate, endDate)
                        .Distinct()
                        .Where(f => !existingFilePaths.Contains(f))
                        .ToList();

                    int totalFiles = filesToIndex.Count;

                    progress?.Report($"Indexing {totalFiles} files using parallel IndexWriters...");
                    var startTime = System.Diagnostics.Stopwatch.StartNew();

                    // 使用 8 個並行 IndexWriter（充分利用您的 8 核心）
                    int parallelWriters = 8;
                    progress?.Report($"Using {parallelWriters} parallel IndexWriters to maximize CPU usage");

                    // 將檔案分成 N 組
                    var fileGroups = new List<List<string>>();
                    for (int i = 0; i < parallelWriters; i++)
                    {
                        fileGroups.Add(new List<string>());
                    }

                    for (int i = 0; i < filesToIndex.Count; i++)
                    {
                        fileGroups[i % parallelWriters].Add(filesToIndex[i]);
                    }

                    // 創建臨時索引目錄
                    var tempIndexDirs = new List<string>();
                    for (int i = 0; i < parallelWriters; i++)
                    {
                        var tempDir = Path.Combine(_indexPath, $"temp_{i}");
                        tempIndexDirs.Add(tempDir);
                        if (System.IO.Directory.Exists(tempDir))
                        {
                            System.IO.Directory.Delete(tempDir, true);
                        }
                        System.IO.Directory.CreateDirectory(tempDir);
                    }

                    int totalProcessedFiles = 0;
                    object progressLock = new object();

                    // 並行執行多個 IndexWriter
                    Parallel.For(0, parallelWriters, writerIndex =>
                    {
                        var myFiles = fileGroups[writerIndex];
                        if (myFiles.Count == 0) return;

                        var tempDirInfo = new DirectoryInfo(tempIndexDirs[writerIndex]);
                        using (var tempDirectory = FSDirectory.Open(tempDirInfo))
                        using (var tempAnalyzer = new SmartChineseAnalyzer(_luceneVersion))
                        {
                            var tempConfig = new IndexWriterConfig(_luceneVersion, tempAnalyzer)
                            {
                                OpenMode = OpenMode.CREATE,
                                RAMBufferSizeMB = 2048.0 // 每個 writer 用 2GB（總共 16GB）
                            };

                            using (var tempWriter = new IndexWriter(tempDirectory, tempConfig))
                            {
                                foreach (var filePath in myFiles)
                                {
                                    string fileName = Path.GetFileName(filePath);
                                    try
                                    {
                                        var documents = CreateDocumentsFromFile(filePath, fileName);

                                        // 直接寫入（中文分詞在這裡並行執行！）
                                        foreach (var doc in documents)
                                        {
                                            tempWriter.AddDocument(doc);
                                        }

                                        lock (progressLock)
                                        {
                                            totalProcessedFiles++;
                                            if (totalProcessedFiles % 5 == 0 || totalProcessedFiles == totalFiles)
                                            {
                                                double avgTime = startTime.Elapsed.TotalSeconds / totalProcessedFiles;
                                                int remaining = totalFiles - totalProcessedFiles;
                                                int eta = (int)(avgTime * remaining);
                                                progress?.Report($"[{totalProcessedFiles}/{totalFiles}] Writer#{writerIndex} processing {fileName} (ETA: {eta}s)");
                                            }
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        Console.WriteLine($"Error indexing {fileName}: {ex.Message}");
                                    }
                                }

                                progress?.Report($"Writer#{writerIndex} committing {myFiles.Count} files...");
                                tempWriter.Commit();
                            }
                        }
                    });

                    startTime.Stop();
                    progress?.Report($"All writers completed in {startTime.Elapsed.TotalSeconds:F1}s. Merging indexes...");

                    // 合併所有臨時索引到最終索引
                    var mergeStart = System.Diagnostics.Stopwatch.StartNew();

                    using (var finalDirectory = FSDirectory.Open(new DirectoryInfo(_indexPath)))
                    using (var finalAnalyzer = new SmartChineseAnalyzer(_luceneVersion))
                    {
                        var finalConfig = new IndexWriterConfig(_luceneVersion, finalAnalyzer)
                        {
                            OpenMode = OpenMode.CREATE_OR_APPEND,
                            RAMBufferSizeMB = 1024.0
                        };

                        using (var finalWriter = new IndexWriter(finalDirectory, finalConfig))
                        {
                            // 打開所有臨時索引
                            var tempDirectories = tempIndexDirs.Select(dir => FSDirectory.Open(new DirectoryInfo(dir))).ToArray();

                            try
                            {
                                // 合併索引
                                finalWriter.AddIndexes(tempDirectories);
                                finalWriter.Commit();
                            }
                            finally
                            {
                                // 關閉臨時目錄
                                foreach (var tempDir in tempDirectories)
                                {
                                    tempDir.Dispose();
                                }
                            }
                        }
                    }

                    mergeStart.Stop();
                    progress?.Report($"Merge completed in {mergeStart.Elapsed.TotalSeconds:F1}s");

                    // 清理臨時索引目錄
                    foreach (var tempDir in tempIndexDirs)
                    {
                        try
                        {
                            if (System.IO.Directory.Exists(tempDir))
                            {
                                System.IO.Directory.Delete(tempDir, true);
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Warning: Could not delete temp directory {tempDir}: {ex.Message}");
                        }
                    }

                    progress?.Report($"Total time: {startTime.Elapsed.TotalSeconds + mergeStart.Elapsed.TotalSeconds:F1}s");
                }
            });
        }

        // 抽取出來的檔案搜尋邏輯（優化版：一次性讀取，記憶體過濾）
        private List<string> GetFilesByDateRange(string folderPath, string fileFilterKeyword, DateTime startDate, DateTime endDate)
        {
            var targetFiles = new List<string>();

            try
            {
                // 優化：一次性取得所有檔案，避免多次磁碟 I/O
                string searchPattern = string.IsNullOrEmpty(fileFilterKeyword)
                    ? "*"
                    : $"*{fileFilterKeyword}*";

                var allFiles = System.IO.Directory.GetFiles(folderPath, searchPattern);

                // 在記憶體中過濾符合日期範圍的檔案
                foreach (var filePath in allFiles)
                {
                    string fileName = Path.GetFileName(filePath);

                    // 檢查副檔名（支援 .txt、.txt20251214、.log、.log20251214 等格式）
                    // 因為 Path.GetExtension("XXX.txt20251214") 會回傳 ".txt20251214"
                    // 所以需要檢查檔名是否包含 .txt 或 .log
                    bool isLogFile = fileName.Contains(".txt", StringComparison.OrdinalIgnoreCase) ||
                                     fileName.Contains(".log", StringComparison.OrdinalIgnoreCase);

                    if (!isLogFile)
                    {
                        continue;
                    }

                    // 解析檔案日期（DateParser 已經支援 .txt20251214 格式）
                    var fileDate = DateParser.ParseDateFromFileName(fileName);

                    // 如果無法解析日期，使用檔案最後修改時間
                    if (fileDate == null)
                    {
                        fileDate = File.GetLastWriteTime(filePath).Date;
                    }

                    // 檢查是否在日期範圍內
                    if (fileDate.HasValue &&
                        fileDate.Value.Date >= startDate.Date &&
                        fileDate.Value.Date <= endDate.Date)
                    {
                        targetFiles.Add(filePath);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error listing files: {ex.Message}");
            }

            return targetFiles;
        }

        // 從單一檔案創建文檔列表（生產者-消費者模式）
        private List<Document> CreateDocumentsFromFile(string filePath, string fileName)
        {
            var documents = new List<Document>();

            var fileDate = DateParser.ParseDateFromFileName(fileName);
            if (fileDate == null)
            {
                fileDate = File.GetLastWriteTime(filePath);
            }

            string fileDateStr = fileDate?.ToString("yyyyMMdd") ?? "";

            // 使用 StreamReader 搭配大緩衝區（128KB）以提升讀取速度
            using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 131072))
            using (var reader = new StreamReader(fileStream, _encoding, detectEncodingFromByteOrderMarks: false, bufferSize: 131072))
            {
                string lineContent;
                int lineIndex = 0;

                while ((lineContent = reader.ReadLine()) != null)
                {
                    if (string.IsNullOrWhiteSpace(lineContent))
                    {
                        lineIndex++;
                        continue;
                    }

                    // 只對可能包含時間戳的行提取
                    var timestamp = DateParser.ExtractTimestampFromLog(lineContent);

                    // 優化：限制分詞內容長度（中文分詞很慢，只處理前 100 字）
                    // 大部分日誌的關鍵信息都在前面
                    string contentForTokenization = lineContent.Length > 100
                        ? lineContent.Substring(0, 100)
                        : lineContent;

                    var doc = new Document
                    {
                        new StringField("FileName", fileName, Field.Store.YES),
                        new StringField("FilePath", filePath, Field.Store.YES),
                        new StringField("LineNumber", lineIndex.ToString(), Field.Store.YES),
                        new StoredField("Content", lineContent),
                        new StringField("FileDate", fileDateStr, Field.Store.YES),
                        new StringField("LogTimestamp", timestamp ?? "", Field.Store.YES),
                        new TextField("TokenizedContent", contentForTokenization, Field.Store.NO)
                    };

                    documents.Add(doc);
                    lineIndex++;
                }
            }

            return documents;
        }
    }
}