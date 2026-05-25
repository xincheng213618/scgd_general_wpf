#ゴースト検出

このページでは、現在ウェアハウスにある実際のゴースト検出アクセス チェーンのみを説明しており、「独立した `ghost-detection` アルゴリズム API」の古いドラフトは管理されていません。

## まず、現在のページが実際に何について話しているのかを見てみましょう。

現在のソース コードのステータスによると、ゴースト検出は独立した公開アルゴリズム パッケージではなく、`ColorVision.Engine` の ARVR テンプレート ファミリのブランチです。現在、次のレイヤーで構成されています。

- ゴーストパラメータテンプレート
- ゴーストアルゴリズムUIホスト
- 画像入力と色選択インターフェイス
- MQTTコマンドのパッケージ化
- 結果のロード、オーバーレイ表示、CSV エクスポート

したがって、このページが本当に話したいのは、ホストから独立して存在する一連のプロセス API を想像するのではなく、「Ghost がどのようにホストされ、メイン プログラムで実行されるか」ということです。

## 現時点で最も重要なファイル

- `Engine/ColorVision.Engine/Templates/ARVR/Ghost/TemplateGhost.cs`
- `Engine/ColorVision.Engine/Templates/ARVR/Ghost/GhostParam.cs`
- `Engine/ColorVision.Engine/Templates/ARVR/Ghost/AlgorithmGhost.cs`
- `Engine/ColorVision.Engine/Templates/ARVR/Ghost/DisplayGhost.xaml.cs`
- `Engine/ColorVision.Engine/Templates/ARVR/Ghost/ViewHandleGhost.cs`
- `Engine/ColorVision.Engine/Templates/ARVR/Ghost/AlgResultGhostDao.cs`

Ghost が現在どのように構成されているか、コマンドを送信する方法、結果を表示する方法を知りたいだけであれば、これらの項目で主要な点はすでにカバーされています。

## 現在のメインチェーンを実行する方法

### テンプレートエントリ

`TemplateGhost` は、Ghost のパラメータ テンプレート エントリです。現在の実装は非常に簡単です。

- `ITemplate<GhostParam>` を継承
- `TemplateDicId = 7`
- `Code = ghost`

これは、Ghost が現在、JSON テンプレートや独立した構成ファイル チェーンの代わりに、従来の強い型のパラメーター テンプレート チェーンを使用していることを示しています。

### パラメトリックモデル

`GhostParam` 現在公開されているのは、古いドラフトで設定された一般化されたしきい値、面積、および形態学的スイッチではなく、ゴースト格子検出用のパラメーターのセットです。現在直接表示できるコア フィールドには次のものが含まれます。

- `Ghost_radius`
- `Ghost_cols`
- `Ghost_rows`
- `Ghost_ratioH`
- `Ghost_ratioL`

フィールドの名前と説明から判断すると、このパラメータのセットは、画像欠陥検出器の一般的なパラメータ テーブルではなく、「検出されるゴースト格子」の幾何学的制約とグレースケール制約に偏っています。

### アルゴリズムホスト

`AlgorithmGhost` は現在、基礎となる画像処理カーネルではなく、`DisplayAlgorithmBase` から派生したホスト クラスです。主に次のことを担当します。

- `TemplateGhost`の編集ウィンドウを開きます
- `DisplayGhost` ユーザー制御を提供します
- 現在の色の選択を維持します `CVOLEDCOLOR`
- テンプレート、色、デバイス情報、画像パスをメッセージにパックします

最終的には、統合された `ghost-detection` 呼び出しインターフェイスを公開する代わりに、イベント名 `Ghost` のメッセージをパブリッシュします。

### 入力および実行インターフェイス

`DisplayGhost` は、現在のユーザーが実際に公開されている実行中のインターフェイスです。これが行う作業は、古いドキュメントの「入力画像 + パラメーター」よりも具体的です。

- `TemplateGhost.Params`をバインドする
- 3 つの `CVOLEDCOLOR` オプションを提供します: `BLUE`、`GREEN`、`RED`
- `ServiceManager` から画像ソース デバイスを取得します
- 3 つの入力パスをサポート: バッチ番号、Raw/CIE ファイル、ローカル イメージ
- デバイス側の Raw/CIE ファイル リストの更新を許可します
- 画像をローカルまたはデバイス側で直接開くことができます

したがって、現在の Ghost 走行面は、本質的にはデバイス対話機能を備えた WPF パネルであり、純粋なアルゴリズム機能の入口ではありません。

### MQTT コマンド チェーン

`AlgorithmGhost.SendCommand(...)` は現在、次の情報をパッケージ化しています。

- `ImgFileName`
- `FileType`
- `DeviceCode`
- `DeviceType`
- `TemplateParam`
- `Color`

次に、`MsgSend` を構築し、`Ghost` イベントを発行します。

これは、Ghost 計算の現在の実際の実行端がこの UI クラス内ではなく、メッセージ チェーンの反対側にあることも示しています。

## 現在の結果をどのように処理すればよいでしょうか?

`ViewHandleGhost` は、現在の結果表示チェーンで最も重要なエントリです。それは次のことを担当します。

- `AlgResultGhostDao.Instance.GetAllByPid(...)` 経由で結果の詳細を読み込みます
- 結果リストを `ViewResultAlg` に受け取る
- `GhostPixel` および `LedPixel` に従って画像上にオーバーレイ ポイントを描画します
- `LEDCenters`、`LEDBlobGray`、`GhostAverageGray`を左側のリストに表示します
- CSVのエクスポート

古いドラフトの「統一された JSON 構造を返す」とは異なり、現在の Ghost の結果は主にデータベース結果モデル、画像オーバーレイ、リスト ビューを通じて表示されます。

## 現在、最もよくある間違いのいくつかが犯されています

### これはスタンドアロンのパブリック API ではありません

現在、ゴースト検出は明示的に ARVR テンプレート ファミリの一部であり、一般的な `ghost-detection` ライブラリではなく `Templates/ARVR/Ghost` にエントリが含まれています。

### アルゴリズム クラスはローカル コンピューティング カーネルではありません

`AlgorithmGhost` は現在、主にウィンドウ、入力、テンプレート、メッセージのアセンブリを担当しています。 `Mat` を直接処理するネイティブ アルゴリズム実装として記述すると、実際のコードと矛盾します。

### パラメータは古いドラフトよりもはるかに狭い

現在、`GhostParam` は格子半径、行数と列数、グレースケール比の上限と下限を公開しています。古い文書には、しきい値/領域/形態テーブルの完全なセットはありません。

### 結果の表示は UI と結果プロセッサーに依存します

実際の出力チェーンは、サンプル JSON を返す 1 回の呼び出しではなく、`ViewHandleGhost` + 結果の DAO + 画像オーバーレイです。

## 推奨される読む順序

1. `Engine/ColorVision.Engine/Templates/ARVR/Ghost/TemplateGhost.cs`
2. `Engine/ColorVision.Engine/Templates/ARVR/Ghost/GhostParam.cs`
3. `Engine/ColorVision.Engine/Templates/ARVR/Ghost/AlgorithmGhost.cs`
4. `Engine/ColorVision.Engine/Templates/ARVR/Ghost/DisplayGhost.xaml.cs`
5. `Engine/ColorVision.Engine/Templates/ARVR/Ghost/ViewHandleGhost.cs`

## 続きを読む

- [ARVR テンプレート](../templates/arvr-template.md)
- [アルゴリズムシステムの概要](../overview.md)
- [ColorVision.Engine](../../engine-components/ColorVision.Engine.md)