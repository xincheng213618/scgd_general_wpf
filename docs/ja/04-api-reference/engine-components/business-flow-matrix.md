# Engine 業務チェーンマトリクス

このページは、引き継ぎ担当者が Engine 側の業務経路を素早く判断するための地図です。デバイス資源、テンプレート、Flow、アルゴリズム結果、プロジェクト出力のどこに変更点があるかを先に切り分けます。

## メインチェーン

```text
SysResourceModel / DB resource
  -> ServiceTypes / DeviceServiceFactoryRegistry
  -> DeviceService / MQTTDeviceService / MQTTControl
  -> TemplateControl / TemplateModel / TemplateFlow
  -> FlowEngineControl / Flow node / NodeConfiguratorRegistry
  -> AlgResult / ViewResult / ImageEditor Overlay
  -> Projects/* / ObjectiveTestResult / export or Socket response
```

Engine は単なるアルゴリズムライブラリではありません。DB 資源、デバイスサービス、テンプレート設定、Flow ノード、結果表示、プロジェクト後処理をつなぐ業務編成層です。

## シナリオマトリクス

| 業務シナリオ | 最初の入り口 | 主なコード | 引き継ぎ時の確認 |
| --- | --- | --- | --- |
| 起動と初期化 | Engine 初期化処理 | `ColorVision.Engine/Services/`, `MQTT/`, `Templates/` | サービス登録、MQTT 接続、テンプレート読み込み |
| DB デバイス表示 | `SysResourceModel`, `ServiceTypes` | `Services/Type/TypeService.cs`, `Services/DeviceServiceFactoryRegistry.cs` | Type、Factory、資源フィルタ |
| デバイス種別追加 | `DeviceService<TConfig>` | `Services/Devices/**`, `Services/Devices/*/MQTT*.cs` | Config、Service、画面、MQTT、Flow 設定器 |
| リモート制御 / MQTT | `MQTTControl`, `MQTTDeviceService` | `Services/Devices/**/MQTT*.cs`, `MQTT/` | Topic、Token、応答項目、タイムアウト |
| テンプレートパラメータ | `TemplateControl`, `TemplateModel<T>` | `Templates/Jsons/`, `Templates/ARVR/`, `Templates/POI/` | `Code`, `Title`, `TemplateDicId` の互換性 |
| Flow 編集と実行 | `TemplateFlow`, `FlowControl` | `Templates/Flow/`, `FlowEngineLib/` | 開く、保存、インポート、実行、`FlowCompleted` |
| Flow ノード紐付け | `NodeConfiguratorRegistry` | `Templates/Flow/NodeConfigurator/`, `Templates/Flow/Nodes/` | ノード、テンプレート、デバイス種別の一致 |
| 結果表示 | `AlgResultMasterModel`, `ViewResultAlg` | `Templates/**/ViewHandle*.cs`, `Abstractions/IResultHandlers.cs` | DAO、結果項目、画像パス、座標系 |
| 画像オーバーレイ | `IViewResult`, `IResultHandleBase` | `UI/ColorVision.ImageEditor/Draw/**` | 表示責任と顧客判定を分離 |
| プロジェクト出力 | `Projects/<Project>/Process` | `Projects/*/Recipe`, `Fix`, `Process`, exporter | Engine 結果 key とプロジェクト読取の一致 |
| バッチとアーカイブ | `MeasureBatchModel` | `Services/Batch`, DAO, CSV/SQLite/MySQL | バッチ番号、結果 ID、保存先 |
| ファイル / 画像 | `ColorVision.FileIO`, `cvColorVision` | `Engine/ColorVision.FileIO/`, `Engine/cvColorVision/` | ネイティブ DLL、形式、CopyLocal、x64 runtime |

## デバイス種別

代表的な種別は Camera、PG、Spectrum、SMU、Sensor、FileServer、Algorithm、Calibration、Motor、CfwPort、FlowDevice、ThirdPartyAlgorithms です。変更時は次を同時に確認します。

- `ServiceTypes` に種別がある。
- `DeviceServiceFactoryRegistry` がサービスを作れる。
- `ServiceManager.DeviceServices` に保持される。
- Flow の設定器で選択できる。
- MQTT の命令、状態、結果応答を追える。

## 変更の置き場所

| 変更内容 | 優先場所 | 同時更新 |
| --- | --- | --- |
| 新しいデバイス種別 | `Services/Type/TypeService.cs`, `Services/Devices/` | [デバイスサービスチェーン](./device-service-chain.md) |
| 新しい Flow ノード | `FlowEngineLib/`, `Templates/Flow/Nodes/`, `Templates/Flow/NodeConfigurator/` | [テンプレートと Flow チェーン](./template-flow-chain.md) |
| 新しいテンプレート | `Templates/Jsons/`, `Templates/ARVR/`, `Templates/POI/` | ノード設定、結果表示、プロジェクト読取 |
| 新しい結果表示 | `Templates/**/ViewHandle*.cs`, `UI/ColorVision.ImageEditor/Draw/` | [結果表示とプロジェクト引き継ぎチェーン](./result-handoff-chain.md) |
| 顧客項目 / 判定 | `Projects/<Project>/Recipe`, `Fix`, `Process` | プロジェクト説明と出力形式 |

## 調査順序

1. DB 資源、テンプレート、バッチ、結果 ID が存在するか確認します。
2. サービス生成と MQTT の命令/応答を確認します。
3. Flow ノードが正しいテンプレートとデバイスに紐付くか確認します。
4. Engine が `AlgResultMasterModel` と明細結果を生成したか確認します。
5. 最後に UI オーバーレイと `Projects/*` の判定、出力、Socket 応答を確認します。
