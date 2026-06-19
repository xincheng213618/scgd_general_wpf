# 專案包發布證據與版本核查表

這頁用於發布專案 `.cvxp`、現場替換 `Plugins/{ProjectName}/`，或排查專案包載入後菜單、協議、結果欄位不一致的問題。

## 版本快照

| 專案 | manifest version | `.csproj VersionPrefix` | 說明 |
| --- | --- | --- | --- |
| ProjectARVR | `1.0` | `1.6.1.11` | 包名通常跟 DLL FileVersion，不一定跟 manifest |
| ProjectARVRLite | `1.0` | `1.2.5.17` | 發布時要記錄啟用測項配置 |
| ProjectARVRPro | `1.1.7.7` | `1.1.7.7` | 仍需核對實際 DLL FileVersion |
| ProjectBlackMura | `1.0` | `1.2.6.3` | 保留 Excel、PG 串口證據 |
| ProjectHeyuan | `1.0` | `1.1.6.3` | 交付時確認繼承的 TargetFramework |
| ProjectKB | `1.0` | `1.4.2.19` | MES DLL、Modbus、背光修正要留證 |
| ProjectLUX | `1.0` | `1.1.4.25` | ProcessGroups、Recipe/Fix、SocketCode 是版本證據 |
| ProjectShiyuan | `1.0` | `1.3.5.3` | 顯式目標為 `net8.0-windows` |
| ProjectARVRPro.IntegrationDemo | 無 manifest | 無 VersionPrefix | 作為客戶 Demo 發布，不按 `.cvxp` 驗收 |

## 必留證據

| 證據 | 來源 |
| --- | --- |
| manifest | `Projects/{Name}/manifest.json` |
| 專案版本 | csproj 中的 TargetFramework、VersionPrefix、引用和複製文件 |
| 輸出 DLL | 主 DLL 的 FileVersion |
| `.cvxp` 包 | `Scripts\package_project.bat {Name} --no-upload` |
| 配置 | ProcessGroups、Recipe/Fix、Socket/MES、路徑和設備配置 |
| 協議樣例 | Socket/MES/串口/Modbus 命令與回覆 |
| 結果樣例 | SQLite、CSV、XLSX、PDF、MES 或 Socket 返回 |
| 外部依賴 | NPOI/EPPlus/NModbus、MES DLL、`FunTestDll.dll`、串口/PG/設備 SDK |
| 回退 | 上一版 `.cvxp`、專案目錄備份、配置備份、資料庫備份 |

```powershell
$name = "ProjectLUX"
Get-Content "Projects/$name/manifest.json"
Select-String "Projects/$name/$name.csproj" -Pattern "TargetFramework|VersionPrefix|ProjectReference|PackageReference|CopyToOutputDirectory"
Scripts\package_project.bat ProjectLUX --no-upload
```

## 專案專項證據

| 專案 | 必留 |
| --- | --- |
| ProjectARVR | `ProjectARVRInit`、`SwitchPGCompleted`、`ProjectARVRResult`、PG 順序、整機 CSV |
| ProjectARVRLite | 啟用測項配置、預處理開關、Socket 初始化、CSV |
| ProjectARVRPro | ProcessGroups、Recipe、PictureSwitchConfig、Legacy/新版 CSV、客戶 XLSX、SocketRelay/AOI |
| ProjectBlackMura | PG 串口命令、五色流程、Excel 報告、EPPlus、POI overlay |
| ProjectHeyuan | STX/ETX 原始報文、WBRO 四點結果、CSV、`HYMesConfig` |
| ProjectKB | `FunTestDll.dll`、`FunTestDllConfig.INI`、NModbus、MES `Collect_test`、背光修正記錄 |
| ProjectLUX | ProcessGroups、Recipe/Fix、`T00XX,SN;`、SocketCode、SQLite/CSV/PDF |
| ProjectShiyuan | `DataPath`、JND/POI CSV、固定圖像、偽彩圖、串口鏈路狀態 |

## 繼續閱讀

- [目前專案文件覆蓋清單](./current-project-coverage.md)
- [專案包能力與交接矩陣](./project-capability-matrix.md)
- [專案包執行與交接場景手冊](./project-package-playbook.md)
- [專案包交接手冊](./project-handoff.md)
