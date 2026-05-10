# 流程引擎

本页只描述当前仓库里 `Engine/ColorVision.Engine/Templates/Flow` 这一层的真实职责，不再继续维护“把整个 Flow 执行内核、宿主桥接、节点库都混成一页”的旧稿。

## 先看这页现在讲什么

当前这页讲的不是 `FlowEngineLib` 执行内核本身，而是主程序里围绕流程模板的宿主层，重点包括：

- 流程模板怎样从数据库和资源表加载。
- 双击流程模板后怎样打开编辑窗口。
- 编辑窗口怎样托管 `STNodeEditor`、属性面板和节点树。
- 宿主层怎样把设备、模板和节点配置器挂到流程编辑器里。

如果要看节点执行语义和节点基类，请转到 [FlowEngineLib](../../engine-components/FlowEngineLib.md)。

## 当前最关键的文件

- `Engine/ColorVision.Engine/Templates/Flow/TemplateFlow.cs`
- `Engine/ColorVision.Engine/Templates/Flow/FlowEngineToolWindow.xaml.cs`
- `Engine/ColorVision.Engine/Templates/Flow/STNodeEditorHelper.cs`
- `Engine/ColorVision.Engine/Templates/Flow/NodeConfigurator/*.cs`

这几处代码共同决定了主程序里的流程模板是如何编辑、保存和配置的。

## 当前主链怎么跑

### 流程模板入口

`MenuTemplateFlow` 会打开 `TemplateEditorWindow(new TemplateFlow())`。`TemplateFlow` 本身是 `ITemplate<FlowParam>` 的一个具体实现，当前负责：

- 从 MySQL 读取流程模板主表
- 把节点图内容从 `SysResourceModel.Value` 取回成 Base64
- 将其包装进 `FlowParam`
- 管理保存、删除、导入、导出和新建

因此当前流程模板不是单纯的磁盘文件列表，而是“数据库主记录 + 资源表二进制内容”的组合。

### 双击后的编辑窗口

`TemplateFlow.PreviewMouseDoubleClick(...)` 会直接打开 `FlowEngineToolWindow`。这说明流程模板和很多普通模板不同：

- 列表窗口只是入口
- 真正的流程编辑发生在独立窗口里

窗口里再通过 `STNodeEditorHelper` 托管节点画布、属性面板、节点树、剪贴板和右键菜单。

### 编辑器辅助层

`STNodeEditorHelper` 当前负责的事情很多，远不止“帮忙调一下节点树”：

- 节点复制和粘贴的压缩序列化
- 当前选中节点与属性面板同步
- 节点树初始化和装配
- 右键菜单、删除、全选等命令
- 合法性检查和自动布局
- 设备与模板选择面板的宿主挂接

这意味着流程编辑窗口的大量交互逻辑都集中在这个 helper 里，而不是散落在每个节点控件中。

### 节点配置器桥接

`NodeConfigurator` 目录当前是主程序和节点库之间的重要桥接层。这里会把：

- 设备服务列表
- 本地图像路径输入
- 普通模板选择器
- JSON 模板选择器

装进节点属性面板。

例如 POI 相关配置器会把 `TemplatePoi`、`TemplatePoiFilterParam`、`TemplatePoiReviseParam`、`TemplatePoiOutputParam` 等模板接回流程节点。也就是说，节点在宿主里的可编辑体验，并不完全由 `FlowEngineLib` 决定。

## 当前存储与导出边界

### 主存储仍是数据库

`TemplateFlow.Load()` 和 `Save2DB(...)` 当前都围绕 MySQL 主表、明细表以及 `SysResourceModel` 展开。Base64 节点图内容会落到资源表，再通过明细记录关联回来。

### 导出不只是一种格式

当前流程模板导出至少有两种实际形式：

- `.stn`：节点图原始文件
- `.cvflow`：带关联模板信息的流程包

因此把流程模板简单写成“只是一张节点图文件”，会漏掉当前包导出的能力。

## 当前几个最容易写错的点

### 这页不是 FlowEngineLib 重复页

`FlowEngineLib` 负责节点执行与基类体系；本页这层负责主程序里的模板管理、窗口编辑和宿主桥接。两层都叫“流程引擎”，但边界不同。

### 流程模板不是纯磁盘资产

当前主路径仍然是数据库 + 资源表，不是扫描某个目录里的 `.stn` 文件。导入导出只是附加能力。

### 节点属性编辑大量依赖宿主代码

真正把设备下拉框、模板下拉框、JSON 模板下拉框挂进节点属性区的，是 `NodeConfigurator` 和 `STNodeEditorHelper` 这一层，而不只是节点类本身。

### 窗口行为和一般模板编辑器不同

普通模板多半在 `TemplateEditorWindow` 右侧编辑；流程模板当前走的是“列表窗口 + 独立流程编辑器窗口”的路径。继续沿用一般模板的叙述会误导读者。

## 推荐阅读顺序

1. `Engine/ColorVision.Engine/Templates/Flow/TemplateFlow.cs`
2. `Engine/ColorVision.Engine/Templates/Flow/FlowEngineToolWindow.xaml.cs`
3. `Engine/ColorVision.Engine/Templates/Flow/STNodeEditorHelper.cs`
4. `Engine/ColorVision.Engine/Templates/Flow/NodeConfigurator/POINodeConfigurators.cs`
5. `Engine/ColorVision.Engine/Templates/Flow/NodeConfigurator` 下其它配置器

## 继续阅读

- [FlowEngineLib](../../engine-components/FlowEngineLib.md)
- [Flow 节点扩展](../../extensions/flow-node.md)
- [ColorVision.Engine](../../engine-components/ColorVision.Engine.md)