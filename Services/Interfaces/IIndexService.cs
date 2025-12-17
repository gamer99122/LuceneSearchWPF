using System;
using System.Threading.Tasks;

namespace LuceneSearchWPFApp.Services.Interfaces
{
    public interface IIndexService
    {
        Task CreateIndexAsync(string folderPath, string fileFilterKeyword, DateTime startDate, DateTime endDate, IProgress<string> progress);
    }
}
