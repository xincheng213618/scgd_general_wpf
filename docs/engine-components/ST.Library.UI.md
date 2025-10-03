# ST.Library.UI

## 目录
1. [概述](#概述)
2. [核心功能](#核心功能)
3. [架构设计](#架构设计)
4. [主要组件](#主要组件)
5. [节点编辑器系统](#节点编辑器系统)
6. [节点容器系统](#节点容器系统)
7. [多语言支持](#多语言支持)
8. [使用示例](#使用示例)
9. [扩展机制](#扩展机制)
10. [最佳实践](#最佳实践)
11. [相关资源](#相关资源)

## 概述

ST.Library.UI 是一个专业的可视化节点编辑器UI库，为 ColorVision 系统提供强大的流程图编辑和可视化编程能力。该库基于 Windows Forms 构建，提供了完整的节点编辑、连接管理、属性编辑等功能。

### 技术规格

- **框架版本**: .NET 8.0 / .NET Framework 4.7.2
- **UI技术**: Windows Forms
- **语言版本**: C# 11.0
- **版本号**: 1.2.0.2410
- **代码规模**: 约 10,567 行代码
- **文件数量**: 41 个 C# 源文件

### 主要特点

- ✅ 可视化节点编辑器
- ✅ 流程图渲染与交互
- ✅ 拖拽式节点连接
- ✅ 动态属性编辑
- ✅ 多语言支持
- ✅ 撤销/重做功能
- ✅ 节点库管理
- ✅ 自定义节点扩展

## 核心功能

### 1. 节点编辑器 (STNodeEditor)

提供完整的可视化节点编辑功能：

- **画布管理**: 无限画布、缩放、平移
- **节点操作**: 创建、删除、移动、选择
- **连接管理**: 拖拽连接、断开连接、连接验证
- **视图控制**: 网格显示、磁吸对齐、边框高亮
- **交互优化**: 框选、快捷键、右键菜单

### 2. 节点系统 (STNode)

灵活的节点基础架构：

- **输入/输出选项**: 类型化的数据端口
- **节点控件**: 嵌入式UI控件
- **属性系统**: 反射式属性编辑
- **标记系统**: 节点注释和分组
- **事件系统**: 完整的生命周期事件

### 3. 属性编辑器 (STNodePropertyGrid)

专业的属性编辑界面：

- **反射式属性**: 自动识别节点属性
- **类型编辑器**: 多种数据类型支持
- **实时编辑**: 即时属性更新
- **描述显示**: 属性说明和错误提示
- **只读模式**: 属性查看模式

### 4. 节点库管理 (STNodeTreeView)

层次化的节点组织：

- **分类浏览**: 树形节点库
- **搜索功能**: 快速查找节点
- **拖拽创建**: 拖放创建节点
- **程序集加载**: 动态加载节点库

## 架构设计

### 整体架构

```
ST.Library.UI
├── NodeEditor/           # 节点编辑器核心
│   ├── STNodeEditor.cs          # 主编辑器控件
│   ├── STNode.cs                # 节点基类
│   ├── STNodeOption.cs          # 节点选项/端口
│   ├── STNodeControl.cs         # 节点控件
│   ├── STNodePropertyGrid.cs    # 属性网格
│   ├── STNodeTreeView.cs        # 节点库视图
│   ├── STNodeEditorPannel.cs    # 编辑器面板
│   ├── STNodeHub.cs             # 中继节点
│   └── ...                      # 其他辅助类
├── NodeContainer/       # 节点容器扩展
│   ├── CVNode.cs                # ColorVision节点基类
│   ├── CVNodeCollection.cs      # 节点集合
│   └── CVNodeContainer.cs       # 节点容器
├── Properties/          # 项目属性
│   ├── AssemblyInfo.cs          # 程序集信息
│   └── Resources.cs             # 资源管理
├── Lang.cs              # 多语言支持
└── FrmSTNodePropertyInput.cs    # 属性输入窗体
```

### 设计模式

#### 1. 组合模式 (Composite Pattern)
节点树结构采用组合模式，支持节点的嵌套和层次化组织。

#### 2. 观察者模式 (Observer Pattern)
事件系统基于观察者模式，实现松耦合的组件通信。

#### 3. 工厂模式 (Factory Pattern)
节点创建通过反射和工厂模式动态实例化。

#### 4. 策略模式 (Strategy Pattern)
不同的连接验证策略通过策略模式实现。

## 主要组件

### STNodeEditor - 节点编辑器

**核心类**: `ST.Library.UI.NodeEditor.STNodeEditor`

节点编辑器是整个库的核心控件，提供完整的可视化编辑能力。

#### 关键属性

```csharp
public float CanvasOffsetX { get; set; }        // 画布X偏移
public float CanvasOffsetY { get; set; }        // 画布Y偏移
public float CanvasScale { get; set; }          // 画布缩放比例
public STNodeCollection Nodes { get; }          // 节点集合
public STNode ActiveNode { get; set; }          // 当前活动节点
public bool ShowGrid { get; set; }              // 显示网格
public bool ShowMagnet { get; set; }            // 磁吸对齐
```

#### 关键方法

```csharp
public void LoadCanvas(string filePath);        // 加载画布
public void SaveCanvas(string filePath);        // 保存画布
public void Clear();                            // 清空画布
public void StartUndo();                        // 开始撤销
public void EndUndo();                          // 结束撤销
```

#### 主要事件

```csharp
event CanvasMoveEvent CanvasMoving;             // 画布移动中
event CanvasMoveEvent CanvasMoved;              // 画布移动完成
event STNodeEditorEventHandler NodeSelected;    // 节点选中
event STNodeEditorEventHandler NodeAdded;       // 节点添加
event STNodeEditorEventHandler NodeRemoved;     // 节点移除
```

### STNode - 节点基类

**核心类**: `ST.Library.UI.NodeEditor.STNode`

所有节点的抽象基类，定义节点的基本行为和属性。

#### 核心属性

```csharp
public string Title { get; set; }                      // 节点标题
public int Left { get; set; }                          // 节点X坐标
public int Top { get; set; }                           // 节点Y坐标
public int Width { get; set; }                         // 节点宽度
public int Height { get; set; }                        // 节点高度
public STNodeOptionCollection InputOptions { get; }    // 输入选项集合
public STNodeOptionCollection OutputOptions { get; }   // 输出选项集合
public STNodeControlCollection Controls { get; }       // 控件集合
public bool IsSelected { get; set; }                   // 是否选中
public bool LockOption { get; set; }                   // 锁定选项
public bool LockLocation { get; set; }                 // 锁定位置
```

#### 生命周期方法

```csharp
protected virtual void OnCreate();                  // 节点创建时
protected virtual void OnOwnerChanged();            // 所有者改变
protected virtual void OnEditorChanged();           // 编辑器改变
protected override void OnDrawTitle(Graphics g);    // 绘制标题
protected override void OnDrawBody(Graphics g);     // 绘制主体
```

#### 自定义节点示例

```csharp
[STNode("示例/自定义节点", "节点描述")]
public class CustomNode : STNode
{
    private int _value;
    
    [STNodeProperty("数值", "数值描述")]
    public int Value
    {
        get => _value;
        set
        {
            _value = value;
            Invalidate();  // 触发重绘
        }
    }
    
    protected override void OnCreate()
    {
        base.OnCreate();
        
        // 添加输入选项
        InputOptions.Add(new STNodeOption("输入", typeof(int), false));
        
        // 添加输出选项
        OutputOptions.Add(new STNodeOption("输出", typeof(int), false));
        
        // 设置节点大小
        Width = 150;
        Height = 100;
    }
}
```

### STNodeOption - 节点选项/端口

**核心类**: `ST.Library.UI.NodeEditor.STNodeOption`

表示节点的输入/输出端口，负责节点间的数据连接。

#### 核心属性

```csharp
public STNode Owner { get; }                // 所属节点
public string Text { get; set; }            // 选项文本
public Type DataType { get; set; }          // 数据类型
public bool IsInput { get; }                // 是否为输入
public bool IsSingle { get; }               // 是否单连接
public int ConnectionCount { get; }         // 连接数量
public Color DotColor { get; set; }         // 端点颜色
```

#### 连接管理

```csharp
public ConnectionStatus ConnectTo(STNodeOption option);      // 连接到选项
public void DisConnectionAll();                              // 断开所有连接
public ConnectionStatus CanConnect(STNodeOption option);     // 检查是否可连接
```

#### 连接事件

```csharp
event STNodeOptionEventHandler Connected;         // 连接建立
event STNodeOptionEventHandler DisConnected;      // 连接断开
event STNodeOptionEventHandler DataTransfer;      // 数据传输
```

### STNodePropertyGrid - 属性编辑器

**核心类**: `ST.Library.UI.NodeEditor.STNodePropertyGrid`

专业的属性编辑控件，支持反射式属性编辑。

#### 核心属性

```csharp
public STNode STNode { get; set; }              // 目标节点
public bool ShowTitle { get; set; }             // 显示标题
public bool AutoColor { get; set; }             // 自动着色
public bool ReadOnlyModel { get; set; }         // 只读模式
public Color ItemSelectedColor { get; set; }    // 选中项颜色
```

#### 属性特性支持

```csharp
[STNodeProperty("属性名称", "属性描述")]
public int PropertyName { get; set; }
```

### STNodeTreeView - 节点库视图

**核心类**: `ST.Library.UI.NodeEditor.STNodeTreeView`

提供层次化的节点库浏览和管理。

#### 核心功能

```csharp
public bool AddNode(Type nodeType);             // 添加节点类型
public int LoadAssembly(string fileName);       // 加载程序集
public void Clear();                            // 清空节点库
public bool ExpandAll();                        // 展开所有
public bool CollapseAll();                      // 折叠所有
```

## 节点编辑器系统

### 画布管理

#### 画布操作

```csharp
// 移动画布
editor.CanvasOffsetX = 100;
editor.CanvasOffsetY = 100;

// 缩放画布
editor.CanvasScale = 1.5f;  // 150% 缩放

// 画布边界
Rectangle bounds = editor.CanvasValidBounds;
```

#### 网格和辅助线

```csharp
// 显示网格
editor.ShowGrid = true;
editor.GridColor = Color.Gray;

// 磁吸对齐
editor.ShowMagnet = true;
editor.MagnetColor = Color.Lime;

// 位置显示
editor.ShowLocation = true;
```

### 节点操作

#### 添加节点

```csharp
// 创建并添加节点
var node = new CustomNode();
node.Left = 100;
node.Top = 100;
editor.Nodes.Add(node);

// 从类型创建
Type nodeType = typeof(CustomNode);
var instance = Activator.CreateInstance(nodeType) as STNode;
editor.Nodes.Add(instance);
```

#### 选择节点

```csharp
// 单选
editor.ActiveNode = node;
node.IsSelected = true;

// 多选
editor.SetSelectedNode(new STNode[] { node1, node2, node3 });

// 获取选中节点
STNode[] selected = editor.GetSelectedNode();
```

#### 删除节点

```csharp
// 删除单个节点
editor.Nodes.Remove(node);

// 删除选中节点
foreach (var n in editor.GetSelectedNode())
{
    editor.Nodes.Remove(n);
}
```

### 连接管理

#### 建立连接

```csharp
// 代码方式连接
STNodeOption output = node1.OutputOptions[0];
STNodeOption input = node2.InputOptions[0];

ConnectionStatus status = output.ConnectTo(input);
if (status == ConnectionStatus.Connected)
{
    Console.WriteLine("连接成功");
}
```

#### 连接验证

系统自动验证以下规则：

- ✅ 输出只能连接到输入
- ✅ 数据类型必须兼容
- ✅ 不能形成循环依赖
- ✅ 单连接选项只能连接一次
- ✅ 不能连接到同一节点

#### 连接状态

```csharp
public enum ConnectionStatus
{
    NoOwner,              // 不存在所有者
    SameOwner,            // 相同的所有者
    SameInputOrOutput,    // 均为输入或输出
    ErrorType,            // 数据类型不匹配
    SingleOption,         // 单连接限制
    Loop,                 // 形成循环
    Exists,               // 连接已存在
    Connected,            // 已连接
    DisConnected,         // 已断开
    Locked,               // 节点锁定
    Reject                // 操作被拒绝
}
```

### 数据传输

#### 数据流

```csharp
// 输出选项设置数据
outputOption.Data = 42;

// 输入选项接收数据
inputOption.DataTransfer += (s, e) =>
{
    var data = e.TargetOption.Data;
    ProcessData(data);
};
```

#### 数据类型

支持的数据类型：
- `typeof(int)`, `typeof(float)`, `typeof(double)`
- `typeof(string)`, `typeof(bool)`
- `typeof(object)` - 通用类型
- 自定义类型

### 撤销/重做

```csharp
// 开始记录撤销操作
editor.StartUndo();

// 执行操作
node.Left = 200;
node.Top = 300;

// 结束记录
editor.EndUndo();

// 撤销
editor.Undo();

// 重做
editor.Redo();
```

### 序列化和持久化

#### 保存画布

```csharp
// 保存到文件
editor.SaveCanvas("workflow.stn");

// 保存到流
using (var stream = new FileStream("workflow.stn", FileMode.Create))
{
    editor.SaveCanvas(stream);
}
```

#### 加载画布

```csharp
// 从文件加载
editor.LoadCanvas("workflow.stn");

// 从流加载
using (var stream = new FileStream("workflow.stn", FileMode.Open))
{
    editor.LoadCanvas(stream);
}
```

## 节点容器系统

### CVNode - ColorVision节点基类

**核心类**: `ST.Library.UI.NodeContainer.CVNode`

ColorVision 系统的节点基类，扩展了 STNode 的功能。

### CVNodeContainer - 节点容器

**核心类**: `ST.Library.UI.NodeContainer.CVNodeContainer`

提供节点的容器化管理和执行环境。

### CVNodeCollection - 节点集合

**核心类**: `ST.Library.UI.NodeContainer.CVNodeCollection`

节点集合的专用管理类。

## 多语言支持

### Lang - 语言管理器

**核心类**: `ST.Library.UI.Lang`

提供完整的多语言支持功能。

#### 语言设置

```csharp
// 设置语言
Lang.SetLanguage("zh-CN");  // 中文
Lang.SetLanguage("en-US");  // 英文

// 获取当前语言
CultureInfo culture = Lang.GetCurrentCulture();

// 获取支持的语言
string[] languages = Lang.GetSupportedLanguages();
```

#### 使用翻译

```csharp
// 获取翻译文本
string text = Lang.Get("KeyName");

// 带参数的翻译
string formatted = Lang.Get("KeyName", arg1, arg2);
```

#### 资源文件

资源文件位于 `Properties/Resources.resx`：
- `Resources.zh-CN.resx` - 中文资源
- `Resources.en-US.resx` - 英文资源

## 使用示例

### 创建简单的节点编辑器

```csharp
using ST.Library.UI.NodeEditor;

// 创建编辑器
var editor = new STNodeEditor
{
    Dock = DockStyle.Fill,
    BackColor = Color.FromArgb(34, 34, 34)
};

// 添加到窗体
this.Controls.Add(editor);

// 创建节点
var node1 = new CustomNode
{
    Title = "节点1",
    Left = 100,
    Top = 100
};

var node2 = new CustomNode
{
    Title = "节点2",
    Left = 300,
    Top = 100
};

editor.Nodes.Add(node1);
editor.Nodes.Add(node2);

// 连接节点
node1.OutputOptions[0].ConnectTo(node2.InputOptions[0]);
```

### 创建节点库面板

```csharp
using ST.Library.UI.NodeEditor;

// 创建面板
var panel = new STNodeEditorPannel
{
    Dock = DockStyle.Fill
};

this.Controls.Add(panel);

// 加载节点库
panel.LoadAssembly("CustomNodes.dll");

// 添加节点类型
panel.AddSTNode(typeof(CustomNode));
```

### 创建属性编辑器

```csharp
var propertyGrid = new STNodePropertyGrid
{
    Dock = DockStyle.Right,
    Width = 250
};

this.Controls.Add(propertyGrid);

// 绑定节点
editor.NodeSelected += (s, e) =>
{
    propertyGrid.STNode = e.Node;
};
```

### 自定义节点开发

```csharp
[STNode("数学/加法", "两个数相加")]
public class AddNode : STNode
{
    [STNodeProperty("结果", "计算结果")]
    public double Result { get; set; }
    
    protected override void OnCreate()
    {
        base.OnCreate();
        
        Title = "加法";
        TitleColor = Color.FromArgb(200, Color.Blue);
        
        var input1 = InputOptions.Add("输入1", typeof(double), false);
        var input2 = InputOptions.Add("输入2", typeof(double), false);
        var output = OutputOptions.Add("输出", typeof(double), false);
        
        input1.DataTransfer += Calculate;
        input2.DataTransfer += Calculate;
    }
    
    private void Calculate(object sender, STNodeOptionEventArgs e)
    {
        double value1 = 0;
        double value2 = 0;
        
        if (InputOptions[0].Data != null)
            value1 = Convert.ToDouble(InputOptions[0].Data);
            
        if (InputOptions[1].Data != null)
            value2 = Convert.ToDouble(InputOptions[1].Data);
        
        Result = value1 + value2;
        OutputOptions[0].Data = Result;
        
        // 触发输出传输
        OutputOptions[0].TransferData();
    }
}
```

## 扩展机制

### 节点属性特性

#### STNodeAttribute

用于定义节点的元数据：

```csharp
[STNode("分类/节点名称", "作者", "邮箱", "网站", "描述")]
public class MyNode : STNode
{
    // ...
}
```

#### STNodePropertyAttribute

用于定义可编辑的属性：

```csharp
[STNodeProperty("显示名称", "属性描述")]
public int MyProperty { get; set; }
```

### 自定义控件

在节点中嵌入自定义控件：

```csharp
protected override void OnCreate()
{
    base.OnCreate();
    
    var ctrl = new STNodeControl
    {
        Left = 10,
        Top = TitleHeight + 10,
        Width = Width - 20,
        Height = 30,
        Text = "自定义控件"
    };
    
    Controls.Add(ctrl);
}
```

### 自定义绘制

```csharp
protected override void OnDrawTitle(DrawingTools dt)
{
    base.OnDrawTitle(dt);
    
    // 自定义标题绘制
    dt.Graphics.DrawString(
        Title,
        Font,
        new SolidBrush(Color.White),
        new Point(10, 5)
    );
}

protected override void OnDrawBody(DrawingTools dt)
{
    base.OnDrawBody(dt);
    
    // 自定义主体绘制
    dt.Graphics.FillRectangle(
        new SolidBrush(BackColor),
        new Rectangle(Left, Top, Width, Height)
    );
}
```

### 程序集动态加载

```csharp
// 加载外部节点库
int count = treeView.LoadAssembly("MyNodeLibrary.dll");
Console.WriteLine($"加载了 {count} 个节点");

// 扫描程序集中的节点
Assembly assembly = Assembly.LoadFrom("MyNodeLibrary.dll");
foreach (Type type in assembly.GetTypes())
{
    if (type.IsSubclassOf(typeof(STNode)) && !type.IsAbstract)
    {
        treeView.AddNode(type);
    }
}
```

## 最佳实践

### 1. 节点设计

- ✅ 保持节点功能单一
- ✅ 合理设置输入输出端口
- ✅ 提供清晰的属性说明
- ✅ 实现适当的数据验证
- ✅ 处理异常情况

### 2. 性能优化

- ✅ 避免在绘制方法中创建对象
- ✅ 使用缓存减少重复计算
- ✅ 批量操作时使用 BeginUpdate/EndUpdate
- ✅ 合理使用 Invalidate 减少重绘

### 3. 数据流设计

- ✅ 明确定义数据类型
- ✅ 避免循环依赖
- ✅ 实现数据验证
- ✅ 处理空数据情况

### 4. 用户体验

- ✅ 提供清晰的节点分类
- ✅ 使用有意义的节点名称
- ✅ 提供详细的属性描述
- ✅ 实现合理的默认值
- ✅ 提供撤销/重做支持

### 5. 扩展开发

- ✅ 遵循命名约定
- ✅ 实现标准接口
- ✅ 提供完整的元数据
- ✅ 编写单元测试
- ✅ 编写使用文档

## 相关资源

### 内部文档

- [Engine组件概览](./Engine组件概览.md)
- [FlowEngineLib](./FlowEngineLib.md)
- [ColorVision.Engine](./ColorVision.Engine.md)

### 代码示例

项目路径: `/Engine/ST.Library.UI/`

### 许可证

Copyright © Crystal_lz

### 版本历史

- **1.2.0.2410**: 当前版本
  - 完善的节点编辑功能
  - 多语言支持
  - 属性编辑器优化
  - 性能改进

## 技术支持

如有问题或建议，请参考：
- [改进建议文档](../../Engine/ST.Library.UI/改进建议.md)
- [项目README](../../Engine/ST.Library.UI/README.md)
