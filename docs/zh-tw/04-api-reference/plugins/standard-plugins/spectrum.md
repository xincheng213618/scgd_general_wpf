# Spectrum 外掛

本頁只描述當前倉庫裡實際存在的 Spectrum 外掛實現，不再繼續維護“版本表 + 功能宣傳 + 理想化 API 手冊”式舊稿。

## 先看這個外掛現在是什麼

按當前原始碼狀態，Spectrum 不是一個零散的裝置驅動示例，而是一個以獨立光譜測試視窗為中心的外掛工作臺。它當前至少包含四條明確的執行鏈：

- Tool 選單裡的視窗入口。
- `Spectrum` 目標視窗自己的選單和狀態列。
- 圍繞 `SpectrometerManager` 的連線、標定和測量控制。
- 圍繞 `ViewResultManager` 的結果展示、SQLite 持久化和測量畫像記錄。

因此它比舊文件裡“光譜儀測試工具”這類泛化描述更具體，實際是一個完整但仍然以單視窗為中心的測量工作臺。

## 當前最關鍵的檔案

- `Plugins/Spectrum/manifest.json`
- `Plugins/Spectrum/MainWindow.xaml(.cs)`
- `Plugins/Spectrum/MainWindow.Connection.cs`
- `Plugins/Spectrum/MainWindow.Measurement.cs`
- `Plugins/Spectrum/SpectrometerManager.cs`
- `Plugins/Spectrum/Data/ViewResultManager.cs`
- `Plugins/Spectrum/SpectrumStatusBarProvider.cs`
- `Plugins/Spectrum/Calibration/CalibrationGroupWindow.xaml.cs`
- `Plugins/Spectrum/License/LicenseDatabase.cs`

如果只是想弄清外掛怎麼進入宿主、怎麼連線裝置、怎麼儲存結果，這幾處程式碼已經覆蓋了主體。

## 當前接入宿主的幾條鏈

### 視窗入口

`MenuSpectrumWindow` 當前繼承 `MenuItemBase`，掛在 `Tool` 選單下，執行時直接開啟 `MainWindow`。

這說明 Spectrum 現在最核心的宿主入口不是某個很厚的外掛入口類，而是選單項和隨後開啟的工作視窗。

### 視窗級選單與狀態列

`MainWindow` 初始化時會呼叫：

- `MenuManager.GetInstance().LoadMenuForWindow("Spectrum", menu)`
- `StatusBarManager.GetInstance().Init(StatusBarGrid, "Spectrum")`

也就是說，這個外掛並不只是在主程式選單上掛一個入口。視窗開啟後，還有一套以 `TargetName = "Spectrum"` 為目標的區域性選單和狀態列擴充套件面。

### 狀態列提供器

`SpectrumStatusBarProvider` 當前會把這些資訊接到 `Spectrum` 視窗的狀態列：

- 連線狀態
- 硬體型號
- SN 序列號
- 當前標定組
- 當前測量模式
- Shutter 連線狀態
- CFW 濾光輪連線狀態

其中 SN 文字當前還支援點選複製，因此它不是隻讀裝飾項。

### manifest 資訊

按當前 `manifest.json`，外掛對宿主公開的裝載資訊是：

- `Id = "Spectrum"`
- `name = "Spectrum"`
- `version = "1.0"`
- `dllpath = "Spectrum.dll"`
- `requires = "1.3.15.8"`

這比舊文件裡自訂的一長串版本和依賴表更接近當前真實裝載模型。

## 當前執行時核心是誰

`SpectrometerManager` 是當前外掛最重要的單例狀態中心。它透過 `ConfigService` 取得並持有：

- `SpectrumConfig`
- `ShutterController`
- `FilterWheelController`
- `SmuController`
- 當前裝置控制代碼
- 當前連線狀態、硬體型號、序列號
- 當前標定組配置與活動分組
- 當前測量模式文字

因此 `MainWindow` 更多是在組織 UI 和呼叫鏈，真正跨檔案共享的測量狀態主要都收斂在 `SpectrometerManager`。

## 連線與標定當前怎麼工作

`MainWindow.Connection.cs` 展示了當前連線鏈的真實順序：

1. 連線前先呼叫 `LicenseDatabase.Instance.SyncToLocal()` 同步許可證。
2. 透過 `Spectrometer.CM_CreateEmission(...)` 建立控制代碼。
3. 根據配置決定走 USB 還是 COM 口初始化。
4. 連線成功後讀取裝置序列號。
5. 按序列號載入標定分組配置。
6. 載入當前波長標定檔案和幅值標定檔案。
7. 應用 SP100 參數。

如果連線失敗，當前實現還會嘗試讀取裝置列表；當檢測到單個裝置但連線失敗時，會把問題引導到許可證管理，而不是隻彈一個通用錯誤框。

### 標定分組不是簡單檔案選擇框

`CalibrationGroupWindow` 當前按光譜儀 SN 管理分組配置。編輯時變更會先暫存在記憶體裡，只有點選儲存才真正寫回；直接關閉視窗會放棄未儲存改動。

這和舊文件裡“選擇一個標定檔案繼續測量”的說法相比，已經是更明確的一套按裝置分組的配置模型。

## 測量鏈當前怎麼展開

`MainWindow.Measurement.cs` 當前把單次測量拆成了幾個清晰階段：

- 自動校零前置檢查
- 自動積分
- 自適應校零
- 採集資料
- 渲染結果
- 持久化結果

具體行為上，當前實現已經不只是“呼叫一次裝置 SDK 然後畫圖”：

- 自動校零依賴 `ShutterController`。
- 同步頻率模式會走 `CM_Emission_GetDataSyncfreq(...)`。
- 標準模式在超時時會做一次重試。
- EQE 模式下會接入 `SmuController`，並把電壓電流結果回寫到視窗配置和結果物件。

同時，測量過程會額外記錄每個步驟的耗時、輸入快照和成功狀態。

## 結果與持久化當前怎麼落地

`ViewResultManager` 當前不只是一個記憶體列表管理器，而是外掛的資料落點。按實現看，它會：

- 在 `AppData\Spectromer\Config\Spectrum.db` 維護 SQLite 資料庫。
- 儲存 `SprectrumModel` 結果記錄。
- 維護 `SpectrumMeasurementProfile` 測量畫像。
- 儲存測量步驟明細 JSON。
- 在需要時更新 EQE 欄位和測量總耗時。

因此 Spectrum 當前不是“測完即丟”的臨時工具，已經包含基本的資料追蹤和回看能力。

### 匯出與列表操作

當前主視窗還內建了：

- 相對光譜 / 絕對光譜切換
- CIE 圖聯動顯示
- 可見列複製
- 普通光譜 CSV 匯出
- EQE 模式 CSV 匯出
- 結果刪除與資料庫清空

這些行為分散在 `MainWindow.Chart.cs`、`MainWindow.ListView.cs` 和 `MainWindow.Export.cs` 中。

## 當前還有哪些附加子系統

### 佈局持久化

`MainWindow` 目前透過 `DockLayoutManager` 管理 AvalonDock 佈局，並在視窗關閉時自動儲存佈局。這意味著它不是固定死板的單面板視窗。

### 許可證同步

`LicenseDatabase` 當前用 SQLite 跟蹤已匯入許可證檔案的後設資料，並在連線光譜儀前把全域性許可證目錄和本地 `license` 目錄同步起來。

### 獨立啟動殼存在，但不是宿主擴充套件重點

倉庫裡確實還有 `App.xaml.cs`，它會在獨立啟動時初始化主題、日誌、Socket 和主視窗。但在當前主程式外掛裝載模型裡，文件更應該關注 manifest、選單入口、provider 和視窗本體，而不是把這個 `Application` 類誤寫成日常宿主擴充套件入口。

## 當前幾個最容易寫錯的點

### 它不是隻有“連線裝置 + 讀一幀資料”

現在的 Spectrum 實現已經把許可證同步、標定分組、視窗布局、狀態列、SQLite 結果落庫和測量畫像都串起來了。繼續把它寫成輕量測試小工具，會明顯低估當前複雜度。

### 結果持久化不只是一張表

除了光譜結果記錄，當前還會單獨落 `SpectrumMeasurementProfile` 和步驟明細 JSON。舊文件如果只寫匯出 CSV，會漏掉真正的追蹤鏈。

### 標定是按 SN 組織的

當前標定配置不是簡單的全域性單檔案路徑，而是跟序列號和活動分組繫結。這個邊界對理解現場裝置切換很重要。

### 狀態列是視窗級擴充套件，不是全域性主程式常駐項

`SpectrumStatusBarProvider` 的目標名是 `Spectrum`，它描述的是外掛視窗內部狀態列，而不是主程式任意頁面都可見的全域性狀態條。

## 推薦閱讀順序

1. `Plugins/Spectrum/MainWindow.xaml.cs`
2. `Plugins/Spectrum/SpectrometerManager.cs`
3. `Plugins/Spectrum/MainWindow.Connection.cs`
4. `Plugins/Spectrum/MainWindow.Measurement.cs`
5. `Plugins/Spectrum/Data/ViewResultManager.cs`
6. `Plugins/Spectrum/SpectrumStatusBarProvider.cs`
7. `Plugins/Spectrum/Calibration/CalibrationGroupWindow.xaml.cs`
8. `Plugins/Spectrum/manifest.json`

這樣能先看到宿主入口，再看到狀態中心、裝置鏈和結果落點。

## 繼續閱讀

- [Plugins/README.md](../../../../Plugins/README.md)
- [docs/02-developer-guide/plugin-development/overview.md](../../../02-developer-guide/plugin-development/overview.md)
- [docs/04-api-reference/plugins/standard-plugins/system-monitor.md](./system-monitor.md)