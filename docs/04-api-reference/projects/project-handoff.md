# 项目包交接手册

项目包不是普通工具插件。它把客户现场的测试顺序、FlowEngine 模板、设备动作、Recipe/Fix、Socket/MES 协议和结果导出揉成一套生产流程。交接时不要先从某个 Process 类单点看起，先把“谁触发、跑哪个流程、结果写到哪里、外部系统怎么拿结果”串起来。

如果你还不知道该项目属于哪类协议或输出方式，先看 [项目包能力与交接矩阵](./project-capability-matrix.md)。如果你已经遇到具体现场问题，先按 [项目包运行与交接场景手册](./project-package-playbook.md) 处理。发版、现场替换或回退时，按 [项目包发布证据与版本核查表](./project-release-evidence.md) 记录版本、配置、协议样例和回退包，再回到本页顺执行链读。

## 先判断项目类型

| 类型 | 典型目录 | 交接重点 |
| --- | --- | --- |
| AR/VR 流程组项目 | `ProjectARVRPro/`、`ProjectLUX/` | `ProcessGroup`、`ProcessMeta`、FlowTemplate、Recipe、Socket 协议 |
| 轻量或历史项目 | `ProjectARVR/`、`ProjectARVRLite/` | 保持兼容，修改前确认客户仍在使用的协议和导出字段 |
| 业务算法项目 | `ProjectBlackMura/`、`ProjectKB/` | 算法参数、结果模型、报告/导出字段 |
| 客户定制项目 | `ProjectHeyuan/`、`ProjectShiyuan/` | 客户协议、现场配置、菜单入口、依赖设备 |
| 对接示例 | `ProjectARVRPro.IntegrationDemo/` | 外部系统如何发 JSON、如何确认切图、如何接收结果 |

## 通用执行链

大多数项目包都遵循下面的链路。定位问题时按顺序查，不要直接跳到算法类。

| 步骤 | 代码入口 | 要确认的内容 |
| --- | --- | --- |
| 插件加载 | `manifest.json`、`PluginConfig/` | `Id`、`dllpath`、菜单名、窗口单例、最低宿主版本 |
| 初始化 | 主窗口 `InitTest()` | SN 如何写入配置，是否清空上一轮 `ObjectiveTestResult` |
| 选择流程 | `ProcessManager`、`ProcessGroup` | 当前激活组、启用步骤、步骤顺序、旧配置是否迁移 |
| 绑定模板 | `ProcessMeta.FlowTemplate` | FlowEngine 模板名称是否能在 `TemplateFlow.Params` 中匹配 |
| 运行流程 | 主窗口 `RunTemplate()` 或 `RunAllAsync()` | 是否创建批次，是否执行预处理，超时和重试策略是什么 |
| 解析结果 | `IProcess.Execute(ctx)` | 从哪个 Engine 结果读数据，Recipe/Fix 怎么参与判定 |
| 聚合结果 | `ObjectiveTestResult` | 每个测试项写入哪个属性或动态集合 |
| 保存与导出 | `ViewResultManager`、CSV/XLSX/PDF exporter | SQLite 路径、CSV 路径、客户格式开关、文件名规则 |
| 外部响应 | `Services/SocketControl.cs` 或专用 handler | 文本协议/JSON 协议、状态码、最终结果事件 |

## 配置和持久化

项目包常见的配置有三层，交接时要区分清楚。

| 配置层 | 作用 | 常见文件或类 |
| --- | --- | --- |
| 项目全局配置 | SN、路径、重试次数、输出模式 | `Project*Config`、`ViewResultManagerConfig` |
| 流程配置 | 当前产品跑哪些步骤、每步用哪个模板 | `ProcessGroups.json`、旧版 `ProcessMetas.json` |
| 判定/修正配置 | 上下限、修正因子、客户规格 | `RecipeManager`、`FixManager`、`RecipeBase`、`FixConfig` |

`ProcessGroups.json` 是新流程组模型。它通常保存到 `%APPDATA%\ColorVision\Config\`，和项目结果 SQLite 在同一个配置根目录附近。旧项目可能还有 `ProcessMetas.json`，接手时要确认源码是否有迁移逻辑，避免现场升级后流程丢失。

## ProcessGroup 和 ProcessMeta

`ProcessGroup` 表示一套产品或场景的流程方案，内部是有序的 `ProcessMeta` 列表。`ProcessMeta` 是“单个测试步骤”的元数据，不是算法结果。

| 字段 | 含义 | 维护风险 |
| --- | --- | --- |
| `Name` | 给现场看的步骤名 | 改名会影响操作员识别，但通常不影响执行 |
| `FlowTemplate` | 绑定的 FlowEngine 模板名 | 名称匹配失败会导致流程无法启动 |
| `ProcessTypeFullName` | 绑定的 `IProcess` 实现类型 | 类名/命名空间变更会影响旧配置反序列化 |
| `IsEnabled` | 是否参与当前组执行 | 影响自动流程顺序和最终结果完整性 |
| `ConfigJson` | 单步骤行为配置 | 字段重命名会影响旧配置兼容 |
| `SocketCode` | LUX 文本协议步骤码 | 只在 `ProjectLUX` 这类按命令码查步骤的项目中使用 |
| `PictureSwitchConfig` | ARVRPro 执行前切图配置 | 串口、命令、期望返回值、延时都会影响节拍 |

新增步骤时，优先复制一个结构相近的 `Process` 子目录，并确认四件事：`IProcess` 实现可被反射发现，`FlowTemplate` 能匹配实际模板，Recipe/Fix 配置能在编辑窗口看到，结果能写入 `ObjectiveTestResult` 并导出。

## IProcess 扩展模式

`IProcess` 是项目包业务逻辑的核心接口。Engine 负责跑设备和算法，项目包的 `Process` 负责把算法结果变成客户要的判定。

| 方法 | 用途 |
| --- | --- |
| `Execute(IProcessExecutionContext ctx)` | 主业务入口，读取批次结果、应用 Recipe/Fix、写入 `ObjectiveTestResult` |
| `Render(ctx)` | 可选的结果可视化或图像叠加 |
| `GenText(ctx)` | 可选的文本摘要 |
| `GetRecipeConfig()` | 返回当前测试项的限值配置 |
| `GetFixConfig()` | LUX 中返回修正因子配置 |
| `GetProcessConfig()` / `SetProcessConfig()` | 返回和恢复单步骤配置，通常序列化到 `ConfigJson` |
| `CreateInstance()` | `ProcessManager` 复制或加载步骤时创建同类型实例 |

不要把客户项目的判定逻辑写回 Engine 通用层。Engine 的模板和设备结果应保持通用，客户规格、阈值、返工码、导出格式放在项目包。

## Socket 协议分型

| 项目 | 协议风格 | 入口 | 说明 |
| --- | --- | --- | --- |
| ProjectLUX | 文本命令，例如 `T00XX,SN;` | `Services/SocketControl.cs` | `XX` 通过 `SocketCode` 找到当前流程组里的步骤 |
| ProjectARVRPro | JSON 事件 | `Services/SocketControl.cs`、`RunAllSocket.cs`、`SwitchGroupSocket.cs` | `EventName` 分发到 handler，支持 `ProjectARVRInit`、`SwitchPGCompleted`、`SwitchGroup`、`RunAll` |
| ProjectARVRPro AOI | JSON + 中转服务器 | `SocketRelay/` | Flow 与外部 Client 之间通过 9200 端口中转 AOI 切图 |
| IntegrationDemo | 客户端示例 | `ProjectARVRPro.IntegrationDemo/` | 用来验证外部系统对 ARVRPro 协议的理解 |

修改协议时要同步三处：项目 docs 页、项目根目录 README 或协议手册、客户现场对接文档。只改代码会让下一次交接非常痛苦。

## 结果交付链

项目包通常会产生两类结果：

| 结果 | 作用 | 常见位置 |
| --- | --- | --- |
| 过程结果 | 单个 Flow 执行状态、批次号、模板名、耗时 | `Project*Reuslt`、SQLite |
| 客户结果 | 一轮测试的聚合判定和导出字段 | `ObjectiveTestResult`、CSV/XLSX/PDF、Socket `Data` |

排查“结果不对”时先确认当前 Flow 是否完成，再看 `IProcess.Execute()` 是否被匹配到，最后看 exporter 是否使用了旧版/新版输出模式。不要只盯 CSV 文件，很多问题发生在 ProcessMeta 匹配之前。

## 交接检查清单

| 检查项 | 通过标准 |
| --- | --- |
| manifest | `Id`、`dllpath`、`requires` 和包名一致 |
| 菜单入口 | 主程序 Tools 或功能区能打开项目窗口 |
| 流程组 | 至少有一个有效组，当前组里关键步骤已启用 |
| 模板绑定 | 每个 `FlowTemplate` 都能匹配模板列表 |
| Recipe/Fix | 编辑窗口能打开，保存后重启仍在 |
| Socket | 外部命令能初始化、切图确认、跑完并返回结果 |
| 结果 | SQLite、CSV/XLSX/PDF 或客户系统上传路径可写 |
| 兼容 | 旧配置、旧导出、旧协议开关有明确说明 |

## 推荐阅读顺序

1. [项目包能力与交接矩阵](./project-capability-matrix.md)，先判断协议、输出和验收重点。
2. 当前项目 docs 页，例如 [ProjectARVRPro](./project-arvr-pro.md) 或 [ProjectLUX](./project-lux.md)。
3. 项目根目录 README，确认源码侧最新约定。
4. `PluginConfig/`，确认插件入口和窗口单例。
5. 主窗口 `.xaml.cs`，顺着 `InitTest`、`RunTemplate`、`Processing` 或 `RunAllAsync` 读。
6. `Process/ProcessManager.cs`、`ProcessMeta.cs`、`IProcess.cs`。
7. 具体测试项 `Process/<TestName>/`。
8. `Recipe/`、`Fix/`、`ObjectiveTestResult.cs`、`ViewResultManager.cs`。
9. `Services/` 或 `SocketRelay/`，最后核对客户协议。
