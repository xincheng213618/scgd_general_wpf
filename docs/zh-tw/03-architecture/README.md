# 架構設計

本章節只保留當前系統設計的主閱讀路徑。歷史性方案、拆分草案和一次性討論文件仍保留在目錄中，但不再作為預設入口。

## 主閱讀路徑

1. [系統架構概覽](./overview/system-overview.md)
2. [架構執行時](./overview/runtime.md)
3. [元件互動](./overview/component-interactions.md)
4. [FlowEngineLib 架構](./components/engine/flow-engine.md)
5. [Templates 架構設計](./components/templates/design.md)
6. [安全概覽](./security/overview.md)
7. [RBAC 模型](./security/rbac.md)

## 目錄說明

- `overview/` 關注系統級視角，例如啟動、執行時和元件關係。
- `components/engine/` 關注流程引擎與執行模型。
- `components/templates/` 關注模板系統的設計與現狀分析。
- `security/` 關注權限模型和安全邊界。

## 建議怎麼讀

- 第一次接觸系統時，按“系統概覽 → 執行時 → 元件互動”的順序閱讀。
- 需要修改流程或模板時，再進入 `components/` 下的專題頁。
- 需要介面和型別細節時，轉到 [API 參考](../04-api-reference/README.md)。

## 補充閱讀

- [Templates 模組分析](./components/templates/analysis.md)：適合在已經理解模板設計主線後，再回來看目錄演進、註冊邊界和現狀約束。

## 歷史資料說明

- 本目錄中以 `ColorVision.Engine-Refactoring-` 開頭的文件屬於歷史設計資料，用於追溯思路，不再視為當前預設方案。
- 若歷史方案與當前程式碼實現衝突，以程式碼和現行模組文件為準。