---
knowledge_id: "ui.solution"
knowledge_type: "topic"
status: "current"
summary: "工作区创建、打开与最近列表，文件树搜索和引用移除，切换取消及cvsln恢复；同名创建可能覆盖配置，取消切换不回滚全部文件变化。"
aliases: ["ColorVision.Solution", "SolutionManager", "ResourceOpenService", "OpenSolutionAsync", "OpenWith", "OpenManyAsync", "PrivateWorkspaceService", "SolutionConfigStore", "SolutionConfigStore.Load", "SolutionCache", "MruPathService", "MruPathService.Touch", "cvsln", "cvproj", "打开文件夹工作区", "取消切换工作区", "批量打开图片和项目", "工作区文件树缓存", "解决方案备份恢复", "默认打开方式保存失败", "解决方案资源管理器", "文件系统视图", "与活动文档同步", "全部折叠", "TreeViewControl", "OpenSolutionWindow", "NewCreateWindow", "CreateSolutionAsync", "SolutionSetting", "DefaultCreatName", "SolutionConfig", "SolutionSearchService", "SolutionOperationHistory", "ProjectProviderRegistry", "创建工程", "最近工作区列表", "从解决方案中移除", "移除解决方案文件夹"]
code_paths: ["UI/ColorVision.Solution/README.md", "UI/ColorVision.Solution/ColorVision.Solution.csproj", "UI/ColorVision.Solution/SolutionManager.cs", "UI/ColorVision.Solution/SolutionManagerInitializer.cs", "UI/ColorVision.Solution/StartupResourceOpenInitializer.cs", "ColorVision/ForwardedCommandLineHandler.cs", "ColorVision/MainWindow.Setting.cs", "UI/ColorVision.Solution/OpenSolutionWindow.xaml", "UI/ColorVision.Solution/OpenSolutionWindow.xaml.cs", "UI/ColorVision.Solution/NewCreatWindow.xaml", "UI/ColorVision.Solution/NewCreatWindow.xaml.cs", "UI/ColorVision.Solution/SolutionSetting.cs", "UI/ColorVision.Solution/SolutionMenuItems.cs", "UI/ColorVision.Solution/CommandInitializer.cs", "UI/ColorVision.Solution/Editor/ResourceOpenService.cs", "UI/ColorVision.Solution/Editor/ResourcePathIdentityComparer.cs", "UI/ColorVision.Solution/Editor/CommandLineResourceOpenRequest.cs", "UI/ColorVision.Solution/Workspace/PrivateWorkspaceService.cs", "UI/ColorVision.Solution/Explorer", "UI/ColorVision.Solution/Mru", "UI/ColorVision.Solution/TreeViewControl.xaml", "UI/ColorVision.Solution/TreeViewControl.xaml.cs", "UI/ColorVision.Solution/TreeViewControl.Navigation.cs", "UI/ColorVision.Solution/TreeViewControl.ViewMode.cs", "UI/ColorVision.Solution/TreeViewControl.Search.cs", "UI/ColorVision.Solution/TreeViewControl.Command.cs", "UI/ColorVision.Solution/TreeViewControl.WorkspaceState.cs", "UI/ColorVision.Solution/SolutionFeatureVisibility.cs", "UI/ColorVision.UI/FileProcessorFactory.cs"]
test_paths: ["Test/ColorVision.UI.Tests/MruPathServiceTests.cs", "Test/ColorVision.UI.Tests/SolutionExplorerPresentationTests.cs", "Test/ColorVision.UI.Tests/SolutionFileSystemViewTests.cs"]
related: ["ui.index", "ui.documents", "operations.terminal", "operations.first-run", "operations.main-window", "ui.configuration", "ui.hotkeys"]
---

# 工作区创建、资源打开与文件树管理

`UI/ColorVision.Solution/` 是 ColorVision 的工作区壳层，不是 Visual Studio 解决方案加载器、算法运行时或文件访问权限沙箱。`ResourceOpenService` 区分工作区激活与文件编辑，`SolutionManager` 维护一个活动工作区，Explorer 负责项目树与配置；**编辑器能读某个文件，不等于它能作为工作区或项目加载**。

本页说明工作区的创建、打开、树视图、项目组织与持久化。编辑器选择、文档身份、保存/关闭和停靠布局见[编辑器与文档生命周期](./editor-document-lifecycle.md)；脚本和终端执行见[终端契约](../../01-user-guide/interface/terminal.md)。

## 打开或创建工作区

### 选择打开入口

| 需要打开的内容 | 界面入口 | 结果 |
| --- | --- | --- |
| 图片、文本等普通文件 | “打开文件”，默认 `Ctrl+O` | 进入文件处理器或编辑器，不必切换工作区 |
| 已有文件夹 | 文件 → 打开 → 打开文件夹，默认 `Ctrl+Shift+O` | 以该目录为活动工作区，使用用户目录中的私有 `.cvsln` |
| `.cvsln` 或已支持的项目文件 | 文件 → 打开 → 项目/解决方案，默认 `Ctrl+Alt+O`；在列表窗口点击“打开项目” | `.cvsln` 加载已有配置；项目文件准备私有工作区后激活 |
| 最近使用的工作区 | 同一列表窗口中双击记录，或选中后按 `Enter` | 尝试打开记录指向的文件夹、项目或解决方案 |

快捷键可由用户调整，默认值和已有自定义绑定的规则见[快捷键](./hotkeys.md)。工作区列表还提供“打开文件夹”和“创建工程”；其中“打开项目”的选择器只接受 `.cvsln` 与已注册 Provider 的项目模式，不能按副标题把它当作任意文件选择器。内置项目格式为 `.cvproj`、`.csproj`、`.fsproj`、`.vbproj`；后者的解析范围见项目 Provider 小节。

一次只激活一个工作区。选择后窗口显示“正在打开工作区…”并暂时禁用选择区域，点击“取消”或关闭该窗口可取消等待。切换需要关闭旧工作区关联文档，遇到未保存内容按文档提示选择；拒绝关闭会保留旧工作区。取消与准备阶段写入的关系见下文。

### 创建工程

“创建工程”先建立一个含 `.cvsln` 的工作区，配置采用 `ProjectMode=Explicit`，不会直接替你生成某种业务项目。

1. 在工作区列表点击“创建工程”，填写“项目名称”和“选择项目保存位置”。结果目录为 `<保存位置>/<项目名称>`。
2. 使用准备新建的目录名。父目录不存在时，界面询问是否创建父目录。
3. 点击“创建工程”。窗口关闭后，`SolutionManager.CreateSolutionAsync` 写入 `<项目名称>.cvsln`，再尝试切换。需要项目或文件时，在资源树支持的容器节点右键“添加”，选择“新建项目”“现有项目”“新建项”或“现有项”。

**同名目录提示不是取消创建的完整保护。** 目标目录已存在时，选择清空会直接递归删除该目录；选择“否”（不清空）仍会继续，之后可能替换同名 `.cvsln`。清空异常被提示后也没有终止后续创建。这是当前创建入口的覆盖风险，已有工作区应通过打开入口访问。创建后切换失败或取消，不会撤销已经生成、替换或删除的文件。

没有可用创建位置记录时，打开新建窗口就会准备 Documents 下的 `ColorVision` 目录并记录位置。默认名称由 `SolutionSetting.DefaultCreatName` 提供，可在资源树“更多选项 → 解决方案设置”中修改；它只影响新建建议名，不重命名已有工作区。

### 管理最近列表

列表搜索按名称、完整路径和显示的最后使用时间匹配，空格分隔的关键词需全部命中，不区分大小写。右键可复制路径、固定/取消固定或“从最近列表中移除”；“清空最近列表”有确认并清除全部记录，包括固定项。这些动作不删除磁盘上的工作区。显示“不可用”表示当前路径不能识别为可用工作区，移除记录不修复缺失文件。

## 资源管理器视图与导航

`TreeViewControl` 在同一个活动工作区内提供“解决方案”和“文件系统”两个视图，切换不会重新调用工作区打开入口：

- **解决方案**保留 Provider 项目模型、显式项目引用、虚拟文件夹和解决方案项。
- **文件系统**以工作区根目录为范围，按需加载真实目录和文件；显示实际项目文件、配置文件和隐藏项，不套用项目包含/排除或虚拟组织规则。它不代表项目引用中的外部目录。根节点不能重命名、删除或剪切，子节点沿用物理文件操作。该视图不建立第二个 Explorer、缓存或监控器；需要更新目录内容时使用“刷新”。
- 搜索按当前视图的节点名称、完整路径和显示路径匹配，不读取文件正文。支持空白分隔的多个关键词及双引号短语，所有关键词需命中，最多显示 500 个结果；清除搜索回到当前视图，“在解决方案资源管理器中定位”在该视图的真实节点中展开定位。搜索异步结果和定位请求在视图/工作区切换时取消，避免旧结果替换新树。
- “与活动文档同步”根据文档服务保存的文件路径定位，兼容旧编辑器以路径作为 ContentId 的方式；不把文档的散列 ContentId 当作磁盘路径。未打开文件文档或文档不属于当前视图范围时禁用入口。
- “全部折叠”清除搜索并折叠已加载的子节点，保留根节点展开；不为折叠而递归加载整个目录。“刷新”针对当前视图根节点。“属性”作用于单个选中节点，设置入口位于“更多选项”。

工具栏和树采用紧凑布局；选中行的中性灰圆角背景与左侧主题色标记固定在可见区域，横向滚动长文件名时仍保留标记。滚动条保留主题模板、拖动范围及自动显隐，由局部 `ExplorerScrollBarBehavior` 调整透明度，不修改全局主题资源。

解决方案树继续保存原有的展开/选中状态；文件系统视图的状态仅在当前控件生命周期保留，不覆盖解决方案状态文件。两种视图的功能边界不同，外观参考 VS 不意味着增加了 `.sln` 导入、Git 状态或构建/调试支持。

## 打开入口的区别

`CommandInitializer` 将 `ApplicationCommands.Open` 接到普通文件选择器，将 `SolutionWorkspaceCommands.OpenWorkspace` 接到工作区列表；可配置默认键由各入口的 `IHotKey` 声明。资源树自己的 `SolutionResourceCommands.Open` 用 `Enter` 打开选中项，避免把树内打开混同于全局文件选择器。

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

## 启动恢复与命令行打开

`SolutionManager` 构造时接入文件路由、加载最近工作区，并在存在 `Application.Current` 时向 Dispatcher 安排 `RestoreInitialWorkspaceAsync`，保存为 `InitialWorkspaceOpenTask`。它优先用 `solutionpath`，否则尝试最近一次使用的路径；失败且没有当前/正在打开的工作区时，在用户 Documents 下建立 `ColorVision/Default/Default.cvsln` 并打开。取消恢复则直接返回，不进入该默认创建分支。

`CommandLineResourceOpenRequest` 优先选显式工作区；未指定时从资源列表找首个可激活工作区，并从普通资源列表移除同一身份路径。启动与转发使用同一个请求模型，但执行顺序不同：

- **应用启动**：`SolutionManagerInitializer` 的 Order=1 只安排取得单例，不等待工作区准备完成。`StartupResourceOpenInitializer` 在主窗口就绪且存在待打开文件时等待 `InitialWorkspaceOpenTask`；任务正常结束后继续打开文件，不以初始工作区是否成功激活为门槛，也不再次打开请求中的 WorkspacePath。
- **转发到已运行的主窗口**：`ForwardedCommandLineHandler` 调用 `TryOpenCommandLineWithFeedbackAsync`，先打开请求中的工作区，失败/取消就不继续打开文件。这是顺序控制，不会撤销已发生的文件操作。

取得 `SolutionManager` 单例、初始化器返回或转发消息被接收，都不能单独证明工作区与文件内容已经加载完成。

## 切换顺序与取消边界

`OpenSolutionAsync` 先检查是否已是当前路径：相同则取消其它待打开请求并返回成功，**不重新加载当前工作区**。不同路径的请求建立关联 token 和递增版本，取消上一请求；新请求获胜由 token 与版本共同决定，而不是谁先读完磁盘。

正常路径为：解析目标并准备配置/项目引用 → 建候选 Explorer → 核对当前请求 → 请求关闭旧工作区关联文档 → 再核对请求 → Dispose 旧 Explorer → 替换环境、路径与当前 Explorer → 更新 MRU → 触发 `SolutionLoaded`。候选 Explorer 构造还会初始化 SQLite 树缓存、文件监控并可能后台重建缓存，不只是创建内存对象；缓存初始化失败会降级到文件系统加载。

- 解析、准备失败或在替换前取消，通常保留旧 Explorer；文档拒绝关闭时释放候选并返回 `Canceled=true`。
- “保留旧工作区”不等于磁盘无变化。准备文件夹/项目会生成私有 `.cvsln`；读取损坏共享配置可能自动从备份修复。后台准备使用 `Task.Run(..., CancellationToken.None)` 加 `WaitAsync(token)`，取消等待不能回滚或强制停止已经开始的文件读写。
- 关闭文档的确认/保存也有独立副作用，不能承诺后续取消会撤销已保存文件。具体顺序见文档生命周期主题。
- 提交后通知不是事务。`CurrentWorkspaceChanged`、MRU 的 Changed 和 `SolutionLoaded` 使用同步事件调用；订阅者抛异常可能发生在新工作区已经安装后。部分异常被转换为失败，其它异常可继续向上传播，没有恢复旧 Explorer 的统一补偿。

`TryCloseSolution` 先取消待打开请求，再请求关闭当前工作区关联文档；拒绝关闭保留当前 Explorer。成功则 Dispose Explorer、清活动路径与环境并发出 `SolutionClosed`。关联范围来自根目录、配置文件、树中项目目录/项目文件、解决方案项及不可用项目的已解析路径，不简单等于“关闭所有标签页”。外部项目引用也可能属于这个范围。

`CreateSolutionAsync` 先建目录、写 `<目录名>.cvsln` 并发出 `SolutionCreated`，之后才尝试打开；没有“仅当目标不存在”门禁。打开被取消/失败，不撤销已创建或替换的配置。

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

## 移除引用、删除文件与撤销

资源树的操作取决于选中节点类型。单选时查看右键菜单的“删除”“从解决方案中移除”或“移除解决方案文件夹”，不要仅凭图标判断磁盘影响。

| 节点或操作 | 实际影响 |
| --- | --- |
| `Explicit` 模式的项目节点、解决方案项 | 从 `.cvsln` 移除引用，保留项目文件、目录和被引用文件 |
| 虚拟解决方案文件夹 | 移除虚拟分组，子文件夹、项目与解决方案项移到上一层，不删除物理目录 |
| 普通物理文件/目录、`AutoDiscover` 模式的可删除项目目录 | 调用 Windows 回收站删除流程，影响磁盘内容；文件系统视图中的项目文件也只是物理文件 |
| 多选删除 | 排除已选祖先之下的重复子项，确认后请求关闭相关文档，再按所属工作区处理；失败时可能已有部分项完成，没有整批回滚 |
| 撤销/重做 | `SolutionOperationHistory` 保存最多 100 项配置快照；只恢复被记录的 `.cvsln` 配置变化，不提供磁盘删除、外部命令或任意项目文件修改的恢复 |

文件系统视图根节点禁止重命名、删除和剪切，解决方案根节点也有自己的能力限制。物理操作入口集中在 `SolutionResourceCommands`、`SolutionPhysicalItemOperations`、`SolutionClipboardFileOperations` 与 `SolutionBatchDeleteService`；虚拟组织和配置变更由 Explorer 分部处理。恢复已删除文件应检查实际回收站或备份，不能把配置撤销当作文件恢复。

## 验证入口与缺口

`SolutionExplorerPresentationTests` 使用生产 XAML 和合成节点检查窄面板、主题切换、选中行与滚动条，并验证已加载节点定位、取消和折叠。`SolutionFileSystemViewTests` 使用隔离目录验证物理文件展示、根节点保护、刷新与搜索范围。这些测试不启动主窗口、不加载用户工作区；离屏渲染不能替代当前运行窗口的实际点击验收。

`Test/ColorVision.UI.Tests/MruPathServiceTests.cs` 覆盖大小写去重、别名移除、固定项顺序/容量、一次通知和 JSON 往返/坏 JSON 回退。它没有覆盖工作区异步竞争、取消后的文件写入、共享配置备份恢复、Provider 命令或事件失败后状态。

尚缺直接覆盖 SolutionManager/ResourceOpenService 真实切换竞争、创建同名目录覆盖、配置自动恢复及批量物理操作的专项测试。其它测试中的隔离或反射替换不验证这些流程。补充验证应使用合成工作区与独立状态目录，分别检查返回值、内存状态及实际文件变化；涉及文件删除或项目命令的集成应在受控环境执行。
