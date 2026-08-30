---
generated_knowledge_index: true
search: false
editLink: false
prev: false
next: false
---

# Engine 源码知识

> 自动生成的源码目录。修改主题 Markdown 的 `code_paths` 后运行 `node docs/.vitepress/scripts/knowledge.mjs generate`；不要手工编辑。

返回[知识总入口](../index.md)。只读与当前模块有关的主题，再核对其中的源码、测试和状态；`规划`、`历史`不代表当前能力。

以下是已声明源码路径的关联，不是完整调用图或完整模块清单。跨模块主题可出现在多处；根目录概览只列在根目录项，不自动覆盖所有子模块。

## Engine/ColorVision.Engine {#module-456e67696e652f436f6c6f72566973696f6e2e456e67696e65}

- [算法与模板知识入口](../../04-api-reference/algorithms/README.md) — `algorithms.index`
  区分统一 Runner、ImageEditor 直接 native 分析与 Engine 模板/MQTT 算法，并按任务定位专题。

- [ROI](../../04-api-reference/algorithms/primitives/roi.md) — `algorithms.roi-routes`
  区分发光区定位、JSON 裁剪、SFR 找 ROI 与统一算法 ROI 数据模型。

- [Engine 模板共享构件](../../04-api-reference/algorithms/primitives/common-modules.md) — `algorithms.template-primitives`
  路由 Engine 模板中的 ROI、POI、Matching 共享构件并区分统一算法平台。

- [Engine 知识入口](../../04-api-reference/engine-components/README.md) — `engine.index`
  按实际代码职责路由 Engine 的设备、消息、模板、Flow、结果与工程依赖；契约和验证由各主题维护。

- [Flow 节点检索入口](../../04-api-reference/flow_nodes_summary.md) — `flow.index`
  按节点用途与执行归属定位 FlowEngineLib、Engine 本地节点和属性编辑器。

- [数据所有者与存储定位](../../01-user-guide/data-management/README.md) — `operations.data`
  按设置JSON、Engine MySQL、模块SQLite和结果文件定位数据所有者；有记录、有图片、已导出和已备份不是同一状态。

- [架构设计](../../03-architecture/README.md) — `platform.architecture`
  按启动、跨模块调用、流程、模板与权限问题定位架构契约。

- [扩展任务入口](../../04-api-reference/extensions/README.md) — `platform.extensions`
  按 Flow 节点、属性编辑器、模板、设备和插件问题定位可复用扩展契约。

- [DataLoad 数据加载模板](../../04-api-reference/algorithms/templates/data-load-template.md) — `algorithms.data-load`
  区分 DataLoad 模板与显式参数节点如何按设备、批次和 ZIndex 读取上游结果。

- [FindLightArea 发光区定位模板](../../04-api-reference/algorithms/templates/find-light-area.md) — `algorithms.find-light-area`
  区分远端 FindLightArea 模板与本地原生亮区检测 RobustV2；四角点不等于成功，须核对置信度、失败原因和各调用层的结果契约。

- [FocusPoints 关注点模板](../../04-api-reference/algorithms/templates/focus-points-template.md) — `algorithms.focus-points`
  说明 FocusPoints 传统模板参数、通用手动宿主与 Flow 发光区检测请求。

- [Ghost Detection](../../04-api-reference/algorithms/detectors/ghost-detection.md) — `algorithms.ghost`
  说明 ARVR Ghost 传统模板的参数、MQTT 事件、结果 DAO 和叠图。

- [ImageCropping 图像裁剪模板](../../04-api-reference/algorithms/templates/image-cropping-template.md) — `algorithms.image-cropping`
  区分强类型 ImageCropping 的持久参数、运行时四点 ROI、Flow 双输入和图像结果。

- [Matching 模板匹配](../../04-api-reference/algorithms/templates/matching-template.md) — `algorithms.matching`
  说明 Matching 通用配置宿主、运行时模板文件、Flow 请求和 AOI 结果绘制。

- [统一图像算法平台 V1](../../02-developer-guide/core-concepts/image-algorithm-platform-v1.md) — `algorithms.platform`
  统一图像算法Catalog、Invocation和Runner；普通像素预览、应用/取消、所有权与发布门禁；ONNX仅设计。

- [POI](../../04-api-reference/algorithms/primitives/poi.md) — `algorithms.poi-routes`
  说明 POI 点位、伴生模板、文件模式与 Flow 和 JSON 算法的消费关系。

- [模板编辑与创建宿主](../../04-api-reference/algorithms/templates/template-management.md) — `algorithms.template-management`
  TemplateEditorWindow与TemplateCreateView的共享参数、创建来源、预览、索引和关闭语义；关闭不是通用回滚，筛选后的操作目标需单独核对。

- [模板编辑入口与菜单契约](../../04-api-reference/algorithms/templates/template-menu-entries.md) — `algorithms.template-menus`
  区分现存模板主菜单、专用入口与通用算法配置中的模板编辑命令。

- [算法与模板接入概览](../../04-api-reference/algorithms/overview.md) — `algorithms.template-overview`
  说明 Engine 模板发现、手动算法宿主、MQTT 请求和 Flow 接入链。

- [Copilot 扩展、MCP 与 Hook](../../02-developer-guide/core-concepts/copilot-agent-extensions.md) — `copilot.extensions`
  业务模块动态上下文、外部 MCP client 和 Hook 如何进入统一宿主权限与生命周期。

- [Copilot 工具契约](../../02-developer-guide/core-concepts/copilot-agent-tool-contracts.md) — `copilot.tool-contracts`
  Copilot 工具结果、事件、审批恢复和 Flow 编辑必须遵守的执行契约。

- [数据库清理窗口、能力接入与完成边界](../../04-api-reference/engine-components/database-maintenance.md) — `engine.database-maintenance`
  数据库清理窗口与provider能力：表统计不是删除预览，确认只固定部分参数；备份默认关闭、组合维护不是事务，关窗不取消，成功与统计刷新分开。

- [Engine 设备资源与运行装配](../../04-api-reference/engine-components/device-service-chain.md) — `engine.devices`
  设备资源、工厂、运行集合与显示页的装配契约；区分记录存在、默认可见、服务在线和动作完成。

- [CV 文件读取、通道与写回契约](../../04-api-reference/engine-components/ColorVision.FileIO.md) — `engine.file-io`
  CVRAW/CVCIE 二进制读取、关联源文件与内嵌通道的区别，以及版本写回、长度校验和失败边界。

- [ColorVision.Engine 工程、资源与依赖](../../04-api-reference/engine-components/ColorVision.Engine.md) — `engine.host`
  ColorVision.Engine工程的条件引用、NuGet/DLL依赖回退与资源打包；schema嵌入程序集，缺少输出散文件不等于漏包，也不保证脱离UI源码独立构建。

- [MySQL 结果清理、备份与失败边界](../../04-api-reference/engine-components/mysql-maintenance.md) — `engine.mysql-maintenance`
  MySQL 批次与结果表的历史删除、整表截断和SQL备份；统计不是清理预览，无全程事务或自动恢复，主从选择和管理员权限不能只依赖界面提示。

- [RC 注册、服务快照与连接测试](../../04-api-reference/engine-components/rc-registration.md) — `engine.rc-registration`
  RC注册令牌、启动早到服务快照与连接测试；连接标志不等于设备就绪，测试会影响运行单例，取消不回滚注册或订阅。

- [Engine 结果展示链路](../../04-api-reference/engine-components/result-handoff-chain.md) — `engine.results`
  区分 Engine 历史结果 handler、项目业务结果和统一算法 overlay 的注册及生命周期。

- [模板注册、参数与持久化](../../03-architecture/components/templates/design.md) — `engine.template-design`
  TemplateControl注册与普通ITemplate\<T\>参数加载、保存、复制和删除契约；注册、内存变更和数据库成功是不同状态，JSON与Flow另有实现。

- [Flow 架构与责任边界](../../03-architecture/components/engine/flow-engine.md) — `flow.architecture`
  区分 Flow 底层画布、节点内核、模板存储、编辑工作区、共享会话与隔离执行的所有权。

- [Flow 隔离无界面执行](../../04-api-reference/algorithms/templates/flow-engine.md) — `flow.headless`
  隔离 STN 无界面执行的不可变请求、终止结果与 HeadlessFlowJob 调度边界，不自动运行批次和前后处理。

- [Flow 启动、停止与最终化](../../01-user-guide/workflow/execution.md) — `flow.session`
  FlowExecutionSession 的启动前提、停止请求与最终化判据，以及按失败阶段定位证据。

- [Flow 模板、持久化与流程包](../../04-api-reference/engine-components/template-flow-chain.md) — `flow.templates`
  Flow 模板的数据库保存、文档基线、cvflow v3 包兼容，以及版本/搜索侧车的失败边界。

- [Flow 编辑工作区与文档命令](../../01-user-guide/workflow/design.md) — `flow.workspace`
  ViewFlow 与 FlowEditorCanvas 的编辑命令、文档目标、工作区隔离及未保存画布的执行边界。

- [校准服务、本地文件校正与结果持久化](../../01-user-guide/devices/calibration.md) — `operations.calibration`
  校准服务绑定物理相机并执行本地文件或MQTT校正；输出文件、结果显示、历史落库与缓存删除是不同完成边界。

- [相机服务、采集与结果视图](../../01-user-guide/devices/camera.md) — `operations.camera`
  DeviceCamera的物理关联、远程采集完成判据与本地采集/实时预览边界；无文件设备结果预览仍未实现。

- [相机参数来源、同步与保存](../../01-user-guide/devices/camera-configuration.md) — `operations.camera-configuration`
  区分物理配置、逻辑服务、显示参数与CameraRunParam，说明同步覆盖、ROI约束、保存重启和路径移动副作用。

- [设备资源配置、保存与重启](../../01-user-guide/devices/configuration.md) — `operations.device-configuration`
  终端与设备资源的创建、JSON恢复、保存和RC重启边界；保存、导入、重置与删除均不能视为无副作用检查。

- [FileServer 设备配置与实现边界](../../01-user-guide/devices/file-server.md) — `operations.file-server`
  FileServer 工厂存在但默认类型树过滤；当前仅有配置与通用 MQTT 包装，未实现远端文件列表、上传或下载操作。

- [FlowDevice 远端服务包装与本地图边界](../../01-user-guide/devices/flow-device.md) — `operations.flow-device`
  Flow 远端设备包装有工厂但默认类型树过滤；它不执行 FlowEngineLib 本地图，也未提供专用运行/停止和完成回执。

- [跨模块运行问题定位](../../01-user-guide/README.md) — `operations.index`
  从启动、配置、日志、设备、流程和结果现象定位代码责任，区分已完成阶段与待验证阶段，避免用重启或改数据代替诊断。

- [电机命令与位置读回](../../01-user-guide/devices/motor.md) — `operations.motor`
  电机设备配置、MQTT运动命令与位置读回契约；移动回包不会刷新位置，客户端参数不能代替现场限位与急停。

- [物理相机发现、许可证与资源管理](../../01-user-guide/devices/camera-management.md) — `operations.physical-camera`
  PhyCameraManager发现、许可导入、校准资源与恢复点契约；许可证导入可重置配置，并在唯一物理相机时批量绑定设备。

- [SMU 参数、结果与输出关闭](../../01-user-guide/devices/smu.md) — `operations.smu`
  SMU手动与Flow参数、A/B通道、扫描结果及关闭输出边界；成功回包、空读数或超时都不能单独证明输出安全关闭。

- [扩展性开发](../../02-developer-guide/core-concepts/extensibility.md) — `platform.extensibility`
  菜单、插件、属性编辑器、算法模板和 Copilot 扩展的职责与源码入口。

- [系统职责与跨模块边界](../../03-architecture/overview/system-overview.md) — `platform.system`
  宿主、UI、Engine、插件与项目的职责及调用边界：UI操作不必经过Engine，程序集依赖不是统一执行顺序，构建产物不等于交付制品。

- [通用查询、条件会话与整表操作](../../04-api-reference/ui-components/database-query.md) — `ui.database-query`
  实体驱动的通用查询窗口：条件参数化、执行时SQL预览、结果替换与进程内会话；关闭不取消查询，清空表/截断表作用于整表而非筛选结果。

- [ColorVision.ImageEditor：打开、绘制与输出](../../04-api-reference/ui-components/ColorVision.ImageEditor.md) — `ui.image-editor`
  图像/视频打开、绘图撤销、叠加层、3D 与快照输出边界，区分渲染图、当前源像素和重读源文件的模型导出。

- [多图查看、刷新与缩略图缓存](../../04-api-reference/ui-components/ColorVision.ImageTools.md) — `ui.image-tools`
  ImageTools内置注册、多图列表中的单张预览、刷新与SQLite缩略图缓存；重选不保证重载，关窗不清缓存，缓存关闭也不等于零数据库访问。

- [PropertyGrid 属性编辑契约](../../04-api-reference/ui-components/property-grid.md) — `ui.property-grid`
  属性面板的字段生成、编辑器选择和 Flow 适配；区分直接修改、工作副本、关闭、重置与宿主持久化。

- [Quartz 任务定义、恢复与执行历史](../../04-api-reference/ui-components/ColorVision.Scheduler.md) — `ui.scheduler`
  Quartz 调度定义的启动恢复、JSON/SQLite 分工与执行统计；暂停不终止在途任务，重启恢复不是执行进度续跑。

- [主窗口搜索：候选、刷新与执行](../../04-api-reference/ui-components/search.md) — `ui.search`
  主窗口搜索框的关键词匹配、候选来源、刷新缓存、结果顺序和执行；关闭搜索设置不回滚也不直接保存，回车不统一检查命令权限。

- [SQLite 正文存储、迁移与文件维护](../../04-api-reference/ui-components/sqlite-storage.md) — `ui.sqlite-storage`
  Socket 与 Flow 的 SQLite 正文 gzip 编解码、按ID读写、旧TEXT逐批迁移、WAL备份与VACUUM；通用工具不自动停写/备份/恢复，失败可能已有批次提交。

- [本地相机内存帧预览：实施与验证 \[规划\]](../../02-developer-guide/engine-development/local-camera-memory-preview-validation.md) — `engine.camera-preview-validation-plan`
  列出尚未实施的相机内存预览阶段、验收用例和实施前需要重新核对的源码。

- [Engine MQTT 消息处理指南](../../02-developer-guide/engine-development/mqtt.md) — `engine.mqtt`
  说明 Engine MQTT 连接、设备请求、MsgID 关联、超时和订阅恢复。

- [OpenCV 和 native 集成开发指南](../../02-developer-guide/engine-development/opencv-integration.md) — `engine.native-integration`
  说明 native ABI、HImage 所有权、首次 helper 构建与 CUDA 发布输入的验证边界。

- [现场操作验收清单](../../01-user-guide/field-operation-acceptance.md) — `operations.acceptance`
  记录设备、流程、数据和外部系统的现场验收证据，区分自动化测试与真机结果。

- [设置、流程与结果的导入导出边界](../../01-user-guide/data-management/export-import.md) — `operations.exports`
  按设置、流程、图像和项目结果定位导入导出实现，说明配置覆盖、文件验收与迁移边界。

- [主程序启动与最小图像验证](../../00-getting-started/first-steps.md) — `operations.first-run`
  主程序启动的配置、实例和服务副作用，以及隔离测试环境中的最小本地图像验证。

- [ARVR 模板](../../04-api-reference/algorithms/templates/arvr-template.md) — `algorithms.arvr`
  对照 ARVR 模板族、手动请求、Flow 算子和结果 handler 的版本边界。

- [JSON 模板](../../04-api-reference/algorithms/templates/json-templates.md) — `algorithms.json-templates`
  JSON模板数据库存储、编辑器与结果版本匹配；Schema优先读取程序集嵌入资源，再回退磁盘索引，不要求输出目录有散文件。

- [LED 检测模板](../../04-api-reference/algorithms/templates/led-detection.md) — `algorithms.led`
  区分灯条、灯珠强类型与 JSON V2 模板、事件、POI 输入和结果限制。

- [POI 模板](../../04-api-reference/algorithms/templates/poi-template.md) — `algorithms.poi-template`
  说明 POI 主从表、伴生模板、复制导入、运行事件与结果类型映射。

- [SysDictionary 系统字典兼容层](../../04-api-reference/algorithms/templates/sys-dictionary-template.md) — `algorithms.template-dictionary`
  说明保留的系统字典 DAO 与模板默认值、传感器和旧流程兼容依赖。

- [Flow 转换与校准节点](../../04-api-reference/engine-components/flow-conversion-calibration-nodes.md) — `flow.conversion-calibration`
  定位 Flow 数据转换、图像转换、单双输入校准及属性选择器。

- [本地相机内存帧预览：生命周期与显示语义 \[规划\]](../../02-developer-guide/engine-development/local-camera-memory-preview-runtime.md) — `engine.camera-preview-lifecycle-plan`
  记录待实施预览的租约取得、latest-wins、RAW/CIE 模式和内存预算约束。

- [本地相机内存帧预览方案（待实施） \[规划\]](../../02-developer-guide/engine-development/local-camera-memory-preview.md) — `engine.camera-preview-plan`
  记录待实施的设备级内存帧预览设计，不代表当前 ViewCamera 已支持无文件历史结果。

## Engine/ColorVision.FileIO {#module-456e67696e652f436f6c6f72566973696f6e2e46696c65494f}

- [CV 文件读取、通道与写回契约](../../04-api-reference/engine-components/ColorVision.FileIO.md) — `engine.file-io`
  CVRAW/CVCIE 二进制读取、关联源文件与内嵌通道的区别，以及版本写回、长度校验和失败边界。

- [Explorer 缩略图读取与 COM 注册](../../04-api-reference/engine-components/ColorVision.ShellExtension.md) — `engine.shell-extension`
  Explorer 的 CVRAW/CVCIE COM provider 如何读取像素、生成非测量用途缩略图，以及源码脚本与 ServiceHost 注册的不同副作用和失败边界。

## Engine/ColorVision.ShellExtension {#module-456e67696e652f436f6c6f72566973696f6e2e5368656c6c457874656e73696f6e}

- [Explorer 缩略图读取与 COM 注册](../../04-api-reference/engine-components/ColorVision.ShellExtension.md) — `engine.shell-extension`
  Explorer 的 CVRAW/CVCIE COM provider 如何读取像素、生成非测量用途缩略图，以及源码脚本与 ServiceHost 注册的不同副作用和失败边界。

## Engine/cvColorVision {#module-456e67696e652f6376436f6c6f72566973696f6e}

- [cvColorVision](../../04-api-reference/engine-components/cvColorVision.md) — `engine.native-bindings`
  定位供应商 native DLL 的相机、光谱、XYZ、OLED、PG 与源表绑定契约。

## Engine/FlowEngineLib {#module-456e67696e652f466c6f77456e67696e654c6962}

- [Engine 知识入口](../../04-api-reference/engine-components/README.md) — `engine.index`
  按实际代码职责路由 Engine 的设备、消息、模板、Flow、结果与工程依赖；契约和验证由各主题维护。

- [Flow 节点检索入口](../../04-api-reference/flow_nodes_summary.md) — `flow.index`
  按节点用途与执行归属定位 FlowEngineLib、Engine 本地节点和属性编辑器。

- [扩展任务入口](../../04-api-reference/extensions/README.md) — `platform.extensions`
  按 Flow 节点、属性编辑器、模板、设备和插件问题定位可复用扩展契约。

- [DataLoad 数据加载模板](../../04-api-reference/algorithms/templates/data-load-template.md) — `algorithms.data-load`
  区分 DataLoad 模板与显式参数节点如何按设备、批次和 ZIndex 读取上游结果。

- [ImageCropping 图像裁剪模板](../../04-api-reference/algorithms/templates/image-cropping-template.md) — `algorithms.image-cropping`
  区分强类型 ImageCropping 的持久参数、运行时四点 ROI、Flow 双输入和图像结果。

- [Matching 模板匹配](../../04-api-reference/algorithms/templates/matching-template.md) — `algorithms.matching`
  说明 Matching 通用配置宿主、运行时模板文件、Flow 请求和 AOI 结果绘制。

- [Flow 架构与责任边界](../../03-architecture/components/engine/flow-engine.md) — `flow.architecture`
  区分 Flow 底层画布、节点内核、模板存储、编辑工作区、共享会话与隔离执行的所有权。

- [Flow 隔离无界面执行](../../04-api-reference/algorithms/templates/flow-engine.md) — `flow.headless`
  隔离 STN 无界面执行的不可变请求、终止结果与 HeadlessFlowJob 调度边界，不自动运行批次和前后处理。

- [电机命令与位置读回](../../01-user-guide/devices/motor.md) — `operations.motor`
  电机设备配置、MQTT运动命令与位置读回契约；移动回包不会刷新位置，客户端参数不能代替现场限位与急停。

- [SMU 参数、结果与输出关闭](../../01-user-guide/devices/smu.md) — `operations.smu`
  SMU手动与Flow参数、A/B通道、扫描结果及关闭输出边界；成功回包、空读数或超时都不能单独证明输出安全关闭。

- [PropertyGrid 属性编辑契约](../../04-api-reference/ui-components/property-grid.md) — `ui.property-grid`
  属性面板的字段生成、编辑器选择和 Flow 适配；区分直接修改、工作副本、关闭、重置与宿主持久化。

- [Engine MQTT 消息处理指南](../../02-developer-guide/engine-development/mqtt.md) — `engine.mqtt`
  说明 Engine MQTT 连接、设备请求、MsgID 关联、超时和订阅恢复。

- [FlowEngineLib 节点扩展](../../04-api-reference/extensions/flow-node.md) — `flow.node-extension`
  说明服务节点基类、请求与响应扩展点、属性编辑和流程完成的边界。

- [ARVR 模板](../../04-api-reference/algorithms/templates/arvr-template.md) — `algorithms.arvr`
  对照 ARVR 模板族、手动请求、Flow 算子和结果 handler 的版本边界。

- [Flow 转换与校准节点](../../04-api-reference/engine-components/flow-conversion-calibration-nodes.md) — `flow.conversion-calibration`
  定位 Flow 数据转换、图像转换、单双输入校准及属性选择器。

- [FlowEngineLib](../../04-api-reference/engine-components/FlowEngineLib.md) — `flow.runtime`
  说明节点图加载、服务绑定、完成事件和隔离 RuntimeHost 的执行边界。

- [本地相机内存帧预览：生命周期与显示语义 \[规划\]](../../02-developer-guide/engine-development/local-camera-memory-preview-runtime.md) — `engine.camera-preview-lifecycle-plan`
  记录待实施预览的租约取得、latest-wins、RAW/CIE 模式和内存预算约束。

## Engine/ST.Library.UI {#module-456e67696e652f53542e4c6962726172792e5549}

- [ST.Library.UI](../../04-api-reference/engine-components/ST.Library.UI.md) — `flow.editor`
  说明 ST WPF 节点画布、端口、类型目录及 STN 兼容边界。
