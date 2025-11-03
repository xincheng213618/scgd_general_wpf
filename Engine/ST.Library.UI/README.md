# ST.Library.UI

> 专业的可视化节点编辑器UI库 - ColorVision 系统核心组件

[![.NET Version](https://img.shields.io/badge/.NET-8.0%20%7C%204.7.2-blue)](https://dotnet.microsoft.com/)
[![Version](https://img.shields.io/badge/version-1.2.0.2410-green)](https://github.com/xincheng213618/scgd_general_wpf)
[![License](https://img.shields.io/badge/license-Custom-orange)](../../docs/Software%20License%20Agreement.md)

## 📋 简介

ST.Library.UI 是一个功能强大的可视化节点编辑器UI库，为 ColorVision 系统提供完整的流程图编辑和可视化编程能力。该库基于 Windows Forms 构建，提供了专业级的节点编辑、连接管理、属性编辑等功能。

## ✨ 核心特性

### 🎨 可视化编辑
- **无限画布**: 支持缩放、平移的无限编辑空间
- **拖拽操作**: 直观的拖拽式节点连接
- **网格对齐**: 智能磁吸对齐和网格显示
- **框选多选**: 支持框选、多选节点操作

### 🔗 连接管理
- **类型安全**: 基于数据类型的连接验证
- **循环检测**: 自动检测和防止循环依赖
- **可视化反馈**: 连接状态的实时视觉反馈
- **单/多连接**: 灵活的单连接和多连接模式

### ⚙️ 属性编辑
- **反射式编辑**: 自动识别和编辑节点属性
- **多类型支持**: 支持多种数据类型的编辑器
- **实时更新**: 属性更改即时生效
- **描述提示**: 详细的属性说明和错误提示

### 📚 节点库管理
- **层次分类**: 树形结构的节点组织
- **动态加载**: 支持程序集动态加载
- **搜索功能**: 快速查找节点类型
- **拖放创建**: 从库中拖放创建节点

### 🌍 多语言支持
- **中英文**: 内置中文和英文支持
- **资源化**: 基于资源文件的国际化
- **动态切换**: 运行时语言切换

### 🔄 撤销/重做
- **完整历史**: 支持多步撤销重做
- **智能记录**: 自动记录操作历史
- **状态恢复**: 准确的状态恢复

## 🏗️ 架构概览

### 核心组件

```
ST.Library.UI/
├── NodeEditor/                    # 节点编辑器核心
│   ├── STNodeEditor              # 主编辑器控件
│   ├── STNode                    # 节点基类
│   ├── STNodeOption              # 节点选项/端口
│   ├── STNodeControl             # 节点控件
│   ├── STNodePropertyGrid        # 属性编辑器
│   ├── STNodeTreeView            # 节点库视图
│   ├── STNodeHub                 # 中继节点
│   └── ...                       # 辅助类
│
├── NodeContainer/                # 节点容器扩展
│   ├── CVNode                    # ColorVision节点基类
│   ├── CVNodeCollection          # 节点集合
│   └── CVNodeContainer           # 节点容器
│
└── Lang                          # 多语言支持
```

### 类层次结构

```
Control (System.Windows.Forms)
    ├── STNodeEditor              # 节点编辑器
    ├── STNodeTreeView            # 节点库视图  
    ├── STNodePropertyGrid        # 属性编辑器
    └── STNodeEditorPannel        # 编辑器面板

STNode (Abstract)                  # 节点基类
    ├── STNodeHub                 # 中继节点
    │   ├── STNodeInHub           # 输入中继
    │   └── STNodeOutHub          # 输出中继
    └── CVNode                    # ColorVision节点

STNodeOption                       # 节点选项
    └── STNodeHubOption           # 中继选项

STNodeControl                      # 节点控件基类
```

## 🚀 快速开始

### 安装

项目作为 ColorVision 系统的一部分，通过引用项目方式使用：

```xml
<ProjectReference Include="..\Engine\ST.Library.UI\ST.Library.UI.csproj" />
```

### 基础使用

#### 1. 创建节点编辑器

```csharp
using ST.Library.UI.NodeEditor;

// 创建编辑器实例
var editor = new STNodeEditor
{
    Dock = DockStyle.Fill,
    BackColor = Color.FromArgb(34, 34, 34),
    ShowGrid = true,
    ShowMagnet = true
};

// 添加到窗体
this.Controls.Add(editor);
```

#### 2. 创建自定义节点

```csharp
[STNode("数学/加法", "执行两个数的加法运算")]
public class AddNode : STNode
{
    [STNodeProperty("结果", "计算结果")]
    public double Result { get; set; }
    
    protected override void OnCreate()
    {
        base.OnCreate();
        
        // 设置节点属性
        Title = "加法";
        TitleColor = Color.FromArgb(200, Color.Blue);
        
        // 添加输入输出
        InputOptions.Add("输入1", typeof(double), false);
        InputOptions.Add("输入2", typeof(double), false);
        OutputOptions.Add("输出", typeof(double), false);
        
        // 监听数据变化
        InputOptions[0].DataTransfer += Calculate;
        InputOptions[1].DataTransfer += Calculate;
    }
    
    private void Calculate(object sender, STNodeOptionEventArgs e)
    {
        double val1 = Convert.ToDouble(InputOptions[0].Data ?? 0);
        double val2 = Convert.ToDouble(InputOptions[1].Data ?? 0);
        
        Result = val1 + val2;
        OutputOptions[0].Data = Result;
        OutputOptions[0].TransferData();
    }
}
```

#### 3. 添加节点到编辑器

```csharp
// 创建节点实例
var addNode = new AddNode
{
    Left = 100,
    Top = 100
};

// 添加到编辑器
editor.Nodes.Add(addNode);
```

#### 4. 连接节点

```csharp
// 获取两个节点
var node1 = editor.Nodes[0];
var node2 = editor.Nodes[1];

// 连接输出到输入
var status = node1.OutputOptions[0].ConnectTo(node2.InputOptions[0]);

if (status == ConnectionStatus.Connected)
{
    Console.WriteLine("连接成功！");
}
```

#### 5. 添加属性编辑器

```csharp
var propertyGrid = new STNodePropertyGrid
{
    Dock = DockStyle.Right,
    Width = 300,
    ShowTitle = true,
    AutoColor = true
};

this.Controls.Add(propertyGrid);

// 节点选中时更新属性编辑器
editor.NodeSelected += (s, e) =>
{
    propertyGrid.STNode = e.Node;
};
```

#### 6. 添加节点库

```csharp
var nodeTree = new STNodeTreeView
{
    Dock = DockStyle.Left,
    Width = 250
};

this.Controls.Add(nodeTree);

// 加载节点类型
nodeTree.AddNode(typeof(AddNode));

// 或加载整个程序集
nodeTree.LoadAssembly("CustomNodes.dll");
```

## 📦 主要类说明

### STNodeEditor - 节点编辑器

主编辑器控件，提供完整的编辑功能。

**关键属性**:
- `Nodes`: 节点集合
- `CanvasOffsetX/Y`: 画布偏移
- `CanvasScale`: 缩放比例
- `ShowGrid`: 显示网格
- `ShowMagnet`: 磁吸对齐

**关键方法**:
- `LoadCanvas(string)`: 加载画布
- `SaveCanvas(string)`: 保存画布
- `StartUndo()/EndUndo()`: 撤销管理
- `GetSelectedNode()`: 获取选中节点

### STNode - 节点基类

所有节点的基类，定义节点行为。

**关键属性**:
- `Title`: 节点标题
- `Left/Top/Width/Height`: 位置和大小
- `InputOptions`: 输入选项集合
- `OutputOptions`: 输出选项集合
- `Controls`: 控件集合

**生命周期方法**:
- `OnCreate()`: 节点创建
- `OnOwnerChanged()`: 所有者改变
- `OnDrawTitle/Body()`: 自定义绘制

### STNodeOption - 节点选项

表示节点的输入/输出端口。

**关键属性**:
- `DataType`: 数据类型
- `IsInput`: 是否为输入
- `IsSingle`: 是否单连接
- `ConnectionCount`: 连接数量

**关键方法**:
- `ConnectTo(STNodeOption)`: 连接到选项
- `DisConnectionAll()`: 断开所有连接
- `CanConnect(STNodeOption)`: 检查能否连接

### STNodePropertyGrid - 属性编辑器

属性编辑控件，支持反射编辑。

**关键属性**:
- `STNode`: 目标节点
- `ShowTitle`: 显示标题
- `AutoColor`: 自动着色
- `ReadOnlyModel`: 只读模式

## 🔧 高级功能

### 自定义绘制

```csharp
protected override void OnDrawBody(DrawingTools dt)
{
    base.OnDrawBody(dt);
    
    // 自定义绘制逻辑
    dt.Graphics.FillRectangle(
        new SolidBrush(Color.Blue),
        new Rectangle(Left, Top, Width, Height)
    );
}
```

### 嵌入控件

```csharp
protected override void OnCreate()
{
    base.OnCreate();
    
    var ctrl = new STNodeControl
    {
        Left = 10,
        Top = TitleHeight + 10,
        Width = Width - 20,
        Height = 30
    };
    
    Controls.Add(ctrl);
}
```

### 数据验证

```csharp
protected override ConnectionStatus OnCheckConnectable(STNodeOption optionIn, STNodeOption optionOut)
{
    // 自定义连接验证逻辑
    if (optionIn.DataType != optionOut.DataType)
        return ConnectionStatus.ErrorType;
        
    return base.OnCheckConnectable(optionIn, optionOut);
}
```

### 序列化支持

```csharp
// 保存画布
editor.SaveCanvas("workflow.stn");

// 加载画布
editor.LoadCanvas("workflow.stn");
```

## 🌍 多语言配置

```csharp
// 设置语言
Lang.SetLanguage("zh-CN");  // 中文
Lang.SetLanguage("en-US");  // 英文

// 使用翻译
string text = Lang.Get("KeyName");

// 在节点中使用
Title = Lang.Get("AddNode");
```

## 📊 技术规格

- **框架**: .NET 8.0 / .NET Framework 4.7.2
- **UI技术**: Windows Forms
- **语言**: C# 11.0
- **版本**: 1.2.0.2410
- **代码行数**: ~10,500
- **文件数**: 41 个 C# 文件

## 📚 文档资源

### 详细文档
- [完整技术文档](../../docs/engine-components/ST.Library.UI.md) - 详细的API和使用指南
- [Engine组件概览](../../docs/engine-components/Engine组件概览.md) - 引擎组件总览
- [改进建议](./改进建议.md) - 项目改进建议和优化方向

### 相关组件
- [FlowEngineLib](../../docs/04-api-reference/engine-components/FlowEngineLib.md) - 流程引擎库
- [ColorVision.Engine](../../docs/04-api-reference/engine-components/ColorVision.Engine.md) - 主引擎

## 🤝 开发指南

### 节点开发规范

1. **使用 STNodeAttribute 标记节点**
   ```csharp
   [STNode("分类/节点名", "描述")]
   ```

2. **使用 STNodePropertyAttribute 标记属性**
   ```csharp
   [STNodeProperty("显示名", "描述")]
   ```

3. **在 OnCreate 中初始化**
   - 添加输入/输出选项
   - 设置节点样式
   - 注册事件处理

4. **实现数据处理逻辑**
   - 监听 DataTransfer 事件
   - 处理输入数据
   - 更新输出数据

### 最佳实践

✅ **推荐做法**:
- 保持节点功能单一
- 提供清晰的属性描述
- 实现适当的数据验证
- 处理异常情况
- 使用有意义的命名

❌ **避免**:
- 在绘制方法中创建对象
- 循环依赖的连接
- 未验证的数据处理
- 过于复杂的节点逻辑

## 📝 示例项目

完整示例请参考:
- ColorVision 主项目中的节点使用
- FlowEngineLib 中的流程节点实现

## 🐛 问题反馈

如遇到问题或有改进建议:
1. 查看 [改进建议文档](./改进建议.md)
2. 参考详细技术文档
3. 提交 Issue 到项目仓库

## 📄 许可证

Copyright © Crystal_lz

详见 [软件许可协议](../../docs/Software%20License%20Agreement.md)

## 🔄 版本历史

### v1.2.0.2410 (当前版本)
- ✨ 完善的节点编辑功能
- 🌍 多语言支持
- ⚡ 属性编辑器优化  
- 🚀 性能改进
- 🐛 Bug 修复

---

**技术支持**: Color Vision Team  
**更新日期**: 2024
