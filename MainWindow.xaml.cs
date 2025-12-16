using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using LuceneSearchWPFApp.Services;
using LuceneSearchWPFApp.Models;
using LuceneSearchWPFApp.Utilities;
using LuceneSearchWPFApp.Configuration;

namespace LuceneSearchWPFApp
{
    public partial class MainWindow : Window
    {
        private readonly IndexService _indexService;
        private readonly SearchService _searchService;
        private List<SearchResult> _currentResults; // 儲存當前搜尋結果

        public MainWindow()
        {
            InitializeComponent();
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            // Initialize Core Services (Lucene Implementation)
            _indexService = new IndexService();
            _searchService = new SearchService();

            // 初始化 Filter 下拉選單
            InitializeFilterComboBox();

            // 初始化日期選擇器：預設為最近 7 天
            dpEndDate.SelectedDate = DateTime.Today;
            dpStartDate.SelectedDate = DateTime.Today.AddDays(-7);

            // 預設日期篩選為停用狀態
            dpStartDate.IsEnabled = false;
            dpEndDate.IsEnabled = false;
            btnLast3Days.IsEnabled = false;
            btnLast7Days.IsEnabled = false;
            btnLast30Days.IsEnabled = false;
            btnCustomDate.IsEnabled = false;

            // 視窗關閉時釋放資源
            this.Closing += (s, e) =>
            {
                _searchService?.Dispose();
            };
        }

        /// <summary>
        /// 初始化 Filter ComboBox
        /// </summary>
        private void InitializeFilterComboBox()
        {
            // 從設定檔讀取 Filter 選項
            var filterOptions = AppSettings.Instance.UI.FileFilterOptions;

            if (filterOptions != null && filterOptions.Count > 0)
            {
                foreach (var option in filterOptions)
                {
                    cmbFilter.Items.Add(option);
                }
            }

            // 設定預設值
            string defaultFilter = AppSettings.Instance.UI.DefaultFileFilter;
            if (!string.IsNullOrEmpty(defaultFilter) && cmbFilter.Items.Contains(defaultFilter))
            {
                cmbFilter.SelectedItem = defaultFilter;
            }
            else if (cmbFilter.Items.Count > 0)
            {
                cmbFilter.SelectedIndex = 0; // 預設選第一個（全部）
            }
        }

        private void chkEnableDateFilter_Changed(object sender, RoutedEventArgs e)
        {
            bool isEnabled = chkEnableDateFilter.IsChecked == true;
            dpStartDate.IsEnabled = isEnabled;
            dpEndDate.IsEnabled = isEnabled;

            // 更新快速選擇按鈕的狀態
            btnLast3Days.IsEnabled = isEnabled;
            btnLast7Days.IsEnabled = isEnabled;
            btnLast30Days.IsEnabled = isEnabled;
            btnCustomDate.IsEnabled = isEnabled;
        }

        /// <summary>
        /// 快速選擇：最近 3 天
        /// </summary>
        private void btnLast3Days_Click(object sender, RoutedEventArgs e)
        {
            SetDateRange(3);
        }

        /// <summary>
        /// 快速選擇：最近 7 天
        /// </summary>
        private void btnLast7Days_Click(object sender, RoutedEventArgs e)
        {
            SetDateRange(7);
        }

        /// <summary>
        /// 快速選擇：最近 30 天
        /// </summary>
        private void btnLast30Days_Click(object sender, RoutedEventArgs e)
        {
            SetDateRange(30);
        }

        /// <summary>
        /// 自訂日期：啟用手動選擇
        /// </summary>
        private void btnCustomDate_Click(object sender, RoutedEventArgs e)
        {
            // 啟用日期篩選（如果尚未啟用）
            if (chkEnableDateFilter.IsChecked != true)
            {
                chkEnableDateFilter.IsChecked = true;
            }

            // 提示使用者可以手動選擇日期
            dpStartDate.Focus();
        }

        /// <summary>
        /// 設定日期範圍（最近 N 天）
        /// </summary>
        private void SetDateRange(int days)
        {
            // 自動啟用日期篩選
            chkEnableDateFilter.IsChecked = true;

            // 設定日期範圍
            dpEndDate.SelectedDate = DateTime.Today;
            dpStartDate.SelectedDate = DateTime.Today.AddDays(-days + 1);

            // 視覺回饋：短暫高亮顯示日期選擇器
            HighlightDatePickers();
        }

        /// <summary>
        /// 高亮顯示日期選擇器（視覺回饋）
        /// </summary>
        private async void HighlightDatePickers()
        {
            var originalStartBg = dpStartDate.Background;
            var originalEndBg = dpEndDate.Background;

            // 高亮
            dpStartDate.Background = System.Windows.Media.Brushes.LightYellow;
            dpEndDate.Background = System.Windows.Media.Brushes.LightYellow;

            await Task.Delay(300);

            // 恢復
            dpStartDate.Background = originalStartBg;
            dpEndDate.Background = originalEndBg;
        }

        private void btnCreateIndex_Click(object sender, RoutedEventArgs e)
        {
             string path = txtPath.Text.Trim();
             string filter = cmbFilter.Text.Trim();

             // 如果選擇「全部」，則清空 filter
             if (filter == "全部")
             {
                 filter = "";
             }

             if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
             {
                 MessageBox.Show($"Path not found: {path}");
                 return;
             }

             btnCreateIndex.IsEnabled = false;
             
             // Use Progress<T> to safely update UI from background thread
             var progress = new Progress<string>(fileName => 
             {
                 lblFileName.Text = fileName;
             });

             Task.Run(async () => 
             {
                 try 
                 {
                     // Run Indexing (Lucene)
                     await _indexService.CreateIndexAsync(path, filter, progress);
                     
                     Dispatcher.Invoke(() => MessageBox.Show("完成索引 (Lucene.Net)"));
                 }
                 catch(Exception ex)
                 {
                     Dispatcher.Invoke(() => MessageBox.Show("索引建立失敗: " + ex.Message));
                 }
                 finally
                 {
                     Dispatcher.Invoke(() => btnCreateIndex.IsEnabled = true);
                 }
             });
        }

        private void btnSearch_Click(object sender, RoutedEventArgs e)
        {
            string keyword = txtSearch.Text.Trim();
            if (string.IsNullOrEmpty(keyword)) return;

            btnSearch.IsEnabled = false;
            dgResults.ItemsSource = null;

            Task.Run(async () =>
            {
                try
                {
                    // 取得日期範圍（如果啟用）
                    DateTime? startDate = null;
                    DateTime? endDate = null;

                    Dispatcher.Invoke(() =>
                    {
                        if (chkEnableDateFilter.IsChecked == true)
                        {
                            startDate = dpStartDate.SelectedDate;
                            endDate = dpEndDate.SelectedDate;
                        }
                    });

                    // 執行搜尋
                    var results = await _searchService.SearchAsync(keyword, null, startDate, endDate);

                    Dispatcher.Invoke(() =>
                    {
                        // 儲存當前結果
                        _currentResults = results;

                        // 檢查是否要分組顯示（目前 DataGrid 模式不需要分組，因為表格已經很清楚）
                        // 直接綁定到 DataGrid
                        dgResults.ItemsSource = results.OrderByDescending(r => r.FileDate).ToList();

                        lblFindCnt.Text = $"筆數: {results.Count}";
                    });
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(() => MessageBox.Show("搜尋錯誤: " + ex.Message));
                }
                finally
                {
                    Dispatcher.Invoke(() => btnSearch.IsEnabled = true);
                }
            });
        }

        #region DataGrid 事件處理

        /// <summary>
        /// DataGrid 雙擊事件：開啟 Notepad++ 並跳到指定行
        /// </summary>
        private void dgResults_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (dgResults.SelectedItem is SearchResult result)
            {
                OpenFileInEditor(result);
            }
        }

        /// <summary>
        /// 用編輯器開啟檔案並跳到指定行
        /// </summary>
        private void OpenFileInEditor(SearchResult result)
        {
            if (string.IsNullOrEmpty(result.FilePath))
            {
                MessageBox.Show($"檔案路徑為空。\n\n可能原因：索引是用舊版本建立的，缺少檔案路徑資訊。\n\n解決方法：請重新建立索引。",
                    "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (!File.Exists(result.FilePath))
            {
                MessageBox.Show($"檔案不存在: {result.FilePath}\n\n可能原因：\n1. 檔案已被移動或刪除\n2. 索引資訊過舊\n\n解決方法：請重新建立索引。",
                    "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                var editorSettings = AppSettings.Instance.Editor;
                string executablePath = editorSettings.ExecutablePath;

                // 檢查 Notepad++ 是否存在
                if (!File.Exists(executablePath))
                {
                    if (editorSettings.FallbackToNotepad)
                    {
                        // 使用記事本開啟（無法跳轉行號）
                        Process.Start("notepad.exe", $"\"{result.FilePath}\"");
                        MessageBox.Show($"找不到 Notepad++，已改用記事本開啟\n（記事本無法自動跳轉到第 {result.LineNumber} 行）\n\n" +
                            $"設定檔中的路徑: {executablePath}\n\n" +
                            $"常見安裝位置：\n" +
                            $"• C:\\Program Files\\Notepad++\\notepad++.exe\n" +
                            $"• C:\\Program Files (x86)\\Notepad++\\notepad++.exe\n\n" +
                            $"請修改 appsettings.json 中的 ExecutablePath",
                            "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }
                    else
                    {
                        MessageBox.Show($"找不到編輯器: {executablePath}\n\n" +
                            $"常見安裝位置：\n" +
                            $"• C:\\Program Files\\Notepad++\\notepad++.exe\n" +
                            $"• C:\\Program Files (x86)\\Notepad++\\notepad++.exe\n\n" +
                            $"請修改 appsettings.json 中的 Editor.ExecutablePath",
                            "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                }

                // 格式化命令列
                string commandLine = editorSettings.CommandLineFormat
                    .Replace("{ExecutablePath}", executablePath)
                    .Replace("{LineNumber}", result.LineNumber)
                    .Replace("{FilePath}", result.FilePath);

                // 啟動 Notepad++
                var startInfo = new ProcessStartInfo
                {
                    FileName = executablePath,
                    Arguments = $"-n{result.LineNumber} \"{result.FilePath}\"",
                    UseShellExecute = false
                };

                Process.Start(startInfo);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"開啟檔案失敗: {ex.Message}", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region 右鍵選單事件處理

        /// <summary>
        /// 右鍵選單：用 Notepad++ 開啟
        /// </summary>
        private void MenuItem_OpenInNotepadPlusPlus_Click(object sender, RoutedEventArgs e)
        {
            if (dgResults.SelectedItem is SearchResult result)
            {
                OpenFileInEditor(result);
            }
        }

        /// <summary>
        /// 右鍵選單：用記事本開啟
        /// </summary>
        private void MenuItem_OpenInNotepad_Click(object sender, RoutedEventArgs e)
        {
            if (dgResults.SelectedItem is SearchResult result)
            {
                if (string.IsNullOrEmpty(result.FilePath) || !File.Exists(result.FilePath))
                {
                    MessageBox.Show($"檔案不存在: {result.FilePath}", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                try
                {
                    Process.Start("notepad.exe", $"\"{result.FilePath}\"");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"開啟檔案失敗: {ex.Message}", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        /// <summary>
        /// 右鍵選單：複製此行內容
        /// </summary>
        private void MenuItem_CopyContent_Click(object sender, RoutedEventArgs e)
        {
            if (dgResults.SelectedItem is SearchResult result)
            {
                try
                {
                    Clipboard.SetText(result.Content);
                    MessageBox.Show("已複製到剪貼簿", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"複製失敗: {ex.Message}", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        /// <summary>
        /// 右鍵選單：複製檔案路徑
        /// </summary>
        private void MenuItem_CopyFilePath_Click(object sender, RoutedEventArgs e)
        {
            if (dgResults.SelectedItem is SearchResult result)
            {
                try
                {
                    Clipboard.SetText(result.FilePath);
                    MessageBox.Show("已複製檔案路徑到剪貼簿", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"複製失敗: {ex.Message}", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        /// <summary>
        /// 右鍵選單：只顯示此檔案的結果
        /// </summary>
        private void MenuItem_FilterByFile_Click(object sender, RoutedEventArgs e)
        {
            if (dgResults.SelectedItem is SearchResult result && _currentResults != null)
            {
                var filteredResults = _currentResults
                    .Where(r => r.FileName == result.FileName)
                    .OrderBy(r => int.Parse(r.LineNumber))
                    .ToList();

                dgResults.ItemsSource = filteredResults;
                lblFindCnt.Text = $"筆數: {filteredResults.Count} (已篩選檔案: {result.FileName})";
            }
        }

        #endregion
    }
}
