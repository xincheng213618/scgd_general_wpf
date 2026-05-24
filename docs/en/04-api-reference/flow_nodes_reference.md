# Flow Engine Node Reference Documentation

> Auto-generated on 2026-05-22, based on the following source directories:
> - Node Configuration: `Engine\ColorVision.Engine\Templates\Flow\NodeConfigurator\`
> - Node Implementation: `Engine\FlowEngineLib\`

## Overview

A total of **42** configured nodes, grouped by type as follows:

| Type | Count |
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

## Algorithm Nodes

### 1. AlgorithmARVRNode

- **Full Type**: `FlowEngineLib.Algorithm.AlgorithmARVRNode`
- **Configurator**: `AlgorithmNodeConfigurators.cs`
- **Implementation File**: `Algorithm\AlgorithmARVRNode.cs`
- **Base Class**: `CVBaseServerNode`

**Configuration Panel Properties** (NodeConfigurator)

| Property Name | Type | Description |
|--------|------|------|
| `DeviceCode` | DeviceCode | Device Code |
| `ImgFileName` | ImagePath | Image File Path |
| `TempName` | TemplateRef | MTF |
| `POITempName` | TemplateRef | POI |

**Class-Level Properties** (Node Implementation)

| Property Name | C# Type |
|--------|---------|
| `Algorithm` | `AlgorithmARVRType` |
| `TempName` | `string` |
| `POITempName` | `string` |
| `ImgFileName` | `string` |
| `Color` | `CVOLED_COLOR` |
| `BufferLen` | `int` |

---

### 2. AlgorithmNode

- **Full Type**: `FlowEngineLib.Algorithm.AlgorithmNode`
- **Configurator**: `AlgorithmNodeConfigurators.cs`
- **Implementation File**: `Algorithm\AlgorithmNode.cs`
- **Base Class**: `CVBaseServerNode`

**Configuration Panel Properties** (NodeConfigurator)

| Property Name | Type | Description |
|--------|------|------|
| `DeviceCode` | DeviceCode | Device Code |
| `ImgFileName` | ImagePath | Image File Path |
| `POITempName` | TemplateRef | POI |
| `TempName` | TemplateRef | MTF |

**Class-Level Properties** (Node Implementation)

| Property Name | C# Type |
|--------|---------|
| `Algorithm` | `AlgorithmType` |
| `TempName` | `string` |
| `POITempName` | `string` |
| `ImgFileName` | `string` |
| `Color` | `CVOLED_COLOR` |
| `BufferLen` | `int` |

---

### 3. CalibrationNode

- **Full Type**: `FlowEngineLib.Algorithm.CalibrationNode`
- **Configurator**: `DeviceNodeConfigurators.cs`
- **Implementation File**: `Algorithm\CalibrationNode.cs`
- **Base Class**: `CVBaseServerNode`

**Configuration Panel Properties** (NodeConfigurator)

| Property Name | Type | Description |
|--------|------|------|
| `DeviceCode` | DeviceCode | Device Code |
| `ImgFileName` | ImagePath | Image File Path |
| `TempName` | TemplateRef | Calibration |

**Class-Level Properties** (Node Implementation)

| Property Name | C# Type |
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

- **Full Type**: `FlowEngineLib.Node.Algorithm.AlgComplianceMathNode`
- **Configurator**: `AlgorithmNodeConfigurators.cs`
- **Implementation File**: `Node\Algorithm\AlgComplianceMathNode.cs`
- **Base Class**: `CVBaseServerNode`

**Configuration Panel Properties** (NodeConfigurator)

| Property Name | Type | Description |
|--------|------|------|
| `DeviceCode` | DeviceCode | Device Code |
| `TempName` | TemplateRef | JND |

**Class-Level Properties** (Node Implementation)

| Property Name | C# Type |
|--------|---------|
| `TempName` | `string` |
| `ComplianceMath` | `ComplianceMathType` |
| `IsBreak` | `bool` |

---

### 5. AlgDataLoadNode

- **Full Type**: `FlowEngineLib.Node.Algorithm.AlgDataLoadNode`
- **Configurator**: `AlgorithmNodeConfigurators.cs`
- **Implementation File**: `Node\Algorithm\AlgDataLoadNode.cs`
- **Base Class**: `CVBaseServerNode`

**Configuration Panel Properties** (NodeConfigurator)

| Property Name | Type | Description |
|--------|------|------|
| `DeviceCode` | DeviceCode | Device Code |
| `TempName` | TemplateRef | Template |

**Class-Level Properties** (Node Implementation)

| Property Name | C# Type |
|--------|---------|
| `TempName` | `string` |

---

### 6. AlgorithmBlackMuraNode

- **Full Type**: `FlowEngineLib.Node.Algorithm.AlgorithmBlackMuraNode`
- **Configurator**: `AlgorithmNodeConfigurators.cs`
- **Implementation File**: `Node\Algorithm\AlgorithmBlackMuraNode.cs`
- **Base Class**: `CVBaseServerNode`

**Configuration Panel Properties** (NodeConfigurator)

| Property Name | Type | Description |
|--------|------|------|
| `DeviceCode` | DeviceCode | Device Code |
| `ImgFileName` | ImagePath | Image File Path |
| `TempName` | TemplateJsonRef | BlackMura |

**Class-Level Properties** (Node Implementation)

| Property Name | C# Type |
|--------|---------|
| `TempName` | `string` |
| `ImgFileName` | `string` |
| `OIndex` | `string` |
| `SavePOITempName` | `string` |

---

### 7. AlgorithmCaliNode

- **Full Type**: `FlowEngineLib.Node.Algorithm.AlgorithmCaliNode`
- **Configurator**: `AlgorithmNodeConfigurators.cs`
- **Implementation File**: `Node\Algorithm\AlgorithmCaliNode.cs`
- **Base Class**: `CVBaseServerNode`

**Configuration Panel Properties** (NodeConfigurator)

| Property Name | Type | Description |
|--------|------|------|
| `DeviceCode` | DeviceCode | Device Code |
| `ImgFileName` | ImagePath | Image File Path |
| `TempName` | TemplateJsonRef | Color Difference |

**Class-Level Properties** (Node Implementation)

| Property Name | C# Type |
|--------|---------|
| `TempName` | `string` |
| `ImgFileName` | `string` |
| `OutputFileName` | `string` |

---

### 8. AlgorithmFindLEDNode

- **Full Type**: `FlowEngineLib.Node.Algorithm.AlgorithmFindLEDNode`
- **Configurator**: `AlgorithmNodeConfigurators.cs`
- **Implementation File**: `Node\Algorithm\AlgorithmFindLEDNode.cs`
- **Base Class**: `CVBaseServerNode`

**Configuration Panel Properties** (NodeConfigurator)

| Property Name | Type | Description |
|--------|------|------|
| `DeviceCode` | DeviceCode | Device Code |
| `ImgFileName` | ImagePath | Image File Path |
| `TempName` | TemplateRef | Pixel-Level LED Detection |

**Class-Level Properties** (Node Implementation)

| Property Name | C# Type |
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

- **Full Type**: `FlowEngineLib.Node.Algorithm.AlgorithmFindLightAreaNode`
- **Configurator**: `AlgorithmNodeConfigurators.cs`
- **Implementation File**: `Node\Algorithm\AlgorithmFindLightAreaNode.cs`
- **Base Class**: `CVBaseServerNode`

**Configuration Panel Properties** (NodeConfigurator)

| Property Name | Type | Description |
|--------|------|------|
| `DeviceCode` | DeviceCode | Device Code |
| `ImgFileName` | ImagePath | Image File Path |
| `TempName` | TemplateRef | Light Area Localization |
| `SavePOITempName` | TemplateRef | Save POI |

**Class-Level Properties** (Node Implementation)

| Property Name | C# Type |
|--------|---------|
| `TempName` | `string` |
| `ImgFileName` | `string` |
| `SavePOITempName` | `string` |
| `BufferLen` | `int` |
| `OIndex` | `string` |

---

### 10. AlgorithmGhostV2Node

- **Full Type**: `FlowEngineLib.Node.Algorithm.AlgorithmGhostV2Node`
- **Configurator**: `AlgorithmNodeConfigurators.cs`
- **Implementation File**: `Node\Algorithm\AlgorithmGhostV2Node.cs`
- **Base Class**: `CVBaseServerNodeHub`

**Configuration Panel Properties** (NodeConfigurator)

| Property Name | Type | Description |
|--------|------|------|
| `DeviceCode` | DeviceCode | Device Code |
| `ImgFileName` | ImagePath | Image File Path |
| `TempName` | TemplateRef | Ghost |

**Class-Level Properties** (Node Implementation)

| Property Name | C# Type |
|--------|---------|
| `TempName` | `string` |
| `ImgFileName` | `string` |
| `BufferLen` | `int` |

---

### 11. AlgorithmImageROINode

- **Full Type**: `FlowEngineLib.Node.Algorithm.AlgorithmImageROINode`
- **Configurator**: `AlgorithmNodeConfigurators.cs`
- **Implementation File**: `Node\Algorithm\AlgorithmImageROINode.cs`
- **Base Class**: `CVBaseServerNode`

**Configuration Panel Properties** (NodeConfigurator)

| Property Name | Type | Description |
|--------|------|------|
| `DeviceCode` | DeviceCode | Device Code |
| `ImgFileName` | ImagePath | Image File Path |
| `TempName` | TemplateJsonRef | Template Name |

**Class-Level Properties** (Node Implementation)

| Property Name | C# Type |
|--------|---------|
| `TempName` | `string` |
| `ImgFileName` | `string` |
| `OutputFileName` | `string` |

---

### 12. AlgorithmKBNode

- **Full Type**: `FlowEngineLib.Node.Algorithm.AlgorithmKBNode`
- **Configurator**: `AlgorithmNodeConfigurators.cs`
- **Implementation File**: `Node\Algorithm\AlgorithmKBNode.cs`
- **Base Class**: `CVBaseServerNode`

**Configuration Panel Properties** (NodeConfigurator)

| Property Name | Type | Description |
|--------|------|------|
| `DeviceCode` | DeviceCode | Device Code |
| `ImgFileName` | ImagePath | Image File Path |
| `TempName` | TemplateKBRef | KB |

**Class-Level Properties** (Node Implementation)

| Property Name | C# Type |
|--------|---------|
| `TempName` | `string` |
| `ImgFileName` | `string` |

---

### 13. AlgorithmKBOutputNode

- **Full Type**: `FlowEngineLib.Node.Algorithm.AlgorithmKBOutputNode`
- **Configurator**: `AlgorithmNodeConfigurators.cs`
- **Implementation File**: `Node\Algorithm\AlgorithmKBOutputNode.cs`
- **Base Class**: `CVBaseServerNode`

**Configuration Panel Properties** (NodeConfigurator)

| Property Name | Type | Description |
|--------|------|------|
| `DeviceCode` | DeviceCode | Device Code |
| `TempName` | TemplateKBRef | KB |

**Class-Level Properties** (Node Implementation)

| Property Name | C# Type |
|--------|---------|
| `TempName` | `string` |

---

### 14. AlgorithmOLEDNode

- **Full Type**: `FlowEngineLib.Node.Algorithm.AlgorithmOLEDNode`
- **Configurator**: `OLEDNodeConfigurators.cs`
- **Implementation File**: `Node\Algorithm\AlgorithmOLEDNode.cs`
- **Base Class**: `CVBaseServerNode`

**Configuration Panel Properties** (NodeConfigurator)

| Property Name | Type | Description |
|--------|------|------|
| `DeviceCode` | DeviceCode | Device Code |
| `ImgFileName` | ImagePath | Image File Path |
| `TempName` | TemplateJsonRef | Sub-Pixel |

**Class-Level Properties** (Node Implementation)

| Property Name | C# Type |
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

- **Full Type**: `FlowEngineLib.Node.Algorithm.AlgorithmOLED_AOINode`
- **Configurator**: `OLEDNodeConfigurators.cs`
- **Implementation File**: `Node\Algorithm\AlgorithmOLED_AOINode.cs`
- **Base Class**: `CVBaseServerNode`

**Configuration Panel Properties** (NodeConfigurator)

| Property Name | Type | Description |
|--------|------|------|
| `DeviceCode` | DeviceCode | Device Code |
| `ImgFileName` | ImagePath | Image File Path |
| `TempName` | TemplateJsonRef | AOI |

**Class-Level Properties** (Node Implementation)

| Property Name | C# Type |
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

- **Full Type**: `FlowEngineLib.Node.OLED.Algorithm2InNode`
- **Configurator**: `OLEDNodeConfigurators.cs`
- **Implementation File**: `Node\OLED\Algorithm2InNode.cs`
- **Base Class**: `CVBaseServerNodeHub`

**Configuration Panel Properties** (NodeConfigurator)

| Property Name | Type | Description |
|--------|------|------|
| `DeviceCode` | DeviceCode | Device Code |
| `TempName` | TemplateRef | MTF |

**Class-Level Properties** (Node Implementation)

| Property Name | C# Type |
|--------|---------|
| `TempName` | `string` |
| `Algorithm` | `Algorithm2Type` |
| `BufferLen` | `int` |
| `IsAdd` | `bool` |

---

### 17. AlgorithmCompoundImgNode

- **Full Type**: `FlowEngineLib.Node.OLED.AlgorithmCompoundImgNode`
- **Configurator**: `OLEDNodeConfigurators.cs`
- **Implementation File**: `Node\OLED\AlgorithmCompoundImgNode.cs`
- **Base Class**: `CVBaseServerNodeHub`

**Configuration Panel Properties** (NodeConfigurator)

| Property Name | Type | Description |
|--------|------|------|
| `DeviceCode` | DeviceCode | Device Code |
| `TempName` | TemplateJsonRef | Parameter Template |

**Class-Level Properties** (Node Implementation)

| Property Name | C# Type |
|--------|---------|
| `TempName` | `string` |
| `OutputFileName` | `string` |
| `BufferLen` | `int` |

---

## Camera Nodes

### 1. AOILocAndRegPixelsCameraNode

- **Full Type**: `FlowEngineLib.AOILocAndRegPixelsCameraNode`
- **Configurator**: `CameraNodeConfigurators.cs`
- **Implementation File**: `AOILocAndRegPixelsCameraNode.cs`
- **Base Class**: `CVBaseServerNode`

**Configuration Panel Properties** (NodeConfigurator)

| Property Name | Type | Description |
|--------|------|------|
| `DeviceCode` | DeviceCode | Device Code |
| `AutoExpTempName` | TemplateRef | Exposure Template |
| `CaliTempName` | TemplateRef | Calibration |
| `AlgTempName` | TemplateJsonRef | AOI |

**Class-Level Properties** (Node Implementation)

| Property Name | C# Type |
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

- **Full Type**: `FlowEngineLib.AOILocatePixelsCameraNode`
- **Configurator**: `CameraNodeConfigurators.cs`
- **Implementation File**: `AOILocatePixelsCameraNode.cs`
- **Base Class**: `CVBaseServerNode`

**Configuration Panel Properties** (NodeConfigurator)

| Property Name | Type | Description |
|--------|------|------|
| `DeviceCode` | DeviceCode | Device Code |
| `AutoExpTempName` | TemplateRef | Exposure Template |
| `CaliTempName` | TemplateRef | Calibration |
| `AlgTempName` | TemplateJsonRef | Sub-Pixel LED Detection |

**Class-Level Properties** (Node Implementation)

| Property Name | C# Type |
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

- **Full Type**: `FlowEngineLib.CVCameraNode`
- **Configurator**: `CameraNodeConfigurators.cs`
- **Implementation File**: `CVCameraNode.cs`
- **Base Class**: `CVBaseServerNode`

**Configuration Panel Properties** (NodeConfigurator)

| Property Name | Type | Description |
|--------|------|------|
| `DeviceCode` | DeviceCode | Device Code |
| `CalibTempName` | TemplateRef | Calibration |
| `POITempName` | TemplateRef | POI Template |
| `POIFilterTempName` | TemplateRef | POI Filter |
| `POIReviseTempName` | TemplateRef | POI Revision |

**Class-Level Properties** (Node Implementation)

| Property Name | C# Type |
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

- **Full Type**: `FlowEngineLib.CamMotorNode`
- **Configurator**: `CameraNodeConfigurators.cs`
- **Implementation File**: `CamMotorNode.cs`
- **Base Class**: `CVBaseServerNode`

**Configuration Panel Properties** (NodeConfigurator)

| Property Name | Type | Description |
|--------|------|------|
| `DeviceCode` | DeviceCode | Device Code |
| `AutoFocusTemp` | TemplateRef | Camera Template |

**Class-Level Properties** (Node Implementation)

| Property Name | C# Type |
|--------|---------|
| `RunType` | `CamMotorRunType` |
| `IsAbs` | `bool` |
| `Position` | `int` |
| `Aperture` | `float` |
| `AutoFocusTemp` | `string` |

---

### 5. LVCameraNode

- **Full Type**: `FlowEngineLib.LVCameraNode`
- **Configurator**: `CameraNodeConfigurators.cs`
- **Implementation File**: `LVCameraNode.cs`
- **Base Class**: `BaseCameraNode`

**Configuration Panel Properties** (NodeConfigurator)

| Property Name | Type | Description |
|--------|------|------|
| `DeviceCode` | DeviceCode | Device Code |
| `CaliTempName` | TemplateRef | Calibration |
| `POITempName` | TemplateRef | POI Template |
| `POIFilterTempName` | TemplateRef | POI Filter |
| `POIReviseTempName` | TemplateRef | POI Revision |

---

### 6. CVAOI2CameraNode

- **Full Type**: `FlowEngineLib.Node.Camera.CVAOI2CameraNode`
- **Configurator**: `CameraNodeConfigurators.cs`
- **Implementation File**: `Node\Camera\CVAOI2CameraNode.cs`
- **Base Class**: `CVBaseServerNodeHub`

**Configuration Panel Properties** (NodeConfigurator)

| Property Name | Type | Description |
|--------|------|------|
| `DeviceCode` | DeviceCode | Device Code |
| `CamTempName` | TemplateRef | Camera Template |
| `TempName` | TemplateRef | Exposure Template |
| `CalibTempName` | TemplateRef | Calibration |
| `AlgTempName` | TemplateJsonRef | AOI |

**Class-Level Properties** (Node Implementation)

| Property Name | C# Type |
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

- **Full Type**: `FlowEngineLib.Node.Camera.CVAOICameraNode`
- **Configurator**: `CameraNodeConfigurators.cs`
- **Implementation File**: `Node\Camera\CVAOICameraNode.cs`
- **Base Class**: `CVBaseServerNode`

**Configuration Panel Properties** (NodeConfigurator)

| Property Name | Type | Description |
|--------|------|------|
| `DeviceCode` | DeviceCode | Device Code |
| `CamTempName` | TemplateRef | Camera Template |
| `TempName` | TemplateRef | Exposure Template |
| `CalibTempName` | TemplateRef | Calibration |
| `AlgTempName` | TemplateJsonRef | Sub-Pixel LED Detection |

**Class-Level Properties** (Node Implementation)

| Property Name | C# Type |
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

- **Full Type**: `FlowEngineLib.Node.Camera.CommCameraNode`
- **Configurator**: `CameraNodeConfigurators.cs`
- **Implementation File**: `Node\Camera\CommCameraNode.cs`
- **Base Class**: `CVBaseServerNode`

**Configuration Panel Properties** (NodeConfigurator)

| Property Name | Type | Description |
|--------|------|------|
| `DeviceCode` | DeviceCode | Device Code |
| `CalibTempName` | TemplateRef | Calibration |
| `CamTempName` | TemplateRef | Camera Template |
| `TempName` | TemplateRef | Exposure Template |
| `POITempName` | TemplateRef | POI Template |
| `POIFilterTempName` | TemplateRef | POI Filter |
| `POIReviseTempName` | TemplateRef | POI Revision |

**Class-Level Properties** (Node Implementation)

| Property Name | C# Type |
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

## OLED Nodes

### 1. OLEDImageCroppingNode

- **Full Type**: `FlowEngineLib.Node.OLED.OLEDImageCroppingNode`
- **Configurator**: `OLEDNodeConfigurators.cs`
- **Implementation File**: `Node\OLED\OLEDImageCroppingNode.cs`
- **Base Class**: `CVBaseServerNodeHub`

**Configuration Panel Properties** (NodeConfigurator)

| Property Name | Type | Description |
|--------|------|------|
| `DeviceCode` | DeviceCode | Device Code |
| `TempName` | TemplateRef | Parameter Template |

**Class-Level Properties** (Node Implementation)

| Property Name | C# Type |
|--------|---------|
| `TempName` | `string` |
| `ImgFileName` | `string` |

---

### 2. OLEDRebuildPixelsNode

- **Full Type**: `FlowEngineLib.Node.OLED.OLEDRebuildPixelsNode`
- **Configurator**: `OLEDNodeConfigurators.cs`
- **Implementation File**: `Node\OLED\OLEDRebuildPixelsNode.cs`
- **Base Class**: `CVBaseServerNodeHub`

**Configuration Panel Properties** (NodeConfigurator)

| Property Name | Type | Description |
|--------|------|------|
| `DeviceCode` | DeviceCode | Device Code |
| `ImgFileName` | ImagePath | Image File Path |
| `OutputTemplateName` | TemplateRef | PoiOutPut |
| `TempName` | TemplateJsonRef | Sub-Pixel LED Detection |

**Class-Level Properties** (Node Implementation)

| Property Name | C# Type |
|--------|---------|
| `Channel` | `CVOLED_Channel` |
| `TempName` | `string` |
| `ImgFileName` | `string` |
| `OutputTemplateName` | `string` |

---

## POI Nodes

### 1. BuildPOINode

- **Full Type**: `FlowEngineLib.BuildPOINode`
- **Configurator**: `POINodeConfigurators.cs`
- **Implementation File**: `BuildPOINode.cs`
- **Base Class**: `CVBaseServerNode`

**Configuration Panel Properties** (NodeConfigurator)

| Property Name | Type | Description |
|--------|------|------|
| `DeviceCode` | DeviceCode | Device Code |
| `ImgFileName` | ImagePath | Image File Path |
| `TemplateName` | TemplateRef | POI Layout Template |
| `RePOITemplateName` | TemplateRef | RePOI |
| `LayoutROITemplate` | TemplateRef | Layout ROI |
| `SavePOITempName` | TemplateRef | SavePOI |

**Class-Level Properties** (Node Implementation)

| Property Name | C# Type |
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

- **Full Type**: `FlowEngineLib.Node.POI.POIAnalysisNode`
- **Configurator**: `POINodeConfigurators.cs`
- **Implementation File**: `Node\POI\POIAnalysisNode.cs`
- **Base Class**: `CVBaseServerNode`

**Configuration Panel Properties** (NodeConfigurator)

| Property Name | Type | Description |
|--------|------|------|
| `DeviceCode` | DeviceCode | Device Code |
| `TempName` | TemplateJsonRef | PoiAnalysis |

**Class-Level Properties** (Node Implementation)

| Property Name | C# Type |
|--------|---------|
| `TempName` | `string` |

---

### 3. POIReviseNode

- **Full Type**: `FlowEngineLib.Node.POI.POIReviseNode`
- **Configurator**: `POINodeConfigurators.cs`
- **Implementation File**: `Node\POI\POIReviseNode.cs`
- **Base Class**: `CVBaseServerNodeHub`

**Configuration Panel Properties** (NodeConfigurator)

| Property Name | Type | Description |
|--------|------|------|
| `DeviceCode` | DeviceCode | Device Code |
| `TemplateName` | TemplateRef | POI Revision Calibration |

**Class-Level Properties** (Node Implementation)

| Property Name | C# Type |
|--------|---------|
| `TemplateName` | `string` |
| `POIPointName` | `string` |
| `IsSelfResultRevise` | `bool` |

---

### 4. RealPOINode

- **Full Type**: `FlowEngineLib.Node.POI.RealPOINode`
- **Configurator**: `POINodeConfigurators.cs`
- **Implementation File**: `Node\POI\RealPOINode.cs`
- **Base Class**: `CVBaseServerNodeHub`

**Configuration Panel Properties** (NodeConfigurator)

| Property Name | Type | Description |
|--------|------|------|
| `DeviceCode` | DeviceCode | Device Code |
| `FilterTemplateName` | TemplateRef | POI Filter |
| `ReviseTemplateName` | TemplateRef | POI Revision |
| `OutputTemplateName` | TemplateRef | File Output Template |

**Class-Level Properties** (Node Implementation)

| Property Name | C# Type |
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

- **Full Type**: `FlowEngineLib.POINode`
- **Configurator**: `POINodeConfigurators.cs`
- **Implementation File**: `POINode.cs`
- **Base Class**: `CVBaseServerNode`

**Configuration Panel Properties** (NodeConfigurator)

| Property Name | Type | Description |
|--------|------|------|
| `DeviceCode` | DeviceCode | Device Code |
| `ImgFileName` | ImagePath | Image File Path |
| `TempName` | TemplateRef | POI Template |
| `FilterTemplateName` | TemplateRef | POI Filter |
| `ReviseTemplateName` | TemplateRef | POI Revision |
| `OutputTemplateName` | TemplateRef | File Output Template |

**Class-Level Properties** (Node Implementation)

| Property Name | C# Type |
|--------|---------|
| `TempName` | `string` |
| `FilterTemplateName` | `string` |
| `ReviseTemplateName` | `string` |
| `OutputTemplateName` | `string` |
| `ImgFileName` | `string` |
| `IsCCTWave` | `bool` |
| `IsSubPixel` | `bool` |

---

## PG Nodes

### 1. PGNode

- **Full Type**: `FlowEngineLib.Node.PG.PGNode`
- **Configurator**: `DeviceNodeConfigurators.cs`
- **Implementation File**: `Node\PG\PGNode.cs`
- **Base Class**: `CVBaseServerNode`

**Configuration Panel Properties** (NodeConfigurator)

| Property Name | Type | Description |
|--------|------|------|
| `DeviceCode` | DeviceCode | Device Code |

**Class-Level Properties** (Node Implementation)

| Property Name | C# Type |
|--------|---------|
| `PGCmd` | `PGCommCmdType` |
| `IndexFrame` | `int` |

---

## SMU Nodes

### 1. SMUFromCSVNode

- **Full Type**: `FlowEngineLib.SMUFromCSVNode`
- **Configurator**: `DeviceNodeConfigurators.cs`
- **Implementation File**: `SMUFromCSVNode.cs`
- **Base Class**: `SMUBaseNode`

**Configuration Panel Properties** (NodeConfigurator)

| Property Name | Type | Description |
|--------|------|------|
| `DeviceCode` | DeviceCode | Device Code |
| `CsvFileName` | ImagePath | Image File Path |

**Class-Level Properties** (Node Implementation)

| Property Name | C# Type |
|--------|---------|
| `Source` | `SourceType` |
| `Channel` | `SMUChannelType` |
| `CsvFileName` | `string` |
| `IsAutoRng` | `bool` |
| `SrcRng` | `double` |
| `LmtRng` | `double` |

---

### 2. SMUModelNode

- **Full Type**: `FlowEngineLib.SMUModelNode`
- **Configurator**: `DeviceNodeConfigurators.cs`
- **Implementation File**: `SMUModelNode.cs`
- **Base Class**: `SMUBaseNode`

**Configuration Panel Properties** (NodeConfigurator)

| Property Name | Type | Description |
|--------|------|------|
| `DeviceCode` | DeviceCode | Device Code |
| `ModelName` | TemplateRef | SMUParam Settings |

**Class-Level Properties** (Node Implementation)

| Property Name | C# Type |
|--------|---------|
| `ModelName` | `string` |

---

### 3. SMUNode

- **Full Type**: `FlowEngineLib.SMUNode`
- **Configurator**: `DeviceNodeConfigurators.cs`
- **Implementation File**: `SMUNode.cs`
- **Base Class**: `SMUBaseNode`

**Configuration Panel Properties** (NodeConfigurator)

| Property Name | Type | Description |
|--------|------|------|
| `DeviceCode` | DeviceCode | Device Code |

**Class-Level Properties** (Node Implementation)

| Property Name | C# Type |
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

## Sensor Nodes

### 1. CommonSensorNode

- **Full Type**: `FlowEngineLib.CommonSensorNode`
- **Configurator**: `DeviceNodeConfigurators.cs`
- **Implementation File**: `CommonSensorNode.cs`
- **Base Class**: `CVBaseServerNode`

**Configuration Panel Properties** (NodeConfigurator)

| Property Name | Type | Description |
|--------|------|------|
| `DeviceCode` | DeviceCode | Device Code |
| `TempName` | SensorTemplateRef | Sensor Template |

**Class-Level Properties** (Node Implementation)

| Property Name | C# Type |
|--------|---------|
| `TempName` | `string` |
| `CmdType` | `CommCmdType` |
| `CmdSend` | `string` |
| `CmdReceive` | `string` |

---

### 2. TempCommonSensorNode

- **Full Type**: `FlowEngineLib.TempCommonSensorNode`
- **Configurator**: `DeviceNodeConfigurators.cs`
- **Implementation File**: `TempCommonSensorNode.cs`
- **Base Class**: `CVBaseServerNode`

**Configuration Panel Properties** (NodeConfigurator)

| Property Name | Type | Description |
|--------|------|------|
| `DeviceCode` | DeviceCode | Device Code |
| `TempName` | SensorTemplateRef | Sensor Template |

**Class-Level Properties** (Node Implementation)

| Property Name | C# Type |
|--------|---------|
| `TempName` | `string` |

---

## FW Nodes

### 1. FWNode

- **Full Type**: `FlowEngineLib.FWNode`
- **Configurator**: `DeviceNodeConfigurators.cs`
- **Implementation File**: `FWNode.cs`
- **Base Class**: `CVBaseServerNode`

**Configuration Panel Properties** (NodeConfigurator)

| Property Name | Type | Description |
|--------|------|------|
| `DeviceCode` | DeviceCode | Device Code |

**Class-Level Properties** (Node Implementation)

| Property Name | C# Type |
|--------|---------|
| `Port` | `int` |
| `ModelType` | `FWModelType` |

---

## Spectrum Nodes

### 1. SpectrumEQENode

- **Full Type**: `SpectrumEQENode`
- **Configurator**: `SpectrumNodeConfigurators.cs`

---

### 2. SpectrumLoopNode

- **Full Type**: `SpectrumLoopNode`
- **Configurator**: `SpectrumNodeConfigurators.cs`

---

### 3. SpectrumNode

- **Full Type**: `SpectrumNode`
- **Configurator**: `SpectrumNodeConfigurators.cs`

---
