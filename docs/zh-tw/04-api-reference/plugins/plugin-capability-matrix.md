# 外掛能力與交接矩陣

本頁按交接和現場排查視角橫向比較目前 `Plugins/` 下真實存在的外掛。

## 當前外掛總表

| 外掛 | 宿主入口 | 主要能力 | 外部邊界 | 主要風險 |
| --- | --- | --- | --- | --- |
| Conoscope | Tool 菜單 `VAM`、ImageEditor 右鍵 | 錐鏡/VAM 圖像、關注點、參考軸、預處理、色域和對比度分析 | MVS 相機、`MvCameraControl.dll`、CSV 匯出 | manifest 版本與程序集版本可能不同；相機環境影響大 |
| Spectrum | Tool 菜單光譜窗口、窗口級菜單/狀態列、Socket | 光譜儀連接、標定、測量、EQE、CIE、SQLite 結果 | native DLL、串口、SMU/Shutter/CFW、授權、Socket | 需要窗口、設備和標定狀態配合 |
| SystemMonitor | Tool 菜單、設定頁、主程式狀態列 | CPU/RAM/磁碟/網路/程序/GPU/快取監控 | Windows 效能計數器、CUDA、磁碟權限 | 性能計數器可能失敗並降級 |
| EventVWR | Help 菜單事件窗口、Dump 子菜單 | Application 錯誤事件、WER LocalDumps、當前程序 Dump | EventLog、HKLM 登錄檔、Dump 目錄 | 需要管理員權限 |
| WindowsServicePlugin | Help 菜單服務管理器、安裝向導 | CVWindowsService 安裝/註冊/啟停、MySQL/MQTT 配置同步 | Windows 服務、MySQL、MQTT、服務包 ZIP | 會修改本機服務和配置，需管理員權限 |

## 發版前必查

| 檢查項 | 說明 |
| --- | --- |
| manifest 版本 | 外掛管理器或市場展示可能使用 `manifest.version` |
| DLL 文件版本 | `.cvxp` 文件名通常來自主 DLL `FileVersion` |
| README / CHANGELOG | 需要與本次實際 DLL 對應 |
| native DLL | Spectrum、Conoscope 不能只檢查託管 DLL |
| 管理員權限 | EventVWR、WindowsServicePlugin 必須明確標註 |

## 單外掛繼續閱讀

- [Conoscope](./standard-plugins/conoscope.md)
- [Spectrum](./standard-plugins/spectrum.md)
- [SystemMonitor](./standard-plugins/system-monitor.md)
- [EventVWR](./standard-plugins/eventvwr.md)
- [WindowsServicePlugin](./standard-plugins/windows-service.md)
