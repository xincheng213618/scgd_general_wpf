# ST.ライブラリ.UI

このページでは、現在のウェアハウスで実際に利用可能な `ST.Library.UI` モジュールのみについて説明しており、「完全な UI プラットフォーム マニュアル + 大規模なサンプル + 統合拡張フレームワーク」の古いドラフトは維持されなくなります。

## まず、このモジュールが現在どのようなものであるかを見てみましょう

現在のソース コードのステータスによると、`ST.Library.UI` は低レベルの WinForms ノード エディター ライブラリです。現在の最も明確な役割は、スタンドアロンのアプリケーション シェルではなく、フロー関連の機能を提供することです。

- ノードキャンバスとインタラクティブエディター
- ノードベースクラスとポート接続モデル
- プロパティ編集パネル
- ノードツリーとノードパネルの組み合わせ制御

したがって、ColorVision ビジネス レイヤー自体よりも「ノード エディター インフラストラクチャ」に近いものになります。

## 現時点で最も重要なファイル

- `Engine/ST.Library.UI/NodeEditor/STNodeEditor.cs`
- `Engine/ST.Library.UI/NodeEditor/STNode.cs`
- `Engine/ST.Library.UI/NodeEditor/STNodeOption.cs`
- `Engine/ST.Library.UI/NodeEditor/STNodePropertyGrid.cs`
- `Engine/ST.Library.UI/NodeEditor/STNodeTreeView.cs`
- `Engine/ST.Library.UI/NodeEditor/STNodeEditorPannel.cs`
- `Engine/ST.Library.UI/FrmSTNodePropertyInput.cs`

このライブラリが現在のリポジトリで実際に何を行うのかを知りたいだけの場合、これらのファイルはすでに本体をカバーしています。

## 現在のコントロール プレーンをブロックに分割する方法

### キャンバス コントロール

`STNodeEditor` はライブラリ全体を中央制御します。現在の実装によれば、次の役割を果たします。

- メンテナンス `Nodes`
- キャンバスのオフセットとスケーリングを維持する
- ノードの選択、ホバー、およびアクティブ ステータスを管理します
- ノードの接続、切断、キャンバスの対話を処理します。
- ノードおよびキャンバス関連イベントのトリガー

これは、現在のノード エディターの制御ロジックが、多数の独立した MVVM サービスに分割されるのではなく、WinForms `Control` に集中していることを示しています。

### ノードオブジェクトモデル

`STNode` は、現在のすべてのノードの共通の基本クラスであり、次の役割を果たします。

- タイトル、サイズ、位置
- 入力および出力オプションのコレクション
- ノード組み込み制御コレクション
- 選択状態とアクティブ状態
- 自動サイズ変更と再描画

`STNodeOption` はポート モデルを担当しており、現在以下を提供しています。

- ポートのテキストとデータ型
- 単一接続/複数接続の制限
- 接続数と接続ポートのセット
- 接続、切断、データ転送イベント

したがって、このライブラリの基本的なメンタルモデルは、「ノードは単なる絵である」ではなく、「ノード + ポート + コントロール + イベント」を組み合わせたオブジェクトです。

### [プロパティ] パネル

`STNodePropertyGrid` は現在、ノード プロパティ専用に設計されたコントロールであり、.NET 標準の PropertyGrid を直接再利用しません。現在の `STNode` をラップアラウンドします。

- プロパティ記述子の読み取り
- アイテム、説明、エラー領域をレンダリングします。
- ノードのタイトルの色またはカスタム色に基づいて強調表示します
- 読み取り専用と編集モードの切り替えを処理します

`FrmSTNodePropertyInput` は、単一の属性値を編集するために使用される、一致する軽量入力フォームです。

### ノードツリーと組み合わせパネル

`STNodeTreeView` 現在の担当者:

- ノードタイプツリーを整理する
- 検索とグループ表示の維持
- エディターとプロパティパネルとの連携

`STNodeEditorPannel` の場合:

- `STNodeEditor`
- `STNodeTreeView`
- `STNodePropertyGrid`

これを直接使用できるパネル全体に結合し、分割線、ズーム プロンプト、接続ステータス プロンプトを追加します。

これは、`ST.Library.UI` が現在単一のエディター コントロールであるだけでなく、結合されたホスト パネルの比較的完全なセットも提供していることを示しています。

## ColorVision との現在の関係

このウェアハウスでは、`ST.Library.UI` は `FlowEngineLib` とそのホスト層によってインフラストラクチャとして使用されます。現在のビジネス層は通常、次のことを行います。

- `STNode` を継承して独自のノード タイプを作成します
- `STNodeEditor` をプロセス キャンバスとして使用する
- `STNodePropertyGrid` を借用してノード属性を公開する
- `STNodeTreeView` を借用してノード分類とドラッグ アンド ドロップ作成を管理します

したがって、文書では業務と同レベルの「プロセスシステム」として記述すべきではありません。プロセス系のUI基本ライブラリです。

## 現在、最もよくある間違いのいくつかが犯されています

### これは WinForms ライブラリであり、WPF プロセス フレームワークではありません

上部のメイン プログラムは WPF を広範囲に使用していますが、現在のコア コントロールは依然として WinForms `Control` です。この境界は、ホストがどのように組み込まれるかを理解するために重要です。

### このライブラリは単なるエディター コントロール以上の機能を提供します

`STNodeEditor` に加えて、現在、ノード オブジェクト モデル、ポート モデル、プロパティ グリッド、ノード ツリー、および組み合わせパネルがあります。これを「キャンバス コントロール」と省略すると、実際の範囲が過小評価されてしまいます。

### プロパティ編集はカスタム実装であり、システム PropertyGrid を直接使用するものではありません

`STNodePropertyGrid` および `FrmSTNodePropertyInput` は、ライブラリ内の独自のノード属性編集チェーンです。ドキュメントで通常どおりユニバーサル反射パネルとして説明し続けると、現在の独自の実装がわかりにくくなります。

### 主に上位ノードシステムで消費されます。

現在の実際の使用法は、ビジネス ノード ロジックを `ST.Library.UI` に直接記述するのではなく、上位層でノード タイプを定義し、それをここでエディター、ツリー、および属性パネルに渡すことです。

## 推奨される読む順序

1. `Engine/ST.Library.UI/NodeEditor/STNodeEditor.cs`
2. `Engine/ST.Library.UI/NodeEditor/STNode.cs`
3. `Engine/ST.Library.UI/NodeEditor/STNodeOption.cs`
4. `Engine/ST.Library.UI/NodeEditor/STNodePropertyGrid.cs`
5. `Engine/ST.Library.UI/NodeEditor/STNodeTreeView.cs`
6. `Engine/ST.Library.UI/NodeEditor/STNodeEditorPannel.cs`

このようにして、最初にキャンバスとノード モデルを確立し、次にプロパティ パネルとノード ライブラリがどのようにハングアップするかを理解できます。

## 続きを読む

- [docs/04-api-reference/engine-components/FlowEngineLib.md](./FlowEngineLib.md)
- [docs/04-api-reference/extensions/flow-node.md](../extensions/flow-node.md)
- [docs/03-architecture/components/engine/flow-engine.md](../../03-architecture/components/engine/flow-engine.md)