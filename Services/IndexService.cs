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

        public async Task CreateIndexAsync(string folderPath, string fileFilterKeyword, IProgress<string> progress)
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

                                var fileDate = DateParser.ParseDateFromFileName(fileName);
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