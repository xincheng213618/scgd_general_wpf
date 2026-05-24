# 裝置服務概覽

本頁作為裝置章節入口，優先回答“有哪些裝置頁可看、通常怎麼配置、遇到問題先查哪裡”。

## 裝置服務是什麼

在 ColorVision 裡，裝置通常以“服務”的形式被管理。主程式會維護一個裝置服務列表，使用者在裝置視窗中檢視、配置、啟用和操作這些服務。

裝置相關實現主要位於：

- `Engine/ColorVision.Engine/Services/`
- `Engine/ColorVision.Engine/Services/Devices/`

當前程式碼目錄中可以看到的典型裝置分類包括：

- Camera
- Calibration
- Motor
- FileServer
- FlowDevice
- Sensor
- SMU
- Spectrum

## 本章節怎麼讀

### 通用入口

- [新增與配置裝置](./configuration.md)

### 具體裝置

- [相機服務](./camera.md)
- [相機管理](./camera-management.md)
- [相機參數配置](./camera-configuration.md)
- [校準服務](./calibration.md)
- [電機服務](./motor.md)
- [SMU 服務](./smu.md)
- [流程裝置服務](./flow-device.md)
- [檔案伺服器](./file-server.md)

## 常見使用順序

1. 先看 [新增與配置裝置](./configuration.md)，瞭解新增和儲存裝置的基本流程。
2. 再進入具體裝置頁，確認該裝置有哪些參數和操作。
3. 如果涉及相機，繼續看 [相機管理](./camera-management.md) 和 [相機參數配置](./camera-configuration.md)。
4. 如果需要讓裝置參與自動化流程，再看 [工作流程概覽](../workflow/README.md)。

## 使用時通常會遇到什麼

- 一個裝置服務可能繫結真實硬體，也可能只是某類通訊或檔案型服務。
- 裝置列表順序、啟用狀態和配置內容通常會影響後續視窗和流程裡的可見性。
- 某些裝置除了基礎配置，還會有獨立的物理裝置管理、標定或參數配置頁。

## 排查建議

### 裝置沒有出現在列表裡

- 先確認是否已經在裝置配置視窗中建立並儲存。
- 再確認對應裝置依賴是否已經滿足，例如物理硬體、驅動或通訊環境。

### 裝置出現了，但無法工作

- 優先檢查該裝置專題頁裡的參數說明。
- 再檢查日誌和連線狀態。
- 若是流程裡呼叫失敗，再聯動檢視 [流程執行與除錯](../workflow/execution.md)。

## 說明

- 本頁只做入口和使用路徑說明，不再承擔裝置服務程式碼分析。
- 裝置實現細節以 `Engine/ColorVision.Engine/Services/` 下的實際程式碼為準。

