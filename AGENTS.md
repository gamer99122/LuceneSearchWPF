# AGENTS.md

## Project Overview (專案概述)
這是一個基於 **Lucene.Net** 的高效能桌面全文檢索系統 (WPF)。
本專案採用 **Clean MVVM (Model-View-ViewModel)** 架構，強調關注點分離 (Separation of Concerns)，在保持開發效率的同時，確保程式碼的可測試性與可維護性。

## Tech Stack (技術堆疊)
- **Framework**: .NET 8 (Windows Desktop / WPF)
- **Search Engine**: Lucene.Net 4.8.0 (Beta)
- **Analysis**: Jieba.NET / SmartChineseAnalyzer
- **Configuration**: JSON (appsettings.json)
- **Logging**: (File-based / Console)
- **MVVM Framework**: CommunityToolkit.Mvvm (建議) 或原生實作

## Architecture & Directory Structure (架構與目錄結構)
本專案採用扁平化的 MVVM 結構，核心邏輯封裝於 Service 層：

### 1. `/Models` (資料模型)
單純的資料容器 (POCOs)，不含複雜邏輯。
- 代表業務資料結構，如 `Document`, `SearchResult`。
- 可直接用於 Service 與 ViewModel 之間的資料傳遞 (不再強制區分 DTO 與 Entity)。

### 2. `/Services` (服務層 - 核心功能)
封裝所有與 "資料獲取" 或 "外部系統" 互動的邏輯。
- **Interfaces**: 每個 Service 必須有對應的 Interface (e.g., `ISearchService`)，以利於 ViewModel 測試與依賴注入。
- **Implementations**:
  - `SearchService`: 封裝 Lucene.Net 的 `IndexSearcher` 與查詢邏輯。
  - `IndexService`: 封裝 Lucene.Net 的 `IndexWriter` 與檔案讀取邏輯。
  - `ConfigurationService`: 讀取 `appsettings.json`。

### 3. `/ViewModels` (視圖模型層)
UI 的邏輯大腦，負責協調 View 與 Model/Service。
- 透過 **Constructor Injection** 取得 Services (e.g., `ISearchService`)。
- 暴露 `ObservableProperty` 供 View 綁定 (Binding)。
- 暴露 `ICommand` (RelayCommand) 供 View 觸發事件。
- **不應參考任何 UI 元件** (如 `Button`, `TextBox`)，確保可單元測試。

### 4. `/Views` (視圖層)
純粹的 XAML 定義與極少量的 Code-behind。
- 負責 UI 佈局、樣式 (Styles) 與資源 (Resources)。
- 透過 `DataContext` 綁定對應的 ViewModel。

### 5. `/Utilities` (工具類)
通用的靜態方法或擴充方法 (Helpers, Converters)。
- `DateParser`, `StringExtensions` 等。

## Coding Conventions (編碼規範)
- **Naming**:
  - Class, Method, Property 使用 `PascalCase`.
  - Private Field 使用 `_camelCase`.
  - ViewModel 類別應以 `ViewModel` 結尾 (e.g., `MainViewModel`).
  - Service 介面必須以 `I` 開頭 (e.g., `ISearchService`).
- **Dependency Injection (DI)**:
  - 統一使用 **Constructor Injection**。
  - 應在 `App.xaml.cs` (Composition Root) 中配置 DI 容器 (如 `Microsoft.Extensions.DependencyInjection`)。
- **Async/Await**:
  - 避免在 ViewModel 建構子中呼叫 Async 方法。
  - 避免使用 `async void` (除了 Event Handler 以外)，應使用 `async Task` 或 MVVM Toolkit 的 `[RelayCommand]`.

## Lucene Specific Guidelines
- **封裝**: 所有 Lucene 相關的引用 (`using Lucene.Net.*`) 應盡量限制在 `Services` 目錄下的實作類別中。
- **資源管理**: `IndexWriter` 與 `IndexSearcher` 需妥善處理 `Dispose`，建議在 Service 中使用 Singleton 或適當的生命週期管理。

## Testing Protocols (測試規範)
- **ViewModel Tests**: Mock `ISearchService` 來測試 ViewModel 的狀態變化與命令邏輯。
- **Service Tests**: (Optional) 針對複雜的 Lucene 查詢邏輯撰寫整合測試。