---
knowledge_id: "operations.index"
knowledge_type: "topic"
status: "current"
summary: "从启动、配置、日志、设备、流程和结果现象定位代码责任，区分已完成阶段与待验证阶段，避免用重启或改数据代替诊断。"
aliases: ["操作入口", "使用手册", "操作工作流", "我该看哪里", "运行故障归属", "常见问题", "启动失败", "故障排查"]
code_paths: ["ColorVision/MainWindow.xaml.cs", "ColorVision/Recovery", "UI/ColorVision.UI/ConfigHandler.cs", "UI/ColorVision.UI/LogImp", "Engine/ColorVision.Engine/FlowProcessing/Runtime", "UI/ColorVision.SocketProtocol"]
test_paths: []
related: ["platform.system", "ui.configuration", "operations.logs", "ui.discovery", "engine.results", "flow.session", "operations.data", "operations.exports", "operations.acceptance"]
---

# 跨模块运行问题定位

运行问题按代码责任分流，不按操作员、开发者或维护者安排阅读路线。先确认“哪个阶段已完成、下一阶段由谁负责”，再读取该主题的行为、实现和测试；不要仅凭窗口位置、方法名称或一句“成功”判断实际结果。

完整源码关联由[知识地图](../knowledge/index.md)生成。本页是跨模块诊断契约，不维护第二份组件目录；具体失败规则由对应主题负责。

## 从现象定位责任边界

| 现象 | 必须先区分 | 对应知识 |
| --- | --- | --- |
| 程序打不开，或普通图片打开失败 | 主程序初始化、依赖缺失、恢复状态，还是独立图片打开分支 | [启动与最小运行验证](../00-getting-started/first-steps.md) |
| 菜单、设置项或工具没有出现 | 程序集未加载、扩展未发现，还是权限/配置/目标窗口过滤 | [UI 发现链](../04-api-reference/ui-components/ui-runtime-handoff.md)、[插件装载](../02-developer-guide/plugin-development/overview.md) |
| 参数改了但行为未变，或保存后又丢失 | 编辑目标、工作副本、内存发布、文件落盘、重载绑定和远端应用分别核对 | [PropertyGrid](../04-api-reference/ui-components/property-grid.md)、[软件配置](../04-api-reference/ui-components/configuration.md)、[设备配置](./devices/configuration.md) |
| 日志中没有错误，或切换筛选后内容消失 | 来源是否输出、文件是否留存、历史读取范围和窗口筛选，不先假定动作没发生 | [日志来源与显示](./interface/log-viewer.md) |
| 设备在列表中但命令失败 | 资源身份、实例装配、通信与真实动作是不同阶段 | [设备装配](../04-api-reference/engine-components/device-service-chain.md)、[相机](./devices/camera.md)、[物理相机](./devices/camera-management.md) |
| 流程开始后没有完成 | 节点结束、引擎执行结束与整轮后处理结束不等价 | [流程执行与最终化](./workflow/execution.md) |
| JSON 或模板校验失败 | 原始 JSON 格式、模板 schema/版本、编辑模式与实际保存对象，而非笼统“参数不支持” | [JSON 模板](../04-api-reference/algorithms/templates/json-templates.md)、[流程模板与包](../04-api-reference/engine-components/template-flow-chain.md) |
| 图像存在但标注缺失或坐标不对 | Engine 历史结果、中立算法 overlay 和客户项目结果使用不同处理链 | [结果展示边界](../04-api-reference/engine-components/result-handoff-chain.md) |
| 结果存在但数据库/导出为空 | 查询对象、批次、来源路径和写入完成状态；导出不是统一数据中心 | [存储所有者](./data-management/README.md)、[导入导出](./data-management/export-import.md) |
| TCP 已连接但项目没有返回结果 | 通道连通、命令接收与业务最终响应不同；协议字段由项目 handler 定义 | [Socket 基础设施](../04-api-reference/ui-components/ColorVision.SocketProtocol.md)、[项目协议](../04-api-reference/projects/README.md) |
| 更新后应用重开但功能仍旧 | 下载、目录替换、重新启动和新程序集加载是不同证据 | [插件产物与恢复](../02-developer-guide/plugin-development/getting-started.md)、[更新机制](../02-developer-guide/deployment/auto-update.md) |

## 诊断前收集的最小证据

- 当前可执行文件位置、主程序/插件/项目包版本、配置路径及模板身份，先排除不同安装实例或不同数据源。
- 具体输入和触发入口；流程或结果问题使用同一轮的设备 Code、SN、批次、主记录 ID 和时间，不混用历史记录。
- 最后已确认完成的阶段、首个可观察失败、原始错误信息及实际输出。进度条、端口连通、文件存在、窗口重开都不能单独证明业务完成。
- 已经执行过的动作及其副作用。保留变更前证据；日志、配置、客户样本、连接字符串和反馈包在分享前按内容脱敏。

## 只读排查不等于“点一次试试”

默认先读源码、已有配置、已有日志和对应测试；需要真实运行时证据时，再确认本次任务是否允许访问目标实例。入口方法可能初始化目录、建表、触发服务装配、改全局日志输出等级或加载插件代码。

不能把“重新保存配置”“重载设备”“提高全局日志等级”“重新触发流程”“重启服务”“删除记录后重试”当作通用排障步骤。先到责任主题判断该动作会修改什么、是否有旧状态引用、如何确认结果，以及是否需要新的授权或隔离数据。

软件配置的保存与内存发布、浏览器的多行写入、文件生成与外部协议都可能处于不同的部分成功状态。失败后先核对实际持久化结果；没有对应源码保证时，不假设自动回滚，也不盲目重复执行。

## 从诊断到验证

需要验收时，从目标主题选择最小验证，并按[现场证据规范](./field-operation-acceptance.md)记录输入、观察和未覆盖事项。能定位失败只说明诊断路径有效，不代表业务验收通过；本页没有声明跨模块自动化测试覆盖。

如果现有主题不足以回答，沿其源码入口收窄到具体分支并补充那份规范主题。新增知识应说明条件、结果与验证缺口，而不是再增加一个面向另一种读者的“常见问题”副本。
