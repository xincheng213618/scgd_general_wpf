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

- [UI DLL 发布](../../04-api-reference/ui-components/publishing.md) — `ui.publishing`
  说明 UI NuGet 构建、版本占用预检、显式 Release 发布与包消费验证。

- [UI DLL 速查](../../04-api-reference/ui-components/component-handbook.md) — `ui.package-boundaries`
  按职责和依赖方向判断 UI DLL 的修改归属与消费方兼容风险。

## UI/ColorVision.Algorithms {#module-55492f436f6c6f72566973696f6e2e416c676f726974686d73}

- [算法与模板知识入口](../../04-api-reference/algorithms/README.md) — `algorithms.index`
  区分统一 Runner、ImageEditor 直接 native 分析与 Engine 模板/MQTT 算法，并按任务定位专题。

- [ROI](../../04-api-reference/algorithms/primitives/roi.md) — `algorithms.roi-routes`
  区分发光区定位、JSON 裁剪、SFR 找 ROI 与统一算法 ROI 数据模型。

- [统一图像算法平台 V1](../../02-developer-guide/core-concepts/image-algorithm-platform-v1.md) — `algorithms.platform`
  统一图像算法Catalog、Invocation和Runner；普通像素预览、应用/取消、所有权与发布门禁；ONNX仅设计。

- [Engine 结果展示链路](../../04-api-reference/engine-components/result-handoff-chain.md) — `engine.results`
  区分 Engine 历史结果 handler、项目业务结果和统一算法 overlay 的注册及生命周期。

- [系统职责与跨模块边界](../../03-architecture/overview/system-overview.md) — `platform.system`
  宿主、UI、Engine、插件与项目的职责及调用边界：UI操作不必经过Engine，程序集依赖不是统一执行顺序，构建产物不等于交付制品。

- [UI DLL 发布](../../04-api-reference/ui-components/publishing.md) — `ui.publishing`
  说明 UI NuGet 构建、版本占用预检、显式 Release 发布与包消费验证。

- [ONNX / AI 推理接入设计（Deferred） \[规划\]](../../02-developer-guide/core-concepts/onnx-inference-future-design.md) — `algorithms.onnx`
  尚未实现的 ONNX 接入设计：保持基础产品无 ONNX 运行时，新增 adapter 前须明确模型与验收门禁。

## UI/ColorVision.Common {#module-55492f436f6c6f72566973696f6e2e436f6d6d6f6e}

- [UI 组件目录](../../04-api-reference/ui-components/control-catalog.md) — `ui.control-catalog`
  按控件、窗口和扩展接口定位对应 UI 源码与专题。

- [Copilot 扩展、MCP 与 Hook](../../02-developer-guide/core-concepts/copilot-agent-extensions.md) — `copilot.extensions`
  业务模块动态上下文、外部 MCP client 和 Hook 如何进入统一宿主权限与生命周期。

- [模板注册、参数与持久化](../../03-architecture/components/templates/design.md) — `engine.template-design`
  TemplateControl注册与普通ITemplate\<T\>参数加载、保存、复制和删除契约；注册、内存变更和数据库成功是不同状态，JSON与Flow另有实现。

- [扩展性开发](../../02-developer-guide/core-concepts/extensibility.md) — `platform.extensibility`
  菜单、插件、属性编辑器、算法模板和 Copilot 扩展的职责与源码入口。

- [安全与权限控制](../../03-architecture/security/overview.md) — `platform.security`
  区分全局粗粒度权限和独立RBAC模块，不承诺不存在的统一业务授权边界。

- [插件装载、依赖门禁与扩展发现](../../02-developer-guide/plugin-development/overview.md) — `plugins.model`
  PluginLoader的manifest/依赖门禁、禁用缓存、程序集发现和失败边界；载入不等于provider可用，也不支持隔离卸载。

- [共享接口、属性通知与粗粒度权限](../../04-api-reference/ui-components/ColorVision.Common.md) — `ui.common`
  共享接口的宿主接入、属性通知与命令的同步执行限制、粗粒度权限判据，以及第三方工具发现和启动边界。

- [配置持久化、重载与对象所有权](../../04-api-reference/ui-components/configuration.md) — `ui.configuration`
  ConfigHandler的配置路径、延迟实例、文件合并保存和重载契约；单文件替换不等于内存发布成功，重载会使旧配置引用失效。

- [数据库 Provider、表浏览与写入契约](../../04-api-reference/ui-components/ColorVision.Database.md) — `ui.database`
  数据库 Provider、表浏览和 MySQL/DAO 契约；区分读取、行级写入、内存撤销与事务，保存可能部分成功。

- [多图查看、刷新与缩略图缓存](../../04-api-reference/ui-components/ColorVision.ImageTools.md) — `ui.image-tools`
  ImageTools内置注册、多图列表中的单张预览、刷新与SQLite缩略图缓存；重选不保证重载，关窗不清缓存，缓存关闭也不等于零数据库访问。

- [菜单：发现、显示、执行与管理提交](../../04-api-reference/ui-components/menus.md) — `ui.menus`
  菜单的插件 DLL 发现、类型缓存、父子树和管理提交；IHotKey 提示随运行时键位更新，隐藏不禁用快捷键，应用成功提示不保证配置落盘，菜单入口不构成统一鉴权。

- [主窗口搜索：候选、刷新与执行](../../04-api-reference/ui-components/search.md) — `ui.search`
  主窗口搜索框的关键词匹配、候选来源、刷新缓存、结果顺序和执行；关闭搜索设置不回滚也不直接保存，回车不统一检查命令权限。

- [设置窗口：发现、编辑与关闭契约](../../04-api-reference/ui-components/settings.md) — `ui.settings`
  设置窗口的发现缓存、侧栏搜索、活对象编辑和自定义页面生命周期；普通选项关窗不撤销，启动检查更新的勾选只表示至少一个更新开关开启。

- [状态栏：发现、刷新与宿主生命周期](../../04-api-reference/ui-components/status-bar.md) — `ui.status-bar`
  状态栏的插件发现、活动文档通知、绑定更新、控件重建和关闭生命周期；刷新不保证发现新provider，隐藏不等于保存偏好或停止采样。

- [配置向导：步骤、应用与完成边界](../../04-api-reference/ui-components/wizards.md) — `ui.wizards`
  配置向导的步骤发现、初始化时序、前进应用和完成标记；关闭不回滚，完成标记不证明组件健康或重启成功。

## UI/ColorVision.Core {#module-55492f436f6c6f72566973696f6e2e436f7265}

- [FindLightArea 发光区定位模板](../../04-api-reference/algorithms/templates/find-light-area.md) — `algorithms.find-light-area`
  区分远端 FindLightArea 模板与本地原生亮区检测 RobustV2；四角点不等于成功，须核对置信度、失败原因和各调用层的结果契约。

- [ImageEditor 直接 native 分析](../../04-api-reference/algorithms/local-native-analysis.md) — `algorithms.local-native-analysis`
  ImageEditor直接native灯珠与P2分析：Ghost/旋转模板/双目标定、缺失计数与完成边界；区别Engine/MQTT模板和统一Runner。

- [日志来源、历史读取与筛选](../../01-user-guide/interface/log-viewer.md) — `operations.logs`
  区分log4net输出、历史文件读取与UI筛选，说明刷新、截断和原生日志采集边界；没有显示不等于动作未发生。

- [景深融合：输入、执行与结果生命周期](../../04-api-reference/ui-components/image-fusion.md) — `ui.image-fusion`
  景深融合的CPU/CUDA调用、HImage显示和计时；自动模式不做失败回退，关窗不取消计算，GPU少量图片存在未修复的越界风险。

- [系统要求](../../00-getting-started/prerequisites.md) — `delivery.prerequisites`
  首次构建所需Windows x64、.NET与C++工具链，区分已有native DLL与干净克隆。

- [OpenCV 和 native 集成开发指南](../../02-developer-guide/engine-development/opencv-integration.md) — `engine.native-integration`
  native ABI与HImage所有权、函数族返回值、视频异步/关闭边界，以及helper构建和CUDA发布输入；路由校准Context与POI原生参考。

- [ColorVision.Core](../../04-api-reference/ui-components/ColorVision.Core.md) — `ui.core`
  定位 HImage 所有权、OpenCV/CUDA PInvoke、ImageCompute 融合分流、位图桥接与默认关闭的原生日志。

## UI/ColorVision.Database {#module-55492f436f6c6f72566973696f6e2e4461746162617365}

- [数据所有者与存储定位](../../01-user-guide/data-management/README.md) — `operations.data`
  按设置JSON、Engine MySQL、模块SQLite和结果文件定位数据所有者；有记录、有图片、已导出和已备份不是同一状态。

- [MySQL 结果清理、备份与失败边界](../../04-api-reference/engine-components/mysql-maintenance.md) — `engine.mysql-maintenance`
  MySQL 批次与结果表的历史删除、整表截断和SQL备份；统计不是清理预览，无全程事务或自动恢复，主从选择和管理员权限不能只依赖界面提示。

- [数据库 Provider、表浏览与写入契约](../../04-api-reference/ui-components/ColorVision.Database.md) — `ui.database`
  数据库 Provider、表浏览和 MySQL/DAO 契约；区分读取、行级写入、内存撤销与事务，保存可能部分成功。

- [通用查询、条件会话与整表操作](../../04-api-reference/ui-components/database-query.md) — `ui.database-query`
  实体驱动的通用查询窗口：条件参数化、执行时SQL预览、结果替换与进程内会话；关闭不取消查询，清空表/截断表作用于整表而非筛选结果。

- [SQLite 正文存储、迁移与文件维护](../../04-api-reference/ui-components/sqlite-storage.md) — `ui.sqlite-storage`
  Socket 与 Flow 的 SQLite 正文 gzip 编解码、按ID读写、旧TEXT逐批迁移、WAL备份与VACUUM；通用工具不自动停写/备份/恢复，失败可能已有批次提交。

## UI/ColorVision.ImageEditor {#module-55492f436f6c6f72566973696f6e2e496d616765456469746f72}

- [UI 组件目录](../../04-api-reference/ui-components/control-catalog.md) — `ui.control-catalog`
  按控件、窗口和扩展接口定位对应 UI 源码与专题。

- [UI 知识入口](../../04-api-reference/ui-components/README.md) — `ui.index`
  按问题路由到 UI 模块、属性编辑契约、运行时发现与 DLL 发布证据。

- [ImageEditor 直接 native 分析](../../04-api-reference/algorithms/local-native-analysis.md) — `algorithms.local-native-analysis`
  ImageEditor直接native灯珠与P2分析：Ghost/旋转模板/双目标定、缺失计数与完成边界；区别Engine/MQTT模板和统一Runner。

- [统一图像算法平台 V1](../../02-developer-guide/core-concepts/image-algorithm-platform-v1.md) — `algorithms.platform`
  统一图像算法Catalog、Invocation和Runner；普通像素预览、应用/取消、所有权与发布门禁；ONNX仅设计。

- [Copilot 扩展、MCP 与 Hook](../../02-developer-guide/core-concepts/copilot-agent-extensions.md) — `copilot.extensions`
  业务模块动态上下文、外部 MCP client 和 Hook 如何进入统一宿主权限与生命周期。

- [Engine 结果展示链路](../../04-api-reference/engine-components/result-handoff-chain.md) — `engine.results`
  区分 Engine 历史结果 handler、项目业务结果和统一算法 overlay 的注册及生命周期。

- [系统职责与跨模块边界](../../03-architecture/overview/system-overview.md) — `platform.system`
  宿主、UI、Engine、插件与项目的职责及调用边界：UI操作不必经过Engine，程序集依赖不是统一执行顺序，构建产物不等于交付制品。

- [UI 运行时组件](../../04-api-reference/ui-components/ui-runtime-handoff.md) — `ui.discovery`
  排查程序集加载后菜单、设置、PropertyGrid、工具和服务扩展的发现链。

- [ColorVision.ImageEditor：打开、绘制与输出](../../04-api-reference/ui-components/ColorVision.ImageEditor.md) — `ui.image-editor`
  图像/视频打开、绘图撤销、叠加层、3D 与快照输出边界，区分渲染图、当前源像素和重读源文件的模型导出。

- [ImageEditor：上下文、工具装配与临时选区](../../04-api-reference/ui-components/image-editor-context.md) — `ui.image-editor-context`
  ImageEditor 的状态归属、扩展构造、工具刷新与临时 ROI 有效期；区分配置分类、图像版本和真实像素坐标。

- [状态栏：发现、刷新与宿主生命周期](../../04-api-reference/ui-components/status-bar.md) — `ui.status-bar`
  状态栏的插件发现、活动文档通知、绑定更新、控件重建和关闭生命周期；刷新不保证发现新provider，隐藏不等于保存偏好或停止采样。

- [设置、流程与结果的导入导出边界](../../01-user-guide/data-management/export-import.md) — `operations.exports`
  按设置、流程、图像和项目结果定位导入导出实现，说明配置覆盖、文件验收与迁移边界。

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

- [图像比较基础 V1（M3）](../../02-developer-guide/core-concepts/image-comparison-v1.md) — `algorithms.image-comparison`
  ImageComparison 的输入、参数、结果、宿主接入与定向验证契约。

- [图像比较高级 V1（M4）](../../02-developer-guide/core-concepts/image-comparison-advanced-v1.md) — `algorithms.image-comparison-advanced`
  ImageComparison 的输入、参数、结果、宿主接入与定向验证契约。

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

- [UI DLL 速查](../../04-api-reference/ui-components/component-handbook.md) — `ui.package-boundaries`
  按职责和依赖方向判断 UI DLL 的修改归属与消费方兼容风险。

- [ONNX / AI 推理接入设计（Deferred） \[规划\]](../../02-developer-guide/core-concepts/onnx-inference-future-design.md) — `algorithms.onnx`
  尚未实现的 ONNX 接入设计：保持基础产品无 ONNX 运行时，新增 adapter 前须明确模型与验收门禁。

- [本地相机内存帧预览：生命周期与显示语义 \[规划\]](../../02-developer-guide/engine-development/local-camera-memory-preview-runtime.md) — `engine.camera-preview-lifecycle-plan`
  记录待实施预览的租约取得、latest-wins、RAW/CIE 模式和内存预算约束。

## UI/ColorVision.ImageTools {#module-55492f436f6c6f72566973696f6e2e496d616765546f6f6c73}

- [景深融合：输入、执行与结果生命周期](../../04-api-reference/ui-components/image-fusion.md) — `ui.image-fusion`
  景深融合的CPU/CUDA调用、HImage显示和计时；自动模式不做失败回退，关窗不取消计算，GPU少量图片存在未修复的越界风险。

- [多图查看、刷新与缩略图缓存](../../04-api-reference/ui-components/ColorVision.ImageTools.md) — `ui.image-tools`
  ImageTools内置注册、多图列表中的单张预览、刷新与SQLite缩略图缓存；重选不保证重载，关窗不清缓存，缓存关闭也不等于零数据库访问。

- [存储清理与选择性设置重置](../../04-api-reference/ui-components/storage-maintenance.md) — `ui.storage-maintenance`
  设置中的存储清理与选择性启动重置：先确认白名单扫描清单，保护活跃任务和业务数据；删除不回滚，重置先独立备份再在启动时应用。

## UI/ColorVision.Rbac {#module-55492f436f6c6f72566973696f6e2e52626163}

- [RBAC：登录缓存、会话与权限边界](../../03-architecture/security/rbac.md) — `platform.rbac`
  本地RBAC的登录缓存、会话校验和权限同步限制，以及自动登录失败、登出撤销和用户中心统计的实际边界。

- [安全与权限控制](../../03-architecture/security/overview.md) — `platform.security`
  区分全局粗粒度权限和独立RBAC模块，不承诺不存在的统一业务授权边界。

- [共享接口、属性通知与粗粒度权限](../../04-api-reference/ui-components/ColorVision.Common.md) — `ui.common`
  共享接口的宿主接入、属性通知与命令的同步执行限制、粗粒度权限判据，以及第三方工具发现和启动边界。

## UI/ColorVision.Scheduler {#module-55492f436f6c6f72566973696f6e2e5363686564756c6572}

- [Quartz 任务定义、恢复与执行历史](../../04-api-reference/ui-components/ColorVision.Scheduler.md) — `ui.scheduler`
  Quartz 调度定义的启动恢复、JSON/SQLite 分工与执行统计；暂停不终止在途任务，重启恢复不是执行进度续跑。

- [状态栏：发现、刷新与宿主生命周期](../../04-api-reference/ui-components/status-bar.md) — `ui.status-bar`
  状态栏的插件发现、活动文档通知、绑定更新、控件重建和关闭生命周期；刷新不保证发现新provider，隐藏不等于保存偏好或停止采样。

## UI/ColorVision.SocketProtocol {#module-55492f436f6c6f72566973696f6e2e536f636b657450726f746f636f6c}

- [数据所有者与存储定位](../../01-user-guide/data-management/README.md) — `operations.data`
  按设置JSON、Engine MySQL、模块SQLite和结果文件定位数据所有者；有记录、有图片、已导出和已备份不是同一状态。

- [数据库清理窗口、能力接入与完成边界](../../04-api-reference/engine-components/database-maintenance.md) — `engine.database-maintenance`
  数据库清理窗口与provider能力：表统计不是删除预览，确认只固定部分参数；备份默认关闭、组合维护不是事务，关窗不取消，成功与统计刷新分开。

- [跨模块运行问题定位](../../01-user-guide/README.md) — `operations.index`
  从启动、配置、日志、设备、流程和结果现象定位代码责任，区分已完成阶段与待验证阶段，避免用重启或改数据代替诊断。

- [通用查询、条件会话与整表操作](../../04-api-reference/ui-components/database-query.md) — `ui.database-query`
  实体驱动的通用查询窗口：条件参数化、执行时SQL预览、结果替换与进程内会话；关闭不取消查询，清空表/截断表作用于整表而非筛选结果。

- [TCP 监听、协议分发与消息记录](../../04-api-reference/ui-components/ColorVision.SocketProtocol.md) — `ui.socket-protocol`
  TCP网络通信的监听快照、窗口关闭与服务停止、JSON/Text分发及消息记录；Sent不证明对端执行，重发可能换客户端并追加记录。

- [SQLite 正文存储、迁移与文件维护](../../04-api-reference/ui-components/sqlite-storage.md) — `ui.sqlite-storage`
  Socket 与 Flow 的 SQLite 正文 gzip 编解码、按ID读写、旧TEXT逐批迁移、WAL备份与VACUUM；通用工具不自动停写/备份/恢复，失败可能已有批次提交。

## UI/ColorVision.Solution {#module-55492f436f6c6f72566973696f6e2e536f6c7574696f6e}

- [UI 组件目录](../../04-api-reference/ui-components/control-catalog.md) — `ui.control-catalog`
  按控件、窗口和扩展接口定位对应 UI 源码与专题。

- [主窗口与入口装配](../../01-user-guide/interface/main-window.md) — `operations.main-window`
  主窗口如何挂接菜单、搜索、状态栏和工作区，以及入口缺失时应核对的代码边界。

- [终端进程、会话与脚本运行](../../01-user-guide/interface/terminal.md) — `operations.terminal`
  定义内嵌ConPTY会话、编辑器Python运行与外部CMD入口，区分命令提交、脚本结束、shell退出和强制释放。

- [编辑器选择、文档生命周期与停靠布局](../../04-api-reference/ui-components/editor-document-lifecycle.md) — `ui.documents`
  编辑器注册与选择、按路径和编辑器区分文档、保存重载关闭及外部变更；停靠布局不恢复未注册文件标签，重置也不预审脏文档。

- [资源打开与单工作区切换](../../04-api-reference/ui-components/ColorVision.Solution.md) — `ui.solution`
  工作区与普通文件的打开分流、单工作区切换和取消、私有cvsln与共享配置恢复；打开和加载不保证无写入。

- [状态栏：发现、刷新与宿主生命周期](../../04-api-reference/ui-components/status-bar.md) — `ui.status-bar`
  状态栏的插件发现、活动文档通知、绑定更新、控件重建和关闭生命周期；刷新不保证发现新provider，隐藏不等于保存偏好或停止采样。

- [主程序启动与最小图像验证](../../00-getting-started/first-steps.md) — `operations.first-run`
  主程序启动的配置、实例和服务副作用，以及隔离测试环境中的最小本地图像验证。

- [UI DLL 速查](../../04-api-reference/ui-components/component-handbook.md) — `ui.package-boundaries`
  按职责和依赖方向判断 UI DLL 的修改归属与消费方兼容风险。

## UI/ColorVision.Themes {#module-55492f436f6c6f72566973696f6e2e5468656d6573}

- [主题选择、资源应用与窗口外观](../../04-api-reference/ui-components/ColorVision.Themes.md) — `ui.themes`
  ThemeManager的主题选择、资源追加、系统跟随和窗口外观契约；选择不等于应用成功，预览不等于配置落盘。

## UI/ColorVision.UI {#module-55492f436f6c6f72566973696f6e2e5549}

- [数据所有者与存储定位](../../01-user-guide/data-management/README.md) — `operations.data`
  按设置JSON、Engine MySQL、模块SQLite和结果文件定位数据所有者；有记录、有图片、已导出和已备份不是同一状态。

- [扩展任务入口](../../04-api-reference/extensions/README.md) — `platform.extensions`
  按 Flow 节点、属性编辑器、模板、设备和插件问题定位可复用扩展契约。

- [插件装配与模块知识入口](../../04-api-reference/plugins/README.md) — `plugins.index`
  从插件程序集装载、产物安装和具体模块能力定位源码；同一责任不再分开发手册与使用手册。

- [UI 组件目录](../../04-api-reference/ui-components/control-catalog.md) — `ui.control-catalog`
  按控件、窗口和扩展接口定位对应 UI 源码与专题。

- [ColorVision.UI 壳层责任与知识入口](../../04-api-reference/ui-components/ColorVision.UI.md) — `ui.framework`
  ColorVision.UI壳层责任入口：按配置、插件、菜单、热键、搜索、语言、状态栏、属性编辑和日志定位规范主题，业务行为仍归所属模块。

- [UI 知识入口](../../04-api-reference/ui-components/README.md) — `ui.index`
  按问题路由到 UI 模块、属性编辑契约、运行时发现与 DLL 发布证据。

- [模板编辑入口与菜单契约](../../04-api-reference/algorithms/templates/template-menu-entries.md) — `algorithms.template-menus`
  区分现存模板主菜单、专用入口与通用算法配置中的模板编辑命令。

- [Copilot 设置、持久化与连接诊断](../../02-developer-guide/core-concepts/copilot-configuration.md) — `copilot.configuration`
  ColorVision内置Copilot的设置草稿、配置保存与运行态发布、模型选择和联网诊断；保存失败可能已落盘，Local MCP测试核验会话握手与只读状态调用。

- [自动更新](../../02-developer-guide/deployment/auto-update.md) — `delivery.update`
  主程序及插件更新、检查结果一次性消费、失败元数据回退、目录替换与启动恢复的实现和验收边界。

- [数据库清理窗口、能力接入与完成边界](../../04-api-reference/engine-components/database-maintenance.md) — `engine.database-maintenance`
  数据库清理窗口与provider能力：表统计不是删除预览，确认只固定部分参数；备份默认关闭、组合维护不是事务，关窗不取消，成功与统计刷新分开。

- [Explorer 缩略图读取与 COM 注册](../../04-api-reference/engine-components/ColorVision.ShellExtension.md) — `engine.shell-extension`
  Explorer 的 CVRAW/CVCIE COM provider 如何读取像素、生成非测量用途缩略图，以及源码脚本与 ServiceHost 注册的不同副作用和失败边界。

- [模板注册、参数与持久化](../../03-architecture/components/templates/design.md) — `engine.template-design`
  TemplateControl注册与普通ITemplate\<T\>参数加载、保存、复制和删除契约；注册、内存变更和数据库成功是不同状态，JSON与Flow另有实现。

- [跨模块运行问题定位](../../01-user-guide/README.md) — `operations.index`
  从启动、配置、日志、设备、流程和结果现象定位代码责任，区分已完成阶段与待验证阶段，避免用重启或改数据代替诊断。

- [日志来源、历史读取与筛选](../../01-user-guide/interface/log-viewer.md) — `operations.logs`
  区分log4net输出、历史文件读取与UI筛选，说明刷新、截断和原生日志采集边界；没有显示不等于动作未发生。

- [主窗口与入口装配](../../01-user-guide/interface/main-window.md) — `operations.main-window`
  主窗口如何挂接菜单、搜索、状态栏和工作区，以及入口缺失时应核对的代码边界。

- [终端进程、会话与脚本运行](../../01-user-guide/interface/terminal.md) — `operations.terminal`
  定义内嵌ConPTY会话、编辑器Python运行与外部CMD入口，区分命令提交、脚本结束、shell退出和强制释放。

- [扩展性开发](../../02-developer-guide/core-concepts/extensibility.md) — `platform.extensibility`
  菜单、插件、属性编辑器、算法模板和 Copilot 扩展的职责与源码入口。

- [架构运行时](../../03-architecture/overview/runtime.md) — `platform.runtime`
  启动分支、配置初始化、插件装载和恢复流程的运行时顺序。

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

- [UI 运行时组件](../../04-api-reference/ui-components/ui-runtime-handoff.md) — `ui.discovery`
  排查程序集加载后菜单、设置、PropertyGrid、工具和服务扩展的发现链。

- [编辑器选择、文档生命周期与停靠布局](../../04-api-reference/ui-components/editor-document-lifecycle.md) — `ui.documents`
  编辑器注册与选择、按路径和编辑器区分文档、保存重载关闭及外部变更；停靠布局不恢复未注册文件标签，重置也不预审脏文档。

- [快捷键：发现、注册、编辑与释放](../../04-api-reference/ui-components/hotkeys.md) — `ui.hotkeys`
  快捷键的发现、展示、窗口/全局注册与搜索编辑；单个操作只保留一组键位，确认后立即应用并保存，注册或持久化失败按结果执行补偿。

- [ImageEditor：上下文、工具装配与临时选区](../../04-api-reference/ui-components/image-editor-context.md) — `ui.image-editor-context`
  ImageEditor 的状态归属、扩展构造、工具刷新与临时 ROI 有效期；区分配置分类、图像版本和真实像素坐标。

- [界面语言：资源发现、配置与重启](../../04-api-reference/ui-components/localization.md) — `ui.localization`
  界面语言的资源发现、系统语言回退、设置绑定和重启切换；语言下拉框不证明插件翻译完整，修改配置值不等于刷新窗口。

- [菜单：发现、显示、执行与管理提交](../../04-api-reference/ui-components/menus.md) — `ui.menus`
  菜单的插件 DLL 发现、类型缓存、父子树和管理提交；IHotKey 提示随运行时键位更新，隐藏不禁用快捷键，应用成功提示不保证配置落盘，菜单入口不构成统一鉴权。

- [PropertyGrid 属性编辑契约](../../04-api-reference/ui-components/property-grid.md) — `ui.property-grid`
  属性面板的字段生成、编辑器选择和 Flow 适配；区分直接修改、工作副本、关闭、重置与宿主持久化。

- [Quartz 任务定义、恢复与执行历史](../../04-api-reference/ui-components/ColorVision.Scheduler.md) — `ui.scheduler`
  Quartz 调度定义的启动恢复、JSON/SQLite 分工与执行统计；暂停不终止在途任务，重启恢复不是执行进度续跑。

- [主窗口搜索：候选、刷新与执行](../../04-api-reference/ui-components/search.md) — `ui.search`
  主窗口搜索框的关键词匹配、候选来源、刷新缓存、结果顺序和执行；关闭搜索设置不回滚也不直接保存，回车不统一检查命令权限。

- [设置窗口：发现、编辑与关闭契约](../../04-api-reference/ui-components/settings.md) — `ui.settings`
  设置窗口的发现缓存、侧栏搜索、活对象编辑和自定义页面生命周期；普通选项关窗不撤销，启动检查更新的勾选只表示至少一个更新开关开启。

- [资源打开与单工作区切换](../../04-api-reference/ui-components/ColorVision.Solution.md) — `ui.solution`
  工作区与普通文件的打开分流、单工作区切换和取消、私有cvsln与共享配置恢复；打开和加载不保证无写入。

- [状态栏：发现、刷新与宿主生命周期](../../04-api-reference/ui-components/status-bar.md) — `ui.status-bar`
  状态栏的插件发现、活动文档通知、绑定更新、控件重建和关闭生命周期；刷新不保证发现新provider，隐藏不等于保存偏好或停止采样。

- [存储清理与选择性设置重置](../../04-api-reference/ui-components/storage-maintenance.md) — `ui.storage-maintenance`
  设置中的存储清理与选择性启动重置：先确认白名单扫描清单，保护活跃任务和业务数据；删除不回滚，重置先独立备份再在启动时应用。

- [主题选择、资源应用与窗口外观](../../04-api-reference/ui-components/ColorVision.Themes.md) — `ui.themes`
  ThemeManager的主题选择、资源追加、系统跟随和窗口外观契约；选择不等于应用成功，预览不等于配置落盘。

- [配置向导：步骤、应用与完成边界](../../04-api-reference/ui-components/wizards.md) — `ui.wizards`
  配置向导的步骤发现、初始化时序、前进应用和完成标记；关闭不回滚，完成标记不证明组件健康或重启成功。

- [设置、流程与结果的导入导出边界](../../01-user-guide/data-management/export-import.md) — `operations.exports`
  按设置、流程、图像和项目结果定位导入导出实现，说明配置覆盖、文件验收与迁移边界。

- [主程序启动与最小图像验证](../../00-getting-started/first-steps.md) — `operations.first-run`
  主程序启动的配置、实例和服务副作用，以及隔离测试环境中的最小本地图像验证。

- [UI DLL 速查](../../04-api-reference/ui-components/component-handbook.md) — `ui.package-boundaries`
  按职责和依赖方向判断 UI DLL 的修改归属与消费方兼容风险。

## UI/ColorVision.UI.Desktop {#module-55492f436f6c6f72566973696f6e2e55492e4465736b746f70}

- [Android 运维伴侣](../../02-developer-guide/backend/android-operations.md) — `delivery.android-operations`
  Android原生运维入口、现场HTTPS与固定签名中继的职责边界；连接、可见证据和操作授权不能互相替代。

- [Backend Operations 中继与只读概览](../../02-developer-guide/backend/operations-relay.md) — `delivery.backend-operations`
  Backend Operations 的 Bearer 与设备签名中继、任务回执和管理员只读投影；在线、排队、验签与真实动作完成各有边界。

- [自动更新](../../02-developer-guide/deployment/auto-update.md) — `delivery.update`
  主程序及插件更新、检查结果一次性消费、失败元数据回退、目录替换与启动恢复的实现和验收边界。

- [插件产物、安装与交付](../../02-developer-guide/plugin-development/getting-started.md) — `plugins.getting-started`
  插件构建产物、HostCopy、manifest包身份、安装替换和恢复契约；发布会上传，安装器返回不等于替换或重启后加载成功。

- [插件装载、依赖门禁与扩展发现](../../02-developer-guide/plugin-development/overview.md) — `plugins.model`
  PluginLoader的manifest/依赖门禁、禁用缓存、程序集发现和失败边界；载入不等于provider可用，也不支持隔离卸载。

- [快捷键：发现、注册、编辑与释放](../../04-api-reference/ui-components/hotkeys.md) — `ui.hotkeys`
  快捷键的发现、展示、窗口/全局注册与搜索编辑；单个操作只保留一组键位，确认后立即应用并保存，注册或持久化失败按结果执行补偿。

- [界面语言：资源发现、配置与重启](../../04-api-reference/ui-components/localization.md) — `ui.localization`
  界面语言的资源发现、系统语言回退、设置绑定和重启切换；语言下拉框不证明插件翻译完整，修改配置值不等于刷新窗口。

- [菜单：发现、显示、执行与管理提交](../../04-api-reference/ui-components/menus.md) — `ui.menus`
  菜单的插件 DLL 发现、类型缓存、父子树和管理提交；IHotKey 提示随运行时键位更新，隐藏不禁用快捷键，应用成功提示不保证配置落盘，菜单入口不构成统一鉴权。

- [主窗口搜索：候选、刷新与执行](../../04-api-reference/ui-components/search.md) — `ui.search`
  主窗口搜索框的关键词匹配、候选来源、刷新缓存、结果顺序和执行；关闭搜索设置不回滚也不直接保存，回车不统一检查命令权限。

- [设置窗口：发现、编辑与关闭契约](../../04-api-reference/ui-components/settings.md) — `ui.settings`
  设置窗口的发现缓存、侧栏搜索、活对象编辑和自定义页面生命周期；普通选项关窗不撤销，启动检查更新的勾选只表示至少一个更新开关开启。

- [存储清理与选择性设置重置](../../04-api-reference/ui-components/storage-maintenance.md) — `ui.storage-maintenance`
  设置中的存储清理与选择性启动重置：先确认白名单扫描清单，保护活跃任务和业务数据；删除不回滚，重置先独立备份再在启动时应用。

- [配置向导：步骤、应用与完成边界](../../04-api-reference/ui-components/wizards.md) — `ui.wizards`
  配置向导的步骤发现、初始化时序、前进应用和完成标记；关闭不回滚，完成标记不证明组件健康或重启成功。

- [设置、流程与结果的导入导出边界](../../01-user-guide/data-management/export-import.md) — `operations.exports`
  按设置、流程、图像和项目结果定位导入导出实现，说明配置覆盖、文件验收与迁移边界。

- [ColorVision.UI.Desktop](../../04-api-reference/ui-components/ColorVision.UI.Desktop.md) — `ui.desktop`
  桌面辅助壳层而非产品主入口：定位设置、市场下载、第三方工具、反馈和特权崩溃诊断。
