# ImageCropping 画像クロッピングテンプレート

`ImageCropping/` は旧強型テンプレート、手動アルゴリズム画面、Flow ノード、クロッピング結果表示を担当します。`Jsons/ImageROI` とは別のモジュールです。

## クイック情報

| 項目 | 値 |
| --- | --- |
| テンプレートクラス | `TemplateImageCropping` |
| パラメータクラス | `ImageCroppingParam` |
| `TemplateDicId` | `32` |
| Code | `ImageCropping` |
| 手動アルゴリズム | `AlgorithmImageCropping` |
| MQTT イベント | `Event_Image_Cropping` |
| Flow operator | `OLED.GetRIAand` |
| 結果タイプ | `ViewResultAlgType.Image_Cropping` |
| 結果 handler | `ViewHandleImageCropping` |

## パラメータと ROI

`ImageCroppingParam` の永続フィールドは現在 2 つです。

| フィールド | 意味 |
| --- | --- |
| `UnEgde` | エッジ関連のクロッピングパラメータ。綴りはソースに合わせます |
| `O_Index` | 出力順/インデックス。復旧 SQL の既定値は `[0,1,2,3]` |

`Point1` から `Point4` は `AlgorithmImageCropping` の実行時 ROI 点です。手動実行時に `ROI` 配列として送信され、テンプレート項目として保存されません。

## Flow と結果

手動実行は `ImgFileName`、`FileType`、`DeviceCode`、`DeviceType`、`TemplateParam`、`ROI` を `Event_Image_Cropping` へ送ります。

Flow には 2 つの経路があります。

- 汎用 `AlgorithmNode`: `AlgorithmType.图像裁剪` が `operatorCode = "OLED.GetRIAand"` に対応します。
- `OLEDImageCroppingNode`: `图像裁剪2` は `IN_IMG` と `IN_ROI` を持ち、上流 ROI の master id を `ROI_MasterId` に入れます。

`ViewHandleImageCropping` は `ViewResultAlgType.Image_Cropping` を処理し、`AlgResultImageDao` から明細を読み、`file_name`、`order_index`、`FileInfo` を表示します。

## 引き継ぎ注意

- 本ページは強型 `ImageCropping` を説明し、JSON `ImageROI` ではありません。
- 手動の四点 ROI は実行時入力であり、テンプレート永続項目ではありません。
- Flow の二入力ノードは上流 ROI 結果に依存します。
- `SideSave(...)` は `selectedPath` を CSV パスと画像ディレクトリの両方のように扱うため、エクスポートは現場で確認してください。

## 関連ページ

- [結果引き継ぎチェーン](../../engine-components/result-handoff-chain.md)
- [テンプレートと Flow チェーン](../../engine-components/template-flow-chain.md)
- [ROI プリミティブ](../primitives/roi.md)
- [JSON テンプレート](./json-templates.md)
