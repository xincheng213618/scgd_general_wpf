---
generated_knowledge_index: true
search: false
editLink: false
prev: false
next: false
---

# 算法与模板

> 自动生成的领域目录。修改主题 Markdown 元数据后运行 `node docs/.vitepress/scripts/knowledge.mjs generate`；不要手工编辑。

算法平台、传统模板、计算适配和规划中的能力。 返回[知识总入口](../index.md)。

只读与当前问题相关的主题，再核对源码和测试。`规划`、`历史`不代表当前能力。

- [算法与模板知识入口](../../04-api-reference/algorithms/README.md) — `algorithms.index`
  区分统一 Runner、ImageEditor 直接 native 分析与 Engine 模板/MQTT 算法，并按任务定位专题。

- [ROI 模型与模板入口](../../04-api-reference/algorithms/primitives/roi.md) — `algorithms.roi-routes`
  按用途定位发光区、传统与 JSON 裁剪、SFR 寻边和中立算法 ROI 模型；各分支参数与坐标契约分别维护。

- [Engine 模板共享构件](../../04-api-reference/algorithms/primitives/common-modules.md) — `algorithms.template-primitives`
  路由 Engine 模板中的 ROI、POI、Matching 共享构件并区分统一算法平台。

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

- [本地灯珠与 P2 分析](../../04-api-reference/algorithms/local-native-analysis.md) — `algorithms.local-native-analysis`
  ImageEditor 本地灯珠、Ghost、旋转模板和双目标定融合的操作、参数与结果；灯珠暗区候选不完整，P2 运行失败后复制结果可能仍取上次 JSON。

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

- [ARVR 算法与模板](../../04-api-reference/algorithms/templates/arvr-template.md) — `algorithms.arvr`
  ARVR 手动算法与流程节点的模板、POI 和请求对应关系；说明结果版本匹配及 SFR 曲线、查询和两种 CSV 导出的数据范围。

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

- [图像比较：差分、SSIM 与对齐预检](../../02-developer-guide/core-concepts/image-comparison-v1.md) — `algorithms.image-comparison`
  图像比较的操作、参数范围、ROI、差分/SSIM/对齐结果和PNG/JSON/CSV导出；预检不校正图像，192MiB仅限制图像输出，采样数存在狭长区域上限缺口。

- [灰度与颜色剖面：采样、曲线与数据导出](../../02-developer-guide/core-concepts/image-profile-v1.md) — `algorithms.image-profile`
  灰度与颜色剖面的操作、采样/越界规则、2000行预览和完整JSON/CSV导出；多点入口受多边形选择器限制，MaximumSamples还受执行/字节预算限制，旧接口参数不同。

- [图像配准 V1（M8.1）](../../02-developer-guide/core-concepts/image-registration-v1.md) — `algorithms.image-registration`
  ImageRegistration 的输入、参数、结果、宿主接入与定向验证契约。

- [成像校正 V1（M9）](../../02-developer-guide/core-concepts/imaging-correction-v1.md) — `algorithms.imaging-correction`
  ImagingCorrection 的输入、参数、结果、宿主接入与定向验证契约。

- [JSON 模板](../../04-api-reference/algorithms/templates/json-templates.md) — `algorithms.json-templates`
  JSON模板的文本/属性编辑、数据库保存、默认参数与重置；校验Json按钮只同步模型，Schema提供字段提示而不补默认值或执行完整校验。

- [LED 检测模板](../../04-api-reference/algorithms/templates/led-detection.md) — `algorithms.led`
  区分灯条、灯珠强类型与 JSON V2 模板、事件、POI 输入和结果限制。

- [镜头畸变校正 V1（M8.2）](../../02-developer-guide/core-concepts/lens-distortion-correction-v1.md) — `algorithms.lens-distortion-correction`
  LensDistortionCorrection 的输入、参数、结果、宿主接入与定向验证契约。

- [直线拟合 V1（M6.2）](../../02-developer-guide/core-concepts/line-fit-v1.md) — `algorithms.line-fit`
  LineFit 保留实现的参数、结果与验证契约；默认运行时由 Experimental 门禁拒绝执行。

- [摩尔纹分析 V1（M11）](../../02-developer-guide/core-concepts/moire-analysis-v1.md) — `algorithms.moire-analysis`
  MoireAnalysis 保留实现的参数、结果与验证契约；默认运行时由 Experimental 门禁拒绝执行。

- [POI 模板](../../04-api-reference/algorithms/templates/poi-template.md) — `algorithms.poi-template`
  说明 POI 主从表、伴生模板、复制导入、运行事件与结果类型映射。

- [ROI 统计 V1（M1）](../../02-developer-guide/core-concepts/roi-statistics-v1.md) — `algorithms.roi-statistics`
  RoiStatistics 的输入、参数、结果、宿主接入与定向验证契约。

- [亚像素边缘 V1（M6.1）](../../02-developer-guide/core-concepts/subpixel-edge-v1.md) — `algorithms.subpixel-edge`
  SubpixelEdge 保留实现的参数、结果与验证契约；默认运行时由 Experimental 门禁拒绝执行。

- [SysDictionary 系统字典兼容层](../../04-api-reference/algorithms/templates/sys-dictionary-template.md) — `algorithms.template-dictionary`
  说明保留的系统字典 DAO 与模板默认值、传感器和旧流程兼容依赖。

- [ONNX / AI 推理接入设计（Deferred） \[规划\]](../../02-developer-guide/core-concepts/onnx-inference-future-design.md) — `algorithms.onnx`
  尚未实现的 ONNX 接入设计：保持基础产品无 ONNX 运行时，新增 adapter 前须明确模型与验收门禁。
