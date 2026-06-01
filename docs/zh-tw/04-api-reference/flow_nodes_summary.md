# Flow Engine 結點文件彙總

## 文件說明

本目錄包含以下自動生成的結點文件：

1. **`flow_nodes_reference.md`** (已配置結點參考)
   - 基於 `NodeConfigurator` 目錄中的配置器檔案
   - 包含 **42 個已配置結點** 的詳細屬性
   - 每個結點包含：
     - 配置面板屬性（來自 NodeConfigurator）
     - 類級別屬性（來自 FlowEngineLib 實現）
     - 基類、實現檔案等資訊

2. **`flow_nodes_complete.md`** (完整結點清單)
   - 基於 `FlowEngineLib` 目錄中所有結點類定義
   - 包含 **90 個結點類** 的完整清單
   - 按型別分類統計
   - 每個結點包含基類、實現檔案、屬性數量

## 統計概覽

### 已配置結點 (42 個)

| 型別 | 數量 |
|------|------|
| Algorithm | 17 |
| Camera | 8 |
| POI | 5 |
| OLED | 2 |
| SMU | 3 |
| Sensor | 2 |
| Spectrum | 3 |
| PG | 1 |
| FW | 1 |

### 所有結點類 (90 個)

| 型別 | 數量 |
|------|------|
| Algorithm | 25 |
| Camera | 14 |
| POI | 8 |
| OLED | 7 |
| SMU | 7 |
| MQTT | 5 |
| Sensor | 3 |
| Start | 3 |
| Other | 8 |
| Loop | 2 |
| End | 2 |
| Spectrum | 2 |
| FW | 1 |
| Manual | 1 |
| Device | 1 |
| PG | 1 |

## 使用建議

- **開發人員**：參考 `flow_nodes_complete.md` 瞭解所有可用的結點類及其屬性
- **配置人員**：參考 `flow_nodes_reference.md` 瞭解已配置結點的面板屬性和配置方式
- **維護人員**：兩個文件結合使用，瞭解結點配置與實現的對應關係

## 資料來源

- **結點配置**：`Engine\ColorVision.Engine\Templates\Flow\NodeConfigurator\`
- **結點實現**：`Engine\FlowEngineLib\`

## 更新說明

文件基於原始碼自動生成，當結點配置或實現發生變化時，需要重新執行生成指令碼。

生成時間：2026-05-22