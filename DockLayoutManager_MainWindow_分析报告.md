# ColorVision MainWindow 全面引入 DockLayoutManager 影响分析报告

> 生成日期: 2026-03-28  
> 分析范围: ColorVision MainWindow 结构、ViewGridManager、DisPlayManager、WorkspaceMainView、各设备 Display 控件

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
2. WorkspaceMainView 创建 → 放入 SolutionGrid
3. ViewGridManager 初始化 → 绑定到 ViewGrid
4. DisPlayManager 初始化 → 绑定到 StackPanelSPD  
5. TreeViewControl 创建 → 放入 SolutionTab1
6. MenuManager 加载菜单项
7. StatusBarManager 初始化
8. LoadIMainWindowInitialized() → 加载所有 IMainWindowInitialized 扩展
```

---

## 2. 受影响的核心子系统分析

### 2.1 ViewGridManager — 采集视图网格管理器

**文件**: `UI/ColorVision.Common/Interfaces/Views/ViewGridManager.cs`  
**当前机制**: 
- 接收一个 `Grid MainView` (即 MainWindow 中的 `ViewGrid`)
- 在内部创建 N×M 的子 Grid，通过 `GridSplitter` 分割
- 每个 View (如 ImageView, ViewSpectrum, ViewFlow) 通过 `AddView()` 注册
- 支持 1-N 窗口布局 (`SetViewGrid`, `SetViewGridTwo`, `SetViewGridThree`)
- View 可以独立弹出为浮动窗口 (`SetSingleWindowView`)

**引用关系** (11 个文件):
| 文件 | 用途 |
|------|------|
| `ColorVision/MainWindow.xaml.cs` | 初始化 `ViewGrid` 为 `MainView` |
| `ColorVision/ViewRightMenuItemProvider.cs` | 视图布局切换按钮 |
| `UI/ColorVision.UI/DisPlayManager.cs` | `AddViewConfig` 扩展方法 |
| `UI/ColorVision.UI/Views/ViewHotKey1.cs` | 视图快捷键 |
| `UI/ColorVision.Common/Interfaces/Views/View.cs` | View 基类 |
| `Engine/ColorVision.Engine/Templates/Flow/ViewFlow.xaml.cs` | 流程引擎视图 |
| `Engine/ColorVision.Engine/Services/Devices/SMU/Views/ViewSMU.xaml.cs` | SMU 视图 |
| `Engine/ColorVision.Engine/MainWindow.xaml.cs` | Engine 独立窗口版本 |

**DockLayoutManager 引入影响**:
- ⚠️ **高影响** — `ViewGridManager` 的整个 Grid-based 布局系统需要被 AvalonDock `LayoutDocumentPane` 替代
- 每个 View 不再通过 `Grid.Row/Column` 定位，而是变成 `LayoutDocument`
- `SetViewGrid(N)` / `SetViewGridTwo()` / `SetViewGridThree()` 的 N 宫格功能会被 AvalonDock 的自由拖拽替代
- `SetSingleWindowView()` → AvalonDock 原生支持 Float

### 2.2 DisPlayManager — 设备显示控件管理器

**文件**: `UI/ColorVision.UI/DisPlayManager.cs`  
**当前机制**:
- 管理 `ObservableCollection<IDisPlayControl>`
- 将设备控件添加到 `StackPanel` (侧边栏的 `StackPanelSPD`)
- 支持拖拽排序 (`AddAdorners`)
- 点击设备控件时切换选中状态

**12 个 IDisPlayControl 实现**:
| 设备类型 | 文件 |
|----------|------|
| Camera | `DisplayCamera.xaml.cs` |
| Sensor | `DisplaySensor.xaml.cs` |
| Spectrum | `DisplaySpectrum.xaml.cs` |
| SMU | `DisplaySMU.xaml.cs` |
| Algorithm | `DisplayAlgorithm.xaml.cs` |
| ThirdPartyAlgorithms | `DisplayThirdPartyAlgorithms.xaml.cs` |
| Calibration | `DisplayCalibrationControl.xaml.cs` |
| CfwPort | `DisplayCfwPort.xaml.cs` |
| FileServer | `FileServerDisplayControl.xaml.cs` |
| PG | `DisplayPG.xaml.cs` |
| Motor | `DisplayMotor.xaml.cs` |
| Flow | `DisplayFlow.xaml.cs` |

**DockLayoutManager 引入影响**:
- ⚠️ **中等影响** — 如果要把设备控制面板也放入 AvalonDock，需要将每个 `IDisPlayControl` 包装为 `LayoutAnchorable`
- 现有的 StackPanel + 拖拽排序 机制需要替换为 AvalonDock 的面板系统
- **可选方案**: 保持 `DisPlayManager` + StackPanel 不变，将整个侧边栏作为一个 `LayoutAnchorable` 注册

### 2.3 WorkspaceMainView — 解决方案工作区

**文件**: `UI/ColorVision.Solution/Workspace/WorkspaceMainView.xaml(.cs)`  
**当前机制**:
- 已使用 AvalonDock `DockingManager` + `LayoutDocumentPane`
- 已有 `DockLayoutManager` (最近添加，包含日志面板)
- 7 个编辑器通过 `WorkspaceManager.LayoutDocumentPane.Children.Add()` 添加文档

**7 个编辑器**:
| 编辑器 | 文件 |
|--------|------|
| SolutionEditor | `Workspace/SoloutionEditorControl.xaml.cs` |
| TextEditor | `Editor/TextEditor.cs` |
| ImageEditor | `Editor/ImageEditor.cs` |
| HexEditor | `Editor/HexEditor.cs` |
| ProjectEditor | `Editor/ProjectEditor.cs` |
| WebEditor | `Editor/WebEditor.cs` |
| MultiImageViewerEditor | `MultiImageViewer/MultiImageViewer.xaml.cs` |

**DockLayoutManager 引入影响**:
- ✅ **低影响** — 已经是 AvalonDock 架构，无需大改
- 需要调整为在 MainWindow 级别的 DockingManager 内工作

### 2.4 StatusBarManager

**文件**: `ColorVision/StatusBarManager.cs`  
**当前机制**: 独立的 Grid + StackPanel，通过反射发现 `IStatusBarProvider`

**DockLayoutManager 引入影响**:
- ✅ **无影响** — 状态栏可以保持独立，不需要放入 DockLayoutManager

### 2.5 MainWindowConfig — 视图切换配置

**文件**: `ColorVision/MainWindowConfig.cs`  
**关键属性**:
- `IsOpenSidebar` → 控制侧边栏显隐
- `IsOpenStatusBar` → 控制状态栏显隐  
- `LeftTabControlSelectedIndex` → 控制"项目/采集"标签页切换（默认值=1，即采集页）
- `IsFull` → 全屏状态

**DockLayoutManager 引入影响**:
- ⚠️ **中等影响** — `IsOpenSidebar` / `LeftTabControlSelectedIndex` 的切换逻辑需要重构
- 侧边栏切换可改为 AvalonDock 面板的 Show/Hide

---

## 3. 全面引入 DockLayoutManager 的改造方案

### 3.1 目标架构

```
Window (MainWindow)
├── Row 0: Menu + SearchBar + RightMenuItemPanel (不变)
├── Row 1: DockingManager (替代现在的 ContentGrid)
│   └── LayoutRoot
│       └── LayoutPanel (Horizontal)
│           ├── LayoutAnchorablePaneGroup (左侧, 可隐藏/自动隐藏)
│           │   ├── LayoutAnchorable "项目" → TreeViewControl
│           │   └── LayoutAnchorable "采集" → StackPanelSPD (设备控件列表)
│           └── LayoutPanel (Vertical)
│               ├── LayoutDocumentPaneGroup (中央文档区, 替代 ViewGrid + SolutionGrid)
│               │   └── LayoutDocumentPane (所有文档/视图共用)
│               │       ├── LayoutDocument (各 IView 视图, 如 ImageView)
│               │       └── LayoutDocument (各编辑器, 如 TextEditor)
│               └── LayoutAnchorablePaneGroup (底部)
│                   └── LayoutAnchorablePane
│                       ├── LayoutAnchorable "日志" → LogOutput
│                       └── LayoutAnchorable "输出" (可扩展)
└── Row 2: StatusBar (不变)
```

### 3.2 核心变更点

#### 变更 1: 移除 ViewGrid / SolutionGrid 二分法

**当前**:
```xml
<!-- MainWindow.xaml 第 106-108 行 -->
<Grid x:Name="ViewGrid" Visibility="{Binding SolutionTab1.IsSelected, Converter=bool2VisibilityConverter1}"/>
<Grid x:Name="SolutionGrid" Visibility="{Binding SolutionTab1.IsSelected, Converter=bool2VisibilityConverter}"/>
```

**改为**: 一个统一的 `DockingManager`，所有视图和编辑器作为 `LayoutDocument` 共存。

**影响文件**: `ColorVision/MainWindow.xaml`, `ColorVision/MainWindow.xaml.cs`

#### 变更 2: 重构 ViewGridManager

**当前**: 基于 `Grid` 的 N 宫格布局系统  
**改为**: 基于 AvalonDock `LayoutDocumentPane` 的标签页/分屏系统

**必须保留的功能**:
- 多视图同时显示 → AvalonDock 支持拖拽分屏
- 视图索引切换 → 改为 `LayoutDocumentPane.SelectedContentIndex`
- 浮动窗口 → AvalonDock 原生支持

**影响文件**: `UI/ColorVision.Common/Interfaces/Views/ViewGridManager.cs` (重构核心)  
**间接影响**: 所有 `AddView()` / `SetViewIndex()` 的调用者

#### 变更 3: 侧边栏改造

**当前**: `TabControl` 内含"项目"和"采集"两个 Tab  
**改为**: 两个独立的 `LayoutAnchorable` 面板

**影响文件**: `ColorVision/MainWindow.xaml` (移除 LeftTabControl)

#### 变更 4: WorkspaceMainView 整合

**当前**: 作为独立 `UserControl` 放在 `SolutionGrid` 中，内部有自己的 `DockingManager`  
**改为**: 将其 `LayoutDocumentPane` 合并到 MainWindow 级别的 `DockingManager`

**影响文件**: `UI/ColorVision.Solution/Workspace/WorkspaceMainView.xaml(.cs)`, `WorkspaceManager.cs`

#### 变更 5: 编辑器消费者统一

**当前**: 7 个编辑器使用 `WorkspaceManager.LayoutDocumentPane`  
**改为**: 统一使用 MainWindow 级别的 LayoutDocumentPane

**影响文件**: 7 个编辑器文件（见上方列表）

#### 变更 6: DisPlayManager 适配

**当前**: 设备控件放在侧边栏的 `StackPanel` 中  
**方案 A (最小改动)**: 保持 `StackPanel` 不变，将整个控件列表作为一个 `LayoutAnchorable` 包装  
**方案 B (完全 DockLayout)**: 每个设备控件变为独立的 `LayoutAnchorable`

**建议选方案 A**，改动量最小。

---

## 4. 影响评估矩阵

### 4.1 需要修改的文件清单

| 优先级 | 文件 | 改动类型 | 难度 | 风险 |
|--------|------|----------|------|------|
| 🔴 P0 | `ColorVision/MainWindow.xaml` | 大改 — 替换 ContentGrid 为 DockingManager | 高 | 高 |
| 🔴 P0 | `ColorVision/MainWindow.xaml.cs` | 大改 — 重构初始化流程 | 高 | 高 |
| 🔴 P0 | `UI/ColorVision.Common/Interfaces/Views/ViewGridManager.cs` | 重写 — Grid 布局 → AvalonDock | 高 | 高 |
| 🟡 P1 | `UI/ColorVision.Solution/Workspace/WorkspaceMainView.xaml` | 中改 — 移除内部 DockingManager | 中 | 中 |
| 🟡 P1 | `UI/ColorVision.Solution/Workspace/WorkspaceMainView.xaml.cs` | 中改 — 重构初始化 | 中 | 中 |
| 🟡 P1 | `UI/ColorVision.Solution/Workspace/WorkspaceManager.cs` | 中改 — 指向 MainWindow 级别 DockingManager | 中 | 中 |
| 🟡 P1 | `UI/ColorVision.Solution/Workspace/DockLayoutManager.cs` | 中改 — 提升为 MainWindow 级别 | 中 | 低 |
| 🟡 P1 | `UI/ColorVision.UI/DisPlayManager.cs` | 小改 — 适配 LayoutAnchorable | 低 | 低 |
| 🟢 P2 | `UI/ColorVision.Solution/Workspace/SoloutionEditorControl.xaml.cs` | 小改 — 适配新 DocumentPane | 低 | 低 |
| 🟢 P2 | `UI/ColorVision.Solution/Editor/TextEditor.cs` | 小改 | 低 | 低 |
| 🟢 P2 | `UI/ColorVision.Solution/Editor/ImageEditor.cs` | 小改 | 低 | 低 |
| 🟢 P2 | `UI/ColorVision.Solution/Editor/HexEditor.cs` | 小改 | 低 | 低 |
| 🟢 P2 | `UI/ColorVision.Solution/Editor/ProjectEditor.cs` | 小改 | 低 | 低 |
| 🟢 P2 | `UI/ColorVision.Solution/Editor/WebEditor.cs` | 小改 | 低 | 低 |
| 🟢 P2 | `UI/ColorVision.Solution/MultiImageViewer/MultiImageViewer.xaml.cs` | 小改 | 低 | 低 |
| 🟢 P2 | `ColorVision/MainWindowConfig.cs` | 小改 — 移除 LeftTabControlSelectedIndex 等 | 低 | 低 |
| 🟢 P2 | `ColorVision/ViewRightMenuItemProvider.cs` | 小改 — 视图布局按钮适配 | 低 | 低 |
| 🟢 P2 | `UI/ColorVision.UI/Views/ViewHotKey1.cs` | 小改 — 快捷键适配 | 低 | 低 |
| ⚪ P3 | `Engine/ColorVision.Engine/Templates/Flow/ViewFlow.xaml.cs` | 小改 — AddView 适配 | 低 | 低 |
| ⚪ P3 | `Engine/ColorVision.Engine/Services/Devices/SMU/Views/ViewSMU.xaml.cs` | 小改 | 低 | 低 |
| ⚪ P3 | `Engine/ColorVision.Engine/Services/ServiceManager.cs` | 小改 | 低 | 低 |

### 4.2 影响统计

| 类别 | 数量 |
|------|------|
| 需要大改的文件 | **3** |
| 需要中改的文件 | **5** |
| 需要小改的文件 | **13** |
| 总影响文件数 | **~21** |
| 不受影响的文件 | StatusBarManager, 各 Display*.xaml (12个), Engine MainWindow |

### 4.3 需要修改的代码量估计

| 组件 | 预计修改行数 | 预计新增行数 |
|------|------------|------------|
| MainWindow.xaml | ~30 行 | ~50 行 |
| MainWindow.xaml.cs | ~40 行 | ~60 行 |
| ViewGridManager | ~300 行 (几乎全部重写) | ~200 行 |
| WorkspaceMainView | ~30 行 | ~10 行 |
| WorkspaceManager | ~20 行 | ~10 行 |
| DockLayoutManager (提升) | ~30 行 | ~80 行 |
| 7 个编辑器 | 各 ~5 行 = 35 行 | 0 行 |
| 其他 | ~30 行 | ~20 行 |
| **总计** | **~515 行修改** | **~430 行新增** |

---

## 5. 主要风险和挑战

### 5.1 🔴 高风险: ViewGridManager 重写

**问题**: ViewGridManager 的 N 宫格布局 (`SetViewGrid`, `SetViewGridTwo`, `SetViewGridThree`) 是自定义实现的 Grid-based 系统，与 AvalonDock 的理念完全不同。

**具体困难**:
- 当前支持精确的 N 宫格布局（1-100 格），AvalonDock 不直接支持
- 每个 View 有 `ViewIndex` 属性用于持久化位置，需要新的映射机制
- `ViewMaxChangedEvent` 和所有 `ComboBox` 选择器需要适配
- `View.cs` 基类中的 `ViewIndex`, `PreViewIndex`, `ViewType` 等属性需要重新定义语义

**缓解方案**: 可以保留 ViewGridManager 作为"传统模式"，新增 DockLayout 模式作为可选项。但这会导致两套并行系统，长期维护成本更高。

### 5.2 🔴 高风险: 采集/解决方案视图合并

**问题**: 当前两个视图（ViewGrid 和 SolutionGrid）是互斥显示的。合并后：
- 采集视图 (ImageView 等) 和解决方案编辑器 (TextEditor 等) 将出现在同一个 TabStrip 中
- 用户可能会困惑于混在一起的文档
- 需要提供新的 UI 来区分不同类型的文档

**缓解方案**: 
- 使用 `LayoutDocumentPaneGroup` 将采集视图和编辑器分到不同的 Pane 组
- 为不同类型的文档使用不同的标题图标
- 提供"仅显示采集视图"/"仅显示编辑器"的快速筛选

### 5.3 🟡 中风险: 12 个 IDisPlayControl 控件

**问题**: 这些设备控制面板目前位于侧边栏的 StackPanel 中，有自己的选中/未选中状态管理和颜色高亮逻辑。

**关键耦合**:
- `DisPlayManagerExtension.ApplyChangedSelectedColor` 依赖于 `StackPanel.Tag`
- `DisPlayManagerExtension.AddViewConfig` 通过 `Selected` 事件自动切换视图
- `DisPlayManager.RestoreControl()` 恢复控件排序

**缓解方案**: 采用方案 A，保持 `StackPanel` 不变，将整个列表作为一个 `LayoutAnchorable "采集设备"` 包装。

### 5.4 🟡 中风险: 嵌套的 DockingManager

**问题**: WorkspaceMainView 目前有自己的 `DockingManager`。如果 MainWindow 也引入 `DockingManager`，会出现嵌套的 DockingManager 问题。

**AvalonDock 限制**: 嵌套的 DockingManager 虽然技术上可行，但容易导致：
- 拖拽时的目标 DockingManager 不明确
- 布局序列化/反序列化时状态不一致
- 主题切换时需要同步两个 DockingManager

**缓解方案**: 将 WorkspaceMainView 的 DockingManager 合并到 MainWindow 级别，WorkspaceMainView 简化为只提供 `LayoutDocumentPane` 引用。

### 5.5 🟡 中风险: 布局持久化

**问题**: 当前已有 `WorkspaceDockLayout.xml` 持久化。全面引入后需要一个 MainWindow 级别的布局持久化，且需要处理：
- 动态创建的 LayoutDocument（编辑器打开/关闭是运行时行为）
- IView 视图的状态恢复
- 设备面板注册时机（服务器连接后才知道有哪些设备）

### 5.6 🟢 低风险: 项目兼容性

**问题**: `Projects/` 目录下的项目（ProjectARVRPro, ProjectShiyuan, ProjectBlackMura 等）有自己的窗口和配置。

**评估**: 这些项目使用独立的 Window，不直接使用 MainWindow 的 ViewGrid/SolutionGrid，因此**不受影响**。

---

## 6. 阶段化实施建议

### 阶段 1: 基础设施 (影响最小, 可独立合并)

1. 将 `DockLayoutManager` 从 `ColorVision.Solution.Workspace` 提升到 `ColorVision.UI` 或独立库
2. 增强 `DockLayoutManager` 以支持 `LayoutDocument`（不仅仅是 `LayoutAnchorable`）
3. 增加布局保存/恢复的事件回调机制

### 阶段 2: MainWindow DockingManager 引入 (核心改动)

1. 在 `MainWindow.xaml` 中引入 `DockingManager`，替代 `ContentGrid`
2. 将 `SolutionGrid` (WorkspaceMainView) 和 `ViewGrid` 统一为 `LayoutDocumentPane`
3. 将 `LeftTabControl` 改为两个 `LayoutAnchorable`（"项目"面板和"采集"面板）
4. 日志面板从 WorkspaceMainView 提升到 MainWindow 级别

### 阶段 3: ViewGridManager 适配 (最大改动)

1. 重写 `ViewGridManager` 使其操作 `LayoutDocumentPane` 而非 `Grid`
2. 适配所有 `AddView()` 调用者
3. 适配 `DisPlayManagerExtension.AddViewConfig`

### 阶段 4: 编辑器统一 (低风险批量改动)

1. 7 个编辑器改用新的统一 DocumentPane 引用
2. 移除 WorkspaceMainView 内部的 DockingManager（已在 MainWindow 级别）

### 阶段 5: 配置和菜单适配

1. 更新 `MainWindowConfig`
2. 更新视图菜单项（布局保存/恢复/重置）
3. 更新快捷键

---

## 7. 是否实施的决策参考

### 收益

| 收益 | 说明 |
|------|------|
| ✅ 统一的拖拽布局体验 | 用户可以自由排列采集视图、编辑器、日志、设备面板 |
| ✅ 消除采集/解决方案割裂感 | 两种模式不再需要切换，融为一体 |
| ✅ 布局持久化 | 用户自定义的窗口布局可以保存和恢复 |
| ✅ 浮动窗口原生支持 | 不需要自定义的 `SetSingleWindowView` |
| ✅ 与 Spectrum 插件架构一致 | 统一的 DockLayoutManager 模式 |
| ✅ 简化 ViewGridManager | 不再需要维护复杂的 N 宫格算法 |

### 代价

| 代价 | 说明 |
|------|------|
| ❌ ~21 个文件需要修改 | 包括 3 个大改文件 |
| ❌ ViewGridManager 几乎全部重写 | ~300 行核心代码变动 |
| ❌ 回归测试工作量大 | 12 种设备显示控件、7 种编辑器都需要验证 |
| ❌ 可能引入布局状态管理复杂性 | 动态文档 + 静态面板 + 服务发现 |
| ❌ 用户习惯改变 | N 宫格布局消失，改为自由拖拽 |

### 折中方案（推荐）

如果完全重构的风险过大，可以采用 **渐进式方案**:

1. **保持 ViewGrid 不变**（采集视图保持 N 宫格）
2. **将 MainWindow 的左侧面板和底部面板引入 DockLayoutManager**
3. **统一 SolutionGrid 和 ViewGrid 的显示逻辑**（不再互斥，改为 AvalonDock 的文档组）
4. **逐步迁移 ViewGridManager 的功能到 AvalonDock**

这种方式可以在 **~10 个文件、~200 行改动** 内完成第一阶段，大幅减少风险。

---

## 8. 附录

### 8.1 当前 MainWindow.xaml 关键行号

```
第 15 行: Root Grid 开始
第 88 行: ContentGrid (主体区域)
第 93-105 行: LeftMainContent (侧边栏 + TabControl)
第 106 行: MainContent 开始
第 107 行: ViewGrid (采集视图) ← 核心改造点
第 108 行: SolutionGrid (解决方案视图) ← 核心改造点
第 111 行: StatusBar
```

### 8.2 ViewGridManager 公开 API 清单

```csharp
Grid MainView;                          // 需要替换为 DockingManager
List<Grid> Grids;                       // N 宫格子 Grid，需要替换
List<Control> Views;                    // 所有注册的 View
int AddView(Control control);           // 注册 View
int AddView(int index, Control control);// 注册 View 到指定位置
void RemoveView(Control control);       // 移除 View
void SetViewGrid(int nums);             // 设置 N 宫格
void SetViewGridTwo();                  // 2 窗口横排
void SetViewGridThree(bool left);       // 3 窗口 L 型布局
void SetViewNum(int num);               // 设置显示数量
void SetViewIndex(Control c, int idx);  // 设置 View 位置
void SetOneView(int main);              // 单视图模式
void SetOneView(Control control);       // 单视图模式
void SetSingleWindowView(Control c);    // 弹出浮动窗口
Control? CurrentView;                   // 当前激活的 View
int GetViewNums();                      // 获取窗口数
bool IsGridEmpty(int index);            // 检查格子是否为空
```

### 8.3 相关引用的项目层次

```
ColorVision (主应用)
├── 引用 → UI/ColorVision.Solution (解决方案模块)
│   └── 引用 → UI/ColorVision.UI (基础 UI 库, 含 DisPlayManager)
│       └── 引用 → UI/ColorVision.Common (公共库, 含 ViewGridManager, IDisPlayControl)
├── 引用 → Engine/ColorVision.Engine (引擎, 含各设备 Display)
│   └── 引用 → UI/ColorVision.UI
└── 引用 → UI/ColorVision.UI.Desktop (桌面扩展)
```

`DockLayoutManager` 如果要在 MainWindow 级别使用，最合适的位置是：
- **方案 1**: 放在 `ColorVision` 主应用项目中（最简单，但不能复用）
- **方案 2**: 放在 `UI/ColorVision.UI` 中（需要添加 AvalonDock 依赖到 UI 库）
- **方案 3**: 放在 `UI/ColorVision.Solution` 中（当前位置，但需要让 MainWindow 能访问）
- **推荐方案 3**: 保持在 `ColorVision.Solution.Workspace` 中，因为 MainWindow 已经引用了 `ColorVision.Solution`
