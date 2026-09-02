---
knowledge_id: "operations.acceptance"
knowledge_type: "guide"
status: "current"
summary: "记录设备、流程、数据和外部系统的现场验收证据，区分自动化测试与真机结果。"
aliases: ["现场验收","交付检查","真机验证"]
code_paths: ["Engine/ColorVision.Engine/Services/Devices","Engine/ColorVision.Engine/FlowProcessing/Runtime"]
test_paths: []
related: ["engine.devices","delivery.testing","operations.exports"]
---

# 现场操作验收清单

本页描述人工确认条件后的验收，不自动授予AI设备控制、通电、运动或远端写入权限。真实设备验收不能由文档校验、模拟测试或既有日志替代；只读任务应给出计划和缺口，不直接执行下表。

这页用于首次交付、版本升级、现场复测或培训操作员时逐项确认 ColorVision 是否“能用”。它不替代具体设备、流程、项目或插件页面，而是把 UI、设备、流程、数据、外部系统和交付证据串成一张现场验收表。

按[跨模块运行问题定位](./README.md)确认责任边界，再选择本次交付涉及的项目、设备与输出。某项验收失败时进入对应代码主题，不要求按角色通读手册。

## 使用方式

先记录主程序版本、项目包、插件包和配置目录，再按本页顺序做最小验收。每项都记录通过结果、失败现象、日志时间点和回退方式；涉及项目包或插件替换时，同时记录包版本、结果样例和回退包位置。

## 验收总表

| 验收项 | 最小动作 | 通过标准 | 失败先看 |
| --- | --- | --- | --- |
| 主程序启动 | 启动 ColorVision，打开主窗口 | 主窗口、菜单、状态栏、日志入口可见 | [主窗口导览](./interface/main-window.md)、[日志查看器](./interface/log-viewer.md) |
| UI 组件入口 | 打开本次交付实际需要的设置、日志、数据库、Socket、调度或插件市场窗口 | 目标入口可见且无启动级错误；窗口出现不证明相关服务或业务完成 | [UI 组件与源码目录](../04-api-reference/ui-components/control-catalog.md) |
| 配置保存 | 在获准的隔离配置上修改、保存并重启 | 重启后值仍在，相关服务状态正确；先确认编辑会话模式，关闭窗口不一定撤销改值 | [PropertyGrid 编辑与持久化](../04-api-reference/ui-components/property-grid.md) |
| 设备服务 | 检查本次实际装配的设备与授权功能；不以有工厂推断默认显示或支持某项操作 | 资源、运行实例、显示页、在线状态和获准动作分别有证据 | [设备装配契约](../04-api-reference/engine-components/device-service-chain.md)、具体设备页 |
| 相机取图 | 连接相机，拍一张图或打开实时预览 | 图像生成并能在图像编辑器打开 | [相机服务](./devices/camera.md)、[ImageEditor](../04-api-reference/ui-components/ColorVision.ImageEditor.md) |
| 流程设计 | 打开现场流程模板，确认起始节点和关键节点参数 | 流程能保存、重新打开，设备/模板绑定正确 | [Flow 编辑工作区](./workflow/design.md) |
| 流程执行 | 在已确认安全的环境中运行目标最小流程或项目流程 | 同一轮最终状态与预期一致，结果可追踪；能定位失败节点仅证明诊断有效，不算业务通过 | [Flow 执行会话与最终化](./workflow/execution.md) |
| 图像与 overlay | 打开一张结果图并查看 ROI/POI/overlay | 图像、图层和坐标对齐 | [ImageEditor](../04-api-reference/ui-components/ColorVision.ImageEditor.md)、[Engine 结果展示链路](../04-api-reference/engine-components/result-handoff-chain.md) |
| 数据落库 | 按 SN、时间或批次查一条结果 | SQLite/MySQL 有对应记录，字段基本完整 | [数据库连接与 DAO 契约](../04-api-reference/ui-components/ColorVision.Database.md) |
| 文件导出 | 导出 CSV/Excel/图片或项目结果 | 文件存在、字段顺序和客户格式正确 | [数据导出与导入](./data-management/export-import.md)、项目页 |
| Socket/MES/Modbus | 发送现场最小命令或联机样例 | 外部系统能触发并收到正确状态码/数据 | [SocketProtocol](../04-api-reference/ui-components/ColorVision.SocketProtocol.md)、[项目包总览](../04-api-reference/projects/README.md) |
| 插件能力 | 打开现场插件并执行最小功能 | 插件菜单、窗口、设备连接、结果或导出正常 | [现有插件能力说明](../04-api-reference/plugins/README.md) |
| 项目包流程 | 打开客户项目，输入 SN，运行最小流程 | 客户结果、文件、Socket/MES 返回符合项目页 | [项目说明](../04-api-reference/projects/README.md) |
| 回退材料 | 找到上一版包、配置和数据库备份 | 现场可退回到上一套可运行状态 | 插件或项目包总览、现场记录 |

## 设备验收

设备验收不要只看“列表里有”。要证明设备能被使用，并能被流程或项目包引用。

| 检查项 | 通过标准 |
| --- | --- |
| 设备资源 | 关键设备已经创建，名称和 Code 能区分现场真实设备 |
| 通信参数 | IP、端口、串口、波特率、设备号、文件路径与现场一致 |
| 最小动作 | 只执行本次获准且符合现场安全条件的动作，记录请求与实际响应；电机回零、SMU 通电、文件上传不是默认无风险检查 |
| 流程引用 | 流程节点或项目窗口能选到正确设备 |
| 日志证据 | 连接、超时、驱动、权限错误已经处理或记录 |

如果手动设备页能操作，但流程里失败，优先查流程节点绑定和模板参数；如果设备页也失败，优先查硬件、驱动、端口/IP 和服务配置。

## 流程验收

流程验收只证明当前版本能稳定进入、执行、定位失败、查看结果。确认当前 Flow 模板名称符合现场要求，有起始节点，关键设备/模板/图片/SN 能读取，失败时能找到第一个失败节点和对应日志，完成后结果列表、图像、数据库或导出文件能找到同一轮记录。

## 数据和导出验收

| 交付物 | 验收方式 | 重点 |
| --- | --- | --- |
| SQLite/MySQL | 按 SN、时间、批次查询 | 批次、模板、结果字段是否匹配 |
| CSV/Excel | 打开文件核对字段和单位 | 字段顺序、PASS/FAIL、客户旧格式兼容 |
| 图片/overlay | 打开结果图并查看标注 | 点位、框线、图层和原图坐标 |
| Socket/MES 返回 | 保存请求和响应样例 | 状态码、错误信息、`Data` 字段 |
| Summary/文本 | 核对产量、良率、失败项 | 目录、文件名、模型分组、统计口径 |

如果导出为空，不要只重试导出按钮。先确认源数据是否已经落库、当前查看的批次和导出对象是否一致，再查项目 exporter 或字段映射。

## 外部系统验收

外部系统联调至少保留一组原始请求和响应。不同项目的协议不同，不能用一个项目的命令去验另一个项目。

| 类型 | 最小证据 |
| --- | --- |
| JSON Socket | `EventName`、SN、请求 JSON、响应 JSON、窗口状态 |
| 文本 Socket | 原始命令，例如 `T00XX,SN;`，返回码和数据 |
| MES/串口 | STX/ETX 原始报文、设备号、返回码、超时 |
| Modbus | IP、端口、寄存器地址、触发值、完成写回值 |
| 文件服务器 | 请求路径、返回文件列表、下载/上传目标路径 |

外部系统能连上但业务没跑时，先查项目窗口是否打开、当前流程组是否正确、SN 是否有效、项目 handler 是否加载。

## 交付记录模板

```text
site/customer:
host version:
project package:
plugin package:
config folder:
device smoke result:
workflow smoke result:
image/overlay result:
database query result:
export file sample:
external protocol sample:
known failures:
rollback package/config:
operator trained:
owner/date:
```
