# ColorVision.UI

本頁只保留 `UI/ColorVision.UI/` 當前最關鍵的基礎設施和入口型別，不再繼續維護舊文件裡那種“版本清單 + 全量偽 API + 更新日誌”的寫法。

## 模組定位

`ColorVision.UI` 不是某一個單獨控制元件，而是桌面應用的大量 UI 基礎設施所在位置。它當前承擔的角色更接近“UI 殼層和公共服務集合”，主要給主程式、Engine 和其他 UI 子專案複用。

從目錄結構看，它覆蓋的內容至少包括：

- 配置讀寫和環境路徑
- 外掛裝載與外掛包處理
- 選單系統
- 屬性編輯器
- 快捷鍵系統
- 多語言資源
- 日誌相關 UI 配置
- Shell、搜尋、狀態列、頁面與通用控制元件

所以它不是單一“控制元件庫”，也不適合再被寫成一個擁有穩定公共 API 面的標準 SDK。

## 當前最關鍵的目錄

如果只是想快速建立認知，建議先看這些目錄：

- `Plugins/`：外掛發現、後設資料、依賴檢查、解包與更新
- `PropertyEditor/`：屬性編輯視窗、樹節點、編輯器型別系統
- `Menus/`：選單註冊和動態選單重新整理
- `HotKey/`：全域性和視窗級快捷鍵
- `Languages/`：語言與資源切換
- `LogImp/`：日誌相關配置和視窗狀態
- `ConfigSetting/` 與根目錄 `ConfigHandler.cs`：配置系統入口
- `Shell/`、`Serach/`、`StatusBar/`：桌面互動輔助能力

## 關鍵入口型別

### `ConfigHandler`

`ConfigHandler` 是這個專案裡最核心的基礎設施之一。很多 `IConfig` 配置物件最終都圍繞它或相關配置服務完成讀取、快取和儲存。

如果問題表現為“設定沒儲存”“配置沒載入”“預設值異常”，通常先看這條鏈。

### `PluginLoader`

`PluginLoader` 當前負責外掛執行時裝載。它做的並不只是“掃 DLL”，還包括：

- 掃描 `Plugins/` 目錄
- 讀取 `manifest.json`
- 解析可選 `.deps.json`
- 檢查 `ColorVision.*` 依賴版本
- 最終裝載外掛程式集

這也是為什麼外掛相關文件如果只寫成“反射掃描外掛型別”，通常都會失真。

### `MenuManager`

`MenuManager` 是選單系統的中心物件。很多動態選單、最近檔案重新整理和外掛選單入口，最終都會落到它的註冊或重新整理鏈上。

所以這部分更像應用殼層的選單協調器，而不是一組靜態 XAML 選單定義。

### `PropertyEditor`

`PropertyEditor/` 當前負責屬性編輯體驗的主鏈：

- `PropertyEditorWindow`
- `PropertyTreeNode`
- 編輯器型別與輔助類

這一套系統和倉庫裡大量帶 `Category`、`DisplayName`、`Description`、`PropertyEditorType` 這類特性的物件配合使用，是當前動態屬性編輯體驗的基礎。

### `HotKey`

快捷鍵系統當前不是單點實現，而是分成：

- `GlobalHotKey/`
- `WindowHotKey/`
- `HotKeys` 及其配置與設定視窗

因此改快捷鍵時，通常要先區分你改的是系統級熱鍵，還是視窗內熱鍵。

### `Languages`

多語言資源和 UI 文化切換相關能力在這裡集中管理。主程式啟動階段設定 UI Culture 後，很多介面資源載入都會受這裡影響。

### `LogImp`

日誌相關的 UI 配置和本地日誌視窗狀態也放在這個專案裡。它更偏“日誌顯示與配置配套”，不是完整日誌後端本身。

## 這個專案當前最容易被寫錯的地方

### 它不是單一控制元件庫

舊文件喜歡把 `ColorVision.UI` 寫成“核心 UI 控制元件包”。當前程式碼遠比這複雜，它同時承接外掛、配置、選單、快捷鍵、屬性編輯和多語言等橫切能力。

### 外掛系統不等於擴充套件點定義本身

`PluginLoader` 位於這裡，但外掛真正擴充套件到什麼能力，仍取決於各外掛程式集和被實現的選單、模板、服務、結果檢視介面。

### 權限不應在這頁被泛化為“全域性 RBAC 中心”

當前全域性粗粒度權限來自 `Authorization.Instance.PermissionMode`，而更細的本地 RBAC 子系統主要位於 `UI/ColorVision.Solution/Rbac/`。`ColorVision.UI` 提供的是授權基礎設施和公共依賴，不應該在這裡繼續寫成完整權限平台。

## 當前更適合怎樣讀這個專案

### 想看配置和全域性服務

先看：

- `ConfigHandler.cs`
- `Environments.cs`
- `FileProcessorFactory.cs`

### 想看外掛執行時

先看：

- `Plugins/PluginLoader.cs`
- `Plugins/PluginManifest.cs`
- `Plugins/PluginInfo.cs`

### 想看屬性編輯體系

先看：

- `PropertyEditor/PropertyEditorWindow.xaml(.cs)`
- `PropertyEditor/PropertyTreeNode.cs`
- `PropertyEditor/PropertyEditors.cs`

### 想看選單和快捷鍵

先看：

- `Menus/MenuManager.cs`
- `HotKey/HotKeys.cs`
- `HotKey/GlobalHotKey/`
- `HotKey/WindowHotKey/`

## 這頁不再做什麼

本頁不再繼續維護這些高風險內容：

- 過時版本號和目標框架清單
- 大段未經核實的類成員虛擬碼
- 把 `ColorVision.UI` 說成穩定公共 SDK
- 把權限、外掛、日誌等橫切能力都講成各自完整平台

如果後續要補某個子系統，應直接落到對應專題頁，而不是在這裡繼續堆“大而全”說明。

## 繼續閱讀

- [UI元件概覽](./README.md)
- [ColorVision.Solution](./ColorVision.Solution.md)
- [安全與權限控制](../../03-architecture/security/overview.md)