# 開發指南

本章節聚焦二次開發、擴充套件點和交付流程；類庫細節和模組設計請分別進入 API 參考與架構設計。

## 從這裡開始的常見場景

### 理解擴充套件機制

- [擴充套件性概覽](./core-concepts/extensibility.md)

### 修改 Engine 或模板相關功能

- [Engine 開發指南](./engine-development/README.md)
- [架構設計](../03-architecture/README.md)
- [Engine 元件 API](../04-api-reference/engine-components/README.md)

### 開發外掛

- [外掛開發總覽](./plugin-development/README.md)
- [外掛開發入門](./plugin-development/getting-started.md)
- [外掛生命週期](./plugin-development/lifecycle.md)

### 建置、部署與更新

- [部署概覽](./deployment/overview.md)
- [自動更新系統](./deployment/auto-update.md)
- [建置與釋出指令碼](./scripts/README.md)

### 後端與輔助系統

- [外掛市場後端](./backend/README.md)
- [效能最佳化概覽](./performance/overview.md)
- [Socket 通訊模組最佳化路線](./performance/socket-protocol-optimization-roadmap.md)

## 推薦閱讀路徑

1. 先看 [架構設計](../03-architecture/README.md)，確認模組邊界。
2. 再看 [擴充套件性概覽](./core-concepts/extensibility.md)，確認擴充套件點和外掛入口。
3. 進入自己的目標專題：外掛、Engine、部署或後端。
4. 需要類和介面細節時，轉到 [API 參考](../04-api-reference/README.md)。

## 章節邊界

- 本章節優先提供“怎麼進入程式碼”的路徑，而不是替代 API 手冊。
- Engine 子目錄中的部分細分專題仍在收斂中，因此預設入口改為總覽頁，避免把未維護的小頁繼續放在主導航。
- 與 AI/Agent 相關的試驗性材料保留在子目錄中，但不再作為預設閱讀路徑。

## 補充入口

- [專案結構總覽](../05-resources/project-structure/README.md)
- [線上倉庫](https://github.com/xincheng213618/scgd_general_wpf)
- [問題跟蹤](https://github.com/xincheng213618/scgd_general_wpf/issues)
