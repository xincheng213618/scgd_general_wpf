# Flow Engine 结点参考文档

> 自动生成于 2026-05-22，基于以下源码目录：
> - 结点配置: `Engine\ColorVision.Engine\Templates\Flow\NodeConfigurator\`
> - 结点实现: `Engine\FlowEngineLib\`

## 概览

共 **42** 个已配置结点，按类型分组如下：

| 类型 | 数量 |
|------|------|
| Algorithm | 17 |
| Camera | 8 |
| OLED | 2 |
| POI | 5 |
| PG | 1 |
| SMU | 3 |
| Sensor | 2 |
| FW | 1 |
| Spectrum | 3 |

## Algorithm 类结点

### 1. AlgorithmARVRNode

- **完整类型**: `FlowEngineLib.Algorithm.AlgorithmARVRNode`
- **配置器**: `AlgorithmNodeConfigurators.cs`
- **实现文件**: `Algorithm\AlgorithmARVRNode.cs`
- **基类**: `CVBaseServerNode`

**配置面板属性** (NodeConfigurator)

| 属性名 | 类型 | 说明 |
|--------|------|------|
| `DeviceCode` | DeviceCode | 设备编码 |
| `ImgFileName` | ImagePath | 图片文件路径 |
| `TempName` | TemplateRef | MTF |
| `POITempName` | TemplateRef | POI |

**类级别属性** (Node Implementation)

| 属性名 | C# 类型 |
|--------|---------|
| `Algorithm` | `AlgorithmARVRType` |
| `TempName` | `string` |
| `POITempName` | `string` |
| `ImgFileName` | `string` |
| `Color` | `CVOLED_COLOR` |
| `BufferLen` | `int` |

---

### 2. AlgorithmNode

- **完整类型**: `FlowEngineLib.Algorithm.AlgorithmNode`
- **配置器**: `AlgorithmNodeConfigurators.cs`
- **实现文件**: `Algorithm\AlgorithmNode.cs`
- **基类**: `CVBaseServerNode`

**配置面板属性** (NodeConfigurator)

| 属性名 | 类型 | 说明 |
|--------|------|------|
| `DeviceCode` | DeviceCode | 设备编码 |
| `ImgFileName` | ImagePath | 图片文件路径 |
| `POITempName` | TemplateRef | POI |
| `TempName` | TemplateRef | MTF |

**类级别属性** (Node Implementation)

| 属性名 | C# 类型 |
|--------|---------|
| `Algorithm` | `AlgorithmType` |
| `TempName` | `string` |
| `POITempName` | `string` |
| `ImgFileName` | `string` |
| `Color` | `CVOLED_COLOR` |
| `BufferLen` | `int` |

---

### 3. CalibrationNode

- **完整类型**: `FlowEngineLib.Algorithm.CalibrationNode`
- **配置器**: `DeviceNodeConfigurators.cs`
- **实现文件**: `Algorithm\CalibrationNode.cs`
- **基类**: `CVBaseServerNode`

**配置面板属性** (NodeConfigurator)

| 属性名 | 类型 | 说明 |
|--------|------|------|
| `DeviceCode` | DeviceCode | 设备编码 |
| `ImgFileName` | ImagePath | 图片文件路径 |
| `TempName` | TemplateRef | 校正 |

**类级别属性** (Node Implementation)

| 属性名 | C# 类型 |
|--------|---------|
| `TempName` | `string` |
| `ExpTempName` | `string` |
| `ImgFileName` | `string` |
| `IsSaveCIE` | `bool` |
| `POITempName` | `string` |
| `POIFilterTempName` | `string` |
| `POIReviseTempName` | `string` |
| `OutputTemplateName` | `string` |

---

### 4. AlgComplianceMathNode

- **完整类型**: `FlowEngineLib.Node.Algorithm.AlgComplianceMathNode`
- **配置器**: `AlgorithmNodeConfigurators.cs`
- **实现文件**: `Node\Algorithm\AlgComplianceMathNode.cs`
- **基类**: `CVBaseServerNode`

**配置面板属性** (NodeConfigurator)

| 属性名 | 类型 | 说明 |
|--------|------|------|
| `DeviceCode` | DeviceCode | 设备编码 |
| `TempName` | TemplateRef | JND |

**类级别属性** (Node Implementation)

| 属性名 | C# 类型 |
|--------|---------|
| `TempName` | `string` |
| `ComplianceMath` | `ComplianceMathType` |
| `IsBreak` | `bool` |

---

### 5. AlgDataLoadNode

- **完整类型**: `FlowEngineLib.Node.Algorithm.AlgDataLoadNode`
- **配置器**: `AlgorithmNodeConfigurators.cs`
- **实现文件**: `Node\Algorithm\AlgDataLoadNode.cs`
- **基类**: `CVBaseServerNode`

**配置面板属性** (NodeConfigurator)

| 属性名 | 类型 | 说明 |
|--------|------|------|
| `DeviceCode` | DeviceCode | 设备编码 |
| `TempName` | TemplateRef | 模板 |

**类级别属性** (Node Implementation)

| 属性名 | C# 类型 |
|--------|---------|
| `TempName` | `string` |

---

### 6. AlgorithmBlackMuraNode

- **完整类型**: `FlowEngineLib.Node.Algorithm.AlgorithmBlackMuraNode`
- **配置器**: `AlgorithmNodeConfigurators.cs`
- **实现文件**: `Node\Algorithm\AlgorithmBlackMuraNode.cs`
- **基类**: `CVBaseServerNode`

**配置面板属性** (NodeConfigurator)

| 属性名 | 类型 | 说明 |
|--------|------|------|
| `DeviceCode` | DeviceCode | 设备编码 |
| `ImgFileName` | ImagePath | 图片文件路径 |
| `TempName` | TemplateJsonRef | BlackMura |

**类级别属性** (Node Implementation)

| 属性名 | C# 类型 |
|--------|---------|
| `TempName` | `string` |
| `ImgFileName` | `string` |
| `OIndex` | `string` |
| `SavePOITempName` | `string` |

---

### 7. AlgorithmCaliNode

- **完整类型**: `FlowEngineLib.Node.Algorithm.AlgorithmCaliNode`
- **配置器**: `AlgorithmNodeConfigurators.cs`
- **实现文件**: `Node\Algorithm\AlgorithmCaliNode.cs`
- **基类**: `CVBaseServerNode`

**配置面板属性** (NodeConfigurator)

| 属性名 | 类型 | 说明 |
|--------|------|------|
| `DeviceCode` | DeviceCode | 设备编码 |
| `ImgFileName` | ImagePath | 图片文件路径 |
| `TempName` | TemplateJsonRef | 色差 |

**类级别属性** (Node Implementation)

| 属性名 | C# 类型 |
|--------|---------|
| `TempName` | `string` |
| `ImgFileName` | `string` |
| `OutputFileName` | `string` |

---

### 8. AlgorithmFindLEDNode

- **完整类型**: `FlowEngineLib.Node.Algorithm.AlgorithmFindLEDNode`
- **配置器**: `AlgorithmNodeConfigurators.cs`
- **实现文件**: `Node\Algorithm\AlgorithmFindLEDNode.cs`
- **基类**: `CVBaseServerNode`

**配置面板属性** (NodeConfigurator)

| 属性名 | 类型 | 说明 |
|--------|------|------|
| `DeviceCode` | DeviceCode | 设备编码 |
| `ImgFileName` | ImagePath | 图片文件路径 |
| `TempName` | TemplateRef | 像素级灯珠检测 |

**类级别属性** (Node Implementation)

| 属性名 | C# 类型 |
|--------|---------|
| `Color` | `CVOLED_Channel` |
| `TempName` | `string` |
| `FDAType` | `CVOLED_FDAType` |
| `FixedLEDPoint` | `PointFloat[]` |
| `ImgFileName` | `string` |
| `OutputFileName` | `string` |
| `ImgPosResultFile` | `string` |

---

### 9. AlgorithmFindLightAreaNode

- **完整类型**: `FlowEngineLib.Node.Algorithm.AlgorithmFindLightAreaNode`
- **配置器**: `AlgorithmNodeConfigurators.cs`
- **实现文件**: `Node\Algorithm\AlgorithmFindLightAreaNode.cs`
- **基类**: `CVBaseServerNode`

**配置面板属性** (NodeConfigurator)

| 属性名 | 类型 | 说明 |
|--------|------|------|
| `DeviceCode` | DeviceCode | 设备编码 |
| `ImgFileName` | ImagePath | 图片文件路径 |
| `TempName` | TemplateRef | 发光区定位 |
| `SavePOITempName` | TemplateRef | 保存POI |

**类级别属性** (Node Implementation)

| 属性名 | C# 类型 |
|--------|---------|
| `TempName` | `string` |
| `ImgFileName` | `string` |
| `SavePOITempName` | `string` |
| `BufferLen` | `int` |
| `OIndex` | `string` |

---

### 10. AlgorithmGhostV2Node

- **完整类型**: `FlowEngineLib.Node.Algorithm.AlgorithmGhostV2Node`
- **配置器**: `AlgorithmNodeConfigurators.cs`
- **实现文件**: `Node\Algorithm\AlgorithmGhostV2Node.cs`
- **基类**: `CVBaseServerNodeHub`

**配置面板属性** (NodeConfigurator)

| 属性名 | 类型 | 说明 |
|--------|------|------|
| `DeviceCode` | DeviceCode | 设备编码 |
| `ImgFileName` | ImagePath | 图片文件路径 |
| `TempName` | TemplateRef | Ghost |

**类级别属性** (Node Implementation)

| 属性名 | C# 类型 |
|--------|---------|
| `TempName` | `string` |
| `ImgFileName` | `string` |
| `BufferLen` | `int` |

---

### 11. AlgorithmImageROINode

- **完整类型**: `FlowEngineLib.Node.Algorithm.AlgorithmImageROINode`
- **配置器**: `AlgorithmNodeConfigurators.cs`
- **实现文件**: `Node\Algorithm\AlgorithmImageROINode.cs`
- **基类**: `CVBaseServerNode`

**配置面板属性** (NodeConfigurator)

| 属性名 | 类型 | 说明 |
|--------|------|------|
| `DeviceCode` | DeviceCode | 设备编码 |
| `ImgFileName` | ImagePath | 图片文件路径 |
| `TempName` | TemplateJsonRef | 模板名称 |

**类级别属性** (Node Implementation)

| 属性名 | C# 类型 |
|--------|---------|
| `TempName` | `string` |
| `ImgFileName` | `string` |
| `OutputFileName` | `string` |

---

### 12. AlgorithmKBNode

- **完整类型**: `FlowEngineLib.Node.Algorithm.AlgorithmKBNode`
- **配置器**: `AlgorithmNodeConfigurators.cs`
- **实现文件**: `Node\Algorithm\AlgorithmKBNode.cs`
- **基类**: `CVBaseServerNode`

**配置面板属性** (NodeConfigurator)

| 属性名 | 类型 | 说明 |
|--------|------|------|
| `DeviceCode` | DeviceCode | 设备编码 |
| `ImgFileName` | ImagePath | 图片文件路径 |
| `TempName` | TemplateKBRef | KB |

**类级别属性** (Node Implementation)

| 属性名 | C# 类型 |
|--------|---------|
| `TempName` | `string` |
| `ImgFileName` | `string` |

---

### 13. AlgorithmKBOutputNode

- **完整类型**: `FlowEngineLib.Node.Algorithm.AlgorithmKBOutputNode`
- **配置器**: `AlgorithmNodeConfigurators.cs`
- **实现文件**: `Node\Algorithm\AlgorithmKBOutputNode.cs`
- **基类**: `CVBaseServerNode`

**配置面板属性** (NodeConfigurator)

| 属性名 | 类型 | 说明 |
|--------|------|------|
| `DeviceCode` | DeviceCode | 设备编码 |
| `TempName` | TemplateKBRef | KB |

**类级别属性** (Node Implementation)

| 属性名 | C# 类型 |
|--------|---------|
| `TempName` | `string` |

---

### 14. AlgorithmOLEDNode

- **完整类型**: `FlowEngineLib.Node.Algorithm.AlgorithmOLEDNode`
- **配置器**: `OLEDNodeConfigurators.cs`
- **实现文件**: `Node\Algorithm\AlgorithmOLEDNode.cs`
- **基类**: `CVBaseServerNode`

**配置面板属性** (NodeConfigurator)

| 属性名 | 类型 | 说明 |
|--------|------|------|
| `DeviceCode` | DeviceCode | 设备编码 |
| `ImgFileName` | ImagePath | 图片文件路径 |
| `TempName` | TemplateJsonRef | 亚像素 |

**类级别属性** (Node Implementation)

| 属性名 | C# 类型 |
|--------|---------|
| `Algorithm` | `AlgorithmOLEDType` |
| `Color` | `CVOLED_COLOR` |
| `TempName` | `string` |
| `FDAType` | `CVOLED_FDAType` |
| `FixedLEDPoint` | `PointFloat[]` |
| `ImgFileName` | `string` |
| `OutputFileName` | `string` |
| `ImgPosResultFile` | `string` |

---

### 15. AlgorithmOLED_AOINode

- **完整类型**: `FlowEngineLib.Node.Algorithm.AlgorithmOLED_AOINode`
- **配置器**: `OLEDNodeConfigurators.cs`
- **实现文件**: `Node\Algorithm\AlgorithmOLED_AOINode.cs`
- **基类**: `CVBaseServerNode`

**配置面板属性** (NodeConfigurator)

| 属性名 | 类型 | 说明 |
|--------|------|------|
| `DeviceCode` | DeviceCode | 设备编码 |
| `ImgFileName` | ImagePath | 图片文件路径 |
| `TempName` | TemplateJsonRef | AOI |

**类级别属性** (Node Implementation)

| 属性名 | C# 类型 |
|--------|---------|
| `Algorithm` | `AlgorithmOLED_AOIType` |
| `TempName` | `string` |
| `ImgFileName` | `string` |
| `OutputFileName` | `string` |
| `CustomSN` | `string` |
| `VhLineEnable` | `bool` |
| `PixelDefectEnable` | `bool` |
| `MuraEnable` | `bool` |

---

### 16. Algorithm2InNode

- **完整类型**: `FlowEngineLib.Node.OLED.Algorithm2InNode`
- **配置器**: `OLEDNodeConfigurators.cs`
- **实现文件**: `Node\OLED\Algorithm2InNode.cs`
- **基类**: `CVBaseServerNodeHub`

**配置面板属性** (NodeConfigurator)

| 属性名 | 类型 | 说明 |
|--------|------|------|
| `DeviceCode` | DeviceCode | 设备编码 |
| `TempName` | TemplateRef | MTF |

**类级别属性** (Node Implementation)

| 属性名 | C# 类型 |
|--------|---------|
| `TempName` | `string` |
| `Algorithm` | `Algorithm2Type` |
| `BufferLen` | `int` |
| `IsAdd` | `bool` |

---

### 17. AlgorithmCompoundImgNode

- **完整类型**: `FlowEngineLib.Node.OLED.AlgorithmCompoundImgNode`
- **配置器**: `OLEDNodeConfigurators.cs`
- **实现文件**: `Node\OLED\AlgorithmCompoundImgNode.cs`
- **基类**: `CVBaseServerNodeHub`

**配置面板属性** (NodeConfigurator)

| 属性名 | 类型 | 说明 |
|--------|------|------|
| `DeviceCode` | DeviceCode | 设备编码 |
| `TempName` | TemplateJsonRef | 参数模板 |

**类级别属性** (Node Implementation)

| 属性名 | C# 类型 |
|--------|---------|
| `TempName` | `string` |
| `OutputFileName` | `string` |
| `BufferLen` | `int` |

---

## Camera 类结点

### 1. AOILocAndRegPixelsCameraNode

- **完整类型**: `FlowEngineLib.AOILocAndRegPixelsCameraNode`
- **配置器**: `CameraNodeConfigurators.cs`
- **实现文件**: `AOILocAndRegPixelsCameraNode.cs`
- **基类**: `CVBaseServerNode`

**配置面板属性** (NodeConfigurator)

| 属性名 | 类型 | 说明 |
|--------|------|------|
| `DeviceCode` | DeviceCode | 设备编码 |
| `AutoExpTempName` | TemplateRef | 曝光模板 |
| `CaliTempName` | TemplateRef | 校正 |
| `AlgTempName` | TemplateJsonRef | AOI |

**类级别属性** (Node Implementation)

| 属性名 | C# 类型 |
|--------|---------|
| `ImgSaveMode` | `ImgSaveBppMode` |
| `ImgSaveName` | `string` |
| `AvgCount` | `int` |
| `Gain` | `float` |
| `ExpTime` | `float` |
| `IsAutoExp` | `bool` |
| `AutoExpTempName` | `string` |
| `IsWithND` | `bool` |
| `CaliTempName` | `string` |
| `FlipMode` | `CVImageFlipMode` |
| `AlgTempName` | `string` |
| `Channel` | `CVOLED_Channel` |
| `OutputTempName` | `string` |

---

### 2. AOILocatePixelsCameraNode

- **完整类型**: `FlowEngineLib.AOILocatePixelsCameraNode`
- **配置器**: `CameraNodeConfigurators.cs`
- **实现文件**: `AOILocatePixelsCameraNode.cs`
- **基类**: `CVBaseServerNode`

**配置面板属性** (NodeConfigurator)

| 属性名 | 类型 | 说明 |
|--------|------|------|
| `DeviceCode` | DeviceCode | 设备编码 |
| `AutoExpTempName` | TemplateRef | 曝光模板 |
| `CaliTempName` | TemplateRef | 校正 |
| `AlgTempName` | TemplateJsonRef | 亚像素灯珠检测 |

**类级别属性** (Node Implementation)

| 属性名 | C# 类型 |
|--------|---------|
| `ImgSaveMode` | `ImgSaveBppMode` |
| `ImgSaveName` | `string` |
| `AvgCount` | `int` |
| `Gain` | `float` |
| `ExpTime` | `float` |
| `IsAutoExp` | `bool` |
| `AutoExpTempName` | `string` |
| `IsWithND` | `bool` |
| `CaliTempName` | `string` |
| `FlipMode` | `CVImageFlipMode` |
| `AlgTempName` | `string` |
| `Channel` | `CVOLED_Channel` |

---

### 3. CVCameraNode

- **完整类型**: `FlowEngineLib.CVCameraNode`
- **配置器**: `CameraNodeConfigurators.cs`
- **实现文件**: `CVCameraNode.cs`
- **基类**: `CVBaseServerNode`

**配置面板属性** (NodeConfigurator)

| 属性名 | 类型 | 说明 |
|--------|------|------|
| `DeviceCode` | DeviceCode | 设备编码 |
| `CalibTempName` | TemplateRef | 校正 |
| `POITempName` | TemplateRef | POI模板 |
| `POIFilterTempName` | TemplateRef | POI过滤 |
| `POIReviseTempName` | TemplateRef | POI修正 |

**类级别属性** (Node Implementation)

| 属性名 | C# 类型 |
|--------|---------|
| `AvgCount` | `int` |
| `Gain` | `float` |
| `TempR` | `float` |
| `TempG` | `float` |
| `TempB` | `float` |
| `CV2LVChannel` | `CV2LVChannelMode` |
| `CalibTempName` | `string` |
| `FlipMode` | `CVImageFlipMode` |
| `POITempName` | `string` |
| `POIFilterTempName` | `string` |
| `POIReviseTempName` | `string` |

---

### 4. CamMotorNode

- **完整类型**: `FlowEngineLib.CamMotorNode`
- **配置器**: `CameraNodeConfigurators.cs`
- **实现文件**: `CamMotorNode.cs`
- **基类**: `CVBaseServerNode`

**配置面板属性** (NodeConfigurator)

| 属性名 | 类型 | 说明 |
|--------|------|------|
| `DeviceCode` | DeviceCode | 设备编码 |
| `AutoFocusTemp` | TemplateRef | 相机模板 |

**类级别属性** (Node Implementation)

| 属性名 | C# 类型 |
|--------|---------|
| `RunType` | `CamMotorRunType` |
| `IsAbs` | `bool` |
| `Position` | `int` |
| `Aperture` | `float` |
| `AutoFocusTemp` | `string` |

---

### 5. LVCameraNode

- **完整类型**: `FlowEngineLib.LVCameraNode`
- **配置器**: `CameraNodeConfigurators.cs`
- **实现文件**: `LVCameraNode.cs`
- **基类**: `BaseCameraNode`

**配置面板属性** (NodeConfigurator)

| 属性名 | 类型 | 说明 |
|--------|------|------|
| `DeviceCode` | DeviceCode | 设备编码 |
| `CaliTempName` | TemplateRef | 校正 |
| `POITempName` | TemplateRef | POI模板 |
| `POIFilterTempName` | TemplateRef | POI过滤 |
| `POIReviseTempName` | TemplateRef | POI修正 |

---

### 6. CVAOI2CameraNode

- **完整类型**: `FlowEngineLib.Node.Camera.CVAOI2CameraNode`
- **配置器**: `CameraNodeConfigurators.cs`
- **实现文件**: `Node\Camera\CVAOI2CameraNode.cs`
- **基类**: `CVBaseServerNodeHub`

**配置面板属性** (NodeConfigurator)

| 属性名 | 类型 | 说明 |
|--------|------|------|
| `DeviceCode` | DeviceCode | 设备编码 |
| `CamTempName` | TemplateRef | 相机模板 |
| `TempName` | TemplateRef | 曝光模板 |
| `CalibTempName` | TemplateRef | 校正 |
| `AlgTempName` | TemplateJsonRef | AOI |

**类级别属性** (Node Implementation)

| 属性名 | C# 类型 |
|--------|---------|
| `CamTempName` | `string` |
| `ImgSaveMode` | `ImgSaveBppMode` |
| `FlipMode` | `CVImageFlipMode` |
| `IsAutoExp` | `bool` |
| `TempName` | `string` |
| `IsWithND` | `bool` |
| `CalibTempName` | `string` |
| `AOIType` | `AOI2TypeEnum` |
| `AlgTempName` | `string` |

---

### 7. CVAOICameraNode

- **完整类型**: `FlowEngineLib.Node.Camera.CVAOICameraNode`
- **配置器**: `CameraNodeConfigurators.cs`
- **实现文件**: `Node\Camera\CVAOICameraNode.cs`
- **基类**: `CVBaseServerNode`

**配置面板属性** (NodeConfigurator)

| 属性名 | 类型 | 说明 |
|--------|------|------|
| `DeviceCode` | DeviceCode | 设备编码 |
| `CamTempName` | TemplateRef | 相机模板 |
| `TempName` | TemplateRef | 曝光模板 |
| `CalibTempName` | TemplateRef | 校正 |
| `AlgTempName` | TemplateJsonRef | 亚像素灯珠检测 |

**类级别属性** (Node Implementation)

| 属性名 | C# 类型 |
|--------|---------|
| `CamTempName` | `string` |
| `ImgSaveMode` | `ImgSaveBppMode` |
| `FlipMode` | `CVImageFlipMode` |
| `IsAutoExp` | `bool` |
| `TempName` | `string` |
| `IsWithND` | `bool` |
| `CalibTempName` | `string` |
| `AOIType` | `AOITypeEnum` |
| `AlgTempName` | `string` |

---

### 8. CommCameraNode

- **完整类型**: `FlowEngineLib.Node.Camera.CommCameraNode`
- **配置器**: `CameraNodeConfigurators.cs`
- **实现文件**: `Node\Camera\CommCameraNode.cs`
- **基类**: `CVBaseServerNode`

**配置面板属性** (NodeConfigurator)

| 属性名 | 类型 | 说明 |
|--------|------|------|
| `DeviceCode` | DeviceCode | 设备编码 |
| `CalibTempName` | TemplateRef | 校正 |
| `CamTempName` | TemplateRef | 相机模板 |
| `TempName` | TemplateRef | 曝光模板 |
| `POITempName` | TemplateRef | POI模板 |
| `POIFilterTempName` | TemplateRef | POI过滤 |
| `POIReviseTempName` | TemplateRef | POI修正 |

**类级别属性** (Node Implementation)

| 属性名 | C# 类型 |
|--------|---------|
| `IsHDR` | `bool` |
| `CamTempName` | `string` |
| `FlipMode` | `CVImageFlipMode` |
| `IsAutoExp` | `bool` |
| `TempName` | `string` |
| `IsWithND` | `bool` |
| `CalibTempName` | `string` |
| `POITempName` | `string` |
| `POIFilterTempName` | `string` |
| `POIReviseTempName` | `string` |

---

## OLED 类结点

### 1. OLEDImageCroppingNode

- **完整类型**: `FlowEngineLib.Node.OLED.OLEDImageCroppingNode`
- **配置器**: `OLEDNodeConfigurators.cs`
- **实现文件**: `Node\OLED\OLEDImageCroppingNode.cs`
- **基类**: `CVBaseServerNodeHub`

**配置面板属性** (NodeConfigurator)

| 属性名 | 类型 | 说明 |
|--------|------|------|
| `DeviceCode` | DeviceCode | 设备编码 |
| `TempName` | TemplateRef | 参数模板 |

**类级别属性** (Node Implementation)

| 属性名 | C# 类型 |
|--------|---------|
| `TempName` | `string` |
| `ImgFileName` | `string` |

---

### 2. OLEDRebuildPixelsNode

- **完整类型**: `FlowEngineLib.Node.OLED.OLEDRebuildPixelsNode`
- **配置器**: `OLEDNodeConfigurators.cs`
- **实现文件**: `Node\OLED\OLEDRebuildPixelsNode.cs`
- **基类**: `CVBaseServerNodeHub`

**配置面板属性** (NodeConfigurator)

| 属性名 | 类型 | 说明 |
|--------|------|------|
| `DeviceCode` | DeviceCode | 设备编码 |
| `ImgFileName` | ImagePath | 图片文件路径 |
| `OutputTemplateName` | TemplateRef | PoiOutPut |
| `TempName` | TemplateJsonRef | 亚像素灯珠检测 |

**类级别属性** (Node Implementation)

| 属性名 | C# 类型 |
|--------|---------|
| `Channel` | `CVOLED_Channel` |
| `TempName` | `string` |
| `ImgFileName` | `string` |
| `OutputTemplateName` | `string` |

---

## POI 类结点

### 1. BuildPOINode

- **完整类型**: `FlowEngineLib.BuildPOINode`
- **配置器**: `POINodeConfigurators.cs`
- **实现文件**: `BuildPOINode.cs`
- **基类**: `CVBaseServerNode`

**配置面板属性** (NodeConfigurator)

| 属性名 | 类型 | 说明 |
|--------|------|------|
| `DeviceCode` | DeviceCode | 设备编码 |
| `ImgFileName` | ImagePath | 图片文件路径 |
| `TemplateName` | TemplateRef | 布点模板 |
| `RePOITemplateName` | TemplateRef | RePOI |
| `LayoutROITemplate` | TemplateRef | 布点ROI |
| `SavePOITempName` | TemplateRef | SavePOI |

**类级别属性** (Node Implementation)

| 属性名 | C# 类型 |
|--------|---------|
| `TemplateName` | `string` |
| `RePOITemplateName` | `string` |
| `LayoutROITemplate` | `string` |
| `BuildType` | `POIBuildType` |
| `PrefixName` | `string` |
| `POIType` | `POIPointTypes` |
| `POIHeight` | `int` |
| `POIWidth` | `int` |
| `ImgFileName` | `string` |
| `CAD_PosFileName` | `string` |
| `POIOutput` | `POIStorageModel` |
| `OutputFileName` | `string` |
| `SavePOITempName` | `string` |
| `BufferLen` | `int` |

---

### 2. POIAnalysisNode

- **完整类型**: `FlowEngineLib.Node.POI.POIAnalysisNode`
- **配置器**: `POINodeConfigurators.cs`
- **实现文件**: `Node\POI\POIAnalysisNode.cs`
- **基类**: `CVBaseServerNode`

**配置面板属性** (NodeConfigurator)

| 属性名 | 类型 | 说明 |
|--------|------|------|
| `DeviceCode` | DeviceCode | 设备编码 |
| `TempName` | TemplateJsonRef | PoiAnalysis |

**类级别属性** (Node Implementation)

| 属性名 | C# 类型 |
|--------|---------|
| `TempName` | `string` |

---

### 3. POIReviseNode

- **完整类型**: `FlowEngineLib.Node.POI.POIReviseNode`
- **配置器**: `POINodeConfigurators.cs`
- **实现文件**: `Node\POI\POIReviseNode.cs`
- **基类**: `CVBaseServerNodeHub`

**配置面板属性** (NodeConfigurator)

| 属性名 | 类型 | 说明 |
|--------|------|------|
| `DeviceCode` | DeviceCode | 设备编码 |
| `TemplateName` | TemplateRef | POI修正标定 |

**类级别属性** (Node Implementation)

| 属性名 | C# 类型 |
|--------|---------|
| `TemplateName` | `string` |
| `POIPointName` | `string` |
| `IsSelfResultRevise` | `bool` |

---

### 4. RealPOINode

- **完整类型**: `FlowEngineLib.Node.POI.RealPOINode`
- **配置器**: `POINodeConfigurators.cs`
- **实现文件**: `Node\POI\RealPOINode.cs`
- **基类**: `CVBaseServerNodeHub`

**配置面板属性** (NodeConfigurator)

| 属性名 | 类型 | 说明 |
|--------|------|------|
| `DeviceCode` | DeviceCode | 设备编码 |
| `FilterTemplateName` | TemplateRef | POI过滤 |
| `ReviseTemplateName` | TemplateRef | POI修正 |
| `OutputTemplateName` | TemplateRef | 文件输出模板 |

**类级别属性** (Node Implementation)

| 属性名 | C# 类型 |
|--------|---------|
| `ImgFileName` | `string` |
| `FilterTemplateName` | `string` |
| `ReviseTemplateName` | `string` |
| `ReviseFileName` | `string` |
| `OutputTemplateName` | `string` |
| `SubPixelTemplateName` | `string` |
| `POIType` | `POIPointTypes` |
| `POIHeight` | `float` |
| `POIWidth` | `float` |
| `IsResultAdd` | `bool` |
| `IsCCTWave` | `bool` |

---

### 5. POINode

- **完整类型**: `FlowEngineLib.POINode`
- **配置器**: `POINodeConfigurators.cs`
- **实现文件**: `POINode.cs`
- **基类**: `CVBaseServerNode`

**配置面板属性** (NodeConfigurator)

| 属性名 | 类型 | 说明 |
|--------|------|------|
| `DeviceCode` | DeviceCode | 设备编码 |
| `ImgFileName` | ImagePath | 图片文件路径 |
| `TempName` | TemplateRef | POI模板 |
| `FilterTemplateName` | TemplateRef | POI过滤 |
| `ReviseTemplateName` | TemplateRef | POI修正 |
| `OutputTemplateName` | TemplateRef | 文件输出模板 |

**类级别属性** (Node Implementation)

| 属性名 | C# 类型 |
|--------|---------|
| `TempName` | `string` |
| `FilterTemplateName` | `string` |
| `ReviseTemplateName` | `string` |
| `OutputTemplateName` | `string` |
| `ImgFileName` | `string` |
| `IsCCTWave` | `bool` |
| `IsSubPixel` | `bool` |

---

## PG 类结点

### 1. PGNode

- **完整类型**: `FlowEngineLib.Node.PG.PGNode`
- **配置器**: `DeviceNodeConfigurators.cs`
- **实现文件**: `Node\PG\PGNode.cs`
- **基类**: `CVBaseServerNode`

**配置面板属性** (NodeConfigurator)

| 属性名 | 类型 | 说明 |
|--------|------|------|
| `DeviceCode` | DeviceCode | 设备编码 |

**类级别属性** (Node Implementation)

| 属性名 | C# 类型 |
|--------|---------|
| `PGCmd` | `PGCommCmdType` |
| `IndexFrame` | `int` |

---

## SMU 类结点

### 1. SMUFromCSVNode

- **完整类型**: `FlowEngineLib.SMUFromCSVNode`
- **配置器**: `DeviceNodeConfigurators.cs`
- **实现文件**: `SMUFromCSVNode.cs`
- **基类**: `SMUBaseNode`

**配置面板属性** (NodeConfigurator)

| 属性名 | 类型 | 说明 |
|--------|------|------|
| `DeviceCode` | DeviceCode | 设备编码 |
| `CsvFileName` | ImagePath | 图片文件路径 |

**类级别属性** (Node Implementation)

| 属性名 | C# 类型 |
|--------|---------|
| `Source` | `SourceType` |
| `Channel` | `SMUChannelType` |
| `CsvFileName` | `string` |
| `IsAutoRng` | `bool` |
| `SrcRng` | `double` |
| `LmtRng` | `double` |

---

### 2. SMUModelNode

- **完整类型**: `FlowEngineLib.SMUModelNode`
- **配置器**: `DeviceNodeConfigurators.cs`
- **实现文件**: `SMUModelNode.cs`
- **基类**: `SMUBaseNode`

**配置面板属性** (NodeConfigurator)

| 属性名 | 类型 | 说明 |
|--------|------|------|
| `DeviceCode` | DeviceCode | 设备编码 |
| `ModelName` | TemplateRef | SMUParam设置 |

**类级别属性** (Node Implementation)

| 属性名 | C# 类型 |
|--------|---------|
| `ModelName` | `string` |

---

### 3. SMUNode

- **完整类型**: `FlowEngineLib.SMUNode`
- **配置器**: `DeviceNodeConfigurators.cs`
- **实现文件**: `SMUNode.cs`
- **基类**: `SMUBaseNode`

**配置面板属性** (NodeConfigurator)

| 属性名 | 类型 | 说明 |
|--------|------|------|
| `DeviceCode` | DeviceCode | 设备编码 |

**类级别属性** (Node Implementation)

| 属性名 | C# 类型 |
|--------|---------|
| `Source` | `SourceType` |
| `Channel` | `SMUChannelType` |
| `BeginVal` | `float` |
| `EndVal` | `float` |
| `LimitVal` | `float` |
| `PointNum` | `int` |
| `IsAutoRng` | `bool` |
| `SrcRng` | `double` |
| `LmtRng` | `double` |

---

## Sensor 类结点

### 1. CommonSensorNode

- **完整类型**: `FlowEngineLib.CommonSensorNode`
- **配置器**: `DeviceNodeConfigurators.cs`
- **实现文件**: `CommonSensorNode.cs`
- **基类**: `CVBaseServerNode`

**配置面板属性** (NodeConfigurator)

| 属性名 | 类型 | 说明 |
|--------|------|------|
| `DeviceCode` | DeviceCode | 设备编码 |
| `TempName` | SensorTemplateRef | 传感器模板 |

**类级别属性** (Node Implementation)

| 属性名 | C# 类型 |
|--------|---------|
| `TempName` | `string` |
| `CmdType` | `CommCmdType` |
| `CmdSend` | `string` |
| `CmdReceive` | `string` |

---

### 2. TempCommonSensorNode

- **完整类型**: `FlowEngineLib.TempCommonSensorNode`
- **配置器**: `DeviceNodeConfigurators.cs`
- **实现文件**: `TempCommonSensorNode.cs`
- **基类**: `CVBaseServerNode`

**配置面板属性** (NodeConfigurator)

| 属性名 | 类型 | 说明 |
|--------|------|------|
| `DeviceCode` | DeviceCode | 设备编码 |
| `TempName` | SensorTemplateRef | 传感器模板 |

**类级别属性** (Node Implementation)

| 属性名 | C# 类型 |
|--------|---------|
| `TempName` | `string` |

---

## FW 类结点

### 1. FWNode

- **完整类型**: `FlowEngineLib.FWNode`
- **配置器**: `DeviceNodeConfigurators.cs`
- **实现文件**: `FWNode.cs`
- **基类**: `CVBaseServerNode`

**配置面板属性** (NodeConfigurator)

| 属性名 | 类型 | 说明 |
|--------|------|------|
| `DeviceCode` | DeviceCode | 设备编码 |

**类级别属性** (Node Implementation)

| 属性名 | C# 类型 |
|--------|---------|
| `Port` | `int` |
| `ModelType` | `FWModelType` |

---

## Spectrum 类结点

### 1. SpectrumEQENode

- **完整类型**: `SpectrumEQENode`
- **配置器**: `SpectrumNodeConfigurators.cs`

---

### 2. SpectrumLoopNode

- **完整类型**: `SpectrumLoopNode`
- **配置器**: `SpectrumNodeConfigurators.cs`

---

### 3. SpectrumNode

- **完整类型**: `SpectrumNode`
- **配置器**: `SpectrumNodeConfigurators.cs`

---
