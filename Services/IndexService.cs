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
using LuceneSearchWPFApp.Configuration;
using LuceneSearchWPFApp.Utilities;

namespace LuceneSearchWPFApp.Services
{
    public class IndexService
    {
        private readonly string _indexPath;
        private readonly LuceneVersion _luceneVersion = LuceneVersion.LUCENE_48;

        public IndexService()
        {
            _indexPath = AppSettings.Instance.Lucene.GetFullIndexPath();
        }

        public async Task CreateIndexAsync(string folderPath, string fileFilterKeyword, IProgress<string> progress)
        {
            if (!System.IO.Directory.Exists(folderPath))
            {
                throw new DirectoryNotFoundException($"Directory not found: {folderPath}");
            }

            await Task.Run(() =>
            {
                // Ensure index directory exists
                var dirInfo = new DirectoryInfo(_indexPath);
                if (!dirInfo.Exists) dirInfo.Create();

                using (var directory = FSDirectory.Open(dirInfo))
                using (var analyzer = new SmartChineseAnalyzer(_luceneVersion))
                {
                    // Create an IndexWriterConfig with SmartChineseAnalyzer
                    var config = new IndexWriterConfig(_luceneVersion, analyzer)
                    {
                        OpenMode = OpenMode.CREATE_OR_APPEND // 保留舊索引，新增或更新文件
                    };

                    using (var writer = new IndexWriter(directory, config))
                    {
                        var files = System.IO.Directory.GetFiles(folderPath);
                        var targetFiles = string.IsNullOrEmpty(fileFilterKeyword)
                            ? files
                            : files.Where(f => Path.GetFileName(f).Contains(fileFilterKeyword, StringComparison.OrdinalIgnoreCase)).ToArray();

                        foreach (var filePath in targetFiles)
                        {
                            string fileName = Path.GetFileName(filePath);
                            progress?.Report(fileName);

                            try
                            {
                                int lineIndex = 0;

                                // 解析檔名中的日期
                                var fileDate = DateParser.ParseDateFromFileName(fileName);
                                string fileDateStr = fileDate?.ToString("yyyyMMdd") ?? "";

                                // 使用設定檔中的編碼設定
                                var encodingCodePage = AppSettings.Instance.LogFiles.EncodingCodePage;
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

                                    // 嘗試從 log 內容提取時間戳記
                                    var timestamp = DateParser.ExtractTimestampFromLog(lineContent);

                                    var doc = new Document
                                    {
                                        // Store file name (Stored, Not Analyzed)
                                        new StringField("FileName", fileName, Field.Store.YES),

                                        // Store full file path (Stored, Not Analyzed)
                                        new StringField("FilePath", filePath, Field.Store.YES),

                                        // Store line number (Stored, Not Analyzed)
                                        new StringField("LineNumber", lineIndex.ToString(), Field.Store.YES),

                                        // Store content for display (Stored, Not Analyzed)
                                        new StoredField("Content", lineContent),

                                        // Store file date (Stored, Indexed for range queries)
                                        new StringField("FileDate", fileDateStr, Field.Store.YES),

                                        // Store timestamp from log content (if exists)
                                        new StringField("LogTimestamp", timestamp ?? "", Field.Store.YES),

                                        // Index content for search (Analyzed, Not Stored - we store it in "Content" field)
                                        // TextField is tokenized and indexed.
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