# 外掛開發總覽

本章節面向需要擴充套件 ColorVision 功能的開發者，優先給出當前仍然有效的外掛開發路徑。

## 外掛在倉庫中的位置

- 執行時外掛原始碼位於 `Plugins/`
- 外掛被主程式在執行時發現並載入
- 如果外掛帶 UI，通常需要啟用 WPF 並遵循主應用的介面約定

## 開發一個外掛的最短路徑

1. 先看 [擴充套件性概覽](../core-concepts/extensibility.md)
2. 再看 [外掛開發入門](./getting-started.md)
3. 需要理解執行階段時，再看 [外掛生命週期](./lifecycle.md)

## 當前推薦約定

- 目標框架保持與主倉庫一致的 Windows 桌面方向
- 需要介面時啟用 WPF
- 建置後將產物複製到主程式輸出目錄下的 `Plugins/<Name>/`
- 優先參考現有標準外掛的組織方式，而不是另起一套約定

## 建議參考的現有外掛

- [Conoscope 外掛](../../04-api-reference/plugins/standard-plugins/conoscope.md)
- [Spectrum 外掛](../../04-api-reference/plugins/standard-plugins/spectrum.md)
- [SystemMonitor 外掛](../../04-api-reference/plugins/standard-plugins/system-monitor.md)
- [Spectrum 外掛](../../04-api-reference/plugins/standard-plugins/spectrum.md)
- [SystemMonitor 外掛](../../04-api-reference/plugins/standard-plugins/system-monitor.md)

## 說明

- 本頁只提供入口，不展開過細的歷史設計細節。
- 如果某個外掛依賴專案級定製邏輯，應同時檢視 `Projects/` 下對應實現。
