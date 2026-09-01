---
knowledge_id: "flow.editor"
knowledge_type: "reference"
status: "current"
summary: "说明 ST WPF 节点画布、端口、类型目录及 STN 兼容边界。"
aliases: ["Flow画布加载后节点丢失","ST.Library.UI","STNodeEditor","STNodeTypeRegistry","CVNodeContainer"]
code_paths: ["Engine/ST.Library.UI/README.md","Engine/ST.Library.UI/ST.Library.UI.csproj","Engine/ST.Library.UI/NodeEditor/STNodeEditor.cs","Engine/ST.Library.UI/NodeEditor/STNodeTreeView.cs","Engine/ST.Library.UI/NodeContainer/CVNodeContainer.cs"]
test_paths: ["Test/ColorVision.UI.Tests/STNodeEditorWpfTests.cs","Test/ColorVision.UI.Tests/STNodeEditorCanvasTests.cs","Test/ColorVision.UI.Tests/STNodeTypeRegistryConcurrencyTests.cs"]
related: ["flow.architecture","flow.runtime","flow.workspace"]
---

# ST.Library.UI

`Engine/ST.Library.UI/` 是 Flow 功能使用的 WPF 节点编辑器库，为流程画布、
节点、端口、节点目录和属性描述提供基础能力。它不是 ColorVision 业务层，
业务节点仍由 `FlowEngineLib` 和项目/插件程序集实现。

## 先查什么

| 现象 | 第一检查点 |
| --- | --- |
| 画布打开后节点丢失 | `STNodeTypeRegistry` 是否加载对应程序集，`CVNodeContainer` 是否报告缺失类型 |
| 连线没有恢复 | 保存数据中的端口 key、`LoadCanvas(...)` 连线恢复顺序 |
| 自定义节点外观不正确 | 节点的 `OnDrawNode(...)` 和编辑器的 GDI 到 WPF 位图呈现 |
| WPF 窗口内鼠标/键盘异常 | `STNodeEditor` 的 WPF 输入、焦点捕获和快捷键处理 |
| 拖动/缩放卡顿 | `STNodeEditor` 重绘位图、视口裁剪、节点和连线数量 |

## 当前能力

| 能力 | 当前入口 | 说明 |
| --- | --- | --- |
| WPF 画布控件 | `STNodeEditor` | 直接继承 WPF `Control`，不需要 `WindowsFormsHost` |
| 节点模型 | `STNode` | 标题、尺寸、位置、输入输出端口、选中态和自定义绘制 |
| 端口模型 | `STNodeOption` | 端口文本、数据类型、连接限制、连接集合和数据传递事件 |
| WPF 属性面板 | `STNodePropertyGrid`、`STNodePropertyDescriptor` | 内联编辑节点属性，保留自定义描述符入口和错误状态 |
| WPF 节点目录 | `STNodeTreeView` | 节点类型发现、程序集加载、搜索、拖放、预览和过时类型过滤 |
| WPF 组合面板 | `STNodeEditorPannel` | 用 `GridSplitter` 组合目录、画布和属性面板，保留历史类型名 |
| 画布加载 | `CVNodeContainer.LoadCanvas(...)` | 从文件、`byte[]`、`Stream` 恢复节点、属性、位置和连线 |

## WPF 呈现与兼容

`STNodeEditor` 的输入、焦点、拖放、调度和宿主生命周期都是 WPF 实现。
已有节点仍使用 `System.Drawing` 的 `OnDrawNode(...)` 协议绘制，编辑器把结果
呈现到 WPF 位图，因此不需要重写现有业务节点，也不会改变 `.stn`、`.cvflow`
的序列化格式。

当前由 `FlowProcessing/Editor/FlowEditorCanvas.xaml` 声明 `<st:STNodeEditor />`，
`ViewFlow` 组合 Canvas，`FlowEngineToolWindow` 再承载 standalone `ViewFlow`。
主/独立窗口命令与文档目标由[工作区契约](../../01-user-guide/workflow/design.md)维护，
不是 ST 库职责。旧的 WinForms 编辑器、属性输入窗体、预览窗体和
`STNodeEditorHost` 已移除；`STNodeEditorPannel` 保留名称但已是 WPF 组合控件。

## 与 ColorVision 的关系

| 上层 | 如何使用它 |
| --- | --- |
| `FlowEngineLib` | 继承 `STNode` 实现业务节点，使用 `STNodeOption` 做端口和数据传递 |
| `ColorVision.Engine/FlowProcessing/Editor` | `FlowEditorCanvas` 承载 `STNodeEditor`，组合属性面板、右键菜单与编辑命令 |
| `ColorVision.Engine/Templates/Flow` | 保存/读取画布序列化内容及流程包，不拥有 WPF 编辑窗口 |
| 项目/插件节点 | 在上层节点程序集实现并注册，不应直接写进 `ST.Library.UI` |

## 检查

| 验收项 | 通过标准 |
| --- | --- |
| WPF 类型 | `STNodeEditor` 可直接加入 WPF 视觉树，程序集不引用 `System.Windows.Forms` |
| 节点类型加载 | 当前程序集和外部节点程序集能注册，过时类型不会出现在新建目录 |
| 画布加载 | 文件、`byte[]`、`Stream` 三类入口能恢复节点、位置、属性和连线 |
| 节点编辑 | 新增、移动、选中、删除、活动态切换和重绘正常 |
| 端口连接 | 连接限制、断开、事件顺序和数据传递正常 |
| 画布交互 | 空白左键拖动可按宿主模式平移或框选；Ctrl + 左键拖动和中键拖动可平移，指针中心缩放、无限画布和视口裁剪正常 |
| 自定义绘制 | 现有节点的 `OnDrawNode(...)` 外观保持不变 |

## 变更边界

| 变更类型 | 位置判断 |
| --- | --- |
| 节点画布、端口连接、节点目录、WPF 输入和呈现 | 应该在这里 |
| 业务节点执行逻辑 | 通常在 `FlowEngineLib` 或具体节点程序集 |
| WPF 外层页面布局、菜单和命令 | 通常在 Engine Flow 宿主窗口 |
| 节点配置保存格式 | 影响 `LoadCanvas(...)` 时改这里；仅业务字段时改上层节点 |
| 新增客户业务节点 | 在上层节点程序集实现并注册 |

## 关键文件

| 任务 | 先看 |
| --- | --- |
| WPF 画布 | `NodeEditor/STNodeEditor.cs` |
| WPF 输入适配 | `NodeEditor/STNodeInputEventArgs.cs` |
| 节点 | `NodeEditor/STNode.cs` |
| 端口 | `NodeEditor/STNodeOption.cs` |
| 属性描述 | `NodeEditor/STNodePropertyGrid.cs` |
| 节点目录 | `NodeEditor/STNodeTreeView.cs` |
| 画布加载 | `NodeContainer/CVNodeContainer.cs` |

## 验证入口与缺口

关联测试：`Test/ColorVision.UI.Tests/STNodeEditorWpfTests.cs`、`Test/ColorVision.UI.Tests/STNodeEditorCanvasTests.cs`、`Test/ColorVision.UI.Tests/STNodeTypeRegistryConcurrencyTests.cs`。

测试覆盖 WPF 控件、画布和类型注册契约；业务节点执行、真实外部程序集和旧流程语料需按变更补验。
