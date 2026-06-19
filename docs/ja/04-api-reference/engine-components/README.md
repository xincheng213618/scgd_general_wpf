# エンジンコンポーネントの概要

この章では、現在のウェアハウス構造に直接一致するエンジン側モジュールの入り口のみが保持され、「バージョン テーブル + サンプル コード + 統合された階層化されたブループリント」スタイルの古いドラフトは保持されなくなりました。

## この章では実際に何を説明しますか?

`Engine/` ディレクトリ内のコードは単一のアルゴリズム ライブラリではなく、相互に連携する一連のランタイム モジュールです。

- `ColorVision.Engine/`: メイン エンジン層。サービス、テンプレート、MQTT、データベース、プロセス アクセスを担当します。
- `FlowEngineLib/`: プロセス ノードと実行制御コア。
- `cvColorVision/`: ネイティブ機能のカプセル化と相互運用性ブリッジング。
- `ColorVision.FileIO/`: 画像およびカスタム フォーマット ファイルの読み取りと書き込み。
- `ST.Library.UI/`: ノード エディターおよび関連する UI 基本コントロール。
- `ColorVision.ShellExtension/`: Windows Explorer の `.cvraw` / `.cvcie` thumbnail extension。

したがって、エンジンの章を読むときは、「アルゴリズムの実装のみ」と解釈しないでください。また、ランタイム オーケストレーション、プロセス実行、基礎となるカプセル化、エディター サポート層も含まれます。

## 引き継ぎの入り口

Engine の業務ロジックを引き継ぐ場合は、個別モジュールに入る前に次のページを先に読んでください。

- [現在の Engine 文書カバレッジ](./current-engine-coverage.md): `Engine/` project、key business directory、handoff page の対応を確認します。
- [Engine 業務チェーンマトリクス](./business-flow-matrix.md): 業務シナリオからコードの入り口、設定、受け入れ確認を探します。
- [Engine 業務シナリオ引き継ぎプレイブック](./business-scenario-playbook.md): よくある要望と障害から変更場所を判断します。
- [Engine 業務引き継ぎマニュアル](./business-handoff.md): デバイス資源、テンプレート、Flow、結果、プロジェクト出力を一本の流れで説明します。
- [Engine ランタイムオブジェクトマップ](./runtime-object-map.md): クラス名から責任、発生元、最初の確認点を引きます。
- [デバイスサービスチェーン](./device-service-chain.md): DB 資源が `DeviceService`、MQTT、Flow 選択肢になる流れです。
- [テンプレートと Flow チェーン](./template-flow-chain.md): テンプレート、ノード設定、Flow 保存/インポート/実行を説明します。
- [Flow 変換と校正ノード](./flow-conversion-calibration-nodes.md): データ変換、画像変換、校正、校正 ROI、旧色差校正ノードの実入口です。
- [結果表示とプロジェクト引き継ぎチェーン](./result-handoff-chain.md): アルゴリズム結果、画像オーバーレイ、`Projects/*` の境界を整理します。

## この章の読み方

初めてエンジン コードを入力する場合は、次の順序で認識を確立することをお勧めします。

1. まず `ColorVision.Engine` を読んで、サービス、テンプレート、プロセスがメイン プログラムによってどのように接続されているかを理解します。
2. `FlowEngineLib` をもう一度見て、ノードの実行、チェーンの開始/終了、およびプロセス完了イベントがどこから来たのかを理解します。
3. 次に、`ColorVision.FileIO` と `cvColorVision` を追加して、ファイルの読み取りおよび書き込みレイヤーをネイティブ アルゴリズム/デバイス カプセル化レイヤーから区別します。
4. 最後に、`ST.Library.UI` を見て、プロセス エディターが依存するノード UI インフラストラクチャを理解します。
5. Explorer file preview の問題は `ColorVision.ShellExtension` を確認します。これは main business chain ではありません。

## モジュールマップ

### メインエンジン層

- [ColorVision.Engine](./ColorVision.Engine.md): 現在のシステムで最も重要なエンジン エントリ。主に `Services/`、`Templates/`、`MQTT/`、`Messages/` などのディレクトリに重点を置いています。
- [Engine 業務引き継ぎマニュアル](./business-handoff.md): デバイス、テンプレート、Flow、結果、プロジェクトを引き継ぎ視点でつなぎます。

### プロセス実行層

- [FlowEngineLib](./FlowEngineLib.md): ノード実行およびプロセス制御コアですが、完全な実際の実行チェーンになるように `ColorVision.Engine/Templates/Flow/` と一緒に表示する必要があります。
- [テンプレートと Flow チェーン](./template-flow-chain.md): Flow テンプレート、ノード設定器、実行確認を補足します。

### 下部サポート層

- [ColorVision.FileIO](./ColorVision.FileIO.md): ファイル形式、インポートとエクスポート、および関連する I/O 処理。
- [cvColorVision](./cvColorVision.md): ネイティブのビジュアル機能のカプセル化とデバイス/アルゴリズムの相互運用性ブリッジ。

### エディターのベースレイヤー

- [ST.Library.UI](./ST.Library.UI.md): プロセス ノード エディターやプロパティ パネルなどの基本的な UI 機能。

### 外部 Shell 連携層

- [ColorVision.ShellExtension](./ColorVision.ShellExtension.md): Explorer thumbnail extension、COM registration、OpenCvSharp runtime、registry、rollback acceptance。

## 現在、誤って記述されやすい境界がいくつかあります。

- `ColorVision.Engine` は、「すべてのアルゴリズムがここで計算される」という単一のモジュールではなく、テンプレート、機器、プロセス、メッセージ チェーンを整理することに重点を置いています。
- `FlowEngineLib` は、プロセス システム全体の完全な実装ではありません。実際にメイン プログラムに入るときは、`Templates/Flow/` のテンプレート層とウィンドウ層を通過する必要があります。
- `cvColorVision` と `ColorVision.FileIO` はどちらもサポート層に属しており、テンプレート/UI 側の機能と同じ層に混在させないでください。
- `ColorVision.ShellExtension` は Engine main business chain ではなく、Explorer 内の `.cvraw` / `.cvcie` thumbnail preview だけを扱います。

## 最初にソースコードアンカーを読むことをお勧めします

エンジン側の実際のコントロール サーフェスを理解することが目的の場合は、最初に古いドキュメントを調べるよりも、最初にこのコードを確認する方が効果的です。

- `Engine/ColorVision.Engine/Templates/TemplateContorl.cs`
- `Engine/ColorVision.Engine/Templates/TemplateManagerWindow.xaml.cs`
- `Engine/ColorVision.Engine/Templates/Flow/TemplateFlow.cs`
- `Engine/FlowEngineLib/FlowEngineControl.cs`
- `Engine/FlowEngineLib/Start/BaseStartNode.cs`
- `Engine/FlowEngineLib/End/CVEndNode.cs`
- `Engine/ColorVision.ShellExtension/CVThumbnailProviderBase.cs`

## 続きを読む

- [テンプレートモジュール分析](../../03-architecture/components/templates/analysis.md)
- [FlowEngineLib アーキテクチャ](../../03-architecture/components/engine/flow-engine.md)
- [Flow 変換と校正ノード](./flow-conversion-calibration-nodes.md)
- [ColorVision.ShellExtension](./ColorVision.ShellExtension.md)
- [システムランタイム](../../03-architecture/overview/runtime.md)
