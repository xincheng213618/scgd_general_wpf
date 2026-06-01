# 外掛開發入門

本頁提供當前倉庫可執行的最短外掛開發路徑，不再沿用舊版通用宿主、非同步生命週期和 `plugin.json` 示例。

## 先準備什麼

- Windows 開發環境
- .NET 8.0 SDK
- WPF 開發工具鏈
- 當前倉庫原始碼和主程式可執行輸出

## 最小開發路徑

### 1. 新建外掛專案

建議把外掛專案直接建在 `Plugins/<PluginId>/` 下，目標框架保持為 `net8.0-windows`。如果外掛帶介面，啟用 WPF。

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0-windows</TargetFramework>
    <UseWPF>true</UseWPF>
    <OutputType>Library</OutputType>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\UI\ColorVision.Common\ColorVision.Common.csproj" Private="false" />

如果需要顯式指定入口型別，可以繼續補 `entry_point`。

## 4. 把產物複製到主程式外掛目錄

主程式執行時會從自己的輸出目錄掃描 `Plugins/`，所以除錯時需要把外掛產物複製進去。

```xml
<Target Name="PostBuild" AfterTargets="PostBuildEvent">
  <Exec Command="xcopy /Y /E /I $(TargetDir)* $(SolutionDir)ColorVision\bin\$(ConfigurationName)\net8.0-windows\Plugins\MyPlugin\" />
</Target>
```

如果你本地輸出目錄不同，應按實際主程式輸出路徑調整。

## 5. 執行和除錯

1. 建置主程式。
2. 建置外掛專案，確認 DLL 和 `manifest.json` 已複製到外掛目錄。
3. 啟動 `ColorVision/ColorVision.csproj`。
4. 在對應選單、工具頁或外掛管理介面驗證外掛是否被載入。

## 推薦參考實現

- `Plugins/EventVWR/EventVWRPlugins.cs`
- `Plugins/EventVWR/Dump/MenuDump.cs`
- `Plugins/SystemMonitor/SystemMonitorControl.xaml.cs`
- `Plugins/README.md`

這些示例已經覆蓋了基礎外掛入口和選單擴充套件兩類常見模式。

## 常見問題

### 外掛沒有被發現

- 檢查 `manifest.json` 是否存在
- 檢查 `dllpath` 指向的 DLL 是否真實存在
- 檢查外掛目錄是否已經複製到主程式輸出目錄下的 `Plugins/<PluginId>/`

### 外掛被發現但功能沒出現

- 檢查是否只實現了基礎外掛類，但沒有實現需要的 provider 介面
- 檢查入口型別是否有公開無參構造
- 檢查型別是否為非抽象、非泛型開放型別

### 依賴衝突

- 不要重複打包平台自帶的 `ColorVision.*.dll`
- 若外掛帶 `.deps.json`，確認依賴版本不高於目標平台

## 下一步

- 想理解平台如何掃描和裝載外掛：看 [外掛生命週期](./lifecycle.md)
- 想先了解整體結構：看 [外掛開發概覽](./overview.md)
