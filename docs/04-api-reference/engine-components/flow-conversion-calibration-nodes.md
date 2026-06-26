# Flow 转换与校准节点

当前没有 `Templates/FileConvert/`、`ImageTransform/`、`Calibration/` 这三个强类型模板目录。相关能力分散在 `FlowEngineLib` 节点、Engine Flow `NodeConfigurator/` 和校准设备服务里。

## 先查什么

| 现象 | 第一检查点 |
| --- | --- |
| 找不到同名模板目录 | 这是正常现状，按 Flow 节点、`operatorCode`、参数对象追 |
| 数据转换不像文件转换器 | `AlgDataConvertNode` 只覆盖当前枚举和上游结果转换 |
| 图像转换输出不对 | `ImageFormat`、`Channel`、上游图像参数、输出文件名 |
| 校准模板面板不出现 | 选中的 `DeviceCalibration` / `DeviceCamera` 是否有 `PhyCamera` |
| 双输入校准无 POI | `IN_POI` 上游是否返回有效 `MasterId` |
| 旧色差校正异常 | `TemplateCaliAngleShift`、Deprecated JSON 模板和 handler |

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
| 旧色差校正 | `AlgorithmCaliNode` | `Node/Algorithm/AlgorithmCaliNode.cs` | 兼容 `CaliAngleShift` JSON 模板 |

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
| 旧色差校正 | `CaliAngleShift` 在 Deprecated JSON 目录，按兼容链维护 |

## Engine 配置器

| 配置器 | 对应节点 | 补充内容 |
| --- | --- | --- |
| `CalibrationNodeConfigurator` | `CalibrationNode` | 设备选择、图像路径、按 `PhyCamera` 添加 `TemplateCalibrationParam` |
| Camera node configurators | 多个相机节点 | 根据 `DeviceCamera.PhyCamera` 添加校准模板选择器 |
| `AlgorithmCaliNodeConfigurator` | `AlgorithmCaliNode` | Algorithm 设备、图像路径、`TemplateCaliAngleShift` |

Flow 节点类本身只定义属性和参数对象；真正让用户选设备、图像路径和模板的是 `NodeConfigurator/`。

## 验收

| 场景 | 必验项 |
| --- | --- |
| 数据转换 | `Math.DataConvert` 收到上一步参数、`MethodType` 和 `TemplateParam` |
| 图像转换 | `Image.Convert` 对已知图像结果输出 `CSV` / `TIF`，通道正确 |
| 单输入校准 | 请求包含 `CalibrationData`、`ExpTemplateParam`、`IsSaveCIE` |
| 双输入校准 | `POI_MasterId` 不是 `-1` |
| 校准 ROI | `SetROI` 后设备端 ROI 更新 |
| 旧色差校正 | `TemplateCaliAngleShift` 可加载，`CaliAngleShift` 结果可展示 |

## 维护要求

- 新增转换类型时，同步枚举、算法服务解释、节点 UI、测试样例和本页矩阵。
- 新增校准字段时，检查 `CalibrationData`、`CalibrationNode`、`Calibration2InNode`、`CalibrationNodeConfigurator`。
- 修改 `PhyCamera` 关系时，回归校准模板选择器。
- 新需求优先走当前 JSON V2 或强类型模板规范，不优先扩展 Deprecated 链。
