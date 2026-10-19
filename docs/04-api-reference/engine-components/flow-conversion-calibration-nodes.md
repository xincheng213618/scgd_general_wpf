---
knowledge_id: "flow.conversion-calibration"
knowledge_type: "reference"
status: "current"
summary: "定位 Flow 数据转换、图像转换、校准及属性选择器。"
aliases: ["找不到图像转换或校准模板","AlgDataConvertNode","LocalCalibrationRealPoiNode","CalibrationTemplatePropertiesEditor"]
code_paths: ["Engine/FlowEngineLib/Node/Algorithm/AlgDataConvertNode.cs","Engine/ColorVision.Engine/FlowProcessing/Nodes/Compatibility/Calibration/CalibrationNode.cs","Engine/ColorVision.Engine/FlowProcessing/Nodes/LocalCalibrationRealPoiNode.cs","Engine/ColorVision.Engine/PropertyEditor/FlowNodePropertyEditorRegistration.cs"]
test_paths: ["Test/ColorVision.UI.Tests/CameraNodeTemplateMappingTests.cs"]
related: ["flow.index","flow.templates","ui.property-grid"]
---

# Flow 转换与校准节点

数据转换节点位于 `FlowEngineLib`，使用模板编辑器的校准节点位于 Engine 的 `FlowProcessing/Nodes/Compatibility/Calibration/`；相关选择器和校准设备服务由 Engine 提供。

校正并实时测量上游 POI 使用本地 `LocalCalibrationRealPoiNode`（“本地设备 → 校正+实时 POI”），图像接 `IN_IMG`，布点结果接 `IN_POI`。包含旧服务端双输入校正节点的流程需要手动替换为此本地节点并重新选择校正资源、核对连线；两者配置语义不同，不自动映射旧节点。

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
| `CalibrationNode` | `Calibration` | Calibration 默认服务 | `CalibrationData` |
| `CalibrationROINode` | `SetROI` | Calibration 默认服务 | `CalibrationSetROIParam` |
| `AlgorithmCaliNode` | `CaliAngleShift` | Algorithm 默认服务 | `AlgorithmCaliParam` |

## 关键边界

| 链路 | 当前边界 |
| --- | --- |
| 数据转换 | `CVDataConvertMethodType` 当前很窄，不是任意文件格式互转 |
| 图像转换 | 当前目标格式主要是 `CSV`、`TIF`；默认通道为 `GREEN` |
| 单输入校准 | 只执行图像校正，使用输入图像携带的曝光信息；服务端请求固定 `IsSaveCIE=true` |
| 校准 ROI | 只设置 ROI，不保存校准结果文件 |
| 旧色差校正 | `AlgorithmCaliNode` 同时保留 `STNode` 和 `Obsolete`，由编辑器的 Obsolete 过滤隐藏但仍可反序列化旧画布 |

旧画布中单输入校准节点的 POI、曝光、文件输出模板及 `IsSaveCIE` 字段在加载时忽略，重存时不再写入；类型标识和端口连接保持兼容。请求不携带 `POIParam`、`POI_MasterId` 或 `ExpTemplateParam`，不会触发附带 POI 或曝光模板查找。输入图像须保留实际曝光信息；缺失时应在图像输入端补齐。本地校正节点不读取服务端 `IsSaveCIE`，CIE 生成和文件输出遵循本地执行策略。

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
| 单输入校准 | 请求包含 `CalibrationData`，固定 `IsSaveCIE=true`，不携带曝光或 POI 参数 |
| 校准 ROI | `SetROI` 后设备端 ROI 更新 |
| 旧色差校正 | 新建节点目录不显示；包含该类型的旧流程可正常反序列化 |

## 维护要求

- 新增转换类型时，同步枚举、算法服务解释、节点 UI、测试样例和本页矩阵。
- 新增服务端校准字段时，检查 `CalibrationData`、`CalibrationNode` 和属性编辑器声明。
- 修改 `PhyCamera` 关系时，回归校准模板选择器。
- 新需求使用 JSON V2 或强类型模板规范。

## 验证入口与缺口

关联测试：`Test/ColorVision.UI.Tests/CameraNodeTemplateMappingTests.cs`。

CameraNodeTemplateMappingTests 只覆盖相机相关模板编辑器映射；`Test/ColorVision.UI.Tests/CompatibilityNodeMigrationTests.cs` 覆盖旧校准节点字段忽略、重新保存、请求精简及画布连线兼容。转换与校准服务协议、结果文件仍需独立集成验证。
