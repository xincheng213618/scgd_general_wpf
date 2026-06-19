# ARVR テンプレート

このページは、現在のリポジトリに実在する ARVR テンプレート群だけを扱います。光学アルゴリズムの教材や汎用パラメータ表ではなく、交代担当者が現在の実装を追えるための地図です。

## 現在の役割

現在の ARVR は単一テンプレートではなく、複数の伝統的テンプレート、JSON V2 テンプレート、POI テンプレート、Flow ノードが組み合わさった実行面です。

- `MTF`
- `SFR`
- `FOV`
- `Distortion`
- `Ghost`

これらは似た宿主構造を使いますが、パラメータ、結果表示、POI 依存は同じではありません。Flow ではさらに `SFR_FindROI`、`ARVR.BinocularFusion`、`FindCross` などの JSON 分岐も混ざります。

## 重要なファイル

- `Engine/ColorVision.Engine/Templates/ARVR/MTF/TemplateMTF.cs`
- `Engine/ColorVision.Engine/Templates/ARVR/MTF/AlgorithmMTF.cs`
- `Engine/ColorVision.Engine/Templates/ARVR/MTF/ViewHandleMTF.cs`
- `Engine/ColorVision.Engine/Templates/ARVR/SFR/AlgorithmSFR.cs`
- `Engine/ColorVision.Engine/Templates/ARVR/SFR/WindowSFR.xaml.cs`
- `Engine/ColorVision.Engine/Templates/ARVR/FOV/AlgorithmFOV.cs`
- `Engine/ColorVision.Engine/Templates/ARVR/FOV/DisplayFOV.xaml.cs`
- `Engine/ColorVision.Engine/Templates/ARVR/Distortion/AlgorithmDistortion.cs`
- `Engine/ColorVision.Engine/Templates/ARVR/Distortion/ViewResultDistortion.cs`
- `Engine/ColorVision.Engine/Templates/ARVR/Ghost/AlgorithmGhost.cs`
- `Engine/ColorVision.Engine/Templates/Flow/NodeConfigurator/AlgorithmNodeConfigurators.cs`
- `Engine/FlowEngineLib/Algorithm/AlgorithmARVRNode.cs`

## 現在のテンプレートマトリクス

| ファミリ | 伝統的テンプレート | 辞書/コード | 実行イベント | 主要リクエストパラメータ | 結果入口 |
| --- | --- | --- | --- | --- | --- |
| `FOV` | `TemplateFOV` | `TemplateDicId = 6`, `Code = FOV` | `Event_FOV_GetData` | `TemplateParam` | `ViewHandleFOV`, `ViewResultAlgType.FOV` |
| `Ghost` | `TemplateGhost` | `TemplateDicId = 7`, `Code = ghost` | `Ghost` | `TemplateParam`, `Color` | `ViewHandleGhost`, `ViewResultAlgType.Ghost` |
| `MTF` | `TemplateMTF` | `TemplateDicId = 8`, `Code = MTF` | `Event_MTF_GetData` | `TemplateParam`, `POITemplateParam` | `ViewHandleMTF`, `ViewResultAlgType.MTF` |
| `SFR` | `TemplateSFR` | `TemplateDicId = 9`, `Code = SFR` | `Event_SFR_GetData` | `TemplateParam`, `POITemplateParam` | `ViewHandleSFR`, `ViewResultAlgType.SFR` |
| `Distortion` | `TemplateDistortionParam` | `TemplateDicId = 10`, `Code = distortion` | `Distortion` | `TemplateParam` | `ViewHandleDistortion`, `ViewResultAlgType.Distortion` |
| `AOI` | `TemplateAOIParam` | `TemplateDicId = 12`, `Code = AOI` | 現時点では独立した主実行入口ではない | テンプレート設定 | 主に ARVR/AOI パラメータ設定 |

この表のイベントは手動アルゴリズムクラスから見える経路です。Flow の `operatorCode` は JSON 分岐も含むため、次の Flow 表も必ず確認してください。

## 主な手動実行チェーン

### MTF

`TemplateMTF` は `TemplateDicId = 8`、`Code = MTF` の伝統的テンプレートです。`AlgorithmMTF` はローカルで数値計算を完結するのではなく、`TemplateMTF` と `TemplatePoi` を選択し、`TemplateParam` と `POITemplateParam` を含む `Event_MTF_GetData` を送ります。

結果側では `ViewHandleMTF` が `ViewResultAlgType.MTF` を処理し、CSV エクスポート、最大/最小/平均/分散/均一性の集計を担当します。

### SFR

`TemplateSFR` は `TemplateDicId = 9`、`Code = SFR` です。`AlgorithmSFR` も POI を必要とし、`Event_SFR_GetData` を送ります。結果表示では `WindowSFR` が `Pdfrequency` と `PdomainSamplingData` を曲線に戻し、閾値や周波数変換を扱います。

### FOV

`TemplateFOV` は `TemplateDicId = 6`、`Code = FOV` です。`AlgorithmFOV` は `Event_FOV_GetData` を送ります。`DisplayFOV` はサービスマネージャから画像ソースを取り、バッチ、Raw ファイル、ローカル画像の入力を扱います。

### Distortion

`TemplateDistortionParam` は `TemplateDicId = 10`、`Code = distortion` です。`AlgorithmDistortion` は `Distortion` イベントを送ります。`ViewResultDistortion` は enum と最終点列を表示用モデルに変換するため、結果表示の調査では必ず確認します。

### Ghost

`TemplateGhost` は `TemplateDicId = 7`、`Code = ghost` です。`AlgorithmGhost` は `TemplateParam` に加えて `Color` を送り、`Ghost` イベントを発行します。色チャンネルは補助情報ではなく、現在の Ghost チェーンの正式な入力です。

## Flow での接続

| Flow 算子 | `operatorCode` | 設定器が接続するテンプレート | 引き継ぎポイント |
| --- | --- | --- | --- |
| `MTF` | `MTF` | `TemplateMTF` + `TemplatePoi` | POI が無いと `POITemplateParam` が欠け、点位の意味が不完全になる。 |
| `SFR` | `SFR` | `TemplateSFR` + `TemplatePoi` | SFR 曲線は ROI/POI の空間定義に依存する。 |
| `FOV` | `FOV` | `TemplateDFOV` + `TemplateFOV` | 同じスロットに JSON V2 と伝統テンプレートが出るため、実際の選択元を見る。 |
| `Distortion` | `Distortion` | `TemplateDistortion2` + `TemplateDistortionParam` | JSON V2 と伝統パラメータが共存する。 |
| `SFR_FindROI` | `ARVR.SFR.FindROI` | `TemplateSFRFindROI` + `TemplatePoi` | 伝統的 `TemplateSFR` ではなく JSON ROI 検出チェーン。 |
| `BinocularFusion` | `ARVR.BinocularFusion` | `TemplateBinocularFusion` | JSON テンプレート経路。 |
| `FindCross` | `FindCross` | `TemplateFindCross` + `TemplatePoi` | UI 表示が ROI でも、選択器は `TemplatePoi` を使う。 |

`AlgorithmARVRNode.getBaseEventData(...)` は `BufferLen`、色、前段画像パラメータ、SMU 結果もリクエストに入れます。手動実行だけが成功する場合は、手動アルゴリズムのリクエストと Flow が生成したリクエストを比較してください。

## 結果保存と表示

| 結果 | 結果表/フィールド | 表示入口 | 調査ポイント |
| --- | --- | --- | --- |
| `FOV` | `t_scgd_algorithm_result_detail_fov`, `pattern`, `radio`, `camera_degrees`, `dist`, `threshold`, `degrees` | `ViewHandleFOV` | 画像入力、テンプレート、角度/距離フィールドを一緒に見る。 |
| `Ghost` | `t_scgd_algorithm_result_detail_ghost`, `rows`, `cols`, `radius`, `led_centers`, `ghost_pixels` | `ViewHandleGhost` | 色チャンネルと点列数が overlay に影響する。 |
| `SFR` | `t_scgd_algorithm_result_detail_sfr`, ROI, `gamma`, `pdfrequency`, `pdomain_sampling_data` | `ViewHandleSFR`, `WindowSFR` | 曲線表示はサンプリングデータの復元結果。 |
| `Distortion` | `t_scgd_algorithm_result_detail_distortion`, `layout_type`, `slope_type`, `corner_type`, `max_ratio`, `final_points` | `ViewHandleDistortion`, `ViewResultDistortion` | enum マッピングと最終点列を合わせて確認する。 |

## よくある誤解

### ARVR は統一 schema ではない

各ディレクトリは似た宿主を共有しますが、同じ JSON schema や同じパラメータ構造を共有しているわけではありません。

### アルゴリズムクラスは主にホストとコマンドアダプタ

`AlgorithmMTF`、`AlgorithmSFR`、`AlgorithmFOV`、`AlgorithmDistortion`、`AlgorithmGhost` は、画面を開き、入力を集め、MQTT リクエストを送る役割が中心です。

### POI は脇役ではない

MTF、SFR、SFR_FindROI は明示的に `TemplatePoi` を使います。POI を無視すると現在の ARVR 実行チェーンは説明できません。

### 伝統テンプレートと JSON V2 は単純な置換関係ではない

FOV、Ghost、Distortion、SFR_FindROI などは Flow で伝統テンプレートと JSON テンプレートを同時に見せます。`operatorCode`、テンプレート種別、結果バージョンを確認してください。

## 受け入れ確認

| 場面 | 必須確認 |
| --- | --- |
| 手動 MTF/SFR | `TemplateParam` と `POITemplateParam` の両方が送られ、結果が対応する `ViewHandle*` に入る。 |
| Flow ARVR ノード | 算子を切り替えるとテンプレート選択器が切り替わり、`operatorCode` も一致する。 |
| FOV/Distortion V2 | 伝統テンプレートと JSON テンプレートが混線せず、結果 handler も誤らない。 |
| SFR 曲線 | `WindowSFR` が曲線を開き、CSV と `pdomain_sampling_data` が対応する。 |
| Ghost | リクエストに `Color` があり、結果表の点列数と overlay が一致する。 |

## 推奨される読む順序

1. `Engine/ColorVision.Engine/Templates/ARVR/MTF/AlgorithmMTF.cs`
2. `Engine/ColorVision.Engine/Templates/ARVR/SFR/AlgorithmSFR.cs`
3. `Engine/ColorVision.Engine/Templates/ARVR/FOV/DisplayFOV.xaml.cs`
4. `Engine/ColorVision.Engine/Templates/ARVR/Distortion/ViewResultDistortion.cs`
5. `Engine/ColorVision.Engine/Templates/Flow/NodeConfigurator/AlgorithmNodeConfigurators.cs`

## 続きを読む

- [POI テンプレート](./poi-template.md)
- [JSON テンプレート](./json-templates.md)
- [Flow エンジン](./flow-engine.md)
