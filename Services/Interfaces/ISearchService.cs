using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LuceneSearchWPFApp.Models; // 假設 SearchResult 仍然作為 Model 傳遞

namespace LuceneSearchWPFApp.Services.Interfaces
{
    public interface ISearchService : IDisposable
    {
        Task<(List<SearchResult> Results, int TotalHits)> SearchAsync(string keyword, int? limit = null, DateTime? startDate = null, DateTime? endDate = null);
    }
}
