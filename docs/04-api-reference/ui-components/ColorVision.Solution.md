# ColorVision.Solution

`UI/ColorVision.Solution/` 是桌面工作区壳层：负责 `.cvsln`、工程树、编辑器分发、停靠布局、终端和资源打开。

它不是算法运行时，也不是全项目统一权限网关。遇到问题时，先按下面的链路定位。

## 先查什么

| 现象 | 第一检查点 |
| --- | --- |
| `.cvsln` 打不开、最近文件失效 | `SolutionManager` 的路径、文件存在性、目录权限 |
| 文件双击后编辑器不对 | `EditorManager` 是否扫描到对应 Attribute，默认编辑器配置是否指向旧类型 |
| 标签页或面板布局丢失 | `WorkspaceManager`、`DockLayoutManager`、`ContentId` 和 layout 文件 |
| 终端空白或无法输入 | `TerminalControl` 的 ConPTY 初始化、shell 路径、当前目录、释放流程 |
| Web/HTML 编辑器空白 | WebView2 Runtime、用户数据目录权限、Web 编辑器初始化 |

## 模块边界

| 模块 | 负责 |
| --- | --- |
| 根目录 | `SolutionManager`、启动初始化、打开窗口 |
| `Explorer/` | 解决方案树、节点、新建项目、新建文件、右键菜单 |
| `Editor/` | 文件/文件夹编辑器注册、选择、默认编辑器配置 |
| `Workspace/` | AvalonDock 文档区、面板、布局保存/恢复/重置 |
| `Terminal/` | 内置终端控件和 ConPTY 封装 |

## 运行链路

1. `SolutionManager` 打开或创建 `.cvsln`，维护最近文件和当前工作区。
2. `SolutionExplorer` 把目录、文件、项目模板和文件模板组织成树。
3. `EditorManager` 扫描实现 `IEditor` 的类型，并按 Attribute 选择编辑器。
4. `WorkspaceManager` 和 `DockLayoutManager` 把编辑器放进文档区并恢复布局。
5. 终端、Web/HTML、图像、三维模型和 Hex 等能力作为具体编辑器或控件挂进工作区。

## 编辑器扩展

新增文件或文件夹编辑器时，不要改成手写 switch 表。

| 需求 | 做法 |
| --- | --- |
| 打开指定扩展名 | 实现 `IEditor`，通常继承 `EditorBase`，添加 `EditorForExtensionAttribute` |
| 提供通用编辑器 | 添加 `GenericEditorAttribute` |
| 打开文件夹 | 添加 `FolderEditorAttribute` |
| 改默认编辑器 | 检查 `EditorManagerConfig` 和默认编辑器配置 |

验收时至少打开一次目标文件、目标文件夹，并验证重复打开时会激活已有文档而不是创建一堆重复标签。

## 模板扩展

| 模板 | 接口与 Attribute | 验证入口 |
| --- | --- | --- |
| 新项目 | `IProjectTemplate` + `ProjectTemplateAttribute` | `AddNewProjectWindow` |
| 新文件 | `INewItemTemplate` + `NewItemTemplateAttribute` | `AddNewItemWindow` |

模板类通过程序集扫描发现。新增后要确认分类、排序、默认文件名和实际创建路径都正常。

## 发布验收

| 验收项 | 通过标准 |
| --- | --- |
| DLL 和依赖 | `ImageEditor`、`UI.Desktop`、AvalonDock、AvalonEdit、WebView2、WPFHexaEditor 能解析 |
| 解决方案入口 | `.cvsln`、文件夹、最近文件打开正常 |
| 树和模板 | 新项目、新文件、右键菜单可用 |
| 编辑器 | 文本、图像、Web/Markdown、Hex、文件夹编辑器能被扫描和选择 |
| 布局 | 标签页、面板布局保存、加载、重置正常 |
| 终端 | 打开/关闭后不残留 shell，退出时释放进程和计时器 |

## 关键文件

| 想看 | 文件 |
| --- | --- |
| 工作区入口 | `SolutionManager.cs`、`SolutionManagerInitializer.cs`、`OpenSolutionWindow.xaml.cs` |
| 树和节点 | `Explorer/SolutionExplorer.cs`、`Explorer/SolutionNodeFactory.cs`、`TreeViewControl.xaml.cs` |
| 编辑器分发 | `Editor/EditorManager.cs`、`Editor/IEditor.cs`、`Editor/EditorForExtensionAttribute.cs` |
| 工作区布局 | `Workspace/WorkspaceManager.cs`、`Workspace/DockLayoutManager.cs` |
