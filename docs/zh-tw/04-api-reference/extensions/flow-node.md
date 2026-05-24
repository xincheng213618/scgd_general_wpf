# FlowEngineLib 節點擴充套件

本頁只描述當前倉庫裡真實可用的 Flow 節點擴充套件路徑，不再繼續維護基於示意 API 的舊版“開發指南”。

## 先看節點體系實際長什麼樣

從當前程式碼看，Flow 節點擴充套件主要圍繞這幾類基類展開：

- `CVCommonNode`：所有節點的共同基類，提供 `NodeName`、`NodeType`、`DeviceCode`、`NodeID`、`ZIndex` 以及 `nodeEvent` / `nodeRunEvent` / `nodeEndEvent` 等公共能力。
- `BaseStartNode`：流程開始節點，負責建立 `CVStartCFC`、維護執行中的 `startActions`，並在流程結束時丟擲 `Finished`。
- `CVBaseServerNode`：最常見的服務/演算法類節點基類，負責輸入輸出、MQTT 請求組裝、超時處理和節點級完成回傳。
- `CVEndNode`：流程結束節點，最終呼叫 `startAction.FireFinished()` 把整條流程標記為完成。

這意味著當前節點擴充套件並不是一套“實現介面即可”的輕量外掛模型，而是直接建立在 `STNode` 和一組具體基類之上。

## 當前最值得先看的程式碼錨點

如果你要新增或理解一個節點，優先看這些檔案：

- `Engine/FlowEngineLib/Base/CVCommonNode.cs`
- `Engine/FlowEngineLib/Base/CVBaseServerNode.cs`
- `Engine/FlowEngineLib/Start/BaseStartNode.cs`
- `Engine/FlowEngineLib/End/CVEndNode.cs`
- `Engine/FlowEngineLib/Algorithm/AlgorithmNode.cs`

其中 `AlgorithmNode` 是一個很典型的現例項子：它不是在節點內部直接算圖，而是收集模板、顏色、影像路徑等參數，再拼出真正發往服務端的請求資料。

## 服務節點當前通常怎麼擴充套件

從 `CVBaseServerNode` 的實現看，當前最常見的擴充套件方式是：

1. 繼承 `CVBaseServerNode`。
2. 在建構函式里確定標題、`NodeType`、服務名和裝置程式碼，並設定 `operatorCode` 等節點行為欄位。
3. 在 `OnCreate()` 裡新增輸入輸出或編輯控制元件。
4. 透過重寫 `getBaseEventData(CVStartCFC start)` 組裝真正發往執行端的參數物件。
5. 需要時重寫 `OnServerResponse(...)`、`Reset(...)` 或連線相關虛方法，補充響應處理和清理邏輯。

舊文件裡那種“重寫 `DoServerWork` 就完成節點開發”的說法，和當前 `CVBaseServerNode` 的真實實現並不一致。

## 一個更接近現狀的骨架

```csharp
using FlowEngineLib.Algorithm;
using FlowEngineLib.Base;
using FlowEngineLib.MQTT;
using ST.Library.UI.NodeEditor;

[STNode("/Custom/MyNode")]
public class MyNode : CVBaseServerNode
{
    public MyNode()
        : base("自訂節點", "Algorithm", "SVR.Custom", "DEV.Custom")
    {
        operatorCode = "CustomEvent";
    }

    protected override void OnCreate()
    {
        base.OnCreate();
        CreateTempControl(m_custom_item);
    }

    protected override object getBaseEventData(CVStartCFC start)
    {
        var param = new AlgorithmParam();
        BuildTemp(param);
        BuildImageParam(param);
        return param;
    }

    protected override void OnServerResponse(CVServerResponse resp, CVStartCFC startCFC)
    {
        base.OnServerResponse(resp, startCFC);
        // 按需處理返回資料
    }
}
```

這個骨架和當前程式碼更接近：節點的核心通常是“如何建置請求資料並接入現有執行鏈”，而不是自己在節點裡完成整段業務計算。

## 開始節點和結束節點分別控制什麼

### `BaseStartNode`

開始節點當前負責：

- 建立並儲存 `CVStartCFC`
- 透過 `m_op_start` 和多個 `m_op_loop` 把啟動動作分發出去
- 管理 `Ready`、`Running` 和進行中的 `startActions`
- 在流程真正結束後向外丟擲 `Finished`

因此如果你擴充套件的是流程入口節點，關注點不會是模板參數，而是啟動、迴圈輸出和流程狀態管理。

### `CVEndNode`

結束節點當前負責：

- 接收 `CVStartCFC` 或迴圈繼續動作
- 在 `DoNodeEnded(...)` 中呼叫 `startAction.DoFinishing()`
- 最終呼叫 `startAction.FireFinished()`

這也是當前程式碼裡“整條流程 finished”的真正出口。

## 當前幾個最容易寫錯的點

### `nodeEndEvent` 不等於流程完成

`CVCommonNode.nodeEndEvent` 只表示節點級別的結束反饋。整條流程完成要走到 `CVEndNode`，再由 `startAction.FireFinished()` 觸發。

### 不要圍繞不存在的 `DoServerWork` 設計新節點

當前 `CVBaseServerNode` 真正的擴充套件點更接近：

- `OnCreate()`
- `getBaseEventData(...)`
- `OnServerResponse(...)`
- `Reset(...)`

如果按舊文件去找 `DoServerWork`，會直接把擴充套件路徑理解錯。

### 節點和服務主題不是自動推斷萬能匹配

`CVBaseServerNode` 當前透過 `GetSendTopic()`、`GetRecvTopic()`、`operatorCode` 和 `FlowServiceManager` 配合訊息鏈。如果這些欄位和服務端約定不一致，節點會表現成超時或收不到響應。

### 分類路徑沒有單一固定規範

當前 `[STNode("...")]` 的路徑字串是實際樹結構的一部分，但倉庫內現有節點已經混用了 `/00 全域性`、`/03_2 Algorithm` 等風格。擴充套件時更應該遵循相鄰節點的現有分組，而不是照搬舊文件裡那套假定分類表。

## 推薦閱讀順序

1. `CVCommonNode`：先理解公共屬性、事件和控制元件輔助方法。
2. `CVBaseServerNode`：再看典型服務節點怎樣發起請求、等待響應和處理超時。
3. `BaseStartNode`：理解流程啟動、迴圈輸出和 `Finished` 事件來源。
4. `CVEndNode`：確認流程結束鏈在哪裡閉環。
5. `AlgorithmNode` 或其他相鄰真實節點：最後照著現有節點擴充套件，而不是從舊教程樣板出發。

## 繼續閱讀

- [FlowEngineLib 架構](../../03-architecture/components/engine/flow-engine.md)
- [Engine 元件總覽](../engine-components/README.md)
- [演算法系統概覽](../algorithms/overview.md)
