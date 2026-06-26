# FlowEngineLib 架构

Flow 能力跨两层：`Engine/FlowEngineLib/` 提供节点执行控制和基础节点，`Engine/ColorVision.Engine/Templates/Flow/` 把流程模板、编辑窗口、运行窗口和批次处理接入主程序。

## 先查什么

| 现象 | 第一检查点 |
| --- | --- |
| 流程无法启动 | 是否存在 `BaseStartNode` / `MQTTStartNode`，`FlowEngineControl.GetStartNodeName()` |
| 节点执行完但流程没结束 | `nodeEndEvent` 只是节点级事件，真正完成要看 `CVEndNode` -> `FireFinished()` |
| Flow 打开但无节点 | `Load(...)` / `LoadFromBase64(...)` 数据、节点 DLL 是否加载 |
| 服务节点不可用 | `FlowEngineControl.services`、RC service token、Engine 层节点刷新 |
| 调度执行卡住 | `DisplayFlow.RunFlowAndWaitAsync()`、UI Dispatcher、`FlowCompleted` |
| 完成后项目没处理 | `FlowControl.FinishedAsync`、`DisplayFlow.FlowControl_FlowCompleted`、项目包 `Processing` |

## 核心对象

| 对象 | 作用 |
| --- | --- |
| `FlowEngineControl` | 绑定 `STNodeEditor`，识别开始节点/服务节点，加载画布，启动/停止流程，转发 `Finished` |
| `BaseStartNode` | 创建 `CVStartCFC`，向下游派发动作，并在结束时触发 `Finished` |
| `CVBaseServerNode` | 设备/算法服务节点基类，处理运行、超时、失败、返回数据和下游传递 |
| `CVEndNode` | 结束节点，调用 `startAction.FireFinished()` 标记整条流程完成 |
| `TemplateFlow` | 让流程图以模板形式存储、导入、导出和编辑 |
| `FlowEngineToolWindow` | 独立流程编辑窗口，承载节点画布和编辑命令 |
| `DisplayFlow` / `FlowControl` | 主程序运行面，负责运行、批次、日志、完成回调和后处理 |

## 执行完成链

1. `FlowEngineControl` 绑定编辑器并收集开始节点、服务节点。
2. `LoadFromBase64(...)` 或 `Load(...)` 载入流程图。
3. `StartNode(...)` 选择指定开始节点，或默认取第一个开始节点。
4. `BaseStartNode` 创建 `CVStartCFC` 并派发到下游。
5. `CVBaseServerNode` 派生节点处理自己的运行、超时、失败和数据传递。
6. `CVEndNode` 调用 `startAction.FireFinished()`。
7. `BaseStartNode.Finished` 被触发。
8. `FlowEngineControl.Start_Finished(...)` 再抛出 `FlowEngineControl.Finished`。
9. Engine 层 `FlowControl.FinishedAsync` 转成 `FlowCompleted`。

关键点：`nodeEndEvent` 不是流程完成事件，只是节点级反馈。

## Engine 宿主链

| 层 | 当前职责 |
| --- | --- |
| `TemplateFlow` | 模板列表、双击打开编辑器、`.stn` / `.cvflow` 导入导出、关联模板处理 |
| `FlowEngineToolWindow` | 加载 `FlowEngineLib.dll`，提供撤销/重做/复制/粘贴/缩放/自动对齐 |
| `STNodeEditorHelper` | 连接属性面板、节点树、节点配置器、合法性检查 |
| `DisplayFlow` | 刷新流程模板、启动前预处理、运行日志、批次信息、完成后处理 |

## 扩展落点

| 要扩展什么 | 通常改哪里 |
| --- | --- |
| 新流程节点 | `FlowEngineLib` 节点实现及其开始/结束/服务链行为 |
| 新模板型流程 | `TemplateFlow` 相邻的模板管理、导入导出和编辑窗口接入 |
| 节点属性 UI | `STNodeEditorHelper` 或 `NodeConfigurator/` |
| 运行后项目处理 | `DisplayFlow` 完成链和项目包 `Processing` |

## 边界

- FlowEngineLib 不是独立完成全部 UI 的框架；主程序编辑和运行要经过 Engine 宿主层。
- 开始节点不是任意节点；没有开始节点时流程不能正常启动。
- 失败传播要看节点类型，控制器不会统一兜底推断所有失败。
- MQTT、日志、批次和后处理不是库本身的纯基础设施承诺，而是在 Engine 层组合起来。

## 关键文件

| 任务 | 先看 |
| --- | --- |
| 执行控制 | `Engine/FlowEngineLib/FlowEngineControl.cs` |
| 开始/结束 | `Start/BaseStartNode.cs`、`End/CVEndNode.cs` |
| 服务节点 | `Base/CVBaseServerNode.cs` |
| 模板宿主 | `Engine/ColorVision.Engine/Templates/Flow/TemplateFlow.cs` |
| 编辑窗口 | `FlowEngineToolWindow.xaml.cs`、`STNodeEditorHelper.cs` |
| 运行窗口 | `DisplayFlow.xaml.cs`、`FlowControl.cs` |
