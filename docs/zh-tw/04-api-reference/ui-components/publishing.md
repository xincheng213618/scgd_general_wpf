# UI DLL 發布手冊

本頁說明 `UI/` 下 DLL/NuGet 包的發布方式，面向維護 UI 類庫、交付 Engine 依賴包、排查外掛缺 DLL 的人員。

需要快速確認每個 DLL 的版本、TFM、依賴、包內資源和煙測範圍時，先看 [UI DLL 發布矩陣](./release-matrix.md)。如果接到的是“現場替換 DLL”“外掛缺 DLL”“Engine 只拿 NuGet 包”等具體任務，先按 [UI DLL 發布場景手冊](./ui-dll-release-playbook.md) 判斷範圍。

## 發布對象

- 基礎包：`ColorVision.Common`、`ColorVision.Themes`、`ColorVision.UI`
- 資料與通訊包：`ColorVision.Database`、`ColorVision.SocketProtocol`、`ColorVision.Scheduler`
- 影像包：`ColorVision.Core`、`ColorVision.ImageEditor`
- 桌面殼層包：`ColorVision.UI.Desktop`、`ColorVision.Solution`

`ColorVision.UI.Desktop` 的輸出類型是 `WinExe`，但仍啟用打包；交接時要確認它是桌面輔助模組，不是倉庫主程序入口。

## 構建命令

```powershell
dotnet restore
dotnet build UI/ColorVision.Common/ColorVision.Common.csproj -c Release -p:Platform=x64
dotnet build UI/ColorVision.Themes/ColorVision.Themes.csproj -c Release -p:Platform=x64
dotnet build UI/ColorVision.UI/ColorVision.UI.csproj -c Release -p:Platform=x64
dotnet build UI/ColorVision.Core/ColorVision.Core.csproj -c Release -p:Platform=x64
dotnet build UI/ColorVision.Database/ColorVision.Database.csproj -c Release -p:Platform=x64
dotnet build UI/ColorVision.SocketProtocol/ColorVision.SocketProtocol.csproj -c Release -p:Platform=x64
dotnet build UI/ColorVision.Scheduler/ColorVision.Scheduler.csproj -c Release -p:Platform=x64
dotnet build UI/ColorVision.ImageEditor/ColorVision.ImageEditor.csproj -c Release -p:Platform=x64
dotnet build UI/ColorVision.UI.Desktop/ColorVision.UI.Desktop.csproj -c Release -p:Platform=x64
dotnet build UI/ColorVision.Solution/ColorVision.Solution.csproj -c Release -p:Platform=x64
```

多數 UI 專案設定 `GeneratePackageOnBuild=True`，Release 構建會同步生成 `.nupkg` 和 `.snupkg`。

## 發布前檢查

```powershell
rg -n "VersionPrefix|GeneratePackageOnBuild|PackageReadmeFile|PackagePath|CopyToOutputDirectory|TargetFrameworks|OutputType" UI -g "*.csproj"
Get-Content Directory.Build.props
```

| 檢查項 | 說明 |
| --- | --- |
| 版本 | 每個 `.csproj` 的 `VersionPrefix` |
| 強名稱 | `ColorVision.snk` 存在時不要關閉簽名 |
| 目標框架 | 宿主、Engine、外掛和專案包是否相容 |
| README | 是否進入包根目錄 |
| native runtime | `ColorVision.Core` 的 OpenCV DLL 是否完整 |
| 資源 | `ImageEditor` shader、colormap、CIE、Themes 圖標、UI.Desktop `aria2c.exe` |

## 發布後驗證

1. 構建主程序：`dotnet build ColorVision/ColorVision.csproj -c Release -p:Platform=x64`
2. 檢查輸出目錄中的 `ColorVision.*.dll`。
3. 打開主程序，驗證設定、PropertyGrid、ImageEditor、資料庫、Socket、Scheduler、外掛管理。
4. 如果包含 `Core` 或 `ImageEditor`，額外驗證普通圖片、偽彩、CIE、注釋導入導出、3D 和 OpenCV/native DLL。
5. 如果提供給 Engine 包引用環境，鎖定 `UIProjectPackageVersion`，不要長期依賴 `*`。

## 繼續閱讀

- [UI DLL 發布場景手冊](./ui-dll-release-playbook.md)
- [UI DLL 發布矩陣](./release-matrix.md)
- [UI DLL 元件手冊](./component-handbook.md)
