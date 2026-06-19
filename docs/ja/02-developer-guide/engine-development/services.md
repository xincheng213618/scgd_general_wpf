# Engine サービス開発引き継ぎ

このページは `Engine/ColorVision.Engine/Services/` の現在の実装に基づくサービス開発メモです。ここでのサービスは、ホスト実行時に表示され、設定を持ち、必要に応じて MQTT で命令を送るデバイスまたは業務サービスです。

まず [Engine デバイスサービスチェーン](../../04-api-reference/engine-components/device-service-chain.md) を読んでから、このページで変更箇所を確認します。

## 実行時チェーン

| 段階 | 主要オブジェクト | 説明 |
| --- | --- | --- |
| サービス種別 | `ServiceTypes` | Camera、PG、Spectrum、SMU、Sensor、FileServer、Algorithm、FilterWheel、Calibration、Motor、Flow、ThirdPartyAlgorithms など |
| 設定 | `SysResourceModel.Value` | デバイス設定 JSON。`DeviceService<T>` が具体的な `Config*` に復元します |
| 生成 | `DeviceServiceFactoryRegistry` | `SysResourceModel.Type` により具体的な `Device*` を作成します |
| 実行時リスト | `ServiceManager.GetInstance().DeviceServices` | ホストにロードされたデバイスサービス |
| UI | `GetDeviceInfo()`、`GetDisplayControl()` | 情報パネル、制御パネル、デバイスツリー |
| 命令 | `GetMQTTService()`、`MQTTDeviceService<T>` | 送信、応答、タイムアウト、メッセージ記録 |

読み込み順は、`SysResourceModel` 保存、`ServiceManager.Load()`、`DeviceServiceFactoryRegistry.CreateService()`、`DeviceService<T>` による設定復元、具体 `Device*` による `MQTT*` と UI 作成、という流れです。

## 既定登録

| 種別 | ディレクトリ | Device | MQTT | 主な役割 |
| --- | --- | --- | --- | --- |
| Camera | `Services/Devices/Camera/` | `DeviceCamera` | `MQTTCamera` | カメラ、ライブ、撮影、露光、校正 |
| PG | `Services/Devices/PG/` | `DevicePG` | `MQTTPG` | PG 切替、パターン、プロジェクト連動 |
| Spectrum | `Services/Devices/Spectrum/` | `DeviceSpectrum` | `MQTTSpectrum` | 分光器、暗電流、測定、スペクトル |
| SMU | `Services/Devices/SMU/` | `DeviceSMU` | `MQTTSMU` | SMU、スキャン、結果取得 |
| Sensor | `Services/Devices/Sensor/` | `DeviceSensor` | `MQTTSensor` | センサー通信とコマンドテンプレート |
| FileServer | `Services/Devices/FileServer/` | `DeviceFileServer` | `MQTTFileServer` | ファイルパス、ダウンロード、キャッシュ |
| Algorithm | `Services/Devices/Algorithm/` | `DeviceAlgorithm` | `MQTTAlgorithm` | アルゴリズムサービスと結果確認 |
| FilterWheel | `Services/Devices/CfwPort/` | `DeviceCfwPort` | `MQTTCfwPort` | フィルターホイール制御 |
| Calibration | `Services/Devices/Calibration/` | `DeviceCalibration` | `MQTTCalibration` | 校正命令、ファイル、結果 |
| Motor | `Services/Devices/Motor/` | `DeviceMotor` | `MQTTMotor` | 原点復帰、移動、位置取得 |
| Flow | `Services/Devices/FlowDevice/` | `DeviceFlowDevice` | `MQTTFlowDevice` | Flow デバイスサービス |
| ThirdPartyAlgorithms | `Services/Devices/ThirdPartyAlgorithms/` | `DeviceThirdPartyAlgorithms` | `MQTTThirdPartyAlgorithms` | 外部アルゴリズム接続 |

## 新規サービス追加手順

1. 既存の `ServiceTypes` を再利用できるか確認します。
2. `Config* : DeviceServiceConfig` を追加し、旧 JSON の復元を壊さないようにします。
3. `Device* : DeviceService<Config*>` を追加し、コンストラクタで `DService = new MQTT*(Config)` を作成します。
4. `GetDeviceInfo()`、必要に応じて `GetDisplayControl()` と `GetMQTTService()` を実装します。
5. `MQTT* : MQTTDeviceService<Config*>` を追加します。顧客判定はここに書きません。
6. `DeviceServiceFactoryRegistry.RegisterDefaults()` に登録します。
7. UI、MQTT、Flow またはプロジェクトパッケージから選択できることを確認します。

## 受け入れ確認

- デバイスツリーにサービスが表示され、再起動後も復元される。
- 設定のエクスポート、インポート、リセット、保存ができる。
- `SendTopic` / `SubscribeTopic` と `MsgID` の応答照合が正しい。
- 情報パネルと表示パネルが例外なく開く。
- 依存する Flow ノードまたはプロジェクトが正しいデバイスを選択できる。

## 関連ドキュメント

- [Engine デバイスサービスチェーン](../../04-api-reference/engine-components/device-service-chain.md)
- [MQTT メッセージ処理](./mqtt.md)
- [Engine ランタイムオブジェクトマップ](../../04-api-reference/engine-components/runtime-object-map.md)
- [テストと検証の引き継ぎ](../testing.md)
