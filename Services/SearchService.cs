using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Globalization;
using Lucene.Net.Analysis.Cn.Smart;
using Lucene.Net.Index;
using Lucene.Net.QueryParsers.Classic;
using Lucene.Net.Search;
using Lucene.Net.Store;
using Lucene.Net.Util;
using LuceneSearchWPFApp.Models;
using LuceneSearchWPFApp.Services.Interfaces; // 引入介面
//using LuceneSearchWPFApp.Configuration; // 移除對舊 AppSettings 的引用
using LuceneSearchWPFApp.Utilities;

namespace LuceneSearchWPFApp.Services
{
    public class SearchService : ISearchService // 實現 ISearchService 介面
    {
        private readonly string _indexPath;
        private readonly LuceneVersion _luceneVersion = LuceneVersion.LUCENE_48;
        private readonly IConfigurationService _configurationService; // 注入配置服務

        private FSDirectory _directory;
        private DirectoryReader _reader;
        private IndexSearcher _searcher;
        private readonly object _lock = new object();

        // 透過建構子注入 IConfigurationService
        public SearchService(IConfigurationService configurationService) 
        {
            _configurationService = configurationService;
            _indexPath = _configurationService.GetFullIndexPath(); // 從配置服務獲取路徑
        }

        private void EnsureSearcherInitialized()
        {
            lock (_lock)
            {
                if (_directory == null || _reader == null)
                {
                    if (!System.IO.Directory.Exists(_indexPath))
                        return;

                    _directory = FSDirectory.Open(new DirectoryInfo(_indexPath));
                    _reader = DirectoryReader.Open(_directory);
                    _searcher = new IndexSearcher(_reader);
                }
                else
                {
                    var newReader = DirectoryReader.OpenIfChanged(_reader);
                    if (newReader != null)
                    {
                        _reader.Dispose();
                        _reader = newReader;
                        _searcher = new IndexSearcher(_reader);
                    }
                }
            }
        }

        public async Task<(List<SearchResult> Results, int TotalHits)> SearchAsync(string keyword, int? limit = null, DateTime? startDate = null, DateTime? endDate = null)
        {
            if (string.IsNullOrWhiteSpace(keyword))
                return (new List<SearchResult>(), 0);

            int maxResults = limit ?? _configurationService.GetMaxSearchResults(); // 從配置服務獲取最大搜尋結果數

            return await Task.Run(() =>
            {
                var results = new List<SearchResult>();
                int totalHits = 0;

                if (!System.IO.Directory.Exists(_indexPath))
                {
                    return (results, 0);
                }

                try
                {
                    EnsureSearcherInitialized();

                    if (_searcher == null)
                        return (results, 0);

                    using (var analyzer = new SmartChineseAnalyzer(_luceneVersion))
                    {
                        var parser = new QueryParser(_luceneVersion, "TokenizedContent", analyzer);
                        parser.DefaultOperator = Operator.AND;
                        Query keywordQuery = parser.Parse(keyword);

                        Query finalQuery = keywordQuery;
                        if (startDate.HasValue || endDate.HasValue)
                        {
                            var booleanQuery = new BooleanQuery
                            {
                                { keywordQuery, Occur.MUST }
                            };

                            string startDateStr = startDate?.ToString("yyyyMMdd") ?? "00000000";
                            string endDateStr = endDate?.ToString("yyyyMMdd") ?? "99999999";

                            var dateRangeQuery = TermRangeQuery.NewStringRange(
                                "FileDate",
                                startDateStr,
                                endDateStr,
                                true,
                                true
                            );

                            booleanQuery.Add(dateRangeQuery, Occur.MUST);
                            finalQuery = booleanQuery;
                        }

                        var topDocs = _searcher.Search(finalQuery, maxResults);
                        totalHits = topDocs.TotalHits; // 獲取總命中數

                        foreach (var scoreDoc in topDocs.ScoreDocs)
                        {
                            var doc = _searcher.Doc(scoreDoc.Doc);

                            string fileDateStr = doc.Get("FileDate");
                            DateTime? fileDate = null;
                            if (!string.IsNullOrEmpty(fileDateStr) && fileDateStr.Length == 8)
                            {
                                if (int.TryParse(fileDateStr.Substring(0, 4), out int year) &&
                                    int.TryParse(fileDateStr.Substring(4, 2), out int month) &&
                                    int.TryParse(fileDateStr.Substring(6, 2), out int day))
                                {
                                    try
                                    {
                                        fileDate = new DateTime(year, month, day);
                                    }
                                    catch { }
                                }
                            }

                            if (!fileDate.HasValue)
                            {
                                var ts = doc.Get("LogTimestamp");
                                if (!string.IsNullOrEmpty(ts))
                                {
                                    var formats = new[] { "yyyy-MM-dd HH:mm:ss", "yyyy/MM/dd HH:mm:ss", "yyyyMMdd HH:mm:ss", "yyyy-MM-ddTHH:mm:ss" };
                                    if (DateTime.TryParseExact(ts, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsed))
                                    {
                                        fileDate = parsed.Date;
                                    }
                                    else
                                    {
                                        if (DateTime.TryParse(ts, out DateTime parsed2))
                                            fileDate = parsed2.Date;
                                    }
                                }
                            }

                            results.Add(new SearchResult
                            {
                                FileName = doc.Get("FileName"),
                                FilePath = doc.Get("FilePath"),
                                LineNumber = doc.Get("LineNumber"),
                                Content = doc.Get("Content"),
                                FileDate = fileDate,
                                LogTimestamp = doc.Get("LogTimestamp")
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Search error: {ex.Message}");
                    throw;
                }

                return (results, totalHits);
            });
        }

        public void Dispose()
        {
            _reader?.Dispose();
            _directory?.Dispose();
        }
    }
}