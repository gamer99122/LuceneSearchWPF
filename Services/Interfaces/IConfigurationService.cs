using LuceneSearchWPFApp.Services; // 引入 LuceneSettings, LogFileSettings, UISettings, EditorSettings

namespace LuceneSearchWPFApp.Services.Interfaces
{
    public interface IConfigurationService
    {
        string GetFullIndexPath();
        string GetLocalCachePath(); // 新增
        int GetMaxSearchResults();
        int GetLogFileEncodingCodePage();
        UISettings GetUISettings(); // 新增
        EditorSettings GetEditorSettings(); // 新增
        LogFileSettings GetLogFileSettings(); // 新增
    }
}