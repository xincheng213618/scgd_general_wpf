# 外掛開發概覽

本頁只說明當前倉庫裡真實可用的外掛開發模型，避免繼續沿用舊的通用化介面示例。

## 當前外掛模型

ColorVision 的外掛以獨立目錄部署在主程式執行目錄下的 `Plugins/` 中。主程式啟動時會掃描每個外掛目錄，讀取 `manifest.json`，再按清單指定的 DLL 裝載程式集。

當前程式碼中可以直接確認的幾個關鍵點：

- 外掛基礎介面位於 `UI/ColorVision.Common/Interfaces/IPlugin.cs`
- 外掛清單位於 `UI/ColorVision.UI/Plugins/PluginManifest.cs`
- 外掛裝載邏輯位於 `UI/ColorVision.UI/Plugins/PluginLoader.cs`

## 關鍵組成

### 1. 外掛介面

倉庫裡當前可見的基礎介面非常輕量：

```csharp
public interface IPlugin
{
    string Header { get; }
    string Description { get; }
    void Execute();
}
```

如果只是做一個簡單外掛入口，通常從 `IPluginBase` 開始更方便。

### 2. manifest.json

外掛目錄通常需要提供 `manifest.json`。當前清單物件至少包含這些欄位：

- `id`
- `manifest_version`
- `name`
- `version`
- `requires`
- `description`
- `dllpath`
- `author`
- `url`
- `entry_point`
- `icon`

其中最核心的是外掛標識、描述和 DLL 路徑；`entry_point` 在需要顯式指定入口型別時使用。

### 3. 裝載流程

主程式啟動後，`PluginLoader` 會：

1. 掃描 `Plugins/` 下的外掛目錄。
2. 讀取每個目錄的 `manifest.json`。
3. 根據清單計算 DLL 路徑。
4. 校驗依賴與版本。
5. 裝載程式集，並把外掛資訊寫入內部快取。

如果外掛目錄沒有清單，平台仍會嘗試按“目錄名同名 DLL”的方式裝載，但這不再是推薦形態。

## 推薦目錄結構

```text
Plugins/
└── MyPlugin/
    ├── manifest.json
    ├── MyPlugin.csproj
    ├── MyPlugin.dll
    ├── README.md
    ├── CHANGELOG.md
    ├── Assets/
    └── Sources/ 或 *.cs/*.xaml
```

## 開發建議

### 平台內開發

- 在倉庫內新建 `Plugins/<PluginId>/` 專案。
- 建置後把輸出複製到主程式輸出目錄下的 `Plugins/<PluginId>/`。
- 優先參考現有標準外掛的目錄和打包方式。

### 外部獨立開發

- 外掛最終交付物應保持為一個可直接複製的完整目錄。
- 不要重複打包平台主程式已自帶的 `ColorVision.*.dll`。
- 第三方執行時依賴應和外掛產物一起釋出。

## 建議閱讀順序

1. 先看 [外掛開發總覽](./README.md)
2. 再看 [外掛開發入門](./getting-started.md)
3. 需要理解裝載和執行階段時，再看 [外掛生命週期](./lifecycle.md)
4. 想參考現成外掛時，先看 [現有外掛能力說明](../../04-api-reference/plugins/README.md)，再進入 [Conoscope](../../04-api-reference/plugins/standard-plugins/conoscope.md)、[Spectrum](../../04-api-reference/plugins/standard-plugins/spectrum.md) 或 [SystemMonitor](../../04-api-reference/plugins/standard-plugins/system-monitor.md)

## 說明

- 舊版文件裡出現的 `IPluginContext`、非同步生命週期介面和獨立外掛宿主模型，並不是當前倉庫主路徑裡直接可見的基礎介面。
- 如果文件和程式碼不一致，以 `UI/ColorVision.Common/Interfaces/IPlugin.cs`、`UI/ColorVision.UI/Plugins/PluginLoader.cs` 和現有外掛專案為準。

