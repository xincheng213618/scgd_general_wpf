# Engine MQTT メッセージ処理引き継ぎ

このページは Engine 層の現在の MQTT モデルを説明します。現在の主経路は、各モジュールが個別に MQTT client を持つ形ではありません。`MQTTControl` が接続、購読、送信、trace を管理し、デバイスサービスは `MQTTServiceBase` / `MQTTDeviceService<T>` で命令を送って応答を待ちます。

## 現在の階層

| 階層 | 主要オブジェクト | 役割 |
| --- | --- | --- |
| グローバル接続 | `MQTTControl` | `IMqttClient`、接続、再接続、購読キャッシュ、送信、最近 200 件の trace |
| 設定 | `MQTTSetting`、`MQTTConfig` | Host、Port、UserName、UserPwd、安全保存 |
| 起動 | `MqttInitializer` | ホスト初期化時に MQTT 接続 |
| デバイス命令 | `MQTTServiceBase` | `MsgRecord`、`MsgSend`、`MsgID` による `MsgReturn` 照合、タイムアウト |
| デバイス設定 | `MQTTDeviceService<T>` | `SendTopic` と `SubscribeTopic` を設定から読む |
| Flow MQTT | `FlowEngineLib/MQTT/` | visual Flow の publish/subscribe hub |

## 命令チェーン

1. デバイス UI、Flow、またはプロジェクトが具体 `MQTT*` メソッドを呼びます。
2. `MQTT*` が `MsgSend` を作成し、`EventName` とパラメータを設定します。
3. `MQTTServiceBase.PublishAsyncClient()` が `MsgID`、`DeviceCode`、`Token`、`ServiceName` を補完します。
4. `MsgRecord` を作成し、メッセージ DB に保存し、タイムアウトタイマーを開始します。
5. `MQTTControl.PublishAsyncClient()` が `SendTopic` に送信します。
6. 応答は `SubscribeTopic` に届き、`MsgID` で待機中の記録に照合されます。

## 変更箇所

| 目的 | 主なファイル |
| --- | --- |
| broker 設定 | `MQTTSetting.cs`、`MQTTConnect.xaml.cs` |
| 接続と再接続 | `MQTTControl.cs`、`MqttInitializer.cs` |
| デバイス命令追加 | `Services/Devices/*/MQTT*.cs` |
| topic 変更 | `DeviceServiceConfig` とデバイス設定 UI |
| 応答処理変更 | `MQTTServiceBase` または具体 `MQTT*` |
| Flow MQTT ノード | `FlowEngineLib/MQTT/` |

## 受け入れ確認

- MQTT 設定を保存後、再起動しても接続できる。
- SEND/RECV が trace またはログで確認できる。
- `MsgRecord` に送信時刻、受信時刻、状態、応答が残る。
- 失敗応答を成功扱いしない。
- タイムアウト後に待機状態が片付く。
- 再接続後にキャッシュ済み topic が再購読される。

## 関連ドキュメント

- [Engine デバイスサービスチェーン](../../04-api-reference/engine-components/device-service-chain.md)
- [Engine 業務シナリオ引き継ぎ](../../04-api-reference/engine-components/business-scenario-playbook.md)
- [FlowEngineLib](../../04-api-reference/engine-components/FlowEngineLib.md)
- [テストと検証の引き継ぎ](../testing.md)
