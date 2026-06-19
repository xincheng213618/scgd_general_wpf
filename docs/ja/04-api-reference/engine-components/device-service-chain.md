# Engine デバイスサービスチェーン

デバイスサービスチェーンは、DB 資源を表示可能、制御可能、Flow から利用可能な実行時サービスへ変換します。

## チェーン概要

```text
SysDictionaryModel / SysResourceModel
  -> ServiceTypes
  -> DeviceServiceFactoryRegistry
  -> DeviceService<TConfig>
  -> display control / MQTTDeviceService / Flow NodeConfigurator
```

## 主要クラスとディレクトリ

| 場所 | 役割 |
| --- | --- |
| `Services/ServiceManager.cs` | 作成済みデバイスサービスの保持 |
| `Services/Type/TypeService.cs` | 資源 Type とサービス種別の対応 |
| `Services/DeviceService.cs` | デバイスサービス基底 |
| `Services/DeviceServiceFactory.cs` | サービス作成 Factory |
| `Services/DeviceServiceFactoryRegistry.cs` | Factory 登録と検索 |
| `Services/MQTTDeviceService.cs` | MQTT 命令と状態の基底 |
| `Services/Devices/**` | 各デバイスの Service、Config、MQTT 実装 |

## デバイス追加チェックリスト

1. `ServiceTypes` を追加または確認します。
2. `DeviceServiceConfig` を継承する設定クラスを追加します。
3. `DeviceService<TConfig>` を継承するサービスを追加します。
4. `SysResourceModel.Type` から作れるよう Factory を登録します。
5. UI 表示と設定画面を追加します。
6. MQTT 命令、状態応答、タイムアウトログを追加します。
7. Flow で使うなら `NodeConfigurator` の選択条件を追加します。
8. 文書、テスト手順、プロジェクト利用説明を更新します。

## よくある故障

| 現象 | 優先確認 |
| --- | --- |
| DB に資源があるが UI に出ない | Type 対応、Factory 登録、サービス生成 |
| UI に出るが Flow で選べない | `NodeConfigurator` の種別フィルタ |
| Flow で選べるが命令失敗 | MQTT Topic、Token、命令形式、オンライン状態 |
| 状態が更新されない | 状態イベント、MQTT 応答、UI バインディング |
| 複数台で取り違える | Resource ID、Code、Name、サービス辞書 key |

## 境界

デバイスサービスはデバイス能力をシステムに公開します。テンプレートはいつ使うか、Flow はどう編成するか、結果表示はどう見せるか、プロジェクトは顧客へどう渡すかを担当します。
