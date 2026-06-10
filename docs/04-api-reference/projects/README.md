# 项目包总览

`Projects/` 目录放的是客户项目、业务方案包和对接示例。顶层阅读入口见 [项目说明](../../00-projects/README.md)。本页作为项目包总览，继续保留当前项目清单、构建打包和维护规则。

接手项目包时先看 [当前项目文档覆盖清单](./current-project-coverage.md) 和 [项目包能力与交接矩阵](./project-capability-matrix.md)，再按 [项目包运行与交接场景手册](./project-package-playbook.md) 处理具体问题。发版、现场替换或回退时，按 [项目包发布证据与版本核查表](./project-release-evidence.md) 保存版本、包内容、配置和验收证据，再看 [项目包交接手册](./project-handoff.md) 和具体项目页。

## 当前项目总览

| 项目 | 源码目录 | manifest Id | 业务定位 | 文档 |
| --- | --- | --- | --- | --- |
| ProjectARVR | `Projects/ProjectARVR/` | `ProjectARVR` | 固定 PG 切图顺序、Socket 事件、ObjectiveTestResult 汇总 | [ProjectARVR](./project-arvr.md) |
| ProjectARVRLite | `Projects/ProjectARVRLite/` | `ProjectARVRLite` | 可配置测试类型、预处理、Socket 切图、结果 CSV | [ProjectARVRLite](./project-arvr-lite.md) |
| ProjectARVRPro | `Projects/ProjectARVRPro/` | `ProjectARVRPro` | AR/VR 专业流程组、Recipe、Socket 对接 | [ProjectARVRPro](./project-arvr-pro.md) |
| ProjectARVRPro.IntegrationDemo | `Projects/ProjectARVRPro.IntegrationDemo/` | 无 manifest | 面向客户的 TCP/JSON 对接示例 | [Integration Demo](./project-arvr-pro-integration-demo.md) |
| ProjectBlackMura | `Projects/ProjectBlackMura/` | `ProjectBlackMura` | PG 串口切图、五色流程、Excel 报告 | [ProjectBlackMura](./project-black-mura.md) |
| ProjectHeyuan | `Projects/ProjectHeyuan/` | `ProjectHeyuan` | STX/ETX 串口、WBRO 四点测试、CSV 上传 | [ProjectHeyuan](./project-heyuan.md) |
| ProjectKB | `Projects/ProjectKB/` | `ProjectKB` | Modbus 自动触发、MES DLL、背光自动修正、CSV/summary | [ProjectKB](./project-kb.md) |
| ProjectLUX | `Projects/ProjectLUX/` | `ProjectLUX` | LUX 亮度/色彩/MTF/畸变自动化测试 | [ProjectLUX](./project-lux.md) |
| ProjectShiyuan | `Projects/ProjectShiyuan/` | `ProjectShiyuan` | JND/POI 结果导出、固定图像后处理 | [ProjectShiyuan](./project-shiyuan.md) |

## 推荐阅读路径

| 目标 | 入口 |
| --- | --- |
| 横向比较所有项目包能力 | [项目包能力与交接矩阵](./project-capability-matrix.md) |
| 确认每个真实项目都有文档 | [当前项目文档覆盖清单](./current-project-coverage.md) |
| 按现场问题处理项目包 | [项目包运行与交接场景手册](./project-package-playbook.md) |
| 项目 `.cvxp` 发版、替换、回退 | [项目包发布证据与版本核查表](./project-release-evidence.md) |
| 第一次接手客户项目 | [项目包交接手册](./project-handoff.md) |
| 理解 ARVRPro 自动化对接 | [ProjectARVRPro](./project-arvr-pro.md) -> [Integration Demo](./project-arvr-pro-integration-demo.md) |
| 理解 LUX 文本协议和 SocketCode | [ProjectLUX](./project-lux.md) |
| 查当前所有项目入口 | 本页项目总览 |
| 查代码位置和模块对应关系 | [模块与文档对照表](../../05-resources/project-structure/module-documentation-map.md) |

## 项目包和插件的区别

项目包通常也有 `manifest.json`，也会复制到主程序 `Plugins/<Name>/`。但它们的核心不是“提供一个通用工具”，而是把 Engine、Flow、模板、算法结果和客户协议组织成一套生产流程。

| 模块 | 典型内容 | 为什么重要 |
| --- | --- | --- |
| 插件集成层 | `manifest.json`、`PluginConfig/`、菜单入口、窗口单例 | 决定项目能否被宿主发现和打开 |
| 流程组织层 | `ProcessManager`、`ProcessGroup`、`ProcessMeta` | 决定当前产品跑哪些步骤、按什么顺序跑 |
| Engine 绑定层 | `FlowTemplate`、`TemplateFlow.Params`、FlowEngine | 决定项目步骤对应哪条可视化流程 |
| 业务判定层 | `IProcess.Execute()`、`Recipe/`、`Fix/` | 决定算法结果如何变成 PASS/FAIL 和客户字段 |
| 通信层 | `Services/SocketControl.cs`、Socket handler、MES/串口 | 决定外部系统如何触发和拿结果 |
| 结果层 | `ObjectiveTestResult`、`ViewResultManager`、Exporter | 决定本地留痕、CSV/XLSX/PDF 和 Socket 输出 |

## 维护边界

| 变更类型 | 优先改哪里 | 不建议 |
| --- | --- | --- |
| 新增客户测试项 | 对应项目的 `Process/`、`Recipe/`、`ObjectiveTestResult` | 把客户规格写进 `Engine/` 通用模板 |
| 新增或调整流程顺序 | `ProcessGroup` / `ProcessMeta` 配置 | 直接改 UI 中的临时代码绕过流程组 |
| 外部系统协议调整 | 项目 `Services/` 或 `SocketRelay/` | 只改协议手册不改 docs 站点 |
| 结果字段变更 | `ObjectiveTestResult` 和 exporter | 只改 CSV，忘记 Socket `Data` 或 SQLite 查询 |
| 兼容旧客户 | 配置开关或 converter，例如 ARVRPro Legacy 输出 | 删除旧字段导致现场旧程序无法解析 |

## 构建和打包

单独构建：

```powershell
dotnet build Projects/ProjectLUX/ProjectLUX.csproj -c Release -p:Platform=x64
```

生成 `.cvxp` 包：

```powershell
Scripts\package_project.bat ProjectLUX --no-upload
```

`package_project.bat` 和插件打包流程相同，底层调用 `Scripts/package_cvxp.py`，会把项目输出和项目根目录的 README、CHANGELOG、manifest、PackageIcon 一起打包。

## 交接时优先看什么

| 目标 | 优先看 |
| --- | --- |
| 理解客户业务流程 | [项目包能力与交接矩阵](./project-capability-matrix.md)、[项目包交接手册](./project-handoff.md)、项目 README、主窗口、`Process/` 目录 |
| 理解判定规则 | `Recipe/`、`Fix/`、项目配置类 |
| 理解自动化对接 | `Services/SocketControl.cs`、Modbus/MES/Serial 相关类 |
| 理解结果落库和导出 | `ObjectiveTestResult.cs`、`ViewResultManager.cs`、结果窗口 |
| 理解菜单入口 | `PluginConfig/` 或项目根目录的 `Menu*.cs` |

## 维护要求

- 每个 `Projects/<Name>/` 都必须有项目 README 和 docs 站点对应页。
- 修改项目协议、结果出口、流程组织或交付验收时，同步更新 [项目包运行与交接场景手册](./project-package-playbook.md)、[项目包能力与交接矩阵](./project-capability-matrix.md) 和 [项目包发布证据与版本核查表](./project-release-evidence.md)。
- 修改 manifest、菜单名、Socket 事件、Recipe 字段或导出字段时，同步更新对应项目页。
- 项目页只写当前源码能对上的内容，客户历史口头流程不写成当前系统承诺。
- 项目包页面优先写业务链、配置文件、协议、导出和排查路径；不要停留在目录列表。
