# 项目说明

本章先从业务项目进入，而不是先从源码目录进入。`Projects/` 下的内容通常是客户项目、方案包或对接示例，它们会把 Engine、Flow、模板、设备、Socket/MES 协议和结果导出组合成一套可交付流程。

第一次接手时，建议先读本页建立项目地图，再进入 [项目包能力与交接矩阵](../04-api-reference/projects/project-capability-matrix.md)、[项目包运行与交接场景手册](../04-api-reference/projects/project-package-playbook.md) 和 [项目包发布证据与版本核查表](../04-api-reference/projects/project-release-evidence.md)。如果要理解通用执行链，再看 [项目包交接手册](../04-api-reference/projects/project-handoff.md)，最后看具体项目页。

## 当前项目地图

| 项目 | 业务定位 | 先看 |
| --- | --- | --- |
| ProjectARVR | AR/VR 综合光学测试项目，重点看固定 PG 切图顺序、Socket 事件和 ObjectiveTestResult 汇总 | [ProjectARVR](../04-api-reference/projects/project-arvr.md) |
| ProjectARVRLite | AR/VR 轻量快速测试项目，重点看可配置测试类型、预处理、Socket 切图和结果 CSV | [ProjectARVRLite](../04-api-reference/projects/project-arvr-lite.md) |
| ProjectARVRPro | AR/VR 专业流程组、Recipe、Socket 对接项目 | [ProjectARVRPro](../04-api-reference/projects/project-arvr-pro.md) |
| ProjectARVRPro.IntegrationDemo | 面向客户或上位机的 TCP/JSON 对接示例 | [ARVRPro 对接 Demo](../04-api-reference/projects/project-arvr-pro-integration-demo.md) |
| ProjectBlackMura | 显示面板 Black Mura 检测项目，重点看 PG 串口切图、五色流程和 Excel 报告 | [ProjectBlackMura](../04-api-reference/projects/project-black-mura.md) |
| ProjectHeyuan | 河源精电客户定制测试项目，重点看 STX/ETX 串口、四点 WBRO 测试和 CSV 上传链路 | [ProjectHeyuan](../04-api-reference/projects/project-heyuan.md) |
| ProjectKB | 键盘背光亮度和均匀性测试项目，重点看 Modbus 自动触发、MES DLL、背光自动修正和 CSV/summary | [ProjectKB](../04-api-reference/projects/project-kb.md) |
| ProjectLUX | LUX 亮度、色彩、MTF、畸变自动化测试项目 | [ProjectLUX](../04-api-reference/projects/project-lux.md) |
| ProjectShiyuan | 视源客户定制测试项目，重点看 JND/POI 结果导出和固定图像后处理边界 | [ProjectShiyuan](../04-api-reference/projects/project-shiyuan.md) |

## 项目说明应该回答什么

每个项目页都应优先说明业务闭环，而不是只列文件：

| 问题 | 文档必须说明 |
| --- | --- |
| 这个项目解决什么现场问题 | 客户场景、测试对象、入口窗口、主要流程 |
| 外部系统怎么触发 | Socket、MES、串口、Modbus 或本地按钮入口 |
| 运行时怎么组织步骤 | `Process/`、流程组、Recipe/Fix、模板绑定 |
| 结果怎么判定和导出 | PASS/FAIL、CSV/XLSX/PDF、SQLite、Socket 返回字段 |
| 交付时要带什么 | manifest、README、CHANGELOG、配置、图片资源、依赖 DLL |
| 维护时最容易出错哪里 | 协议字段、旧格式兼容、流程顺序、结果字段、项目配置 |

## 推荐阅读顺序

1. [项目包能力与交接矩阵](../04-api-reference/projects/project-capability-matrix.md)：先横向确认每个项目的触发方式、结果出口和交付风险。
2. [项目包运行与交接场景手册](../04-api-reference/projects/project-package-playbook.md)：遇到外部触发、流程组、模板、Recipe/Fix、导出或打包问题时按场景处理。
3. [项目包发布证据与版本核查表](../04-api-reference/projects/project-release-evidence.md)：发版、现场替换或回退时记录 manifest、DLL、`.cvxp`、配置、协议和结果样例。
4. [项目包交接手册](../04-api-reference/projects/project-handoff.md)：理解所有项目包共用的装载、流程、配置和打包链路。
5. [项目包总览](../04-api-reference/projects/README.md)：确认当前仓库真实存在的项目和 manifest 信息。
6. 具体项目页：从业务协议、流程组、结果导出和维护风险开始读。
7. [Engine 业务链路矩阵](../04-api-reference/engine-components/business-flow-matrix.md)：当项目调用到设备、模板、Flow 或结果展示时再深入 Engine。
8. [现有插件能力说明](../04-api-reference/plugins/README.md)：需要区分通用工具插件和客户项目包时再对照查看。

## 项目包和通用插件的边界

| 类型 | 位置 | 目标 |
| --- | --- | --- |
| 项目包 | `Projects/<Name>/` | 交付客户业务流程，通常包含流程组、Recipe、协议和结果导出 |
| 通用插件 | `Plugins/<Name>/` | 提供可复用工具能力，例如光谱仪、系统监控、事件查看 |
| Engine 模块 | `Engine/` | 提供设备、模板、Flow、MQTT、数据和结果展示的通用能力 |
| UI 模块 | `UI/` | 提供可复用界面组件、主题、窗口、属性编辑器和图像编辑器 |

如果一个改动只服务于某个客户流程，优先放在项目包；如果多个项目都会复用，才考虑沉到 Engine、UI 或通用插件。

## 维护要求

- 新增 `Projects/<Name>/` 后，必须补本章索引、项目包总览和具体项目页。
- 修改项目触发协议、结果出口、验收方式或交付内容时，同步更新 [项目包运行与交接场景手册](../04-api-reference/projects/project-package-playbook.md)、[项目包能力与交接矩阵](../04-api-reference/projects/project-capability-matrix.md) 和 [项目包发布证据与版本核查表](../04-api-reference/projects/project-release-evidence.md)。
- 修改 Socket/MES 字段、流程组、Recipe/Fix、导出字段或 manifest 时，同步更新对应项目页。
- 项目页要写清业务流程和交付风险，不能只写“包含若干窗口和服务”。
- 历史客户口头流程不要写成当前系统承诺；文档必须能回到源码、配置或 manifest。
