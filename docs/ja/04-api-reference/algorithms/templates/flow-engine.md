# プロセスエンジン

このページでは、現在のウェアハウスの `Engine/ColorVision.Engine/Templates/Flow` レイヤーの実際の役割についてのみ説明します。 「フロー実行カーネル、ホスト ブリッジ、ノード ライブラリ全体を 1 つのページに混在させる」という古いドラフトは今後維持されません。

## まず、このページで今話していることを見てみましょう。

現在のページは、`FlowEngineLib` 実行カーネル自体についてではなく、メイン プログラム内のプロセス テンプレートを囲むホスト層について説明しています。主な内容は次のとおりです。

- プロセス テンプレートがデータベースおよびリソース テーブルからロードされる方法。
・プロセステンプレートをダブルクリックして編集画面を開く方法。
- 編集ウィンドウが `STNodeEditor`、プロパティ パネル、およびノー​​ド ツリーをホストする方法。
- ホスト層がデバイス、テンプレート、ノード コンフィギュレーターをプロセス エディターにハングアップする方法。

ノードの実行セマンティクスとノードの基本クラスを確認したい場合は、[FlowEngineLib](../../engine-components/FlowEngineLib.md) に移動してください。

## 現時点で最も重要なファイル

- `Engine/ColorVision.Engine/Templates/Flow/TemplateFlow.cs`
- `Engine/ColorVision.Engine/Templates/Flow/FlowEngineToolWindow.xaml.cs`
- `Engine/ColorVision.Engine/Templates/Flow/STNodeEditorHelper.cs`
- `Engine/ColorVision.Engine/Templates/Flow/NodeConfigurator/*.cs`

これらのコードは、メイン プログラムのプロセス テンプレートがどのように編集、保存、構成されるかを決定します。

## 現在のメインチェーンを実行する方法

### プロセステンプレートのエントリ

`MenuTemplateFlow` は `TemplateEditorWindow(new TemplateFlow())` を開きます。 `TemplateFlow` 自体は `ITemplate<FlowParam>` の特定の実装であり、現在次の役割を担っています。

- MySQLからプロセステンプレートのマスターテーブルを読み込みます
- ノード グラフのコンテンツを `SysResourceModel.Value` から Base64 に取得します。
- `FlowParam` にラップします
- 保存、削除、インポート、エクスポート、作成を管理する

したがって、現在のプロセス テンプレートは単純なディスク ファイルのリストではなく、「データベース マスター レコード + リソース テーブルのバイナリ コンテンツ」の組み合わせです。

### ダブルクリック後の編集ウィンドウ

`TemplateFlow.PreviewMouseDoubleClick(...)` は `FlowEngineToolWindow` を直接開きます。これは、プロセス テンプレートが多くの通常のテンプレートとは異なることを示しています。

- リストウィンドウは単なる入り口です
- 実際のプロセスの編集は別のウィンドウで行われます

このウィンドウは `STNodeEditorHelper` を使用して、ノード キャンバス、プロパティ パネル、ノード ツリー、クリップボード、および右クリック メニューをホストします。

### エディター補助レイヤー

`STNodeEditorHelper` は現在、「ノード ツリーの調整を支援する」ことをはるかに超えて、多くのことを担当しています。

- ノードのコピー＆ペーストの圧縮シリアル化
- 現在選択されているノードがプロパティ パネルと同期されます
- ノードツリーの初期化とアセンブリ
- 右クリックメニュー、削除、すべて選択、その他のコマンド
- 合法性チェックと自動レイアウト
- デバイスおよびテンプレート選択パネル用のホストフック

これは、プロセス編集ウィンドウ内の多くの対話ロジックが、各ノード コントロールに分散されているのではなく、このヘルパーに集中していることを意味します。

### ノード コンフィギュレータ ブリッジ

`NodeConfigurator` ディレクトリは現在、メイン プログラムとノード ライブラリの間の重要なブリッジ層です。以下のようになります:

- デバイスサービスリスト
- ローカル画像パス入力
- 共通テンプレートセレクター
- JSON テンプレート セレクター

ノードのプロパティ パネルをロードします。

たとえば、POI 関連のコンフィギュレータは、`TemplatePoi`、`TemplatePoiFilterParam`、`TemplatePoiReviseParam`、`TemplatePoiOutputParam` およびその他のテンプレートをプロセス ノードに接続します。言い換えれば、ホスト内のノードの編集可能なエクスペリエンスは、`FlowEngineLib` によって完全に決定されるわけではありません。

## 現在のストレージとエクスポートの境界

### メインストレージは引き続きデータベースです

`TemplateFlow.Load()` と `Save2DB(...)` は現在、MySQL マスター テーブル、詳細テーブル、および `SysResourceModel` を中心に展開しています。 Base64 ノード グラフのコンテンツはリソース テーブルにドロップされ、詳細レコードを通じて関連付けられます。

### エクスポートは 1 つの形式だけではありません

現在、プロセス テンプレートのエクスポートには少なくとも 2 つの実用的な形式があります。

- `.stn`: ノードグラフ元ファイル
- `.cvflow`: 関連するテンプレート情報を含むプロセス パッケージ

したがって、単にプロセス テンプレートを「単なるノード グラフ ファイル」として記述すると、現在のパッケージをエクスポートする機能が失われます。

## 現在、最もよくある間違いのいくつかが犯されています

### このページは FlowEngineLib の複製ページではありません

`FlowEngineLib` はノードの実行と基本クラス システムを担当します。このページのこの層は、メイン プログラムでのテンプレート管理、ウィンドウ編集、およびホスト ブリッジングを担当します。どちらの層も「プロセス エンジン」と呼ばれますが、その境界は異なります。

### プロセス テンプレートは純粋なディスク資産ではありません

現在のメイン パスは依然としてデータベース + リソース テーブルであり、特定のディレクトリ内の `.stn` ファイルをスキャンしません。インポートとエクスポートは単なる追加機能です。

### ノード属性の編集はホスト コードに大きく依存します

ノード クラス自体だけでなく、デバイス ドロップダウン ボックス、テンプレート ドロップダウン ボックス、および JSON テンプレート ドロップダウン ボックスをノード属性領域に実際に吊るすのは、`NodeConfigurator` レイヤーと `STNodeEditorHelper` レイヤーです。

### ウィンドウの動作は一般的なテンプレート エディタとは異なります

通常のテンプレートは主に `TemplateEditorWindow` の右側で編集されます。プロセス テンプレートは現在、「リスト ウィンドウ + 独立したプロセス エディター ウィンドウ」のパスを採用しています。一般的なテンプレートの物語に従い続けると、読者を誤解させることになります。

## 推奨される読む順序

1. `Engine/ColorVision.Engine/Templates/Flow/TemplateFlow.cs`
2. `Engine/ColorVision.Engine/Templates/Flow/FlowEngineToolWindow.xaml.cs`
3. `Engine/ColorVision.Engine/Templates/Flow/STNodeEditorHelper.cs`
4. `Engine/ColorVision.Engine/Templates/Flow/NodeConfigurator/POINodeConfigurators.cs`
5. `Engine/ColorVision.Engine/Templates/Flow/NodeConfigurator` のその他のコンフィギュレータ

## 続きを読む

- [FlowEngineLib](../../engine-components/FlowEngineLib.md)
- [フローノード拡張機能](../../extensions/flow-node.md)
- [ColorVision.Engine](../../engine-components/ColorVision.Engine.md)