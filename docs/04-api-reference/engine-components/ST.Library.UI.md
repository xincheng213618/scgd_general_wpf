# ST.Library.UI

本页只描述当前仓库里真实可用的 `ST.Library.UI` 模块，不再继续维护“完整 UI 平台手册 + 海量示例 + 统一扩展框架”式旧稿。

## 先看这个模块现在是什么

按当前源码状态，`ST.Library.UI` 是一套偏底层的 WinForms 节点编辑器库。它当前最明确的角色不是独立应用壳，而是为 Flow 相关功能提供：

- 节点画布与交互编辑器
- 节点基类与端口连接模型
- 属性编辑面板
- 节点树与节点面板组合控件

因此它更接近“节点编辑器基础设施”，而不是 ColorVision 业务层本身。

## 当前最关键的文件

- `Engine/ST.Library.UI/NodeEditor/STNodeEditor.cs`
- `Engine/ST.Library.UI/NodeEditor/STNode.cs`
- `Engine/ST.Library.UI/NodeEditor/STNodeOption.cs`
- `Engine/ST.Library.UI/NodeEditor/STNodePropertyGrid.cs`
- `Engine/ST.Library.UI/NodeEditor/STNodeTreeView.cs`
- `Engine/ST.Library.UI/NodeEditor/STNodeEditorPannel.cs`
- `Engine/ST.Library.UI/FrmSTNodePropertyInput.cs`

如果只是想弄清这个库在当前仓库里真正做什么，这几处文件已经覆盖主体。

## 当前控制面怎么分块

### 画布控件

`STNodeEditor` 是整个库的中心控件。按当前实现看，它负责：

- 维护 `Nodes`
- 维护画布偏移与缩放
- 管理节点选中、悬停、活动态
- 处理节点连线、断线与画布交互
- 触发节点和画布相关事件

这说明当前节点编辑器的控制逻辑都集中在一个 WinForms `Control` 里，而不是拆成一堆独立 MVVM 服务。

### 节点对象模型

`STNode` 是当前所有节点的共同基类，负责：

- 标题、尺寸、位置
- 输入输出选项集合
- 节点内嵌控件集合
- 选中态与活动态
- 自动尺寸与重绘

而 `STNodeOption` 则承担端口模型，当前提供：

- 端口文本与数据类型
- 单连接/多连接限制
- 连接数量和已连端口集合
- 连接、断开和数据传递事件

因此这个库的基础心智模型并不是“节点只是一张图”，而是“节点 + 端口 + 控件 + 事件”的组合对象。

### 属性面板

`STNodePropertyGrid` 当前是一个专门为节点属性设计的控件，不是直接复用 .NET 标准 PropertyGrid。它会围绕当前 `STNode`：

- 读取属性描述符
- 渲染项、描述和错误区
- 根据节点标题色或自定义颜色做高亮
- 处理只读与编辑态切换

`FrmSTNodePropertyInput` 则是与之配套的轻量输入窗体，用来编辑单个属性值。

### 节点树与组合面板

`STNodeTreeView` 当前负责：

- 组织节点类型树
- 维护搜索与分组显示
- 和编辑器、属性面板联动

`STNodeEditorPannel` 则把：

- `STNodeEditor`
- `STNodeTreeView`
- `STNodePropertyGrid`

组合成一个可直接使用的整体面板，并补上分割线、缩放提示和连接状态提示。

这说明 `ST.Library.UI` 当前并不只是单个编辑器控件，还提供了比较完整的一套组合宿主面板。

## 当前和 ColorVision 的关系

在这个仓库里，`ST.Library.UI` 更多被 `FlowEngineLib` 及其宿主层当成基础设施使用。当前业务层通常会：

- 继承 `STNode` 做自己的节点类型
- 把 `STNodeEditor` 作为流程画布
- 借 `STNodePropertyGrid` 暴露节点属性
- 借 `STNodeTreeView` 管理节点分类与拖放创建

所以文档不应该把它写成和业务同一层的“流程系统”，它是流程系统下面的 UI 基础库。

## 当前几个最容易写错的点

### 它是 WinForms 库，不是 WPF 流程框架

虽然上层主程序大量使用 WPF，但 `ST.Library.UI` 当前核心控件仍然是 WinForms `Control`。这个边界对理解宿主嵌入方式很重要。

### 这套库不只提供一个编辑器控件

除了 `STNodeEditor`，当前还有节点对象模型、端口模型、属性网格、节点树和组合面板。把它缩写成“一个画布控件”会低估实际范围。

### 属性编辑是自定义实现，不是直接用系统 PropertyGrid

`STNodePropertyGrid` 和 `FrmSTNodePropertyInput` 是库内自己的节点属性编辑链。继续照旧文档把它描述成通用反射面板，会模糊掉当前专用实现。

### 它主要被上层节点系统消费

当前真实用法是上层定义节点类型后交给这里的编辑器、树和属性面板承载，而不是在 `ST.Library.UI` 里直接写业务节点逻辑。

## 推荐阅读顺序

1. `Engine/ST.Library.UI/NodeEditor/STNodeEditor.cs`
2. `Engine/ST.Library.UI/NodeEditor/STNode.cs`
3. `Engine/ST.Library.UI/NodeEditor/STNodeOption.cs`
4. `Engine/ST.Library.UI/NodeEditor/STNodePropertyGrid.cs`
5. `Engine/ST.Library.UI/NodeEditor/STNodeTreeView.cs`
6. `Engine/ST.Library.UI/NodeEditor/STNodeEditorPannel.cs`

这样能先建立画布和节点模型，再理解属性面板与节点库是怎样挂上来的。

## 继续阅读

- [docs/04-api-reference/engine-components/FlowEngineLib.md](./FlowEngineLib.md)
- [docs/04-api-reference/extensions/flow-node.md](../extensions/flow-node.md)
- [docs/03-architecture/components/engine/flow-engine.md](../../03-architecture/components/engine/flow-engine.md)