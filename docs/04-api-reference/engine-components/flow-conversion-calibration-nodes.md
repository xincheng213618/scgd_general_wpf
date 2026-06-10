# Flow 转换与校准节点

这页补齐 Flow 里的数据转换、图像转换、校准和旧色差校正链路。接手时要先注意一个事实：当前源码里没有 `Engine/ColorVision.Engine/Templates/FileConvert/`、`ImageTransform/`、`Calibration/` 这三个强类型模板目录。相关能力主要分散在 `FlowEngineLib` 节点、`ColorVision.Engine/Templates/Flow/NodeConfigurator/` 配置器和 `Services/Devices/Calibration/` 设备服务里。

因此不要按“找一个同名模板目录”的方式接手，而要按 Flow 节点、`operatorCode`、设备服务和参数对象去追。

## 真实入口

| 能力 | 节点/对象 | 源码入口 | 交接用途 |
| --- | --- | --- | --- |
| 数据转换 | `AlgDataConvertNode` | `Engine/FlowEngineLib/Node/Algorithm/AlgDataConvertNode.cs` | 把上一步结果和转换模板发给 Algorithm 服务 |
| 数据转换参数 | `DataConvertData` | `Engine/FlowEngineLib/Node/Algorithm/DataConvertData.cs` | 承载 `MethodType`、`InType`、`OutType`、`TemplateParam` |
| 图像转换 | `AlgorithmImageConvertNode` | `Engine/FlowEngineLib/Node/Algorithm/AlgorithmImageConvertNode.cs` | 把图像、通道和目标格式发给 Algorithm 服务 |
| 图像转换参数 | `AlgorithmImageConvertParam` | `Engine/FlowEngineLib/Node/Algorithm/AlgorithmImageConvertParam.cs` | 承载 `ResultImageFormat`、`ResultDataFileName`、`Channel` |
| 单输入校准 | `CalibrationNode` | `Engine/FlowEngineLib/Algorithm/CalibrationNode.cs` | 使用校准设备执行曝光模板、图像和可选 POI 参数 |
| 双输入校准 | `Calibration2InNode` | `Engine/FlowEngineLib/Node/OLED/Calibration2InNode.cs` | 第二输入提供 POI 上游结果，把 `MasterId` 写入 `POI_MasterId` |
| 校准 ROI | `CalibrationROINode` | `Engine/FlowEngineLib/Node/Camera/CalibrationROINode.cs` | 向校准设备发送 `SetROI` 请求 |
| 旧色差校正 | `AlgorithmCaliNode` | `Engine/FlowEngineLib/Node/Algorithm/AlgorithmCaliNode.cs` | 兼容 `CaliAngleShift` JSON 模板和结果展示 |
| 校准模板绑定 | `CalibrationNodeConfigurator` | `Engine/ColorVision.Engine/Templates/Flow/NodeConfigurator/DeviceNodeConfigurators.cs` | 选择 `DeviceCalibration`，按 `PhyCamera` 补校准模板列表 |
| 相机侧校准绑定 | Camera node configurators | `Engine/ColorVision.Engine/Templates/Flow/NodeConfigurator/CameraNodeConfigurators.cs` | 相机节点按 `DeviceCamera.PhyCamera` 选择校准模板 |
| 色差校正模板绑定 | `AlgorithmCaliNodeConfigurator` | `Engine/ColorVision.Engine/Templates/Flow/NodeConfigurator/AlgorithmNodeConfigurators.cs` | 绑定 `TemplateCaliAngleShift`，这是旧 JSON 兼容链路 |

## 节点矩阵

| 节点 | 目录分类 | `operatorCode` | 服务/设备 | 参数对象 | 交接重点 |
| --- | --- | --- | --- | --- | --- |
| `AlgDataConvertNode` | Algorithm | `Math.DataConvert` | `SVR.Algorithm.Default` / `DEV.Algorithm.Default` | `DataConvertData` | 当前不是通用文件转换器，只覆盖现有枚举和上游结果转换。 |
| `AlgorithmImageConvertNode` | `/03_3 Image` | `Image.Convert` | `SVR.Algorithm.Default` / `DEV.Algorithm.Default` | `AlgorithmImageConvertParam` | 目标格式只有 `CSV`、`TIF`，默认通道是 `GREEN`。 |
| `CalibrationNode` | `/03_3 校正` | `Calibration` | `SVR.Calibration.Default` / `DEV.Calibration.Default` | `CalibrationData` | 单输入校准，参数来自上一步、曝光模板、图像和可选 POI 模板。 |
| `Calibration2InNode` | `/03_3 校正` | `Calibration` | `SVR.Calibration.Default` / `DEV.Calibration.Default` | `CalibrationData` | 双输入校准，`IN_POI` 的 `MasterId` 会成为 `POI_MasterId`。 |
| `CalibrationROINode` | `/11 ROI` | `SetROI` | `SVR.Calibration.Default` / `DEV.Calibration.Default` | `CalibrationSetROIParam` | 只设置 ROI，不执行完整校准。 |
| `AlgorithmCaliNode` | `/03_3 校正` | `CaliAngleShift` | `SVR.Algorithm.Default` / `DEV.Algorithm.Default` | `AlgorithmCaliParam` | 旧色差校正链路，参数模板来自 `TemplateCaliAngleShift`。 |

## 数据转换链路

`AlgDataConvertNode` 的业务边界很窄：

- 节点标题是“数据转换”，但当前 `CVDataConvertMethodType` 只有 `Camera_Motor_VID`。
- `CVDataConvertInputType` 当前只有 `None = -1`。
- `CVDataConvertOutputType` 当前只有 `None = -1`。
- 请求事件是 `Math.DataConvert`，执行服务仍是 Algorithm 默认服务。
- `getBaseEventData()` 会创建 `DataConvertData`，再通过 `getPreStepParam(start, dataConvertData)` 读取上一步结果，最后用 `BuildTemp()` 写入 `TemplateParam`。

交接时不要把它写成“任意文件格式互转”。如果要扩展真正的文件转换能力，需要同时扩展枚举、算法服务对 `Math.DataConvert` 的解释、模板选择逻辑和现场验收数据。

## 图像转换链路

`AlgorithmImageConvertNode` 的当前能力是把图像结果转换成指定输出格式：

| 属性 | 来源 | 说明 |
| --- | --- | --- |
| `ImageFormat` | 节点属性 | 当前枚举为 `CSV`、`TIF`。 |
| `ImgFileName` | 节点属性或配置器图像路径 | 参与 `BuildImageParam()`，通常来自上一步或手工选择。 |
| `Channel` | 节点属性 | 当前枚举为 `BLUE`、`GREEN`、`RED`、`ALL`，默认 `GREEN`。 |
| `ResultDataFileName` | `_OutputFileName` | 节点内部默认空字符串，当前没有独立 UI 属性暴露输出文件名。 |

执行时会创建 `AlgorithmImageConvertParam(_OutputFileName, _ImageFormat, (int)_Channel)`，再调用 `BuildImageParam(algorithmImageConvertParam)` 和 `getPreStepParam(start, algorithmImageConvertParam)`。这意味着转换请求既依赖当前节点图像参数，也依赖上一步结果参数。

## 校准链路

### 单输入校准

`CalibrationNode` 是完整校准链路的普通节点：

1. 节点使用 Calibration 服务，`operatorCode = "Calibration"`。
2. 运行前先读取上一步参数到 `AlgorithmPreStepParam`。
3. 创建 `CalibrationData(_ExpTempName, param, _IsSaveCIE)`。
4. `BuildImageParam(calibrationData)` 写入图像和模板信息。
5. 如果设置了 `POITempName`，会创建 `POITemplateParam(_POITempName, _POIFilterTempName, _POIReviseTempName)` 并写入 `calibrationData.POIParam`。

关键字段：

| 字段 | 含义 |
| --- | --- |
| `TempName` | 校准参数模板名，由配置器按物理相机提供模板列表。 |
| `ExpTempName` | 曝光模板名，写入 `ExpTemplateParam.Name`。 |
| `ImgFileName` | 图像文件。 |
| `IsSaveCIE` | 是否保存 CIE 文件，默认 `true`。 |
| `POITempName` / `POIFilterTempName` / `POIReviseTempName` | 可选 POI 参数链。 |
| `OutputTemplateName` | 当前节点有属性和 UI 显示，但 `getBaseEventData()` 没有写入 `CalibrationData`。 |

### 双输入校准

`Calibration2InNode` 也是 `operatorCode = "Calibration"`，但它继承 `CVBaseServerNodeHub`，输入口语义不同：

| 输入 | 节点标识 | 用途 |
| --- | --- | --- |
| 0 | `IN_IMG` | 图像或上游算法结果，写入基础 `CalibrationData`。 |
| 1 | `IN_POI` | 上游 POI 结果，`MasterId` 写入 `calibrationData.POI_MasterId`。 |

它不直接设置 `POIParam`，而是通过第二输入引用已有 POI 结果。排查双输入校准时，重点看 `IN_POI` 上游节点是否真的返回了有效 `MasterId`。

### 校准 ROI

`CalibrationROINode` 的 `operatorCode` 是 `SetROI`，请求对象是 `CalibrationSetROIParam`：

| 属性 | 说明 |
| --- | --- |
| `ROI_X` | ROI 左上角 X。 |
| `ROI_Y` | ROI 左上角 Y。 |
| `ROI_Width` | ROI 宽度。 |
| `ROI_Height` | ROI 高度。 |

这个节点只负责把 ROI 设置发给 Calibration 设备，不负责触发完整校准，也不负责保存结果文件。

## Engine 配置器如何补设备和模板

Flow 节点类本身只定义属性和参数对象，真正让用户在主程序里能选设备、图像路径和模板的是 `ColorVision.Engine/Templates/Flow/NodeConfigurator/`。

| 配置器 | 对应节点 | 补充内容 |
| --- | --- | --- |
| `CalibrationNodeConfigurator` | `CalibrationNode` | 添加 `DeviceCalibration` 选择器、图像路径选择器，并在当前校准设备有 `PhyCamera` 时添加 `TemplateCalibrationParam(result.PhyCamera)`。 |
| Camera node configurators | 多个相机节点 | 在 `CVAOICameraNode`、`AOILocatePixelsCameraNode`、`AOILocAndRegPixelsCameraNode`、`CVAOI2CameraNode`、`CommCameraNode`、`CVCameraNode`、`LVCameraNode` 等节点上，根据 `DeviceCamera.PhyCamera` 添加校准模板选择器。 |
| `AlgorithmCaliNodeConfigurator` | `AlgorithmCaliNode` | 添加 Algorithm 设备、图像路径和 `TemplateCaliAngleShift` JSON 模板。 |

如果 Flow 节点界面里看不到校准模板，第一步不是改文档，而是确认所选 `DeviceCalibration` 或 `DeviceCamera` 是否有 `PhyCamera`。没有物理相机对象时，配置器不会添加 `TemplateCalibrationParam` 模板面板。

## 与 POI 和旧 JSON 模板的关系

- 校准节点可以消费 POI 模板，也可以通过双输入节点消费上游 POI 结果。POI 侧详细逻辑见 [POI 模板](../algorithms/templates/poi-template.md)。
- `AlgorithmCaliNode` 使用的是旧 `CaliAngleShift` 链路，模板在 `Engine/ColorVision.Engine/Templates/Jsons/Deprecated/CaliAngleShift/`，结果类型是 `ViewResultAlgType.CaliAngleShift`。这类链路要按兼容维护，不建议作为新算法模板的首选范式。
- JSON 模板体系的通用规则见 [JSON 模板](../algorithms/templates/json-templates.md)。

## 现场验收建议

| 场景 | 验收方法 | 失败时先查 |
| --- | --- | --- |
| 数据转换 | Flow 中触发 `Math.DataConvert`，确认上一步参数、`MethodType` 和 `TemplateParam` 被传入算法服务。 | `AlgDataConvertNode` 枚举、上游 `MasterId`、Algorithm 服务日志。 |
| 图像转换 | 用已知图像结果执行 `Image.Convert`，分别测 `CSV`、`TIF` 和不同 `Channel`。 | `ImgFileName`、上游图像参数、输出文件名是否为空、算法服务返回。 |
| 单输入校准 | 选择 Calibration 设备、校准模板、曝光模板和图像，确认请求包含 `CalibrationData`、`ExpTemplateParam`、`IsSaveCIE`。 | `DeviceCalibration.PhyCamera`、模板面板是否出现、CIE 文件保存路径。 |
| 双输入校准 | `IN_IMG` 接图像结果，`IN_POI` 接 POI 结果，确认 `POI_MasterId` 不是 `-1`。 | 第二输入是否连接、POI 节点是否返回 `MasterId`、上游结果是否落库。 |
| 校准 ROI | 设置 X/Y/Width/Height 后触发 `SetROI`，确认设备端 ROI 更新。 | ROI 坐标合法性、Calibration 设备状态、服务日志。 |
| 旧色差校正 | 执行 `CaliAngleShift`，确认 `TemplateCaliAngleShift` 能加载并能展示 `CaliAngleShift` 结果。 | JSON 模板是否存在、旧 schema 是否兼容、`ViewHandleCaliAngleShift` 是否注册。 |

## 维护要求

- 新增转换类型时，必须同步更新枚举、算法服务解释、节点 UI、测试样例和本页矩阵。
- 新增校准参数字段时，必须检查 `CalibrationData`、`CalibrationNode`、`Calibration2InNode` 和 `CalibrationNodeConfigurator` 是否都需要同步。
- 修改相机或校准设备的 `PhyCamera` 关系时，必须回归模板选择器是否仍能显示正确的 `TemplateCalibrationParam`。
- 修改 `CaliAngleShift` 时，先确认是维护旧项目兼容还是新算法需求；新需求优先走当前 JSON V2 或强类型模板规范。

## 继续阅读

- [Engine 模板与 Flow 链路](./template-flow-chain.md)
- [设备服务链路](./device-service-chain.md)
- [Engine 结果展示与项目交接链路](./result-handoff-chain.md)
- [POI 模板](../algorithms/templates/poi-template.md)
- [JSON 模板](../algorithms/templates/json-templates.md)
- [校准服务使用说明](../../01-user-guide/devices/calibration.md)
