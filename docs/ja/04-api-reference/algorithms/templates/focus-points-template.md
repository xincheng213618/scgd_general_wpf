# FocusPoints フォーカスポイントテンプレート

`FocusPoints/` は旧系統の発光領域/フォーカスポイント検出用パラメータテンプレートです。二値化、ぼかし、モルフォロジー、面積/矩形フィルタ、ROI 境界を保存し、手動アルゴリズム画面または Flow ノードへ渡します。

## クイック情報

| 項目 | 値 |
| --- | --- |
| テンプレートクラス | `TemplateFocusPoints` |
| パラメータクラス | `FocusPointsParam` |
| `TemplateDicId` | `15` |
| Code | `focusPoints` |
| 手動アルゴリズム | `AlgorithmFocusPoints` |
| MQTT イベント | `Event_LightArea_GetData` |
| Flow operator | `FocusPoints` |
| メニュー入口 | `ExportFocusPoints` |

## パラメータ分岐

| グループ | フィールド | 引き継ぎ意味 |
| --- | --- | --- |
| `Binarize` | `Binarize`, `BinarizeThresh` | 二値化の有効化と閾値 |
| `Blur` | `Blur`, `BlurSize` | 平均フィルタの有効化とサイズ |
| `Erode` | `Erode`, `ErodeSize` | 収縮の有効化とサイズ |
| `Dilate` | `Dilate`, `DilateSize` | 膨張の有効化とサイズ |
| `Param` | `FilterRect`, `Width`, `Height` | 矩形フィルタと幅/高さ制限 |
| `FilterArea` | `FilterArea`, `MaxArea`, `MinArea` | 面積フィルタと上下限 |
| `Roi` | `Roi`, `Left`, `Right`, `Top`, `Bottom` | ROI 境界 |

ここでの ROI はテンプレート入力であり、結果 overlay の座標ではありません。結果点と POI 再利用は [ROI プリミティブ](../primitives/roi.md) と [POI プリミティブ](../primitives/poi.md) を参照してください。

## 実行チェーン

`DisplayFocusPoints` はテンプレート、画像ソース、必要ならバッチ番号を選択し、`AlgorithmFocusPoints.SendCommand(...)` が `ImgFileName`、`FileType`、`DeviceCode`、`DeviceType`、`TemplateParam` を送信します。イベント名は `MQTTAlgorithmEventEnum.Event_LightArea_GetData` です。

Flow では `AlgorithmType.发光区检测` が `operatorCode = "FocusPoints"` に対応します。同じ発光領域ノードは ROI、AA 検出、POI 保存テンプレートも公開するため、`FocusPoints/` フォルダだけで機能全体を判断しないでください。

## 引き継ぎ注意

- `TemplateDicId = 15` と `Code = "focusPoints"` が識別子です。
- このテンプレートは前処理閾値であり、プロジェクト判定ルールではありません。
- 手動実行は `Event_LightArea_GetData`、Flow は `FocusPoints` operator code を使います。
- `FocusPoints/` には専用 `ViewHandle*.cs` がなく、結果表示は発光領域、ROI、POI チェーンを追います。

## 関連ページ

- [FindLightArea 発光領域テンプレート](./find-light-area.md)
- [POI テンプレート](./poi-template.md)
- [テンプレートと Flow チェーン](../../engine-components/template-flow-chain.md)
- [現在のアルゴリズムテンプレートカバレッジ](../current-algorithm-template-coverage.md)
