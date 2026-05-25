# ARVR テンプレート

このページでは、現在のウェアハウスに実際に表示されている ARVR テンプレート ファミリについてのみ説明しており、「光アルゴリズムの教科書 + 統一パラメータ マニュアル」の古いドラフトは現在メンテナンスされていません。

## このテンプレート ファミリは現在何をしているのでしょうか?

現在のソース コードの状況によると、ARVR は単一のテンプレートではなく、並列して存在する一連のテンプレートと表示アルゴリズムです。

- `MTF`
- `SFR`
- `FOV`
- `Distortion`
- `Ghost`

これらの実装は同じホスト フレームワークを共有していますが、パラメータ モデル、結果のパフォーマンス、POI に依存するかどうかは統一されていません。さらに Flow ノードに進むと、`SFR_FindROI` などのテンプレートなどの JSON バリアントも混合します。

したがって、このページは、汎用パラメータ テーブルを維持しようとするよりも、「ARVR ファミリ マップ」としての方が適しています。

## 現時点で最も重要なファイル

- `Engine/ColorVision.Engine/Templates/ARVR/MTF/TemplateMTF.cs`
- `Engine/ColorVision.Engine/Templates/ARVR/MTF/MTFParam.cs`
- `Engine/ColorVision.Engine/Templates/ARVR/MTF/AlgorithmMTF.cs`
- `Engine/ColorVision.Engine/Templates/ARVR/MTF/ViewHandleMTF.cs`
- `Engine/ColorVision.Engine/Templates/ARVR/SFR/SFRParam.cs`
- `Engine/ColorVision.Engine/Templates/ARVR/SFR/AlgorithmSFR.cs`
- `Engine/ColorVision.Engine/Templates/ARVR/SFR/WindowSFR.xaml.cs`
- `Engine/ColorVision.Engine/Templates/ARVR/FOV/FOVParam.cs`
- `Engine/ColorVision.Engine/Templates/ARVR/FOV/AlgorithmFOV.cs`
- `Engine/ColorVision.Engine/Templates/ARVR/FOV/DisplayFOV.xaml.cs`
- `Engine/ColorVision.Engine/Templates/ARVR/Distortion/DistortionParam.cs`
- `Engine/ColorVision.Engine/Templates/ARVR/Distortion/AlgorithmDistortion.cs`
- `Engine/ColorVision.Engine/Templates/ARVR/Distortion/ViewResultDistortion.cs`
- `Engine/ColorVision.Engine/Templates/ARVR/Ghost/GhostParam.cs`
- `Engine/ColorVision.Engine/Templates/ARVR/Ghost/AlgorithmGhost.cs`
- `Engine/ColorVision.Engine/Templates/Flow/NodeConfigurator/AlgorithmNodeConfigurators.cs`
- `Engine/FlowEngineLib/Algorithm/AlgorithmARVRNode.cs`

## 現在のメインチェーンを分割する方法

###MTF

`TemplateMTF` は、現在次のような古典的なパラメータ テンプレートです。

- `Code = MTF`
- `TemplateDicId = 8`

`MTFParam` で現在最も直接表示されるパラメータは次のとおりです。

- `MTF_dRatio`
- `eEvaFunc`
- `dx`
- `dy`
- `ksize`

`AlgorithmMTF` の実際の動作はネイティブ グラフではありませんが、次のようになります。

- `TemplateMTF` を開きます
- `TemplatePoi` を開きます
- `POITemplateParam` の組み立て
- `Event_MTF_GetData` をリリース

これは、現在の MTF 実行チェーンが POI から独立して存在するのではなく、POI テンプレートに明示的に依存していることを示しています。

結果側で最も興味深いのは、パラメーター クラスではなく、`ViewHandleMTF` です。それは次のことを行います:

- 結果をCSVにエクスポート
- 統計的な最大値、最小値、平均値、分散、均一性
- `ViewResultAlgType.MTF` プロセッサとして UI にアクセス

### SFR

`SFRParam` は現在、古いドキュメントよりもはるかに単純で、直接表示できるコア パラメーターは `Gamma` のみです。実際の表示と結果の相互作用は次の点に当てはまります。

- `AlgorithmSFR`
- `WindowSFR`

`AlgorithmSFR` は MTF と同じであり、`Event_SFR_GetData` を発行する前にさらに `TemplatePoi` が必要です。 `WindowSFR` は、結果の `Pdfrequency` と `PdomainSamplingData` を曲線にデシリアライズし、しきい値と周波数の変換を提供します。

したがって、現在の SFR ドキュメントでは、テンプレート パラメーターについてのみ説明するだけでなく、結果ウィンドウも含めることができます。

### 視野

`FOVParam` は現在、比較的完全なパラメーター モデルであり、次のものが直接含まれています。

- `Radio`
- `CameraDegrees`
- `ThresholdValus`
- `DFovDist`
- `FovPattern`
- `FovType`
- `Xc`、`Yc`、`Xp`、`Yp`

`AlgorithmFOV` は `Event_FOV_GetData` のパッケージ化を担当し、`DisplayFOV` は現在の非常に実用的な作業層を担当します。

- サービスマネージャーから画像ソースデバイスを取得します
- バッチ、オリジナル ファイル、ローカル イメージの 3 つの入力をサポートします。
- Raw ファイルのリストを取得し、直接開くことを許可します

これは、FOV が現時点では「パラメータを設定してアルゴリズムを実行するだけ」の最小限のテンプレートではないことを示しています。

### ディストーション

`DistortionParam` は現在、非常に大きなパラメーター オブジェクトであり、次のような BLOB しきい値、エリア フィルター、形状フィルター、およびグローバル ポリシー項目の複数のセットが含まれています。

- `filterByColor`
- `minThreshold` / `maxThreshold`
- `minArea` / `maxArea`
- `filterByCircularity`
- `filterByConvexity`
- `filterByInertia`
- `CornerType`
- `SlopeType`
- `LayoutType`
- `DistortionType`

`AlgorithmDistortion` は `Distortion` イベントの発行を担当し、`ViewResultDistortion` は列挙値と最終的なラティス結果を表示可能な記述オブジェクトに再マップします。

###ゴースト

`GhostParam` 現在表示されているコア パラメーターは複雑ではなく、主に検出格子を中心に展開します。

- `Ghost_radius`
- `Ghost_cols`
- `Ghost_rows`
- `Ghost_ratioH`
- `Ghost_ratioL`

`AlgorithmGhost` には追加の `Color` パラメーターが付属しており、`Ghost` イベントを発行します。つまり、カラー チャネルは現在、ゴースト チェーンのファースト クラスの入力であり、ページ アノテーションの追加アイテムではありません。

## フローにアクセスするにはどうすればよいですか?

`AlgorithmARVRNode` と `AlgorithmNodeConfigurators` は、Flow での現在の ARVR ファミリの実際の使用法を共同で明らかにします。

- `MTF`、`SFR` ノードにはパラメーター テンプレートと `POI` テンプレートの両方が必要です。
- `FOV`、`畸变` ノードは、クラシック パラメーター テンプレートと JSON バリアントの両方を受け入れることができます。
- `SFR_FindROI` このタイプのブランチは、`TemplateSFRFindROI` と `TemplatePoi` の両方に接続されます。

したがって、現在の ARVR ファミリはフラット ディレクトリではなく、従来のテンプレート、JSON テンプレート、POI テンプレート、およびフロー ノードで構成される実行サーフェスです。

## 現在、最もよくある間違いのいくつかが犯されています

### ARVR は統一されたスキーマではありません

各サブディレクトリは、同じパラメータ フィールドのセットではなく、テンプレート ホストと表示アルゴリズム スタイルを共有します。

### ほとんどのアルゴリズム クラスはホストとコマンド アダプターです

`AlgorithmMTF`、`AlgorithmSFR`、`AlgorithmFOV`、`AlgorithmDistortion`、`AlgorithmGhost` は、数値計算をローカルで直接完了するのではなく、主にウィンドウを開いたり、入力を取得したり、MQTT リクエストを作成したりする役割を果たします。

### POI は ARVR の残り物ではありません

少なくとも MTF、SFR、SFR_FindROI は現在、すべて明示的に `TemplatePoi` に依存しています。 POI を省略した場合、このページでは現在の実行チェーンについて説明しません。

### 結果処理コードも重要です

`ViewHandleMTF`、`WindowSFR`、`ViewResultDistortion` などの結果層の実装は、ユーザーが最終的に見るものを理解するための重要な入り口であり、古いドキュメントから省略すべきではありません。

## 推奨される読む順序

1. `Engine/ColorVision.Engine/Templates/ARVR/MTF/AlgorithmMTF.cs`
2. `Engine/ColorVision.Engine/Templates/ARVR/SFR/AlgorithmSFR.cs`
3. `Engine/ColorVision.Engine/Templates/ARVR/FOV/DisplayFOV.xaml.cs`
4. `Engine/ColorVision.Engine/Templates/ARVR/Distortion/ViewResultDistortion.cs`
5. `Engine/ColorVision.Engine/Templates/Flow/NodeConfigurator/AlgorithmNodeConfigurators.cs`

## 続きを読む

- [POIテンプレート](./poi-template.md)
- [JSON テンプレート](./json-templates.md)
- [プロセスエンジン](./flow-engine.md)