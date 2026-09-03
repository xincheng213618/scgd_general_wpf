---
generated_knowledge_index: true
search: false
editLink: false
prev: false
next: false
---

# UI 源码知识

> 自动生成的源码目录。修改主题 Markdown 的 `code_paths` 后运行 `node docs/.vitepress/scripts/knowledge.mjs generate`；不要手工编辑。

返回[知识总入口](../index.md)。只读与当前模块有关的主题，再核对其中的源码、测试和状态；`规划`、`历史`不代表当前能力。

以下是已声明源码路径的关联，不是完整调用图或完整模块清单。跨模块主题可出现在多处；根目录概览只列在根目录项，不自动覆盖所有子模块。

## UI/ 根目录与跨模块关联 {#module-5549}

- [UI 知识入口](../../04-api-reference/ui-components/README.md) — `ui.index`
  按问题路由到 UI 模块、属性编辑契约、运行时发现与 DLL 发布证据。

- [UI NuGet 包构建与发布](../../04-api-reference/ui-components/publishing.md) — `ui.publishing`
  UI NuGet整批与Algorithms单包发布、Release标签和版本预检；预检不预留版本，逐包上传没有整批回滚或逐条失败检查。

- [UI 包职责与依赖边界](../../04-api-reference/ui-components/component-handbook.md) — `ui.package-boundaries`
  UI类库的职责、依赖与目标框架/版本兼容；包版本可独立于主程序，中立算法与窗口适配分层维护。

## UI/ColorVision.Algorithms {#module-55492f436f6c6f72566973696f6e2e416c676f726974686d73}

- [算法与模板知识入口](../../04-api-reference/algorithms/README.md) — `algorithms.index`
  区分统一 Runner、ImageEditor 直接 native 分析与 Engine 模板/MQTT 算法，并按任务定位专题。

- [ROI 模型与模板入口](../../04-api-reference/algorithms/primitives/roi.md) — `algorithms.roi-routes`
  按用途定位发光区、传统与 JSON 裁剪、SFR 寻边和中立算法 ROI 模型；各分支参数与坐标契约分别维护。

- [统一图像算法平台 V1](../../02-developer-guide/core-concepts/image-algorithm-platform-v1.md) — `algorithms.platform`
  统一图像算法Catalog、Invocation和Runner；普通像素预览、应用/取消、所有权与发布门禁；ONNX仅设计。

- [Engine 结果展示链路](../../04-api-reference/engine-components/result-handoff-chain.md) — `engine.results`
  区分 Engine 历史结果 handler、项目业务结果和统一算法 overlay 的注册及生命周期。

- [系统职责与跨模块边界](../../03-architecture/overview/system-overview.md) — `platform.system`
  宿主、UI、Engine、插件与项目的职责及调用边界：UI操作不必经过Engine，程序集依赖不是统一执行顺序，构建产物不等于交付制品。

- [UI NuGet 包构建与发布](../../04-api-reference/ui-components/publishing.md) — `ui.publishing`
  UI NuGet整批与Algorithms单包发布、Release标签和版本预检；预检不预留版本，逐包上传没有整批回滚或逐条失败检查。

- [ColorVision 概览](../../00-getting-started/what-is-colorvision.md) — `platform.product`
  ColorVision 的设备、流程、图像分析、结果、插件与客户项目能力，以及从任务进入文档的方法。

- [UI 包职责与依赖边界](../../04-api-reference/ui-components/component-handbook.md) — `ui.package-boundaries`
  UI类库的职责、依赖与目标框架/版本兼容；包版本可独立于主程序，中立算法与窗口适配分层维护。

- [ONNX / AI 推理接入设计（Deferred） \[规划\]](../../02-developer-guide/core-concepts/onnx-inference-future-design.md) — `algorithms.onnx`
  尚未实现的 ONNX 接入设计：保持基础产品无 ONNX 运行时，新增 adapter 前须明确模型与验收门禁。

## UI/ColorVision.Common {#module-55492f436f6c6f72566973696f6e2e436f6d6d6f6e}

- [扩展性开发](../../02-developer-guide/core-concepts/extensibility.md) — `platform.extensibility`
  按插件、菜单、属性面板、设备、Flow、算法和结果扩展任务定位实现与当前契约。

- [UI 组件目录](../../04-api-reference/ui-components/control-catalog.md) — `ui.control-catalog`
  按控件、窗口和扩展接口定位对应 UI 源码与专题。

- [Copilot 扩展、MCP 与 Hook](../../02-developer-guide/core-concepts/copilot-agent-extensions.md) — `copilot.extensions`
  业务模块动态上下文、外部 MCP client 和 Hook 如何进入统一宿主权限与生命周期。

- [模板注册、参数与持久化](../../03-architecture/components/templates/design.md) — `engine.template-design`
  TemplateControl注册与普通ITemplate\<T\>参数加载、保存、复制和删除契约；注册、内存变更和数据库成功是不同状态，JSON与Flow另有实现。

- [权限边界与鉴权入口](../../03-architecture/security/overview.md) — `platform.security`
  区分应用管理员、RBAC会话与权限码、Windows服务身份及远程/工具授权；登录缓存和界面状态不能替代执行入口的权限检查。

- [插件装载、依赖门禁与扩展发现](../../02-developer-guide/plugin-development/overview.md) — `plugins.model`
  PluginLoader的manifest/依赖门禁、禁用缓存、程序集发现和失败边界；载入不等于provider可用，也不支持隔离卸载。

- [共享接口、属性通知与粗粒度权限](../../04-api-reference/ui-components/ColorVision.Common.md) — `ui.common`
  共享接口的宿主接入、属性通知与命令的同步执行限制、粗粒度权限判据，以及第三方工具发现和启动边界。

- [配置持久化、重载与对象所有权](../../04-api-reference/ui-components/configuration.md) — `ui.configuration`
  ConfigHandler的配置路径、延迟实例、文件合并保存和重载契约；单文件替换不等于内存发布成功，重载会使旧配置引用失效。

- [UI 运行时扩展发现与排查](../../04-api-reference/ui-components/ui-runtime-handoff.md) — `ui.discovery`
  UI 扩展发现与入口缺失排查：AssemblyHandler 的程序集过滤、类型缓存和 provider 构造；刷新程序集不重建所有消费者，入口可见不证明初始化或业务完成。

- [多图查看、刷新与缩略图缓存](../../04-api-reference/ui-components/ColorVision.ImageTools.md) — `ui.image-tools`
  ImageTools内置注册、多图列表中的单张预览、刷新与SQLite缩略图缓存；重选不保证重载，关窗不清缓存，缓存关闭也不等于零数据库访问。

- [菜单：发现、显示、执行与管理提交](../../04-api-reference/ui-components/menus.md) — `ui.menus`
  菜单管理器的可见性（Visible）、位置、排序和全目标重置（Reset）；插件 DLL 发现、类型缓存、父子树和管理提交；IHotKey 提示随运行时键位更新，隐藏不禁用快捷键，Apply 成功提示不保证配置落盘，菜单入口不构成统一鉴权。

- [PropertyGrid 属性编辑契约](../../04-api-reference/ui-components/property-grid.md) — `ui.property-grid`
  属性面板的字段生成、编辑器选择和 Flow 适配；区分直接修改、工作副本、关闭、重置与宿主持久化。

- [应用搜索：入口、候选与执行](../../04-api-reference/ui-components/search.md) — `ui.search`
  应用搜索窗口的入口、关键词匹配、候选来源、缓存刷新与命令执行；Ctrl+F 按焦点执行局部查找，Ctrl+Shift+P 打开应用搜索。

- [设置窗口：发现、编辑与关闭契约](../../04-api-reference/ui-components/settings.md) — `ui.settings`
  设置窗口的元数据发现、全局搜索定位、侧栏筛选与活对象编辑；普通选项关窗不撤销，启动检查更新仍是聚合开关。

- [状态栏：发现、刷新与宿主生命周期](../../04-api-reference/ui-components/status-bar.md) — `ui.status-bar`
  状态栏的插件发现、活动文档通知、绑定更新、控件重建和关闭生命周期；刷新不保证发现新provider，隐藏不等于保存偏好或停止采样。

- [配置向导：步骤、应用与完成边界](../../04-api-reference/ui-components/wizards.md) — `ui.wizards`
  配置向导的步骤发现、初始化时序、前进应用和完成标记；关闭不回滚，完成标记不证明组件健康或重启成功。

- [JSON 模板](../../04-api-reference/algorithms/templates/json-templates.md) — `algorithms.json-templates`
  JSON模板的文本/属性编辑、数据库保存、默认参数与重置；校验Json按钮只同步模型，Schema提供字段提示而不补默认值或执行完整校验。

- [系统监控（SystemMonitor）](../../04-api-reference/plugins/standard-plugins/system-monitor.md) — `plugins.system-monitor`
  系统监控的 CPU/RAM 采样、手动刷新与状态栏生命周期；缓存大小包含子目录，清理只删顶层文件，逐文件失败不会单独提示。

- [UI 包职责与依赖边界](../../04-api-reference/ui-components/component-handbook.md) — `ui.package-boundaries`
  UI类库的职责、依赖与目标框架/版本兼容；包版本可独立于主程序，中立算法与窗口适配分层维护。

## UI/ColorVision.Core {#module-55492f436f6c6f72566973696f6e2e436f7265}

- [本地十字定位 FindCross](../../04-api-reference/algorithms/detectors/find-cross.md) — `algorithms.find-cross`
  本地十字定位的图像菜单、Flow 节点、生产参数、全图坐标、原生返回值与失败诊断。

- [发光区定位：远端模板与本地 V2](../../04-api-reference/algorithms/templates/find-light-area.md) — `algorithms.find-light-area`
  发光区定位1与本地发光区定位(V2)的使用、图像来源、POI保存模板和结果边界；区分算法拒绝、数据库提交与消息发布，并说明模板字典恢复不一致。

- [本地灯珠与 P2 分析](../../04-api-reference/algorithms/local-native-analysis.md) — `algorithms.local-native-analysis`
  ImageEditor 本地灯珠、Ghost、旋转模板和双目标定融合的操作、参数与结果；灯珠暗区候选不完整，P2 运行失败后复制结果可能仍取上次 JSON。

- [日志来源、历史读取与筛选](../../01-user-guide/interface/log-viewer.md) — `operations.logs`
  区分log4net输出、历史文件读取与UI筛选，说明刷新、截断和原生日志采集边界；没有显示不等于动作未发生。

- [源图像帧：租约、位图复制与缓存失效](../../04-api-reference/ui-components/image-frame-lifetime.md) — `ui.image-frames`
  位图读取时借用原图内存与复制像素的区别、租约释放责任和缓存版本；原图修改须显式失效，复制HImage不延长租约。

- [景深融合：输入、执行与结果生命周期](../../04-api-reference/ui-components/image-fusion.md) — `ui.image-fusion`
  景深融合的文件准备、CPU/CUDA执行、结果另存与计时；自动模式不做失败回退，关窗不取消计算，GPU的2–4张输入存在越界风险。

- [系统要求与首次构建](../../00-getting-started/prerequisites.md) — `delivery.prerequisites`
  Windows x64 运行与源码构建前提：Desktop Runtime、SDK、C++ 工具集及已有 native DLL 的选择。

- [OpenCV 和 native 集成开发指南](../../02-developer-guide/engine-development/opencv-integration.md) — `engine.native-integration`
  native ABI与HImage所有权、函数族返回值、视频异步/关闭边界，以及helper构建和CUDA发布输入；路由校准Context与POI原生参考。

- [UI NuGet 包构建与发布](../../04-api-reference/ui-components/publishing.md) — `ui.publishing`
  UI NuGet整批与Algorithms单包发布、Release标签和版本预检；预检不预留版本，逐包上传没有整批回滚或逐条失败检查。

- [opencv\_helper.dll API 参考](../../04-api-reference/engine-components/opencv-helper-api.md) — `engine.opencv-helper-api`
  opencv\_helper 英文 API 参考：校准/POI、图像处理、SFR、检测、视频与内存释放；核对真实参数单位和函数族错误码，声明的选项不等于当前 Engine 提供操作入口。

- [ColorVision.Core](../../04-api-reference/ui-components/ColorVision.Core.md) — `ui.core`
  定位 HImage 所有权、OpenCV/CUDA PInvoke、ImageCompute 融合分流、位图桥接与默认关闭的原生日志。

- [UI 包职责与依赖边界](../../04-api-reference/ui-components/component-handbook.md) — `ui.package-boundaries`
  UI类库的职责、依赖与目标框架/版本兼容；包版本可独立于主程序，中立算法与窗口适配分层维护。

## UI/ColorVision.Database {#module-55492f436f6c6f72566973696f6e2e4461746162617365}

- [数据所有者与存储定位](../../01-user-guide/data-management/README.md) — `operations.data`
  按设置JSON、Engine MySQL、模块SQLite和结果文件定位数据所有者；有记录、有图片、已导出和已备份不是同一状态。

- [Ghost1.0 鬼影检测](../../04-api-reference/algorithms/detectors/ghost-detection.md) — `algorithms.ghost`
  Ghost1.0 鬼影检测的模板、颜色和请求入口；说明数据库明细、首条结果叠图、全部明细 CSV 追加导出及读取失败边界。

- [MySQL 结果清理、备份与失败边界](../../04-api-reference/engine-components/mysql-maintenance.md) — `engine.mysql-maintenance`
  MySQL 批次与结果表的历史删除、整表截断和SQL备份；统计不是清理预览，无全程事务或自动恢复，主从选择和管理员权限不能只依赖界面提示。

- [MySQL SQL 恢复、重置与资源保留](../../04-api-reference/engine-components/mysql-recovery.md) — `engine.mysql-recovery`
  MySQL手动SQL恢复、数据库重置与资源保留：导入后才同步配置和重启注册中心，失败不回滚；迁移备份不含结果，配置更新计数不证明键完整。

- [数据库连接、DAO 与旧插件兼容](../../04-api-reference/ui-components/ColorVision.Database.md) — `ui.database`
  MySQL 连接配置、业务 DAO 与批 SQL 的失败边界，以及旧插件注册的二进制兼容。

- [通用查询、条件会话与整表操作](../../04-api-reference/ui-components/database-query.md) — `ui.database-query`
  实体驱动的通用查询窗口：条件参数化、执行时SQL预览、结果替换与进程内会话；关闭不取消查询，清空表/截断表作用于整表而非筛选结果。

- [SQLite 正文存储、迁移与文件维护](../../04-api-reference/ui-components/sqlite-storage.md) — `ui.sqlite-storage`
  Socket 与 Flow 的 SQLite 正文 gzip 编解码、按ID读写、旧TEXT逐批迁移、WAL备份与VACUUM；通用工具不自动停写/备份/恢复，失败可能已有批次提交。

- [UI 包职责与依赖边界](../../04-api-reference/ui-components/component-handbook.md) — `ui.package-boundaries`
  UI类库的职责、依赖与目标框架/版本兼容；包版本可独立于主程序，中立算法与窗口适配分层维护。

## UI/ColorVision.ImageEditor {#module-55492f436f6c6f72566973696f6e2e496d616765456469746f72}

- [UI 组件目录](../../04-api-reference/ui-components/control-catalog.md) — `ui.control-catalog`
  按控件、窗口和扩展接口定位对应 UI 源码与专题。

- [UI 知识入口](../../04-api-reference/ui-components/README.md) — `ui.index`
  按问题路由到 UI 模块、属性编辑契约、运行时发现与 DLL 发布证据。

- [本地十字定位 FindCross](../../04-api-reference/algorithms/detectors/find-cross.md) — `algorithms.find-cross`
  本地十字定位的图像菜单、Flow 节点、生产参数、全图坐标、原生返回值与失败诊断。

- [发光区定位：远端模板与本地 V2](../../04-api-reference/algorithms/templates/find-light-area.md) — `algorithms.find-light-area`
  发光区定位1与本地发光区定位(V2)的使用、图像来源、POI保存模板和结果边界；区分算法拒绝、数据库提交与消息发布，并说明模板字典恢复不一致。

- [本地灯珠与 P2 分析](../../04-api-reference/algorithms/local-native-analysis.md) — `algorithms.local-native-analysis`
  ImageEditor 本地灯珠、Ghost、旋转模板和双目标定融合的操作、参数与结果；灯珠暗区候选不完整，P2 运行失败后复制结果可能仍取上次 JSON。

- [统一图像算法平台 V1](../../02-developer-guide/core-concepts/image-algorithm-platform-v1.md) — `algorithms.platform`
  统一图像算法Catalog、Invocation和Runner；普通像素预览、应用/取消、所有权与发布门禁；ONNX仅设计。

- [Copilot 扩展、MCP 与 Hook](../../02-developer-guide/core-concepts/copilot-agent-extensions.md) — `copilot.extensions`
  业务模块动态上下文、外部 MCP client 和 Hook 如何进入统一宿主权限与生命周期。

- [CV 文件读取、通道与写回契约](../../04-api-reference/engine-components/ColorVision.FileIO.md) — `engine.file-io`
  CVRAW/CVCIE 读取、内嵌 XYZ 真彩显示与原图回退、手动校正数值校验，以及版本写回和失败边界。

- [Engine 结果展示链路](../../04-api-reference/engine-components/result-handoff-chain.md) — `engine.results`
  区分 Engine 历史结果 handler、项目业务结果和统一算法 overlay 的注册及生命周期。

- [系统职责与跨模块边界](../../03-architecture/overview/system-overview.md) — `platform.system`
  宿主、UI、Engine、插件与项目的职责及调用边界：UI操作不必经过Engine，程序集依赖不是统一执行顺序，构建产物不等于交付制品。

- [UI 运行时扩展发现与排查](../../04-api-reference/ui-components/ui-runtime-handoff.md) — `ui.discovery`
  UI 扩展发现与入口缺失排查：AssemblyHandler 的程序集过滤、类型缓存和 provider 构造；刷新程序集不重建所有消费者，入口可见不证明初始化或业务完成。

- [ColorVision.ImageEditor：打开、绘制与输出](../../04-api-reference/ui-components/ColorVision.ImageEditor.md) — `ui.image-editor`
  图像/视频打开、绘图撤销、叠加层、3D 与快照输出边界，区分渲染图、当前源像素和重读源文件的模型导出。

- [ImageEditor：上下文、工具装配与临时选区](../../04-api-reference/ui-components/image-editor-context.md) — `ui.image-editor-context`
  ImageEditor 的状态归属、扩展构造、工具刷新与临时 ROI 有效期；区分配置分类、图像版本和真实像素坐标。

- [源图像帧：租约、位图复制与缓存失效](../../04-api-reference/ui-components/image-frame-lifetime.md) — `ui.image-frames`
  位图读取时借用原图内存与复制像素的区别、租约释放责任和缓存版本；原图修改须显式失效，复制HImage不延长租约。

- [状态栏：发现、刷新与宿主生命周期](../../04-api-reference/ui-components/status-bar.md) — `ui.status-bar`
  状态栏的插件发现、活动文档通知、绑定更新、控件重建和关闭生命周期；刷新不保证发现新provider，隐藏不等于保存偏好或停止采样。

- [设置、流程与结果的导入导出边界](../../01-user-guide/data-management/export-import.md) — `operations.exports`
  按设置、流程、图像和项目结果定位导入导出实现，说明配置覆盖、文件验收与迁移边界。

- [UI NuGet 包构建与发布](../../04-api-reference/ui-components/publishing.md) — `ui.publishing`
  UI NuGet整批与Algorithms单包发布、Release标签和版本预检；预检不预留版本，逐包上传没有整批回滚或逐条失败检查。

- [Blob / 连通域 V1（M5.1）](../../02-developer-guide/core-concepts/blob-analysis-v1.md) — `algorithms.blob-analysis`
  BlobAnalysis 保留实现的参数、结果与验证契约；默认运行时由 Experimental 门禁拒绝执行。

- [圆拟合 V1（M6.3）](../../02-developer-guide/core-concepts/circle-fit-v1.md) — `algorithms.circle-fit`
  CircleFit 保留实现的参数、结果与验证契约；默认运行时由 Experimental 门禁拒绝执行。

- [轮廓提取 V1（M5.2）](../../02-developer-guide/core-concepts/contour-analysis-v1.md) — `algorithms.contour-analysis`
  ContourAnalysis 保留实现的参数、结果与验证契约；默认运行时由 Experimental 门禁拒绝执行。

- [FFT / 频域分析 V1（M10）](../../02-developer-guide/core-concepts/frequency-spectrum-v1.md) — `algorithms.frequency-spectrum`
  FrequencySpectrum 保留实现的参数、结果与验证契约；默认运行时由 Experimental 门禁拒绝执行。

- [几何变换 V1（M7）](../../02-developer-guide/core-concepts/geometric-transform-v1.md) — `algorithms.geometric-transform`
  GeometricTransform 的输入、参数、结果、宿主接入与定向验证契约。

- [图像比较 V1（M3–M4）](../../02-developer-guide/core-concepts/image-comparison-v1.md) — `algorithms.image-comparison`
  ImageComparison 当前行为版本 1.1、schema 2 的双输入比较、ROI、SSIM、对齐预检、输出预算及 schema 1 迁移契约。

- [灰度与颜色剖面 V1（M2）](../../02-developer-guide/core-concepts/image-profile-v1.md) — `algorithms.image-profile`
  ImageProfile 的输入、参数、结果、宿主接入与定向验证契约。

- [图像配准 V1（M8.1）](../../02-developer-guide/core-concepts/image-registration-v1.md) — `algorithms.image-registration`
  ImageRegistration 的输入、参数、结果、宿主接入与定向验证契约。

- [成像校正 V1（M9）](../../02-developer-guide/core-concepts/imaging-correction-v1.md) — `algorithms.imaging-correction`
  ImagingCorrection 的输入、参数、结果、宿主接入与定向验证契约。

- [镜头畸变校正 V1（M8.2）](../../02-developer-guide/core-concepts/lens-distortion-correction-v1.md) — `algorithms.lens-distortion-correction`
  LensDistortionCorrection 的输入、参数、结果、宿主接入与定向验证契约。

- [直线拟合 V1（M6.2）](../../02-developer-guide/core-concepts/line-fit-v1.md) — `algorithms.line-fit`
  LineFit 保留实现的参数、结果与验证契约；默认运行时由 Experimental 门禁拒绝执行。

- [摩尔纹分析 V1（M11）](../../02-developer-guide/core-concepts/moire-analysis-v1.md) — `algorithms.moire-analysis`
  MoireAnalysis 保留实现的参数、结果与验证契约；默认运行时由 Experimental 门禁拒绝执行。

- [ROI 统计 V1（M1）](../../02-developer-guide/core-concepts/roi-statistics-v1.md) — `algorithms.roi-statistics`
  RoiStatistics 的输入、参数、结果、宿主接入与定向验证契约。

- [亚像素边缘 V1（M6.1）](../../02-developer-guide/core-concepts/subpixel-edge-v1.md) — `algorithms.subpixel-edge`
  SubpixelEdge 保留实现的参数、结果与验证契约；默认运行时由 Experimental 门禁拒绝执行。

- [ColorVision 概览](../../00-getting-started/what-is-colorvision.md) — `platform.product`
  ColorVision 的设备、流程、图像分析、结果、插件与客户项目能力，以及从任务进入文档的方法。

- [UI 包职责与依赖边界](../../04-api-reference/ui-components/component-handbook.md) — `ui.package-boundaries`
  UI类库的职责、依赖与目标框架/版本兼容；包版本可独立于主程序，中立算法与窗口适配分层维护。

- [ONNX / AI 推理接入设计（Deferred） \[规划\]](../../02-developer-guide/core-concepts/onnx-inference-future-design.md) — `algorithms.onnx`
  尚未实现的 ONNX 接入设计：保持基础产品无 ONNX 运行时，新增 adapter 前须明确模型与验收门禁。

- [本地相机内存帧预览方案（待实施） \[规划\]](../../02-developer-guide/engine-development/local-camera-memory-preview.md) — `engine.camera-preview-plan`
  待实施的设备级内存帧预览单一方案：发布器、租约、latest-wins、RAW/CIE 模式、内存预算、实施阶段与验收。

## UI/ColorVision.ImageTools {#module-55492f436f6c6f72566973696f6e2e496d616765546f6f6c73}

- [景深融合：输入、执行与结果生命周期](../../04-api-reference/ui-components/image-fusion.md) — `ui.image-fusion`
  景深融合的文件准备、CPU/CUDA执行、结果另存与计时；自动模式不做失败回退，关窗不取消计算，GPU的2–4张输入存在越界风险。

- [多图查看、刷新与缩略图缓存](../../04-api-reference/ui-components/ColorVision.ImageTools.md) — `ui.image-tools`
  ImageTools内置注册、多图列表中的单张预览、刷新与SQLite缩略图缓存；重选不保证重载，关窗不清缓存，缓存关闭也不等于零数据库访问。

- [存储清理与选择性设置重置](../../04-api-reference/ui-components/storage-maintenance.md) — `ui.storage-maintenance`
  设置中的日志、缓存、安装包扫描与清理，以及配置恢复点和选择性启动重置；先确认白名单清单，保护活跃任务和业务数据，删除不回滚，重置先独立备份。

- [UI 包职责与依赖边界](../../04-api-reference/ui-components/component-handbook.md) — `ui.package-boundaries`
  UI类库的职责、依赖与目标框架/版本兼容；包版本可独立于主程序，中立算法与窗口适配分层维护。

## UI/ColorVision.Rbac {#module-55492f436f6c6f72566973696f6e2e52626163}

- [RBAC：登录缓存、会话与权限边界](../../03-architecture/security/rbac.md) — `platform.rbac`
  本地RBAC的登录缓存、会话校验和权限同步限制，以及自动登录失败、登出撤销和用户中心统计的实际边界。

- [权限边界与鉴权入口](../../03-architecture/security/overview.md) — `platform.security`
  区分应用管理员、RBAC会话与权限码、Windows服务身份及远程/工具授权；登录缓存和界面状态不能替代执行入口的权限检查。

- [共享接口、属性通知与粗粒度权限](../../04-api-reference/ui-components/ColorVision.Common.md) — `ui.common`
  共享接口的宿主接入、属性通知与命令的同步执行限制、粗粒度权限判据，以及第三方工具发现和启动边界。

- [UI 包职责与依赖边界](../../04-api-reference/ui-components/component-handbook.md) — `ui.package-boundaries`
  UI类库的职责、依赖与目标框架/版本兼容；包版本可独立于主程序，中立算法与窗口适配分层维护。

## UI/ColorVision.Scheduler {#module-55492f436f6c6f72566973696f6e2e5363686564756c6572}

- [UI 运行时扩展发现与排查](../../04-api-reference/ui-components/ui-runtime-handoff.md) — `ui.discovery`
  UI 扩展发现与入口缺失排查：AssemblyHandler 的程序集过滤、类型缓存和 provider 构造；刷新程序集不重建所有消费者，入口可见不证明初始化或业务完成。

- [任务计划程序：创建、调度与执行历史](../../04-api-reference/ui-components/ColorVision.Scheduler.md) — `ui.scheduler`
  任务计划程序的状态栏入口、创建步骤、调度参数、启动恢复和执行历史；暂停只限制后续触发，重启按保存的定义重新调度。

- [状态栏：发现、刷新与宿主生命周期](../../04-api-reference/ui-components/status-bar.md) — `ui.status-bar`
  状态栏的插件发现、活动文档通知、绑定更新、控件重建和关闭生命周期；刷新不保证发现新provider，隐藏不等于保存偏好或停止采样。

- [UI 包职责与依赖边界](../../04-api-reference/ui-components/component-handbook.md) — `ui.package-boundaries`
  UI类库的职责、依赖与目标框架/版本兼容；包版本可独立于主程序，中立算法与窗口适配分层维护。

## UI/ColorVision.SocketProtocol {#module-55492f436f6c6f72566973696f6e2e536f636b657450726f746f636f6c}

- [数据所有者与存储定位](../../01-user-guide/data-management/README.md) — `operations.data`
  按设置JSON、Engine MySQL、模块SQLite和结果文件定位数据所有者；有记录、有图片、已导出和已备份不是同一状态。

- [数据库清理窗口、能力接入与完成边界](../../04-api-reference/engine-components/database-maintenance.md) — `engine.database-maintenance`
  数据库清理窗口与provider能力：表统计不是删除预览，确认只固定部分参数；备份默认关闭、组合维护不是事务，关窗不取消，成功与统计刷新分开。

- [跨模块运行问题定位](../../01-user-guide/README.md) — `operations.index`
  从启动、配置、日志、设备、流程和结果现象定位代码责任，区分已完成阶段与待验证阶段，避免用重启或改数据代替诊断。

- [Spectrum Socket 业务指令与完成边界](../../04-api-reference/plugins/standard-plugins/spectrum-socket.md) — `plugins.spectrum-socket`
  Spectrum Socket 的启用与状态查询、五个指令的参数和返回值；连接成功与标定就绪不同，30/60 秒取消不保证原生操作按时停止。

- [通用查询、条件会话与整表操作](../../04-api-reference/ui-components/database-query.md) — `ui.database-query`
  实体驱动的通用查询窗口：条件参数化、执行时SQL预览、结果替换与进程内会话；关闭不取消查询，清空表/截断表作用于整表而非筛选结果。

- [UI 运行时扩展发现与排查](../../04-api-reference/ui-components/ui-runtime-handoff.md) — `ui.discovery`
  UI 扩展发现与入口缺失排查：AssemblyHandler 的程序集过滤、类型缓存和 provider 构造；刷新程序集不重建所有消费者，入口可见不证明初始化或业务完成。

- [TCP 监听、协议分发与消息记录](../../04-api-reference/ui-components/ColorVision.SocketProtocol.md) — `ui.socket-protocol`
  TCP网络通信的监听快照、窗口关闭与服务停止、JSON/Text分发及消息记录；Sent不证明对端执行，重发可能换客户端并追加记录。

- [SQLite 正文存储、迁移与文件维护](../../04-api-reference/ui-components/sqlite-storage.md) — `ui.sqlite-storage`
  Socket 与 Flow 的 SQLite 正文 gzip 编解码、按ID读写、旧TEXT逐批迁移、WAL备份与VACUUM；通用工具不自动停写/备份/恢复，失败可能已有批次提交。

- [ARVRPro TCP 通讯协议](../../04-api-reference/projects/project-arvr-pro-protocol.md) — `projects.arvr-pro-protocol`
  ARVRPro TCP/JSON 对接：初始化与 RunAll、流程启用设置、切图确认、AOI 中转、状态码和最终结果关联；说明分帧与并发会话限制。

- [LUX TCP 通讯协议](../../04-api-reference/projects/project-lux-protocol.md) — `projects.lux-protocol`
  LUX TCP 文本协议的 T0000 握手、VID、光学中心、光通量与 SocketCode 流程，说明响应字段、状态码、分帧及共享会话限制。

- [UI 包职责与依赖边界](../../04-api-reference/ui-components/component-handbook.md) — `ui.package-boundaries`
  UI类库的职责、依赖与目标框架/版本兼容；包版本可独立于主程序，中立算法与窗口适配分层维护。

## UI/ColorVision.Solution {#module-55492f436f6c6f72566973696f6e2e536f6c7574696f6e}

- [UI 组件目录](../../04-api-reference/ui-components/control-catalog.md) — `ui.control-catalog`
  按控件、窗口和扩展接口定位对应 UI 源码与专题。

- [主窗口与入口装配](../../01-user-guide/interface/main-window.md) — `operations.main-window`
  主窗口如何挂接菜单、搜索、状态栏和工作区，以及现代停靠外观的主题覆盖与交互边界。

- [终端进程、会话与脚本运行](../../01-user-guide/interface/terminal.md) — `operations.terminal`
  定义内嵌ConPTY会话、编辑器Python运行与外部CMD入口，区分命令提交、脚本结束、shell退出和强制释放。

- [UI 运行时扩展发现与排查](../../04-api-reference/ui-components/ui-runtime-handoff.md) — `ui.discovery`
  UI 扩展发现与入口缺失排查：AssemblyHandler 的程序集过滤、类型缓存和 provider 构造；刷新程序集不重建所有消费者，入口可见不证明初始化或业务完成。

- [编辑器选择、文档生命周期与停靠布局](../../04-api-reference/ui-components/editor-document-lifecycle.md) — `ui.documents`
  编辑器注册与选择、按路径和编辑器区分文档、保存重载关闭及外部变更；停靠布局不恢复未注册文件标签，重置也不预审脏文档。

- [快捷键：发现、注册、编辑与释放](../../04-api-reference/ui-components/hotkeys.md) — `ui.hotkeys`
  快捷键的发现、多组绑定、窗口/全局注册与搜索编辑；同一操作共享作用域，未分配操作保留展示，确认后立即保存，注册或持久化失败按结果补偿。

- [资源打开与单工作区切换](../../04-api-reference/ui-components/ColorVision.Solution.md) — `ui.solution`
  工作区与普通文件的打开分流、单工作区切换和取消、私有cvsln与共享配置恢复；打开和加载不保证无写入。

- [状态栏：发现、刷新与宿主生命周期](../../04-api-reference/ui-components/status-bar.md) — `ui.status-bar`
  状态栏的插件发现、活动文档通知、绑定更新、控件重建和关闭生命周期；刷新不保证发现新provider，隐藏不等于保存偏好或停止采样。

- [主程序启动与最小图像验证](../../00-getting-started/first-steps.md) — `operations.first-run`
  主程序启动的配置、实例和服务副作用，以及隔离测试环境中的最小本地图像验证。

- [UI 包职责与依赖边界](../../04-api-reference/ui-components/component-handbook.md) — `ui.package-boundaries`
  UI类库的职责、依赖与目标框架/版本兼容；包版本可独立于主程序，中立算法与窗口适配分层维护。

## UI/ColorVision.Themes {#module-55492f436f6c6f72566973696f6e2e5468656d6573}

- [主窗口与入口装配](../../01-user-guide/interface/main-window.md) — `operations.main-window`
  主窗口如何挂接菜单、搜索、状态栏和工作区，以及现代停靠外观的主题覆盖与交互边界。

- [主题选择、资源应用与窗口外观](../../04-api-reference/ui-components/ColorVision.Themes.md) — `ui.themes`
  在外观与语言中切换主题；ThemeManager 的资源应用、系统跟随、窗口外观和公共控件样式，以及即时预览与保存的区别。

- [UI NuGet 包构建与发布](../../04-api-reference/ui-components/publishing.md) — `ui.publishing`
  UI NuGet整批与Algorithms单包发布、Release标签和版本预检；预检不预留版本，逐包上传没有整批回滚或逐条失败检查。

- [UI 包职责与依赖边界](../../04-api-reference/ui-components/component-handbook.md) — `ui.package-boundaries`
  UI类库的职责、依赖与目标框架/版本兼容；包版本可独立于主程序，中立算法与窗口适配分层维护。

## UI/ColorVision.UI {#module-55492f436f6c6f72566973696f6e2e5549}

- [数据所有者与存储定位](../../01-user-guide/data-management/README.md) — `operations.data`
  按设置JSON、Engine MySQL、模块SQLite和结果文件定位数据所有者；有记录、有图片、已导出和已备份不是同一状态。

- [扩展性开发](../../02-developer-guide/core-concepts/extensibility.md) — `platform.extensibility`
  按插件、菜单、属性面板、设备、Flow、算法和结果扩展任务定位实现与当前契约。

- [插件装配与模块知识入口](../../04-api-reference/plugins/README.md) — `plugins.index`
  从插件程序集装载、产物安装和具体模块能力定位源码；同一责任不再分开发手册与使用手册。

- [UI 组件目录](../../04-api-reference/ui-components/control-catalog.md) — `ui.control-catalog`
  按控件、窗口和扩展接口定位对应 UI 源码与专题。

- [ColorVision.UI 壳层责任与知识入口](../../04-api-reference/ui-components/ColorVision.UI.md) — `ui.framework`
  ColorVision.UI壳层责任入口：按配置、插件、菜单、热键、搜索、语言、状态栏、属性编辑和日志定位规范主题，业务行为仍归所属模块。

- [UI 知识入口](../../04-api-reference/ui-components/README.md) — `ui.index`
  按问题路由到 UI 模块、属性编辑契约、运行时发现与 DLL 发布证据。

- [模板编辑入口与菜单契约](../../04-api-reference/algorithms/templates/template-menu-entries.md) — `algorithms.template-menus`
  从模板菜单、算法面板或应用搜索打开模板；说明选择索引、流程设计器直达和菜单发现的边界。

- [Copilot 设置、持久化与连接诊断](../../02-developer-guide/core-concepts/copilot-configuration.md) — `copilot.configuration`
  ColorVision内置Copilot的设置草稿、配置保存与运行态发布、模型选择和联网诊断；保存失败可能已落盘，Local MCP测试核验会话握手与只读状态调用。

- [检查更新、重新安装与程序备份](../../02-developer-guide/deployment/auto-update.md) — `delivery.update`
  检查更新、重新安装与程序备份入口，以及主程序和插件的检查复用、下载安装、失败回退与启动恢复。

- [更新扫描保护：临时排除项与清理所有权](../../02-developer-guide/deployment/update-scan-protection.md) — `delivery.update-scan-protection`
  ServiceHost提供的主程序增量更新临时Defender排除项、目录准入和清理所有权；启用失败不阻断更新，服务停止或保护超时不保证排除项立即恢复。

- [CVRAW / CVCIE 图像导出](../../04-api-reference/engine-components/cv-image-export.md) — `engine.cv-image-export`
  CVRAW/CVCIE 原生导出的窗口、命令行参数、通道和命名规则，以及覆盖、部分失败和退出码边界。

- [数据库清理窗口、能力接入与完成边界](../../04-api-reference/engine-components/database-maintenance.md) — `engine.database-maintenance`
  数据库清理窗口与provider能力：表统计不是删除预览，确认只固定部分参数；备份默认关闭、组合维护不是事务，关窗不取消，成功与统计刷新分开。

- [Engine 设备资源与运行装配](../../04-api-reference/engine-components/device-service-chain.md) — `engine.devices`
  设备工厂、资源重载与显示装配；旧对象释放、集合重建和显示替换并非一个事务，记录存在、默认可见、服务在线和动作完成分别判断。

- [MySQL SQL 恢复、重置与资源保留](../../04-api-reference/engine-components/mysql-recovery.md) — `engine.mysql-recovery`
  MySQL手动SQL恢复、数据库重置与资源保留：导入后才同步配置和重启注册中心，失败不回滚；迁移备份不含结果，配置更新计数不证明键完整。

- [Explorer 缩略图读取与 COM 注册](../../04-api-reference/engine-components/ColorVision.ShellExtension.md) — `engine.shell-extension`
  Explorer 的 CVRAW/CVCIE COM provider 如何读取像素、生成非测量用途缩略图，以及源码脚本与 ServiceHost 注册的不同副作用和失败边界。

- [模板注册、参数与持久化](../../03-architecture/components/templates/design.md) — `engine.template-design`
  TemplateControl注册与普通ITemplate\<T\>参数加载、保存、复制和删除契约；注册、内存变更和数据库成功是不同状态，JSON与Flow另有实现。

- [跨模块运行问题定位](../../01-user-guide/README.md) — `operations.index`
  从启动、配置、日志、设备、流程和结果现象定位代码责任，区分已完成阶段与待验证阶段，避免用重启或改数据代替诊断。

- [日志来源、历史读取与筛选](../../01-user-guide/interface/log-viewer.md) — `operations.logs`
  区分log4net输出、历史文件读取与UI筛选，说明刷新、截断和原生日志采集边界；没有显示不等于动作未发生。

- [主窗口与入口装配](../../01-user-guide/interface/main-window.md) — `operations.main-window`
  主窗口如何挂接菜单、搜索、状态栏和工作区，以及现代停靠外观的主题覆盖与交互边界。

- [终端进程、会话与脚本运行](../../01-user-guide/interface/terminal.md) — `operations.terminal`
  定义内嵌ConPTY会话、编辑器Python运行与外部CMD入口，区分命令提交、脚本结束、shell退出和强制释放。

- [系统开发工具管理](../../02-developer-guide/core-concepts/developer-tools-manager.md) — `platform.developer-tools`
  独立开发工具窗口发现系统 Python、Node.js/npm，并由用户选择校验后启动官方安装向导；不托管项目环境，不自动改默认版本。

- [启动、初始化与故障恢复](../../03-architecture/overview/runtime.md) — `platform.runtime`
  启动顺序与故障恢复：初始化进度和ready不代表全部成功，运行期维护区分浏览、禁用、文档准备与重启，一次性插件跳过不绕过真实故障。

- [ColorVisionServiceHost：本机权限代理与生命周期](../../03-architecture/components/service-host.md) — `platform.service-host`
  ColorVision 服务主机的状态刷新、安装修复、日志诊断、身份票据与就绪条件；自动刷新只更新日志，客户端超时不取消命令，服务停止超过两分钟仍等待排空，服务启动成功日志不证明后台清理和启动完整性检查完成。

- [系统职责与跨模块边界](../../03-architecture/overview/system-overview.md) — `platform.system`
  宿主、UI、Engine、插件与项目的职责及调用边界：UI操作不必经过Engine，程序集依赖不是统一执行顺序，构建产物不等于交付制品。

- [插件产物、安装与交付](../../02-developer-guide/plugin-development/getting-started.md) — `plugins.getting-started`
  插件构建产物、HostCopy、manifest包身份、安装替换和恢复契约；发布会上传，安装器返回不等于替换或重启后加载成功。

- [插件装载、依赖门禁与扩展发现](../../02-developer-guide/plugin-development/overview.md) — `plugins.model`
  PluginLoader的manifest/依赖门禁、禁用缓存、程序集发现和失败边界；载入不等于provider可用，也不支持隔离卸载。

- [WindowsServicePlugin：选包、本机安装与恢复](../../04-api-reference/plugins/standard-plugins/windows-service.md) — `plugins.windows-service`
  WindowsServicePlugin的在线选包与缓存、本机完整安装、数据库版本切换和恢复边界；下载、日志完成、备份与实际服务状态不能互相替代。

- [共享接口、属性通知与粗粒度权限](../../04-api-reference/ui-components/ColorVision.Common.md) — `ui.common`
  共享接口的宿主接入、属性通知与命令的同步执行限制、粗粒度权限判据，以及第三方工具发现和启动边界。

- [配置持久化、重载与对象所有权](../../04-api-reference/ui-components/configuration.md) — `ui.configuration`
  ConfigHandler的配置路径、延迟实例、文件合并保存和重载契约；单文件替换不等于内存发布成功，重载会使旧配置引用失效。

- [UI 运行时扩展发现与排查](../../04-api-reference/ui-components/ui-runtime-handoff.md) — `ui.discovery`
  UI 扩展发现与入口缺失排查：AssemblyHandler 的程序集过滤、类型缓存和 provider 构造；刷新程序集不重建所有消费者，入口可见不证明初始化或业务完成。

- [编辑器选择、文档生命周期与停靠布局](../../04-api-reference/ui-components/editor-document-lifecycle.md) — `ui.documents`
  编辑器注册与选择、按路径和编辑器区分文档、保存重载关闭及外部变更；停靠布局不恢复未注册文件标签，重置也不预审脏文档。

- [快捷键：发现、注册、编辑与释放](../../04-api-reference/ui-components/hotkeys.md) — `ui.hotkeys`
  快捷键的发现、多组绑定、窗口/全局注册与搜索编辑；同一操作共享作用域，未分配操作保留展示，确认后立即保存，注册或持久化失败按结果补偿。

- [ImageEditor：上下文、工具装配与临时选区](../../04-api-reference/ui-components/image-editor-context.md) — `ui.image-editor-context`
  ImageEditor 的状态归属、扩展构造、工具刷新与临时 ROI 有效期；区分配置分类、图像版本和真实像素坐标。

- [界面语言：资源发现、配置与重启](../../04-api-reference/ui-components/localization.md) — `ui.localization`
  界面语言的资源发现、系统语言回退、设置绑定和重启切换；语言下拉框不证明插件翻译完整，修改配置值不等于刷新窗口。

- [菜单：发现、显示、执行与管理提交](../../04-api-reference/ui-components/menus.md) — `ui.menus`
  菜单管理器的可见性（Visible）、位置、排序和全目标重置（Reset）；插件 DLL 发现、类型缓存、父子树和管理提交；IHotKey 提示随运行时键位更新，隐藏不禁用快捷键，Apply 成功提示不保证配置落盘，菜单入口不构成统一鉴权。

- [PropertyGrid 属性编辑契约](../../04-api-reference/ui-components/property-grid.md) — `ui.property-grid`
  属性面板的字段生成、编辑器选择和 Flow 适配；区分直接修改、工作副本、关闭、重置与宿主持久化。

- [任务计划程序：创建、调度与执行历史](../../04-api-reference/ui-components/ColorVision.Scheduler.md) — `ui.scheduler`
  任务计划程序的状态栏入口、创建步骤、调度参数、启动恢复和执行历史；暂停只限制后续触发，重启按保存的定义重新调度。

- [应用搜索：入口、候选与执行](../../04-api-reference/ui-components/search.md) — `ui.search`
  应用搜索窗口的入口、关键词匹配、候选来源、缓存刷新与命令执行；Ctrl+F 按焦点执行局部查找，Ctrl+Shift+P 打开应用搜索。

- [设置窗口：发现、编辑与关闭契约](../../04-api-reference/ui-components/settings.md) — `ui.settings`
  设置窗口的元数据发现、全局搜索定位、侧栏筛选与活对象编辑；普通选项关窗不撤销，启动检查更新仍是聚合开关。

- [资源打开与单工作区切换](../../04-api-reference/ui-components/ColorVision.Solution.md) — `ui.solution`
  工作区与普通文件的打开分流、单工作区切换和取消、私有cvsln与共享配置恢复；打开和加载不保证无写入。

- [状态栏：发现、刷新与宿主生命周期](../../04-api-reference/ui-components/status-bar.md) — `ui.status-bar`
  状态栏的插件发现、活动文档通知、绑定更新、控件重建和关闭生命周期；刷新不保证发现新provider，隐藏不等于保存偏好或停止采样。

- [存储清理与选择性设置重置](../../04-api-reference/ui-components/storage-maintenance.md) — `ui.storage-maintenance`
  设置中的日志、缓存、安装包扫描与清理，以及配置恢复点和选择性启动重置；先确认白名单清单，保护活跃任务和业务数据，删除不回滚，重置先独立备份。

- [主题选择、资源应用与窗口外观](../../04-api-reference/ui-components/ColorVision.Themes.md) — `ui.themes`
  在外观与语言中切换主题；ThemeManager 的资源应用、系统跟随、窗口外观和公共控件样式，以及即时预览与保存的区别。

- [配置向导：步骤、应用与完成边界](../../04-api-reference/ui-components/wizards.md) — `ui.wizards`
  配置向导的步骤发现、初始化时序、前进应用和完成标记；关闭不回滚，完成标记不证明组件健康或重启成功。

- [设置、流程与结果的导入导出边界](../../01-user-guide/data-management/export-import.md) — `operations.exports`
  按设置、流程、图像和项目结果定位导入导出实现，说明配置覆盖、文件验收与迁移边界。

- [主程序启动与最小图像验证](../../00-getting-started/first-steps.md) — `operations.first-run`
  主程序启动的配置、实例和服务副作用，以及隔离测试环境中的最小本地图像验证。

- [JSON 模板](../../04-api-reference/algorithms/templates/json-templates.md) — `algorithms.json-templates`
  JSON模板的文本/属性编辑、数据库保存、默认参数与重置；校验Json按钮只同步模型，Schema提供字段提示而不补默认值或执行完整校验。

- [系统监控（SystemMonitor）](../../04-api-reference/plugins/standard-plugins/system-monitor.md) — `plugins.system-monitor`
  系统监控的 CPU/RAM 采样、手动刷新与状态栏生命周期；缓存大小包含子目录，清理只删顶层文件，逐文件失败不会单独提示。

- [UI 包职责与依赖边界](../../04-api-reference/ui-components/component-handbook.md) — `ui.package-boundaries`
  UI类库的职责、依赖与目标框架/版本兼容；包版本可独立于主程序，中立算法与窗口适配分层维护。

## UI/ColorVision.UI.Desktop {#module-55492f436f6c6f72566973696f6e2e55492e4465736b746f70}

- [Android 运维伴侣](../../02-developer-guide/backend/android-operations.md) — `delivery.android-operations`
  Android原生运维入口、现场HTTPS与固定签名中继的职责边界；连接、可见证据和操作授权不能互相替代。

- [Backend Operations 中继与只读概览](../../02-developer-guide/backend/operations-relay.md) — `delivery.backend-operations`
  Backend Operations 的 Bearer 与设备签名中继、任务回执和管理员只读投影；在线、排队、验签与真实动作完成各有边界。

- [检查更新、重新安装与程序备份](../../02-developer-guide/deployment/auto-update.md) — `delivery.update`
  检查更新、重新安装与程序备份入口，以及主程序和插件的检查复用、下载安装、失败回退与启动恢复。

- [插件产物、安装与交付](../../02-developer-guide/plugin-development/getting-started.md) — `plugins.getting-started`
  插件构建产物、HostCopy、manifest包身份、安装替换和恢复契约；发布会上传，安装器返回不等于替换或重启后加载成功。

- [插件装载、依赖门禁与扩展发现](../../02-developer-guide/plugin-development/overview.md) — `plugins.model`
  PluginLoader的manifest/依赖门禁、禁用缓存、程序集发现和失败边界；载入不等于provider可用，也不支持隔离卸载。

- [UI 运行时扩展发现与排查](../../04-api-reference/ui-components/ui-runtime-handoff.md) — `ui.discovery`
  UI 扩展发现与入口缺失排查：AssemblyHandler 的程序集过滤、类型缓存和 provider 构造；刷新程序集不重建所有消费者，入口可见不证明初始化或业务完成。

- [快捷键：发现、注册、编辑与释放](../../04-api-reference/ui-components/hotkeys.md) — `ui.hotkeys`
  快捷键的发现、多组绑定、窗口/全局注册与搜索编辑；同一操作共享作用域，未分配操作保留展示，确认后立即保存，注册或持久化失败按结果补偿。

- [界面语言：资源发现、配置与重启](../../04-api-reference/ui-components/localization.md) — `ui.localization`
  界面语言的资源发现、系统语言回退、设置绑定和重启切换；语言下拉框不证明插件翻译完整，修改配置值不等于刷新窗口。

- [菜单：发现、显示、执行与管理提交](../../04-api-reference/ui-components/menus.md) — `ui.menus`
  菜单管理器的可见性（Visible）、位置、排序和全目标重置（Reset）；插件 DLL 发现、类型缓存、父子树和管理提交；IHotKey 提示随运行时键位更新，隐藏不禁用快捷键，Apply 成功提示不保证配置落盘，菜单入口不构成统一鉴权。

- [应用搜索：入口、候选与执行](../../04-api-reference/ui-components/search.md) — `ui.search`
  应用搜索窗口的入口、关键词匹配、候选来源、缓存刷新与命令执行；Ctrl+F 按焦点执行局部查找，Ctrl+Shift+P 打开应用搜索。

- [设置窗口：发现、编辑与关闭契约](../../04-api-reference/ui-components/settings.md) — `ui.settings`
  设置窗口的元数据发现、全局搜索定位、侧栏筛选与活对象编辑；普通选项关窗不撤销，启动检查更新仍是聚合开关。

- [存储清理与选择性设置重置](../../04-api-reference/ui-components/storage-maintenance.md) — `ui.storage-maintenance`
  设置中的日志、缓存、安装包扫描与清理，以及配置恢复点和选择性启动重置；先确认白名单清单，保护活跃任务和业务数据，删除不回滚，重置先独立备份。

- [主题选择、资源应用与窗口外观](../../04-api-reference/ui-components/ColorVision.Themes.md) — `ui.themes`
  在外观与语言中切换主题；ThemeManager 的资源应用、系统跟随、窗口外观和公共控件样式，以及即时预览与保存的区别。

- [配置向导：步骤、应用与完成边界](../../04-api-reference/ui-components/wizards.md) — `ui.wizards`
  配置向导的步骤发现、初始化时序、前进应用和完成标记；关闭不回滚，完成标记不证明组件健康或重启成功。

- [设置、流程与结果的导入导出边界](../../01-user-guide/data-management/export-import.md) — `operations.exports`
  按设置、流程、图像和项目结果定位导入导出实现，说明配置覆盖、文件验收与迁移边界。

- [ColorVision.UI.Desktop](../../04-api-reference/ui-components/ColorVision.UI.Desktop.md) — `ui.desktop`
  桌面辅助壳层而非产品主入口：定位设置、市场下载、第三方工具、反馈和特权崩溃诊断。

- [UI 包职责与依赖边界](../../04-api-reference/ui-components/component-handbook.md) — `ui.package-boundaries`
  UI类库的职责、依赖与目标框架/版本兼容；包版本可独立于主程序，中立算法与窗口适配分层维护。
