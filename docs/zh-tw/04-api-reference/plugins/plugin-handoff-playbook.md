# 外掛執行與交接場景手冊

本頁用於接手現有外掛、排查外掛不載入、打包 `.cvxp`、驗證現場外掛包。

## 場景入口

| 問題 | 先看 | 相關外掛 |
| --- | --- | --- |
| 外掛目錄存在但菜單看不到 | 場景 A | 全部外掛 |
| 外掛載入但入口沒出現 | 場景 B | 全部外掛 |
| 缺 `ColorVision.*.dll` 或版本不足 | 場景 C | 全部外掛 |
| 要發布 `.cvxp` | 場景 D | 全部外掛 |
| 設備或 native DLL 相關 | 場景 E | Spectrum、Conoscope |
| 需要管理員權限 | 場景 F | EventVWR、WindowsServicePlugin |
| Socket 指令不通 | 場景 G | Spectrum |
| 歷史名稱要恢復為當前外掛 | 場景 H | Pattern、ImageProjector、ScreenRecorder |

## 場景 A：目錄存在但沒有載入

1. 確認目錄在主程式輸出 `Plugins/<PluginName>/`。
2. 檢查 `manifest.json` 的 `Id`、`version`、`dllpath`。
3. 確認 `dllpath` 指向的 DLL 存在。
4. 若有 `.deps.json`，核對主程式根目錄的 `ColorVision.*.dll` 版本。
5. 查看日誌中的 `PluginDllNotFound`、`DependencyVersionInsufficient`、`PluginLoadError`。

## 場景 B：載入但入口沒有出現

| 外掛 | 入口檢查 |
| --- | --- |
| Conoscope | Tool -> `VAM`、`ConoscopeWindow` Ribbon、ImageEditor 右鍵 |
| Spectrum | Tool 光譜窗口、窗口級菜單、狀態列、Socket handler |
| SystemMonitor | Tool 菜單、設定頁、主程式狀態列 |
| EventVWR | Help 事件窗口、Dump 子菜單 |
| WindowsServicePlugin | Help 服務管理器、安裝向導 |

## 場景 C：依賴版本或 DLL 問題

不要只替換外掛目錄。若外掛由新版 UI DLL 編譯，現場主程式根目錄的 `ColorVision.*.dll` 也必須同步相容。

## 場景 D：打包 `.cvxp`

```powershell
Scripts\package_plugin.bat Spectrum --no-upload
```

打包後展開 `.cvxp`，檢查 DLL、manifest、README、CHANGELOG、PackageIcon、native DLL 和必要資料文件。

## 場景 E：設備或 native DLL

Spectrum 先查光譜儀 DLL、串口、授權、標定組、SQLite 結果庫；Conoscope 先查 MVS SDK、相機驅動、`MvCameraControl.dll`、圖像與關注點配置。

## 場景 F：管理員權限

EventVWR 和 WindowsServicePlugin 涉及登錄檔、Dump、Windows 服務、本機資料夾和配置同步。普通使用者下的失敗不能直接判定為功能缺陷。

## 場景 G：Socket 指令不通

Spectrum 的 Socket JSON 指令依賴 `ColorVision.SocketProtocol`、JSON 模式、端口配置、Spectrum 程序集載入和窗口/設備狀態。

## 場景 H：恢復歷史名稱

Pattern、ImageProjector、ScreenRecorder 目前不在當前外掛清單中。恢復前必須補齊原始碼、工程、manifest、README、CHANGELOG、構建複製、打包驗證、[當前外掛文件覆蓋清單](./current-plugin-coverage.md)、矩陣和導航。
