# 算法系统概览

本页只描述当前仓库里实际在跑的模板与算法接入链，不维护“算法分类百科 + 示例代码 + GPU 能力总论”式旧稿。

## 代码落点

| 位置 | 作用 |
| --- | --- |
| `Engine/ColorVision.Engine/Templates/` | 模板定义、模板管理、模板编辑和大部分业务算法 UI 接入点 |
| `Engine/FlowEngineLib/` | 流程节点、开始/结束链和执行控制 |
| `Engine/ColorVision.Engine/Services/Devices/Algorithm/` | 算法设备服务接入面 |
| `Engine/cvColorVision/` 和原生库 | 部分底层计算与互操作 |

如果只把这章理解成“托管算法函数目录”，会偏离当前实现。

## 主链路

1. `TemplateContorl` 扫描已加载程序集中的 `IITemplateLoad`，并把模板注册进系统。
2. `TemplateManagerWindow` 和 `TemplateEditorWindow` 让用户浏览、创建、编辑模板。
3. 业务算法 UI 通常继承 `DisplayAlgorithmBase`，并暴露 `OpenTemplateCommand` 一类入口。
4. `SendCommand(...)` 组装 `CVTemplateParam`、文件路径、设备信息等参数。
5. 参数通过 `MQTTAlgorithm` 或相邻服务链发给真正执行端。
6. 流程模板进入 `TemplateFlow` + `FlowEngineToolWindow` + `FlowEngineLib` 链路。

很多 `Templates/*/Algorithm*.cs` 当前更像“算法前端适配器”，不是最终算子本身。

## 关键模块

| 模块 | 关键入口 | 关注点 |
| --- | --- | --- |
| 模板注册与管理 | `ITemplate.cs`、`TemplateContorl.cs`、`TemplateManagerWindow`、`TemplateEditorWindow` | 模板怎么出现、打开和编辑 |
| Flow 模板 | `TemplateFlow.cs`、`FlowEngineToolWindow`、`DisplayFlow` | 流程图、流程编辑、导入导出和批次执行 |
| JSON 模板 | `ITemplateJson<T>`、`TemplateJsonParam`、`EditTemplateJson` | JSON 装载、保存、导入导出、文本/属性编辑 |
| 业务模板族 | `POI/`、`ARVR/`、`JND/`、`LedCheck/`、`Compliance/`、`Jsons/` | 不同历史阶段的业务算法接入 |

## 易误读点

| 误区 | 当前事实 |
| --- | --- |
| `Algorithm*.cs` 就是最终算法实现 | 多数负责模板窗口、UI 选择状态、消息参数和 `PublishAsyncClient(...)` |
| `POI` 只是独立小专题 | 它是多个 ARVR/定位/分析算法共享的上游模板依赖 |
| Flow 模板不属于模板系统 | Flow 仍通过 Templates 系统进入主程序，再由流程窗口和流程库接管 |
| JSON 模板只是临时兼容层 | `Jsons/` 和 `ITemplateJson<T>` 仍是实际在用的主路径之一 |

## 推荐阅读顺序

| 顺序 | 入口 |
| --- | --- |
| 1 | `Engine/ColorVision.Engine/Templates/TemplateContorl.cs` |
| 2 | `TemplateManagerWindow.xaml.cs`、`TemplateEditorWindow.xaml.cs` |
| 3 | `Templates/Flow/TemplateFlow.cs` |
| 4 | `Templates/Jsons/ITemplateJson.cs`、`EditTemplateJson.xaml.cs` |
| 5 | 具体业务目录，如 `POI/`、`ARVR/`、`Jsons/` 下各 `Algorithm*.cs` |
