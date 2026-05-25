#ROI

このページでは、現在のウェアハウスに実際に存在する ROI 関連のプリミティブのみを説明します。「統合 ROI モジュール設計図」の古いドラフトは維持されなくなりました。

## まず、現在のウェアハウスの ROI が実際にいくつのブランチに分割されているかを見てみましょう。

現在のソース コードのステータスによると、ROI は別のディレクトリにある統合ライブラリではありませんが、少なくとも 3 つの関連ブランチがあります。

1. クラシックな発光エリア位置決めテンプレート (`Templates/FindLightArea` にあります)
2. 画像トリミング JSON テンプレート (`Templates/Jsons/ImageROI` にあります)
3. ARVR の `SFR_FindROI` JSON テンプレート (`Templates/Jsons/SFRFindROI` にあります)

したがって、このページは「グローバル ROI 抽象クラスの説明」というよりは「ROI ポータル マップ」に似ています。

## 現時点で最も重要なファイル

- `Engine/ColorVision.Engine/Templates/FindLightArea/TemplateRoi.cs`
- `Engine/ColorVision.Engine/Templates/FindLightArea/ROIParam.cs`
- `Engine/ColorVision.Engine/Templates/FindLightArea/AlgorithmRoi.cs`
- `Engine/ColorVision.Engine/Templates/FindLightArea/DisplayRoi.xaml.cs`
- `Engine/ColorVision.Engine/Templates/Jsons/ImageROI/TemplateImageROI.cs`
- `Engine/ColorVision.Engine/Templates/Jsons/ImageROI/AlgorithmImageROI.cs`
- `Engine/ColorVision.Engine/Templates/Jsons/SFRFindROI/TemplateSFRFindROI.cs`
- `Engine/ColorVision.Engine/Templates/Jsons/SFRFindROI/AlgorithmSFRFindROI.cs`

## 従来の ROI チェーンは現在どのようなものですか?

### テンプレートエントリ

現在の古典的な ROI は、実際には、古いドキュメントに記載されている `Templates/ROI` ではなく、コードの `FindLightArea` グループに分類されます。

`TemplateRoi` の実装特性は明らかです。

- `Name = FindLightArea`
- `Code = FindLightArea`
- `TemplateDicId = 31`
- `GetMysqlCommand()` 経由で `MysqlRoi` を返す

したがって、このチェーンは現時点では本質的に「発光ゾーン位置決めテンプレート」であり、システム全体の統一された ROI 定義ではありません。

### パラメトリックモデル

`RoiParam` は現在非常に単純で、次の 3 つのパラメータのみを公開しています。

- `Threshold`
- `Times`
- `SmoothSize`

これは、古いドラフトの一般的な長方形 ROI または多角形 ROI API と同じものではありません。これは、抽象的な幾何学的オブジェクトというよりは、特定のアルゴリズムのしきい値テンプレートに似ています。

### 実行と UI

`AlgorithmRoi` は以下を担当します。

- `TemplateRoi`の編集ウィンドウを開きます
- `DisplayRoi` を入手
- `Event_LightArea2_GetData` リクエストのアセンブル

`DisplayRoi` は、現在の実際のユーザー入力プロセスを担当します。

- テンプレートを選択します
- 画像ソースサービスの選択
- バッチ番号、元のファイル、ローカル画像の 3 つの入力をサポートします。
- Raw ファイルリストを取得し、直接開くことをサポートします

これは、現在のクラシック ROI が別個の描画コンポーネントではなく、「発光領域検出アルゴリズムのフロントエンド ホスト」に近いことを示しています。

## 2 つの JSON ROI ブランチ

### イメージROI

`TemplateImageROI` は、現在次の JSON テンプレート ブランチです。

- `Code = Image.ROI`
- `TemplateDicId = 52`
- `IsUserControl = true`

これは、`Image.ROI` イベントを発行する `EditTemplateJson` を介して構造化されたクリッピング パラメーターを伝達します。

このチェーンは画像のトリミング設定に関するものであり、古典的な発光領域テンプレートのレプリカではありません。

### SFR_FindROI

`TemplateSFRFindROI` も JSON テンプレート ブランチであり、現在は次のようになります。

- `Code = ARVR.SFR.FindROI`
- `TemplateDicId = 36`
- `IsUserControl = true`

説明テキストに `SfrRoiParam` 構造のヒントが明確に示されています。 `AlgorithmSFRFindROI` には、JSON テンプレート自体に加えて、追加の `POITemplateParam` も付属し、その後 `ARVR.SFR.FindROI` がリリースされます。

これは、ARVR での「ROI の検索」が単なる ROI テンプレートではなく、ROI と POI をリンクするアルゴリズム チェーンであることを示しています。

## 現在、最もよくある間違いのいくつかが犯されています

### ROI は統合されたベース ライブラリではありません

現在のウェアハウス内の ROI 関連の実装は、クラシック パラメーター テンプレートと JSON テンプレートの 2 つのパスに分散されています。すべてのシナリオを担当する統合された `ROI` ルート モジュールはありません。

### クラシック ROI は現在、主に発光領域の位置を指します。

`FindLightArea` をメインアンカーとして使用しない場合、このページは存在しない「ユニバーサル ROI SDK」として簡単に作成できます。

### JSON ROI とクラシック ROI は同じ構成モデルのセットではありません

`TemplateImageROI` と `TemplateSFRFindROI` は両方とも JSON テンプレート ホストですが、`TemplateRoi` は従来のパラメーター テンプレートです。 3 つを 1 つのパラメータ テーブルに混在させることはできません。

### 一部の ROI チェーンはすでに POI にバインドされています

`AlgorithmSFRFindROI` には明示的に `TemplatePoi` が必要です。現在の ARVR チェーンでは、ROI と POI は完全に別個の 2 つの概念レイヤーではなくなりました。

## 推奨される読む順序

1. `Engine/ColorVision.Engine/Templates/FindLightArea/TemplateRoi.cs`
2. `Engine/ColorVision.Engine/Templates/FindLightArea/AlgorithmRoi.cs`
3. `Engine/ColorVision.Engine/Templates/FindLightArea/DisplayRoi.xaml.cs`
4. `Engine/ColorVision.Engine/Templates/Jsons/ImageROI/TemplateImageROI.cs`
5. `Engine/ColorVision.Engine/Templates/Jsons/SFRFindROI/TemplateSFRFindROI.cs`

## 続きを読む

- [POIプリミティブ](./poi.md)
- [POI テンプレート](../templates/poi-template.md)
- [ARVR テンプレート](../templates/arvr-template.md)