# ColorVision.Common

本頁只描述 UI/ColorVision.Common 當前已經承擔的共享基礎能力，不再延續舊文件裡那種“大而全公共 SDK 介面大全”的寫法。

## 模組定位

ColorVision.Common 當前是 UI 層的共享基礎庫，主要提供這些內容：

- MVVM 基礎型別
- 命令封裝
- 通用介面和後設資料模型
- 粗粒度權限控制
- Windows 原生方法封裝
- 常用工具類

它更像一組跨模組複用的基礎積木，而不是一個獨立執行的業務模組。

## 當前最關鍵的目錄

從專案目錄看，最值得先認識的是：

- MVVM/：`ViewModelBase`、`RelayCommand` 等基礎型別
- Interfaces/：配置、選單、狀態列、初始化器、檢視等共享介面
- Authorizations/：`Authorization`、`AccessControl`、`PermissionMode`
- NativeMethods/：Windows API 包裝
- Utilities/：檔案、集合、視窗等常用工具
- Input/：輸入相關能力
- ThirdPartyApps/：第三方應用接入相關定義

## 關鍵入口型別

### ViewModelBase

`ViewModelBase` 是當前最基礎的可繫結物件基類，實現了 `INotifyPropertyChanged`，供大量配置類、管理器和檢視模型繼承。

### RelayCommand 與 Commands

當前命令層主要有兩種常用入口：

- `RelayCommand` / `RelayCommand<T>`：通用命令封裝
- `Commands`：少量全域性 `RoutedUICommand`

舊文件裡把命令系統寫成一整套獨立框架，但從當前程式碼看，真正高頻使用的還是 `RelayCommand`。

### Interfaces/

`Interfaces/` 承擔的是共享邊界定義，而不是完整業務實現。當前常見的幾組介面有：

- `IConfig`、`IConfigSettingProvider`
- `IInitializer`、`InitializerBase`
- `IMenuItemProvider`
- `IStatusBarProvider`、`IStatusBarProviderUpdatable`
- 檢視相關型別，如 `View` 和 `IViewManager`

這些型別大多隻定義最小契約，真正的註冊、發現和執行邏輯通常在上層模組裡。

### StatusBarMeta

`StatusBarMeta` 不是舊文件裡那種只有圖示和命令的簡化模型。當前它已經承載了：

- 唯一標識和名稱
- 描述文字
- 左右對齊和排序
- `Command` 或 `Popup` 兩類動作
- 繫結源物件
- 圖示資源或直接圖示內容
- 目標視窗範圍和預設可見性

所以它已經是 UI 狀態列系統的核心後設資料，而不只是一個輕量 DTO。

### Authorization / AccessControl / PermissionMode

`Authorizations/` 下提供的是當前通用的粗粒度權限控制：

- `Authorization.Instance.PermissionMode`
- `AccessControl.Check(...)`
- `RequiresPermissionAttribute`

這裡要特別注意邊界：

- Common 層只提供全域性粗粒度權限模式
- 細粒度本地 RBAC 在 `UI/ColorVision.Solution/Rbac`

不要把 Common 的權限系統寫成整個專案唯一的完整 RBAC。

## 當前實現更像什麼

ColorVision.Common 當前更接近“共享協議層 + 基礎工具層”，而不是一個面向外部使用者釋出的穩定公共框架。很多介面雖然名字通用，但它們的真實作用是給倉庫內的 UI 模組提供統一契約。

例如：

- `IConfig` 本身只是標記介面
- `InitializerBase` 只提供預設名字、順序和依賴結構
- `View` 是一個帶索引、標題和圖示的共享 ViewModel，而不是完整檢視框架

## 當前更適合怎樣讀這個模組

### 想看共享 MVVM 和命令基礎

先看：

- `MVVM/ViewModelBase.cs`
- `MVVM/RelayCommand.cs`
- `Commands.cs`

### 想看配置、選單、狀態列這些公共契約

先看：

- `Interfaces/Config/` 或 `Interfaces/ConfigSetting/`
- `Interfaces/Menus/`
- `Interfaces/StatusBar/`
- `Interfaces/IInitializer/`

### 想看權限邊界

先看：

- `Authorizations/AccessControl.cs`
- `Authorizations/PermissionMode.cs`

### 想看原生方法和工具類

先看：

- `NativeMethods/`
- `Utilities/`

## 當前實現的邊界

### 不是完整外掛平台文件

雖然 Common 裡定義了不少擴充套件介面，但真正的外掛發現、選單註冊、配置聚合、狀態列重新整理都分散在上層模組實現裡。這裡只是共享契約，不應被寫成統一執行時中心。

### 不是完整權限中心

Common 裡的權限檢查適合做全域性模式開關或粗粒度限制，但並不等於 Solution 側的本地 RBAC。

### 很多介面是“最小形狀”而不是“最終抽象”

像 `IConfig`、`IInitializer` 這類介面很輕，後續閱讀時應優先順著實現方去看真實控制鏈，而不是停留在介面定義本身。

## 這頁不再做什麼

本頁不再繼續維護這些高風險內容：

- 大段版本號和包釋出資訊
- 理想化的公共 SDK 清單
- 把所有介面都擴寫成完整框架能力
- 把 Common 權限系統誤寫成全域性唯一 RBAC

## 繼續閱讀

- [UI元件概覽](./README.md)
- [ColorVision.UI](./ColorVision.UI.md)
- [ColorVision.Solution](./ColorVision.Solution.md)