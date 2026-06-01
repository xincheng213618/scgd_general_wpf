#FlowEngineLib

このページでは、現在ウェアハウスで利用可能な実際の FlowEngineLib 実装についてのみ説明しており、「クラス図 + 理想化されたデータ フロー + 擬似 API テーブル」の古いドラフトは維持されません。

## まず、このモジュールが現在どのようなものであるかを見てみましょう

現在のソース コードのステータスによると、FlowEngineLib は抽象的なプロセス設計概念ではなく、ノード エディタ上に直接構築されたランタイム実行コアのセットです。現在、少なくとも 4 種類の処理を行っています。

- ホストはキャンバス オブジェクトとノード オブジェクトを処理します。
- 開始ノード、サービスノード、ロードされたキャンバスを管理します。
- `FlowNodeManager` のデバイス ビューにノードを追加します。
- 開始ノードと終了ノードの間のプロセス全体を終了する完了イベント。

したがって、古いドキュメントにあるようなホストから独立して存在する汎用 DSL プラットフォームよりも、「ノード実行カーネル」に近いものになります。

## 現時点で最も重要なファイル

- `Engine/FlowEngineLib/FlowEngineControl.cs`
- `Engine/FlowEngineLib/CVFlowContainer.cs`
- `Engine/FlowEngineLib/Base/CVCommonNode.cs`
- `Engine/FlowEngineLib/Base/CVBaseServerNode.cs`
- `Engine/FlowEngineLib/Start/BaseStartNode.cs`
- `Engine/FlowEngineLib/End/CVEndNode.cs`
- `Engine/FlowEngineLib/Algorithm/AlgorithmNode.cs`
- `Engine/FlowEngineLib/Base/CVStartCFC.cs`

プロセスがどのようにロード、開始、転送、終了するかを知りたいだけの場合、これらのコードはすでにメイン リンクをカバーしています。

## 現在のコントロール プレーンを階層化する方法

### プロセスコントローラー

`FlowEngineControl` は現在のコア ランタイム コントローラーです。実装によれば、次のことを担当します。

-フック `STNodeEditor`
- トレース開始ノードの辞書 `startNodeNames`
- 追跡サービスノード辞書 `services`
- キャッシュロードされたキャンバス `loadedCanvas`
- プロセス完了イベントのトリガー `Finished`

ノードがエディターに入ると、`FlowEngineControl` はノードを `NodeAdded` イベントでの処理のために 2 つのカテゴリに分割します。

- `BaseStartNode` は開始ノード ディクショナリに入り、`Finished` をサブスクライブします。
- `CVBaseServerNode` はサービス ノード コレクションに入り、`FlowNodeManager` に同期します

古いドキュメントの「グラフを読み込んだ後すぐに実行する」という記述よりも、こちらの方が実際の実装に近いです。

### マルチプロセスコンテナ

`CVFlowContainer` は、`FlowEngineControl` に隣接する別の制御線です。以下を保持します:

- 複数の開始ノードのマッピング
- `startNodesFlowMap`
- 追加/ロード/開始の組み合わせ機能

これは、FlowEngineLib が現在、単一の固定キャンバスを提供するだけでなく、キーによってプロセスを追加および開始するシナリオも考慮していることを示しています。

## ノード システムは実際にはどのようになっているのでしょうか?

### `CVCommonNode`

これはすべてのコア ノードに共通の基本クラスであり、現在以下を提供します。

- `NodeName`
- `NodeType`
- `DeviceCode`
- `NodeID`
- `ZIndex`
- `nodeEvent`
- `nodeRunEvent`
- `nodeEndEvent`

さらに、コントロール作成ヘルパー メソッドを統合し、`OnOwnerChanged()` でノード エディターにタイプ カラーを登録します。

### `BaseStartNode`

開始ノードは現在、次の役割を担っています。

- 複数の `OUT_LOOP` 出力を持つ `OUT_START` を作成する
- `Ready`、`Running`、`startActions` を保守します。
- `CVStartCFC` を接続されたノードの最初のバッチに配布します
- プロセス完了後に `Finished` をスローします

したがって、プロセスの「開始」は外部コントローラーだけで完了するのではなく、開始ノードの内部で実装されます。

### `CVBaseServerNode`

これは現在、最も一般的な実行ノードの基本クラスです。実装によれば、次のことを担当します。

- `IN` / `OUT` などのノード ポートを作成します
- テンプレートID、テンプレート名、画像ファイル名、トークンおよびタイムアウト設定を維持します
- 基本的なリクエストデータの組み立て
- サーバー応答を受信し、プロセスを続行します

古いドキュメントに常に登場していた `DoServerWork` は、現在強調すべき拡張機能ではありません。現在は、`OnCreate()`、要求パラメータの構築、応答処理、およびリセット ロジックに重点が置かれています。

### `CVEndNode`

エンドノードが現在実行していることは非常に具体的です。

- `CVStartCFC` を受信するか、次の入力をループします
- `startAction.DoFinishing()` に電話してください
- 最後に `startAction.FireFinished()` を呼び出します

これは、完了したプロセス全体の真の閉ループ位置です。

### `AlgorithmNode`

`AlgorithmNode` は、サービス ノードを理解するための非常に典型的なサンプルです。現在は次のことを行っています。

- オペレーターのタイプ、テンプレート、POI テンプレート、色、キャッシュ長を維持します
- `OnCreate()` でノード内編集コントロールを作成する
- テンプレート、画像、色、SMU データを `getBaseEventData(...)` のアルゴリズム リクエスト パラメーターにパックします。

これは、FlowEngineLib の現在のノードの中心的な仕事が、完全なアルゴリズムをノード内でローカルに実行するのではなく、「実行パラメータを構築して転送する」ことであることを再度示しています。

## 現在プロセス完了チェーンを閉じるにはどうすればよいですか?

`CVStartCFC` は現在、ノード間でプロセス全体のステータスを転送するためのキー オブジェクトです。次の内容がログに記録されます。

- 開始時間と終了時間
- プロセスのステータス
- IMEI
- データ辞書
- 対応する開始ノード

プロセスの最後に、`CVEndNode` は `DoFinishing()` と `FireFinished()` を呼び出し、その後 `BaseStartNode` の `Finished` イベントに戻り、最後に `FlowEngineControl` が `FlowEngineEventArgs` を外の世界にスローします。

このチェーンが接続されていない場合、「ノード端」と「プロセス端」を同じものとして混同しやすくなります。

## 現在のコードとホストコードの間の境界

FlowEngineLib 自体はノード実行カーネルのみを担当します。実際に ColorVision メイン プログラムに接続するのは `Engine/ColorVision.Engine/Templates/Flow/` レイヤーです。次に例を示します。

- `FlowEngineManager.cs`
- `DisplayFlow.xaml.cs`
- `TemplateFlow.cs`

そこに責任があります。

- MQTT RC サービス トークンと組み合わせたリフレッシュ プロセス キャンバス
- プロセス テンプレートを Base64 からコントローラーにロードします
- UI でプロセスを選択、編集、実行します

そのため、テンプレート層を見ずにFlowEngineLibだけを読んだ場合、「どのように実行するか」はわかりますが、「メインプログラムで誰がトリガーして実行するか」はわかりません。

## 現在、最もよくある間違いのいくつかが犯されています

### これは、ホスト レベルの完全なワークフロー システムのコード全体ではありません。

FlowEngineLib はノード実行カーネルのみを実装します。メイン プログラムに入った後のテンプレート管理、ウィンドウの操作、およびデータのロードは依然として `ColorVision.Engine/Templates/Flow/` レベルです。

### 「ノードが完了しました」は「プロセスが完了しました」と等しくありません

現在、`nodeEndEvent` を発行するノードではなく、実際にプロセスを完了するのは `CVEndNode -> CVStartCFC.FireFinished() -> BaseStartNode.Finished -> FlowEngineControl.Finished` チェーンです。

### サービス ノード拡張ポイントは、古いドラフト作成方法に基づいて理解されるべきではなくなりました。

現在の実際の拡張パスは次のようになります。

- `OnCreate()`
- パラメータの組み立て
- 応答の処理
- `Reset()`

いつものようにドキュメントで統一された「ローカル実行ビジネス機能」を探し続けると、ノード モデルを誤解することになります。

### `loadedCanvas` はデコレーション キャッシュではありません

`FlowEngineControl` と `CVFlowContainer` は両方とも、繰り返しロードを避けるためにキャンバス コンテンツのハッシュを使用します。この詳細は、同じプロセスが再度再構築されない理由の理解に影響します。

## 推奨される読む順序

1. `Engine/FlowEngineLib/FlowEngineControl.cs`
2. `Engine/FlowEngineLib/Base/CVCommonNode.cs`
3. `Engine/FlowEngineLib/Start/BaseStartNode.cs`
4. `Engine/FlowEngineLib/Base/CVBaseServerNode.cs`
5. `Engine/FlowEngineLib/End/CVEndNode.cs`
6. `Engine/FlowEngineLib/Algorithm/AlgorithmNode.cs`
7. `Engine/FlowEngineLib/Base/CVStartCFC.cs`
8. `Engine/ColorVision.Engine/Templates/Flow/DisplayFlow.xaml.cs`

これにより、最初にカーネル認識が確立され、次にそれがホスト側の UI トリガー チェーンに接続されます。

## 続きを読む

- [docs/04-api-reference/extensions/flow-node.md](../extensions/flow-node.md)
- [docs/03-architecture/components/engine/flow-engine.md](../../03-architecture/components/engine/flow-engine.md)
- [docs/04-api-reference/engine-components/ColorVision.Engine.md](./ColorVision.Engine.md)