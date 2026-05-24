# Copilot Context Integration v1

本文件記錄 ColorVision Copilot 從“獨立聊天面板”演進為“軟體上下文服務”的第一版設計邊界、已落地項與後續規劃。

## 目標

當前 Copilot 已經具備會話、模型配置、附件、工具呼叫、日誌診斷與右側 Dock 面板，但它對業務物件的理解仍主要停留在檔案路徑和使用者手動掛載的上下文上。

下一階段的目標不是繼續單點增強聊天能力，而是把 Copilot 變成可被各模組呼叫的公共平台能力：

- 業務模組不直接依賴 ColorVision/Copilot 目錄實現。
- Copilot 可以自動攜帶當前軟體場景的結構化上下文。
- Copilot 的入口從狀態列擴充套件到解決方案樹、影像、流程、演算法結果和異常等工作流節點。

## 本次已落地的簡單版本

這次先完成了低風險、可複用的基礎層：

1. 公共契約層

- 在 UI/ColorVision.Common/Interfaces/Copilot 下新增 `ICopilotService`、`ICopilotContextProvider` 以及最小請求/上下文模型。
- `CopilotPanelService` 現在作為 `ICopilotService` 的實現，並透過 `CopilotServiceRegistry` 暴露給其它模組。

2. 最小上下文 Provider

- 新增 `CopilotContextRegistry` 和 `CopilotWorkspaceContextProvider`。
- 當前 Agent 請求會自動掛入“當前工作區”上下文，包括：解決方案根目錄、當前活動內容、當前搜尋根。
- 這一步不直接理解業務物件，但先把“軟體當前工作面”變成了公共可擴充套件的上下文入口。

3. 解決方案樹入口

- `FileNode` 增加“問 AI 解釋此檔案”和“問 AI 診斷此檔案/日誌”。
- `FolderNode` 增加“問 AI 總結此資料夾”。
- 這些入口透過公共 `ICopilotService` 觸發，而不是直接引用 Copilot 具體實現。

4. 品牌統一

- 狀態列入口名稱從 `GitHub Copilot` 調整為 `ColorVision Copilot`。

## 為什麼先停在這裡

這版刻意不直接進入 Engine、ImageEditor、Flow 或裝置服務層，原因是：

- 這些模組一旦直接引用 ColorVision/Copilot，會很快形成反向耦合。
- 當前最缺的不是“更多聊天功能”，而是“可被業務模組穩定呼叫的上下文介面”。
- 先把公共契約、最小上下文采集和高頻入口跑通，後面再接業務物件時成本更低。

## 後續規劃

### Phase 1：上下文橋擴充套件

優先新增這些 Provider：

- 當前視窗/當前頁面 Provider
- 當前選中物件 Provider
- 當前影像與 ROI Provider
- 當前日誌與反饋包 Provider
- 當前流程/節點/最近失敗 Provider

這一階段的驗收標準是：

- 使用者從任意關鍵頁面發起 Copilot 時，不需要手動重複說明當前物件。
- Agent 能看到結構化的應用上下文，而不是隻有檔案路徑。

### Phase 2：場景入口

優先從這幾類入口接入：

- 影像頁右鍵：分析當前影像、解釋當前 ROI
- 演算法結果頁：解釋此結果、為什麼失敗
- Flow：解釋當前流程、診斷上次失敗
- 裝置面板：分析當前裝置狀態
- 異常對話方塊：交給 Copilot 分析

這一階段要堅持“呼叫公共介面”，不要讓這些模組直接依賴 Copilot 具體視窗或 ViewModel。

### Phase 3：導航型動作

在解釋/診斷穩定後，再引入低風險 UI 動作：

- 開啟某個面板
- 聚焦某個視窗
- 定位某個配置項
- 開啟日誌目錄

這一階段只做導航和定位，不做改配置、控裝置、跑流程這類高風險動作。

### Phase 4：業務工具

在現有 SearchFiles、GrepText、ReadLocalFile、GetRecentLog 基礎上，補充只讀業務工具：

- `GetActiveWorkspaceContextTool`
- `GetSelectedImageContextTool`
- `GetAlgorithmResultContextTool`
- `GetFlowRunSummaryTool`
- `GetDeviceStatusTool`
- `GetDiagnosticsPackageSummaryTool`

## 與 Agent/ReAct 路線的關係

Copilot Context Integration v1 解決的是“它是否理解當前軟體場景”，而不是“它是否更像一個通用程式碼代理”。

因此後續路線應分成兩條並行線：

- Context Integration：解決軟體內上下文與入口。
- Agent/ReAct：解決工具規劃、低成本檢索、結構化執行與更多診斷工具。

兩條路線都重要，但當前優先順序應是 Context Integration。

## 當前邊界

截至本版本：

- 已有公共 Copilot 契約層。
- 已有最小工作區上下文 provider。
- 已有解決方案樹高頻入口。
- 仍未接入 ImageEditor、Flow、Algorithm、Device 的業務物件上下文。
- 仍未實現 UI 動作卡片、業務工具和全域性設定頁嵌入。

後續設計與排期建議以本文件為基線推進。