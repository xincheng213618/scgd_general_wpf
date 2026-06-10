# ColorVision.Solution

本页只保留当前 `UI/ColorVision.Solution/` 里最重要、最稳定的入口类型和子模块，不再继续维护旧文档里那种“完整 API 白皮书 + 版本清单 + 全面 RBAC 接管”式写法。

## 模块定位

`ColorVision.Solution` 当前更适合被理解成桌面工作区壳层，而不是单纯的“解决方案管理器”。

它现在实际承接的内容包括：

- `.cvsln` 解决方案的创建、打开和最近文件管理
- 左侧树形工程浏览与新建项入口
- 文件/文件夹编辑器选择与打开
- AvalonDock 文档区和面板布局管理
- 内置终端控件
- 多图像查看器和缩略图缓存
- Markdown 预览
- Solution 侧本地 RBAC 子模块

这意味着它不是单一窗口，也不是只围绕 `SolutionManager` 组织的一层很薄的 UI。

## 当前最关键的目录

从项目目录看，最值得先认识的是：

- `Editor/`：文件和文件夹编辑器注册、选择和打开
- `Explorer/`：解决方案树、节点模型、新建项和上下文菜单
- `Workspace/`：AvalonDock 文档区与面板布局管理
- `Terminal/`：内置终端控件和 ConPTY 封装
- `MultiImageViewer/`：文件夹多图预览和缩略图缓存
- `RecentFile/`：最近文件历史
- `Rbac/`：Solution 侧本地用户、角色、权限、会话与审计模块
- 根目录的 `SolutionManager.cs`：解决方案打开、创建与当前工作区切换入口

## 关键入口类型

### `SolutionManager`

`SolutionManager` 是当前工作区入口的中心对象。它负责：

- 打开或创建 `.cvsln`
- 维护最近打开的解决方案列表
- 生成当前 `SolutionExplorer`
- 根据命令行或最近文件决定启动时打开哪个解决方案

如果要追“解决方案是怎么进来的”，通常先看它，而不是先看树控件。

### `SolutionExplorer`

`SolutionExplorer` 和 `Explorer/` 目录下的节点类型一起，负责把目录、文件、新建项和右键动作组织成树形工作区。

这部分是“用户怎么看到工程结构”的主入口。

### `EditorManager`

`EditorManager` 负责编辑器注册和分发。当前实现特点很明确：

- 从已加载程序集扫描实现 `IEditor` 的类型
- 通过 `EditorForExtensionAttribute`、`GenericEditorAttribute`、`FolderEditorAttribute` 注册
- 允许为扩展名配置默认编辑器
- 也支持文件夹编辑器

所以当前编辑器系统不是手写 switch 表，而是属性驱动的注册机制。

### `WorkspaceManager` 和 `DockLayoutManager`

这两者负责当前文档工作区的停靠与恢复：

- 查找并激活当前文档
- 维护 `ContentId` 和文档选择状态
- 保存和加载 AvalonDock 布局
- 在布局恢复时按注册表恢复面板和文档内容

如果问题表现为“标签页去哪了”“布局没恢复”“文档区丢了”，通常先看这条链。

### `TerminalControl`

终端能力当前就在这个项目里，而不是单独外置服务。`TerminalControl` 当前负责：

- 启动 `cmd` 或 `powershell`
- 承接 ConPTY 输出
- 维护屏幕缓冲和命令历史
- 运行脚本并处理 URL 点击

所以它更接近一个内建终端 UI 组件，而不是仅仅“调用系统终端”。

### `MultiImageViewer`

`MultiImageViewer` 既可以作为独立 `UserControl` 使用，也通过 `MultiImageViewerEditor` 接到编辑器系统里。

它当前主要负责：

- 文件夹内多图加载
- 支持扩展名过滤
- 缩略图显示
- 与工作区文档标签页协同打开和释放

## 关于 RBAC，这个模块当前到底承担什么

旧文档最大的问题，是把 `ColorVision.Solution` 写成了“全项目统一 RBAC 权限控制层”。当前代码并不是这个状态。

### 当前真实情况

`Rbac/` 的确是 `ColorVision.Solution` 的一个重要子模块，里面已经有：

- `RbacManager`
- `LoginWindow`、`UserManagerWindow`、`PermissionManagerWindow`
- 用户、角色、权限、会话、审计相关实体和服务
- 本地 SQLite 持久化
- `PermissionChecker` 的细粒度权限码缓存

### 但当前边界也要写清

这套 RBAC 目前主要作用在它自己的管理窗口和 Solution 侧本地权限子系统。

从当前搜索结果看，`HasPermissionAsync` 和 `PermissionChecker` 的细粒度调用几乎都还留在 `Rbac/` 子目录中；与此同时，很多窗口入口仍先依赖全局的 `Authorization.Instance.PermissionMode` 做粗粒度判断。

所以更准确的描述是：

- `ColorVision.Solution` 内含一个本地 RBAC 子模块
- 它和全局 `PermissionMode` 并存
- 不能把整个解决方案树、所有编辑器和全部文件操作都描述成已经全面接入细粒度权限码控制

## 作为 DLL 使用时

### 应该引用它的场景

- 需要 `.cvsln` 工作区、文件树、最近文件和工作区状态栏。
- 需要可插拔文件编辑器、文件夹编辑器或通用编辑器。
- 需要 AvalonDock 文档区、布局保存恢复和面板 Provider。
- 需要内置终端、Markdown 预览、多图预览或本地 RBAC 管理窗口。

### 新增文件编辑器

1. 实现 `IEditor`，通常继承 `EditorBase`。
2. 按场景添加 `EditorForExtensionAttribute`、`GenericEditorAttribute` 或 `FolderEditorAttribute`。
3. 确认 `EditorManager` 能扫描到类型。
4. 打开对应文件或文件夹，验证编辑器选择、默认编辑器和文档区激活。

### 新增项目或文件模板

1. 新项目模板实现 `IProjectTemplate` 并添加 `ProjectTemplateAttribute`。
2. 新文件模板实现 `INewItemTemplate` 并添加 `NewItemTemplateAttribute`。
3. 通过 `AddNewProjectWindow` 或 `AddNewItemWindow` 验证 UI 是否出现。

### 发布注意

`Solution` 依赖 `ImageEditor`、`UI.Desktop`、AvalonDock、AvalonEdit、WebView2 和 WPFHexaEditor。发布后要验证 `.cvsln` 打开、文件树、文本编辑器、图像编辑器、终端和布局恢复。

### DLL 发布验收表

| 验收项 | 要查什么 | 通过标准 |
| --- | --- | --- |
| 目标框架产物 | `net10.0-windows7.0` | 能生成 DLL、`.nupkg`、`.snupkg` |
| 包内 README | `PackageReadmeFile`、包根目录 | `README.md` 随包进入根目录 |
| 项目依赖 | `ColorVision.Database`、`ColorVision.ImageEditor`、`ColorVision.UI.Desktop`、`ColorVision.UI` | 工作区、数据库、图像编辑和桌面工具依赖能解析 |
| 三方依赖 | `AvalonEdit`、`AvalonDock`、`WebView2`、`WPFHexaEditor`、`Markdig.Signed` | 文本编辑、停靠布局、Markdown/Web、Hex 查看都能加载 |
| 解决方案入口 | `SolutionManager`、`OpenSolutionWindow` | `.cvsln`、文件夹、最近文件打开路径正常 |
| 编辑器注册 | `EditorManager`、`EditorForExtensionAttribute`、`FolderEditorAttribute` | 文本、图像、Web、Hex、文件夹编辑器可被扫描和选择 |
| 工作区布局 | `WorkspaceManager`、`DockLayoutManager` | 标签页、面板布局保存、加载、重置正常 |
| 终端和资源释放 | `TerminalControl`、`TerminalService` | 打开/关闭终端不残留 shell，退出时能释放计时器和进程 |
| 本地 RBAC | `RbacManager`、`RbacManagerConfig` | 登录、退出、用户/角色/权限管理窗口能打开，登录态边界清楚 |

### 现场故障首查

| 现象 | 第一检查点 |
| --- | --- |
| `.cvsln` 打不开或最近文件失效 | 检查 `SolutionManager` 的路径归一化、文件是否存在、目录权限 |
| 文件双击没有合适编辑器 | 检查 `EditorManager` 是否扫描到对应 Attribute，默认编辑器配置是否指向旧类型 |
| 布局恢复后标签页丢失 | 先查 `DockLayoutManager` 的 layout 文件和 `ContentId` 是否稳定 |
| 终端打开空白或无法输入 | 检查 ConPTY 初始化、shell 路径、当前解决方案目录和 `TerminalControl.Dispose()` |
| Markdown/WebView2 空白 | 检查 WebView2 Runtime、用户数据目录权限和 `WebEditor` 初始化 |
| 多图浏览器卡顿或缩略图异常 | 检查目录图片数量、扩展名过滤、缩略图缓存和释放流程 |
| RBAC 登录态异常 | 区分 Solution 本地 RBAC 与全局 `PermissionMode`，先查 `RbacManagerConfig` 和本地 SQLite |

## 当前更适合怎样读这个项目

### 想看解决方案入口

先看：

- `SolutionManager.cs`
- `SolutionManagerInitializer.cs`
- `OpenSolutionWindow.xaml(.cs)`

### 想看树和文件节点

先看：

- `Explorer/SolutionExplorer.cs`
- `Explorer/SolutionNodeFactory.cs`
- `TreeViewControl.xaml(.cs)`

### 想看文件怎么被不同编辑器打开

先看：

- `Editor/EditorManager.cs`
- `Editor/EditorForExtensionAttribute.cs`
- `Editor/*.cs`

### 想看工作区布局和文档宿主

先看：

- `Workspace/WorkspaceManager.cs`
- `Workspace/DockLayoutManager.cs`
- `Workspace/LayoutMenuItems.cs`

### 想看本地权限子系统

先看：

- `Rbac/RbacManager.cs`
- `Rbac/Services/`
- `Rbac/Entity/`

## 这页不再做什么

本页不再继续维护这些高风险内容：

- 过时版本号和目标框架清单
- 假定存在完整公共 API 的大段伪代码
- 把 `RbacManager` 写成全项目统一权限入口
- 把所有文件操作都写成已经被细粒度权限完全拦截

如果要补具体类或方法，应在对应子模块页里单独展开，而不是在这里继续堆一整页伪 API。

## 继续阅读

- [UI组件概览](./README.md)
- [安全与权限控制](../../03-architecture/security/overview.md)
- [RBAC 模块](../../03-architecture/security/rbac.md)
