# JND テンプレート

このページは `Engine/ColorVision.Engine/Templates/JND/` の業務チェーンを説明します。JND テンプレート自体のパラメータは少ないですが、実行時は POI テンプレートも必要で、結果も POI 点ごとに表示・エクスポートされます。

## スコープ

| 項目 | 現在の実装 |
| --- | --- |
| テンプレート コード | `OLED.JND.CalVas` |
| テンプレート クラス | `TemplateJND : ITemplate<JNDParam>, IITemplateLoad` |
| パラメータ クラス | `JNDParam` |
| 依存テンプレート | `TemplatePoi` |
| 実行入口 | `AlgorithmJND`、表示名 `JND` |
| UI パネル | `DisplayJND.xaml(.cs)` |
| MQTT イベント | `MQTTAlgorithmEventEnum.Event_OLED_JND_CalVas_GetData` |
| 結果ハンドラ | `ViewHandleJND` |
| 主な結果タイプ | `Compliance_Math_JND`、`JND_CalVas` |

## ソース入口

| ファイル | 引き継ぎ用途 |
| --- | --- |
| `TemplateJND.cs` | JND テンプレートを登録し、`TemplateDicId = 30` と `Code = OLED.JND.CalVas` を設定する。 |
| `JNDParam.cs` | `CutOff` を定義する。 |
| `AlgorithmJND.cs` | JND と POI テンプレートを集め、要求を送る。 |
| `DisplayJND.xaml.cs` | JND テンプレート、POI テンプレート、画像、デバイスを選ぶ。 |
| `ViewHandleJND.cs` | 結果ロード、表表示、POI 描画、CSV と画像保存を扱う。 |
| `ViewRsultJND.cs` | POI 結果の JSON を `POIResultDataJND` に解析する。 |
| `MysqlJND.cs` | 辞書を復元し、既定 `CutOff = 0.3` を提供する。 |

## 実行チェーン

1. `TemplateJND` が `TemplateJND.Params` にロードされる。
2. `DisplayJND` は `TemplateJND.Params` と `TemplatePoi.Params` を同時にバインドする。
3. ユーザーは JND テンプレート、POI テンプレート、入力画像を選ぶ。
4. `AlgorithmJND.SendCommand(...)` は `ImgFileName`、`FileType`、`DeviceCode`、`DeviceType`、`TemplateParam`、`POITemplateParam` を送る。
5. コマンドは `Event_OLED_JND_CalVas_GetData` に発行される。
6. `ViewHandleJND` は `PoiPointResultDao` から点を読み、`ViewRsultJND` が `h_jnd` / `v_jnd` を解析する。

## パラメータと結果

| 項目 | 説明 |
| --- | --- |
| `CutOff` | 輪郭カットオフ係数。既定値 `0.3`。変更時は画像、POI テンプレート、サービス版を残す。 |
| `h_jnd` | 横方向 JND 結果。 |
| `v_jnd` | 縦方向 JND 結果。 |
| POI 点 | JND は `TemplatePoi` を消費するため、点変更は結果に直結する。 |

## プロジェクト境界

`ProjectShiyuan` は JND/POI エクスポートと JND 検証を使います。「JND CSV がある」だけでは PASS ではありません。プロジェクト側は `Compliance_Math_JND`、`Validate`、画像コピー、疑似カラー出力を追加で確認します。

関連ページ: [ProjectShiyuan](../../projects/project-shiyuan.md)。

## トラブルシュート

| 現象 | 先に確認するもの |
| --- | --- |
| POI 関連エラー | `TemplatePoi.Params` と `TemplatePoiSelectedIndex`。 |
| JND 結果が空 | 結果タイプと `PoiPointResultDao` の `Pid` データ。 |
| 点はあるが JND 値が空 | `PoiPointResultModel.Value` が `POIResultDataJND` に解析できるか。 |
| プロジェクト OK/NG が合わない | プロジェクト側 JND 検証。 |
| エクスポートパスが不自然 | `SideSave(...)` の `selectedPath` の意味。 |

## 引き継ぎチェック

- `CutOff` 変更時は `JNDParam.cs`、`MysqlJND.cs`、現場推奨値を更新する。
- POI 選択や座標系変更時は [POI テンプレート](./poi-template.md) とプロジェクト文書を更新する。
- 結果フィールド変更時は `ViewRsultJND.cs`、エクスポート列、受け入れ例を更新する。
- プロジェクトが JND 判定に依存する場合、最終 OK/NG の來源を明記する。

## 続けて読む

- [POI テンプレート](./poi-template.md)
- [POI プリミティブ](../primitives/poi.md)
- [ProjectShiyuan](../../projects/project-shiyuan.md)
- [結果引き継ぎチェーン](../../engine-components/result-handoff-chain.md)
- [現在のアルゴリズムテンプレートカバレッジ](../current-algorithm-template-coverage.md)
