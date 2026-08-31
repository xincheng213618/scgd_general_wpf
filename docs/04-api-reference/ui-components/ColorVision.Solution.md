---
knowledge_id: "ui.solution"
knowledge_type: "topic"
status: "current"
summary: "工作区与普通文件的打开分流、单工作区切换和取消、私有cvsln与共享配置恢复；打开和加载不保证无写入。"
aliases: ["ColorVision.Solution", "SolutionManager", "ResourceOpenService", "OpenSolutionAsync", "OpenWith", "OpenManyAsync", "PrivateWorkspaceService", "SolutionConfigStore", "SolutionConfigStore.Load", "SolutionCache", "MruPathService", "MruPathService.Touch", "cvsln", "cvproj", "打开文件夹工作区", "取消切换工作区", "批量打开图片和项目", "工作区文件树缓存", "解决方案备份恢复", "默认打开方式保存失败"]
code_paths: ["UI/ColorVision.Solution/README.md", "UI/ColorVision.Solution/ColorVision.Solution.csproj", "UI/ColorVision.Solution/SolutionManager.cs", "UI/ColorVision.Solution/SolutionManagerInitializer.cs", "UI/ColorVision.Solution/StartupResourceOpenInitializer.cs", "UI/ColorVision.Solution/Editor/ResourceOpenService.cs", "UI/ColorVision.Solution/Editor/ResourcePathIdentityComparer.cs", "UI/ColorVision.Solution/Editor/CommandLineResourceOpenRequest.cs", "UI/ColorVision.Solution/Workspace/PrivateWorkspaceService.cs", "UI/ColorVision.Solution/Explorer/SolutionExplorer.cs", "UI/ColorVision.Solution/Explorer/SolutionExplorer.Persistence.cs", "UI/ColorVision.Solution/Explorer/SolutionCache.cs", "UI/ColorVision.Solution/Explorer/SolutionOperationHistory.cs", "UI/ColorVision.Solution/Explorer/ProjectTemplate.cs", "UI/ColorVision.Solution/Explorer/NewItemTemplate.cs", "UI/ColorVision.Solution/Explorer/SolutionConfigStore.cs", "UI/ColorVision.Solution/Explorer/SolutionWorkspaceStateStore.cs", "UI/ColorVision.Solution/Explorer/ProjectProviderRegistry.cs", "UI/ColorVision.Solution/Explorer/FolderProjectProvider.cs", "UI/ColorVision.Solution/Explorer/MsBuildProjectProvider.cs", "UI/ColorVision.Solution/SolutionFeatureVisibility.cs", "UI/ColorVision.Solution/Mru/MruPathService.cs", "UI/ColorVision.Solution/Mru/JsonMruPathStore.cs", "UI/ColorVision.UI/FileProcessorFactory.cs"]
test_paths: ["Test/ColorVision.UI.Tests/MruPathServiceTests.cs"]
related: ["ui.index", "ui.documents", "operations.terminal", "operations.first-run", "operations.main-window", "ui.configuration"]
---

# 资源打开与单工作区切换

`UI/ColorVision.Solution/` 是 ColorVision 的工作区壳层，不是 Visual Studio 解决方案加载器、算法运行时或文件访问权限沙箱。`ResourceOpenService` 区分工作区激活与文件编辑，`SolutionManager` 维护一个活动工作区，Explorer 负责项目树与配置；**编辑器能读某个文件，不等于它能作为工作区或项目加载**。

本页负责打开路由、工作区身份、切换和持久化。编辑器选择、文档身份、保存/关闭和停靠布局统一见[编辑器与文档生命周期](./editor-document-lifecycle.md)；脚本、ConPTY 与进程完成见[终端契约](../../01-user-guide/interface/terminal.md)，不按“用户/开发者”再复制这些知识。

## 打开入口的区别

主窗口的可配置“打开文件”默认 Ctrl+O，`CommandInitializer` 的 `ApplicationCommands.Open` 也接到同一文件选择入口；“打开文件夹工作区”默认 Ctrl+Shift+O。工作区列表改用无内建键位的 `SolutionWorkspaceCommands.OpenWorkspace`，在快捷键页显示“未分配”，不再占 Ctrl+O。键位与取消/恢复机制见[快捷键契约](./hotkeys.md)。

| 入口 | 实际分流 | 不能混同的结果 |
| --- | --- | --- |
| `ResourceOpenService.Open` | 同步入口仅接受普通文件；先 `FileProcessorFactory.TryOpenFileAction`，未被处理才交给 EditorManager | 文件夹、项目、`.cvsln` 返回失败并要求异步入口；不是同步切换工作区 |
| `OpenAsync` | 工作区资源交给 `SolutionManager.OpenSolutionAsync`；普通文件仍先文件 action 再编辑器 | action 已处理但失败/取消时不会继续尝试其它编辑器；也不保证编辑器的异步内容已载入 |
| `OpenInEditor` | 仅普通文件，直接用配置的工作区编辑器，跳过独立文件 action | 例如 CVRAW/CVCIE 可进入工作区，而不是走独立预览 |
| `OpenWith(path, editorId)` | 明确选择文件或文件夹编辑器，绕过工作区激活和文件 action | 用文本编辑器打开 `.cvsln` / `.csproj` 不会切换工作区 |
| `OpenMany` / `OpenManyAsync` | 去重后逐项处理；多项时将非普通文件记为失败，继续处理其它项 | 不是整批预检拒绝；部分成功不回滚；同步版本即使只剩一个工作区资源也不能异步激活它 |
| `OpenManyFromWorkspace` | CVRAW/CVCIE 用 `OpenInEditor`，其它资源沿用普通同步打开 | 与命令行或 Shell 的默认打开路线不同 |

`Classify` 先判断目录，再检查文件存在，随后判断 `.cvsln` 和 Provider 声明的项目模式，其余归为普通文件。未知项目格式可以出现在文本/Hex 编辑器中，不等于有项目 Provider 支持。内置 `.sln` 导入和 VC++ 项目 Provider 当前不存在。

`OpenWith(..., setAsDefault: true)` 先打开，成功后才保存默认选择；因此可以返回 `Succeeded=true`、`DefaultEditorUpdated=false` 并带错误信息。不能把默认方式保存失败描述成“资源没打开”或自动关闭已经打开的文档。反过来，`DefaultEditorUpdated=true` 也不是磁盘提交证明：当前默认项保存调用的 void 配置包装会丢弃部分失败结果。具体内存、异常回退和落盘缺口见文档生命周期主题。

批量路径去掉空字符串，按绝对路径、去掉结尾目录分隔符、不区分大小写去重，保留第一个传入字符串用于实际打开和报告。这不是解析链接、硬链接或文件系统对象的真实身份。`IsComplete` 要求非零请求且全部成功；空集合不是全部成功。批量取消在项目前检查，可能抛出取消异常并留下之前已打开项，也可能在末项得到取消失败结果；没有批量事务。

`CanOpenTogether` 对多项混入工作区的组合返回 false，但 `OpenMany*` 自身不以此整批拒绝：图片与项目混入同一批次时，项目项记录限制错误，图片仍会尝试打开，已经打开的文件不会撤销。调用方若用 `CanOpenTogether` 禁用入口，则可能根本不调用打开方法；命令行的“先工作区、后文件”又是下述另一条执行链，不能只凭资源列表推断执行结果。

## 初次访问不是只读动作

`SolutionManager` 构造时接入文件路由、加载最近工作区，并在存在 `Application.Current` 时向 Dispatcher 安排 `RestoreInitialWorkspaceAsync`，保存为 `InitialWorkspaceOpenTask`。它优先用 `solutionpath`，否则尝试最近一次使用的路径；失败且没有当前/正在打开的工作区时，在用户 Documents 下建立 `ColorVision/Default/Default.cvsln` 并打开。取消恢复则直接返回，不进入该默认创建分支。

`SolutionManagerInitializer` 的 Order=1 只安排取得单例，不等待所有工作区准备完成。`StartupResourceOpenInitializer` 在主窗口就绪且存在待打开文件时等待 `InitialWorkspaceOpenTask`，然后打开文件。不能因为取得单例或初始化器返回就断言工作区和文件内容均已就绪；只读代码问答不需要启动这些入口。

`CommandLineResourceOpenRequest` 优先选显式工作区；未指定时从资源列表找首个可激活工作区，并从普通资源列表移除同一身份路径。`TryOpenCommandLineWithFeedbackAsync` 会先打开工作区，失败/取消就不继续打开资源；这是顺序控制，不是失败时撤销已发生的文件操作。

## 切换顺序与取消边界

`OpenSolutionAsync` 先检查是否已是当前路径：相同则取消其它待打开请求并返回成功，**不重新加载当前工作区**。不同路径的请求建立关联 token 和递增版本，取消上一请求；新请求获胜由 token 与版本共同决定，而不是谁先读完磁盘。

正常路径为：解析目标并准备配置/项目引用 → 建候选 Explorer → 核对当前请求 → 请求关闭旧工作区关联文档 → 再核对请求 → Dispose 旧 Explorer → 替换环境、路径与当前 Explorer → 更新 MRU → 触发 `SolutionLoaded`。候选 Explorer 构造还会初始化 SQLite 树缓存、文件监控并可能后台重建缓存，不只是创建内存对象；缓存初始化失败会降级到文件系统加载。

- 解析、准备失败或在替换前取消，通常保留旧 Explorer；文档拒绝关闭时释放候选并返回 `Canceled=true`。
- “保留旧工作区”不等于磁盘无变化。准备文件夹/项目会生成私有 `.cvsln`；读取损坏共享配置可能自动从备份修复。后台准备使用 `Task.Run(..., CancellationToken.None)` 加 `WaitAsync(token)`，取消等待不能回滚或强制停止已经开始的文件读写。
- 关闭文档的确认/保存也有独立副作用，不能承诺后续取消会撤销已保存文件。具体顺序见文档生命周期主题。
- 提交后通知不是事务。`CurrentWorkspaceChanged`、MRU 的 Changed 和 `SolutionLoaded` 使用同步事件调用；订阅者抛异常可能发生在新工作区已经安装后。部分异常被转换为失败，其它异常可继续向上传播，没有恢复旧 Explorer 的统一补偿。

`TryCloseSolution` 先取消待打开请求，再请求关闭当前工作区关联文档；拒绝关闭保留当前 Explorer。成功则 Dispose Explorer、清活动路径与环境并发出 `SolutionClosed`。关联范围来自根目录、配置文件、树中项目目录/项目文件、解决方案项及不可用项目的已解析路径，不简单等于“关闭所有标签页”。外部项目引用也可能属于这个范围。

`CreateSolutionAsync` 先建目录、写 `<目录名>.cvsln` 并发出 `SolutionCreated`，之后才尝试打开；没有“仅当目标不存在”门禁。打开被取消/失败，不撤销已创建或替换的配置。不要用创建或打开动作代替无副作用验证。

## 共享配置与机器私有状态

| 资料 | 位置和身份 | 当前责任 |
| --- | --- | --- |
| 用户显式 `.cvsln` | 用户选中的文件；RootPath 可与配置文件目录不同 | 根路径、项目引用、启动项目、配置/平台、虚拟文件夹和解决方案项 |
| 文件树 SQLite 缓存 | 当前 `.cvsln` 文件路径加 `.cache.db` | `SolutionCache` 的目录项缓存；显式共享工作区可在其源目录旁生成，并非一律位于用户私有状态目录 |
| 文件夹私有 `.cvsln` | `%LOCALAPPDATA%/ColorVision/FolderWorkspaces/<key>.cvsln` | 文件夹激活所需配置，不在源目录新建 `.ColorVision.cvsln` |
| 项目私有 `.cvsln` | `%LOCALAPPDATA%/ColorVision/ImplicitSolutions/<key>.cvsln` | 直接打开项目时建立 Explicit 配置，登记项目并设为 StartupProject |
| 树呈现状态 | `%LOCALAPPDATA%/ColorVision/SolutionState/<key>.json` | 展开节点、选中项、anchor；不是项目定义或文档内容 |
| 最近工作区 | `Environments.DirLocalAppData/Solution/recent-workspaces.json` | 最近使用时间与固定项，不是配置备份或当前打开成功的证明 |
| 停靠布局与编辑器默认项 | 各自独立存储 | 见文档生命周期与配置主题，不混入 `.cvsln` |

私有工作区 key 来自规范化源路径转大写后的 MD5。`WorkspaceSourceKind` / `WorkspaceSourcePath` 保存来源；解析回原路径还要求私有目录、类型、源资源存在和期望文件名同时匹配，不能凭同名字段任意重定向。旧源目录的 `.ColorVision.cvsln` 可作为兼容输入读取，失败则记录日志并用新配置建立私有文件；不是删除旧文件或把新状态继续写回源目录。

“打开文件夹不新建源目录配置”不能扩大成任何打开都只读：显式 `.cvsln` 可能被恢复、配置编辑会保存；项目文件 Provider 也有独立契约。工作区 RootPath、项目引用和解决方案项不是权限白名单。

### cvsln 版本、保存与恢复

`SolutionConfigStore.CurrentSchemaVersion` 当前为 4，按 v0→v1 项目模式/配置、v1→v2 虚拟文件夹、v2→v3 解决方案项、v3→v4 平台逐步迁移。正常 Load 在内存迁移/规范化，不因旧版本本身立即落盘；后续 Save 才写当前结构。Explorer 还注册 ProcessExit 保存，退出时可能写入；Dispose 会解除该订阅，不能把正常 Load 的局部行为扩大成工作区整个生命周期只读。未来版本抛 `NotSupportedException`，不当作损坏文件自动回退旧备份。

Save 先规范化传入配置，再写同目录临时文件、Flush 到磁盘；已有主文件使用 `File.Replace` 留 `.bak`，平台不支持 Replace 时回退复制备份再覆盖移动。它是单个配置文件的提交，不是配置、项目文件、文档和内存的跨资源事务。

Load 遇到受支持的读取/JSON/数据异常会尝试 `.bak`；备份解析后先尽力复制损坏主文件到 `.corrupt-*`，再把恢复内容写回主文件，只有解析和恢复写回均成功才返回 `RecoveredFromBackup=true`。损坏副本复制失败不会阻止修复，不能承诺总保留 `.corrupt-*`。备份读取/解析或恢复写回失败都会抛错，不自动创建空工作区；写回的权限或 I/O 错误也可能被包装成“主配置及备份都无法读取”，应检查内部 `AggregateException`，不能据外层文字判断备份损坏。排障前先保存证据，调用 Load 本身可能覆盖主文件。

### MRU 与树状态

`MruPathService` 默认容量50，按规范化路径去重；固定项全部保留，非固定项填剩余容量，所以固定项过多时总数可以超过50。`Touch` 可同时去掉路径别名。更新先改内存，`IsStorageException` 识别的持久化异常只记日志，之后仍通知 Changed；订阅者正常返回时最终返回 true，它不是“已成功写入磁盘”的返回值。未捕获的存储异常或 Changed 订阅者异常仍会抛出，已更新的内存不回滚。移除历史/清空列表不删除工作区或项目文件。

`JsonMruPathStore` 用临时文件加覆盖移动保存，没有 `.bak` 恢复链；未知版本/坏 JSON 返回空列表。`SolutionWorkspaceStateStore` 则按 `.cvsln` 绝对路径的 SHA-256 命名状态文件，版本不支持或读取失败也返回未持久化的空状态。这些静默回退不能证明用户从未配置过状态，也不能替代共享配置的失败诊断。

## 项目 Provider 与扩展归属

`ProjectProviderRegistry` 扫描 `[ProjectProvider]` 的 `IProjectProvider`，并监听新程序集加载。注册按不区分大小写的 Provider Id 替换，优先级降序选择；声明文件模式与实际 `CanLoad` / `Load` 是不同门槛，插件未装或类型不匹配会产生可诊断的不可用项目，不应偷偷当普通文件夹使用。

- `FolderProjectProvider` 负责 `.cvproj` JSON，兼容旧文件缺失的 ProjectType，并支持自己的项目项/依赖修改和命令定义。
- `MsBuildProjectProvider` 只读接入 `.csproj`、`.fsproj`、`.vbproj` 的 XML 字面信息，不执行完整 MSBuild import/条件求值，也不提供改写项目文件接口。只读适配器仍可提供生成/运行命令，不能把“只读解析”理解为整个模块绝不会启动进程。
- `SolutionFeatureVisibility.ShowBuildAndDebugUI` 当前固定 false，隐藏生成/运行/调试 UI；底层能力和命令仍存在，这不是执行权限沙箱或删除了相关实现。命令提交、退出与业务成功按终端主题分别核实。
- 新项目/文件模板分别由 `Explorer/ProjectTemplate.cs` 的 `IProjectTemplate` / `ProjectTemplateAttribute`、`Explorer/NewItemTemplate.cs` 的 `INewItemTemplate` / `NewItemTemplateAttribute` 扩展；不是在资源打开路由里添加业务 switch。菜单、项目 Provider 和编辑器是三种不同扩展点。

物理文件操作位于 `SolutionResourceCommands`、`SolutionPhysicalItemOperations`、`SolutionClipboardFileOperations`、`SolutionBatchDeleteService`；虚拟组织和配置操作位于 Explorer 对应分部。`SolutionOperationHistory` 是有界配置快照历史，不能把存在 Undo/Redo 理解成任意磁盘删除、外部命令或项目文件修改都有撤销。操作真实文件前应沿具体入口核对授权和补偿，不从树的视觉变化推断磁盘状态。

## 验证入口与缺口

`Test/ColorVision.UI.Tests/MruPathServiceTests.cs` 覆盖大小写去重、别名移除、固定项顺序/容量、一次通知和 JSON 往返/坏 JSON 回退。它没有覆盖工作区异步竞争、取消后的文件写入、共享配置备份恢复、Provider 命令或事件失败后状态；测试文件被引用不代表本次已经运行。

当前本页不登记不存在的 SolutionManager/ResourceOpenService 专门测试；相关模块在其它测试中被隔离或反射替换，不证明其真实启动恢复通过。修改这些边界时应补合成工作区与隔离状态目录测试，先验证身份、文件变化和返回值，再按授权做 WPF/项目命令集成。仅研究代码不需要初始化管理器、创建默认工作区、恢复 `.bak` 或执行项目命令。
