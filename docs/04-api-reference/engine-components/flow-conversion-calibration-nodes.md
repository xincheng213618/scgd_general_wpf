---
knowledge_id: "flow.conversion-calibration"
knowledge_type: "reference"
status: "current"
summary: "定位 Flow 数据转换、图像转换、单双输入校准及属性选择器。"
aliases: ["找不到图像转换或校准模板","AlgDataConvertNode","Calibration2InNode","FlowCalibrationTemplateEditor"]
code_paths: ["Engine/FlowEngineLib/Node/Algorithm/AlgDataConvertNode.cs","Engine/FlowEngineLib/Algorithm/CalibrationNode.cs","Engine/FlowEngineLib/Node/OLED/Calibration2InNode.cs","Engine/ColorVision.Engine/PropertyEditor/FlowNodePropertyEditorRegistration.cs"]
test_paths: ["Test/ColorVision.UI.Tests/CameraNodeTemplateMappingTests.cs"]
related: ["flow.index","flow.templates","ui.property-grid"]
---

# Flow 转换与校准节点

当前没有 `Templates/FileConvert/`、`ImageTransform/`、`Calibration/` 这三个强类型模板目录。相关能力分散在 `FlowEngineLib` 节点、Engine 属性编辑器注册和校准设备服务里；`Engine/ColorVision.Engine/FlowProcessing/Editor/NodeConfiguration/` 只保留节点类型级补充面板，当前没有 `CalibrationNodeConfigurator`。

## 先查什么

| 现象 | 第一检查点 |
| --- | --- |
| 找不到同名模板目录 | 这是正常现状，按 Flow 节点、`operatorCode`、参数对象追 |
| 数据转换不像文件转换器 | `AlgDataConvertNode` 只覆盖当前枚举和上游结果转换 |
| 图像转换输出不对 | `ImageFormat`、`Channel`、上游图像参数、输出文件名 |
| 校准模板面板不出现 | 选中的 `DeviceCalibration` / `DeviceCamera` 是否有 `PhyCamera` |
| 双输入校准无 POI | `IN_POI` 上游是否返回有效 `MasterId` |
| 旧色差校正节点找不到 | 节点已从目录隐藏；仅保留类型用于旧流程反序列化 |

## 真实入口

| 能力 | 节点/对象 | 入口 | 维护重点 |
| --- | --- | --- | --- |
| 数据转换 | `AlgDataConvertNode` | `FlowEngineLib/Node/Algorithm/AlgDataConvertNode.cs` | 发送 `Math.DataConvert` 到 Algorithm 服务 |
| 数据转换参数 | `DataConvertData` | `DataConvertData.cs` | `MethodType`、`InType`、`OutType`、`TemplateParam` |
| 图像转换 | `AlgorithmImageConvertNode` | `AlgorithmImageConvertNode.cs` | 发送 `Image.Convert` |
| 图像转换参数 | `AlgorithmImageConvertParam` | `AlgorithmImageConvertParam.cs` | `ResultImageFormat`、`ResultDataFileName`、`Channel` |
| 单输入校准 | `CalibrationNode` | `FlowEngineLib/Algorithm/CalibrationNode.cs` | 曝光模板、图像、可选 POI 参数 |
| 双输入校准 | `Calibration2InNode` | `Node/OLED/Calibration2InNode.cs` | 第二输入的 `MasterId` 写入 `POI_MasterId` |
| 校准 ROI | `CalibrationROINode` | `Node/Camera/CalibrationROINode.cs` | 发送 `SetROI`，不执行完整校准 |
| 旧色差校正 | `AlgorithmCaliNode` | `Node/Algorithm/AlgorithmCaliNode.cs` | 不提供新建入口，仅兼容旧流程解析 |

## 节点矩阵

| 节点 | `operatorCode` | 服务/设备 | 参数对象 |
| --- | --- | --- | --- |
| `AlgDataConvertNode` | `Math.DataConvert` | Algorithm 默认服务 | `DataConvertData` |
| `AlgorithmImageConvertNode` | `Image.Convert` | Algorithm 默认服务 | `AlgorithmImageConvertParam` |
| `CalibrationNode` | `Calibration` | Calibration 默认服务 | `CalibrationData` |
| `Calibration2InNode` | `Calibration` | Calibration 默认服务 | `CalibrationData` |
| `CalibrationROINode` | `SetROI` | Calibration 默认服务 | `CalibrationSetROIParam` |
| `AlgorithmCaliNode` | `CaliAngleShift` | Algorithm 默认服务 | `AlgorithmCaliParam` |

## 关键边界

| 链路 | 当前边界 |
| --- | --- |
| 数据转换 | `CVDataConvertMethodType` 当前很窄，不是任意文件格式互转 |
| 图像转换 | 当前目标格式主要是 `CSV`、`TIF`；默认通道为 `GREEN` |
| 单输入校准 | 可写曝光模板、图像、`IsSaveCIE` 和可选 POI 模板 |
| 双输入校准 | 不直接设置 `POIParam`，而是引用第二输入的 POI 结果 |
| 校准 ROI | 只设置 ROI，不保存校准结果文件 |
| 旧色差校正 | `AlgorithmCaliNode` 同时保留 `STNode` 和 `Obsolete`，由编辑器的 Obsolete 过滤隐藏但仍可反序列化旧画布 |

## Engine 属性编辑器

| 编辑器 | 对应节点 | 补充内容 |
| --- | --- | --- |
| `FlowCalibrationTemplateEditor` | `CalibrationNode`、`Calibration2InNode` 和相机节点 | 根据节点设备和 `PhyCamera` 提供 `TemplateCalibrationParam` 选择 |
| `FlowAutoExposureTemplateEditor` / `TextSelectFilePropertiesEditor` | 校准节点 | 选择曝光模板和图像路径 |

这些节点通过 `PropertyEditorTypeAttribute` 或 `FlowNodePropertyEditorAttribute` 声明编辑器，再由 `Engine/ColorVision.Engine/PropertyEditor/FlowNodePropertyEditorRegistration.cs` 注册具体 UI。

## 验收

| 场景 | 必验项 |
| --- | --- |
| 数据转换 | `Math.DataConvert` 收到上一步参数、`MethodType` 和 `TemplateParam` |
| 图像转换 | `Image.Convert` 对已知图像结果输出 `CSV` / `TIF`，通道正确 |
| 单输入校准 | 请求包含 `CalibrationData`、`ExpTemplateParam`、`IsSaveCIE` |
| 双输入校准 | `POI_MasterId` 不是 `-1` |
| 校准 ROI | `SetROI` 后设备端 ROI 更新 |
| 旧色差校正 | 新建节点目录不显示；包含该类型的旧流程可正常反序列化 |

## 维护要求

- 新增转换类型时，同步枚举、算法服务解释、节点 UI、测试样例和本页矩阵。
- 新增校准字段时，检查 `CalibrationData`、`CalibrationNode`、`Calibration2InNode` 和属性编辑器注册。
- 修改 `PhyCamera` 关系时，回归校准模板选择器。
- 新需求使用 JSON V2 或强类型模板规范。

## 验证入口与缺口

关联测试：`Test/ColorVision.UI.Tests/CameraNodeTemplateMappingTests.cs`。

CameraNodeTemplateMappingTests 只覆盖相机相关模板编辑器映射；转换与校准服务协议、POI MasterId 和结果文件仍需独立集成验证。
