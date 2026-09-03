---
knowledge_id: "plugins.model"
knowledge_type: "topic"
status: "current"
summary: "PluginLoader的manifest/依赖门禁、禁用缓存、程序集发现和失败边界；载入不等于provider可用，也不支持隔离卸载。"
aliases: ["插件生命周期","插件菜单为什么没出现","插件能否卸载和热更新","插件禁用后仍运行","requires","deps.json","Loaded","IPlugin","PluginManifest","PluginLoader","PluginLoaderrConfig","ModuleCatalog"]
code_paths: ["ColorVision/App.xaml.cs","ColorVision/MainWindow.xaml.cs","UI/ColorVision.Common/Interfaces/IPlugin.cs","UI/ColorVision.Common/Interfaces/Assembly/ModuleCatalog.cs","UI/ColorVision.UI/Plugins/PluginManifest.cs","UI/ColorVision.UI/Plugins/PluginLoader.cs","UI/ColorVision.UI/Plugins/PluginLoaderrConfig.cs","UI/ColorVision.UI/Plugins/PluginInfo.cs","UI/ColorVision.UI/AssemblyHandler.cs","UI/ColorVision.UI/Menus/MenuManager.cs","UI/ColorVision.UI/StatusBar/StatusBarManager.cs","UI/ColorVision.UI.Desktop/Marketplace/PluginUpdateCompatibility.cs"]
test_paths: ["Test/ColorVision.UI.Tests/PluginLoaderTests.cs","Test/ColorVision.UI.Tests/ModuleCatalogTests.cs","Test/ColorVision.UI.Tests/PluginUpdateCompatibilityTests.cs","Test/ColorVision.UI.Tests/MenuDiscoveryExclusionTests.cs"]
related: ["plugins.index","plugins.getting-started","ui.discovery"]
---

# 插件装载、依赖门禁与扩展发现

本页是运行时插件装载的契约：DLL 进入主进程后，由对应宿主发现扩展，而不是按 manifest 调用一套统一的插件生命周期。项目创建、HostCopy、`.cvxp` 安装/导出、打包与恢复交接见[插件产物与交付](./getting-started.md)，不把包校验规则当成运行装载器已经执行的检查。

## 主程序的装载顺序

`ColorVision/App.xaml.cs` 将工作目录设为程序基础目录，先建立 `ModuleCatalog` 并注册内置模块；正常插件阶段调用 `PluginLoader.LoadPlugins(catalog, skipOncePluginKeys, onPluginLoading)`，扫描 `Plugins/` 的直接子目录。启动恢复可跳过全部插件或指定插件；装载结束后记录 `PluginsLoaded` 并 `Seal()` 目录，再进入后续窗口流程。

`PluginLoader.LoadPlugins(string path)` 本身允许传入路径，没有 `ModuleCatalog` 参数的重载不会登记模块；它仍会加载程序集并在末尾刷新 `AssemblyHandler`。这些重载不能当作隔离测试器：扫描会创建缺失目录、保存插件配置，加载及后续实例化可执行插件代码。

`ModuleCatalog` 按大小写不敏感的 `Kind:Id` 记录程序集：同一键/同一程序集重复注册幂等，同键换程序集或封存后注册会抛异常。它记录模块并交给 `IAssemblyService.RegisterAssembly`，不负责启动业务，也不是完整依赖排序器。

## manifest 实际参与哪些门禁

运行模型在 `UI/ColorVision.UI/Plugins/PluginManifest.cs`；具体判断在同目录的 `PluginLoader.cs`。

| 字段或文件 | 当前装载器行为 |
| --- | --- |
| `manifest.json` / `id` | 清单存在时必须可反序列化且 `id` 非空；无效清单记失败，不回退为 legacy 插件 |
| `dllpath`（`DllName`） | 非空时与候选目录组合；否则回退为“目录名.dll”，再检查文件存在并 `Assembly.LoadFrom` |
| `requires` | **不作为 `PluginLoader` 的启动版本门禁**；不能因写了最低版本就断言旧宿主会拒载 |
| `manifest_version` / `version` | 装载器不按它们选择协议或执行版本准入；成功加载后另记录实际程序集版本 |
| `entry_point` | 仅模型字段，装载器不据此构造入口或调用 `IPlugin.Execute()` |

运行装载器对 `id` 的检查不是安装器的安全目录名校验，组合 `dllpath` 也不是插件目录边界校验。有效安装包的路径约束见交付主题；不能把 `manifest.json` 当沙箱或信任证明。

### `.deps.json` 预检不是完整依赖解析

1. 只有候选目录恰好存在 **一份** `*.deps.json` 时才解析；零份或多份不会执行这段解析。单份 JSON 格式错误会使该候选失败；反序列化为 `null` 则没有预检对象。
2. 对有 manifest 且 `depsObj != null` 的候选，只取 `Targets` 的第一个 target、其中第一个 package 的 `Dependencies`，不按 `runtimeTarget.name` 精确选取。依赖集合为空或缺失时判预检失败。
3. 非空依赖集合中，仅检查名称以 `ColorVision` 开头的项：宿主基础目录必须有 `<依赖名>.dll`，其 **AssemblyVersion** 必须不小于 `new Version(dep.Value)`。缺文件、版本不足、非法版本字符串或读取程序集元数据失败都会拒载；这不是 FileVersion 比较，也不支持任意语义版本范围。
4. 第三方依赖不在这段比较中；没有执行预检不等于依赖全部满足，实际 `Assembly.LoadFrom` / 类型扫描仍可能失败。legacy 分支不做上述版本比较，但单份 `.deps.json` 的读取/解析发生在分支前，解析异常仍可能阻止它加载。

市场更新选择由 `UI/ColorVision.UI.Desktop/Marketplace/PluginUpdateCompatibility.cs` 根据更新元数据的 `RequiresVersion` 与宿主版本判断，和上述启动装载规则不同；不要用市场兼容测试证明 `PluginLoader` 已检查 manifest 的 `requires`。

## 禁用状态、缓存与 legacy

`PluginLoaderrConfig.Plugins` 主要以 manifest ID 保存 `PluginInfo`。扫描开始时，用磁盘清单中的有效 ID（否则用目录名）计算保留集合，删除已无对应候选的缓存项；这只删配置记录，不卸载程序集。

对单个目录，装载器先检查目录名是否退役、缓存中同目录名是否禁用，以及是否属于本次跳过名单，再解析清单。解析后按 manifest ID 新建或更新 `PluginInfo`：新项默认 `Enabled=true`，已有项保留禁用状态，再执行退役/禁用/本次跳过判断。当前退役名单包含 `EventVWR`。缓存字典的默认键比较与大小写不敏感的退役/跳过集合并不相同；不要通过改 ID 或只改大小写来尝试绕过管理状态。

`skipOncePluginKeys` 可按 manifest ID 或目录名匹配，匹配不区分大小写，只影响该次扫描；它不自动改写 `Enabled`。`Enabled=false` 是后续装载时的跳过条件，不是停止当前插件的命令。

缓存文件是 `PluginLoaderrConfig.ConfigFilePath`：优先使用当前目录已有的 `Config`，否则在应用数据目录中选择公司配置目录，再按安装路径哈希区分 `Plugins/<hash>.json`。装载结束和进程退出时保存；保存异常只记日志。因此改禁用状态后仍需确认保存结果，不能假定所有安装实例共享同一份状态。

没有 manifest 的 legacy 候选尝试 `<目录名>/<目录名>.dll`，成功时可以登记模块并被后续程序集扫描发现，但该分支**不会新建对应 `PluginInfo` 缓存项**。所以管理列表不是当前进程已加载程序集的完整清单；有缓存条目也不表示 DLL 已成功加载，缓存项在实际加载前就可能建立。

## Loaded 不等于 provider 可见

“Loaded”应明确指哪个阶段。`PluginInfo.Assembly` 只在进程中保存且不序列化，版本/路径等缓存字段可能来自以前的扫描；`PluginsLoaded` 是启动阶段标记，`LastLoadCompletedWithoutFailures` 只表示装载器记录的失败计数为零，均不证明所有扩展已构造、初始化或可执行。

装载后的可见性继续经过程序集过滤、类型读取、provider 构造和宿主缓存，统一按[UI 扩展发现与排查](../../04-api-reference/ui-components/ui-runtime-handoff.md)核对。程序集登记不绕过目录/名称过滤，刷新程序集也不自动重建菜单或状态栏；不要将本页的装载成功当作入口已经显示。

基础 `IPlugin` 只有 `Header`、`Description`、`Execute()`；实现它或填写 `entry_point` 本身不会注册菜单、设置或状态栏。主窗口另有 `IMainWindowInitialized` 扩展：在 dispatcher 回调中按 `Order` 逐个等待 `Initialize()`，该调用抛出的异常记录后继续；它不是插件通用的 Init/Start/Stop/Unload 状态机。

## 异常、隔离与恢复边界

单个候选目录内的清单、依赖、加载或登记异常由 `PluginLoader` 捕获，计失败、弹窗/记日志后继续其它目录；但目录创建、外层目录枚举等并不全在该候选的 `try` 中，不能宣称任何插件目录异常都不会影响启动。类型扫描、provider 构造和主窗口初始化还发生在各自的异常边界，不能只看装载失败计数。

`Assembly.LoadFrom` 在 `ModuleCatalog.AddPlugin` 之前；后续登记失败不会撤销已经加载的程序集，末尾刷新仍可能从 AppDomain 发现它。这里没有独立进程、权限沙箱或可回收加载上下文，也没有统一卸载/撤销副作用的实现。`ClearCaches()`、禁用或删除缓存项都不卸载 DLL；不能承诺当前进程内原位热替换。

诊断从“候选目录/缓存禁用或跳过 → manifest 与 DLL → 条件性依赖预检 → 实际程序集 → 类型/实例 → 目标入口”收窄。读取配置和日志与启动插件是不同授权范围；不要为了验证本文自动加载未知 DLL、启停硬件、删除插件目录或重启用户进程。需更换/恢复磁盘产物时，按[插件交付与恢复契约](./getting-started.md)处理，不在此另建恢复流程。

## 测试覆盖与缺口

- `PluginLoaderTests` 仅覆盖本次跳过匹配和 DLL 路径存在检查；文件存在测试写入任意字节，不验证该文件是可加载程序集，也不覆盖完整加载或 `.deps.json` 版本门禁。
- `ModuleCatalogTests` 覆盖同程序集重复注册、封存后拒绝、Rbac 内置 provider 对已建立发现快照可见及类型快照复用；不代表所有外部插件与全部 provider 都可用。
- `PluginUpdateCompatibilityTests` 覆盖市场更新版本筛选、最低宿主要求与回退规则，不覆盖运行装载器。
- `MenuDiscoveryExclusionTests` 检查指定旧菜单类型缺失和部分保留菜单的候选/位置，不是所有菜单或状态栏发现的端到端测试。

完整加载、依赖文件异常、缓存持久化、失败后残留程序集、provider 副作用及目标窗口可见性仍需专门验证。文档/路径检查通过不等于已启动插件或完成业务验收。
