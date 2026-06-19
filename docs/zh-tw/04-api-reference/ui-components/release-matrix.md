# UI DLL 發布矩陣

本頁面向負責發布、交付、排查 DLL 缺失問題的維護人員。它不講 UI 如何操作，而是把 `UI/` 下每個發布單元的構建形態、依賴邊界、包內資源和發布後驗收點放在一起。

## 發布單元

| 發布單元 | 專案檔 | 目標框架 | 版本 | 輸出 | 依賴重點 |
| --- | --- | --- | --- | --- | --- |
| `ColorVision.Common` | `UI/ColorVision.Common/ColorVision.Common.csproj` | net8/net10 Windows | `1.5.5.2` | DLL + `.nupkg` + `.snupkg` | WPF、WinForms、共享介面 |
| `ColorVision.Themes` | `UI/ColorVision.Themes/ColorVision.Themes.csproj` | net8/net10 Windows | `1.5.5.3` | DLL + packages | `HandyControl`、主題資源 |
| `ColorVision.UI` | `UI/ColorVision.UI/ColorVision.UI.csproj` | net8/net10 Windows | `1.5.5.3` | DLL + packages | `Common`、`Themes`、log4net、Newtonsoft.Json |
| `ColorVision.Core` | `UI/ColorVision.Core/ColorVision.Core.csproj` | net8/net10 Windows | `1.5.5.2` | DLL + packages + native runtime | `opencv_helper.dll`、OpenCV runtime、可選 `opencv_cuda.dll` |
| `ColorVision.Database` | `UI/ColorVision.Database/ColorVision.Database.csproj` | net8/net10 Windows | `1.5.5.3` | DLL + packages | `ColorVision.UI`、SqlSugarCore |
| `ColorVision.SocketProtocol` | `UI/ColorVision.SocketProtocol/ColorVision.SocketProtocol.csproj` | net8/net10 Windows | `1.5.5.2` | DLL + packages | `ColorVision.UI`、`ColorVision.Database` |
| `ColorVision.Scheduler` | `UI/ColorVision.Scheduler/ColorVision.Scheduler.csproj` | net8/net10 Windows | `1.5.5.2` | DLL + packages | `ColorVision.UI`、Quartz、SqlSugarCore |
| `ColorVision.ImageEditor` | `UI/ColorVision.ImageEditor/ColorVision.ImageEditor.csproj` | net10 Windows | `1.5.5.5` | DLL + packages + resources | `Core`、`UI`、OpenCvSharp、HelixToolkit、ScottPlot |
| `ColorVision.UI.Desktop` | `UI/ColorVision.UI.Desktop/ColorVision.UI.Desktop.csproj` | net10 Windows | `1.5.5.3` | `WinExe` + packages | `Database`、`UI`、WebView2、Markdig |
| `ColorVision.Solution` | `UI/ColorVision.Solution/ColorVision.Solution.csproj` | net10 Windows | `1.5.5.2` | DLL + packages | `ImageEditor`、`UI.Desktop`、AvalonDock、AvalonEdit、WebView2 |

## 包內資源驗收

| 發布單元 | 必查資源 | 缺失表現 |
| --- | --- | --- |
| `Common` | README、cursor 資源 | 基礎工具游標或包說明缺失 |
| `Themes` | 圖標、`uploadbg.avif`、主題 XAML | 圖標、背景或主題載入失敗 |
| `UI` | 插件、配置、PropertyEditor 類型 | 菜單、設定、屬性編輯器異常 |
| `Core` | `runtimes/win-x64/native/opencv_helper.dll` 和 OpenCV DLL | `DllNotFoundException`、圖像/影片失敗 |
| `Database` | README、SqlSugar 依賴 | 資料庫瀏覽器或 DAO 異常 |
| `SocketProtocol` | README、Socket 配置、訊息實體 | Socket 管理、歷史、JSON/Text 分發異常 |
| `Scheduler` | README、Quartz/SqlSugar 依賴 | 任務管理或歷史庫異常 |
| `ImageEditor` | shader、colormap、CIE CSV、圖標、OpenCvSharp runtime | 偽彩、CIE、3D、圖像開啟失敗 |
| `UI.Desktop` | `github-markdown.css`、`aria2c.exe` | 市場 README 預覽或下載器失敗 |
| `Solution` | AvalonDock/AvalonEdit/WebView2/WPFHexaEditor | 工作區、編輯器、終端或 RBAC 異常 |

## 發布後煙測

| 能力 | 驗證 |
| --- | --- |
| 主程序啟動 | Release 輸出能啟動 |
| 外掛裝載 | 外掛管理器能讀 manifest、README、CHANGELOG |
| 設定與 PropertyGrid | 設定能保存，配置物件能開屬性編輯器 |
| ImageEditor | 圖片、偽彩、CIE、注釋、3D 至少各驗一次 |
| 資料庫 | MySQL/SQLite Provider 能列庫表 |
| Socket | 啟動服務、發 JSON/Text、看歷史 |
| Scheduler | 任務列表和歷史庫可讀 |
| Solution | `.cvsln`、檔案樹、文字編輯、終端、布局恢復 |
