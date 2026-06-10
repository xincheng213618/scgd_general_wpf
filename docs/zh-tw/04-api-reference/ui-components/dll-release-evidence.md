# UI DLL 發布證據與現場核查表

這頁給實際發布 UI DLL 或現場替換 DLL 的人使用。它重點不是介紹功能，而是保留能證明「這批 DLL 完整、版本一致、能被 Engine/外掛/專案包消費」的證據。

## 必留證據

| 證據 | 要保存什麼 | 作用 |
| --- | --- | --- |
| 專案配置 | `TargetFrameworks`、`VersionPrefix`、`GeneratePackageOnBuild`、`PackageReadmeFile`、資源項 | 證明發布依據來自目前 `.csproj` |
| 建置記錄 | `dotnet restore`、UI 專案 build、主程式/Engine build | 證明 DLL、`.nupkg`、`.snupkg` 是同一輪產物 |
| 包內容 | 展開後的 `.nupkg` 清單 | 證明 README、native runtime、shader、CIE、CSS、工具 exe 進包 |
| 輸出目錄 | 主程式輸出目錄中 `ColorVision.*.dll` 的版本和時間 | 證明現場執行載入的是預期 DLL |
| 消費方 | Engine、關鍵外掛、關鍵專案包的 build 或煙測 | 證明不是只有 UI 包能建置 |
| 回退 | 上一批 DLL、`.nupkg`、`.snupkg`、外掛目錄備份 | 現場出問題時可快速回退 |

## 配置核查

```powershell
rg -n "TargetFrameworks|VersionPrefix|GeneratePackageOnBuild|PackageReadmeFile|PackagePath|CopyToOutputDirectory|OutputType" UI -g "*.csproj"
Get-Content Directory.Build.props
```

`ColorVision.UI.Desktop` 是 `WinExe`，但仍然參與包與依賴發布檢查。重點看 TFM、版本、README 是否進包、native DLL/CSS/工具 exe 是否 `Pack` 或 `CopyToOutputDirectory`。

## DLL 證據矩陣

| 發布單元 | `.csproj` 證據 | 包/輸出證據 |
| --- | --- | --- |
| `ColorVision.Common` | net8/net10 Windows、`VersionPrefix=1.5.5.2`、cursor resource | README 在包根，cursor 可載入 |
| `ColorVision.Themes` | `VersionPrefix=1.5.5.3`、`HandyControl`、圖示與 `uploadbg.avif` | 主題 XAML、圖示、上傳背景存在 |
| `ColorVision.UI` | `VersionPrefix=1.5.5.3`、引用 `Common`/`Themes` | 外掛、選單、配置、PropertyGrid 使用同一批 DLL |
| `ColorVision.Core` | `opencv_helper.dll`、OpenCV `4130` DLL、可選 `opencv_cuda.dll` 進 `runtimes/win-x64/native` | `.nupkg` 和輸出目錄有 native runtime |
| `ColorVision.Database` | `SqlSugarCore`、`VersionPrefix=1.5.5.3` | MySQL/SQLite Provider 和資料庫瀏覽器能開 |
| `ColorVision.SocketProtocol` | 引用 `Database`/`UI`、`VersionPrefix=1.5.5.2` | Socket 視窗、連接埠配置、訊息歷史正常 |
| `ColorVision.Scheduler` | `Quartz`、`SqlSugarCore`、README 打包 | 任務 JSON、`SchedulerHistory.db`、任務視窗正常 |
| `ColorVision.ImageEditor` | OpenCvSharp、shader、colormap、CIE CSV | 圖片、偽彩、CIE、3D、overlay、註釋能開 |
| `ColorVision.UI.Desktop` | `OutputType=WinExe`、WebView2、CSS、`aria2c.exe` | 外掛市場 README 預覽、下載器、DLL 版本視窗正常 |
| `ColorVision.Solution` | AvalonDock、AvalonEdit、WebView2、WPFHexaEditor | `.cvsln`、檔案樹、編輯器、終端、RBAC 視窗正常 |

## 現場核查命令

```powershell
$out = "ColorVision/bin/x64/Release/net10.0-windows"
Get-ChildItem $out -Filter "ColorVision*.dll" |
  Select-Object Name, LastWriteTime, @{Name="FileVersion";Expression={$_.VersionInfo.FileVersion}}
Get-ChildItem $out -Recurse -Filter "opencv*.dll" |
  Select-Object FullName, LastWriteTime, Length
```

## 交接模板

```text
發布日期：
發布人：
發布範圍：
UI 專案版本：
建置命令：
生成的 nupkg/snupkg：
主程式輸出目錄：
包內容抽檢：
native runtime 抽檢：
資源抽檢：
Engine/外掛/專案包驗證：
煙測結果：
已知限制：
回退包位置：
```

