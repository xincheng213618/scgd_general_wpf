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

- [ROI 模型与模板入口](../../04-api-reference/algorithms/primitives/roi.md) — `algorithms.roi-routes`
  按用途定位发光区、传统与 JSON 裁剪、SFR 寻边和中立算法 ROI 模型；各分支参数与坐标契约分别维护。

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

- [扩展性开发](../../02-developer-guide/core-concepts/extensibility.md) — `platform.extensibility`
  按插件、菜单、属性面板、设备、Flow、算法和结果扩展任务定位实现与当前契约。

- [DataLoad 数据加载模板](../../04-api-reference/algorithms/templates/data-load-template.md) — `algorithms.data-load`
  数据加载与数据加载2的模板选择、参数初值和请求格式；区分要读取的数据来源与本次 Flow 执行设备、流水号及 ZIndex。

- [本地十字定位 FindCross](../../04-api-reference/algorithms/detectors/find-cross.md) — `algorithms.find-cross`
  本地十字定位的图像菜单、Flow 节点、生产参数、全图坐标、原生返回值与失败诊断。

- [发光区定位：远端模板与本地 V2](../../04-api-reference/algorithms/templates/find-light-area.md) — `algorithms.find-light-area`
  发光区定位1与本地发光区定位(V2)的使用、图像来源、POI保存模板和结果边界；区分算法拒绝、数据库提交与消息发布，并说明模板字典恢复不一致。

- [FocusPoints 关注点模板](../../04-api-reference/algorithms/templates/focus-points-template.md) — `algorithms.focus-points`
  发光区1（FocusPoints）的模板选择、参数初值和图像输入；区分手动 MQTT 模板引用、Flow 算子与计算结果。

- [Ghost1.0 鬼影检测](../../04-api-reference/algorithms/detectors/ghost-detection.md) — `algorithms.ghost`
  Ghost1.0 鬼影检测的模板、颜色和请求入口；说明数据库明细、首条结果叠图、全部明细 CSV 追加导出及读取失败边界。

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
  从模板菜单、算法面板或应用搜索打开模板；说明选择索引、流程设计器直达和菜单发现的边界。

- [算法与模板接入概览](../../04-api-reference/algorithms/overview.md) — `algorithms.template-overview`
  说明 Engine 模板发现、手动算法宿主、MQTT 请求和 Flow 接入链。

- [Copilot 扩展、MCP 与 Hook](../../02-developer-guide/core-concepts/copilot-agent-extensions.md) — `copilot.extensions`
  业务模块动态上下文、外部 MCP client 和 Hook 如何进入统一宿主权限与生命周期。

- [Copilot 工具契约](../../02-developer-guide/core-concepts/copilot-agent-tool-contracts.md) — `copilot.tool-contracts`
  Copilot 工具结果、事件、审批恢复和 Flow 编辑必须遵守的执行契约。

- [Backend Operations 中继与只读概览](../../02-developer-guide/backend/operations-relay.md) — `delivery.backend-operations`
  Backend Operations 的接口、身份与任务回执；区分在线、排队和执行完成，并说明加密快照的下载、消费与过期清理。

- [CVRAW / CVCIE 图像导出](../../04-api-reference/engine-components/cv-image-export.md) — `engine.cv-image-export`
  CVRAW/CVCIE 原生导出的窗口、命令行参数、通道和命名规则，以及覆盖、部分失败和退出码边界。

- [CVCIE POI 结果数值](../../04-api-reference/engine-components/cvcie-results.md) — `engine.cvcie-results`
  CVCIE POI 的非正值替换、色值重算与生效时机；区分本地测量、历史结果缓存、鼠标探针和原始文件。

- [数据库清理窗口、能力接入与完成边界](../../04-api-reference/engine-components/database-maintenance.md) — `engine.database-maintenance`
  数据库维护窗口与provider能力：表统计不是删除预览；备份默认关闭，备份和清理不是事务且失败不自动恢复；清理、手动优化和迁移边界彼此独立。

- [Engine 设备资源与运行装配](../../04-api-reference/engine-components/device-service-chain.md) — `engine.devices`
  设备工厂、资源重载与显示装配；旧对象释放、集合重建和显示替换并非一个事务，记录存在、默认可见、服务在线和动作完成分别判断。

- [CV 文件读取、通道与写回契约](../../04-api-reference/engine-components/ColorVision.FileIO.md) — `engine.file-io`
  CVRAW/CVCIE 读取、内嵌 XYZ 真彩显示与原图回退、手动校正数值校验，以及版本写回和失败边界。

- [ColorVision.Engine 工程、资源与依赖](../../04-api-reference/engine-components/ColorVision.Engine.md) — `engine.host`
  ColorVision.Engine工程的条件引用、NuGet/DLL依赖回退与资源打包；schema嵌入程序集，缺少输出散文件不等于漏包，也不保证脱离UI源码独立构建。

- [MySQL 结果索引优化、清理、备份与失败边界](../../04-api-reference/engine-components/mysql-maintenance.md) — `engine.mysql-maintenance`
  MySQL 结果表的手动关联索引优化、历史删除、整表截断和SQL备份；在线DDL、并发、部分成功、备份与恢复边界分别说明。

- [MySQL SQL 恢复、重置与资源保留](../../04-api-reference/engine-components/mysql-recovery.md) — `engine.mysql-recovery`
  MySQL手动SQL恢复、数据库重置与资源保留：导入后才同步配置和重启注册中心，失败不回滚；迁移备份不含结果，配置更新计数不证明键完整。

- [RC 注册、服务快照与连接测试](../../04-api-reference/engine-components/rc-registration.md) — `engine.rc-registration`
  RC注册、服务目录同步、状态快照与连接测试；远端删除不清本地令牌和收发主题，更新可能部分生效，连接或测试成功不等于设备就绪。

- [算法结果交接、展示与导出](../../04-api-reference/engine-components/result-handoff-chain.md) — `engine.results`
  算法结果接收、历史查询、handler 匹配、缺图回放与数据导出，以及统一 overlay 的文档/revision 生命周期；入库、通知、显示和保存分别判断。

- [主程序光谱仪搜索与配置](../../04-api-reference/engine-components/spectrum-device.md) — `engine.spectrum-device`
  主程序光谱仪的全连接方式搜索、设备配置分类和许可证读取入口；区分本机搜索、服务端刷新与实际连接。

- [模板注册、参数与持久化](../../03-architecture/components/templates/design.md) — `engine.template-design`
  TemplateControl注册与普通ITemplate\<T\>参数加载、保存、复制和删除契约；注册、内存变更和数据库成功是不同状态，JSON与Flow另有实现。

- [Flow 架构与责任边界](../../03-architecture/components/engine/flow-engine.md) — `flow.architecture`
  区分 Flow 底层画布、节点内核、模板存储、编辑工作区、共享会话与隔离执行的所有权。

- [Flow 运行诊断、中断恢复与 Incident 处置](../../04-api-reference/engine-components/flow-diagnostics.md) — `flow.diagnostics`
  Flow本地诊断SQLite快照、节点尝试与Incident事件列表的读写边界；快照不保证包含未保存画布，终态持久化与业务结果分开，中断恢复不续跑节点，心跳不是判死条件。

- [Flow 隔离无界面执行](../../04-api-reference/algorithms/templates/flow-engine.md) — `flow.headless`
  隔离STN流程的加载、起始节点就绪、执行超时与诊断收尾；停止请求不证明设备停稳，默认执行不限时，批次与前后处理由调用方负责。

- [Flow 启动、停止与最终化](../../01-user-guide/workflow/execution.md) — `flow.session`
  流程启动、分阶段停止与后处理完成判据；区分当前画布、诊断快照、执行耗时和结果落库。

- [Flow 模板、持久化与流程包](../../04-api-reference/engine-components/template-flow-chain.md) — `flow.templates`
  Flow 模板的保存基线、导出/删除勾选范围、cvflow v3 包兼容，以及版本/搜索侧车的失败边界。

- [Flow 编辑工作区与文档命令](../../01-user-guide/workflow/design.md) — `flow.workspace`
  流程编辑器的打开与保存步骤、导出/删除范围、切换提示和工作区隔离；区分当前画布与已保存模板。

- [校准服务、本地文件校正与结果持久化](../../01-user-guide/devices/calibration.md) — `operations.calibration`
  校准服务绑定物理相机并执行本地文件或MQTT校正；输出文件、结果显示、历史落库与缓存删除是不同完成边界。

- [相机服务、采集与结果视图](../../01-user-guide/devices/camera.md) — `operations.camera`
  远程取图、本地手动/流程采集与结果视图；明确SaveFiles=false文件显示限制、RAW/CIE帧租约与校正读写、命令完成和设备释放边界。

- [相机参数来源、同步与保存](../../01-user-guide/devices/camera-configuration.md) — `operations.camera-configuration`
  相机参数的编辑入口、同步覆盖与保存；物理配置同步保留本地CameraID，路径移动失败或被拒绝不等于取消路径变更。

- [设备资源配置、保存与重启](../../01-user-guide/devices/configuration.md) — `operations.device-configuration`
  终端与设备配置引用、创建、保存、重启和删除清理；未保存的活对象改动可影响运行，删除不保证显示项和通信对象一并释放。

- [FileServer 设备配置与实现边界](../../01-user-guide/devices/file-server.md) — `operations.file-server`
  FileServer 工厂存在但默认类型树过滤；当前仅有配置与通用 MQTT 包装，未实现远端文件列表、上传或下载操作。

- [FlowDevice 远端服务包装与本地图边界](../../01-user-guide/devices/flow-device.md) — `operations.flow-device`
  Flow 远端设备包装有工厂但默认类型树过滤；它不执行 FlowEngineLib 本地图，也未提供专用运行/停止和完成回执。

- [跨模块运行问题定位](../../01-user-guide/README.md) — `operations.index`
  从启动、配置、日志、设备、流程和结果现象定位代码责任，区分已完成阶段与待验证阶段，避免用重启或改数据代替诊断。

- [电机命令与位置读回](../../01-user-guide/devices/motor.md) — `operations.motor`
  电机设备配置、MQTT运动命令与位置读回契约；移动回包不会刷新位置，客户端参数不能代替现场限位与急停。

- [物理相机发现、许可证与资源管理](../../01-user-guide/devices/camera-management.md) — `operations.physical-camera`
  物理相机的扫描、创建、许可证、校正资源和还原点入口；区分扫描结果与缓存列表，创建/导入在唯一物理相机时可批量绑定服务。

- [SMU 参数、结果与输出关闭](../../01-user-guide/devices/smu.md) — `operations.smu`
  SMU手动与Flow参数、A/B通道、扫描结果及关闭输出边界；成功回包、空读数或超时都不能单独证明输出安全关闭。

- [开发工具管理：检测与安装 Python、Node.js](../../02-developer-guide/core-concepts/developer-tools-manager.md) — `platform.developer-tools`
  开发工具管理的Python/Node检测、当前应用与新终端命令路径、官方版本选择和安装校验；下载等待30分钟，关窗停止后续安装但不取消下载或终止安装器。

- [系统职责与跨模块边界](../../03-architecture/overview/system-overview.md) — `platform.system`
  宿主、UI、Engine、插件与项目的职责及调用边界：UI操作不必经过Engine，程序集依赖不是统一执行顺序，构建产物不等于交付制品。

- [WindowsServicePlugin：选包、本机安装与恢复](../../04-api-reference/plugins/standard-plugins/windows-service.md) — `plugins.windows-service`
  WindowsServicePlugin的在线选包与缓存、本机完整安装、数据库版本切换和恢复边界；下载、日志完成、备份与实际服务状态不能互相替代。

- [通用查询、条件会话与整表操作](../../04-api-reference/ui-components/database-query.md) — `ui.database-query`
  实体驱动的通用查询窗口：条件参数化、执行时SQL预览、结果替换与进程内会话；关闭不取消查询，清空表/截断表作用于整表而非筛选结果。

- [ColorVision.ImageEditor：打开、绘制与输出](../../04-api-reference/ui-components/ColorVision.ImageEditor.md) — `ui.image-editor`
  图像/视频打开、绘图撤销、叠加层、3D 与快照输出边界，区分渲染图、当前源像素和重读源文件的模型导出。

- [多图查看、刷新与缩略图缓存](../../04-api-reference/ui-components/ColorVision.ImageTools.md) — `ui.image-tools`
  ImageTools内置注册、多图列表中的单张预览、刷新与SQLite缩略图缓存；重选不保证重载，关窗不清缓存，缓存关闭也不等于零数据库访问。

- [PropertyGrid 属性编辑契约](../../04-api-reference/ui-components/property-grid.md) — `ui.property-grid`
  属性面板的字段生成、编辑器选择和 Flow 适配；区分直接修改、工作副本、关闭、重置与宿主持久化。

- [任务计划程序：创建、调度与执行历史](../../04-api-reference/ui-components/ColorVision.Scheduler.md) — `ui.scheduler`
  任务计划程序的状态栏入口、创建步骤、调度参数、启动恢复和执行历史；暂停只限制后续触发，重启按保存的定义重新调度。

- [应用搜索：入口、候选与执行](../../04-api-reference/ui-components/search.md) — `ui.search`
  应用搜索窗口的入口、关键词匹配、候选来源、缓存刷新与命令执行；Ctrl+F 按焦点执行局部查找，Ctrl+Shift+P 打开应用搜索。

- [SQLite 正文存储、迁移与文件维护](../../04-api-reference/ui-components/sqlite-storage.md) — `ui.sqlite-storage`
  Socket 与 Flow 的 SQLite 正文 gzip 编解码、按ID读写、旧TEXT逐批迁移、WAL备份与VACUUM；通用工具不自动停写/备份/恢复，失败可能已有批次提交。

- [系统要求与首次构建](../../00-getting-started/prerequisites.md) — `delivery.prerequisites`
  Windows x64 运行与源码构建前提：Desktop Runtime、SDK、C++ 工具集及已有 native DLL 的选择。

- [Engine MQTT 消息处理指南](../../02-developer-guide/engine-development/mqtt.md) — `engine.mqtt`
  Engine MQTT 的连接与订阅、异步发送、请求状态、迟到回包和 MsgID 复用限制；区分 Flow 客户端池与设备命令链。

- [OpenCV 和 native 集成开发指南](../../02-developer-guide/engine-development/opencv-integration.md) — `engine.native-integration`
  native ABI与HImage所有权、函数族返回值、视频异步/关闭边界，以及helper构建和CUDA发布输入；路由校准Context与POI原生参考。

- [FlowEngineLib 节点扩展](../../04-api-reference/extensions/flow-node.md) — `flow.node-extension`
  说明服务与本地节点基类、请求与响应扩展点、分支输入隔离、属性编辑和流程完成的边界。

- [现场操作验收清单](../../01-user-guide/field-operation-acceptance.md) — `operations.acceptance`
  按交付范围验收启动、设备、流程、数据和外部协议；明确通过、失败、未测和不适用，记录同一轮证据及回退材料与演练状态。

- [设置、流程与结果的导入导出边界](../../01-user-guide/data-management/export-import.md) — `operations.exports`
  按设置、流程、图像和项目结果定位导入导出实现，说明配置覆盖、文件验收与迁移边界。

- [主程序启动与最小图像验证](../../00-getting-started/first-steps.md) — `operations.first-run`
  主程序启动的配置、实例和服务副作用，以及隔离测试环境中的最小本地图像验证。

- [UI NuGet 包构建与发布](../../04-api-reference/ui-components/publishing.md) — `ui.publishing`
  UI NuGet整批与Algorithms单包发布、Release标签和版本预检；预检不预留版本，逐包上传没有整批回滚或逐条失败检查。

- [ARVR 算法与模板](../../04-api-reference/algorithms/templates/arvr-template.md) — `algorithms.arvr`
  ARVR 手动算法与流程节点的模板、POI 和请求对应关系；说明结果版本匹配及 SFR 曲线、查询和两种 CSV 导出的数据范围。

- [灰度与颜色剖面：采样、曲线与数据导出](../../02-developer-guide/core-concepts/image-profile-v1.md) — `algorithms.image-profile`
  灰度与颜色剖面的操作、采样/越界规则、2000行预览和完整JSON/CSV导出；多点入口受多边形选择器限制，MaximumSamples还受执行/字节预算限制，旧接口参数不同。

- [成像校正：参考图、执行与结果保存](../../02-developer-guide/core-concepts/imaging-correction-v1.md) — `algorithms.imaging-correction`
  成像校正的参考图、固定阶段、参数/preset、执行并提交、mask与PNG/CSV/JSON保存；明确Alpha裁剪、无效样本、精确复制和批量只保存主图的边界。

- [JSON 模板](../../04-api-reference/algorithms/templates/json-templates.md) — `algorithms.json-templates`
  JSON模板的文本/属性编辑、数据库保存、默认参数与重置；校验Json按钮只同步模型，Schema提供字段提示而不补默认值或执行完整校验。

- [LED 检测模板](../../04-api-reference/algorithms/templates/led-detection.md) — `algorithms.led`
  区分灯条、灯珠强类型与 JSON V2 模板、事件、POI 输入和结果限制。

- [POI 模板](../../04-api-reference/algorithms/templates/poi-template.md) — `algorithms.poi-template`
  说明 POI 主从表、伴生模板、复制导入、运行事件与结果类型映射。

- [ROI 统计：区域、直方图与坏点候选](../../02-developer-guide/core-concepts/roi-statistics-v1.md) — `algorithms.roi-statistics`
  ROI统计的区域选择、百分位、直方图、坏点候选计数/返回上限及六文件CSV导出；说明Float32精确统计预算、列名精度限制和实际窗口操作。

- [SysDictionary 系统字典兼容层](../../04-api-reference/algorithms/templates/sys-dictionary-template.md) — `algorithms.template-dictionary`
  说明保留的系统字典 DAO 与模板默认值、传感器和旧流程兼容依赖。

- [opencv\_helper.dll API 参考](../../04-api-reference/engine-components/opencv-helper-api.md) — `engine.opencv-helper-api`
  opencv\_helper 英文 API 参考：校准/POI、图像处理、SFR、检测、视频与内存释放；核对真实参数单位和函数族错误码，声明的选项不等于当前 Engine 提供操作入口。

- [Flow 转换与校准节点](../../04-api-reference/engine-components/flow-conversion-calibration-nodes.md) — `flow.conversion-calibration`
  定位 Flow 数据转换、图像转换、单双输入校准及属性选择器。

- [ColorVision 概览](../../00-getting-started/what-is-colorvision.md) — `platform.product`
  ColorVision 的设备、流程、图像分析、结果、插件与客户项目能力，以及从任务进入文档的方法。

- [设备视图内存预览设计（待实施） \[规划\]](../../02-developer-guide/engine-development/local-camera-memory-preview.md) — `engine.camera-preview-plan`
  待实施的设备视图无文件预览：明确与本地手动窗口的区别、发布租约之外的读写同步、latest-wins、RAW/CIE显示副本及验收缺口。

## Engine/ColorVision.FileIO {#module-456e67696e652f436f6c6f72566973696f6e2e46696c65494f}

- [构建平台与制品边界](../../02-developer-guide/README.md) — `delivery.index`
  定义宿主、插件、客户包和独立FileIO包的构建平台与制品边界，区分构建验证和远端发布。

- [CV 文件读取、通道与写回契约](../../04-api-reference/engine-components/ColorVision.FileIO.md) — `engine.file-io`
  CVRAW/CVCIE 读取、内嵌 XYZ 真彩显示与原图回退、手动校正数值校验，以及版本写回和失败边界。

- [Explorer 缩略图读取与 COM 注册](../../04-api-reference/engine-components/ColorVision.ShellExtension.md) — `engine.shell-extension`
  Explorer 的 CVRAW/CVCIE COM provider 如何读取像素、生成非测量用途缩略图，以及源码脚本与 ServiceHost 注册的不同副作用和失败边界。

## Engine/ColorVision.ShellExtension {#module-456e67696e652f436f6c6f72566973696f6e2e5368656c6c457874656e73696f6e}

- [Explorer 缩略图读取与 COM 注册](../../04-api-reference/engine-components/ColorVision.ShellExtension.md) — `engine.shell-extension`
  Explorer 的 CVRAW/CVCIE COM provider 如何读取像素、生成非测量用途缩略图，以及源码脚本与 ServiceHost 注册的不同副作用和失败边界。

## Engine/cvColorVision {#module-456e67696e652f6376436f6c6f72566973696f6e}

- [RC 注册、服务快照与连接测试](../../04-api-reference/engine-components/rc-registration.md) — `engine.rc-registration`
  RC注册、服务目录同步、状态快照与连接测试；远端删除不清本地令牌和收发主题，更新可能部分生效，连接或测试成功不等于设备就绪。

- [物理相机发现、许可证与资源管理](../../01-user-guide/devices/camera-management.md) — `operations.physical-camera`
  物理相机的扫描、创建、许可证、校正资源和还原点入口；区分扫描结果与缓存列表，创建/导入在唯一物理相机时可批量绑定服务。

- [Conoscope 图像、采集与分析](../../04-api-reference/plugins/standard-plugins/conoscope.md) — `plugins.conoscope`
  Conoscope 的采集、CVCIE 首屏/XYZ 就绪、Mat 与分析快照契约；按钮成功不代表文档加载完成，联合灰尘预处理不走 Y-first。

- [系统要求与首次构建](../../00-getting-started/prerequisites.md) — `delivery.prerequisites`
  Windows x64 运行与源码构建前提：Desktop Runtime、SDK、C++ 工具集及已有 native DLL 的选择。

- [ARVR 算法与模板](../../04-api-reference/algorithms/templates/arvr-template.md) — `algorithms.arvr`
  ARVR 手动算法与流程节点的模板、POI 和请求对应关系；说明结果版本匹配及 SFR 曲线、查询和两种 CSV 导出的数据范围。

- [cvColorVision](../../04-api-reference/engine-components/cvColorVision.md) — `engine.native-bindings`
  定位供应商 native DLL 的相机、光谱、XYZ、OLED、PG 与源表绑定契约。

- [设备视图内存预览设计（待实施） \[规划\]](../../02-developer-guide/engine-development/local-camera-memory-preview.md) — `engine.camera-preview-plan`
  待实施的设备视图无文件预览：明确与本地手动窗口的区别、发布租约之外的读写同步、latest-wins、RAW/CIE显示副本及验收缺口。

## Engine/FlowEngineLib {#module-456e67696e652f466c6f77456e67696e654c6962}

- [Engine 知识入口](../../04-api-reference/engine-components/README.md) — `engine.index`
  按实际代码职责路由 Engine 的设备、消息、模板、Flow、结果与工程依赖；契约和验证由各主题维护。

- [Flow 节点检索入口](../../04-api-reference/flow_nodes_summary.md) — `flow.index`
  按节点用途与执行归属定位 FlowEngineLib、Engine 本地节点和属性编辑器。

- [扩展性开发](../../02-developer-guide/core-concepts/extensibility.md) — `platform.extensibility`
  按插件、菜单、属性面板、设备、Flow、算法和结果扩展任务定位实现与当前契约。

- [DataLoad 数据加载模板](../../04-api-reference/algorithms/templates/data-load-template.md) — `algorithms.data-load`
  数据加载与数据加载2的模板选择、参数初值和请求格式；区分要读取的数据来源与本次 Flow 执行设备、流水号及 ZIndex。

- [FocusPoints 关注点模板](../../04-api-reference/algorithms/templates/focus-points-template.md) — `algorithms.focus-points`
  发光区1（FocusPoints）的模板选择、参数初值和图像输入；区分手动 MQTT 模板引用、Flow 算子与计算结果。

- [ImageCropping 图像裁剪模板](../../04-api-reference/algorithms/templates/image-cropping-template.md) — `algorithms.image-cropping`
  区分强类型 ImageCropping 的持久参数、运行时四点 ROI、Flow 双输入和图像结果。

- [Matching 模板匹配](../../04-api-reference/algorithms/templates/matching-template.md) — `algorithms.matching`
  说明 Matching 通用配置宿主、运行时模板文件、Flow 请求和 AOI 结果绘制。

- [Flow 架构与责任边界](../../03-architecture/components/engine/flow-engine.md) — `flow.architecture`
  区分 Flow 底层画布、节点内核、模板存储、编辑工作区、共享会话与隔离执行的所有权。

- [Flow 隔离无界面执行](../../04-api-reference/algorithms/templates/flow-engine.md) — `flow.headless`
  隔离STN流程的加载、起始节点就绪、执行超时与诊断收尾；停止请求不证明设备停稳，默认执行不限时，批次与前后处理由调用方负责。

- [相机服务、采集与结果视图](../../01-user-guide/devices/camera.md) — `operations.camera`
  远程取图、本地手动/流程采集与结果视图；明确SaveFiles=false文件显示限制、RAW/CIE帧租约与校正读写、命令完成和设备释放边界。

- [电机命令与位置读回](../../01-user-guide/devices/motor.md) — `operations.motor`
  电机设备配置、MQTT运动命令与位置读回契约；移动回包不会刷新位置，客户端参数不能代替现场限位与急停。

- [SMU 参数、结果与输出关闭](../../01-user-guide/devices/smu.md) — `operations.smu`
  SMU手动与Flow参数、A/B通道、扫描结果及关闭输出边界；成功回包、空读数或超时都不能单独证明输出安全关闭。

- [PropertyGrid 属性编辑契约](../../04-api-reference/ui-components/property-grid.md) — `ui.property-grid`
  属性面板的字段生成、编辑器选择和 Flow 适配；区分直接修改、工作副本、关闭、重置与宿主持久化。

- [Engine MQTT 消息处理指南](../../02-developer-guide/engine-development/mqtt.md) — `engine.mqtt`
  Engine MQTT 的连接与订阅、异步发送、请求状态、迟到回包和 MsgID 复用限制；区分 Flow 客户端池与设备命令链。

- [FlowEngineLib 节点扩展](../../04-api-reference/extensions/flow-node.md) — `flow.node-extension`
  说明服务与本地节点基类、请求与响应扩展点、分支输入隔离、属性编辑和流程完成的边界。

- [ARVR 算法与模板](../../04-api-reference/algorithms/templates/arvr-template.md) — `algorithms.arvr`
  ARVR 手动算法与流程节点的模板、POI 和请求对应关系；说明结果版本匹配及 SFR 曲线、查询和两种 CSV 导出的数据范围。

- [Flow 转换与校准节点](../../04-api-reference/engine-components/flow-conversion-calibration-nodes.md) — `flow.conversion-calibration`
  定位 Flow 数据转换、图像转换、单双输入校准及属性选择器。

- [FlowEngineLib](../../04-api-reference/engine-components/FlowEngineLib.md) — `flow.runtime`
  节点图加载、服务绑定、弃用节点兼容、完成事件和隔离 RuntimeHost 的执行边界。

- [设备视图内存预览设计（待实施） \[规划\]](../../02-developer-guide/engine-development/local-camera-memory-preview.md) — `engine.camera-preview-plan`
  待实施的设备视图无文件预览：明确与本地手动窗口的区别、发布租约之外的读写同步、latest-wins、RAW/CIE显示副本及验收缺口。

## Engine/ST.Library.UI {#module-456e67696e652f53542e4c6962726172792e5549}

- [ST.Library.UI](../../04-api-reference/engine-components/ST.Library.UI.md) — `flow.editor`
  说明 ST WPF 节点画布、端口、类型目录及 STN 兼容边界。

- [FlowEngineLib](../../04-api-reference/engine-components/FlowEngineLib.md) — `flow.runtime`
  节点图加载、服务绑定、弃用节点兼容、完成事件和隔离 RuntimeHost 的执行边界。
