# ColorVision.UI.Desktop

本頁只描述 UI/ColorVision.UI.Desktop 當前已經落地的桌面端視窗和配套功能，不再延續舊文件裡那種“整個系統主程式入口”的寫法。

## 模組定位

`ColorVision.UI.Desktop` 當前更接近桌面側輔助殼層功能集合，主要提供：

- 設定視窗
- 配置嚮導
- 選單項管理視窗
- 配置管理視窗
- 第三方應用接入
- DLL 資訊檢視等輔助視窗

它不是整個倉庫的主應用入口。當前真正的主程式專案在 `ColorVision/`，而這裡的 `App.xaml.cs` 和 `MainWindow.xaml.cs` 都非常輕。

## 當前最關鍵的目錄

從專案目錄看，最值得優先閱讀的是：

- `Settings/`：統一設定視窗
- `Wizards/`：嚮導視窗、步驟發現、視窗配置
- `MenuItemManager/`：選單項管理與持久化
- `ThirdPartyApps/`：系統工具和第三方應用入口
- `Marketplace/`：DLL 版本檢視等輔助視窗
- `ConfigManagerWindow.xaml(.cs)`：配置管理視窗
- `Feedback/`、`Download/`、`TimedButtons/`、`WebViewService.cs`：其他桌面輔助能力

## 關鍵入口型別

### App 與 MainWindow

當前 `App.xaml.cs` 只是一個很輕的 `Application` partial，`MainWindow.xaml.cs` 也只保留基礎構造邏輯。

這說明：

- 這個專案裡確實有 `App` 和 `MainWindow`
- 但它們並不是舊文件描述的那種承載完整啟動流程和系統初始化邏輯的中心檔案

閱讀這個專案時，真正更值得先看的是各個功能視窗和管理器，而不是把注意力放在空殼入口上。

### SettingWindow

`Settings/SettingWindow.xaml.cs` 是當前設定系統的主要桌面入口。它負責：

- 讀取 `ConfigSettingManager.GetInstance().GetAllSettings()`
- 按分組建立 Tab
- 根據 `ConfigSettingType` 決定生成 Tab 頁、整類屬性頁或單屬性控制元件
- 對 `ViewType` 做懶載入，避免視窗初始化時把所有檢視一次性例項化

因此這頁舊文件裡“統一設定視窗”這個方向是對的，但實現細節應當落到 `ConfigSettingManager` + 惰性載入上。

### WizardManager / WizardWindow / WizardWindowConfig

當前嚮導鏈是這組型別：

- `WizardManager`：反射掃描 `IWizardStep`
- `WizardWindow`：多步驟視窗與完成邏輯
- `WizardWindowConfig`：視窗配置和完成狀態

`WizardManager` 會遍歷程式集並例項化 `IWizardStep`，然後按 `Order` 排序；`WizardWindow` 會驅動進度條、前後步驟切換和完成驗證。

這部分是當前專案裡最明確的一條“桌面輔助流程鏈”。

### MenuItemManagerConfig 與 MenuItemManagerWindow

`MenuItemManagerConfig` 當前負責選單項設定的持久化，而 `MenuItemManagerWindow` 則提供視覺化管理介面。它們屬於 UI 殼層配置工具，而不是全域性選單執行時本身。

### ConfigManagerWindow

`ConfigManagerWindow` 是另一個桌面側管理視窗，用來從更集中視角管理配置項。它和 `SettingWindow` 不完全重合，屬於桌面工具層而不是基礎介面層。

### ViewDllVersionsWindow

`Marketplace/ViewDllVersionsWindow.xaml.cs` 當前會遍歷已載入程式集，收集：

- 名稱
- 程式集版本
- 檔案版本
- 產品版本
- 公司資訊
- 路徑

它更像一個執行時診斷和排查視窗，而不是外掛更新核心流程本身。

### SystemAppProvider 與 WebViewService

- `ThirdPartyApps/SystemAppProvider.cs` 負責系統工具和第三方應用入口。
- `WebViewService.cs` 則表明這個專案還承載了一部分桌面 WebView 相關能力。

## 當前執行時主鏈

這個專案當前沒有單一主鏈，而是幾條桌面輔助鏈並存。更值得關注的是：

1. 設定鏈：`SettingWindow` -> `ConfigSettingManager` -> 配置頁/屬性頁懶載入。
2. 嚮導鏈：`WizardManager` -> `IWizardStep` 發現 -> `WizardWindow` 切換與完成。
3. 管理鏈：`MenuItemManagerWindow` / `ConfigManagerWindow` / `ViewDllVersionsWindow` 提供不同側面的桌面管理視窗。

## 當前實現有哪些邊界

### 不是整個系統主入口

這是這頁最容易寫錯的地方。當前專案裡的 `App` 和 `MainWindow` 都很輕，不能繼續把 `ColorVision.UI.Desktop` 講成整個產品唯一的啟動中心。

### 不是所有功能都圍繞 MainWindow

這個專案更像一組視窗和管理工具集合。很多價值來自獨立視窗，而不是一個龐大的主視窗編排層。

### 舊文件提到的 SystemInitializer 在這個專案裡並不存在

當前 `UI/ColorVision.UI.Desktop` 目錄裡並沒有實際的 `SystemInitializer` 實現。舊文件把它列為現有元件，會直接誤導讀者去找一個不存在的控制點。

## 當前更適合怎樣讀這個模組

### 想看設定和配置視窗

先看：

- `Settings/SettingWindow.xaml.cs`
- `ConfigManagerWindow.xaml.cs`

### 想看向導和首次配置流程

先看：

- `Wizards/WizardWindow.xaml.cs`
- `Wizards/WizardWindowConfig.cs`

### 想看選單管理和桌面輔助視窗

先看：

- `MenuItemManager/MenuItemManagerConfig.cs`
- `MenuItemManager/MenuItemManagerWindow.xaml.cs`
- `Marketplace/ViewDllVersionsWindow.xaml.cs`
- `ThirdPartyApps/SystemAppProvider.cs`

## 這頁不再做什麼

本頁不再繼續維護這些高風險內容：

- 把本專案寫成整個系統主程式入口
- 不存在元件的說明，例如 `SystemInitializer`
- 大段版本號和偽 API 列表
- 把輕量 `App` / `MainWindow` 擴寫成完整啟動流程中心

## 繼續閱讀

- [UI元件概覽](./README.md)
- [ColorVision.UI](./ColorVision.UI.md)
- [ColorVision.Solution](./ColorVision.Solution.md)