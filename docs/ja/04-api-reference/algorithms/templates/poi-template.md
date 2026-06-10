# POI テンプレート

このページは、現在のリポジトリに実在する POI テンプレート群だけを扱います。POI は単独の検出アルゴリズムではなく、点集合を作成、保存、補正、出力し、他のアルゴリズムから再利用される共有テンプレート体系です。

## 現在の役割

- 主 POI テンプレートは点集合、サイズ、四隅、設定 JSON を保存します。
- フィルタ、補正、校正、出力には別々の伴生テンプレートがあります。
- ランタイムアルゴリズムはこれらのテンプレートを MQTT リクエストに組み立てます。
- Flow ノード、ARVR、JSON アルゴリズムも POI テンプレートを消費します。

## 重要なファイル

- `Engine/ColorVision.Engine/Templates/POI/TemplatePoi.cs`
- `Engine/ColorVision.Engine/Templates/POI/PoiParam.cs`
- `Engine/ColorVision.Engine/Templates/POI/PoiPoint.cs`
- `Engine/ColorVision.Engine/Templates/POI/AlgorithmImp/AlgorithmPOI.cs`
- `Engine/ColorVision.Engine/Templates/POI/BuildPoi/AlgorithmBuildPoi.cs`
- `Engine/ColorVision.Engine/Templates/POI/POIFilters/TemplatePoiFilterParam.cs`
- `Engine/ColorVision.Engine/Templates/POI/POIRevise/TemplatePoiReviseParam.cs`
- `Engine/ColorVision.Engine/Templates/POI/POIOutput/TemplatePoiOutputParam.cs`
- `Engine/ColorVision.Engine/Templates/POI/POIGenCali/TemplatePOICalParam.cs`
- `Engine/ColorVision.Engine/Templates/Flow/NodeConfigurator/POINodeConfigurators.cs`

## 現在のテンプレートマトリクス

| テンプレート | 辞書/コード | 編集入口 | 主な用途 |
| --- | --- | --- | --- |
| `TemplatePoi` | `TemplateDicId = -1`, `Code = POI` | `EditPoiParam` 独立ウィンドウ | 点集合、サイズ、四隅、設定 JSON、点明細を保存する。 |
| `TemplateBuildPoi` | `TemplateDicId = 16`, `Code = BuildPOI` | テンプレート/布点 UI | ルールまたは CAD マッピングで POI を生成する。 |
| `TemplatePoiFilterParam` | `TemplateDicId = 23`, `Code = POIFilter` | カスタムフィルタ編集 | POI 実行時の任意フィルタテンプレート。 |
| `TemplatePoiReviseParam` | `TemplateDicId = 24`, `Code = PoiRevise` | テンプレート編集 | POI 実行時の任意補正テンプレート。 |
| `TemplatePoiGenCalParam` | `TemplateDicId = 25`, `Code = POIGenCali` | カスタム校正編集 | POI 校正/補正 Flow ノードで使用。 |
| `TemplatePoiOutputParam` | `TemplateDicId = 27`, `Code = PoiOutput` | カスタム出力編集 | POI 実行時の任意ファイル出力テンプレート。 |
| `TemplateBuildPOIAA` | `TemplateDicId = 41`, `Code = BuildPOI` | JSON テンプレート編集 | AA 検出結果から POI を作る JSON V2 分岐。 |

主テンプレートは実際の点位置を保存し、伴生テンプレートは点をどう生成、フィルタ、補正、出力するかを表します。

## 主テンプレートとデータモデル

`TemplatePoi` は `ITemplate<PoiParam>` を継承し、`IsSideHide = true`、`Code = POI` です。リスト項目をダブルクリックすると `EditPoiParam` が開くため、通常の右側 `PropertyGrid` だけで説明してはいけません。

`PoiParam` は `Width`、`Height`、四隅座標、`CfgJson`、`PoiConfig`、`ObservableCollection<PoiPoint>` を保持します。`PoiPoint` は `Id`、`Name`、`PointType`、`PixX`、`PixY`、`PixWidth`、`PixHeight` を保存します。

## 永続化

POI 主テンプレートは通常の `ModMasterModel`/`ModDetailModel` ではなく、専用テーブルを使います。

| テーブル | 主なフィールド | 意味 |
| --- | --- | --- |
| `t_scgd_algorithm_poi_template_master` | `name`, `type`, `width`, `height`, 四隅座標, `cfg_json`, `tenant_id`, `is_delete` | POI テンプレート本体、キャンバスサイズ、設定 JSON。 |
| `t_scgd_algorithm_poi_template_detail` | `pid`, `pt_type`, `pix_x`, `pix_y`, `pix_width`, `pix_height`, `remark` | 各 POI 点または領域の位置とサイズ。 |

`PoiParam.LoadPoiDetailFromDB(...)` は点明細を読み戻します。保存時は主レコードを保存し、古い点明細を削除して `PoiDetailModel` をまとめて書き直します。コピーやインポートではテンプレートと点の `Id` を `-1` に戻します。

## ランタイム POI 実行

`AlgorithmPoi` は `Event_POI_GetData` を送ります。主テンプレートだけでなく、フィルタ、補正、出力、ファイル/DB 点集合の選択もまとめます。

| パラメータ | ソース | 説明 |
| --- | --- | --- |
| `TemplateParam` | `TemplatePoi` | 必須の主 POI テンプレート。 |
| `FilterTemplate` | `TemplatePoiFilterParam` | `Id != -1` のとき送信。 |
| `ReviseTemplate` | `TemplatePoiReviseParam` | `Id != -1` のとき送信。 |
| `OutputTemplate` | `TemplatePoiOutputParam` | `Id != -1` のとき送信。 |
| `POIStorageType` | `POIStorageModel` | ファイルモードで送信し、DB 点集合と外部点ファイルを区別する。 |
| `POIPointFileName` | ファイル選択 | ファイルモードの外部点ファイルパス。 |
| `IsSubPixel`, `IsCCTWave` | アルゴリズム UI | サブピクセル/CCT 波形関連の実行オプション。 |

## BuildPOI

`AlgorithmBuildPoi` は `Event_Build_POI` を送ります。`TemplateBuildPoi` を使い、`POILayoutReq`、`POIStorageType`、`BuildType` を送ります。`POIBuildType == CADMapping` の場合は `LayoutPolygon` と `CADMappingParam` も付けます。

`Event_Build_POI` は点集合を生成する側、`Event_POI_GetData` は点集合から値を取る側です。現場調査ではこの 2 つを混同しないでください。

## Flow での消費

| Flow 設定分岐 | デバイス/入力 | テンプレート選択器 | 引き継ぎポイント |
| --- | --- | --- | --- |
| POI 校正補正 | `DeviceAlgorithm` | `TemplatePoiGenCalParam` | 主 POI ではなく校正補正テンプレートを扱う。 |
| POI フィルタ/補正/出力 | `DeviceAlgorithm` | `TemplatePoiFilterParam`, `TemplatePoiReviseParam`, `TemplatePoiOutputParam` | 既存 POI 結果の後処理。 |
| POI 実行 | `DeviceAlgorithm` + 画像パス | `TemplatePoi`, フィルタ, 補正, 出力 | `Event_POI_GetData` の完全な実行チェーン。 |
| BuildPOI | `DeviceAlgorithm` + 画像パス | `TemplateBuildPoi` または `TemplateBuildPOIAA`, `RePOI`, `LayoutROI`, `SavePOI` | 伝統布点と JSON AA 布点を両方扱う。 |
| PoiAnalysis | `DeviceAlgorithm` | `TemplatePoiAnalysis` | JSON 分析テンプレートも POI 関連結果を消費する。 |

## 結果保存と表示

| 結果タイプ | 表示/エクスポート入口 | テーブル/ファイルの手がかり |
| --- | --- | --- |
| `POI`, `POI_Y` | `ViewHanlePOIY` | CSV エクスポート。値は POI 明細結果から来る。 |
| `POI_XYZ` | `ViewHanlePOIXZY` | CSV と XYZ 表示。 |
| `POI_XYZ_File`, `POI_Y_File`, `POI_CIE_File` | `ViewHanlePOIXZY` | ファイル型結果。`t_scgd_algorithm_result_detail_poi_cie_file` を確認する。 |
| `RealPOI`, `POI_XYZ_V2`, `POI_Y_V2`, `KB_Output_Lv`, `KB_Output_CIE` | `ViewHandleRealPOI` | V2/プロジェクト出力チェーン。実際の `ResultType` を確認する。 |
| `BuildPOI`, `BuildPOI_File` | `ViewHandleBuildPoi`, `ViewHandleBuildPoiFile` | 布点結果またはファイル結果。新しい POI データを生成することがある。 |

点値明細には `t_scgd_algorithm_result_detail_poi_mtf` があり、`poi_id`、`poi_name`、`poi_type`、`poi_x/y`、`poi_width/height`、`value` を扱います。

## よくある誤解

### POI は単独アルゴリズムではない

POI は共有点集合テンプレート体系であり、点の生成、フィルタ、補正、他アルゴリズムからの参照を受け持ちます。

### 主保存は通常の detail テーブルではない

主テンプレートは `PoiMasterDao` と `PoiDetailDao` に依存します。通常のテンプレート表だけで説明すると点明細を見落とします。

### 主エディタは純粋な `PropertyGrid` ではない

`TemplatePoi` は `EditPoiParam` を開き、フィルタや出力テンプレートも専用編集 UI を持ちます。

### ファイルモードと DB モードが共存する

`AlgorithmPoi` は `POIStorageModel.Db` と `POIStorageModel.File` の両方を扱います。

## 受け入れ確認

| 場面 | 必須確認 |
| --- | --- |
| POI 新規/保存 | master 表に主レコード、detail 表に点明細ができる。 |
| POI コピー/インポート | テンプレートと点明細の `Id` がリセットされ、旧テンプレートを上書きしない。 |
| ファイルモード実行 | MQTT パラメータに `POIStorageType` と `POIPointFileName` が入る。 |
| フィルタ/補正/出力 | 選択時に `FilterTemplate`、`ReviseTemplate`、`OutputTemplate` が送られる。 |
| BuildPOI CADMapping | `LayoutPolygon` と `CADMappingParam` があり、四点 ROI と CAD ファイルパスが正しい。 |
| 結果表示 | `ViewResultAlgType` に応じた handler に入り、CSV と結果表/ファイルが一致する。 |

## 推奨される読む順序

1. `Engine/ColorVision.Engine/Templates/POI/TemplatePoi.cs`
2. `Engine/ColorVision.Engine/Templates/POI/PoiParam.cs`
3. `Engine/ColorVision.Engine/Templates/POI/AlgorithmImp/AlgorithmPOI.cs`
4. `Engine/ColorVision.Engine/Templates/POI/BuildPoi/AlgorithmBuildPoi.cs`
5. `Engine/ColorVision.Engine/Templates/Flow/NodeConfigurator/POINodeConfigurators.cs`

## 続きを読む

- [POI プリミティブ](../primitives/poi.md)
- [JSON テンプレート](./json-templates.md)
- [Flow エンジン](./flow-engine.md)
