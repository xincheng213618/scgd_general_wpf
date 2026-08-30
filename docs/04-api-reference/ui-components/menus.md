---
knowledge_id: "ui.menus"
knowledge_type: "topic"
status: "current"
summary: "菜单的插件 DLL 发现、类型缓存、父子树和管理提交；IHotKey 提示随运行时键位更新，隐藏不禁用快捷键，应用成功提示不保证配置落盘，菜单入口不构成统一鉴权。"
aliases: ["菜单", "菜单管理", "菜单发现", "菜单隐藏", "菜单父子关系", "菜单重建", "菜单权限", "快捷键提示", "MenuManager", "IMenuItem", "IMenuItemProvider", "MenuItemBase", "MenuItemAttribute", "MenuItemMetadata", "MenuItemScopeKey", "OwnerGuid", "GuidId", "InputGestureText", "HotkeyMenuGestureBinding", "MenuService", "MenuItemManagerService", "MenuItemManagerWindow", "MenuSearchProvider"]
code_paths: ["UI/ColorVision.UI/Menus", "UI/ColorVision.Common/Interfaces/Menus", "UI/ColorVision.UI.Desktop/MenuItemManager", "UI/ColorVision.UI/ConfigHandler.cs", "UI/ColorVision.Common/MVVM/RelayCommand.cs", "UI/ColorVision.UI/Serach/MenuSearchProvider.cs", "UI/ColorVision.UI/Serach/SearchControl.xaml.cs", "UI/ColorVision.UI/HotKey/HotkeyService.cs", "ColorVision/MainWindow.xaml.cs"]
test_paths: ["Test/ColorVision.UI.Tests/MenuDiscoveryExclusionTests.cs", "Test/ColorVision.UI.Tests/MenuItemManagerServiceTests.cs", "Test/ColorVision.UI.Tests/HotkeyMenuBindingTests.cs"]
related: ["ui.framework", "ui.discovery", "ui.common", "ui.desktop", "ui.settings", "ui.configuration", "ui.hotkeys", "ui.search", "platform.security", "algorithms.template-menus"]
---

# 菜单：发现、显示、执行与管理提交

菜单接口在 `ColorVision.Common`，`UI/ColorVision.UI/Menus/MenuManager.cs` 为调用方提供的 WPF `Menu` 装配菜单树；`UI/ColorVision.UI.Desktop/MenuItemManager/` 管理显示覆盖。三者分别负责扩展声明、运行时呈现和配置编辑，不接管菜单背后的设备、文件或业务事务。

主程序分别调用 `LoadMenuForWindow(MainWindow, Menu1)` 和 `LoadHotKeyFromAssembly()`。菜单显示、命令可执行、快捷键注册、搜索结果和业务完成是不同证据；不能通过隐藏一个入口来承诺操作已禁用。

## 发现来源和实例生命周期

`EnsureTypeCaches` 从 `AssemblyHandler.GetAssemblies()` 的缓存/过滤视图取得类型，并通过 `GetTypes` 读取可用类型。候选必须为非抽象、非开放泛型的 class，未直接标注 `[Obsolete]`，且具有公共无参构造函数。仅仅“插件 DLL 已加载”不能证明类型满足这些条件或已进入该视图。

同一类型按以下优先级进入一条发现路径，不会因同时带特性而得到第二个菜单：

| 优先级 | 来源 | 何时构造、什么会被收录 |
| --- | --- | --- |
| 1 | `IMenuItem`，包括 `MenuItemBase` | 每次创建目录时构造，`Command != null` 才收录；这一层不要求 Header 非空 |
| 2 | `IMenuItemProvider` | 每次构造 provider 并枚举结果；仅收录非 null、Command 和 Header 均非 null 的元数据 |
| 3 | 只标注 `[MenuItem]` 的类型 | Header 非 null 时建立懒 adapter；目标类到每次执行时才实例化 |

直接条目和 provider 的构造/枚举异常按类型记录日志后继续；provider 已产出的条目不会因后续枚举失败而统一撤回。调用方的 `typeFilter` 在构造前按声明类型筛选，provider 整体受筛选；`TargetName` 的窗口筛选则在条目创建之后。因此为某个窗口加载菜单仍可能先构造其它窗口的条目，构造函数不应被当作只在用户点击时执行。

类型缓存只构建一次，当前没有公开失效接口。`RebuildAllMenus` 和 `RefreshMenuItemsByGuid` 都不重新扫描菜单类型；它们只是用既有类型列表重新构造条目/provider。上游程序集刷新、菜单重建和新插件菜单可发现，不是同一件事。

普通条目实例也不是 manager 为类型维护的永久单例；重复枚举、加载或刷新会产生新实例。菜单控件持有其 `Tag`/Command，但 manager 没有统一调用这些条目或 provider 的 `Dispose`。业务资源和订阅不应依赖“菜单重建会自动清理旧实例”。

## 窗口目标、标识与父子树

`MenuItemBase` 默认 `TargetName=MainWindow`、`OwnerGuid=Menu`、`GuidId=类型简单名`；`GlobalMenuBase` 改为 `TargetName=Global`。特性路径默认 Global，未指定 GuidId 时 adapter 实际使用类型 **FullName**，不是特性注释所说的简单类名。`MenuItemMetadata` 默认 GuidId 来自 `new Guid()`，即全零 GUID 字符串，不会自动生成随机唯一标识；provider 应提供稳定且不冲突的 ID。

`LoadMenuForWindow(targetName, menu, typeFilter)` 只选 TargetName 精确匹配或 Global 的条目。Global 表示参与这些已接入 manager 的菜单，不会自动给任意窗口添加菜单栏。

- `OwnerGuid=Menu` 才作为根项；其它项按有效 OwnerGuid 匹配父项 GuidId。没有对应父项的孤儿不会自动提升到顶层。
- 同级按有效 Order 排序；子项与前一项的 Order 差大于 4 且当前项可见时插入分隔符，不代表业务分组语义。
- 树构建不按 GuidId 去重或合并。同一窗口中重复 ID 可能显示多个条目、重复挂载子树；沿当前路径遇到循环 ID 时只剪掉该分支，不能把剪枝当成冲突修复。
- `MenuItemScopeKey(TargetName, GuidId)` 用于覆盖与过滤身份，不是树节点的全局唯一性强制机制。作用域覆盖优先于旧 GuidId-only 覆盖，未覆盖时使用条目的原始值。

Desktop 编辑快照会对相同 ScopeKey 保留首个实时条目，与运行时树不去重的规则不同。不能因为菜单管理里只显示一行，就断定实际贡献没有冲突。

## 隐藏、刷新与控件状态

manager 的 `FilteredGuids` 是旧式跨目标过滤，`ScopedFilteredItems` 是按目标过滤。被过滤父项的后代按有效 OwnerGuid 递归过滤，父项匹配考虑同目标和 Global；排序/父级覆盖分别来自有效覆盖表。

这些过滤与 `IMenuItem.Visibility` 不同。`CreateMenuItem` 将 Header、Icon、Command、Visibility 和 IsChecked 复制到控件，没有为这些字段建立通用更新绑定，也不会把控件勾选回写给源条目。InputGestureText 通常也是复制值，只有 `IHotKey` 菜单通过专用适配跟随运行时组合，见下文。`GetAllMenuItemsFiltered()` 只应用上述 ID 过滤，不等于“当前窗口可见且可执行的菜单”：它可以包含其它目标、Collapsed 条目或没有父项的孤儿，且不检查 `CanExecute`。

| 刷新入口 | 实际影响范围 |
| --- | --- |
| `LoadMenuForWindow` / `RebuildAllMenus` | 清空已注册 Menu 并重新建树；不清类型缓存。首次接管时备份已有顶层 MenuItem，重建后按 Header 去重追加这些原对象 |
| `RefreshMenuItemsByGuid(ownerGuid)` | 每个已注册 Menu 只找第一个匹配 ID 的节点，清空并重建它的子项；不更新该节点自身的标题、可见性、Command，也不创建缺失的根节点 |
| Menu Unloaded 或所属 Window Closed | 注销该 Menu 的注册、事件和备份；再次装载控件不等于自动重新注册，需要调用方重新接入 |

注册保留原 TargetName 和 typeFilter。`RebuildAllMenus` 只处理仍注册的菜单，并不是刷新搜索索引、快捷键或所有自定义按钮的通用广播。`IMenuService` 只暴露按父 ID 刷新入口，宿主需先创建/注入 `MenuManager`。

## 执行、搜索与快捷键的边界

默认 `MenuItemBase.Command` 返回 `RelayCommand`，把 `AccessControl.Check(Execute)` 放在 `CanExecute` predicate。`RelayCommand.Execute` 本身直接执行 action，不重新检查 predicate；其它条目可返回完全不同的 ICommand。权限特性和粗粒度检查的准确范围见[Common 命令契约](./ColorVision.Common.md)与[安全边界](../../03-architecture/security/overview.md)，不能从菜单是否变灰推导所有调用都被保护。

懒 adapter 的 Command 使用默认可执行的 RelayCommand；每次执行重新构造目标类，再反射调用公共或非公共的无参 `Execute()`。它不自动解析目标类的 ICommand 属性，也不添加 AccessControl/CanExecute 检查；构造失败、缺少方法或同步反射调用异常会记录警告。反射返回值被忽略，返回 Task 时既不等待，也不观察其后续异步失败，因此菜单点击或返回不能作为异步业务完成信号。实现 `IMenuItem` 的类型优先走直接路径，不会因再加 `[MenuItem]` 自动变成懒加载。

`MenuSearchProvider` 使用 `GetAllMenuItemsFiltered` 建立搜索项，只额外要求 Header/Command 非空；它不按当前窗口 TargetName、Visibility 或 CanExecute 再过滤。搜索执行处也不统一检查 CanExecute，因此仅有 MenuItemBase 的 predicate 不能建立所有入口的鉴权保证。候选刷新、旧选中项和直接执行的完整边界归[产品搜索契约](./search.md)；菜单重建不主动更新已有搜索集合。

`InputGestureText` 仅为显示文字，不注册 `Ctrl+X` 等快捷键。`HotkeyService` 独立发现 `IHotkeyProvider` / `IHotKey`，还接收显式注册；它不读取菜单隐藏覆盖。已注册快捷键可能仍调用原操作，是否能执行继续取决于该回调自己的检查和[热键注册状态](./hotkeys.md)。菜单管理窗口也没有快捷键编辑字段，编辑入口在独立的快捷键设置页。

`MenuManager` 为实现 `IHotKey` 的现有菜单条目附加 `HotkeyMenuGestureBinding`：

- 只读取该条目的 `HotKeys` 声明一次取得明确 ID；未提供 ID 的旧单动作 provider 按与热键发现相同的类型 FullName 规则匹配。不按 Header、Name 或菜单 GuidId 猜测，名称翻译、重名和菜单移动不改变关联。
- 弱订阅 `HotkeyService.HotKeys` 的集合变化和匹配条目的 `Hotkey` / `AdditionalHotkeys` 属性，按顺序以 ` / ` 连接显示全部运行时组合。菜单先创建、随后加载热键、编辑附加组、删除首组、定义替换、清除与恢复默认都会更新提示；没有匹配运行时项或组合已全部清除时留空，不回退为可能失效的默认键位。
- 提示反映组合值，不以临时 `IsRegistered` 变化闪烁；它不是操作系统注册成功的指示灯。适配本身不重新发现 provider、不注册热键、不执行业务回调。声明读取失败只记录警告并留空，不阻止菜单创建。
- 同时实现多动作 `IHotkeyProvider` 的条目必须在其 `IHotKey` 声明中给出明确动作 ID，适配不会枚举所有动作选一个。非 `IHotKey` 菜单保留原始 InputGestureText。

订阅不把丢弃的 MenuItem 强引用留在运行时集合上，也不把子菜单弹出层的临时 Unloaded 当作永久解绑。此适配只属于 MenuManager 创建的控件，不改变 Common 层的 `ToMenuItem()` 扩展。

`IRightMenuItemProvider` 则由 `MainWindow.InitRightMenuItemPanel` 单独装配右侧按钮，不经 MenuManager 树和隐藏覆盖；其它地方直接使用 `ToMenuItem()` 也不能自动获得 manager 的配置规则。

## 菜单管理的草稿与提交

入口由 `MenuItemManagerAppProvider` 提供到 Apps & Tools，声明 `Administrator` 权限要求；不是可由自身隐藏的菜单贡献。具体工具启动检查见[第三方工具契约](./ColorVision.Common.md)，声明权限元数据不代表任意直接调用都被拦截。

`MenuItemManagerService.CreateEditingSnapshot()` 用实时菜单目录与持久化覆盖生成分离的 `MenuItemSetting` 草稿。界面的可见性、OrderOverride、OwnerGuidOverride 修改先留在草稿中；Cancel 或直接关闭不提交这些未应用的覆盖，Reset 也只改草稿，仍须 Apply。这与[常规选项窗口](./settings.md)直接编辑活配置并在菜单返回后保存的模式不同。

但打开管理窗口不是绝对只读：创建快照会迁移/清理活配置中的旧 Settings/退役条目，选择目标或树节点会直接更新 `LastSelected*` 内存字段。关闭不撤销这些辅助状态，也不会撤销此前已经 Apply 的更改；后续配置保存可能将它们落盘。

配置只保存自定义覆盖，不复制完整实时菜单目录。没有 TargetName 的旧覆盖在编辑快照中按已知实时目标展开；Apply 将快照转换为显式作用域覆盖，只有文件保存成功才会持久化，单纯打开后保存活配置不会自动完成这种转换。暂时缺失插件的孤立覆盖会保留。移动父级校验拒绝自身、后代循环和不允许的目标：目标窗口项可挂到同目标或 Global 父项，Global 项只能挂 Global。直接操作 MenuManager 公共覆盖表并不自动经过这套编辑校验。

没有有效自定义覆盖时，`ApplyConfigToMenuManager` 只清理残留的运行时覆盖表，不为构造覆盖表而枚举实时菜单。这个快速分支不代表调用方后续重建菜单也不会枚举或实例化条目。

`CommitEditingSnapshot` 的顺序为：校验草稿 → 生成稀疏覆盖并更新活配置/运行时表 → 有变化时重建注册菜单 → 调用 `ConfigHandler.Save<MenuItemManagerConfig>()`。返回 bool 表示运行时覆盖表是否变化，false 不是保存失败；Apply 窗口也不根据这个 bool 判断落盘。

**当前保存结果存在缺口：** `Save<T>()` 内部丢弃 `TrySave` 的 bool 和错误信息。序列化/写盘失败被底层转成失败返回时，不会触发 Commit 的异常回退；运行时菜单可能已变化，窗口仍能显示应用成功。因此 Commit 正常返回或 Apply 成功弹窗都不能证明文件保存成功。若有异常实际传播，Commit 会尝试恢复旧 Overrides 和运行时表并重建，再重抛；这也不是对所有窗口、副作用和文件的一次原子事务。底层返回值和文件提交归[配置持久化](./configuration.md)，此处记录现存调用链缺口，未修复产品实现。

## 验证入口与缺口

`MenuItemManagerServiceTests` 覆盖草稿不暴露原覆盖对象、稀疏覆盖生成与 JSON 序列化、旧快照迁移、作用域区分/展开、父级循环与跨窗口限制、退役条目清理和暂缺插件覆盖保留。测试使用合成菜单，**没有调用 CommitEditingSnapshot，也没有验收真实窗口 Apply、保存失败或快捷键行为**。

`MenuDiscoveryExclusionTests` 断言特定已删除类型不存在，以及两个保留类型能通过候选判定、MySQL 工具的 Owner/Order；不是完整 `LoadMenuForWindow` 集成测试。当前未发现类型晚加载、重复 ID 树、局部刷新、懒命令执行、搜索鉴权和菜单注册生命周期的直接专项覆盖。

`HotkeyMenuBindingTests` 使用隔离的运行时集合、合成菜单和四个内置菜单的只读声明，覆盖原默认键保留、显式/旧类型 ID、先建菜单后加载热键、名称不参与匹配、清除/恢复、定义替换、普通菜单提示保留、重复附加、不可读声明、多动作 ID 要求与丢弃控件的弱引用生命周期。它不调用生产热键注册、配置保存或业务命令，不代表已验收真实 Win32 输入或完整菜单发现。

修改发现/树构建看 `UI/ColorVision.UI/Menus/MenuManager.cs`；修改管理提交看 `UI/ColorVision.UI.Desktop/MenuItemManager/MenuItemManagerService.cs` 和窗口 Apply/Cancel；修改声明/命令看 `UI/ColorVision.Common/Interfaces/Menus/`。验证时分别证明候选进入、树显示、命令实际检查、运行时应用和文件保存，不通过执行真实业务菜单来默认“验收文档”。
