# Matching テンプレートマッチング

このページでは `Engine/ColorVision.Engine/Templates/Matching/` のテンプレート、手動実行 UI、Flow ノード、AOI 結果表示を説明します。Matching は `MatchTemplate` をアルゴリズムサービスへ送り、返された AOI 結果を四点ポリゴンとして描画します。

## 対象範囲

| 項目 | 現在の実装 |
| --- | --- |
| テンプレートクラス | `TemplateMatch : ITemplate<MatchParam>, IITemplateLoad` |
| パラメータクラス | `MatchParam : ParamModBase` |
| テンプレートコード | `MatchTemplate` |
| 辞書 ID | `TemplateDicId = 34` |
| 手動入口 | `AlgorithmMatching` |
| UI | `DisplayMatching.xaml(.cs)` |
| MQTT イベント | `MQTTAlgorithmEventEnum.Event_MatchTemplate` |
| Flow ノード | `AlgorithmTMNode` |
| 結果種別 | `ViewResultAlgType.AOI` |
| 結果テーブル | `t_scgd_algorithm_result_detail_aoi` |
| 結果 handler | `ViewHandleMatching` |

## パラメータ

| パラメータ | 既定値 | 説明 |
| --- | --- | --- |
| `MinReducedArea` | `256` | サンプリング細かさ。説明範囲は `64 ~ 2048`。 |
| `ToleranceAngle` | `0` | 角度誤差。説明範囲は `0-180`。 |
| `Similarity` | `0.7` | 類似度しきい値。説明範囲は `0-1`。 |
| `MaxOverlapRatio` | `0` | 最大重なり率。説明範囲は `0-0.8`。 |
| `TargetNumber` | `70` | 対象数。 |

`TemplateFile` は `MatchParam` の項目ではなく、`AlgorithmMatching` と `AlgorithmTMNode` の実行時パラメータです。引き継ぎではパラメータテンプレートとテンプレートファイルを分けて記録します。

## 実行チェーン

手動 UI では、パラメータテンプレート、`TemplateFile`、ローカル画像、サービス Raw/CIE、またはバッチ番号を選び、`AlgorithmMatching.SendCommand(...)` を呼びます。要求には `ImgFileName`、`FileType`、`DeviceCode`、`DeviceType`、`TemplateFile`、`TemplateParam` が入ります。

Flow では `AlgorithmTMNode` を使い、`operatorCode` は `MatchTemplate` です。`TMParam` で `TemplateFile` と画像パラメータを送ります。

現在の XAML ではテンプレート ComboBox の `SelectedIndex` が `TemplatePoiSelectedIndex` にバインドされていますが、送信側は `TemplateSelectedIndex` を読みます。UI で選んだテンプレートが反映されない場合、まずここを確認します。

## 結果表示

`ViewHandleMatching` は `ViewResultAlgType.AOI` を処理します。`AlgResultAoiDao.GetAllByPid(result.Id)` で明細を読み、原画像を開き、四角の座標から凸包を作り、青い `DVPolygon` を描画します。表にはスコア、角度、中心点、四隅座標を表示します。

現在の `Load(...)` は `result.ViewResults != null` の場合だけ DAO を読みます。履歴結果に AOI 明細が出ない場合は、呼び出し側の初期化とこの条件を確認してください。

## トラブルシュート

| 現象 | 先に確認すること |
| --- | --- |
| サービスが実行されない | `DeviceCode`、`DeviceType`、`Event_MatchTemplate`、サービス状態。 |
| テンプレートファイル無効 | `TemplateFile` の存在とサービス側アクセス。 |
| パラメータが効かない | ComboBox バインド、`TemplateSelectedIndex`、`TemplateMatch.Params`。 |
| 結果が空 | 主結果種別 `AOI` と詳細テーブル `pid`。 |
| overlay 位置が違う | 四隅座標、元画像、倍率、座標系。 |

## 関連ページ

- [Engine 結果表示とプロジェクト引き継ぎチェーン](../../engine-components/result-handoff-chain.md)
- [Engine テンプレートと Flow チェーン](../../engine-components/template-flow-chain.md)
- [ROI プリミティブ](../primitives/roi.md)
- [現在のアルゴリズムテンプレート網羅表](../current-algorithm-template-coverage.md)
