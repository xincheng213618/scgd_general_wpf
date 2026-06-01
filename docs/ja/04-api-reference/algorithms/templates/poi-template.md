# POI テンプレート

このページでは、現在のウェアハウスに実際に存在する POI テンプレート ファミリについてのみ説明しており、「Detector Interface Encyclopedia + Pluggable Algorithm Sample」スタイルの古いドラフトは維持されていません。

## このテンプレート ファミリは現在何をしているのでしょうか?

現在のソース コードのステータスによると、POI は独立したテンプレートではなく、「ポイント セット データ」を中心とした一連のテンプレートとアルゴリズム ホストです。

- メインの POI テンプレートは、ポイント セット、寸法、構成を保存する役割を果たします。
- フィルタリング、補正、キャリブレーション、および出力には、それぞれ独自の関連テンプレートがあります。
- ランタイム アルゴリズムは、これらのテンプレートを MQTT リクエストに組み込む役割を果たします。
- フロー ノードといくつかの JSON アルゴリズムは、引き続き POI テンプレートを使用します。

したがって、このページで実際に説明するのは、「特定の POI 検出アルゴリズム」ではなく、現在のシステムで POI テンプレートがどのように作成、編集、保存、再利用されるかについてです。

## 現時点で最も重要なファイル

- `Engine/ColorVision.Engine/Templates/POI/TemplatePoi.cs`
- `Engine/ColorVision.Engine/Templates/POI/PoiParam.cs`
- `Engine/ColorVision.Engine/Templates/POI/PoiPoint.cs`
- `Engine/ColorVision.Engine/Templates/POI/AlgorithmImp/AlgorithmPOI.cs`
- `Engine/ColorVision.Engine/Templates/POI/BuildPoi/AlgorithmBuildPoi.cs`
- `Engine/ColorVision.Engine/Templates/POI/POIFilters/TemplatePoiFilterParam.cs`
- `Engine/ColorVision.Engine/Templates/POI/POIRevise/TemplatePoiReviseParam.cs`
- `Engine/ColorVision.Engine/Templates/POI/POIOutput/TemplatePoiOutputParam.cs`
- `Engine/ColorVision.Engine/Templates/POI/POIGenCali/TemplatePoiGenCalParam.cs`
- `Engine/ColorVision.Engine/Templates/Flow/NodeConfigurator/POINodeConfigurators.cs`

## 現在のメインチェーンを実行する方法

### メインテンプレートとデータモデル

`TemplatePoi` が正面玄関です。現在、いくつかの重要な実装機能があります。

- `ITemplate<PoiParam>` を継承
- `IsSideHide = true`
- テンプレートコードは`POI`に固定されています
- リスト項目をダブルクリックすると `EditPoiParam` を直接開きます

多くの通常のテンプレートとは異なり、POI メイン テンプレートは右側の `PropertyGrid` に単純に依存するのではなく、独自の編集ウィンドウを持っています。

`PoiParam` は、いくつかの値を格納するだけの単純なパラメーター クラスではありません。現在、以下をホストしています:

- テンプレートサイズ `Width`、`Height`
- 四隅座標 `LeftTopX/Y`、`RightTopX/Y`、`RightBottomX/Y`、`LeftBottomX/Y`
- `CfgJson` と `PoiConfig` 間の双方向変換
- `ObservableCollection<PoiPoint> PoiPoints`

`PoiPoint` 自体は、現在のシステムで実際に使用されるポイント情報を保存します。

- `Id`
- `Name`
- `PointType`
- `PixX`、`PixY`
- `PixWidth`、`PixHeight`

したがって、POI テンプレートは現在、「ポイント セット テンプレート + 構成テンプレート」の組み合わせに近いものになります。

### 現在の永続化メソッド

POI メイン テンプレートは、通常のデフォルト パスの `ModMasterModel`/`ModDetailModel` セットではありません。現在、専用のテーブルを使用しています。

- `PoiMasterDao`
- `PoiDetailDao`

`PoiParam.LoadPoiDetailFromDB(...)` はポイントの詳細を `PoiPoints` にロードして戻します。拡張メソッド `Save2DB(...)` は次のことを行います。

- マスターレコードの保存
- 古いポイント詳細を削除します
- BulkCopy を使用して `PoiDetailModel` のセット全体を書き換えます

これは、POI ページが偏りやすい場所の 1 つでもあります。これは、「一般的なテンプレート テーブル内の通常の詳細アイテムのセット」ではなく、独自のポイント テーブルです。

### インポート、コピー、作成

`TemplatePoi` は現在以下をサポートしています:

- 現在のテンプレートから JSON の一時コピーとしてコピーします
- `.cfg` から点セット テンプレートをインポート
- エクスポートする前にポイントの詳細をアクティブにロードします
- 作成時に、インポートされたコピーまたは空のテンプレートをデータベースに書き込みます。

さらに、コピーまたはインポート後、古い主キーの直接の再利用を避けるために、テンプレート `Id` と各ポイントの `Id` は `-1` にリセットされます。

### ランタイムアルゴリズムチェーン

`AlgorithmPoi` は現在、メインの POI 操作エントリです。それは次のことを担当します。

- POIメインテンプレート編集ウィンドウを開きます
- フィルタリング、修正、出力テンプレート編集ウィンドウを開く
- ファイルモードで外部ポイントファイルを選択
- `Event_POI_GetData` の MQTT パラメータをアセンブルする

現在送信されているパラメータには、メイン テンプレートだけでなく、次のものも含まれる場合があります。

- `FilterTemplate`
- `ReviseTemplate`
- `OutputTemplate`
- `POIStorageType`
- `POIPointFileName`
- `IsSubPixel`
- `IsCCTWave`

これは、POI 実行チェーンが現在、単独で実行されている単一のテンプレートではなく、「複数のテンプレートの組み合わせリクエスト」であることを示しています。

### ポイント レイアウトと付属のテンプレート

`AlgorithmBuildPoi` も重要なチェーンです。現在、次のことを担当しています。

- レイアウト テンプレート `TemplateBuildPoi` を開きます
- CAD ファイルのオプションのロード
- `POIBuildType == CADMapping` および `CADMappingParam` の 4 点ポリゴンが付属
- `Event_Build_POI` をリリース

これに加えて、POI ファミリには現在、いくつかのコンパニオン テンプレートが含まれています。

- `TemplatePoiFilterParam`: フィルター テンプレート、`Code = POIFilter`、カスタム編集コントロールを使用します。
- `TemplatePoiReviseParam`: テンプレートを修正、`Code = PoiRevise`
- `TemplatePoiGenCalParam`: 正しいキャリブレーション テンプレート、`Code = POIGenCali`、カスタム編集コントロールを使用
- `TemplatePoiOutputParam`: 出力テンプレート、`Code = PoiOutput`、カスタム編集コントロールを使用

これらのテンプレートは、コメント内の「オプションの拡張機能」ではなく、現在のフローおよびアルゴリズム チェーンで実際に参照されるオブジェクトです。

### Flow やその他のアルゴリズムは POI をどのように消費しますか?

POI は、単一アルゴリズムのプライベート テンプレートではなく、共有プリミティブになりました。現在、少なくとも 3 つの明確な消費パスがあります。

1. `POINodeConfigurators` は、`TemplatePoi`、フィルタリング、補正、出力、キャリブレーション、およびその他のテンプレートを POI ノード属性パネルにハングします。
2. `AlgorithmPoiAnalysis` には、JSON 解析テンプレートに加えて `POITemplateParam` が引き続き付属します。
3. `AlgorithmSFRFindROI`、`AlgorithmOLEDAOI` このタイプのアルゴリズムは、さらに `TemplatePoi` も参照します。

## 現在、最もよくある間違いのいくつかが犯されています

### POI は別個のアルゴリズムではありません

現在のウェアハウス内の POI は、ポイントを生成したり、ポイントをフィルタリングしたり、他のアルゴリズムによって使用したりできる、共有ポイント セット テンプレート システムに似ています。

### メインストレージは通常の詳細テーブルではありません

メインのテンプレートは `PoiMasterDao` と `PoiDetailDao` に依存しています。一般的なテンプレート表に従って説明を続けると、詳細レベルを見逃してしまいます。

### メインエディタは純粋な `PropertyGrid` ではありません

`TemplatePoi` はダブルクリックすると `EditPoiParam` に入ります。フィルタリングおよび出力テンプレートには、独自の `UserControl` エディターもあります。そのまま統一した右側のプロパティパネルとして書き続けると、実際のインターフェースと矛盾してしまいます。

### ファイルモードとデータベースモードが共存

`AlgorithmPoi` は、`POIStorageModel.Db` および `POIStorageModel.File` パスを明示的にサポートします。ドキュメントでは、POI を「データベース内にのみ存在」として記述することができなくなりました。

## 推奨される読む順序

1. `Engine/ColorVision.Engine/Templates/POI/TemplatePoi.cs`
2. `Engine/ColorVision.Engine/Templates/POI/PoiParam.cs`
3. `Engine/ColorVision.Engine/Templates/POI/AlgorithmImp/AlgorithmPOI.cs`
4. `Engine/ColorVision.Engine/Templates/POI/BuildPoi/AlgorithmBuildPoi.cs`
5. `Engine/ColorVision.Engine/Templates/Flow/NodeConfigurator/POINodeConfigurators.cs`

## 続きを読む

- [POI プリミティブ](../primitives/poi.md)
- [JSON テンプレート](./json-templates.md)
- [プロセスエンジン](./flow-engine.md)