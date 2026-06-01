# Flow Engine 結點參考文件

> 自動生成於 2026-05-22，基於以下原始碼目錄：
> - 結點配置: `Engine\ColorVision.Engine\Templates\Flow\NodeConfigurator\`
> - 結點實現: `Engine\FlowEngineLib\`

## 概覽

共 **42** 個已配置結點，按型別分組如下：

| 型別 | 數量 |
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

## Algorithm 類結點

### 1. AlgorithmARVRNode

- **完整型別**: `FlowEngineLib.Algorithm.AlgorithmARVRNode`
- **配置器**: `AlgorithmNodeConfigurators.cs`
- **實現檔案**: `Algorithm\AlgorithmARVRNode.cs`
- **基類**: `CVBaseServerNode`

**配置面板屬性** (NodeConfigurator)

| 屬性名 | 型別 | 說明 |
|--------|------|------|
| `DeviceCode` | DeviceCode | 裝置編碼 |
| `ImgFileName` | ImagePath | 圖片檔案路徑 |
| `TempName` | TemplateRef | MTF |
| `POITempName` | TemplateRef | POI |

**類級別屬性** (Node Implementation)

| 屬性名 | C# 型別 |
|--------|---------|
| `Algorithm` | `AlgorithmARVRType` |
| `TempName` | `string` |
| `POITempName` | `string` |
| `ImgFileName` | `string` |
| `Color` | `CVOLED_COLOR` |
| `BufferLen` | `int` |

---

### 2. AlgorithmNode

- **完整型別**: `FlowEngineLib.Algorithm.AlgorithmNode`
- **配置器**: `AlgorithmNodeConfigurators.cs`
- **實現檔案**: `Algorithm\AlgorithmNode.cs`
- **基類**: `CVBaseServerNode`

**配置面板屬性** (NodeConfigurator)

| 屬性名 | 型別 | 說明 |
|--------|------|------|
| `DeviceCode` | DeviceCode | 裝置編碼 |
| `ImgFileName` | ImagePath | 圖片檔案路徑 |
| `POITempName` | TemplateRef | POI |
| `TempName` | TemplateRef | MTF |

**類級別屬性** (Node Implementation)

| 屬性名 | C# 型別 |
|--------|---------|
| `Algorithm` | `AlgorithmType` |
| `TempName` | `string` |
| `POITempName` | `string` |
| `ImgFileName` | `string` |
| `Color` | `CVOLED_COLOR` |
| `BufferLen` | `int` |

---

### 3. CalibrationNode

- **完整型別**: `FlowEngineLib.Algorithm.CalibrationNode`
- **配置器**: `DeviceNodeConfigurators.cs`
- **實現檔案**: `Algorithm\CalibrationNode.cs`
- **基類**: `CVBaseServerNode`

**配置面板屬性** (NodeConfigurator)

| 屬性名 | 型別 | 說明 |
|--------|------|------|
| `DeviceCode` | DeviceCode | 裝置編碼 |
| `ImgFileName` | ImagePath | 圖片檔案路徑 |
| `TempName` | TemplateRef | 校正 |

**類級別屬性** (Node Implementation)

| 屬性名 | C# 型別 |
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

- **完整型別**: `FlowEngineLib.Node.Algorithm.AlgComplianceMathNode`
- **配置器**: `AlgorithmNodeConfigurators.cs`
- **實現檔案**: `Node\Algorithm\AlgComplianceMathNode.cs`
- **基類**: `CVBaseServerNode`

**配置面板屬性** (NodeConfigurator)

| 屬性名 | 型別 | 說明 |
|--------|------|------|
| `DeviceCode` | DeviceCode | 裝置編碼 |
| `TempName` | TemplateRef | JND |

**類級別屬性** (Node Implementation)

| 屬性名 | C# 型別 |
|--------|---------|
| `TempName` | `string` |
| `ComplianceMath` | `ComplianceMathType` |
| `IsBreak` | `bool` |

---

### 5. AlgDataLoadNode

- **完整型別**: `FlowEngineLib.Node.Algorithm.AlgDataLoadNode`
- **配置器**: `AlgorithmNodeConfigurators.cs`
- **實現檔案**: `Node\Algorithm\AlgDataLoadNode.cs`
- **基類**: `CVBaseServerNode`

**配置面板屬性** (NodeConfigurator)

| 屬性名 | 型別 | 說明 |
|--------|------|------|
| `DeviceCode` | DeviceCode | 裝置編碼 |
| `TempName` | TemplateRef | 模板 |

**類級別屬性** (Node Implementation)

| 屬性名 | C# 型別 |
|--------|---------|
| `TempName` | `string` |

---

### 6. AlgorithmBlackMuraNode

- **完整型別**: `FlowEngineLib.Node.Algorithm.AlgorithmBlackMuraNode`
- **配置器**: `AlgorithmNodeConfigurators.cs`
- **實現檔案**: `Node\Algorithm\AlgorithmBlackMuraNode.cs`
- **基類**: `CVBaseServerNode`

**配置面板屬性** (NodeConfigurator)

| 屬性名 | 型別 | 說明 |
|--------|------|------|
| `DeviceCode` | DeviceCode | 裝置編碼 |
| `ImgFileName` | ImagePath | 圖片檔案路徑 |
| `TempName` | TemplateJsonRef | BlackMura |

**類級別屬性** (Node Implementation)

| 屬性名 | C# 型別 |
|--------|---------|
| `TempName` | `string` |
| `ImgFileName` | `string` |
| `OIndex` | `string` |
| `SavePOITempName` | `string` |

---

### 7. AlgorithmCaliNode

- **完整型別**: `FlowEngineLib.Node.Algorithm.AlgorithmCaliNode`
- **配置器**: `AlgorithmNodeConfigurators.cs`
- **實現檔案**: `Node\Algorithm\AlgorithmCaliNode.cs`
- **基類**: `CVBaseServerNode`

**配置面板屬性** (NodeConfigurator)

| 屬性名 | 型別 | 說明 |
|--------|------|------|
| `DeviceCode` | DeviceCode | 裝置編碼 |
| `ImgFileName` | ImagePath | 圖片檔案路徑 |
| `TempName` | TemplateJsonRef | 色差 |

**類級別屬性** (Node Implementation)

| 屬性名 | C# 型別 |
|--------|---------|
| `TempName` | `string` |
| `ImgFileName` | `string` |
| `OutputFileName` | `string` |

---

### 8. AlgorithmFindLEDNode

- **完整型別**: `FlowEngineLib.Node.Algorithm.AlgorithmFindLEDNode`
- **配置器**: `AlgorithmNodeConfigurators.cs`
- **實現檔案**: `Node\Algorithm\AlgorithmFindLEDNode.cs`
- **基類**: `CVBaseServerNode`

**配置面板屬性** (NodeConfigurator)

| 屬性名 | 型別 | 說明 |
|--------|------|------|
| `DeviceCode` | DeviceCode | 裝置編碼 |
| `ImgFileName` | ImagePath | 圖片檔案路徑 |
| `TempName` | TemplateRef | 畫素級燈珠檢測 |

**類級別屬性** (Node Implementation)

| 屬性名 | C# 型別 |
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

- **完整型別**: `FlowEngineLib.Node.Algorithm.AlgorithmFindLightAreaNode`
- **配置器**: `AlgorithmNodeConfigurators.cs`
- **實現檔案**: `Node\Algorithm\AlgorithmFindLightAreaNode.cs`
- **基類**: `CVBaseServerNode`

**配置面板屬性** (NodeConfigurator)

| 屬性名 | 型別 | 說明 |
|--------|------|------|
| `DeviceCode` | DeviceCode | 裝置編碼 |
| `ImgFileName` | ImagePath | 圖片檔案路徑 |
| `TempName` | TemplateRef | 發光區定位 |
| `SavePOITempName` | TemplateRef | 儲存POI |

**類級別屬性** (Node Implementation)

| 屬性名 | C# 型別 |
|--------|---------|
| `TempName` | `string` |
| `ImgFileName` | `string` |
| `SavePOITempName` | `string` |
| `BufferLen` | `int` |
| `OIndex` | `string` |

---

### 10. AlgorithmGhostV2Node

- **完整型別**: `FlowEngineLib.Node.Algorithm.AlgorithmGhostV2Node`
- **配置器**: `AlgorithmNodeConfigurators.cs`
- **實現檔案**: `Node\Algorithm\AlgorithmGhostV2Node.cs`
- **基類**: `CVBaseServerNodeHub`

**配置面板屬性** (NodeConfigurator)

| 屬性名 | 型別 | 說明 |
|--------|------|------|
| `DeviceCode` | DeviceCode | 裝置編碼 |
| `ImgFileName` | ImagePath | 圖片檔案路徑 |
| `TempName` | TemplateRef | Ghost |

**類級別屬性** (Node Implementation)

| 屬性名 | C# 型別 |
|--------|---------|
| `TempName` | `string` |
| `ImgFileName` | `string` |
| `BufferLen` | `int` |

---

### 11. AlgorithmImageROINode

- **完整型別**: `FlowEngineLib.Node.Algorithm.AlgorithmImageROINode`
- **配置器**: `AlgorithmNodeConfigurators.cs`
- **實現檔案**: `Node\Algorithm\AlgorithmImageROINode.cs`
- **基類**: `CVBaseServerNode`

**配置面板屬性** (NodeConfigurator)

| 屬性名 | 型別 | 說明 |
|--------|------|------|
| `DeviceCode` | DeviceCode | 裝置編碼 |
| `ImgFileName` | ImagePath | 圖片檔案路徑 |
| `TempName` | TemplateJsonRef | 模板名稱 |

**類級別屬性** (Node Implementation)

| 屬性名 | C# 型別 |
|--------|---------|
| `TempName` | `string` |
| `ImgFileName` | `string` |
| `OutputFileName` | `string` |

---

### 12. AlgorithmKBNode

- **完整型別**: `FlowEngineLib.Node.Algorithm.AlgorithmKBNode`
- **配置器**: `AlgorithmNodeConfigurators.cs`
- **實現檔案**: `Node\Algorithm\AlgorithmKBNode.cs`
- **基類**: `CVBaseServerNode`

**配置面板屬性** (NodeConfigurator)

| 屬性名 | 型別 | 說明 |
|--------|------|------|
| `DeviceCode` | DeviceCode | 裝置編碼 |
| `ImgFileName` | ImagePath | 圖片檔案路徑 |
| `TempName` | TemplateKBRef | KB |

**類級別屬性** (Node Implementation)

| 屬性名 | C# 型別 |
|--------|---------|
| `TempName` | `string` |
| `ImgFileName` | `string` |

---

### 13. AlgorithmKBOutputNode

- **完整型別**: `FlowEngineLib.Node.Algorithm.AlgorithmKBOutputNode`
- **配置器**: `AlgorithmNodeConfigurators.cs`
- **實現檔案**: `Node\Algorithm\AlgorithmKBOutputNode.cs`
- **基類**: `CVBaseServerNode`

**配置面板屬性** (NodeConfigurator)

| 屬性名 | 型別 | 說明 |
|--------|------|------|
| `DeviceCode` | DeviceCode | 裝置編碼 |
| `TempName` | TemplateKBRef | KB |

**類級別屬性** (Node Implementation)

| 屬性名 | C# 型別 |
|--------|---------|
| `TempName` | `string` |

---

### 14. AlgorithmOLEDNode

- **完整型別**: `FlowEngineLib.Node.Algorithm.AlgorithmOLEDNode`
- **配置器**: `OLEDNodeConfigurators.cs`
- **實現檔案**: `Node\Algorithm\AlgorithmOLEDNode.cs`
- **基類**: `CVBaseServerNode`

**配置面板屬性** (NodeConfigurator)

| 屬性名 | 型別 | 說明 |
|--------|------|------|
| `DeviceCode` | DeviceCode | 裝置編碼 |
| `ImgFileName` | ImagePath | 圖片檔案路徑 |
| `TempName` | TemplateJsonRef | 亞畫素 |

**類級別屬性** (Node Implementation)

| 屬性名 | C# 型別 |
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

- **完整型別**: `FlowEngineLib.Node.Algorithm.AlgorithmOLED_AOINode`
- **配置器**: `OLEDNodeConfigurators.cs`
- **實現檔案**: `Node\Algorithm\AlgorithmOLED_AOINode.cs`
- **基類**: `CVBaseServerNode`

**配置面板屬性** (NodeConfigurator)

| 屬性名 | 型別 | 說明 |
|--------|------|------|
| `DeviceCode` | DeviceCode | 裝置編碼 |
| `ImgFileName` | ImagePath | 圖片檔案路徑 |
| `TempName` | TemplateJsonRef | AOI |

**類級別屬性** (Node Implementation)

| 屬性名 | C# 型別 |
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

- **完整型別**: `FlowEngineLib.Node.OLED.Algorithm2InNode`
- **配置器**: `OLEDNodeConfigurators.cs`
- **實現檔案**: `Node\OLED\Algorithm2InNode.cs`
- **基類**: `CVBaseServerNodeHub`

**配置面板屬性** (NodeConfigurator)

| 屬性名 | 型別 | 說明 |
|--------|------|------|
| `DeviceCode` | DeviceCode | 裝置編碼 |
| `TempName` | TemplateRef | MTF |

**類級別屬性** (Node Implementation)

| 屬性名 | C# 型別 |
|--------|---------|
| `TempName` | `string` |
| `Algorithm` | `Algorithm2Type` |
| `BufferLen` | `int` |
| `IsAdd` | `bool` |

---

### 17. AlgorithmCompoundImgNode

- **完整型別**: `FlowEngineLib.Node.OLED.AlgorithmCompoundImgNode`
- **配置器**: `OLEDNodeConfigurators.cs`
- **實現檔案**: `Node\OLED\AlgorithmCompoundImgNode.cs`
- **基類**: `CVBaseServerNodeHub`

**配置面板屬性** (NodeConfigurator)

| 屬性名 | 型別 | 說明 |
|--------|------|------|
| `DeviceCode` | DeviceCode | 裝置編碼 |
| `TempName` | TemplateJsonRef | 參數模板 |

**類級別屬性** (Node Implementation)

| 屬性名 | C# 型別 |
|--------|---------|
| `TempName` | `string` |
| `OutputFileName` | `string` |
| `BufferLen` | `int` |

---

## Camera 類結點

### 1. AOILocAndRegPixelsCameraNode

- **完整型別**: `FlowEngineLib.AOILocAndRegPixelsCameraNode`
- **配置器**: `CameraNodeConfigurators.cs`
- **實現檔案**: `AOILocAndRegPixelsCameraNode.cs`
- **基類**: `CVBaseServerNode`

**配置面板屬性** (NodeConfigurator)

| 屬性名 | 型別 | 說明 |
|--------|------|------|
| `DeviceCode` | DeviceCode | 裝置編碼 |
| `AutoExpTempName` | TemplateRef | 曝光模板 |
| `CaliTempName` | TemplateRef | 校正 |
| `AlgTempName` | TemplateJsonRef | AOI |

**類級別屬性** (Node Implementation)

| 屬性名 | C# 型別 |
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

- **完整型別**: `FlowEngineLib.AOILocatePixelsCameraNode`
- **配置器**: `CameraNodeConfigurators.cs`
- **實現檔案**: `AOILocatePixelsCameraNode.cs`
- **基類**: `CVBaseServerNode`

**配置面板屬性** (NodeConfigurator)

| 屬性名 | 型別 | 說明 |
|--------|------|------|
| `DeviceCode` | DeviceCode | 裝置編碼 |
| `AutoExpTempName` | TemplateRef | 曝光模板 |
| `CaliTempName` | TemplateRef | 校正 |
| `AlgTempName` | TemplateJsonRef | 亞畫素燈珠檢測 |

**類級別屬性** (Node Implementation)

| 屬性名 | C# 型別 |
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

- **完整型別**: `FlowEngineLib.CVCameraNode`
- **配置器**: `CameraNodeConfigurators.cs`
- **實現檔案**: `CVCameraNode.cs`
- **基類**: `CVBaseServerNode`

**配置面板屬性** (NodeConfigurator)

| 屬性名 | 型別 | 說明 |
|--------|------|------|
| `DeviceCode` | DeviceCode | 裝置編碼 |
| `CalibTempName` | TemplateRef | 校正 |
| `POITempName` | TemplateRef | POI模板 |
| `POIFilterTempName` | TemplateRef | POI過濾 |
| `POIReviseTempName` | TemplateRef | POI修正 |

**類級別屬性** (Node Implementation)

| 屬性名 | C# 型別 |
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

- **完整型別**: `FlowEngineLib.CamMotorNode`
- **配置器**: `CameraNodeConfigurators.cs`
- **實現檔案**: `CamMotorNode.cs`
- **基類**: `CVBaseServerNode`

**配置面板屬性** (NodeConfigurator)

| 屬性名 | 型別 | 說明 |
|--------|------|------|
| `DeviceCode` | DeviceCode | 裝置編碼 |
| `AutoFocusTemp` | TemplateRef | 相機模板 |

**類級別屬性** (Node Implementation)

| 屬性名 | C# 型別 |
|--------|---------|
| `RunType` | `CamMotorRunType` |
| `IsAbs` | `bool` |
| `Position` | `int` |
| `Aperture` | `float` |
| `AutoFocusTemp` | `string` |

---

### 5. LVCameraNode

- **完整型別**: `FlowEngineLib.LVCameraNode`
- **配置器**: `CameraNodeConfigurators.cs`
- **實現檔案**: `LVCameraNode.cs`
- **基類**: `BaseCameraNode`

**配置面板屬性** (NodeConfigurator)

| 屬性名 | 型別 | 說明 |
|--------|------|------|
| `DeviceCode` | DeviceCode | 裝置編碼 |
| `CaliTempName` | TemplateRef | 校正 |
| `POITempName` | TemplateRef | POI模板 |
| `POIFilterTempName` | TemplateRef | POI過濾 |
| `POIReviseTempName` | TemplateRef | POI修正 |

---

### 6. CVAOI2CameraNode

- **完整型別**: `FlowEngineLib.Node.Camera.CVAOI2CameraNode`
- **配置器**: `CameraNodeConfigurators.cs`
- **實現檔案**: `Node\Camera\CVAOI2CameraNode.cs`
- **基類**: `CVBaseServerNodeHub`

**配置面板屬性** (NodeConfigurator)

| 屬性名 | 型別 | 說明 |
|--------|------|------|
| `DeviceCode` | DeviceCode | 裝置編碼 |
| `CamTempName` | TemplateRef | 相機模板 |
| `TempName` | TemplateRef | 曝光模板 |
| `CalibTempName` | TemplateRef | 校正 |
| `AlgTempName` | TemplateJsonRef | AOI |

**類級別屬性** (Node Implementation)

| 屬性名 | C# 型別 |
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

- **完整型別**: `FlowEngineLib.Node.Camera.CVAOICameraNode`
- **配置器**: `CameraNodeConfigurators.cs`
- **實現檔案**: `Node\Camera\CVAOICameraNode.cs`
- **基類**: `CVBaseServerNode`

**配置面板屬性** (NodeConfigurator)

| 屬性名 | 型別 | 說明 |
|--------|------|------|
| `DeviceCode` | DeviceCode | 裝置編碼 |
| `CamTempName` | TemplateRef | 相機模板 |
| `TempName` | TemplateRef | 曝光模板 |
| `CalibTempName` | TemplateRef | 校正 |
| `AlgTempName` | TemplateJsonRef | 亞畫素燈珠檢測 |

**類級別屬性** (Node Implementation)

| 屬性名 | C# 型別 |
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

- **完整型別**: `FlowEngineLib.Node.Camera.CommCameraNode`
- **配置器**: `CameraNodeConfigurators.cs`
- **實現檔案**: `Node\Camera\CommCameraNode.cs`
- **基類**: `CVBaseServerNode`

**配置面板屬性** (NodeConfigurator)

| 屬性名 | 型別 | 說明 |
|--------|------|------|
| `DeviceCode` | DeviceCode | 裝置編碼 |
| `CalibTempName` | TemplateRef | 校正 |
| `CamTempName` | TemplateRef | 相機模板 |
| `TempName` | TemplateRef | 曝光模板 |
| `POITempName` | TemplateRef | POI模板 |
| `POIFilterTempName` | TemplateRef | POI過濾 |
| `POIReviseTempName` | TemplateRef | POI修正 |

**類級別屬性** (Node Implementation)

| 屬性名 | C# 型別 |
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

## OLED 類結點

### 1. OLEDImageCroppingNode

- **完整型別**: `FlowEngineLib.Node.OLED.OLEDImageCroppingNode`
- **配置器**: `OLEDNodeConfigurators.cs`
- **實現檔案**: `Node\OLED\OLEDImageCroppingNode.cs`
- **基類**: `CVBaseServerNodeHub`

**配置面板屬性** (NodeConfigurator)

| 屬性名 | 型別 | 說明 |
|--------|------|------|
| `DeviceCode` | DeviceCode | 裝置編碼 |
| `TempName` | TemplateRef | 參數模板 |

**類級別屬性** (Node Implementation)

| 屬性名 | C# 型別 |
|--------|---------|
| `TempName` | `string` |
| `ImgFileName` | `string` |

---

### 2. OLEDRebuildPixelsNode

- **完整型別**: `FlowEngineLib.Node.OLED.OLEDRebuildPixelsNode`
- **配置器**: `OLEDNodeConfigurators.cs`
- **實現檔案**: `Node\OLED\OLEDRebuildPixelsNode.cs`
- **基類**: `CVBaseServerNodeHub`

**配置面板屬性** (NodeConfigurator)

| 屬性名 | 型別 | 說明 |
|--------|------|------|
| `DeviceCode` | DeviceCode | 裝置編碼 |
| `ImgFileName` | ImagePath | 圖片檔案路徑 |
| `OutputTemplateName` | TemplateRef | PoiOutPut |
| `TempName` | TemplateJsonRef | 亞畫素燈珠檢測 |

**類級別屬性** (Node Implementation)

| 屬性名 | C# 型別 |
|--------|---------|
| `Channel` | `CVOLED_Channel` |
| `TempName` | `string` |
| `ImgFileName` | `string` |
| `OutputTemplateName` | `string` |

---

## POI 類結點

### 1. BuildPOINode

- **完整型別**: `FlowEngineLib.BuildPOINode`
- **配置器**: `POINodeConfigurators.cs`
- **實現檔案**: `BuildPOINode.cs`
- **基類**: `CVBaseServerNode`

**配置面板屬性** (NodeConfigurator)

| 屬性名 | 型別 | 說明 |
|--------|------|------|
| `DeviceCode` | DeviceCode | 裝置編碼 |
| `ImgFileName` | ImagePath | 圖片檔案路徑 |
| `TemplateName` | TemplateRef | 布點模板 |
| `RePOITemplateName` | TemplateRef | RePOI |
| `LayoutROITemplate` | TemplateRef | 布點ROI |
| `SavePOITempName` | TemplateRef | SavePOI |

**類級別屬性** (Node Implementation)

| 屬性名 | C# 型別 |
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

- **完整型別**: `FlowEngineLib.Node.POI.POIAnalysisNode`
- **配置器**: `POINodeConfigurators.cs`
- **實現檔案**: `Node\POI\POIAnalysisNode.cs`
- **基類**: `CVBaseServerNode`

**配置面板屬性** (NodeConfigurator)

| 屬性名 | 型別 | 說明 |
|--------|------|------|
| `DeviceCode` | DeviceCode | 裝置編碼 |
| `TempName` | TemplateJsonRef | PoiAnalysis |

**類級別屬性** (Node Implementation)

| 屬性名 | C# 型別 |
|--------|---------|
| `TempName` | `string` |

---

### 3. POIReviseNode

- **完整型別**: `FlowEngineLib.Node.POI.POIReviseNode`
- **配置器**: `POINodeConfigurators.cs`
- **實現檔案**: `Node\POI\POIReviseNode.cs`
- **基類**: `CVBaseServerNodeHub`

**配置面板屬性** (NodeConfigurator)

| 屬性名 | 型別 | 說明 |
|--------|------|------|
| `DeviceCode` | DeviceCode | 裝置編碼 |
| `TemplateName` | TemplateRef | POI修正標定 |

**類級別屬性** (Node Implementation)

| 屬性名 | C# 型別 |
|--------|---------|
| `TemplateName` | `string` |
| `POIPointName` | `string` |
| `IsSelfResultRevise` | `bool` |

---

### 4. RealPOINode

- **完整型別**: `FlowEngineLib.Node.POI.RealPOINode`
- **配置器**: `POINodeConfigurators.cs`
- **實現檔案**: `Node\POI\RealPOINode.cs`
- **基類**: `CVBaseServerNodeHub`

**配置面板屬性** (NodeConfigurator)

| 屬性名 | 型別 | 說明 |
|--------|------|------|
| `DeviceCode` | DeviceCode | 裝置編碼 |
| `FilterTemplateName` | TemplateRef | POI過濾 |
| `ReviseTemplateName` | TemplateRef | POI修正 |
| `OutputTemplateName` | TemplateRef | 檔案輸出模板 |

**類級別屬性** (Node Implementation)

| 屬性名 | C# 型別 |
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

- **完整型別**: `FlowEngineLib.POINode`
- **配置器**: `POINodeConfigurators.cs`
- **實現檔案**: `POINode.cs`
- **基類**: `CVBaseServerNode`

**配置面板屬性** (NodeConfigurator)

| 屬性名 | 型別 | 說明 |
|--------|------|------|
| `DeviceCode` | DeviceCode | 裝置編碼 |
| `ImgFileName` | ImagePath | 圖片檔案路徑 |
| `TempName` | TemplateRef | POI模板 |
| `FilterTemplateName` | TemplateRef | POI過濾 |
| `ReviseTemplateName` | TemplateRef | POI修正 |
| `OutputTemplateName` | TemplateRef | 檔案輸出模板 |

**類級別屬性** (Node Implementation)

| 屬性名 | C# 型別 |
|--------|---------|
| `TempName` | `string` |
| `FilterTemplateName` | `string` |
| `ReviseTemplateName` | `string` |
| `OutputTemplateName` | `string` |
| `ImgFileName` | `string` |
| `IsCCTWave` | `bool` |
| `IsSubPixel` | `bool` |

---

## PG 類結點

### 1. PGNode

- **完整型別**: `FlowEngineLib.Node.PG.PGNode`
- **配置器**: `DeviceNodeConfigurators.cs`
- **實現檔案**: `Node\PG\PGNode.cs`
- **基類**: `CVBaseServerNode`

**配置面板屬性** (NodeConfigurator)

| 屬性名 | 型別 | 說明 |
|--------|------|------|
| `DeviceCode` | DeviceCode | 裝置編碼 |

**類級別屬性** (Node Implementation)

| 屬性名 | C# 型別 |
|--------|---------|
| `PGCmd` | `PGCommCmdType` |
| `IndexFrame` | `int` |

---

## SMU 類結點

### 1. SMUFromCSVNode

- **完整型別**: `FlowEngineLib.SMUFromCSVNode`
- **配置器**: `DeviceNodeConfigurators.cs`
- **實現檔案**: `SMUFromCSVNode.cs`
- **基類**: `SMUBaseNode`

**配置面板屬性** (NodeConfigurator)

| 屬性名 | 型別 | 說明 |
|--------|------|------|
| `DeviceCode` | DeviceCode | 裝置編碼 |
| `CsvFileName` | ImagePath | 圖片檔案路徑 |

**類級別屬性** (Node Implementation)

| 屬性名 | C# 型別 |
|--------|---------|
| `Source` | `SourceType` |
| `Channel` | `SMUChannelType` |
| `CsvFileName` | `string` |
| `IsAutoRng` | `bool` |
| `SrcRng` | `double` |
| `LmtRng` | `double` |

---

### 2. SMUModelNode

- **完整型別**: `FlowEngineLib.SMUModelNode`
- **配置器**: `DeviceNodeConfigurators.cs`
- **實現檔案**: `SMUModelNode.cs`
- **基類**: `SMUBaseNode`

**配置面板屬性** (NodeConfigurator)

| 屬性名 | 型別 | 說明 |
|--------|------|------|
| `DeviceCode` | DeviceCode | 裝置編碼 |
| `ModelName` | TemplateRef | SMUParam設定 |

**類級別屬性** (Node Implementation)

| 屬性名 | C# 型別 |
|--------|---------|
| `ModelName` | `string` |

---

### 3. SMUNode

- **完整型別**: `FlowEngineLib.SMUNode`
- **配置器**: `DeviceNodeConfigurators.cs`
- **實現檔案**: `SMUNode.cs`
- **基類**: `SMUBaseNode`

**配置面板屬性** (NodeConfigurator)

| 屬性名 | 型別 | 說明 |
|--------|------|------|
| `DeviceCode` | DeviceCode | 裝置編碼 |

**類級別屬性** (Node Implementation)

| 屬性名 | C# 型別 |
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

## Sensor 類結點

### 1. CommonSensorNode

- **完整型別**: `FlowEngineLib.CommonSensorNode`
- **配置器**: `DeviceNodeConfigurators.cs`
- **實現檔案**: `CommonSensorNode.cs`
- **基類**: `CVBaseServerNode`

**配置面板屬性** (NodeConfigurator)

| 屬性名 | 型別 | 說明 |
|--------|------|------|
| `DeviceCode` | DeviceCode | 裝置編碼 |
| `TempName` | SensorTemplateRef | 感測器模板 |

**類級別屬性** (Node Implementation)

| 屬性名 | C# 型別 |
|--------|---------|
| `TempName` | `string` |
| `CmdType` | `CommCmdType` |
| `CmdSend` | `string` |
| `CmdReceive` | `string` |

---

### 2. TempCommonSensorNode

- **完整型別**: `FlowEngineLib.TempCommonSensorNode`
- **配置器**: `DeviceNodeConfigurators.cs`
- **實現檔案**: `TempCommonSensorNode.cs`
- **基類**: `CVBaseServerNode`

**配置面板屬性** (NodeConfigurator)

| 屬性名 | 型別 | 說明 |
|--------|------|------|
| `DeviceCode` | DeviceCode | 裝置編碼 |
| `TempName` | SensorTemplateRef | 感測器模板 |

**類級別屬性** (Node Implementation)

| 屬性名 | C# 型別 |
|--------|---------|
| `TempName` | `string` |

---

## FW 類結點

### 1. FWNode

- **完整型別**: `FlowEngineLib.FWNode`
- **配置器**: `DeviceNodeConfigurators.cs`
- **實現檔案**: `FWNode.cs`
- **基類**: `CVBaseServerNode`

**配置面板屬性** (NodeConfigurator)

| 屬性名 | 型別 | 說明 |
|--------|------|------|
| `DeviceCode` | DeviceCode | 裝置編碼 |

**類級別屬性** (Node Implementation)

| 屬性名 | C# 型別 |
|--------|---------|
| `Port` | `int` |
| `ModelType` | `FWModelType` |

---

## Spectrum 類結點

### 1. SpectrumEQENode

- **完整型別**: `SpectrumEQENode`
- **配置器**: `SpectrumNodeConfigurators.cs`

---

### 2. SpectrumLoopNode

- **完整型別**: `SpectrumLoopNode`
- **配置器**: `SpectrumNodeConfigurators.cs`

---

### 3. SpectrumNode

- **完整型別**: `SpectrumNode`
- **配置器**: `SpectrumNodeConfigurators.cs`

---
