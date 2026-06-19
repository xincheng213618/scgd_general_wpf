# UI DLL 發布場景手冊

本頁面向發布 UI DLL、現場替換 DLL、排查依賴缺失、為 Engine/外掛/專案包準備 UI 包的維護人員。它按發布場景組織，而不是按源碼目錄。

## 發布範圍判斷

| 任務 | 最小發布範圍 | 也要驗證 |
| --- | --- | --- |
| 只改 README 或說明資源 | 對應 UI 專案包 | README 是否進入包根 |
| 改 `Common` 介面、命令、工具 | `Common` 和上層 UI 包 | 主程序、外掛、PropertyGrid、ImageEditor |
| 改 `Themes` 樣式或窗口基類 | `Themes`、`UI` 和使用主題的窗口 | 主窗口、設定、外掛/專案窗口 |
| 改 `UI` 選單、配置、外掛載入、PropertyGrid | `UI` 和外掛/專案消費方 | 外掛管理、設定、屬性編輯器、狀態列 |
| 改 `Core` native bridge | `Core`、`ImageEditor` 和 Engine 圖像路徑 | native DLL、圖像開啟、OpenCV |
| 改 `ImageEditor` overlay、偽彩、CIE、3D | `ImageEditor`，通常連同 `Core` | 資源、普通圖、結果圖、CIE/3D |
| 改 `Database`、`SocketProtocol`、`Scheduler` | 對應包和 UI 入口 | 管理窗口、配置、歷史庫 |
| 改 `UI.Desktop` 或 `Solution` | 桌面工具層或工作區層 | 市場、下載器、WebView2、`.cvsln`、終端 |
| 現場替換外掛或專案包 | 宿主 `ColorVision.*.dll` 一組 | `.deps.json`、manifest、宿主 DLL 版本 |

## 構建與產物檢查

```powershell
rg -n "VersionPrefix|GeneratePackageOnBuild|PackageReadmeFile|PackagePath|CopyToOutputDirectory|TargetFrameworks|OutputType" UI -g "*.csproj"
dotnet restore
dotnet build ColorVision/ColorVision.csproj -c Release -p:Platform=x64
```

如果只分析單包失敗，按底層到高層構建：`Common`、`Themes`、`UI`、`Core`、`ImageEditor`、`Database`、`SocketProtocol`、`Scheduler`、`UI.Desktop`、`Solution`。

## 場景驗收

| 場景 | 驗收 |
| --- | --- |
| 基礎 UI 包 | 主程序能啟動，選單、設定、PropertyGrid、外掛管理器正常 |
| Core/ImageEditor | `.nupkg` 有 native runtime、shader、colormap、CIE，普通圖/偽彩/overlay/3D 正常 |
| Database/Socket/Scheduler | 資料庫瀏覽、Socket 服務和訊息歷史、任務列表和歷史庫正常 |
| UI.Desktop/Solution | 設定、市場、下載器、DLL 版本窗口、工作區、終端、WebView2 正常 |
| 外部 NuGet 交付 | 鎖定 `UIProjectPackageVersion`，restore/build 不解析到非預期包 |

## 發布交接記錄

每次發布至少留下：發布日期、發布範圍、版本、構建命令、包抽檢、運行驗收、外部交付方式、回退包位置和已知限制。

## 繼續閱讀

- [UI DLL 發布手冊](./publishing.md)
- [UI DLL 發布矩陣](./release-matrix.md)
- [UI DLL 元件手冊](./component-handbook.md)
