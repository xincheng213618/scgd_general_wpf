---
knowledge_id: "platform.runtime"
knowledge_type: "topic"
status: "current"
summary: "启动分支、配置初始化、插件装载和恢复流程的运行时顺序。"
aliases: ["启动链路","PluginLoader","App.xaml.cs","启动恢复"]
code_paths: ["ColorVision/App.xaml.cs","ColorVision/App.StartupMaintenance.cs","ColorVision/Recovery","UI/ColorVision.UI/Plugins/PluginLoader.cs"]
test_paths: ["Test/ColorVision.UI.Tests/SingleInstanceStartupTests.cs","Test/ColorVision.UI.Tests/StartupRecoveryPluginScannerTests.cs","Test/ColorVision.UI.Tests/StartupMaintenanceLifecycleTests.cs","Test/ColorVision.UI.Tests/StartupMaintenanceWindowTests.cs","Test/ColorVision.UI.Tests/WizardWindowRuntimeTests.cs","Test/ColorVision.UI.Tests/StartupRecoveryWindowRuntimeTests.cs","Test/ColorVision.Copilot.Tests/CopilotBackgroundShellMaintenanceGuardTests.cs"]
related: ["platform.architecture","platform.startup-integrity","delivery.update","ui.wizards","ui.localization","engine.rc-registration","ui.search"]
---

# 架构运行时

本页只描述当前代码里能看见的主程序运行时链路，不维护脱离实现的统一启动时序图。

## 运行时分支

当前桌面程序不是“一次性初始化完再显示主界面”的单一模型。常见分支是：

| 分支 | 行为 |
| --- | --- |
| 命令行文件处理 | `export` 处理后结束该启动分支；`input` 仅在 `.cvraw` / `.cvcie` 被独立打开路由处理时提前返回，普通 PNG、JPEG、TIFF 不因 `input` 跳过正常启动 |
| 正常桌面启动 | 初始化配置、日志、主题、语言、插件，再进入向导或启动窗口 |
| 异常恢复 | 上次启动未完成时进入独立恢复窗口，可先更新主程序、跳过或禁用插件、回退插件，再决定是否继续 |

## 主程序启动链路

从 `ColorVision/App.xaml.cs` 看，常见启动顺序是：处理更新包输入，创建本次启动记录，设置工作目录并预加载 DLL，初始化配置/日志/主题/语言，处理文件分支，执行单实例检查（非调试且未允许多实例时，尝试关闭同会话、同安装路径的较早实例，不限于异常进程），按上次启动状态决定是否进入恢复窗口，初始化 WinForms 视觉样式，最后显示 `WizardWindow` 或 `StartWindow`。旧实例替换是产品启动行为，不是排障时终止进程的授权；启动副作用与验证前提见[主程序启动与最小图像验证](../../00-getting-started/first-steps.md)。

这里最重要的不是记住所有步骤，而是知道启动并不总会直接进主窗口。

启动记录按安装目录和启动尝试隔离，并记录进程、阶段及正在加载的插件。主窗口初始化完成、功能启动器执行成功、系统关机/注销或已有进程/更新接管等路径会清理相应记录；向导的退出清理分支实际只判断“本次显示过向导且当前内存 `WizardCompletionKey=true`”，不能据此认定新进程已成功启动。向导标记、保存和重启的区别见[向导完成契约](../../04-api-reference/ui-components/wizards.md)。多开进程不会互相覆盖记录；恢复窗口关闭或恢复准备失败时保留原故障阶段，供下次继续判断。

启动时的语言配置读取可能回退到系统文化；运行中从设置选择语言则走确认、保存和重启，不是给所有窗口即时换字，详见[界面语言契约](../../04-api-reference/ui-components/localization.md)。

早期依赖异常另由主程序 `StartupFailureGuard` 与ServiceHost观察协作：主程序可能提示后自行退出，后台则只在有限观察和缺项条件下补充告警，不自动修复或强杀。Release接入、终态上报、十秒窗口及文件存在性检查的范围见[启动失败与缺依赖告警](../components/startup-integrity.md)，不能以无告警代替安装完整性验收。

## 启动恢复

恢复窗口只扫描 `Plugins/` 下的磁盘清单和文件时间，不读取 `.deps.json`，也不加载插件程序集。它会优先标出上次记录到的插件；“疑似”只表示与启动记录匹配，不等于已经确认故障。

| 动作 | 行为 |
| --- | --- |
| 检查更新 | 只检查主程序；有新版时更新并重启，无新版时可下载完整安装程序修复当前版本 |
| 本次跳过选中 | 本次在读取依赖和加载 DLL 前跳过所选插件，配置不变 |
| 本次跳过全部 | 本次不进入插件加载器 |
| 禁用选中并启动 | 持久保存禁用状态；旧式无清单插件也按目录名识别 |
| 插件回退 | 仅在存在同安装目录、校验通过的更新前备份时可用；外部进程精确替换该插件目录后重启 |
| 其他恢复 | 打开程序快照、主日志和更新日志；仅浏览这些入口不会清除故障现场 |

## 搜索中的维护入口

`ColorVision/Recovery/StartupMaintenanceSearchProvider.cs` 提供“初始化向导”和“故障恢复”的搜索目录，不依赖菜单注册。搜索只读取名称、说明和别名；选中后检查宿主与管理员权限，以主窗口为 Owner 居中直接打开对话框，不弹重启确认、不关闭主窗口，也不要求先结束运行任务。向导使用 `runInitializers: false`，不重复执行首次初始化链；步骤自身的刷新、配置和安装行为仍保留。

运行期恢复窗口的“关闭窗口／返回应用”只关闭对话框，“初始化向导”直接打开向导；永久禁用保存后留在窗口，明确下次启动生效，不尝试卸载已加载插件。只有“重启并安全启动／重启并跳过所选”等启动阶段动作才检查活跃任务、更新保护并显示默认否的重启确认，沿正常文档保存和窗口关闭路径处理，取消就不启动新进程。

这些临时跳过动作通过一次性 `--startup-maintenance safe-start` / `skip-plugins` 和 JSON 数组形式的 `--startup-skip-plugins` 传递选择，入口类在 App、配置加载和单实例处理之前消费 `--wait-for-process` 等待原进程退出；用户无需重启后重复选择。解析失败或空列表的 `skip-plugins` 请求回到启动恢复页，不静默加载全部插件；非空键仍按插件加载器的精确匹配规则处理。旧 `setup` / `recovery` 参数保留兼容。入口不修改向导完成标记或多实例设置，也不制造启动失败记录；维护退出不会附带应用预取更新，取消且仍有窗口时不影响之后普通退出的更新行为。

只有所有窗口同意关闭后才启动子进程。此前的取消、权限/任务状态变化或配置保存异常会阻止启动；如果窗口已全部关闭而创建子进程失败，会提示手动重新打开并正常退出旧实例，不遗留无窗口进程继续占用单实例锁。

运行期执行更新、完整修复、插件回退或快照还原前，额外复核任务/更新状态，并在业务启动前完成主文档保存或取消确认及独立编辑窗口的正常关闭；主窗口和当前维护对话框的 Owner 链保持打开。取消或保存失败不会开始恢复业务。仅浏览日志、快照列表和恢复选项不会做这套收尾。

启动时发生真实故障仍在插件加载前打开恢复窗口，不被临时跳过参数绕过。运行期与健康状态下主动进入时显示维护说明，不声称“上次启动失败”；旧命令行 `recovery` 取消仅清理健康的这次启动尝试，真实故障记录仍按原规则保留。向导自身的安装、保存和完成语义见[配置向导](../../04-api-reference/ui-components/wizards.md)，不是恢复默认值功能。

搜索可见不意味着鉴权通过；这些命令行参数只是本机显式启动意图，不是远程维护授权或新进程健康凭据。纯委托测试可以验证取消、失败与执行顺序，但不能证明实际进程成功重启或设备状态健康。

## 插件加载

插件会在进入向导或启动窗口前决定是否加载。关键点：

| 动作 | 说明 |
| --- | --- |
| 扫描 | 扫描 `Plugins/` 目录 |
| 读取 | 读取 `manifest.json` 和可选 `.deps.json` |
| 校验 | 检查 `ColorVision.*` 依赖版本 |
| 装载 | 用 `Assembly.LoadFrom(...)` 装载插件程序集 |
| 恢复 | 上次启动未完成时，可在解析清单或依赖前按 ID/目录跳过，或读取持久禁用状态 |

## 主工作区对象

| 对象 | 作用 | 失败时表现 |
| --- | --- | --- |
| `ServiceManager` | 数据库连接可用后加载服务树，组织 `TypeServices`、`TerminalServices`、`DeviceServices`、`GroupResources` | 设备树为空、设备控件未生成 |
| `MqttRCService` | 注册、查询服务令牌并同步设备服务对象；[RC 契约](../../04-api-reference/engine-components/rc-registration.md)区分连接、快照和设备就绪 | 流程跑不起来、设备在线但状态不更新 |
| `TemplateControl` | 数据库连接可用后扫描已加载程序集中的 `IITemplateLoad`，调用 `Load()` 注册模板 | 模板不可见、模板不能编辑 |

模板是否可见依赖两个前提：相关程序集已经加载，数据库连接已建立。

## 流程执行链

当用户进入流程窗口后，运行时主链延伸为：

```text
DisplayFlow/ViewFlow -> FlowExecutionSession -> FlowControl -> FlowEngineLib
    -> EngineExecutionCompleted -> FlowRunFinalizer -> RunFinalized
```

`MqttRCService` 为节点图提供设备/算法 service token。执行过程中持续更新当前节点、日志、批次和消息；图引擎完成后仍要等待后处理最终化。

## 常见失败点

| 阶段 | 先查 |
| --- | --- |
| 启动 | 插件依赖、上次异常恢复分支、命令行分支是否提前返回 |
| 服务准备 | 数据库是否连通，服务树或模板是否装载，注册中心/MQTT 是否准备好 |
| 执行 | 流程模板是否选中，起始节点是否存在，设备状态是否已同步为可执行 |

## 代码入口

`StartupMaintenanceLifecycleTests` 覆盖一次性参数解析、恢复分支、确认和关窗取消、保存失败及关窗后的失败收尾；`StartupMaintenanceWindowTests` 验证合成 Owner/子窗口打开关闭与动作分流，`WizardWindowRuntimeTests` 和 `StartupRecoveryWindowRuntimeTests` 验证隔离向导及恢复窗口行为。`CopilotBackgroundShellMaintenanceGuardTests` 以延迟进程替身覆盖预留启动、运行和完成状态，确认门禁不读取输出。这些测试不启动真实维护进程、设备或更新还原，不能代替安装环境下的完整恢复验收。

| 主题 | 入口 |
| --- | --- |
| 主启动 | `ColorVision/App.xaml.cs` |
| 启动恢复 | `ColorVision/Recovery/`、`ColorVision/ProgramTimer.cs` |
| 插件加载 | `UI/ColorVision.UI/Plugins/PluginLoader.cs` |
| 服务树 | `Engine/ColorVision.Engine/Services/ServiceManager.cs` |
| 注册中心 | `Engine/ColorVision.Engine/Services/RC/MQTTRCService.cs` |
| 模板注册 | `Engine/ColorVision.Engine/Templates/TemplateControl.cs` |
