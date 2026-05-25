# フロー エンジン ノードのリファレンス ドキュメント

> 次のソース コード ディレクトリに基づいて、2026 年 5 月 22 日に自動的に生成されます。
> - ノード構成: `Engine\ColorVision.Engine\Templates\Flow\NodeConfigurator\`
> - ノード実装: `Engine\FlowEngineLib\`

## 概要

合計で **42** の構成済みノードがあり、次のようにタイプ別にグループ化されています。

|タイプ |数量 |
|------|------|
|アルゴリズム | 17 |
|カメラ | 8 |
|有機EL | 2 |
| POI | 5 |
| PG | 1 |
| SMU | 3 |
|センサー | 2 |
| FW | 1 |
|スペクトル | 3 |

## アルゴリズムクラスノード

### 1.アルゴリズムARVRノード

- **完全なタイプ**: `FlowEngineLib.Algorithm.AlgorithmARVRNode`
- **コンフィギュレーター**: `AlgorithmNodeConfigurators.cs`
- **実装ファイル**: `Algorithm\AlgorithmARVRNode.cs`
- **基本クラス**: `CVBaseServerNode`

**構成パネルのプロパティ** (NodeConfigurator)

|属性名 |タイプ |説明 |
|------|------|------|
| `DeviceCode` |デバイスコード |デバイスコード |
| `ImgFileName` |画像パス |画像ファイルのパス |
| `TempName` |テンプレート参照 | MTF |
| `POITempName` |テンプレート参照 | POI |

**クラスレベルのプロパティ** (ノード実装)

|プロパティ名 | C# タイプ |
|--------|--------|
| `Algorithm` | `AlgorithmARVRType` |
| `TempName` | `string` |
| `POITempName` | `string` |
| `ImgFileName` | `string` |
| `Color` | `CVOLED_COLOR` |
| `BufferLen` | `int` |

---

### 2. アルゴリズムノード

- **完全なタイプ**: `FlowEngineLib.Algorithm.AlgorithmNode`
- **コンフィギュレーター**: `AlgorithmNodeConfigurators.cs`
- **実装ファイル**: `Algorithm\AlgorithmNode.cs`
- **基本クラス**: `CVBaseServerNode`

**構成パネルのプロパティ** (NodeConfigurator)

|属性名 |タイプ |説明 |
|------|------|------|
| `DeviceCode` |デバイスコード |デバイスコード |
| `ImgFileName` |画像パス |画像ファイルのパス |
| `POITempName` |テンプレート参照 | POI |
| `TempName` |テンプレート参照 | MTF |

**クラスレベルのプロパティ** (ノード実装)

|プロパティ名 | C# タイプ |
|--------|--------|
| `Algorithm` | `AlgorithmType` |
| `TempName` | `string` |
| `POITempName` | `string` |
| `ImgFileName` | `string` |
| `Color` | `CVOLED_COLOR` |
| `BufferLen` | `int` |

---

### 3. キャリブレーションノード

- **完全なタイプ**: `FlowEngineLib.Algorithm.CalibrationNode`
- **コンフィギュレーター**: `DeviceNodeConfigurators.cs`
- **実装ファイル**: `Algorithm\CalibrationNode.cs`
- **基本クラス**: `CVBaseServerNode`

**構成パネルのプロパティ** (NodeConfigurator)

|属性名 |タイプ |説明 |
|------|------|------|
| `DeviceCode` |デバイスコード |デバイスコード |
| `ImgFileName` |画像パス |画像ファイルのパス |
| `TempName` |テンプレート参照 |訂正 |

**クラスレベルのプロパティ** (ノード実装)

|プロパティ名 | C# タイプ |
|--------|--------|
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

- **完全なタイプ**: `FlowEngineLib.Node.Algorithm.AlgComplianceMathNode`
- **コンフィギュレーター**: `AlgorithmNodeConfigurators.cs`
- **実装ファイル**: `Node\Algorithm\AlgComplianceMathNode.cs`
- **基本クラス**: `CVBaseServerNode`

**構成パネルのプロパティ** (NodeConfigurator)

|属性名 |タイプ |説明 |
|------|------|------|
| `DeviceCode` |デバイスコード |デバイスコード |
| `TempName` |テンプレート参照 | JND |

**クラスレベルのプロパティ** (ノード実装)

|プロパティ名 | C# タイプ |
|--------|--------|
| `TempName` | `string` |
| `ComplianceMath` | `ComplianceMathType` |
| `IsBreak` | `bool` |

---

### 5. AlgDataLoadNode

- **完全なタイプ**: `FlowEngineLib.Node.Algorithm.AlgDataLoadNode`
- **コンフィギュレーター**: `AlgorithmNodeConfigurators.cs`
- **実装ファイル**: `Node\Algorithm\AlgDataLoadNode.cs`
- **基本クラス**: `CVBaseServerNode`

**構成パネルのプロパティ** (NodeConfigurator)

|属性名 |タイプ |説明 |
|------|------|------|
| `DeviceCode` |デバイスコード |デバイスコード |
| `TempName` |テンプレート参照 |テンプレート |

**クラスレベルのプロパティ** (ノード実装)

|プロパティ名 | C# タイプ |
|--------|--------|
| `TempName` | `string` |

---

### 6. アルゴリズムBlackMuraNode

- **完全なタイプ**: `FlowEngineLib.Node.Algorithm.AlgorithmBlackMuraNode`
- **コンフィギュレーター**: `AlgorithmNodeConfigurators.cs`
- **実装ファイル**: `Node\Algorithm\AlgorithmBlackMuraNode.cs`
- **基本クラス**: `CVBaseServerNode`

**構成パネルのプロパティ** (NodeConfigurator)

|属性名 |タイプ |説明 |
|------|------|------|
| `DeviceCode` |デバイスコード |デバイスコード |
| `ImgFileName` |画像パス |画像ファイルのパス |
| `TempName` |テンプレートJsonRef |黒村 |

**クラスレベルのプロパティ** (ノード実装)

|プロパティ名 | C# タイプ |
|--------|--------|
| `TempName` | `string` |
| `ImgFileName` | `string` |
| `OIndex` | `string` |
| `SavePOITempName` | `string` |

---

### 7. アルゴリズムCaliNode

- **フルタイプ**: `FlowEngineLib.Node.Algorithm.AlgorithmCaliNode`
- **コンフィギュレーター**: `AlgorithmNodeConfigurators.cs`
- **実装ファイル**: `Node\Algorithm\AlgorithmCaliNode.cs`
- **基本クラス**: `CVBaseServerNode`

**構成パネルのプロパティ** (NodeConfigurator)

|属性名 |タイプ |説明 |
|------|------|------|
| `DeviceCode` |デバイスコード |デバイスコード |
| `ImgFileName` |画像パス |画像ファイルのパス |
| `TempName` |テンプレートJsonRef |色の違い |

**クラスレベルのプロパティ** (ノード実装)

|プロパティ名 | C# タイプ |
|--------|--------|
| `TempName` | `string` |
| `ImgFileName` | `string` |
| `OutputFileName` | `string` |

---

### 8. アルゴリズムFindLEDNode

- **フルタイプ**: `FlowEngineLib.Node.Algorithm.AlgorithmFindLEDNode`
- **コンフィギュレーター**: `AlgorithmNodeConfigurators.cs`
- **実装ファイル**: `Node\Algorithm\AlgorithmFindLEDNode.cs`
- **基本クラス**: `CVBaseServerNode`

**構成パネルのプロパティ** (NodeConfigurator)

|属性名 |タイプ |説明 |
|------|------|------|
| `DeviceCode` |デバイスコード |デバイスコード |
| `ImgFileName` |画像パス |画像ファイルのパス |
| `TempName` |テンプレート参照 |ピクセルレベルのランプビード検出 |**クラスレベルのプロパティ** (ノード実装)

|プロパティ名 | C# タイプ |
|--------|--------|
| `Color` | `CVOLED_Channel` |
| `TempName` | `string` |
| `FDAType` | `CVOLED_FDAType` |
| `FixedLEDPoint` | `PointFloat[]` |
| `ImgFileName` | `string` |
| `OutputFileName` | `string` |
| `ImgPosResultFile` | `string` |

---

### 9. アルゴリズムFindLightAreaNode

- **フルタイプ**: `FlowEngineLib.Node.Algorithm.AlgorithmFindLightAreaNode`
- **コンフィギュレーター**: `AlgorithmNodeConfigurators.cs`
- **実装ファイル**: `Node\Algorithm\AlgorithmFindLightAreaNode.cs`
- **基本クラス**: `CVBaseServerNode`

**構成パネルのプロパティ** (NodeConfigurator)

|属性名 |タイプ |説明 |
|------|------|------|
| `DeviceCode` |デバイスコード |デバイスコード |
| `ImgFileName` |画像パス |画像ファイルのパス |
| `TempName` |テンプレート参照 |発光エリアの位置決め |
| `SavePOITempName` |テンプレート参照 | POIを保存 |

**クラスレベルのプロパティ** (ノード実装)

|プロパティ名 | C# タイプ |
|--------|--------|
| `TempName` | `string` |
| `ImgFileName` | `string` |
| `SavePOITempName` | `string` |
| `BufferLen` | `int` |
| `OIndex` | `string` |

---

### 10. アルゴリズムGhostV2Node

- **フルタイプ**: `FlowEngineLib.Node.Algorithm.AlgorithmGhostV2Node`
- **コンフィギュレーター**: `AlgorithmNodeConfigurators.cs`
- **実装ファイル**: `Node\Algorithm\AlgorithmGhostV2Node.cs`
- **基本クラス**: `CVBaseServerNodeHub`

**構成パネルのプロパティ** (NodeConfigurator)

|属性名 |タイプ |説明 |
|------|------|------|
| `DeviceCode` |デバイスコード |デバイスコード |
| `ImgFileName` |画像パス |画像ファイルのパス |
| `TempName` |テンプレート参照 |ゴースト |

**クラスレベルのプロパティ** (ノード実装)

|プロパティ名 | C# タイプ |
|--------|--------|
| `TempName` | `string` |
| `ImgFileName` | `string` |
| `BufferLen` | `int` |

---

### 11.アルゴリズム画像ROIノード

- **フルタイプ**: `FlowEngineLib.Node.Algorithm.AlgorithmImageROINode`
- **コンフィギュレーター**: `AlgorithmNodeConfigurators.cs`
- **実装ファイル**: `Node\Algorithm\AlgorithmImageROINode.cs`
- **基本クラス**: `CVBaseServerNode`

**構成パネルのプロパティ** (NodeConfigurator)

|属性名 |タイプ |説明 |
|------|------|------|
| `DeviceCode` |デバイスコード |デバイスコード |
| `ImgFileName` |画像パス |画像ファイルのパス |
| `TempName` |テンプレートJsonRef |テンプレート名 |

**クラスレベルのプロパティ** (ノード実装)

|プロパティ名 | C# タイプ |
|--------|--------|
| `TempName` | `string` |
| `ImgFileName` | `string` |
| `OutputFileName` | `string` |

---

### 12.アルゴリズムKBノード

- **完全なタイプ**: `FlowEngineLib.Node.Algorithm.AlgorithmKBNode`
- **コンフィギュレーター**: `AlgorithmNodeConfigurators.cs`
- **実装ファイル**: `Node\Algorithm\AlgorithmKBNode.cs`
- **基本クラス**: `CVBaseServerNode`

**構成パネルのプロパティ** (NodeConfigurator)

|属性名 |タイプ |説明 |
|------|------|------|
| `DeviceCode` |デバイスコード |デバイスコード |
| `ImgFileName` |画像パス |画像ファイルのパス |
| `TempName` |テンプレートKBRef | KB |

**クラスレベルのプロパティ** (ノード実装)

|プロパティ名 | C# タイプ |
|--------|--------|
| `TempName` | `string` |
| `ImgFileName` | `string` |

---

### 13. アルゴリズムKBOutputNode

- **完全なタイプ**: `FlowEngineLib.Node.Algorithm.AlgorithmKBOutputNode`
- **コンフィギュレーター**: `AlgorithmNodeConfigurators.cs`
- **実装ファイル**: `Node\Algorithm\AlgorithmKBOutputNode.cs`
- **基本クラス**: `CVBaseServerNode`

**構成パネルのプロパティ** (NodeConfigurator)

|属性名 |タイプ |説明 |
|------|------|------|
| `DeviceCode` |デバイスコード |デバイスコード |
| `TempName` |テンプレートKBRef | KB |

**クラスレベルのプロパティ** (ノード実装)

|プロパティ名 | C# タイプ |
|--------|--------|
| `TempName` | `string` |

---

### 14. アルゴリズムOLEDノード

- **完全なタイプ**: `FlowEngineLib.Node.Algorithm.AlgorithmOLEDNode`
- **コンフィギュレーター**: `OLEDNodeConfigurators.cs`
- **実装ファイル**: `Node\Algorithm\AlgorithmOLEDNode.cs`
- **基本クラス**: `CVBaseServerNode`

**構成パネルのプロパティ** (NodeConfigurator)

|属性名 |タイプ |説明 |
|------|------|------|
| `DeviceCode` |デバイスコード |デバイスコード |
| `ImgFileName` |画像パス |画像ファイルのパス |
| `TempName` |テンプレートJsonRef |サブピクセル |

**クラスレベルのプロパティ** (ノード実装)

|プロパティ名 | C# タイプ |
|--------|--------|
| `Algorithm` | `AlgorithmOLEDType` |
| `Color` | `CVOLED_COLOR` |
| `TempName` | `string` |
| `FDAType` | `CVOLED_FDAType` |
| `FixedLEDPoint` | `PointFloat[]` |
| `ImgFileName` | `string` |
| `OutputFileName` | `string` |
| `ImgPosResultFile` | `string` |

---

### 15.アルゴリズムOLED_AOINode

- **フルタイプ**: `FlowEngineLib.Node.Algorithm.AlgorithmOLED_AOINode`
- **コンフィギュレーター**: `OLEDNodeConfigurators.cs`
- **実装ファイル**: `Node\Algorithm\AlgorithmOLED_AOINode.cs`
- **基本クラス**: `CVBaseServerNode`

**構成パネルのプロパティ** (NodeConfigurator)

|属性名 |タイプ |説明 |
|------|------|------|
| `DeviceCode` |デバイスコード |デバイスコード |
| `ImgFileName` |画像パス |画像ファイルのパス |
| `TempName` |テンプレートJsonRef |葵 |

**クラスレベルのプロパティ** (ノード実装)

|プロパティ名 | C# タイプ |
|--------|--------|
| `Algorithm` | `AlgorithmOLED_AOIType` |
| `TempName` | `string` |
| `ImgFileName` | `string` |
| `OutputFileName` | `string` |
| `CustomSN` | `string` |
| `VhLineEnable` | `bool` |
| `PixelDefectEnable` | `bool` |
| `MuraEnable` | `bool` |

---

### 16. アルゴリズム 2InNode

- **完全なタイプ**: `FlowEngineLib.Node.OLED.Algorithm2InNode`
- **コンフィギュレーター**: `OLEDNodeConfigurators.cs`
- **実装ファイル**: `Node\OLED\Algorithm2InNode.cs`
- **基本クラス**: `CVBaseServerNodeHub`

**構成パネルのプロパティ** (NodeConfigurator)|属性名 |タイプ |説明 |
|------|------|------|
| `DeviceCode` |デバイスコード |デバイスコード |
| `TempName` |テンプレート参照 | MTF |

**クラスレベルのプロパティ** (ノード実装)

|プロパティ名 | C# タイプ |
|--------|--------|
| `TempName` | `string` |
| `Algorithm` | `Algorithm2Type` |
| `BufferLen` | `int` |
| `IsAdd` | `bool` |

---

### 17. アルゴリズムCompoundImgNode

- **フルタイプ**: `FlowEngineLib.Node.OLED.AlgorithmCompoundImgNode`
- **コンフィギュレーター**: `OLEDNodeConfigurators.cs`
- **実装ファイル**: `Node\OLED\AlgorithmCompoundImgNode.cs`
- **基本クラス**: `CVBaseServerNodeHub`

**構成パネルのプロパティ** (NodeConfigurator)

|属性名 |タイプ |説明 |
|------|------|------|
| `DeviceCode` |デバイスコード |デバイスコード |
| `TempName` |テンプレートJsonRef |パラメータテンプレート |

**クラスレベルのプロパティ** (ノード実装)

|プロパティ名 | C# タイプ |
|--------|--------|
| `TempName` | `string` |
| `OutputFileName` | `string` |
| `BufferLen` | `int` |

---

##カメラクラスノード

### 1. AOILocAndRegPixelsCameraNode

- **完全なタイプ**: `FlowEngineLib.AOILocAndRegPixelsCameraNode`
- **コンフィギュレーター**: `CameraNodeConfigurators.cs`
- **実装ファイル**: `AOILocAndRegPixelsCameraNode.cs`
- **基本クラス**: `CVBaseServerNode`

**構成パネルのプロパティ** (NodeConfigurator)

|属性名 |タイプ |説明 |
|------|------|------|
| `DeviceCode` |デバイスコード |デバイスコード |
| `AutoExpTempName` |テンプレート参照 |露出テンプレート |
| `CaliTempName` |テンプレート参照 |訂正 |
| `AlgTempName` |テンプレートJsonRef |葵 |

**クラスレベルのプロパティ** (ノード実装)

|プロパティ名 | C# タイプ |
|--------|--------|
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

- **フルタイプ**: `FlowEngineLib.AOILocatePixelsCameraNode`
- **コンフィギュレーター**: `CameraNodeConfigurators.cs`
- **実装ファイル**: `AOILocatePixelsCameraNode.cs`
- **基本クラス**: `CVBaseServerNode`

**構成パネルのプロパティ** (NodeConfigurator)

|属性名 |タイプ |説明 |
|------|------|------|
| `DeviceCode` |デバイスコード |デバイスコード |
| `AutoExpTempName` |テンプレート参照 |露出テンプレート |
| `CaliTempName` |テンプレート参照 |訂正 |
| `AlgTempName` |テンプレートJsonRef |サブピクセルランプビード検出 |

**クラスレベルのプロパティ** (ノード実装)

|プロパティ名 | C# タイプ |
|--------|--------|
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

### 3.CVカメラノード

- **フルタイプ**: `FlowEngineLib.CVCameraNode`
- **コンフィギュレーター**: `CameraNodeConfigurators.cs`
- **実装ファイル**: `CVCameraNode.cs`
- **基本クラス**: `CVBaseServerNode`

**構成パネルのプロパティ** (NodeConfigurator)

|属性名 |タイプ |説明 |
|------|------|------|
| `DeviceCode` |デバイスコード |デバイスコード |
| `CalibTempName` |テンプレート参照 |訂正 |
| `POITempName` |テンプレート参照 | POI テンプレート |
| `POIFilterTempName` |テンプレート参照 | POIフィルタリング |
| `POIReviseTempName` |テンプレート参照 | POI修正 |

**クラスレベルのプロパティ** (ノード実装)

|プロパティ名 | C# タイプ |
|--------|--------|
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

### 4.CamMotorNode

- **フルタイプ**: `FlowEngineLib.CamMotorNode`
- **コンフィギュレーター**: `CameraNodeConfigurators.cs`
- **実装ファイル**: `CamMotorNode.cs`
- **基本クラス**: `CVBaseServerNode`

**構成パネルのプロパティ** (NodeConfigurator)

|属性名 |タイプ |説明 |
|------|------|------|
| `DeviceCode` |デバイスコード |デバイスコード |
| `AutoFocusTemp` |テンプレート参照 |カメラテンプレート |

**クラスレベルのプロパティ** (ノード実装)

|プロパティ名 | C# タイプ |
|--------|--------|
| `RunType` | `CamMotorRunType` |
| `IsAbs` | `bool` |
| `Position` | `int` |
| `Aperture` | `float` |
| `AutoFocusTemp` | `string` |

---

### 5.LVカメラノード

- **完全なタイプ**: `FlowEngineLib.LVCameraNode`
- **コンフィギュレーター**: `CameraNodeConfigurators.cs`
- **実装ファイル**: `LVCameraNode.cs`
- **基本クラス**: `BaseCameraNode`

**構成パネルのプロパティ** (NodeConfigurator)

|属性名 |タイプ |説明 |
|------|------|------|
| `DeviceCode` |デバイスコード |デバイスコード |
| `CaliTempName` |テンプレート参照 |訂正 |
| `POITempName` |テンプレート参照 | POI テンプレート |
| `POIFilterTempName` |テンプレート参照 | POIフィルタリング |
| `POIReviseTempName` |テンプレート参照 | POI修正 |

---

### 6.CVAOI2カメラノード- **完全なタイプ**: `FlowEngineLib.Node.Camera.CVAOI2CameraNode`
- **コンフィギュレーター**: `CameraNodeConfigurators.cs`
- **実装ファイル**: `Node\Camera\CVAOI2CameraNode.cs`
- **基本クラス**: `CVBaseServerNodeHub`

**構成パネルのプロパティ** (NodeConfigurator)

|属性名 |タイプ |説明 |
|------|------|------|
| `DeviceCode` |デバイスコード |デバイスコード |
| `CamTempName` |テンプレート参照 |カメラテンプレート |
| `TempName` |テンプレート参照 |露出テンプレート |
| `CalibTempName` |テンプレート参照 |訂正 |
| `AlgTempName` |テンプレートJsonRef |葵 |

**クラスレベルのプロパティ** (ノード実装)

|プロパティ名 | C# タイプ |
|--------|--------|
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

### 7.CVAOIカメラノード

- **フルタイプ**: `FlowEngineLib.Node.Camera.CVAOICameraNode`
- **コンフィギュレーター**: `CameraNodeConfigurators.cs`
- **実装ファイル**: `Node\Camera\CVAOICameraNode.cs`
- **基本クラス**: `CVBaseServerNode`

**構成パネルのプロパティ** (NodeConfigurator)

|属性名 |タイプ |説明 |
|------|------|------|
| `DeviceCode` |デバイスコード |デバイスコード |
| `CamTempName` |テンプレート参照 |カメラテンプレート |
| `TempName` |テンプレート参照 |露出テンプレート |
| `CalibTempName` |テンプレート参照 |訂正 |
| `AlgTempName` |テンプレートJsonRef |サブピクセルランプビード検出 |

**クラスレベルのプロパティ** (ノード実装)

|プロパティ名 | C# タイプ |
|--------|--------|
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

- **完全なタイプ**: `FlowEngineLib.Node.Camera.CommCameraNode`
- **コンフィギュレーター**: `CameraNodeConfigurators.cs`
- **実装ファイル**: `Node\Camera\CommCameraNode.cs`
- **基本クラス**: `CVBaseServerNode`

**構成パネルのプロパティ** (NodeConfigurator)

|属性名 |タイプ |説明 |
|------|------|------|
| `DeviceCode` |デバイスコード |デバイスコード |
| `CalibTempName` |テンプレート参照 |訂正 |
| `CamTempName` |テンプレート参照 |カメラテンプレート |
| `TempName` |テンプレート参照 |露出テンプレート |
| `POITempName` |テンプレート参照 | POI テンプレート |
| `POIFilterTempName` |テンプレート参照 | POIフィルタリング |
| `POIReviseTempName` |テンプレート参照 | POI修正 |

**クラスレベルのプロパティ** (ノード実装)

|プロパティ名 | C# タイプ |
|--------|--------|
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

## OLEDクラスノード

### 1. OLEDImageCroppingNode

- **完全なタイプ**: `FlowEngineLib.Node.OLED.OLEDImageCroppingNode`
- **コンフィギュレーター**: `OLEDNodeConfigurators.cs`
- **実装ファイル**: `Node\OLED\OLEDImageCroppingNode.cs`
- **基本クラス**: `CVBaseServerNodeHub`

**構成パネルのプロパティ** (NodeConfigurator)

|属性名 |タイプ |説明 |
|------|------|------|
| `DeviceCode` |デバイスコード |デバイスコード |
| `TempName` |テンプレート参照 |パラメータテンプレート |

**クラスレベルのプロパティ** (ノード実装)

|プロパティ名 | C# タイプ |
|--------|--------|
| `TempName` | `string` |
| `ImgFileName` | `string` |

---

### 2. OLEDRebuildPixelsNode

- **完全なタイプ**: `FlowEngineLib.Node.OLED.OLEDRebuildPixelsNode`
- **コンフィギュレーター**: `OLEDNodeConfigurators.cs`
- **実装ファイル**: `Node\OLED\OLEDRebuildPixelsNode.cs`
- **基本クラス**: `CVBaseServerNodeHub`

**構成パネルのプロパティ** (NodeConfigurator)

|属性名 |タイプ |説明 |
|------|------|------|
| `DeviceCode` |デバイスコード |デバイスコード |
| `ImgFileName` |画像パス |画像ファイルのパス |
| `OutputTemplateName` |テンプレート参照 |ポイ出力 |
| `TempName` |テンプレートJsonRef |サブピクセルランプビード検出 |

**クラスレベルのプロパティ** (ノード実装)

|プロパティ名 | C# タイプ |
|--------|--------|
| `Channel` | `CVOLED_Channel` |
| `TempName` | `string` |
| `ImgFileName` | `string` |
| `OutputTemplateName` | `string` |

---

## POI クラス ノード

### 1.BuildPOINode

- **フルタイプ**: `FlowEngineLib.BuildPOINode`
- **コンフィギュレーター**: `POINodeConfigurators.cs`
- **実装ファイル**: `BuildPOINode.cs`
- **基本クラス**: `CVBaseServerNode`

**構成パネルのプロパティ** (NodeConfigurator)

|属性名 |タイプ |説明 |
|------|------|------|
| `DeviceCode` |デバイスコード |デバイスコード |
| `ImgFileName` |画像パス |画像ファイルのパス |
| `TemplateName` |テンプレート参照 |ポイントレイアウトテンプレート |
| `RePOITemplateName` |テンプレート参照 |リポジトリ |
| `LayoutROITemplate` |テンプレート参照 |レイアウトROI |
| `SavePOITempName` |テンプレート参照 | POIを保存 |

**クラスレベルのプロパティ** (ノード実装)

|プロパティ名 | C# タイプ |
|--------|--------|
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
| `BufferLen` | `int` |---

### 2. POIAna AnalysisNode

- **フルタイプ**: `FlowEngineLib.Node.POI.POIAnalysisNode`
- **コンフィギュレーター**: `POINodeConfigurators.cs`
- **実装ファイル**: `Node\POI\POIAnalysisNode.cs`
- **基本クラス**: `CVBaseServerNode`

**構成パネルのプロパティ** (NodeConfigurator)

|属性名 |タイプ |説明 |
|------|------|------|
| `DeviceCode` |デバイスコード |デバイスコード |
| `TempName` |テンプレートJsonRef |ポイ分析 |

**クラスレベルのプロパティ** (ノード実装)

|プロパティ名 | C# タイプ |
|--------|--------|
| `TempName` | `string` |

---

### 3.POIREviseNode

- **完全なタイプ**: `FlowEngineLib.Node.POI.POIReviseNode`
- **コンフィギュレーター**: `POINodeConfigurators.cs`
- **実装ファイル**: `Node\POI\POIReviseNode.cs`
- **基本クラス**: `CVBaseServerNodeHub`

**構成パネルのプロパティ** (NodeConfigurator)

|属性名 |タイプ |説明 |
|------|------|------|
| `DeviceCode` |デバイスコード |デバイスコード |
| `TemplateName` |テンプレート参照 | POI補正キャリブレーション |

**クラスレベルのプロパティ** (ノード実装)

|プロパティ名 | C# タイプ |
|--------|--------|
| `TemplateName` | `string` |
| `POIPointName` | `string` |
| `IsSelfResultRevise` | `bool` |

---

### 4.RealPOINode

- **フルタイプ**: `FlowEngineLib.Node.POI.RealPOINode`
- **コンフィギュレーター**: `POINodeConfigurators.cs`
- **実装ファイル**: `Node\POI\RealPOINode.cs`
- **基本クラス**: `CVBaseServerNodeHub`

**構成パネルのプロパティ** (NodeConfigurator)

|属性名 |タイプ |説明 |
|------|------|------|
| `DeviceCode` |デバイスコード |デバイスコード |
| `FilterTemplateName` |テンプレート参照 | POIフィルタリング |
| `ReviseTemplateName` |テンプレート参照 | POI修正 |
| `OutputTemplateName` |テンプレート参照 |ファイル出力テンプレート |

**クラスレベルのプロパティ** (ノード実装)

|プロパティ名 | C# タイプ |
|--------|--------|
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

### 5.POIノード

- **フルタイプ**: `FlowEngineLib.POINode`
- **コンフィギュレーター**: `POINodeConfigurators.cs`
- **実装ファイル**: `POINode.cs`
- **基本クラス**: `CVBaseServerNode`

**構成パネルのプロパティ** (NodeConfigurator)

|属性名 |タイプ |説明 |
|------|------|------|
| `DeviceCode` |デバイスコード |デバイスコード |
| `ImgFileName` |画像パス |画像ファイルのパス |
| `TempName` |テンプレート参照 | POI テンプレート |
| `FilterTemplateName` |テンプレート参照 | POIフィルタリング |
| `ReviseTemplateName` |テンプレート参照 | POI修正 |
| `OutputTemplateName` |テンプレート参照 |ファイル出力テンプレート |

**クラスレベルのプロパティ** (ノード実装)

|プロパティ名 | C# タイプ |
|--------|--------|
| `TempName` | `string` |
| `FilterTemplateName` | `string` |
| `ReviseTemplateName` | `string` |
| `OutputTemplateName` | `string` |
| `ImgFileName` | `string` |
| `IsCCTWave` | `bool` |
| `IsSubPixel` | `bool` |

---

## PG クラス ノード

### 1.PGノード

- **フルタイプ**: `FlowEngineLib.Node.PG.PGNode`
- **コンフィギュレーター**: `DeviceNodeConfigurators.cs`
- **実装ファイル**: `Node\PG\PGNode.cs`
- **基本クラス**: `CVBaseServerNode`

**構成パネルのプロパティ** (NodeConfigurator)

|属性名 |タイプ |説明 |
|------|------|------|
| `DeviceCode` |デバイスコード |デバイスコード |

**クラスレベルのプロパティ** (ノード実装)

|プロパティ名 | C# タイプ |
|--------|--------|
| `PGCmd` | `PGCommCmdType` |
| `IndexFrame` | `int` |

---

## SMU クラス ノード

### 1.SMUFromCSVNode

- **フルタイプ**: `FlowEngineLib.SMUFromCSVNode`
- **コンフィギュレーター**: `DeviceNodeConfigurators.cs`
- **実装ファイル**: `SMUFromCSVNode.cs`
- **基本クラス**: `SMUBaseNode`

**構成パネルのプロパティ** (NodeConfigurator)

|属性名 |タイプ |説明 |
|------|------|------|
| `DeviceCode` |デバイスコード |デバイスコード |
| `CsvFileName` |画像パス |画像ファイルのパス |

**クラスレベルのプロパティ** (ノード実装)

|プロパティ名 | C# タイプ |
|--------|--------|
| `Source` | `SourceType` |
| `Channel` | `SMUChannelType` |
| `CsvFileName` | `string` |
| `IsAutoRng` | `bool` |
| `SrcRng` | `double` |
| `LmtRng` | `double` |

---

### 2.SMUModelNode

- **フルタイプ**: `FlowEngineLib.SMUModelNode`
- **コンフィギュレーター**: `DeviceNodeConfigurators.cs`
- **実装ファイル**: `SMUModelNode.cs`
- **基本クラス**: `SMUBaseNode`

**構成パネルのプロパティ** (NodeConfigurator)

|属性名 |タイプ |説明 |
|------|------|------|
| `DeviceCode` |デバイスコード |デバイスコード |
| `ModelName` |テンプレート参照 | SMUParam 設定 |

**クラスレベルのプロパティ** (ノード実装)

|プロパティ名 | C# タイプ |
|--------|--------|
| `ModelName` | `string` |

---

### 3.SMUノード

- **完全なタイプ**: `FlowEngineLib.SMUNode`
- **コンフィギュレーター**: `DeviceNodeConfigurators.cs`
- **実装ファイル**: `SMUNode.cs`
- **基本クラス**: `SMUBaseNode`

**構成パネルのプロパティ** (NodeConfigurator)

|属性名 |タイプ |説明 |
|------|------|------|
| `DeviceCode` |デバイスコード |デバイスコード |

**クラスレベルのプロパティ** (ノード実装)

|プロパティ名 | C# タイプ |
|--------|--------|
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

## センサークラスノード### 1. CommonSensorNode

- **完全なタイプ**: `FlowEngineLib.CommonSensorNode`
- **コンフィギュレーター**: `DeviceNodeConfigurators.cs`
- **実装ファイル**: `CommonSensorNode.cs`
- **基本クラス**: `CVBaseServerNode`

**構成パネルのプロパティ** (NodeConfigurator)

|属性名 |タイプ |説明 |
|------|------|------|
| `DeviceCode` |デバイスコード |デバイスコード |
| `TempName` |センサーテンプレート参照 |センサーテンプレート |

**クラスレベルのプロパティ** (ノード実装)

|プロパティ名 | C# タイプ |
|--------|--------|
| `TempName` | `string` |
| `CmdType` | `CommCmdType` |
| `CmdSend` | `string` |
| `CmdReceive` | `string` |

---

### 2.TempCommonSensorNode

- **完全なタイプ**: `FlowEngineLib.TempCommonSensorNode`
- **コンフィギュレーター**: `DeviceNodeConfigurators.cs`
- **実装ファイル**: `TempCommonSensorNode.cs`
- **基本クラス**: `CVBaseServerNode`

**構成パネルのプロパティ** (NodeConfigurator)

|属性名 |タイプ |説明 |
|------|------|------|
| `DeviceCode` |デバイスコード |デバイスコード |
| `TempName` |センサーテンプレート参照 |センサーテンプレート |

**クラスレベルのプロパティ** (ノード実装)

|プロパティ名 | C# タイプ |
|--------|--------|
| `TempName` | `string` |

---

## FWクラスノード

### 1.FWノード

- **完全なタイプ**: `FlowEngineLib.FWNode`
- **コンフィギュレーター**: `DeviceNodeConfigurators.cs`
- **実装ファイル**: `FWNode.cs`
- **基本クラス**: `CVBaseServerNode`

**構成パネルのプロパティ** (NodeConfigurator)

|属性名 |タイプ |説明 |
|------|------|------|
| `DeviceCode` |デバイスコード |デバイスコード |

**クラスレベルのプロパティ** (ノード実装)

|プロパティ名 | C# タイプ |
|--------|--------|
| `Port` | `int` |
| `ModelType` | `FWModelType` |

---

## スペクトルクラスノード

### 1. SpectrumEQENode

- **フルタイプ**: `SpectrumEQENode`
- **コンフィギュレーター**: `SpectrumNodeConfigurators.cs`

---

### 2. スペクトルループノード

- **フルタイプ**: `SpectrumLoopNode`
- **コンフィギュレーター**: `SpectrumNodeConfigurators.cs`

---

### 3. スペクトルノード

- **フルタイプ**: `SpectrumNode`
- **コンフィギュレーター**: `SpectrumNodeConfigurators.cs`

---