# FindLightArea 発光領域テンプレート

このページは `Engine/ColorVision.Engine/Templates/FindLightArea/` の実際の引き継ぎチェーンを説明します。これは汎用 ROI SDK ではなく、テンプレート パラメータ、入力画像、MQTT アルゴリズム要求、発光領域点、画像オーバーレイをつなぐ業務テンプレートです。

## スコープ

| 項目 | 現在の実装 |
| --- | --- |
| テンプレート コード | `FindLightArea` |
| テンプレート クラス | `TemplateRoi : ITemplate<RoiParam>, IITemplateLoad` |
| パラメータ クラス | `RoiParam` |
| 実行入口 | `AlgorithmRoi`、表示名「發光區定位1」 |
| UI パネル | `DisplayRoi.xaml(.cs)` |
| MQTT イベント | `MQTTAlgorithmEventEnum.Event_LightArea2_GetData` |
| 結果ハンドラ | `ViewHandleFindLightArea` |
| 結果テーブル | `t_scgd_algorithm_result_detail_light_area` |

## ソース入口

| ファイル | 引き継ぎ用途 |
| --- | --- |
| `TemplateRoi.cs` | `FindLightArea` を登録し、`TemplateDicId = 31` を設定し、`MysqlRoi` で辞書を復元する。 |
| `ROIParam.cs` | `Threshold`、`Times`、`SmoothSize` を定義する。 |
| `AlgorithmRoi.cs` | アルゴリズム要求を組み立て、MQTT コマンドを送る。 |
| `DisplayRoi.xaml.cs` | テンプレート、画像入力、バッチ/Raw/ローカルファイル、実行操作を扱う。 |
| `AlgResultLightAreaDao.cs` | 結果モデル、ロード、画像オーバーレイ、一覧表示を定義する。 |
| `MysqlRoi.cs` | MySQL 辞書と既定テンプレートを復元する。 |

## 実行チェーン

1. `TemplateRoi` がテンプレート システムに検出され、`TemplateControl` に登録される。
2. UI で `TemplateRoi.Params` から `RoiParam` を選ぶ。
3. `DisplayRoi` はバッチ番号、Raw/CIE ファイル、ローカル画像を受け取る。
4. 拡張子は `Raw`、`CIE`、`Tif`、`Src` に変換され、`HistoryFilePath` があればフルパスに置き換える。
5. `AlgorithmRoi.SendCommand(...)` は `ImgFileName`、`FileType`、`DeviceCode`、`DeviceType`、`TemplateParam` を送る。
6. コマンドは `Event_LightArea2_GetData` に発行される。
7. `ViewHandleFindLightArea` が `LightArea` / `FindLightArea` 結果を処理する。

## パラメータ

| パラメータ | 既定値 | 引き継ぎメモ |
| --- | --- | --- |
| `Threshold` | `1` | 発光領域しきい値。変更時は画像種類と露光条件も記録する。 |
| `Times` | `1` | アルゴリズム側の処理回数。詳細意味はサービス側で解釈する。 |
| `SmoothSize` | `1` | 平滑サイズ。点一覧だけでなく凸包結果も確認する。 |

## 結果表示

`AlgResultLightAreaModel` は `PosX`、`PosY`、`Pid` を保持します。表示時は `GrahamScan.ComputeConvexHull(...)` で凸包を作り、透明な青い `DVPolygon` として画像に描画します。

注意点:

- 点一覧と凸包は同じものではありません。凸包が異常なら入力画像と ROI パラメータを先に確認します。
- 現在の `SideSave(...)` はファイルを作りますが点行を書きません。安定した CSV エクスポートとは見なさないでください。

## トラブルシュート

| 現象 | 先に確認するもの |
| --- | --- |
| テンプレートが空 | アセンブリロード、`IITemplateLoad`、`TemplateDicId = 31` の辞書復元。 |
| 画像を読めない | `ImgFileName`、`FileType`、履歴パス、デバイス `Code/Type`。 |
| 結果点がない | 結果タイプと `t_scgd_algorithm_result_detail_light_area` の `Pid`。 |
| オーバーレイが異常 | `Threshold`、`Times`、`SmoothSize`、入力画像、凸包入力点。 |

## 引き継ぎチェック

- パラメータ変更時は `ROIParam.cs`、`MysqlRoi.cs`、現場推奨値を同時に更新する。
- 実行イベント変更時は `AlgorithmRoi.SendCommand(...)`、Flow ノード説明、このページを更新する。
- 結果構造変更時は結果テーブル、表示列、エクスポートを更新する。
- プロジェクトが結果を使う場合、点、凸包、画像領域のどれを使うか明記する。

## 続けて読む

- [ROI プリミティブ](../primitives/roi.md)
- [OpenCV 統合](../../../02-developer-guide/engine-development/opencv-integration.md)
- [結果引き継ぎチェーン](../../engine-components/result-handoff-chain.md)
- [現在のアルゴリズムテンプレートカバレッジ](../current-algorithm-template-coverage.md)
