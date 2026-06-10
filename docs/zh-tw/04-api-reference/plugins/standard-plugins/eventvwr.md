# EventVWR 外掛

本頁只描述當前倉庫裡實際存在的 EventVWR 外掛實現，不再繼續維護“完整子系統手冊 + 理想化 API 表”的舊稿。

## 先看這個外掛現在做什麼

從當前原始碼看，EventVWR 主要做兩件事：

- 提供一個只讀的 Windows Application 事件錯誤檢視視窗。
- 提供一組 Dump 配置選單，用於寫入或清除 Windows Error Reporting 的 LocalDumps 登錄檔項。

因此它不是一個複雜的診斷平台，而是“事件視窗 + Dump 配置選單”兩條很直接的功能鏈。

## 當前最關鍵的檔案

- `Plugins/EventVWR/EventVWRPlugins.cs`
- `Plugins/EventVWR/ExportEventWindow.cs`
- `Plugins/EventVWR/EventWindow.xaml(.cs)`
- `Plugins/EventVWR/Dump/DumpConfig.cs`
- `Plugins/EventVWR/Dump/MenuDump.cs`
- `Plugins/EventVWR/manifest.json`

如果只是想弄清外掛如何進入宿主、如何開啟事件視窗、如何修改 Dump 設定，這幾處程式碼已經足夠。

## 當前接入宿主的兩條選單鏈

### 事件視窗入口

`ExportEventWindow` 繼承 `MenuItemBase`，當前掛在 `Help` 選單下：

- `OwnerGuid = "Help"`
- `GuidId = "EventWindow"`
- `Order = 1000`

執行時會開啟 `EventWindow` 對話方塊。

這個入口還有一個重要約束：`Execute()` 當前帶有 `RequiresPermission(PermissionMode.Administrator)`，說明它不是純本地輔助選單，而是受宿主權限模式約束的。

### Dump 設定入口

`MenuDump` 也是 `Help` 選單下的一個父級選單項，`MenuThemeProvider` 則繼續為它提供子選單：

- 各 `DumpType` 列舉項
- 清空 Dmp
- 儲存 Dmp

因此 EventVWR 當前不是隻有一個視窗入口，而是幫助選單下的兩組獨立能力。

## 事件視窗當前怎麼工作

`EventWindow.xaml.cs` 的邏輯非常直接：

1. 視窗初始化時開啟 Windows `Application` 事件日誌。
2. 讀取所有 `EventLogEntry`。
3. 只保留 `EntryType == Error` 的事件。
4. 按 `TimeGenerated` 倒序排列。
5. 把結果繫結到左側列表。
6. 選擇某條記錄時，把 `Message` 顯示到詳情區域。

這意味著當前視窗並沒有複雜的篩選器、搜尋器或非同步分頁邏輯，本質上是一個“錯誤事件快速瀏覽器”。

## Dump 配置當前怎麼落地

`DumpConfig` 負責真正的系統設定寫入，當前核心點包括：

- 目標登錄檔路徑是 `HKLM\SOFTWARE\Microsoft\Windows\Windows Error Reporting\LocalDumps`。
- 會優先讀取預設 LocalDumps 配置，再覆蓋到當前程序對應的 `LocalDumps\{Name}.exe`。
- 當前管理的關鍵欄位有：
  - `DumpFolder`
  - `DumpCount`
  - `DumpType`
  - `CustomDumpFlags`

寫入配置和清除配置都要求管理員權限；如果當前不是管理員，會直接彈窗提示而不繼續執行。

除了登錄檔配置外，`SaveDump()` 還會呼叫 `DumpHelper.WriteMiniDump(...)`，把當前程序轉儲寫到目標目錄。

## 當前 manifest 資訊

按 `manifest.json`，這個外掛當前公開的基本資訊是：

- `Id = "EventVWR"`
- `name = "事件外掛"`
- `version = "1.0"`
- `dllpath = "EventVWR.dll"`
- `requires = "1.3.15.10"`

這比舊文件裡那種“目標框架、依賴矩陣、完整 API 表”更接近當前外掛裝載模型真正關心的資訊。

## 當前幾個容易寫錯的點

### 它不是完整的事件診斷中心

當前實現只讀取 Windows Application 日誌中的錯誤項，並展示訊息文字。不要把它繼續寫成帶高階檢索、匯出和多日誌源分析的平台。

### Dump 配置是系統級寫入

`DumpConfig` 當前操作的是 HKLM 下的 LocalDumps 登錄檔項，不是應用內部配置檔案。也正因為這樣，寫入和清理都要求管理員權限。

### 外掛入口類本身很輕

`EventVWRPlugins` 現在只是一個很薄的 `IPluginBase` 殼，主要提供 Header 和 Description。真正的功能入口並不在這裡，而在選單項和對應視窗/配置類裡。

### 權限邊界分成兩層

- 事件視窗選單入口本身受 `RequiresPermission(PermissionMode.Administrator)` 約束。
- Dump 登錄檔寫入和清理還會在執行時再次檢查是否具備管理員權限。

如果只記錄其中一層，文件就會把當前行為說得過於簡單。

## 推薦閱讀順序

1. `Plugins/EventVWR/ExportEventWindow.cs`
2. `Plugins/EventVWR/EventWindow.xaml.cs`
3. `Plugins/EventVWR/Dump/DumpConfig.cs`
4. `Plugins/EventVWR/Dump/MenuDump.cs`
5. `Plugins/EventVWR/manifest.json`

這樣能先看到宿主入口，再看到視窗行為和系統級配置落點。

## 繼續閱讀

- [Plugins/README.md](../../../../../Plugins/README.md)
- [docs/02-developer-guide/plugin-development/overview.md](../../../02-developer-guide/plugin-development/overview.md)
- [docs/04-api-reference/plugins/standard-plugins/system-monitor.md](./system-monitor.md)
