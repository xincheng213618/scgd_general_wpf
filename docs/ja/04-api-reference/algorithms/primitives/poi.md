# POI

このページでは、現在のウェアハウスに共有プリミティブとして存在する POI のみについて説明しており、古い「POI 検出アルゴリズム エンサイクロペディア」スタイルのドラフトは維持されなくなりました。

## まず、POI がシステム内でどのような役割を果たしているかを見てみましょう。

現在のソース コードのステータスによれば、POI は単一のアルゴリズム結果というよりは、再利用可能なデータとテンプレート システムのセットに似ています。

- マスター テンプレートはポイント セットと構成を保存します。
- ポイントの配置、フィルタリング、補正、キャリブレーション、および出力は、このポイント セットを中心に機能します。
- JSON アルゴリズムと ARVR アルゴリズムは引き続き POI テンプレートを参照します。
- フロー ノードは、POI を共有の入力および出力オブジェクトとしても扱います。

したがって、このページの焦点は「特徴点を見つける方法」ではなく、POI プリミティブが現在どのように保存、転送、および消費されているかにあります。

## 現時点で最も重要なファイル

- `Engine/ColorVision.Engine/Templates/POI/PoiPoint.cs`
- `Engine/ColorVision.Engine/Templates/POI/PoiParam.cs`
- `Engine/ColorVision.Engine/Templates/POI/TemplatePoi.cs`
- `Engine/ColorVision.Engine/Templates/POI/AlgorithmImp/AlgorithmPOI.cs`
- `Engine/ColorVision.Engine/Templates/POI/BuildPoi/AlgorithmBuildPoi.cs`
- `Engine/ColorVision.Engine/Templates/POI/POIFilters/TemplatePoiFilterParam.cs`
- `Engine/ColorVision.Engine/Templates/POI/POIRevise/TemplatePoiReviseParam.cs`
- `Engine/ColorVision.Engine/Templates/POI/POIOutput/TemplatePoiOutputParam.cs`
- `Engine/ColorVision.Engine/Templates/POI/POIGenCali/TemplatePoiGenCalParam.cs`
- `Engine/ColorVision.Engine/Templates/Jsons/PoiAnalysis/AlgorithmPoiAnalysis.cs`
- `Engine/ColorVision.Engine/Templates/Jsons/SFRFindROI/AlgorithmSFRFindROI.cs`
- `Engine/ColorVision.Engine/Templates/Jsons/OLEDAOI/AlgorithmOLEDAOI.cs`
- `Engine/ColorVision.Engine/Templates/Flow/NodeConfigurator/POINodeConfigurators.cs`

## 現在のデータはどのようなものですか?

### 点オブジェクト

`PoiPoint` は、表示フィールドと位置決めフィールドの非常に単純なセットを保存するようになりました。

- `Id`
- `Name`
- `PointType`
- `PixX`、`PixY`
- `PixWidth`、`PixHeight`

これは抽象的な「POI インターフェイス」ではなく、現在の画像編集と結果表示に必要なフィールドに近い具体的なオブジェクトです。

### テンプレートオブジェクト

`PoiParam` は、点セット、寸法、コーナー、構成をテンプレートにパッケージ化する役割を果たします。現在、少なくとも次のものが含まれています。

- テンプレートサイズ `Width`、`Height`
- テンプレート タイプ `Type`
- 四隅の座標
- `PoiPoints`
- `CfgJson` および `PoiConfig`

さらに、`CfgJson` は単純な文字列キャッシュではありません。現在、`PoiConfig` を使用して相互にシリアル化および逆シリアル化します。

## 現在の保存方法

POI の中核となる現実は、現在、POI が独自の専用のマスター/スレーブ データ構造を持っているということです。

- マスターレコードは`PoiMasterDao`経由で保存されます
- ポイントの詳細は `PoiDetailDao` 経由で保存されます

`PoiParam.LoadPoiDetailFromDB(...)` は、`Pid` に従ってポイント セットをバックフィルします。拡張メソッド `Save2DB(...)` は古い詳細をクリアし、新しいポイントをバッチで書き込みます。

これにより、POI は、`ModMasterModel`/`ModDetailModel` のみに依存する一般的なテンプレートとは大きく異なります。

## 現在実行中のチェーンで POI を消費するにはどうすればよいですか?

### 主要な POI アルゴリズム

`AlgorithmPoi` は、最も直接的な POI コンシューマおよびプロデューサーです。現在、以下をサポートしています。

- メインテンプレート `TemplatePoi`
- フィルターテンプレート `TemplatePoiFilterParam`
- テンプレート `TemplatePoiReviseParam` を修正
- 出力テンプレート `TemplatePoiOutputParam`
- ファイルモード `POIStorageModel.File`

最後に、`Event_POI_GetData` を介して複数のテンプレート パラメーターを指定して MQTT リクエストを発行します。

### ポイント配置アルゴリズム

`AlgorithmBuildPoi` は、他の情報を POI ポイント セットに変換する役割を果たします。現在、以下をサポートしています。

- 通常のレイアウト
- CADMapping ポイントのレイアウト
- 4 点ポリゴン `LayoutPolygon`
- `CADMappingParam`
- `Event_Build_POI`

したがって、現在のシステムにおける「POIの取得」は、検出だけでなく構築にも依存します。

### ダウンストリームアルゴリズムリファレンス

POI は現在、他の複数のアルゴリズム チェーンによって消費されています。

- `AlgorithmPoiAnalysis` には `POITemplateParam` が付属します
- `AlgorithmSFRFindROI` には `POITemplateParam` が付属します
- `AlgorithmOLEDAOI` には `POITemplateParam` も付属しています

したがって、POI は現在、他のアルゴリズムの入力形式の 1 つであり、結果ページの最後に表示される補助オブジェクトではありません。

### フローノードリファレンス

`POINodeConfigurators` は、POI がフローの共有ノード リソースになったことを説明しています。

- `POINode` にはマスター テンプレート、フィルター、修正、出力テンプレートが必要です
- `BuildPOINode` は、ポイント テンプレート、ライトバック POI テンプレート、およびレイアウト ROI テンプレートを同時に受け取ります
- `POIReviseNode` は補正キャリブレーション テンプレートに接続されます
- `POIAnalysisNode` は JSON 分析テンプレートに接続します

これは、POI が現在、プロセス設計段階で選択する必要があるコア プリミティブであることも示しています。

## 現在、最もよくある間違いのいくつかが犯されています

### POI は単一の検出アルゴリズムの結果構造ではありません

現在、検出、レイアウト、分析、AOI、フロー ノードで同時に使用されており、共有データ テンプレートのセットです。

### ストレージは単なるデータベースでも、単なるファイルでもありません。

メイン テンプレートはデータベースを使用しますが、`AlgorithmPoi` はファイル モードと外部ポイント ファイルも明示的にサポートします。

### コンパニオン テンプレートは、現在のシステムのファーストクラスのメンバーです

フィルタリング、修正、キャリブレーション、および出力テンプレートにはすべて実際の実装と編集の入り口があり、コメントにある「将来の拡張」ではありません。

### 一部のアルゴリズムは、POI を生成するのではなく、POI を消費します。

`PoiAnalysis`、`SFR_FindROI`、`OLEDAOI` などのチェーンは、基本的に既存の POI テンプレートを読み取り、使用します。

## 推奨される読む順序

1. `Engine/ColorVision.Engine/Templates/POI/PoiPoint.cs`
2. `Engine/ColorVision.Engine/Templates/POI/PoiParam.cs`
3. `Engine/ColorVision.Engine/Templates/POI/AlgorithmImp/AlgorithmPOI.cs`
4. `Engine/ColorVision.Engine/Templates/POI/BuildPoi/AlgorithmBuildPoi.cs`
5. `Engine/ColorVision.Engine/Templates/Flow/NodeConfigurator/POINodeConfigurators.cs`

## 続きを読む

- [POI テンプレート](../templates/poi-template.md)
- [JSON テンプレート](../templates/json-templates.md)
- [プロセス エンジン](../templates/flow-engine.md)