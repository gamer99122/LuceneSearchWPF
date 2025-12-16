using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LuceneSearchWPFApp.Models;
using LuceneSearchWPFApp.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;

namespace LuceneSearchWPFApp.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly ISearchService _searchService;
        private readonly IIndexService _indexService;
        private readonly IConfigurationService _configurationService;

        // ViewModel 屬性
        [ObservableProperty]
        private string _keyword;

        [ObservableProperty]
        private string _folderPath;

        [ObservableProperty]
        private string _fileFilterKeyword;

        [ObservableProperty]
        private ObservableCollection<SearchResult> _searchResults;

        [ObservableProperty]
        private bool _isIndexing;

        [ObservableProperty]
        private string _indexProgressMessage;

        [ObservableProperty]
        private SearchResult _selectedResult;

        [ObservableProperty]
        private DateTime? _startDate;

                [ObservableProperty]

                private DateTime? _endDate;

        

                [ObservableProperty]

                private ObservableCollection<string> _fileFilterOptions;

        

                [ObservableProperty]

                private bool _groupByDate = true; // 預設啟用日期分組 (排序)

        

                [ObservableProperty]

                private string _resultCountText = "筆數: 0";

                

                [ObservableProperty] // 新增 EnableDateFilter

                private bool _enableDateFilter = false;

        

                // Constructor for Dependency Injection

                public MainViewModel(ISearchService searchService, IIndexService indexService, IConfigurationService configurationService)

                {

                    _searchService = searchService;

                    _indexService = indexService;

                    _configurationService = configurationService;

        

                    SearchResults = new ObservableCollection<SearchResult>();

                    FileFilterOptions = new ObservableCollection<string>();

                    LoadSettings(); // 載入初始設定

                }

        

                private void LoadSettings()

                {

                    var uiSettings = _configurationService.GetUISettings();

                    FolderPath = uiSettings.DefaultLogPath;

                    FileFilterKeyword = uiSettings.DefaultFileFilter;

                    

                    // 初始化日期選擇器：預設為最近 7 天

                    EndDate = DateTime.Today;

                    StartDate = DateTime.Today.AddDays(-7);

        

                    // 初始刷新 Filter

                    RefreshFilters();

                }

        

                // Commands

                        [RelayCommand]

                        private async Task Search()

                        {

                            if (string.IsNullOrWhiteSpace(Keyword))

                            {

                                MessageBox.Show("Please enter a keyword to search.", "Search Error", MessageBoxButton.OK, MessageBoxImage.Warning);

                                return;

                            }

                

                            SearchResults.Clear();

                            ResultCountText = "搜尋中...";

                

                            DateTime? searchStartDate = EnableDateFilter ? StartDate : null;

                            DateTime? searchEndDate = EnableDateFilter ? EndDate : null;

                

                            var (results, totalHits) = await _searchService.SearchAsync(Keyword, null, searchStartDate, searchEndDate);

                            

                            // 根據 GroupByDate 決定排序

                            var sortedResults = GroupByDate 

                                ? results.OrderByDescending(r => r.FileDate).ToList() 

                                : results;

                

                            foreach (var result in sortedResults)

                            {

                                SearchResults.Add(result);

                            }

                

                                        // 更新顯示文字，如果總數大於顯示數，則提示總數與限制

                

                                        if (totalHits > SearchResults.Count)

                

                                        {

                

                                            ResultCountText = $"顯示前 {SearchResults.Count} 筆資料 (共找到 {totalHits} 筆，請縮小搜尋範圍)";

                

                                        }

                

                                        else

                

                                        {

                

                                            ResultCountText = $"找到 {SearchResults.Count} 筆資料";

                

                                        }

                        }

        

                [RelayCommand]

                private async Task CreateIndex()

                {

                    if (string.IsNullOrWhiteSpace(FolderPath) || !System.IO.Directory.Exists(FolderPath))

                    {

                        MessageBox.Show("Please select a valid folder path.", "Index Error", MessageBoxButton.OK, MessageBoxImage.Warning);

                        return;

                    }

        

                    IsIndexing = true;

                    IndexProgressMessage = "Indexing started...";

        

                    var progress = new Progress<string>(message =>

                    {

                        IndexProgressMessage = $"Indexing: {message}";

                    });

        

                    try

                    {

                        // 處理 "全部" 選項，將其視為不過濾 (空字串)

                        string actualFilter = FileFilterKeyword == "全部" ? string.Empty : FileFilterKeyword;

        

                        await _indexService.CreateIndexAsync(FolderPath, actualFilter, progress);

                        IndexProgressMessage = "Indexing completed successfully.";

                        MessageBox.Show("Indexing completed successfully.", "Index Status", MessageBoxButton.OK, MessageBoxImage.Information);

                    }

                    catch (Exception ex)

                    {

                        IndexProgressMessage = $"Indexing failed: {ex.Message}";

                        MessageBox.Show($"Indexing failed: {ex.Message}", "Index Error", MessageBoxButton.OK, MessageBoxImage.Error);

                    }

                    finally

                    {

                        IsIndexing = false;

                    }

                }

        

                [RelayCommand]

                private void OpenLogEntry()

                {

                    if (SelectedResult == null) return;

        

                    var editorSettings = _configurationService.GetEditorSettings();

        

                    try

                    {

                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo

                        {

                            FileName = editorSettings.ExecutablePath,

                            Arguments = string.Format(editorSettings.CommandLineFormat,

                                SelectedResult.LineNumber, SelectedResult.FilePath),

                            UseShellExecute = true

                        });

                    }

                    catch (Exception ex)

                    {

                        MessageBox.Show($"Could not open editor: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);

                        if (editorSettings.FallbackToNotepad)

                        {

                            System.Diagnostics.Process.Start("notepad.exe", SelectedResult.FilePath);

                        }

                    }

                }

        

                [RelayCommand]

                private void RefreshFilters()

                {

                    var options = new List<string>();

        

                    // 1. 嘗試從路徑讀取

                    if (!string.IsNullOrEmpty(FolderPath) && Directory.Exists(FolderPath))

                    {

                        try

                        {

                            var files = Directory.GetFiles(FolderPath);

                            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                            var rx = new Regex(@"^(?<name>.*?)(?:\.log|\.txt)?(?:\.?\d{8})?$", RegexOptions.IgnoreCase);

        

                            foreach (var f in files)

                            {

                                var fileName = Path.GetFileName(f);

                                var m = rx.Match(fileName);

                                if (m.Success)

                                {

                                    var n = m.Groups["name"].Value;

                                    if (!string.IsNullOrWhiteSpace(n))

                                        names.Add(n);

                                }

                                else

                                {

                                    names.Add(Path.GetFileNameWithoutExtension(fileName));

                                }

                            }

                            options.AddRange(names.OrderBy(s => s));

                        }

                        catch (Exception ex)

                        {

                            Console.WriteLine($"Error scanning folder: {ex.Message}");

                        }

                    }

        

                    // 2. Fallback: 使用 appsettings 中的選項

                    if (options.Count == 0)

                    {

                        var settingsOptions = _configurationService.GetUISettings().FileFilterOptions;

                        if (settingsOptions != null)

                        {

                            options.AddRange(settingsOptions);

                        }

                    }

        

                    // 3. 確保 "全部" 存在且在最前

                    if (!options.Contains("全部"))

                    {

                        options.Insert(0, "全部");

                    }

        

                    // 更新 ObservableCollection

                    FileFilterOptions.Clear();

                    foreach (var opt in options)

                    {

                        FileFilterOptions.Add(opt);

                    }

        

                    // 4. 重設選取值 (如果當前選取值不在新的選項中，設為預設)

                    if (string.IsNullOrEmpty(FileFilterKeyword) || !FileFilterOptions.Contains(FileFilterKeyword))

                    {

                        var defaultFilter = _configurationService.GetUISettings().DefaultFileFilter;

                        if (!string.IsNullOrEmpty(defaultFilter) && FileFilterOptions.Contains(defaultFilter))

                        {

                            FileFilterKeyword = defaultFilter;

                        }

                        else if (FileFilterOptions.Count > 0)

                        {

                            FileFilterKeyword = FileFilterOptions[0]; // 預設選第一個

                        }

                    }

                }

                

                [RelayCommand] // 新增 SetDateRangeCommand

                private void SetDateRange(string daysStr)

                {

                    if (int.TryParse(daysStr, out int days))

                    {

                        EnableDateFilter = true; // 自動啟用日期篩選

                        EndDate = DateTime.Today;

                        StartDate = DateTime.Today.AddDays(-days + 1);

                        // HighlightDatePickers(); // 視覺回饋，這個應該在 View 中處理，ViewModel 不直接操作 UI

                    }

                }

        

                [RelayCommand] // 新增 CustomDateCommand

                private void CustomDate()

                {

                    EnableDateFilter = true; // 啟用日期篩選

                    // 提示使用者可以手動選擇日期 (這個 MessageBox 也可以移除，讓 View 自己處理焦點)

                    MessageBox.Show("請手動選擇開始日期和結束日期。", "自訂日期", MessageBoxButton.OK, MessageBoxImage.Information);

                }

            }

        }

        