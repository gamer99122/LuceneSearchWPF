using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Lucene.Net.Analysis.Cn.Smart;
using Lucene.Net.Index;
using Lucene.Net.QueryParsers.Classic;
using Lucene.Net.Search;
using Lucene.Net.Store;
using Lucene.Net.Util;
using LuceneSearchWPFApp.Models;
using LuceneSearchWPFApp.Configuration;
using LuceneSearchWPFApp.Utilities;

namespace LuceneSearchWPFApp.Services
{
    public class SearchService : IDisposable
    {
        private readonly string _indexPath;
        private readonly LuceneVersion _luceneVersion = LuceneVersion.LUCENE_48;

        // 快取 DirectoryReader 和 IndexSearcher 以提升效能
        private FSDirectory _directory;
        private DirectoryReader _reader;
        private IndexSearcher _searcher;
        private readonly object _lock = new object();

        public SearchService()
        {
            _indexPath = AppSettings.Instance.Lucene.GetFullIndexPath();
        }

        /// <summary>
        /// 初始化或重新整理 IndexSearcher（索引更新後需要呼叫）
        /// </summary>
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
                    // 檢查索引是否有更新，如果有則重新開啟 reader
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

        public async Task<List<SearchResult>> SearchAsync(string keyword, int? limit = null, DateTime? startDate = null, DateTime? endDate = null)
        {
            if (string.IsNullOrWhiteSpace(keyword))
                return new List<SearchResult>();

            // 使用設定檔中的預設值
            int maxResults = limit ?? AppSettings.Instance.Lucene.MaxSearchResults;

            return await Task.Run(() =>
            {
                var results = new List<SearchResult>();

                if (!System.IO.Directory.Exists(_indexPath))
                {
                    return results; // No index exists
                }

                try
                {
                    // 使用快取的 searcher
                    EnsureSearcherInitialized();

                    if (_searcher == null)
                        return results;

                    using (var analyzer = new SmartChineseAnalyzer(_luceneVersion))
                    {
                        // 建立關鍵字查詢
                        var parser = new QueryParser(_luceneVersion, "TokenizedContent", analyzer);
                        Query keywordQuery = parser.Parse(keyword);

                        // 如果有日期範圍，建立組合查詢
                        Query finalQuery = keywordQuery;
                        if (startDate.HasValue || endDate.HasValue)
                        {
                            var booleanQuery = new BooleanQuery
                            {
                                { keywordQuery, Occur.MUST }
                            };

                            // 建立日期範圍查詢
                            string startDateStr = startDate?.ToString("yyyyMMdd") ?? "00000000";
                            string endDateStr = endDate?.ToString("yyyyMMdd") ?? "99999999";

                            var dateRangeQuery = TermRangeQuery.NewStringRange(
                                "FileDate",
                                startDateStr,
                                endDateStr,
                                true,  // includeLower
                                true   // includeUpper
                            );

                            booleanQuery.Add(dateRangeQuery, Occur.MUST);
                            finalQuery = booleanQuery;
                        }

                        var topDocs = _searcher.Search(finalQuery, maxResults);

                        foreach (var scoreDoc in topDocs.ScoreDocs)
                        {
                            var doc = _searcher.Doc(scoreDoc.Doc);

                            // 解析日期
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

                return results;
            });
        }

        /// <summary>
        /// 釋放資源
        /// </summary>
        public void Dispose()
        {
            _reader?.Dispose();
            _directory?.Dispose();
        }
    }
}