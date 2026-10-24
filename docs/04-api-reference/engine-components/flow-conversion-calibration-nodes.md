---
knowledge_id: "flow.conversion-calibration"
knowledge_type: "reference"
status: "current"
summary: "定位 Flow 数据转换、图像转换、校准及属性选择器。"
aliases: ["找不到图像转换或校准模板","AlgDataConvertNode","LocalCalibrationRealPoiNode","CalibrationTemplatePropertiesEditor"]
code_paths: ["Engine/FlowEngineLib/Node/Algorithm/AlgDataConvertNode.cs","Engine/ColorVision.Engine/FlowProcessing/Nodes/Compatibility/Calibration/CalibrationNode.cs","Engine/ColorVision.Engine/FlowProcessing/Nodes/Compatibility/POI/RealPOINode.cs","Engine/ColorVision.Engine/FlowProcessing/Nodes/LocalCalibrationNode.cs","Engine/ColorVision.Engine/FlowProcessing/Nodes/LocalRealPoiNode.cs","Engine/ColorVision.Engine/FlowProcessing/Nodes/LocalCalibrationRealPoiNode.cs","Engine/ColorVision.Engine/Services/Devices/Calibration","Engine/ColorVision.Engine/PropertyEditor/FlowNodePropertyEditorRegistration.cs"]
test_paths: ["Test/ColorVision.UI.Tests/CompatibilityNodeMigrationTests.cs","Test/ColorVision.UI.Tests/CompatibilityLocalCalibrationFlowTests.cs"]
related: ["flow.index","flow.templates","ui.property-grid"]
---

# Flow 转换与校准节点

数据转换节点位于 `FlowEngineLib`，使用模板编辑器的校准节点位于 Engine 的 `FlowProcessing/Nodes/Compatibility/Calibration/`；相关选择器和校准设备服务由 Engine 提供。

单输入“校正”节点（`CalibrationNode`）在新建节点菜单的“相机”分类下；节点类型标识、参数和旧流程加载保持兼容。

`CalibrationNode` 和原“实时关注点算法”（`RealPOINode`）复用服务节点的内部执行交接链路。校正服务显示配置中的“使用本地校正”开启时，旧节点分别把参数和输入转交给现有的 `LocalCalibrationNode` / `LocalRealPoiNode`，图像加载、计算、入库及通知均复用本地节点实现；关闭后两者使用原 MQTT 请求。切换只影响后续请求，已经开始的调用保持所选后端；内部失败不会再次发送服务请求。本地分支等待调用完成或流程停止，不使用服务节点的最大超时。

单输入“关注点算法”（`POINode`）沿图像输入分支查找最近的相机，跟随相机实际后端：本地相机会话或关闭后选择本地打开时，将所选 POI 模板和图像输入交给现有 `LocalPoiNode`；相机仍由服务持有时继续使用原算法服务。显式本地相机节点也使用内部测量；无法确定唯一上游相机时保留原服务路径。本地分支直接消费当前内存帧，或读取显式图像文件，不要求生成 `.cvcie`；POI 主从表、结果类型与下游交接沿用本地节点。过滤、修正、亚像素和文件输出设置保留但不参与本地测量，内部失败不补发服务请求。此路由不依赖“使用本地校正”开关。

实时关注点从 `IN_CIE` 沿图像分支查找最近的校正服务；直接相机输入通过物理相机关联校正服务。没有对应上游设备时使用默认校正服务，或唯一的校正服务；无法唯一确定时使用原服务。多个校正服务的开关分别生效。原节点类型、输入端口及参数保存格式保持不变，校正结果类型仍为 `30`。本地实时关注点按实际明细使用三通道 `POI_XYZ=1` / 单通道 `POI_Y=2`，入库主表、流程输出、通知及下游关注点分析均使用同一类型；不能为兼容旧节点而覆盖成服务端 `RealPOI=18`，否则下游可能将已有点误判为空。原服务分支继续沿用服务返回的结果类型。

校正并实时测量上游 POI 使用本地 `LocalCalibrationRealPoiNode`（“本地设备 → 校正+实时 POI”），图像接 `IN_IMG`，布点结果接 `IN_POI`。包含旧服务端双输入校正节点的流程需要手动替换为此本地节点并重新选择校正资源、核对连线；两者配置语义不同，不自动映射旧节点。

该节点直接按 `IN_POI` 提供的点位、形状和尺寸测量，输入点位须对应当前图像坐标。旧流程保存的 `UseROI` 字段在加载时忽略，重新保存时不再写入；之前依赖全幅坐标转换的流程，需在上游布点时提供当前图像坐标。物理相机 ROI 采集及校正文件区域匹配仍由相机和本地校正链路负责。

## 先查什么

| 现象 | 第一检查点 |
| --- | --- |
| 数据转换不像文件转换器 | `AlgDataConvertNode` 只覆盖当前枚举和上游结果转换 |
| 图像转换输出不对 | `ImageFormat`、`Channel`、上游图像参数、输出文件名 |
| 校准模板面板不出现 | 选中的 `DeviceCalibration` / `DeviceCamera` 是否有 `PhyCamera` |
| 旧色差校正节点找不到 | 节点已从目录隐藏；仅保留类型用于旧流程反序列化 |

## 真实入口

| 能力 | 节点/对象 | 入口 | 维护重点 |
| --- | --- | --- | --- |
| 数据转换 | `AlgDataConvertNode` | `FlowEngineLib/Node/Algorithm/AlgDataConvertNode.cs` | 发送 `Math.DataConvert` 到 Algorithm 服务 |
| 数据转换参数 | `DataConvertData` | `DataConvertData.cs` | `MethodType`、`InType`、`OutType`、`TemplateParam` |
| 图像转换 | `AlgorithmImageConvertNode` | `AlgorithmImageConvertNode.cs` | 发送 `Image.Convert` |
| 图像转换参数 | `AlgorithmImageConvertParam` | `AlgorithmImageConvertParam.cs` | `ResultImageFormat`、`ResultDataFileName`、`Channel` |
| 单输入校准 | `CalibrationNode` | `ColorVision.Engine/FlowProcessing/Nodes/Compatibility/Calibration/CalibrationNode.cs` | 输入图像和校正模板 |
| 校准 ROI | `CalibrationROINode` | `Node/Camera/CalibrationROINode.cs` | 发送 `SetROI`，不执行完整校准 |
| 旧色差校正 | `AlgorithmCaliNode` | `Node/Algorithm/AlgorithmCaliNode.cs` | 不提供新建入口，仅兼容旧流程解析 |

## 节点矩阵

| 节点 | `operatorCode` | 服务/设备 | 参数对象 |
| --- | --- | --- | --- |
| `AlgDataConvertNode` | `Math.DataConvert` | Algorithm 默认服务 | `DataConvertData` |
| `AlgorithmImageConvertNode` | `Image.Convert` | Algorithm 默认服务 | `AlgorithmImageConvertParam` |
| `CalibrationNode` | `Calibration` | 所选 Calibration 服务的本地偏好决定内部调用或 MQTT | `CalibrationData` |
| `CalibrationROINode` | `SetROI` | Calibration 默认服务 | `CalibrationSetROIParam` |
| `AlgorithmCaliNode` | `CaliAngleShift` | Algorithm 默认服务 | `AlgorithmCaliParam` |

## 关键边界

| 链路 | 当前边界 |
| --- | --- |
| 数据转换 | `CVDataConvertMethodType` 当前很窄，不是任意文件格式互转 |
| 图像转换 | 当前目标格式主要是 `CSV`、`TIF`；默认通道为 `GREEN` |
| 单输入校准 | 只执行图像校正，使用输入图像携带的曝光信息；服务端请求固定 `IsSaveCIE=true`；内部调用复用本地校正实现，保留完整 CIE 内存及本地结果交接策略 |
| 校准 ROI | 只设置 ROI，不保存校准结果文件 |
| 旧色差校正 | `AlgorithmCaliNode` 同时保留 `STNode` 和 `Obsolete`，由编辑器的 Obsolete 过滤隐藏但仍可反序列化旧画布 |

旧画布中单输入校准节点的 POI、曝光、文件输出模板及 `IsSaveCIE` 字段在加载时忽略，重存时不再写入；类型标识和端口连接保持兼容。请求不携带 `POIParam`、`POI_MasterId` 或 `ExpTemplateParam`，不会触发附带 POI 或曝光模板查找。输入图像须保留实际曝光信息；缺失时应在图像输入端补齐。本地校正节点不读取服务端 `IsSaveCIE`，CIE 生成和文件输出遵循本地执行策略。

原实时关注点的内部调用优先使用 `IN_CIE` 分支的内存帧，也能从图像结果或显式图像文件读取 CIE / 带色度参数的 RAW。`POIType=None` 沿用上游点形状和尺寸，普通点、圆和矩形按节点配置覆盖。过滤、修正、二次修正文件、亚像素、文件输出、POI 新增及色温/波长开关仅保留配置，内部测量忽略这些高级设置并计算可用色度指标；亚像素和键盘专用类型沿用上游点定义。关闭“使用本地校正”后，原服务请求继续携带全部原参数。需要这些高级能力时应关闭该开关。

历史循环校准节点仍可通过 `CalibrationData.ExpTemplateParam` 提供曝光参数，不受单输入校准节点的配置精简影响。

## Engine 属性编辑器

| 编辑器 | 对应节点 | 补充内容 |
| --- | --- | --- |
| `CalibrationTemplatePropertiesEditor` | `CalibrationNode` 和相机节点 | 根据节点设备和 `PhyCamera` 提供 `TemplateCalibrationParam` 选择 |
| `TextSelectFilePropertiesEditor` | 校准节点 | 选择图像路径 |

这些节点在属性上直接使用 `PropertyEditorTypeAttribute` 声明 `FlowTemplatePropertiesEditors.cs` 中的具体编辑器；模板控件复用 `FlowNodePropertyEditorRegistration.cs` 的构建方法，无需模板代理注册。

## 验收

| 场景 | 必验项 |
| --- | --- |
| 数据转换 | `Math.DataConvert` 收到上一步参数、`MethodType` 和 `TemplateParam` |
| 图像转换 | `Image.Convert` 对已知图像结果输出 `CSV` / `TIF`，通道正确 |
| 单输入校准 | 开关两侧均保留参数和结果类型；内部调用交接当前帧，服务请求固定 `IsSaveCIE=true`，不携带曝光或 POI 参数 |
| 实时关注点 | 随图像分支的校正开关切换；双输入任意到达顺序均使用 `IN_CIE` 的帧；内部忽略高级设置，服务请求保留原字段 |
| 校准 ROI | `SetROI` 后设备端 ROI 更新 |
| 旧色差校正 | 新建节点目录不显示；包含该类型的旧流程可正常反序列化 |

## 维护要求

- 新增转换类型时，同步枚举、算法服务解释、节点 UI、测试样例和本页矩阵。
- 新增服务端校准字段时，检查 `CalibrationData`、`CalibrationNode` 和属性编辑器声明。
- 修改 `PhyCamera` 关系时，回归校准模板选择器。
- 新需求使用 JSON V2 或强类型模板规范。

## 验证入口与缺口

`CompatibilityNodeMigrationTests.cs` 覆盖旧校准节点字段忽略、重新保存、请求精简及画布连线兼容。`CompatibilityLocalCalibrationFlowTests.cs` 覆盖后端开关联动、单/三通道测量经内部交接传给关注点分析的结果类型、输入内存帧选择、停止后的交接拒绝及内部失败不发送 MQTT。真实设备、数据库入库和完整服务协议仍需独立集成验证。
