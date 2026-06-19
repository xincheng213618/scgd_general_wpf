# Engine 結果表示とプロジェクト引き継ぎチェーン

このページは、Engine がアルゴリズム結果を表示する責任と、Projects が結果を顧客向けの判定/納品形式に変換する責任を分けて説明します。

## メインチェーン

```text
AlgResultMasterModel / detail DAO
  -> ViewResultAlg / IViewResult
  -> ViewHandleXxx / IResultHandleBase / IDisplayAlgorithm
  -> UI/ColorVision.ImageEditor/Draw overlay
  -> Projects/<Project>/Process / Recipe / Fix
  -> ObjectiveTestResult / export / Socket / MES
```

## Engine の表示責任

Engine は結果を追跡可能、検索可能、可視化可能にします。主結果、明細結果、画像パス、ROI または座標、結果種別、表示ハンドラを揃えます。

主な場所:

- `Templates/**/ViewHandle*.cs`
- `Abstractions/IResultHandlers.cs`
- `ViewResultAlg`
- `AlgResultMasterModel`
- `UI/ColorVision.ImageEditor/Draw/**`

関連トピック:

- [Compliance 結果引き継ぎ](../algorithms/templates/compliance-results.md): `ViewHandleComplianceY/XYZ/JND` と Y/XYZ/JND 判定結果。
- [Validate 判定ルールテンプレート](../algorithms/templates/validate-rules.md): 判定ルールの出所と `ValidateResult` の解釈。
- [BuzProduct 製品業務パラメータテンプレート](../algorithms/templates/buz-product-template.md): 製品詳細が `val_rule_temp_id` でルールを参照する流れ。
- [Matching テンプレートマッチング](../algorithms/templates/matching-template.md): `ViewHandleMatching`、AOI 明細、四点 overlay。
- [ImageCropping 画像クロッピングテンプレート](../algorithms/templates/image-cropping-template.md): `ViewHandleImageCropping` とクロップファイル明細。

## Projects の納品責任

プロジェクトは Engine 結果を読み、顧客ルールを適用します。

- Recipe パラメータ。
- Fix または補正ルール。
- 結果項目マッピング。
- `ObjectiveTestResult`。
- CSV、DB、Socket、MES 出力。

顧客判定を画像オーバーレイに書かず、Engine が出すべき結果をプロジェクト側で一時的に作らないようにします。

## 結果欠落の調査

| 現象 | 確認順序 |
| --- | --- |
| UI にオーバーレイが出ない | DAO -> `ViewResultAlg` -> `CanHandle` -> 画像パス -> Draw オブジェクト |
| オーバーレイ位置が違う | 座標系、ROI、倍率、元画像サイズ |
| プロジェクト結果が空 | Engine 結果 key、`Process` 読取、Recipe/Fix |
| Socket 応答が違う | `ObjectiveTestResult`、プロトコル項目、エラーコード |
| 出力項目が欠ける | exporter、項目マッピング、バッチ番号、結果 ID |

## 新しい結果種別の追加

1. 結果モデルと DAO を定義します。
2. アルゴリズム実行後に主結果と明細を保存します。
3. `IViewResult` または表示モデルを追加します。
4. `ViewHandleXxx` を追加し、`CanHandle` を実装します。
5. ImageEditor Draw のオーバーレイを追加します。
6. 必要なら `Projects/<Project>/Process` と `ObjectiveTestResult` のマッピングを追加します。
7. このページと該当プロジェクト文書を更新します。

## 関連ページ

- [Compliance 結果引き継ぎ](../algorithms/templates/compliance-results.md)
- [Validate 判定ルールテンプレート](../algorithms/templates/validate-rules.md)
- [BuzProduct 製品業務パラメータテンプレート](../algorithms/templates/buz-product-template.md)
