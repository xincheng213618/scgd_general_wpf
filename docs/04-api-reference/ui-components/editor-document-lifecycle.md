---
knowledge_id: "ui.documents"
knowledge_type: "topic"
status: "current"
summary: "编辑器注册与选择、按路径和编辑器区分文档、保存重载关闭及外部变更；停靠布局不恢复未注册文件标签，重置也不预审脏文档。"
aliases: ["EditorManager", "EditorDescriptor", "EditorDocumentService", "IEditorDocumentContent", "IReloadableEditorDocumentContent", "IResourcePathAwareDocumentContent", "DockLayoutManager", "WorkspaceManager", "TryCloseAllDocuments", "NotifyResourceRenamed", "ResetLayout", "DefaultEditorUpdated", "默认编辑器", "重复打开文件", "保存文档", "重新加载文件", "文件被外部修改", "重置窗口布局", "停靠布局恢复"]
code_paths: ["UI/ColorVision.Solution/Editor/EditorManager.cs", "UI/ColorVision.Solution/Editor/EditorDescriptor.cs", "UI/ColorVision.Solution/Editor/IEditor.cs", "UI/ColorVision.Solution/Editor/EditorForExtensionAttribute.cs", "UI/ColorVision.Solution/Editor/GenericEditorAttribute.cs", "UI/ColorVision.Solution/Editor/TextEditor.cs", "UI/ColorVision.Solution/Editor/ImageEditor.cs", "UI/ColorVision.Solution/Editor/SystemEditor.cs", "UI/ColorVision.Solution/Workspace/EditorDocumentService.cs", "UI/ColorVision.Solution/Workspace/IEditorDocumentContent.cs", "UI/ColorVision.Solution/Workspace/DockLayoutManager.cs", "UI/ColorVision.Solution/Workspace/WorkspaceManager.cs", "UI/ColorVision.Solution/Workspace/LayoutMenuItems.cs", "UI/ColorVision.Solution/CommandInitializer.cs", "ColorVision/MainWindow.xaml.cs", "UI/ColorVision.UI/ConfigHandler.cs", "UI/ColorVision.UI/Environments.cs"]
test_paths: ["Test/ColorVision.UI.Tests/DockContentRegistrationTests.cs"]
related: ["ui.solution", "ui.configuration", "ui.image-editor", "operations.terminal"]
---

# 编辑器选择、文档生命周期与停靠布局

`EditorManager` 选择并调用编辑器，`EditorDocumentService` 为接入它的内容提供文档身份和保存/关闭协议，`DockLayoutManager` 管理停靠内容注册与布局。三者不是同一个持久化层：打开返回、文档保存成功、布局保存成功有各自的完成边界。

本主题不定义文件与工作区的打开分流、批量打开或工作区切换，见[资源打开与单工作区切换](./ColorVision.Solution.md)。终端面板的进程、脚本及退出责任见[终端契约](../../01-user-guide/interface/terminal.md)；图像内容加载和保存见[图像编辑器](./ColorVision.ImageEditor.md)。

## 编辑器注册和默认选择

`EditorManager` 初始化时扫描可用程序集，并监听后续 `AssemblyLoad`。发现输入是具体 `IEditor` 类型上的 `EditorForExtensionAttribute`、`GenericEditorAttribute` 和 `FolderEditorAttribute`；运行期也可直接 `RegisterEditor(EditorDescriptor)`。只存在一个类、不带发现属性且未显式注册，不会自动成为候选。

注册以 `EditorDescriptor.Id` 为键，不区分大小写；相同 ID 的新注册会替换旧注册，包括旧的文件夹、通用和各扩展名关联。扩展名去空白、补点并转小写；非通用文件编辑器必须声明扩展名，文件夹编辑器不使用扩展名。未指定稳定 ID 的属性注册回退到类型全名。注册验证不等于构造成功：没有 `Factory` 时仍需在打开时通过 `Activator.CreateInstance` 创建实例。

各候选组内按 `IsDefault` 降序、`Priority` 降序、ID 不区分大小写排序。文件默认选择顺序是：

1. 当前扩展名的已配置 ID，或兼容旧配置中的类型全名；必须仍匹配专用或通用候选。
2. 专用候选中的默认项，否则第一项。
3. 通用候选中的默认项，否则第一项。

因此通用编辑器的高优先级不会越过已有专用编辑器，除非被显式配置为默认。文件夹用独立的 `DefaultFolderEditor`，依次选择已配置项、默认项、第一项。读取旧类型全名只是兼容查找，不会在读取时自动写回稳定 ID。

`IsVisibleInOpenWith` 只在 `GetFileEditorDescriptors` / `GetFolderEditorDescriptors` 的 `visibleOnly:true` 查询中过滤；默认解析和按 ID 打开不使用这个过滤。隐藏候选不等于禁止使用，也不等于清除已保存默认项。打开路由中的选择窗口和“始终使用”处理由 `ResourceOpenService` 负责。

`TrySetDefaultEditor` / `TrySetDefaultFolderEditor` 先修改 `EditorManagerConfig`，调用 `ConfigService.Instance.Save<EditorManagerConfig>()`，未抛错才更新管理器的默认项缓存。抛错时恢复该配置字段并返回失败，不回滚已经打开的资源。当前 `ConfigHandler.Save<T>()` 又会丢弃内部 `TrySave` 的布尔失败结果，所以某些磁盘保存失败仍可能让这里返回成功并留下内存默认项；不能用成功返回或 `DefaultEditorUpdated=true` 证明默认选择已经落盘。配置存储的失败语义见[配置持久化](./configuration.md)。管理器只首次加载配置到自身缓存，没有订阅配置重载通知；外部改配置后也不能推断此缓存已刷新。

选定描述符后，管理器只尝试这个编辑器。创建返回空、返回不符合声明类型的实例或 `Open` 抛错都会失败，不再尝试下一个候选。仅当 `CreateEditor` 已成功返回实例，且该实例实现 `IDisposable` 时，外层异常路径才会尝试释放；工厂返回错误类型在 `CreateEditor` 内即抛错，外层没有取得该实例，不会替它释放。`IEditor.Open(path)` 返回 `void`，管理器的成功只表示调用正常返回，不证明异步载入完成、产生了标签页或执行过保存。比如 `SystemEditor` 交给系统打开，不创建本服务管理的文档。

## 文档身份与内容接口

`EditorDocumentService.Open` 根据传入的 `editorType`，在当前资源种类与扩展名的候选中重新解析一个注册 ID，找不到则使用类型全名/类型名。它没有直接接收打开路由选中的描述符 ID；同一个类型注册多个 ID 时，不能据此保证每个 ID 都建立不同文档。

`ContentId` 是 `editorId + 换行 + NormalizeResourcePath(path)` 的 MD5。路径规范化只有 `Path.GetFullPath` 和移除末尾目录分隔符，不折叠大小写、不解析链接或比较文件系统对象。文档查找按 ContentId 精确匹配；这与打开路由中不区分大小写的批量路径去重不是相同身份规则。

同一 ContentId 已在当前布局树中时，只激活既有标签，不再调用内容工厂，也不重新加载磁盘。解析出的编辑器 ID 不同，可以为同一路径建立不同标签；路径文本的大小写差异也可能绕过此处去重。新标签加入 `WorkspaceManager.LayoutDocumentPane`，服务保存会话并挂接激活、Closing、Closed 事件。

普通 `IEditor` 不自动获得文档能力，必须由编辑器调用此服务。内容契约如下：

| 接入内容 | 服务提供的能力 | 内容自身仍负责的事情 |
| --- | --- | --- |
| 任意内容，经 `Open` 建立会话 | 身份、激活和可选关闭回调 | 实际载入及资源所有权 |
| `IEditorDocumentContent` | 读取 `IsDirty` / `CanSave`，响应 `DocumentStateChanged`，调用 `Save()` | 脏状态、保存内容与保存完成的定义 |
| 同时实现 `IReloadableEditorDocumentContent` | 文件存在时允许 Reload，并安排文件监视 | `ReloadFromDisk()` 如何替换内存状态 |
| 同时实现 `IResourcePathAwareDocumentContent` | 重命名时尝试保留内容并更新资源路径 | `TryUpdateResourcePath()` 接受新路径或返回失败 |

后两个扩展接口经会话的 `IEditorDocumentContent` 引用识别；只实现扩展接口而未实现基础内容接口，不会获得该能力。`DocumentStateChanged` 刷新标题中的 ` *` 脏标记和命令可用性。文档激活发出的 `WorkspaceManager.ContentIdSelected` 实际参数是资源路径，不能把事件名当作“传递 MD5 ContentId”的承诺。

## 保存与重新加载

主窗口可配置的保存/另存为动作先沿当前焦点检查并执行 `ApplicationCommands.Save` / `SaveAs`，不是无条件保存后台文档。内容控件可以接管其命令（例如 Copilot 输入框处理草稿）；只有继续路由到主窗口时才使用下述活动文档入口。另存为不增加内容接口能力：当前图像与3D查看器可导出渲染图/截图，文本编辑器没有因此新增通用另存协议。

主窗口 Save 命令由 `CommandInitializer` 接到活动文档：只有受管理内容同时 `CanSave=true`、`IsDirty=true` 时命令可用。直接调用 `TrySaveActiveDocument` 只检查 `CanSave`，并不额外要求脏状态；`TrySaveDocument(content)` 若找不到管理会话，则直接调用该内容的 `Save()`，也不会先检查 `CanSave`。

受管理保存调用内容的 `Save()`；返回 false 或抛异常会返回失败并显示错误，返回 true 则刷新文件时间/长度快照和标题。服务不会自行清脏状态，不保证内容层的原子写入、备份、冲突检测或失败回滚。保存多个文档也不是统一事务。

Reload 要求受管理内容支持重载且 `File.Exists(ResourcePath)`。有未保存修改时询问是否放弃，拒绝则不调用 `ReloadFromDisk()`；接受后由内容实现替换，false 或异常按失败处理。它不是合并外部修改，也不是先自动保存再读。服务层的 true 只沿用内容返回值，不能代替对特定编辑器加载/渲染完成的判断。

## 关闭、资源重命名与释放

主窗口用独立的 `MenuClose.CloseDocumentCommand` 关闭活动标签（默认 Ctrl+W / Ctrl+F4），CanClose=false 时不执行，也不回退到图像 `ApplicationCommands.Close` 的清空操作。独立窗口若没有声明这条文档命令，则通用关闭菜单按该窗口当前/记忆焦点保留原生 Close 路由；并非所有窗口都关闭标签。

单个文档 Closing 时，非脏内容直接通过；脏内容提供保存、不保存、取消三种选择。选择保存必须让 `Save()` 成功，取消或保存失败会阻止关闭；不保存只是批准关闭，不会主动把内存内容恢复成磁盘版本。

| 入口 | 实际行为 |
| --- | --- |
| `TryCloseAllDocuments()` | 对仍挂在布局中的受管理会话逐项预审，**不调用 `Document.Close()`**；主窗口 Closing 通过预审后调用 `SaveLayout` |
| `TryCloseDocumentsForResources(paths)` | 选择路径本身及其后代资源的已挂接会话，全部预审通过后逐项关闭 |
| `TryPrepareResourceRename(path)` | 只选择该路径/后代中不能响应路径更新的会话，预审后关闭；支持路径更新的内容保持打开 |
| `NotifyResourceRenamed(old,new)` | 将相同/后代路径映射到新位置；路径感知内容接受后更新路径、ContentId、监视器和必要标题 |

这些批量动作不是全有或全无：前一项已保存后，后一项取消不会撤销保存；关闭阶段后续标签拒绝关闭，也不会重开之前已关闭标签。资源范围比较不区分大小写，并检查相对路径是否越过根目录，但不是解析真实文件身份的权限边界。资源操作应由调用方先准备、执行文件操作后再通知；这些文档 API 本身不执行重命名。

预审选择“不保存”会缓存 `_closeApproved=true`。`TryCloseAllDocuments()` 全部通过后不清除此批准，后续 `DocumentStateChanged` 也不使它失效；若主窗口被其它关闭处理器取消而继续编辑，后续预审或 Closing 可能沿用旧批准而不再询问。部分预审失败会清理此前已批准项，真正 Closing 会消费该标记；不能将预审当成每次都重新确认当前脏状态的无状态检查。这是当前实现的保护缺口，不是允许丢弃后来修改的预期契约。

路径感知内容的 `TryUpdateResourcePath` 返回 false 时，该会话保持旧路径，`NotifyResourceRenamed` 没有聚合成功结果；异常也没有逐会话隔离。不能承诺“收到重命名通知就全部标签已迁移”。仅当原标题等于旧资源名时，服务才随路径更新标题，自定义标题保留。

对非路径感知内容，通知仍会修改会话路径、ContentId 和监视器，却不会更新内容自身保存的路径。外部文件重命名事件直接调用通知，未经 `TryPrepareResourceRename`；这类内容不保证已先关闭，服务路径与内容内部路径可能分离。不能据标签新名字推断后续保存一定写到新位置。

只有文档触发 Closed 后，会话才解除事件、停止并释放监视器/定时器、移出会话字典，再调用传入的 `closeContent`。服务不会对任意内容自动调用 `IDisposable.Dispose()`，释放责任取决于编辑器是否提供回调；例如 `TextEditor` 提供 Dispose，图像编辑器提供 Clear 和 Dispose。不能把从布局树移除、隐藏面板或布局重建等同于执行了这套 Closed 清理。

## 外部文件变化不是冲突锁

支持重载的受管理内容在父目录存在时尝试建立文件名过滤的 `FileSystemWatcher`，处理更改、创建、删除、重命名。事件通过应用 Dispatcher 安排检查，并由 300 ms 定时器合并；不是每个文件事件都立即重新载入。监视器创建失败会被捕获并停用，不能据“打开了标签”证明正在监视。

变化判定只比较存在标志、文件长度和 UTC 最后修改时间，不比较内容哈希。没有应用 Dispatcher、丢失的监视事件或相同时间/长度的变化，都不能靠此机制证明已捕获。它也不在保存前建立文件锁或检查磁盘版本。

- 检测到现存文件变化，内容未脏则自动尝试 Reload；内容已脏则询问是否放弃本地修改。拒绝后保留内存内容，不进行合并。
- 检查时先更新已观察快照，再提示/重载；拒绝或重载失败不会对同一快照持续重试，后续需要新的可检测变化或主动 Reload。
- 文件不存在时保留文档并显示 `[已删除]`，不关闭文档、不清空内存内容。获取文件信息遇到 I/O 或权限异常也会得到“不存在”快照，所以这个标签不是物理删除的充分证据。
- 收到匹配旧路径的重命名事件且新文件存在时，会转入 `NotifyResourceRenamed`；这不保证外部父目录移动或所有文件系统事件都能被监视器追踪。

## 停靠注册、布局恢复和重置

`DockLayoutManager` 的注册表按 ContentId 保存内容与元数据，独立于 `EditorDocumentService` 的动态文档会话。主窗口先注册自身面板和各 `IDockPanelProvider`，再加载布局；只有已经注册的内容才有恢复入口。

布局位置为 `Environments.DirStateLayout/MainWindowDockLayout.xml`，通常来自 `DirAppData/State/Layout`，不是工作区 `.cvsln` 内的文件标签清单。`SaveLayout()` 建目录后用 `StreamWriter` 直接序列化 XML，捕获异常记警告，不返回失败状态；这里没有配置存储那样的临时文件原子替换、备份或写后验证，返回不证明布局文件完整落盘。

`LoadLayout()` 文件不存在或异常时返回 false。序列化回调只按注册 ContentId 绑定内容，其余项取消，包括未注册的动态编辑器标签；不会从 XML 中的路径重新调用编辑器。成功后刷新注册标题，替换 `WorkspaceManager` 的布局/文档窗格引用，必要时补建文档窗格，并清除 DockView 缓存。方法自身失败时不保证回滚反序列化的中间状态；主窗口调用方在 false 后执行 `ResetLayout()`。

`ResetLayout()` 先删除现有布局文件，再按面板默认位置和 `IsDefaultVisible`、已注册文档重建布局，最后清 DockView 缓存并调用 `ShowAllViews()`。它不保留未注册动态编辑器标签，也没有调用文档保存、关闭预审或逐项 Close；重置菜单直接调用它。因而不能把重置描述为保护未保存内容、触发编辑器释放或仅改变窗口位置的动作。失败捕获后只记警告，不恢复已删除的 XML 或之前的布局，也不立即另存新布局。

面板的 Hide/Show 与文档关闭不同。`TogglePanel` 隐藏/显示已有面板；`ShowPanel` 还会激活面板。面板已从布局移除时，两者可从注册表重新加入；不代表创建全新内容实例或重启终端进程。`IsPanelVisible` 只检查是否找到面板且未隐藏，不保证该页签当前激活或内容已构造。

工厂注册用 `Lazy<object>` 复用内容；恢复布局时先放 `DeferredDockContent`，可见且已 Loaded 后在 `ApplicationIdle` 尝试构造，显式 Show 也会触发。每个延迟宿主只尝试物化一次，失败记录日志，不承诺再次 Show 会重试；空工厂结果在取值时拒绝。Reset 不重建内容注册表，也不使其中的 Lazy 缓存失效；不能用布局重置推断面板实例或失败的工厂已重新创建。布局恢复返回 true 也不意味着所有延迟面板均已构造成功。

## 证据与验证缺口

`Test/ColorVision.UI.Tests/DockContentRegistrationTests.cs` 当前覆盖工厂延迟与单次创建、空结果拒绝、显式物化、已有内容直接恢复，以及 `ShowPanel` 首次构造/物化复用。它没有覆盖主窗口布局 XML 的保存恢复、带脏文档重置、默认编辑器配置落盘失败、文档关闭取消、外部文件事件或路径更新失败，不能从这份测试推断完整生命周期已验证。

本主题依据源码与这些测试定义核对，不记录本轮未执行的测试为通过。后续改动应围绕受影响的选择、保存/取消、外部变更或布局恢复分支取得针对性证据；启动产品、写布局/配置、保存编辑内容及调用资源操作仍需要任务授权，不是只读查阅文档的必要步骤。
