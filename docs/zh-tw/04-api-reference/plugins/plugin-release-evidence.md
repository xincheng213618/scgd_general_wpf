# 外掛發布證據與版本核查表

這頁用於發布 `.cvxp`、現場替換外掛目錄，或排查“外掛已複製但宿主未載入”的問題。它補充現場驗收，重點保留 manifest、DLL 文件版本、`.cvxp` 檔名、依賴、native 檔案、權限和回退包。

## 為什麼要記錄版本證據

目前外掛的 `manifest.version` 和 `.csproj VersionPrefix` 不一定相同。

| 外掛 | manifest version | `.csproj VersionPrefix` |
| --- | --- | --- |
| Conoscope | `1.4.6.1` | `1.4.6.9` |
| Spectrum | `1.0` | `2.3.3.1` |
| SystemMonitor | `1.0.1` | `1.4.3.3` |
| EventVWR | `1.0` | `1.1.8.1` |
| WindowsServicePlugin | `1.0` | `1.4.3.17` |

每次發布至少同時記錄 manifest、DLL FileVersion、`.cvxp` 檔名和 CHANGELOG。

## 必留證據

| 證據 | 來源 |
| --- | --- |
| 外掛源碼清單 | `Get-ChildItem Plugins -Directory` |
| manifest | `Plugins/<Name>/manifest.json` |
| 專案版本 | `Plugins/<Name>/<Name>.csproj` 中的 `VersionPrefix` |
| 輸出 DLL | 現場 `Plugins/<Name>/<Name>.dll` 文件屬性 |
| `.cvxp` 包 | `Scripts\package_plugin.bat <Name> --no-upload` |
| README/CHANGELOG | 外掛根目錄與展開後的包 |
| 宿主共享 DLL | 主程式根目錄 `ColorVision.*.dll` |
| native/資料檔 | 展開 `.cvxp` 或現場外掛目錄 |
| 權限證據 | 管理員模式、註冊表、服務或 Dump 記錄 |
| 回退 | 上一版包和外掛目錄備份 |

## 核查命令

```powershell
$name = "Spectrum"
Get-Content "Plugins/$name/manifest.json"
Select-String "Plugins/$name/$name.csproj" -Pattern "TargetFramework|VersionPrefix|ProjectReference|PackageReference|CopyToOutputDirectory"
Scripts\package_plugin.bat Spectrum --no-upload
```

## 外掛專項證據

| 外掛 | 額外證據 |
| --- | --- |
| Conoscope | MVS SDK 或 `MvCameraControl.dll`、測試圖、關注點配置、CSV 匯出 |
| Spectrum | 光譜儀 native DLL、`Magiude.dat`、`WavaLength.dat`、CIE 圖、授權目錄、結果庫、`SpectrumStatus` 回傳 |
| SystemMonitor | 狀態列配置、CPU/RAM/磁碟/網路刷新、快取清理範圍 |
| EventVWR | 管理員模式、Windows Application Error、HKLM LocalDumps、Dump 路徑 |
| WindowsServicePlugin | 管理員模式、服務根目錄、服務狀態、MySQL/MQTT 配置、ZIP 包、配置同步日誌 |

