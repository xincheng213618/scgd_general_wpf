# WindowsServicePlugin 外掛

本頁只描述當前倉庫裡實際存在的 WindowsServicePlugin 實現，不再繼續維護“運維平台總手冊 + 大而全 API 目錄”式舊稿。

## 先看這個外掛現在是什麼

按當前原始碼狀態，WindowsServicePlugin 不是單純的“服務日誌快捷方式”集合，而是一個圍繞本地 Windows 服務運維展開的外掛包。當前最明確的幾條能力線是：

- Help 選單中的服務管理器入口。
- 服務安裝與更新視窗。
- 服務日誌與本地日誌目錄快捷入口。
- 與 CVWinSMS 配置檔案和更新包的橋接。
- Wizard 步驟裡的配置讀取與 CFG 覆寫。

因此它比舊文件裡那種泛化的“服務工具箱”更具體，實際中心是 `ServiceManagerViewModel` 和 `ServiceInstallViewModel` 兩條控制鏈。

## 當前最關鍵的檔案

- `Plugins/WindowsServicePlugin/manifest.json`
- `Plugins/WindowsServicePlugin/ServiceManager/MenuServiceManager.cs`
- `Plugins/WindowsServicePlugin/ServiceManager/ServiceManagerWindow.xaml.cs`
- `Plugins/WindowsServicePlugin/ServiceManager/ServiceManagerViewModel.cs`
- `Plugins/WindowsServicePlugin/ServiceManager/ServiceInstallWindow.xaml.cs`
- `Plugins/WindowsServicePlugin/ServiceManager/ServiceInstallViewModel.cs`
- `Plugins/WindowsServicePlugin/ServiceManager/ServiceManagerConfig.cs`
- `Plugins/WindowsServicePlugin/CVWinSMS/InstallTool.cs`
- `Plugins/WindowsServicePlugin/SetMysqlConfig.cs`
- `Plugins/WindowsServicePlugin/SetServiceConfig.cs`
- `Plugins/WindowsServicePlugin/Menus/ServiceLog.cs`

如果只是想弄清外掛如何進入宿主、如何開啟服務管理器、如何做配置同步和更新，這些檔案已經覆蓋主體。

## 當前接入宿主的幾條鏈

### Help 選單中的服務管理器入口

`MenuServiceManager` 當前掛在 `Help` 選單下，執行時直接開啟 `ServiceManagerWindow`。

除此之外，同檔案裡的 `ServiceManagerAppProvider` 還實現了 `IThirdPartyAppProvider`，把“服務管理器”作為一個內部工具暴露給宿主的第三方應用入口。

這意味著它不只是一條選單命令，而是至少有兩條 UI 接入鏈。

### 服務日誌選單樹

`ServiceLog` 當前也是 `Help` 選單下的一個根選單項。圍繞它，外掛繼續注入多組日誌快捷入口：

- HTTP 日誌頁面
- 依據 `CVWinSMSConfig.BaseLocation` 解析出來的本地日誌目錄

例如 `ExportRCServiceLog`、`Exportx64ServiceLog` 這類型別會直接開啟本地 URL；而帶字尾的目錄版本則會拼接服務目錄下的 `log` 資料夾。

### Wizard 與初始化入口

`InstallTool` 目前同時實現了：

- `MenuItemBase`
- `IWizardStep`
- `IMainWindowInitialized`

它既能作為選單入口開啟或定位 CVWinSMS，也能在主視窗初始化後檢查更新，還能進入安裝嚮導的聚合鏈。

因此這個外掛當前並不是“只有服務管理視窗”這麼簡單，CVWinSMS 相關的引導和更新邏輯也是宿主接入面的一部分。

### manifest 資訊

按當前 `manifest.json`，外掛公開的裝載資訊是：

- `Id = "WindowsServicePlugin"`
- `name = "視彩服務外掛"`
- `version = "1.0"`
- `dllpath = "WindowsServicePlugin.dll"`
- `requires = "1.3.12.34"`

這比舊文件裡那些額外拼出來的依賴矩陣更接近當前真實裝載模型。

## 服務管理器當前怎麼工作

`ServiceManagerWindow` 本身很薄，視窗初始化時直接把 `DataContext` 設為 `ServiceManagerViewModel.Instance`，並在日誌文字變化時自動滾動日誌區域。

真正的執行時中心在 `ServiceManagerViewModel`。按當前實現，它至少負責：

- 維護預設服務列表。
- 維護 MySQL 和 MQTT 管理器。
- 維護當前版本、可用版本、忙碌狀態、進度和日誌文字。
- 暴露管理員模式狀態和“以管理員身份重啟”命令。
- 暴露一鍵啟停、重新整理、開啟目錄和開啟配置檔案等命令。

### 當前預設管理的服務

`ServiceManagerConfig.GetDefaultServiceEntries()` 裡當前明確列出了：

- `RegistrationCenterService`
- `CVMainService_x64`
- `CVMainService_dev`
- `CVArchService`

所以這頁文件應當圍繞這些真實服務項寫，而不是繼續泛化成“任意服務編排框架”。

### 路徑與版本檢測

`ServiceManagerConfig` 當前會優先嚐試：

1. 從登錄檔的 `RegistrationCenterService` 讀取安裝路徑。
2. 如果失敗，再嘗試從 CVWinSMS 的 `App.config` 讀取 `BaseLocation` 和 `MysqlPort`。

`RefreshAll()` 則會順帶重新整理每個服務狀態，並根據 `RegistrationCenterService` 的版本文字更新當前版本顯示。

## 安裝與更新鏈當前怎麼展開

`ServiceInstallWindow` 本身同樣很薄，核心邏輯在 `ServiceInstallViewModel`。當前這條鏈真正管理的是：

- 服務安裝包選擇
- MySQL ZIP 選擇
- MQTT 安裝程式選擇
- 下載目錄選擇
- 線上檢查更新
- 備份與恢復
- 一鍵安裝全部元件

按當前實現，這個視窗關心的不是單一的“下載最新版”，而是完整的安裝編排狀態，包括進度、日誌、自動啟動、資料庫更新和備份開關。

## 當前與 CVWinSMS 的關係

`CVWinSMSConfig` 負責維護 `CVWinSMSPath`、更新地址和自動更新開關，並提供從外部 `App.config` 解析出來的 `BaseLocation`。

`InstallTool` 則負責：

- 檢測現有 CVWinSMS 可執行檔案。
- 在需要時下載更新包。
- 解壓並替換舊目錄。
- 以管理員權限重新啟動 CVWinSMS。

這說明 WindowsServicePlugin 當前不是單獨封閉的一套服務運維 UI，而是明確帶著對外部 CVWinSMS 工具的橋接和遷移邏輯。

## Wizard 步驟當前怎麼落地

### 讀取服務配置

`SetMysqlConfig` 會讀取 CVWinSMS 目錄下的 `config/App.config`，把其中的 MySQL 配置寫回到當前宿主使用的資料庫配置物件裡。

### 覆寫服務 CFG

`SetServiceConfigStep` 會讀取同一個 `App.config`，然後用當前宿主裡的：

- MySQL 設定
- MQTT 設定
- RC 設定

去更新服務目錄中的：

- `cfg/MySql.config`
- `cfg/MQTT.config`
- `cfg/WinService.config`

寫回完成後，它還會嘗試重啟 `RegistrationCenterService`。這是一條真正會修改服務端配置的運維鏈，不應繼續被寫成“普通向導按鈕”。

## 當前幾個最容易寫錯的點

### 它不只是日誌選單外掛

雖然當前確實有一整組日誌入口，但外掛主體仍然是服務管理與安裝更新控制鏈。如果只寫日誌選單，會把主要實現面縮得過小。

### `Application` 殼不是宿主擴充套件重點

倉庫裡有 `App.xaml.cs`，但它當前更像獨立啟動或除錯殼。對於主程式外掛文件，更應該關注 manifest、選單、provider、view model 和 wizard 步驟，而不是把這個 `Application` 型別誤當成日常外掛入口。

### 配置同步會真的改服務端 CFG

`SetServiceConfigStep` 不是隻讀檢查器。它會把當前宿主配置寫回多個服務目錄下的配置檔案，並嘗試重啟註冊中心服務。

### 服務管理器當前是單例中心

`ServiceManagerViewModel.Instance` 是當前視窗和命令共用的狀態中心。繼續把它寫成“每次開啟視窗重新構造一套上下文”的模型，會和當前實現不符。

## 推薦閱讀順序

1. `Plugins/WindowsServicePlugin/ServiceManager/MenuServiceManager.cs`
2. `Plugins/WindowsServicePlugin/ServiceManager/ServiceManagerViewModel.cs`
3. `Plugins/WindowsServicePlugin/ServiceManager/ServiceManagerConfig.cs`
4. `Plugins/WindowsServicePlugin/ServiceManager/ServiceInstallViewModel.cs`
5. `Plugins/WindowsServicePlugin/CVWinSMS/InstallTool.cs`
6. `Plugins/WindowsServicePlugin/SetMysqlConfig.cs`
7. `Plugins/WindowsServicePlugin/SetServiceConfig.cs`
8. `Plugins/WindowsServicePlugin/Menus/ServiceLog.cs`
9. `Plugins/WindowsServicePlugin/manifest.json`

這樣能先看到宿主入口，再看到狀態中心、配置橋接和安裝鏈。

## 繼續閱讀

- [Plugins/README.md](../../../../Plugins/README.md)
- [docs/02-developer-guide/plugin-development/overview.md](../../../02-developer-guide/plugin-development/overview.md)
- [docs/04-api-reference/plugins/standard-plugins/eventvwr.md](./eventvwr.md)