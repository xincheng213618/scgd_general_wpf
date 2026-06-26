# ST.Library.UI

`Engine/ST.Library.UI/` 是底层 WinForms 节点编辑器库，为 Flow 功能提供画布、节点、端口、属性面板、节点树和组合面板。它不是 ColorVision 业务层，也不是 WPF 流程框架。

## 先查什么

| 现象 | 第一检查点 |
| --- | --- |
| 画布打开后节点丢失 | `STNodeTypeRegistry` 是否加载对应程序集，`CVNodeContainer` 是否报告缺失类型 |
| 连线没有恢复 | 保存数据中的端口 key、`LoadCanvas(...)` 连线恢复顺序 |
| 属性改了但节点没变 | `STNodePropertyGrid` 属性描述符、只读标记、输入窗体回写 |
| WPF 窗口内鼠标/键盘异常 | WinFormsHost 嵌入、焦点转移、快捷键处理 |
| 拖动/缩放卡顿 | `STNodeEditor` 重绘、缓存图、节点数量、连线数量 |

## 当前能力

| 能力 | 当前入口 | 说明 |
| --- | --- | --- |
| 画布控件 | `STNodeEditor` | 节点集合、画布偏移/缩放、选中/悬停/活动态、连线/断线事件 |
| 节点模型 | `STNode` | 标题、尺寸、位置、输入输出端口、内嵌控件、选中态、重绘 |
| 端口模型 | `STNodeOption` | 端口文本、数据类型、连接限制、连接集合、数据传递事件 |
| 属性面板 | `STNodePropertyGrid` | 节点属性描述、编辑状态、描述/错误区、颜色高亮 |
| 输入窗体 | `FrmSTNodePropertyInput`、`FrmSTNodePropertySelect` | 单个属性值输入或选择 |
| 节点树 | `STNodeTreeView` | 节点类型树、搜索、分组、拖放创建 |
| 组合面板 | `STNodeEditorPannel` | 组合编辑器、节点树、属性面板、分割线和提示 |
| 画布加载 | `CVNodeContainer.LoadCanvas(...)` | 从文件、byte[]、Stream 恢复节点、属性、位置和连线 |

## 与 ColorVision 的关系

| 上层 | 如何使用它 |
| --- | --- |
| `FlowEngineLib` | 继承 `STNode` 实现业务节点，使用 `STNodeOption` 做端口和数据传递 |
| `ColorVision.Engine/Templates/Flow` | 把 `STNodeEditor` 嵌入 WPF 流程编辑窗口，接属性面板和节点树 |
| 项目/插件节点 | 在上层节点程序集实现并注册，不应直接写进 `ST.Library.UI` |

## 检查

| 验收项 | 通过标准 |
| --- | --- |
| 节点类型加载 | 当前程序集和外部节点程序集能注册，节点树能看到类型 |
| 画布加载 | 文件、byte[]、Stream 三类入口能恢复节点、位置、属性和连线 |
| 缺失类型 | 缺少节点类型或程序集时能明确抛出/提示 |
| 节点编辑 | 新增、移动、选中、删除、活动态切换和重绘正常 |
| 端口连接 | 连接限制、断开、事件顺序和数据传递正常 |
| 属性编辑 | 文本、枚举、布尔、只读属性按节点定义编辑或禁用 |
| 画布交互 | 拖动、缩放、缩放提示和连接状态提示正常 |
| WPF 嵌入 | 鼠标、键盘、焦点和缩放在宿主窗口内可用 |

## 变更边界

| 变更类型 | 位置判断 |
| --- | --- |
| 节点画布、端口连接、节点树、属性面板交互 | 应该在这里 |
| 业务节点执行逻辑 | 通常在 `FlowEngineLib` 或具体节点程序集 |
| WPF 外层页面布局 | 通常在 Engine Flow 宿主窗口 |
| 节点配置保存格式 | 影响 `LoadCanvas(...)` 时改这里；仅业务字段时改上层节点 |
| 新增客户业务节点 | 在上层节点程序集实现并注册 |

## 边界

- 核心控件是 WinForms `Control`，不是 WPF/MVVM 控件。
- 它不只是一块画布，还包含节点模型、端口模型、属性网格、节点树和组合面板。
- 属性编辑是自定义实现，不是直接复用系统 PropertyGrid。
- 业务节点逻辑应留在上层节点系统。

## 关键文件

| 任务 | 先看 |
| --- | --- |
| 画布 | `NodeEditor/STNodeEditor.cs` |
| 节点 | `NodeEditor/STNode.cs` |
| 端口 | `NodeEditor/STNodeOption.cs` |
| 属性面板 | `NodeEditor/STNodePropertyGrid.cs` |
| 节点树 | `NodeEditor/STNodeTreeView.cs` |
| 组合面板 | `NodeEditor/STNodeEditorPannel.cs` |
| 画布加载 | `NodeContainer/CVNodeContainer.cs` |
