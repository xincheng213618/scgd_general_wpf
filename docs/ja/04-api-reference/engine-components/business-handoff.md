# Engine 業務引き継ぎマニュアル

このマニュアルは Engine の主要業務ロジックを引き継ぐためのものです。読み終えた人が、デバイス生成、テンプレートから Flow への接続、結果表示、プロジェクト出力まで説明できる状態を目指します。

## Engine を一言で言うと

`ColorVision.Engine` は業務編成層です。単体のアルゴリズム DLL でも単体のデバイスドライバでもなく、DB 資源、デバイスサービス、MQTT、テンプレート、Flow、アルゴリズム結果、プロジェクト納品をつなぎます。

## 主なディレクトリ

| ディレクトリ | 責任 |
| --- | --- |
| `Engine/ColorVision.Engine/Services/` | デバイスサービス、資源マッピング、MQTT、バッチ、結果サービス |
| `Engine/ColorVision.Engine/Templates/` | テンプレート管理、Flow、アルゴリズムテンプレート、結果表示 |
| `Engine/FlowEngineLib/` | Flow ノードモデル、開始/終了ノード、実行制御 |
| `Engine/ColorVision.FileIO/` | CVRAW、CVCIE などのファイル I/O |
| `Engine/cvColorVision/` | OpenCV とネイティブ機能のラップ |
| `Projects/*` | 顧客ルール、Recipe、Fix、出力、Socket/MES |

## デバイスサービス

DB 資源は `SysResourceModel` として読み込まれ、`ServiceTypes` により Factory を探します。Factory が `DeviceService<TConfig>` を生成し、`ServiceManager` が管理します。リモート制御が必要なデバイスは `MQTTDeviceService` または `MQTTControl` に接続します。

デバイス追加時は Type、Config、Service、Factory、表示画面、MQTT、Flow ノード設定器をすべて確認します。一部だけ実装すると、UI には出るが Flow で選べない、または Flow では選べるが命令できない状態になります。

## テンプレートと Flow

テンプレートの入り口は `TemplateControl` と `TemplateModel<T>` です。Flow テンプレートは `TemplateFlow` が扱い、実行時は `FlowEngineControl`、`FlowControl`、各ノードに進みます。ノード表示とパラメータ設定は主に `Templates/Flow/NodeConfigurator/` です。

引き継ぎ時はテンプレート ID、Code、ノード種別、デバイス種別、結果 key を確認します。ここがずれると、Flow は動くが結果が合わない状態になりやすいです。

## 結果表示と納品

結果は `AlgResultMasterModel` と明細結果に保存され、`ViewResultAlg`、`IResultHandleBase`、`IViewResult` を通って表示モデルになります。画像オーバーレイは `UI/ColorVision.ImageEditor/Draw/` にあります。

プロジェクトは Engine 結果を読み、顧客判定、補正、`ObjectiveTestResult`、CSV、DB、Socket、MES の形式へ変換します。Engine が出すべき結果をプロジェクト側で偽造しないようにします。

## 変更場所

| 要望 | 優先変更先 |
| --- | --- |
| デバイス追加 | `Services/Devices/`, `Services/Type/TypeService.cs` |
| デバイス命令追加 | `Services/Devices/*/MQTT*.cs`, `MQTT/` |
| テンプレート項目追加 | `Templates/**`, 対応するテンプレートモデル |
| Flow ノード追加 | `FlowEngineLib/`, `Templates/Flow/Nodes/`, `NodeConfigurator/` |
| 結果レイヤ追加 | `Templates/**/ViewHandle*.cs`, `UI/ColorVision.ImageEditor/Draw/` |
| 顧客判定 / 出力 | `Projects/<Project>/Recipe`, `Fix`, `Process`, exporter |

## 調査手順

1. 資源、テンプレート、バッチ、結果 ID の存在を確認します。
2. Engine ログと MQTT ログを確認します。
3. `ServiceManager` にサービスがあるか確認します。
4. Flow ノードが正しいデバイスとテンプレートを選んでいるか確認します。
5. DAO に主結果と明細があるか確認します。
6. ViewHandle が命中しているか確認します。
7. 最後にプロジェクト側の項目変換と出力を確認します。

## 保守ルール

Engine の業務チェーンを変更したらこの章も更新します。デバイスは [デバイスサービスチェーン](./device-service-chain.md)、Flow やテンプレートは [テンプレートと Flow チェーン](./template-flow-chain.md)、結果や顧客項目は [結果表示とプロジェクト引き継ぎチェーン](./result-handoff-chain.md) に反映します。
