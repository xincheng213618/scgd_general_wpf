# Engine 開發指南

Engine 開發要先確認你改的是哪條業務鏈。不要把設備服務、模板、Flow 節點、演算法結果和客戶專案判定混在同一個地方改。

## 先讀

第一次接手 Engine 請先讀：

- [Engine 業務交接手冊](../../04-api-reference/engine-components/business-handoff.md)
- [Engine 元件與業務交接](../../04-api-reference/engine-components/README.md)
- [Engine 執行時物件目錄](../../04-api-reference/engine-components/runtime-object-map.md)

本目錄下的頁面用於補充具體開發主題。

## 常見修改點

| 修改目標 | 主要目錄 | 先看 |
| --- | --- | --- |
| 新增或維護設備服務 | `Engine/ColorVision.Engine/Services/Devices/` | [服務開發交接手冊](./services.md) |
| 新增或維護模板 | `Engine/ColorVision.Engine/Templates/` | [模板系統開發交接手冊](./templates.md) |
| 新增或維護 Flow 節點 | `Engine/ColorVision.Engine/Templates/Flow/`、`Engine/FlowEngineLib/` | [Engine 模板與 Flow 鏈路](../../04-api-reference/engine-components/template-flow-chain.md) |
| 修改 MQTT 行為 | `Engine/ColorVision.Engine/MQTT/`、設備服務目錄 | [MQTT 訊息處理交接手冊](./mqtt.md) |
| 修改 OpenCV/native | `Engine/cvColorVision/`、`UI/ColorVision.Core/`、`Engine/ColorVision.Engine/Media/` | [OpenCV 和 native 整合交接手冊](./opencv-integration.md) |
| 修改結果展示 | `Templates/*/ViewHandle*.cs`、`UI/ColorVision.ImageEditor/` | [Engine 結果展示與專案交接鏈路](../../04-api-reference/engine-components/result-handoff-chain.md) |
| 選擇驗證命令 | `Test/`、後端、腳本、文件站 | [測試與驗證交接手冊](../testing.md) |

## 開發驗證

至少驗證被修改模組、主程式和一條端到端場景：

```powershell
dotnet build Engine/ColorVision.Engine/ColorVision.Engine.csproj -c Release -p:Platform=x64
dotnet build ColorVision/ColorVision.csproj -c Release -p:Platform=x64
```

native/OpenCV 變更按 [OpenCV 和 native 整合交接手冊](./opencv-integration.md) 補充驗證。文件站變更執行 `npm run docs:build`。

## 維護原則

- 設備服務處理設備狀態、命令、設定和 UI，不承載客戶判定。
- 模板處理參數、編輯、持久化和演算法命令輸入，不承載最終 CSV/PDF/MES 格式。
- Flow 節點是視覺化執行單元，結果解讀由模板、結果和專案層處理。
- 專案包承載客戶 Process、Recipe、Fix、協議和 exporter 規則。
