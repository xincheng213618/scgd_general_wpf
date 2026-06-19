# 現有外掛能力說明

本章只把目前 `Plugins/` 目錄中真實存在的外掛列為當前能力。沒有 `Plugins/<Name>/`、`.csproj` 和 `manifest.json` 的名稱，不再放在現有外掛入口中。

## 當前外掛總覽

| 外掛 | 原始碼目錄 | manifest Id | 主要能力 | 文件 |
| --- | --- | --- | --- | --- |
| Conoscope | `Plugins/Conoscope/` | `Conoscope` | VAM/錐鏡圖像觀察、關注點、色域與對比度分析 | [Conoscope](./standard-plugins/conoscope.md) |
| Spectrum | `Plugins/Spectrum/` | `Spectrum` | 光譜儀連接、標定、測量、EQE、SQLite 結果 | [Spectrum](./standard-plugins/spectrum.md) |
| SystemMonitor | `Plugins/SystemMonitor/` | `SystemMonitor` | 效能監控、狀態列、磁碟/網路/程序資訊 | [SystemMonitor](./standard-plugins/system-monitor.md) |
| EventVWR | `Plugins/EventVWR/` | `EventVWR` | Windows 事件錯誤查看、Dump 配置 | [EventVWR](./standard-plugins/eventvwr.md) |
| WindowsServicePlugin | `Plugins/WindowsServicePlugin/` | `WindowsServicePlugin` | CVWindowsService 安裝、註冊、MySQL/MQTT 配置 | [WindowsServicePlugin](./standard-plugins/windows-service.md) |

## 先讀哪一頁

| 目的 | 先看 |
| --- | --- |
| 確認每個當前外掛都有文件 | [當前外掛文件覆蓋清單](./current-plugin-coverage.md) |
| 橫向比較能力、入口和風險 | [外掛能力與交接矩陣](./plugin-capability-matrix.md) |
| 排查載入、缺 DLL、權限、Socket 或打包問題 | [外掛執行與交接場景手冊](./plugin-handoff-playbook.md) |
| 發版、現場替換或交接 | [現有外掛現場驗收與交接清單](./plugin-field-acceptance.md) |
| 記錄 manifest、DLL 版本、`.cvxp`、native 檔案和回退包 | [外掛發布證據與版本核查表](./plugin-release-evidence.md) |
| 開發新外掛 | [外掛開發手冊](../../02-developer-guide/plugin-development/README.md) |

## 裝載與交付模型

外掛由 `UI/ColorVision.UI/Plugins/PluginLoader.cs` 裝載：

1. 掃描主程式輸出目錄下的 `Plugins/` 一級子目錄。
2. 優先讀取每個目錄的 `manifest.json`。
3. 使用 manifest 的 `Id` 更新外掛配置快取。
4. 按 `dllpath` 找到主外掛 DLL。
5. 如果目錄內存在唯一 `.deps.json`，檢查 `ColorVision.*` 依賴版本。
6. 透過 `Assembly.LoadFrom(...)` 載入程序集。

推薦交付形態：

```text
ColorVision/bin/x64/<Config>/net10.0-windows/Plugins/<PluginName>/
  <PluginName>.dll
  manifest.json
  README.md
  CHANGELOG.md
  PackageIcon.png        # optional
```

## 不在當前外掛清單中的名稱

Pattern、ImageProjector、ScreenRecorder 目前沒有對應的 `Plugins/<Name>/` 原始碼目錄、`.csproj` 和 `manifest.json`，不再作為當前外掛能力入口。若日後恢復，先按 [外掛執行與交接場景手冊](./plugin-handoff-playbook.md) 補齊原始碼、manifest、README、CHANGELOG、構建複製與打包驗證，再加入本章。

## 維護要求

- 新增外掛時必須補 `Plugins/<Name>/README.md` 和 docs 站點頁。
- 新增、刪除或恢復外掛時，同步更新 [當前外掛文件覆蓋清單](./current-plugin-coverage.md)、能力矩陣、現場驗收清單和導航。
- 修改入口、Socket、管理員權限、native 依賴、資料庫或登錄檔行為時，同步更新對應單外掛頁和 [外掛發布證據與版本核查表](./plugin-release-evidence.md)。
