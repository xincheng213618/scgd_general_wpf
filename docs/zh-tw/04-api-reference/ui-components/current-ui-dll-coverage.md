# 目前 UI DLL 文件覆蓋清單

本頁是 `UI/` 目錄的模組級台帳。交接人員先用它確認目前實際存在多少個 UI 專案、每個專案對應哪一頁文件、發布 DLL 時要看哪些證據，再進入單一元件頁或發布手冊。

更新時間：2026-06-10。

## 目前結論

- 目前 `UI/` 下有 10 個真實專案目錄，每個目錄都有 `.csproj` 和 `README.md`。
- 10 個專案都已經有對應的文件頁，文件頁放在 `docs/04-api-reference/ui-components/`。
- 10 個專案都啟用了 `GeneratePackageOnBuild`，發布時不能只檢查主程式輸出目錄，還要檢查 NuGet 包內容。
- `ColorVision.Common`、`ColorVision.Themes`、`ColorVision.UI`、`ColorVision.Core`、`ColorVision.Database`、`ColorVision.SocketProtocol`、`ColorVision.Scheduler` 支援 `net8.0-windows7.0` 與 `net10.0-windows7.0`。
- `ColorVision.ImageEditor`、`ColorVision.UI.Desktop`、`ColorVision.Solution` 目前只面向 `net10.0-windows7.0`。
- `ColorVision.Core` 是 native/runtime 風險最高的 UI 包；`ColorVision.ImageEditor` 是影像互動和結果 overlay 風險最高的 UI 包；`ColorVision.UI.Desktop` 是桌面工具和現場運維入口風險最高的 UI 包。

## 模組覆蓋表

| UI 專案 | 專案檔案 | 原始碼 README | 文件頁 | 發布形態 | 交接重點 |
| --- | --- | --- | --- | --- | --- |
| `UI/ColorVision.Common/` | `ColorVision.Common.csproj` | 有 | [ColorVision.Common](./ColorVision.Common.md) | DLL + NuGet | MVVM 基礎、外掛介面、共享契約、狀態列後設資料 |
| `UI/ColorVision.Themes/` | `ColorVision.Themes.csproj` | 有 | [ColorVision.Themes](./ColorVision.Themes.md) | DLL + NuGet | 主題資源字典、視窗樣式、明暗主題切換 |
| `UI/ColorVision.UI/` | `ColorVision.UI.csproj` | 有 | [ColorVision.UI](./ColorVision.UI.md) | DLL + NuGet | 配置、選單、外掛載入、PropertyGrid、快捷鍵、多語言 |
| `UI/ColorVision.Core/` | `ColorVision.Core.csproj` | 有 | [ColorVision.Core](./ColorVision.Core.md) | DLL + NuGet + native runtime | `HImage`、OpenCV helper、影片/影像互操作、`runtimes/win-x64/native` |
| `UI/ColorVision.Database/` | `ColorVision.Database.csproj` | 有 | [ColorVision.Database](./ColorVision.Database.md) | DLL + NuGet | SqlSugar DAO、資料庫瀏覽器、MySQL/SQLite 接入 |
| `UI/ColorVision.SocketProtocol/` | `ColorVision.SocketProtocol.csproj` | 有 | [ColorVision.SocketProtocol](./ColorVision.SocketProtocol.md) | DLL + NuGet | 本地 TCP 服務、JSON/Text 訊息分發、訊息歷史 |
| `UI/ColorVision.Scheduler/` | `ColorVision.Scheduler.csproj` | 有 | [ColorVision.Scheduler](./ColorVision.Scheduler.md) | DLL + NuGet | Quartz 排程、任務恢復、任務歷史、管理視窗 |
| `UI/ColorVision.ImageEditor/` | `ColorVision.ImageEditor.csproj` | 有 | [ColorVision.ImageEditor](./ColorVision.ImageEditor.md) | DLL + NuGet + 影像資源 | `ImageView`、`DrawCanvas`、工具列、結果 overlay、3D/CIE 檢視 |
| `UI/ColorVision.UI.Desktop/` | `ColorVision.UI.Desktop.csproj` | 有 | [ColorVision.UI.Desktop](./ColorVision.UI.Desktop.md) | WinExe + NuGet | 設定視窗、嚮導、外掛市場、下載工具、DLL 版本查看 |
| `UI/ColorVision.Solution/` | `ColorVision.Solution.csproj` | 有 | [ColorVision.Solution](./ColorVision.Solution.md) | DLL + NuGet | 工作區、編輯器、終端、多圖查看、本地 RBAC、專案管理 |

## 發布邊界

| 邊界 | 包含模組 | 發布時先看 | 現場風險 |
| --- | --- | --- | --- |
| 基礎共享層 | `ColorVision.Common`、`ColorVision.Themes`、`ColorVision.UI` | [UI DLL 元件手冊](./component-handbook.md)、[UI DLL 發布矩陣](./release-matrix.md) | 選單、配置、外掛入口、主題資源失效會影響多個上層視窗 |
| 影像與 native 層 | `ColorVision.Core`、`ColorVision.ImageEditor` | [UI DLL 發布證據與現場核查表](./dll-release-evidence.md)、[UI 執行時元件交接手冊](./ui-runtime-handoff.md) | 缺 native DLL、影像資源或 overlay 註冊失敗，會直接影響檢測結果查看 |
| 資料與服務視窗層 | `ColorVision.Database`、`ColorVision.SocketProtocol`、`ColorVision.Scheduler` | [UI DLL 發布場景手冊](./ui-dll-release-playbook.md)、[UI 元件目錄](./control-catalog.md) | 資料源、Socket 監聽、排程歷史和背景任務排障依賴這些視窗 |
| 桌面工具與工作區層 | `ColorVision.UI.Desktop`、`ColorVision.Solution` | [UI 執行時元件交接手冊](./ui-runtime-handoff.md)、對應單模組頁 | 外掛市場、下載工具、Solution 工作區、RBAC 和本地專案管理集中在這裡 |

## 證據來源

本輪審計使用下面幾類證據確認覆蓋狀態：

- `Get-ChildItem UI -Directory`：確認目前真實 UI 專案目錄。
- 每個 `UI/ColorVision.*/` 目錄下的 `.csproj`：確認目標框架、`VersionPrefix`、`GeneratePackageOnBuild`、`PackageReadmeFile`、資源複製規則。
- 每個 `UI/ColorVision.*/README.md`：確認包內 README 來源。
- `docs/04-api-reference/ui-components/*.md`：確認每個 UI 專案都有獨立文件頁。
- `Directory.Build.props`：確認全域版本後設資料、作者、倉庫地址和 `ColorVision.snk` 條件強名稱簽名。

## 重點風險

| 風險點 | 影響模組 | 接手時怎麼查 |
| --- | --- | --- |
| native runtime 遺失 | `ColorVision.Core` | 檢查 NuGet 包和宿主輸出目錄是否包含 `runtimes/win-x64/native` 下的 OpenCV/helper DLL |
| 圖像 overlay 不顯示 | `ColorVision.ImageEditor`、Engine 結果顯示鏈 | 先看 [UI 執行時元件交接手冊](./ui-runtime-handoff.md)，再看 Engine 的 [結果交接鏈路](../engine-components/result-handoff-chain.md) |
| 選單或外掛入口不出現 | `ColorVision.UI`、`ColorVision.Common`、`ColorVision.UI.Desktop` | 檢查選單註冊、外掛發現、權限和配置項是否被載入 |
| 桌面工具缺檔案 | `ColorVision.UI.Desktop` | 檢查 `OutputType=WinExe`、WebView2、CSS、`aria2c.exe`、資源檔案的複製規則 |
| 工作區功能異常 | `ColorVision.Solution` | 檢查編輯器、終端、多圖查看、本地 RBAC 與專案目錄權限 |
| net8/net10 混用 | 全部 UI DLL | 檢查宿主目標框架、外掛目標框架、Engine 包引用回退版本是否一致 |

## 維護規則

新增、刪除或重新命名 UI DLL 時，必須同步更新：

1. 本頁的模組覆蓋表和發布邊界。
2. `docs/04-api-reference/ui-components/README.md` 的 UI 包清單。
3. 對應的單模組文件頁，例如 `ColorVision.Xxx.md`。
4. [UI DLL 元件手冊](./component-handbook.md)。
5. [UI 元件目錄](./control-catalog.md)，如果新增了視窗、控制項、Provider 或擴充點。
6. [UI DLL 發布矩陣](./release-matrix.md)。
7. [UI DLL 發布證據與現場核查表](./dll-release-evidence.md)。
8. [UI 執行時元件交接手冊](./ui-runtime-handoff.md)，如果模組有執行時發現、選單、設定或服務視窗。
9. `docs/.vitepress/i18n/navigation-data.json` 中的側邊欄導航。

## 快速複查命令

```powershell
Get-ChildItem UI -Directory | Sort-Object Name | Select-Object -ExpandProperty Name

Get-ChildItem docs/04-api-reference/ui-components -File |
  Sort-Object Name |
  Select-Object -ExpandProperty Name

Get-ChildItem UI -Directory | Sort-Object Name | ForEach-Object {
  $csproj = Get-ChildItem $_.FullName -Filter *.csproj -File | Select-Object -First 1
  $readme = Test-Path (Join-Path $_.FullName 'README.md')
  "$($_.Name): csproj=$($csproj.Name) readme=$readme"
}
```

複查結果如果出現「有原始碼專案但沒有文件頁」或「有文件頁但原始碼專案已不存在」，優先修本文檔和側邊欄，再處理翻譯版本。
