# ColorVision.Solution

`UI/ColorVision.Solution/` 是桌面工作区壳层：负责 `.cvsln`、工程树、编辑器分发、停靠布局、终端、多图预览、Markdown 预览和 Solution 侧本地 RBAC。

它不是算法运行时，也不是全项目统一权限网关。遇到问题时，先按下面的链路定位。

## 先查什么

| 现象 | 第一检查点 |
| --- | --- |
| `.cvsln` 打不开、最近文件失效 | `SolutionManager` 的路径、文件存在性、目录权限 |
| 文件双击后编辑器不对 | `EditorManager` 是否扫描到对应 Attribute，默认编辑器配置是否指向旧类型 |
| 标签页或面板布局丢失 | `WorkspaceManager`、`DockLayoutManager`、`ContentId` 和 layout 文件 |
| 终端空白或无法输入 | `TerminalControl` 的 ConPTY 初始化、shell 路径、当前目录、释放流程 |
| Markdown/WebView2 空白 | WebView2 Runtime、用户数据目录权限、Web 编辑器初始化 |
| 多图预览卡顿 | 图片数量、扩展名过滤、缩略图缓存和释放 |
| 登录态或权限异常 | 先区分 Solution 本地 RBAC 与全局 `Authorization.Instance.PermissionMode` |

## 模块边界

| 模块 | 负责 |
| --- | --- |
| 根目录 | `SolutionManager`、启动初始化、打开窗口 |
| `Explorer/` | 解决方案树、节点、新建项目、新建文件、右键菜单 |
| `Editor/` | 文件/文件夹编辑器注册、选择、默认编辑器配置 |
| `Workspace/` | AvalonDock 文档区、面板、布局保存/恢复/重置 |
| `Terminal/` | 内置终端控件和 ConPTY 封装 |
| `MultiImageViewer/` | 文件夹多图预览、缩略图缓存、编辑器接入 |
| `Rbac/` | 本地用户、角色、权限、会话、审计和管理窗口 |

## 运行链路

1. `SolutionManager` 打开或创建 `.cvsln`，维护最近文件和当前工作区。
2. `SolutionExplorer` 把目录、文件、项目模板和文件模板组织成树。
3. `EditorManager` 扫描实现 `IEditor` 的类型，并按 Attribute 选择编辑器。
4. `WorkspaceManager` 和 `DockLayoutManager` 把编辑器放进文档区并恢复布局。
5. 终端、多图预览、Markdown、Hex 等能力作为具体编辑器或控件挂进工作区。
6. `Rbac/` 提供 Solution 侧本地权限管理，但很多入口仍同时依赖全局 `PermissionMode`。

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

## RBAC 边界

`Rbac/` 里已经有 `RbacManager`、登录窗口、用户/角色/权限管理窗口、实体、服务、本地 SQLite 和 `PermissionChecker`。

但它当前更准确的定位是 Solution 侧本地 RBAC 子模块：

- 细粒度 `HasPermissionAsync` 调用主要集中在 `Rbac/` 内。
- 许多窗口入口仍用 `Authorization.Instance.PermissionMode` 做粗粒度判断。
- 不要把它写成“所有文件操作、所有编辑器、所有菜单都已被细粒度权限码拦截”。

## 发布验收

| 验收项 | 通过标准 |
| --- | --- |
| DLL 和依赖 | `ImageEditor`、`UI.Desktop`、AvalonDock、AvalonEdit、WebView2、WPFHexaEditor 能解析 |
| 解决方案入口 | `.cvsln`、文件夹、最近文件打开正常 |
| 树和模板 | 新项目、新文件、右键菜单可用 |
| 编辑器 | 文本、图像、Web/Markdown、Hex、文件夹编辑器能被扫描和选择 |
| 布局 | 标签页、面板布局保存、加载、重置正常 |
| 终端 | 打开/关闭后不残留 shell，退出时释放进程和计时器 |
| RBAC | 登录、退出、用户/角色/权限窗口可打开，登录态边界清楚 |

## 关键文件

| 想看 | 文件 |
| --- | --- |
| 工作区入口 | `SolutionManager.cs`、`SolutionManagerInitializer.cs`、`OpenSolutionWindow.xaml.cs` |
| 树和节点 | `Explorer/SolutionExplorer.cs`、`Explorer/SolutionNodeFactory.cs`、`TreeViewControl.xaml.cs` |
| 编辑器分发 | `Editor/EditorManager.cs`、`Editor/IEditor.cs`、`Editor/EditorForExtensionAttribute.cs` |
| 工作区布局 | `Workspace/WorkspaceManager.cs`、`Workspace/DockLayoutManager.cs` |
| 本地权限 | `Rbac/RbacManager.cs`、`Rbac/Services/`、`Rbac/Entity/` |
