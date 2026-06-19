# Engine 業務シナリオ引き継ぎプレイブック

このページは、よくある要望や不具合から Engine の該当コードへ進むための手順です。最初に次の三点を決めます。

1. 入り口は UI、Flow、プロジェクト、Socket/MES、スケジューラ、MQTT のどれか。
2. 所属層は資源/デバイス、テンプレート/Flow、リモートサービス、結果表示、プロジェクト変換、出力のどれか。
3. 証拠はログ、資源 ID、テンプレート ID、バッチ番号、結果 ID、応答パケットのどれか。

## A. DB にデバイスがあるが UI または Flow に出ない

まず `SysResourceModel` が存在し、Type が壊れていないかを確認します。次に `Services/Type/TypeService.cs` が Type を正しい `ServiceTypes` に対応させているかを見ます。さらに `DeviceServiceFactoryRegistry` に Factory があり、`ServiceManager.DeviceServices` にサービスが作られているか確認します。

Flow 側では `Templates/Flow/NodeConfigurator/` の種別フィルタも確認します。デバイスが無いように見える問題は、サービス生成ではなく設定器のフィルタが原因のことがあります。

## B. 新しいデバイスを追加する

最小チェックリスト:

- `ServiceTypes` に種別を追加します。
- `ConfigXxx : DeviceServiceConfig` を追加します。
- `DeviceXxx : DeviceService<ConfigXxx>` を追加します。
- Factory 登録を追加します。
- 表示コントロールと設定画面を追加します。
- Flow で使う場合は `NodeConfigurator` と選択条件を追加します。
- リモート制御が必要なら `MQTTDeviceService`、Topic、応答、タイムアウトログを追加します。
- この章とプロジェクト利用説明を更新します。

## C. テンプレートパラメータを変更する

対象が JSON、POI、Flow、デバイス動作テンプレートのどれかを先に判断します。入り口は `Templates/Jsons/`、`Templates/POI/`、`Templates/Flow/`、各デバイスサービス配下にあります。

`Code`、`Title`、`TemplateDicId`、既定値、属性説明、旧データの逆シリアライズを壊さないことが重要です。Flow ノードが使う項目なら、ノード設定器と結果表示も同時に更新します。

## D. Flow ノードを追加または変更する

Flow は二層で見ます。`FlowEngineLib/` は実行モデル、`ColorVision.Engine/Templates/Flow/` はメインアプリ側のテンプレート、設定器、画面接続です。

受け入れ確認:

- 既存 Flow を開く。
- ノードを追加して保存する。
- 閉じて再度開く。
- `.cvflow` をインポートする。
- 実行して `FlowCompleted` を確認する。
- 結果がバッチ、結果表示、プロジェクトから読める。

## E. 結果はあるが画像表示が出ない

まず `AlgResultMasterModel` と明細 DAO があるか確認します。次に `ViewResultAlg` が表示モデルを作れているか、`DisplayAlgorithmManager` が正しい `ViewHandleXxx` を選んでいるか、`IResultHandleBase.CanHandle` が命中しているかを見ます。

モデルが正しい場合は、画像パス、座標系、ROI、倍率、`UI/ColorVision.ImageEditor/Draw/` の描画オブジェクトを確認します。顧客判定はオーバーレイではなくプロジェクト側に置きます。

## F. プロジェクト結果が空、または項目が違う

Engine 結果が存在することを先に確認します。その後 `Projects/<Project>/Process` が読む key、`Recipe`、`Fix`、`ObjectiveTestResult`、exporter、Socket/MES 応答項目を確認します。

## G. リモートサービスに結果がない

MQTT 接続、Topic、サービストークン、命令順序、FileServer パス、結果 ID、DAO 保存、Flow 状態の順で確認します。コードは `Services/Devices/*/MQTT*.cs` と `MQTT/` を優先します。

## H. Socket/MES 応答が違う

共通 `SocketProtocol` か `Projects/<Project>/SocketControl` の顧客プロトコルかを分けます。EventName、SN、テンプレート、Flow、結果 ID、応答項目、エラーコードを確認します。

## クラス早見表

| 目的 | 最初に見るもの |
| --- | --- |
| デバイス生成 | `ServiceManager`, `DeviceServiceFactoryRegistry` |
| テンプレート読込 | `TemplateControl`, `TemplateModel<T>` |
| Flow 実行 | `TemplateFlow`, `FlowControl`, `NodeConfiguratorRegistry` |
| 結果表示 | `ViewResultAlg`, `IResultHandleBase`, `IViewResult` |
| プロジェクト判定 | `ObjectiveTestResult`, `Projects/<Project>/Process` |
