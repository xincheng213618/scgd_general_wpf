# DataLoad データロードテンプレート

このページでは `Engine/ColorVision.Engine/Templates/DataLoad/` と Flow のデータロードノードを説明します。`DataLoad` は画像アルゴリズムではなく、専用の結果 handler もありません。デバイス、シリアル番号、結果種別、ZIndex を使ってデータ取得条件をサービスへ渡す役割です。

## 対象範囲

| 項目 | 現在の実装 |
| --- | --- |
| テンプレートクラス | `TemplateDataLoad : ITemplate<DataLoadParam>, IITemplateLoad` |
| パラメータクラス | `DataLoadParam : ParamModBase` |
| テンプレートコード | `DataLoad` |
| 辞書 ID | `TemplateDicId = 22` |
| Flow ノード | `AlgDataLoadNode`、`AlgDataLoadNode2` |
| Flow 操作コード | `operatorCode = "DataLoad"` |
| コンフィギュレータ | `AlgDataLoadNodeConfigurator` |
| 結果 handler | 現在 `ViewHandleDataLoad` はありません |

## パラメータ

| パラメータ | 型 | 説明 |
| --- | --- | --- |
| `DeviceCode` | `string?` | データ取得元デバイス Code。 |
| `ResultType` | `CVCommCore.CVResultType` | 取得する結果種別。 |
| `SerialNumber` | `string?` | バッチまたはシリアル番号。 |
| `ZIndex` | `int` | Flow またはサービス側のデータ階層。 |

`AlgDataLoadNode` はテンプレート駆動で、DataLoad テンプレートを選択して `TemplateParam` を送ります。`AlgDataLoadNode2` は明示パラメータ駆動で、`DeviceCode`、`SerialNumber`、`ResultType` 文字列、`ZIndex` を `DataLoadInput` として送ります。

## 境界

DataLoad をファイルインポート機能として説明しないでください。現在の実装はファイル選択やファイル解析を行わず、データの位置を特定するパラメータをサービスまたは Flow チェーンへ渡します。

## トラブルシュート

| 現象 | 先に確認すること |
| --- | --- |
| Flow がテンプレートを見つけない | `TemplateDataLoad.Params` と `TemplateDicId = 22`。 |
| 間違ったバッチを読む | `SerialNumber` の出所。 |
| 間違ったデバイスを読む | `DeviceCode/DataDeviceCode` と対象サービス。 |
| 結果種別が違う | `CVResultType.ToString()` がサービス期待値か。 |
| 階層が違う | `ZIndex/DataZIndex` と Flow ノード順序。 |

## 引き継ぎチェックリスト

- `AlgDataLoadNode` と `AlgDataLoadNode2` のどちらを使うか明記します。
- デバイス Code、結果種別、シリアル番号の出所、ZIndex を記録します。
- サービスプロトコル変更時は `DataLoadData`、`DataLoadData2`、Flow 文書を更新します。
- 本当のファイルインポートが必要な場合は、明確なファイルパラメータを追加します。

## 関連ページ

- [Engine テンプレートと Flow チェーン](../../engine-components/template-flow-chain.md)
- [フローエンジン](./flow-engine.md)
- [現在のアルゴリズムテンプレート網羅表](../current-algorithm-template-coverage.md)
