# ColorVision MainWindow 引入 DockLayoutManager 影响分析报告

> 生成日期: 2026-03-28 (更新)  
> 分析范围: ColorVision MainWindow 结构、ViewGridManager、DisPlayManager、WorkspaceMainView、接口抽象方案  
> 本文档包含两个方案对比: **方案 A (完全重构)** vs **方案 B (轻量外嵌, ★推荐 — 已实施)**

---

## 0. 方案 B 实施状态

> ✅ **方案 B 已实施完成**。以下是实际变更概要。

### 0.1 实际修改文件

| 文件 | 改动类型 | 说明 |
|------|----------|------|
| `ColorVision/MainWindow.xaml` | 中改 | 右侧 MainContent 替换为 `DockingManager`，含 `LayoutDocumentPane` + 底部日志面板 |
| `ColorVision/MainWindow.xaml.cs` | 中改 | ViewGrid 作为 LayoutDocument 注册；AvalonDock 主题初始化；日志面板初始化；DockLayoutManager 初始化；布局自动保存 |
| `ColorVision/ColorVision.csproj` | 小改 | 添加 `Dirkster.AvalonDock.Themes.VS2013 v4.72.1` 包引用 |
| `UI/ColorVision.Solution/Workspace/DockLayoutManager.cs` | 中改 | 新增 `RegisterDocument()` 方法和 `DocumentInfo` 记录；布局文件改为 `MainWindowDockLayout.xml`；ResetLayout 恢复文档 |
| `UI/ColorVision.Solution/Workspace/WorkspaceMainView.xaml` | 简化 | 移除内部 DockingManager（AvalonDock 布局已提升到 MainWindow 级别） |
| `UI/ColorVision.Solution/Workspace/WorkspaceMainView.xaml.cs` | 简化 | 移除 DockingManager/LogOutput/Theme 初始化；保留 Close 命令绑定 |
| `UI/ColorVision.Solution/Workspace/LayoutMenuItems.cs` | **新增** | 视图菜单: 保存窗口布局、应用窗口布局、重置窗口布局 |

### 0.2 未修改的文件 (方案 B 零改动验证)

| 组件 | 状态 |
|------|------|
| `ViewGridManager.cs` | ✅ 零改动 — 整体作为 LayoutDocument 内容 |
| `DisPlayManager.cs` | ✅ 零改动 — 侧边栏 StackPanel 不变 |
| 7 个编辑器 (TextEditor, ImageEditor, HexEditor, ProjectEditor, WebEditor, SoloutionEditorControl, MultiImageViewer) | ✅ 零改动 — 通过 `WorkspaceManager.LayoutDocumentPane` 间接使用 |
| 12 个 IDisPlayControl 设备控件 | ✅ 零改动 |
| Engine 层代码 | ✅ 零改动 |

### 0.3 架构变更概览

**之前:**
```
MainWindow
├── LeftTabControl ("项目"/"采集")
└── MainContent
    ├── ViewGrid (Visibility 绑定 !SolutionTab1.IsSelected)
    │   └── ViewGridManager 管理 N 宫格
    └── SolutionGrid (Visibility 绑定 SolutionTab1.IsSelected)
        └── WorkspaceMainView (内含自己的 DockingManager)
            ├── LayoutDocumentPane (编辑器)
            └── LogPanel
```

**之后:**
```
MainWindow
├── LeftTabControl ("项目"/"采集") [不变]
└── DockingManager (AvalonDock)
    ├── LayoutDocumentPane
    │   ├── LayoutDocument "采集视图" → ViewGrid [ViewGridManager 管理的 N 宫格]
    │   └── (编辑器文档 — 由 WorkspaceManager.LayoutDocumentPane 动态添加)
    └── BottomPaneGroup
        └── LayoutAnchorable "日志" → LogOutput
```

### 0.4 菜单项

视图菜单下新增三项 (位于 `UI/ColorVision.Solution/Workspace/LayoutMenuItems.cs`):
- **保存窗口布局** (Order: 100) — 序列化当前 AvalonDock 布局到 `MainWindowDockLayout.xml`
- **应用窗口布局** (Order: 101) — 从 `MainWindowDockLayout.xml` 反序列化布局
- **重置窗口布局** (Order: 102) — 删除布局文件并重建默认布局

---

## 1. 当前 MainWindow 架构概览

### 1.1 XAML 布局结构 (`ColorVision/MainWindow.xaml`)

```
Window (MainWindow)
├── Row 0: Menu + SearchBar + RightMenuItemPanel (顶部)
├── Row 1: ContentGrid (主体)
│   ├── Column 0: LeftMainContent (左侧侧边栏, 可隐藏)
│   │   └── LeftTabControl (TabStripPlacement=Bottom)
│   │       ├── TabItem "项目" (SolutionTab1) → TreeViewControl
│   │       └── TabItem "采集" (ViewTab) → ScrollViewer → StackPanelSPD
│   └── Column 1: MainContent (右侧主内容)
│       ├── ViewGrid (采集视图) — Visibility 绑定 !SolutionTab1.IsSelected
│       └── SolutionGrid (解决方案视图) — Visibility 绑定 SolutionTab1.IsSelected
└── Row 2: StatusBar (底部)
```

**核心问题**: `ViewGrid` 和 `SolutionGrid` 通过 **Visibility 互斥切换** 实现视图分离。这是导致程序"割裂"的根本原因 —— 两个视图无法同时显示、无法自由拖拽组合。

### 1.2 初始化流程 (`MainWindow.xaml.cs: Window_Initialized`)

```
1. MenuManager 初始化
2. WorkspaceMainView 创建 → 放入 SolutionGrid (内含自己的 DockingManager)
3. ViewGridManager 初始化 → 绑定到 ViewGrid (纯 Grid-based 布局)
4. DisPlayManager 初始化 → 绑定到 StackPanelSPD (设备控件列表)
5. TreeViewControl 创建 → 放入 SolutionTab1
6. MenuManager 加载菜单项
7. StatusBarManager 初始化
8. LoadIMainWindowInitialized() → 加载所有 IMainWindowInitialized 扩展
```

### 1.3 依赖层次与 AvalonDock 现状

```
ColorVision (主应用) — 无直接 AvalonDock 依赖
├── 引用 → UI/ColorVision.Solution — 有 Dirkster.AvalonDock.Themes.VS2013 v4.72.1
│   └── Workspace/DockLayoutManager.cs (现有)
│   └── Workspace/WorkspaceMainView.xaml (含 DockingManager)
│   └── 引用 → UI/ColorVision.UI — 无 AvalonDock 依赖
│       └── 引用 → UI/ColorVision.Common — 无 AvalonDock 依赖
│           └── Interfaces/Views/ViewGridManager.cs
│           └── Interfaces/Views/View.cs, IView.cs
├── 引用 → Engine/ColorVision.Engine (引擎, 含各设备 Display)
│   └── 引用 → UI/ColorVision.UI
└── 引用 → UI/ColorVision.UI.Desktop (桌面扩展)
```

**关键事实**: AvalonDock 依赖目前仅在 `ColorVision.Solution` 中。`ColorVision.UI` 和 `ColorVision.Common` 不依赖 AvalonDock。

---

## 2. 方案 B: 轻量外嵌方案 (★ 推荐)

> **核心思路**: 将 `ViewGridManager` 和 `DisPlayManager` 视为**独立控件黑盒**，不做内部改动。
> 仅在 MainWindow 外层用 AvalonDock 包裹它们，使"采集"和"解决方案"可以同时存在于 DockLayout 中。

### 2.1 目标架构

```
Window (MainWindow)
├── Row 0: Menu + SearchBar + RightMenuItemPanel (不变)
├── Row 1: DockingManager (替代 ContentGrid)
│   └── LayoutRoot
│       └── LayoutPanel (Horizontal)
│           ├── LayoutAnchorablePaneGroup (左侧, 可隐藏/自动隐藏)
│           │   ├── LayoutAnchorable "项目" → TreeViewControl [原样]
│           │   └── LayoutAnchorable "采集" → ScrollViewer+StackPanelSPD [原样]
│           └── LayoutPanel (Vertical)
│               ├── LayoutDocumentPaneGroup (中央文档/视图区)
│               │   └── LayoutDocumentPane
│               │       ├── LayoutDocument "采集视图" → ViewGrid [整个Grid作为内容]
│               │       │   └── (内部依旧由 ViewGridManager 管理 N 宫格)
│               │       └── LayoutDocument (各编辑器, 如 TextEditor, ImageEditor...)
│               └── LayoutAnchorablePaneGroup (底部)
│                   └── LayoutAnchorablePane
│                       └── LayoutAnchorable "日志" → LogOutput
└── Row 2: StatusBar (不变)
```

### 2.2 核心设计原则

| 原则 | 说明 |
|------|------|
| **ViewGridManager 作为整体 Page** | ViewGrid 整个 Grid 成为一个 LayoutDocument 的 Content，内部 N 宫格逻辑 **完全不改** |
| **DisPlayManager 保持与 Solution 同级** | DisPlayManager 的 ScrollViewer+StackPanelSPD 整体成为一个 LayoutAnchorable，内部逻辑 **完全不改** |
| **Solution DockingManager 可移除** | 如果 MainWindow 已有顶层 DockingManager，WorkspaceMainView 内部的 DockingManager **不再需要** |
| **编辑器直接注册到顶层 DocumentPane** | 7 个编辑器改用 MainWindow 级别的 LayoutDocumentPane（WorkspaceManager 引用更新） |

### 2.3 具体变更清单

#### 变更 1: MainWindow.xaml — ContentGrid 替换为 DockingManager

**当前** (MainWindow.xaml 第 88-109 行):
```xml
<Grid x:Name="ContentGrid" Grid.Row="1">
    <Grid.ColumnDefinitions>...</Grid.ColumnDefinitions>
    <Border x:Name="LeftMainContent">
        <TabControl x:Name="LeftTabControl">
            <TabItem x:Name="SolutionTab1" Header="项目"/>
            <TabItem x:Name="ViewTab" Header="采集">
                <ScrollViewer><StackPanel x:Name="StackPanelSPD"/></ScrollViewer>
            </TabItem>
        </TabControl>
    </Border>
    <Grid Grid.Column="1" x:Name="MainContent">
        <Grid x:Name="ViewGrid" Visibility="{Binding !SolutionTab1.IsSelected}"/>
        <Grid x:Name="SolutionGrid" Visibility="{Binding SolutionTab1.IsSelected}"/>
    </Grid>
</Grid>
```

**改为**:
```xml
<xcad:DockingManager x:Name="DockingManager1" Grid.Row="1">
    <xcad:LayoutRoot x:Name="_layoutRoot">
        <xcad:LayoutPanel Orientation="Horizontal">
            <!-- 左侧面板组 -->
            <xcad:LayoutAnchorablePaneGroup DockWidth="303" x:Name="LeftPaneGroup">
                <xcad:LayoutAnchorablePane>
                    <xcad:LayoutAnchorable Title="项目" ContentId="ProjectPanel"
                                           CanClose="False" CanAutoHide="True" CanFloat="True">
                        <Grid x:Name="ProjectPanelHost"/>  <!-- TreeViewControl 放这里 -->
                    </xcad:LayoutAnchorable>
                    <xcad:LayoutAnchorable Title="采集" ContentId="AcquirePanel"
                                           CanClose="False" CanAutoHide="True" CanFloat="True">
                        <ScrollViewer HorizontalScrollBarVisibility="Auto" VerticalScrollBarVisibility="Auto">
                            <StackPanel x:Name="StackPanelSPD" Margin="-2,5,0,0"/>
                        </ScrollViewer>
                    </xcad:LayoutAnchorable>
                </xcad:LayoutAnchorablePane>
            </xcad:LayoutAnchorablePaneGroup>
            <!-- 中央 + 底部 -->
            <xcad:LayoutPanel Orientation="Vertical">
                <xcad:LayoutDocumentPaneGroup>
                    <xcad:LayoutDocumentPane x:Name="MainDocumentPane"/>
                </xcad:LayoutDocumentPaneGroup>
                <xcad:LayoutAnchorablePaneGroup DockHeight="200" x:Name="BottomPaneGroup">
                    <xcad:LayoutAnchorablePane x:Name="BottomPane">
                        <xcad:LayoutAnchorable Title="日志" ContentId="LogPanel"
                                               CanClose="True" CanAutoHide="True" CanFloat="True">
                            <Grid x:Name="LogPanelGrid"/>
                        </xcad:LayoutAnchorable>
                    </xcad:LayoutAnchorablePane>
                </xcad:LayoutAnchorablePaneGroup>
            </xcad:LayoutPanel>
        </xcad:LayoutPanel>
    </xcad:LayoutRoot>
</xcad:DockingManager>
```

**影响**: `MainWindow.xaml` 一个文件，删除 ~20 行，新增 ~35 行。

#### 变更 2: MainWindow.xaml.cs — 初始化适配

**当前**:
```csharp
WorkspaceMainView solutionView = new WorkspaceMainView();
SolutionGrid.Children.Add(solutionView);
ViewGridManager = ViewGridManager.GetInstance();
ViewGridManager.MainView = ViewGrid;
DisPlayManager.GetInstance().Init(this, StackPanelSPD);
SolutionTab1.Content = new TreeViewControl();
```

**改为**:
```csharp
// ViewGrid 整体作为一个文档页
ViewGrid = new Grid { Background = ... };
var viewDoc = new LayoutDocument { Title = "采集视图", ContentId = "ViewGridDoc", CanClose = false };
viewDoc.Content = ViewGrid;
MainDocumentPane.Children.Add(viewDoc);

ViewGridManager = ViewGridManager.GetInstance();
ViewGridManager.MainView = ViewGrid;   // ← ViewGridManager 内部完全不变

DisPlayManager.GetInstance().Init(this, StackPanelSPD);  // ← DisPlayManager 完全不变

ProjectPanelHost.Children.Add(new TreeViewControl());

// 日志面板
var logOutput = new LogOutput(...);
LogPanelGrid.Children.Add(logOutput);

// WorkspaceManager 指向顶层 DocumentPane
WorkspaceManager.layoutRoot = _layoutRoot;
WorkspaceManager.LayoutDocumentPane = MainDocumentPane;

// DockLayoutManager 注册
var layoutManager = new DockLayoutManager(DockingManager1);
layoutManager.RegisterPanel("ProjectPanel", ProjectPanelHost, "项目", PanelPosition.Left);
layoutManager.RegisterPanel("AcquirePanel", StackPanelSPD.Parent, "采集", PanelPosition.Left);
layoutManager.RegisterPanel("LogPanel", LogPanelGrid, "日志", PanelPosition.Bottom);
WorkspaceManager.LayoutManager = layoutManager;
layoutManager.LoadLayout();
```

**影响**: `MainWindow.xaml.cs` 修改 ~15 行，新增 ~20 行。ViewGridManager / DisPlayManager 零改动。

#### 变更 3: WorkspaceMainView — 简化或移除内部 DockingManager

**方案 B-1 (移除 WorkspaceMainView 的 DockingManager)**: 

WorkspaceMainView 不再需要自己的 DockingManager，因为编辑器直接注册到 MainWindow 的 `MainDocumentPane`。WorkspaceMainView 可以简化为一个不含 AvalonDock 的 UserControl，或者完全移除。

```csharp
// WorkspaceMainView.xaml.cs 简化为:
public partial class WorkspaceMainView : UserControl
{
    public WorkspaceMainView()
    {
        InitializeComponent();
        // 不再需要 DockingManager 初始化
        // 不再需要 LogOutput 初始化 (已移至 MainWindow)
        // DealyLoad 仍然执行
        foreach (var action in WorkspaceManager.DealyLoad)
            action();
        WorkspaceManager.DealyLoad.Clear();
    }
}
```

**方案 B-2 (保持 WorkspaceMainView 嵌套)**: 

不修改 WorkspaceMainView，让它作为一个 LayoutDocument 的内容嵌入。此时会有嵌套的 DockingManager（MainWindow 级 + WorkspaceMainView 级），技术上可行但有拖拽冲突风险。

**推荐 B-1**: 移除内部 DockingManager，减少复杂度。

#### 变更 4: WorkspaceManager — 引用指向变更

**当前**: WorkspaceManager 的 `layoutRoot`、`LayoutDocumentPane` 由 WorkspaceMainView 初始化。

**改为**: 由 MainWindow 初始化。

```csharp
// 改动仅在赋值来源:
// 原: WorkspaceMainView.cs → WorkspaceManager.layoutRoot = _layoutRoot;
// 新: MainWindow.xaml.cs → WorkspaceManager.layoutRoot = _layoutRoot;
```

**影响**: `WorkspaceManager.cs` 零代码修改（仅赋值来源改变）。

#### 变更 5: 7 个编辑器 — 零代码修改

所有编辑器使用 `WorkspaceManager.LayoutDocumentPane.Children.Add(layoutDocument)`。
由于 WorkspaceManager.LayoutDocumentPane 现在指向 MainWindow 的 `MainDocumentPane`，
编辑器代码**完全不需要修改**。

验证: 7 个编辑器的引用方式:
```csharp
// 所有编辑器都用这个模式 (SoloutionEditorControl, TextEditor, ImageEditor, HexEditor, 
//                          ProjectEditor, WebEditor, MultiImageViewer):
WorkspaceManager.LayoutDocumentPane.Children.Add(layoutDocument);
WorkspaceManager.LayoutDocumentPane.SelectedContentIndex = ...;
```
只要 `WorkspaceManager.LayoutDocumentPane` 指向正确的 Pane，编辑器就能工作。

### 2.4 方案 B 影响评估矩阵

| 优先级 | 文件 | 改动类型 | 改动量 | 风险 |
|--------|------|----------|--------|------|
| 🔴 P0 | `ColorVision/MainWindow.xaml` | 中改 — ContentGrid → DockingManager | ~35 行 | 中 |
| 🔴 P0 | `ColorVision/MainWindow.xaml.cs` | 中改 — 初始化流程适配 | ~35 行 | 中 |
| 🟡 P1 | `UI/ColorVision.Solution/Workspace/WorkspaceMainView.xaml` | 简化 — 移除 DockingManager | ~20 行 | 低 |
| 🟡 P1 | `UI/ColorVision.Solution/Workspace/WorkspaceMainView.xaml.cs` | 简化 — 移除 DockingManager 初始化 | ~15 行 | 低 |
| 🟢 P2 | `ColorVision/MainWindowConfig.cs` | 小改 — 移除 LeftTabControlSelectedIndex | ~5 行 | 低 |
| 🟢 P2 | `ColorVision/ColorVision.csproj` | 小改 — 添加 AvalonDock 包引用 | 1 行 | 无 |
| ⚪ | `UI/ColorVision.Common/Interfaces/Views/ViewGridManager.cs` | **无改动** | 0 | 无 |
| ⚪ | `UI/ColorVision.UI/DisPlayManager.cs` | **无改动** | 0 | 无 |
| ⚪ | 7 个编辑器 | **无改动** | 0 | 无 |
| ⚪ | 12 个 IDisPlayControl | **无改动** | 0 | 无 |
| ⚪ | `Engine/ColorVision.Engine/**` | **无改动** | 0 | 无 |

### 2.5 方案 B 影响统计

| 类别 | 数量 |
|------|------|
| 需要修改的文件 | **~6** |
| 需要大改的文件 | **0** |
| 需要中改的文件 | **2** (MainWindow.xaml, .cs) |
| 需要小改的文件 | **4** (WorkspaceMainView x2, Config, csproj) |
| **完全不受影响的文件** | ViewGridManager, DisPlayManager, 7 编辑器, 12 Display 控件, Engine |

### 2.6 方案 B 代码量估计

| 组件 | 预计修改行数 | 预计新增行数 |
|------|------------|------------|
| MainWindow.xaml | ~20 行删除 | ~35 行新增 |
| MainWindow.xaml.cs | ~15 行修改 | ~20 行新增 |
| WorkspaceMainView.xaml | ~15 行删除 | ~5 行新增 |
| WorkspaceMainView.xaml.cs | ~30 行删除 | ~5 行新增 |
| MainWindowConfig.cs | ~3 行删除 | 0 行 |
| ColorVision.csproj | 0 | 1 行 |
| **总计** | **~83 行修改/删除** | **~66 行新增** |

---

## 3. 接口抽象方案: 将 DockLayout 封装到 ColorVision.UI

### 3.1 问题背景

当前 `DockLayoutManager` 类直接依赖 `AvalonDock.DockingManager`、`AvalonDock.Layout.*` 等类型。
`ColorVision.UI` 不依赖 AvalonDock，所以 `DockLayoutManager` 无法直接放入 `ColorVision.UI`。

但 `ViewGridManager`、`DisPlayManager`、`View.cs` 等都在 `ColorVision.UI` / `ColorVision.Common` 层，
如果未来其他应用（如 Engine.MainWindow、插件窗口）也想使用 DockLayout，需要一个与 AvalonDock 解耦的抽象。

### 3.2 接口设计方案

在 `ColorVision.UI` 中定义抽象接口:

```csharp
// UI/ColorVision.UI/DockLayout/IDockLayoutService.cs
namespace ColorVision.UI.DockLayout
{
    /// <summary>
    /// 停靠布局服务接口，抽象 AvalonDock 具体实现。
    /// 可由 ColorVision.Solution 或主应用实现。
    /// </summary>
    public interface IDockLayoutService
    {
        /// <summary>注册一个工具面板</summary>
        void RegisterPanel(string contentId, object content, string title, DockPosition position = DockPosition.Bottom);
        
        /// <summary>注册一个文档页</summary>
        void RegisterDocument(string contentId, object content, string title, bool canClose = true);
        
        /// <summary>切换面板可见性</summary>
        void TogglePanel(string contentId);
        
        /// <summary>激活指定文档</summary>
        void ActivateDocument(string contentId);
        
        /// <summary>关闭指定文档</summary>
        void CloseDocument(string contentId);
        
        /// <summary>保存布局</summary>
        void SaveLayout();
        
        /// <summary>加载布局</summary>
        bool LoadLayout();
        
        /// <summary>重置布局</summary>
        void ResetLayout();
        
        /// <summary>面板是否可见</summary>
        bool IsPanelVisible(string contentId);
    }

    public enum DockPosition
    {
        Bottom,
        Left,
        Right
    }
}
```

### 3.3 实现位置

```
ColorVision.UI (接口定义 — 无 AvalonDock 依赖)
├── DockLayout/IDockLayoutService.cs
├── DockLayout/DockPosition.cs

ColorVision.Solution (AvalonDock 实现)
├── Workspace/DockLayoutManager.cs  ← 实现 IDockLayoutService
```

### 3.4 使用方式

```csharp
// 在 ColorVision.UI 层的代码可以通过接口访问布局:
public static class DockLayoutServiceLocator
{
    public static IDockLayoutService? Current { get; set; }
}

// MainWindow 初始化时注册:
var layoutManager = new DockLayoutManager(DockingManager1);
DockLayoutServiceLocator.Current = layoutManager;

// 任何层（包括 Engine）都可以使用:
DockLayoutServiceLocator.Current?.RegisterDocument("ViewResult", viewControl, "结果视图");
```

### 3.5 接口方案影响评估

| 操作 | 影响 |
|------|------|
| 在 `ColorVision.UI` 新增接口 | 新增 2 个文件 (~30 行)，无破坏性 |
| `DockLayoutManager` 实现接口 | 修改 1 个文件 (~10 行)，添加 `: IDockLayoutService` |
| 其他模块通过接口使用 | 无强制改动，渐进式采用 |

### 3.6 接口方案的价值

| 场景 | 无接口 | 有接口 |
|------|--------|--------|
| Engine.MainWindow 想用 DockLayout | 需要引用 ColorVision.Solution 或复制代码 | 引用 ColorVision.UI 的接口即可 |
| Spectrum 插件 DockLayoutManager | 自己维护一套独立实现 | 可以实现相同接口，统一行为 |
| 测试/Mock | 难以隔离 | 可以 Mock IDockLayoutService |
| 未来更换 AvalonDock 为其他库 | 大范围修改 | 只改实现类 |

---

## 4. 方案 A: 完全重构方案 (仅供对比)

> 这是上一版分析中的方案。将 ViewGridManager 内部也重构为 AvalonDock，每个 IView 成为独立的 LayoutDocument。

### 4.1 目标架构 (方案 A)

```
Window (MainWindow)
├── Row 0: Menu + SearchBar (不变)
├── Row 1: DockingManager
│   └── LayoutRoot
│       └── LayoutPanel (Horizontal)
│           ├── LayoutAnchorablePaneGroup (左侧)
│           │   ├── LayoutAnchorable "项目" → TreeViewControl
│           │   └── LayoutAnchorable "采集" → StackPanelSPD
│           └── LayoutPanel (Vertical)
│               ├── LayoutDocumentPaneGroup (中央)
│               │   └── LayoutDocumentPane
│               │       ├── LayoutDocument (ImageView) ← 每个 IView 独立
│               │       ├── LayoutDocument (ViewSpectrum) ← 每个 IView 独立
│               │       ├── LayoutDocument (ViewFlow) ← 每个 IView 独立
│               │       └── LayoutDocument (各编辑器)
│               └── LayoutAnchorablePaneGroup (底部)
│                   └── "日志" → LogOutput
└── Row 2: StatusBar (不变)
```

### 4.2 方案 A 影响统计

| 类别 | 数量 |
|------|------|
| 需要修改的文件 | **~21** |
| 需要大改/重写的文件 | **3** (MainWindow.xaml/cs, ViewGridManager) |
| 需要中改的文件 | **5** |
| 需要小改的文件 | **13** |
| 代码修改量 | **~515 行修改 + ~430 行新增** |

### 4.3 方案 A 核心风险

- 🔴 ViewGridManager 460 行代码几乎全部重写
- 🔴 N 宫格布局 (`SetViewGrid`, `SetViewGridTwo`, `SetViewGridThree`) 功能丧失
- 🔴 View.cs 基类中 `ViewIndex` / `PreViewIndex` / `ViewType` 语义需要重新定义
- 🔴 `DisPlayManagerExtension.AddViewConfig` 的 ComboBox 逻辑需要全面适配
- 🟡 12 个 IDisPlayControl 的 `Selected` 事件 → ViewIndex 自动切换需要重写
- 🟡 嵌套 DockingManager 问题

---

## 5. 方案对比总结

### 5.1 影响面对比

| 维度 | 方案 A (完全重构) | 方案 B (轻量外嵌) ★ |
|------|-------------------|---------------------|
| 修改文件数 | ~21 | **~6** |
| ViewGridManager | ❌ 几乎全部重写 (~460行) | ✅ **零改动** |
| DisPlayManager | ⚠️ 需要适配 | ✅ **零改动** |
| 12个 IDisPlayControl | ⚠️ 可能需要适配 | ✅ **零改动** |
| 7个编辑器 | ⚠️ 各需小改 (~5行) | ✅ **零改动** |
| View.cs / IView | ⚠️ 语义重定义 | ✅ **零改动** |
| Engine 层代码 | ⚠️ ViewFlow, ViewSMU 适配 | ✅ **零改动** |
| 代码改动量 | ~945行 (修改+新增) | **~149行** |
| N宫格布局 | ❌ 消失 | ✅ **完整保留** |
| 采集/解决方案同时可见 | ✅ 完全融合 | ✅ 可作为文档Tab切换 |
| 布局持久化 | ✅ 原生支持 | ✅ 原生支持 |
| 面板拖拽/浮动 | ✅ 全面支持 | ✅ 面板(项目/采集/日志)支持拖拽浮动 |
| 风险 | 🔴 高 | 🟢 **低** |

### 5.2 方案 B 的局限性

| 局限 | 说明 | 严重程度 |
|------|------|----------|
| 采集视图仍是单个 Tab | ViewGrid 作为一个整体文档，不能将 ImageView 单独拖出 | 🟡 中 |
| N 宫格 + AvalonDock 混合 | 采集视图内部是 Grid 分割，外部是 AvalonDock Tab — 两套布局概念 | 🟡 中 |
| 用户仍需要切换 Tab | 采集视图和编辑器是不同的 Tab，虽然不再互斥但需要手动切换 | 🟢 低 |

### 5.3 方案 B 的额外收益

| 收益 | 说明 |
|------|------|
| ✅ 消除 Visibility 互斥切换 | 采集视图和编辑器可以同时存在，不再需要 LeftTabControl 切换 |
| ✅ 左侧面板可浮动/自动隐藏 | "项目"和"采集"面板可以独立拖拽、浮动、自动隐藏 |
| ✅ 日志面板提升到主窗口级别 | 无论在采集还是解决方案模式都能看到日志 |
| ✅ 布局持久化 | 用户自定义的面板位置可以保存和恢复 |
| ✅ 保留所有现有功能 | N 宫格、设备控件拖拽排序、快捷键 — 全部保留 |
| ✅ 低风险 | 几乎不触碰核心逻辑代码，回归测试范围极小 |
| ✅ 渐进式迁移 | 未来可以在此基础上将 ViewGridManager 内部也迁移到 AvalonDock |

---

## 6. 实施建议

### 6.1 推荐实施路径

```
阶段 1: 方案 B 外嵌 (~6 文件, ~149 行改动)
  ├── MainWindow.xaml: ContentGrid → DockingManager
  ├── MainWindow.xaml.cs: 初始化适配
  ├── WorkspaceMainView: 简化/移除内部 DockingManager
  └── MainWindowConfig: 清理过时配置

阶段 2: (可选) 接口抽象 (~3 文件新增, ~10 行改动)
  ├── ColorVision.UI/DockLayout/IDockLayoutService.cs [新增]
  ├── ColorVision.UI/DockLayout/DockPosition.cs [新增]
  └── DockLayoutManager.cs: 实现 IDockLayoutService

阶段 3: (远期, 可选) ViewGridManager 内部迁移
  └── 如果需要将 IView 也作为独立文档拖拽,
      可以在方案 B 基础上渐进式将 ViewGridManager 改造为 AvalonDock
      (此时再评估方案 A 的部分变更)
```

### 6.2 阶段 1 详细步骤

1. **ColorVision.csproj 添加 AvalonDock 依赖** (或通过 ColorVision.Solution 传递)
2. **MainWindow.xaml 重构 ContentGrid**:
   - 移除 LeftTabControl、ViewGrid/SolutionGrid Visibility 切换
   - 引入 DockingManager，定义 LayoutRoot
   - ViewGrid 作为 LayoutDocument 内容
   - TreeViewControl 和 StackPanelSPD 作为 LayoutAnchorable
3. **MainWindow.xaml.cs 适配初始化**:
   - ViewGridManager.MainView = ViewGrid (不变)
   - DisPlayManager.Init(this, StackPanelSPD) (不变)
   - WorkspaceManager.layoutRoot/LayoutDocumentPane 指向新的 LayoutRoot
   - DockLayoutManager 初始化和 LoadLayout
4. **WorkspaceMainView 简化**:
   - 移除内部 DockingManager 和 LogOutput
   - 保留 DealyLoad 执行
5. **主题适配**: DockingManager 主题切换 (从 WorkspaceMainView 移至 MainWindow)
6. **测试验证**:
   - 采集视图 N 宫格是否正常
   - 设备控件列表是否正常
   - 编辑器打开/关闭是否正常
   - 布局保存/恢复是否正常
   - 面板浮动/自动隐藏是否正常

---

## 7. 附录

### 7.1 AvalonDock 依赖链分析

```
Dirkster.AvalonDock.Themes.VS2013 v4.72.1
└── Dirkster.AvalonDock v4.72.1
```

当前仅 `ColorVision.Solution` 引用此包。方案 B 需要 `ColorVision` 主项目也引用（或通过传递依赖自动获取）。

由于 `ColorVision` 已引用 `ColorVision.Solution`，AvalonDock 的 DLL 已经在输出目录中。
只需在 MainWindow.xaml 中添加 `xmlns:xcad` 命名空间即可使用。

### 7.2 WorkspaceManager 消费者完整清单

以下文件通过 `WorkspaceManager.LayoutDocumentPane` 添加文档:

| 文件 | 使用方式 |
|------|----------|
| `Workspace/SoloutionEditorControl.xaml.cs` | `LayoutDocumentPane.Children.Add(...)` |
| `Editor/TextEditor.cs` | `LayoutDocumentPane.Children.Add(...)` |
| `Editor/ImageEditor.cs` | `LayoutDocumentPane.Children.Add(...)` |
| `Editor/HexEditor.cs` | `LayoutDocumentPane.Children.Add(...)` |
| `Editor/ProjectEditor.cs` | `LayoutDocumentPane.Children.Add(...)` |
| `Editor/WebEditor.cs` | `LayoutDocumentPane.Children.Add(...)` |
| `MultiImageViewer/MultiImageViewer.xaml.cs` | `LayoutDocumentPane.Children.Add(...)` |

以下文件通过 `WorkspaceManager.FindDocumentById(layoutRoot, ...)` 查找文档:
- 同上 7 个文件 (用于检查文档是否已打开)

**方案 B 结论**: 这些文件全部通过 `WorkspaceManager` 静态引用间接访问 LayoutDocumentPane。
只要 `WorkspaceManager.LayoutDocumentPane` 指向 MainWindow 的 `MainDocumentPane`，
这 7 个文件 **完全不需要修改**。

### 7.3 ViewGridManager 公开 API (方案 B 全部保留)

```csharp
Grid MainView;                          // 指向 LayoutDocument 内的 ViewGrid
List<Grid> Grids;                       // N 宫格子 Grid — 不变
List<Control> Views;                    // 所有注册的 View — 不变
int AddView(Control control);           // 注册 View — 不变
void SetViewGrid(int nums);             // 设置 N 宫格 — 不变
void SetViewGridTwo();                  // 2 窗口横排 — 不变
void SetViewGridThree(bool left);       // 3 窗口 L 型 — 不变
void SetSingleWindowView(Control c);    // 弹出浮动窗口 — 不变
// ...全部 API 不变
```

### 7.4 DisPlayManager 核心逻辑 (方案 B 全部保留)

```csharp
// DisPlayManager.Init(window, stackPanel) — 不变
// IDisPlayControls.CollectionChanged → StackPanel 同步 — 不变
// AddAdorners 拖拽排序 — 不变
// ApplyChangedSelectedColor — 不变
// AddViewConfig → ViewGridManager.AddView — 不变
```

### 7.5 MainWindowConfig 过时属性 (方案 B 可清理)

| 属性 | 状态 |
|------|------|
| `IsOpenSidebar` | 可保留或改为 AvalonDock 面板状态 |
| `LeftTabControlSelectedIndex` | ❌ 不再需要 (LeftTabControl 被移除) |
| `IsOpenStatusBar` | 保留不变 (StatusBar 不在 DockingManager 内) |
| `IsFull` | 保留不变 |
