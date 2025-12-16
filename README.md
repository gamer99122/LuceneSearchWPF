# LuceneSearchWPF

一個功能完整的 WPF 日誌檔案搜尋工具，使用 Lucene.Net 提供高效能的全文檢索功能，支援中文分詞，並整合 Notepad++ 快速定位。

## 功能特色

- **高效能全文檢索**: 使用 Lucene.Net 4.8 提供快速的關鍵字搜尋
- **中文分詞支援**: 內建 SmartChineseAnalyzer，完美支援中文關鍵字搜尋
- **DataGrid 表格顯示**: 以表格方式清晰呈現搜尋結果（日期、時間戳記、檔案名稱、行號、內容）
- **日期範圍篩選**: 支援自訂日期範圍，或快速選擇最近 3/7/30 天
- **Notepad++ 整合**: 雙擊搜尋結果直接開啟檔案並跳轉到指定行號
- **右鍵選單**: 提供複製內容、開啟檔案、篩選等便利功能
- **Big5 編碼支援**: 完整支援傳統中文 Big5 編碼的日誌檔案
- **增量索引**: 支援索引更新而不需重建全部索引

## 技術架構

- **框架**: .NET 8.0 WPF
- **搜尋引擎**: Lucene.Net 4.8.0-beta00016
- **中文分析器**: Lucene.Net.Analysis.SmartCn
- **UI 設計**: Material Design 風格
- **設定管理**: JSON 設定檔 (appsettings.json)

## 系統需求

- Windows 10/11
- .NET 8.0 Runtime
- Notepad++ (選用，用於快速開啟檔案)

## 安裝說明

### 1. 複製專案
```bash
git clone https://github.com/gamer99122/LuceneSearchWPF.git
cd LuceneSearchWPF
```

### 2. 還原套件並建置
```bash
dotnet restore
dotnet build
```

### 3. 執行程式
```bash
dotnet run
```

或直接開啟 `bin/Debug/net8.0-windows/LuceneSearchWPFApp.exe`

## 使用方法

### 步驟 1: 建立索引
1. 在 **Path** 欄位輸入日誌檔案所在資料夾（預設: `C:\HIS2\log`）
2. 在 **Filter** 下拉選單選擇要索引的檔案類型（例如: Escalator、全部）
3. 點擊 **建立索引** 按鈕
4. 等待索引建立完成（畫面會顯示目前處理的檔案名稱）

### 步驟 2: 搜尋關鍵字
1. 在 **搜尋關鍵字** 欄位輸入要查詢的關鍵字（支援中文）
2. (選用) 勾選 **啟用日期篩選**，選擇日期範圍
   - 可使用快速選擇：最近 3 天、最近 7 天、最近 30 天
   - 或手動選擇自訂日期範圍
3. 點擊 **搜尋** 按鈕
4. 結果會以表格方式顯示

### 步驟 3: 查看詳細內容
- **雙擊**任一筆搜尋結果，會自動開啟 Notepad++ 並跳轉到該行
- **右鍵點擊**可使用更多功能：
  - 用 Notepad++ 開啟
  - 用記事本開啟
  - 複製此行內容
  - 複製檔案路徑
  - 只顯示此檔案的結果

## 設定檔說明

編輯 `appsettings.json` 自訂設定：

```json
{
  "Lucene": {
    "IndexPath": "LuceneIndex",          // 索引儲存路徑
    "MaxSearchResults": 500              // 最大搜尋結果數量
  },
  "LogFiles": {
    "DefaultEncoding": "big5",           // 預設編碼
    "EncodingCodePage": 950              // Big5 編碼頁
  },
  "UI": {
    "DefaultLogPath": "C:\\HIS2\\log",   // 預設日誌路徑
    "DefaultFileFilter": "Escalator",    // 預設篩選器
    "FileFilterOptions": [               // 篩選器選項
      "全部",
      "Escalator",
      "ICUploadXML",
      "Error"
    ]
  },
  "Editor": {
    "ExecutablePath": "C:\\Program Files\\Notepad++\\notepad++.exe",  // Notepad++ 路徑
    "FallbackToNotepad": true            // 找不到 Notepad++ 時改用記事本
  }
}
```

### 常見 Notepad++ 安裝位置
- `C:\Program Files\Notepad++\notepad++.exe`
- `C:\Program Files (x86)\Notepad++\notepad++.exe`

## 日誌檔案命名格式

程式會自動解析檔案名稱中的日期：

- **當天日誌**: `AB1B-LAWLIET_Escalator.txt`
- **歷史日誌**: `AB1B-LAWLIET_Escalator.txt.20251214` (格式: YYYYMMDD)

## 常見問題

### Q: 找不到檔案路徑
**A**: 可能是使用舊版索引。請刪除 `LuceneIndex` 資料夾後重新建立索引。

### Q: Notepad++ 無法啟動
**A**: 請確認 `appsettings.json` 中的 `ExecutablePath` 路徑正確。程式會自動降級使用記事本開啟。

### Q: 搜尋中文關鍵字沒有結果
**A**: 確認：
1. 已正確建立索引
2. 日誌檔案編碼正確（預設為 Big5）
3. 關鍵字確實存在於日誌中

### Q: 如何更新索引
**A**: 直接重新點擊「建立索引」，程式會使用增量模式更新索引。

## 專案結構

```
LuceneSearchWPF/
├── Configuration/
│   └── AppSettings.cs          # 設定檔管理
├── Models/
│   └── SearchResult.cs         # 搜尋結果模型
├── Services/
│   ├── IndexService.cs         # 索引建立服務
│   └── SearchService.cs        # 搜尋服務
├── Utilities/
│   └── DateParser.cs           # 日期解析工具
├── MainWindow.xaml             # 主視窗 UI
├── MainWindow.xaml.cs          # 主視窗邏輯
└── appsettings.json            # 設定檔
```

## 授權

本專案採用 MIT 授權條款。

## 貢獻

歡迎提交 Issue 或 Pull Request 協助改進此專案。

## 聯絡方式

如有問題或建議，請在 GitHub Issues 提出。

## 更新紀錄

- 2025-12-16: 新增自動從 Path 掃描並產生 Filter 選項，移除日期後綴以整理成基底檔名（例如：XXX.log / XXX.log20251216 → XXX），並新增「刷新 Filter」按鈕以手動重新掃描。
- 2025-12-16: 在搜尋結果中加入關鍵字高亮顯示（支援多 token、忽略大小寫），提升定位效率。
- 2025-12-16: 改進 SearchService 的日期解析：若檔名未包含日期，會嘗試從 LogTimestamp 解析日期以減少顯示「日期未知」的情況。
- 2025-12-16: 其他小幅 UI 改進與錯誤處理強化。

