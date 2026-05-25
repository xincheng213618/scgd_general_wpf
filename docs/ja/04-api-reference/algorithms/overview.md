# アルゴリズム体系の概要

このページでは、現在のウェアハウスで実際に実行されているテンプレートとアルゴリズム アクセス リンクのみを説明しており、「アルゴリズム分類百科事典 + サンプル コード + GPU 機能の概要」という古いドラフトは維持されなくなります。

## まず、このシステムが実際にどこに当てはまるのかを見てみましょう。

現在「アルゴリズム」に最も直接関係しているコードは、次の 1 つのディレクトリにあるだけではありません。

- `Engine/ColorVision.Engine/Templates/`: テンプレート定義、テンプレート管理、テンプレート編集、およびほとんどのビジネス アルゴリズム UI アクセス ポイント。
- `Engine/FlowEngineLib/`: プロセス ノード、開始/終了チェーン、および実行制御。
- `Engine/ColorVision.Engine/Services/Devices/Algorithm/`: アルゴリズム デバイス サービス アクセス プレーン。
- `Engine/cvColorVision/` および下位レベルのネイティブ ライブラリ: 実際の基礎となる計算と相互運用性を実行します。

したがって、この章を「管理アルゴリズム関数のディレクトリ」としてのみ理解すると、現在の実装から直接逸脱することになります。

## 現在のメインチェーンはどのようにつながっているのでしょうか?

現在の状況から判断すると、最も一般的に実行されているアルゴリズム/テンプレートのチェーンはおおよそ次のとおりです。

1. `TemplateContorl` は、ロードされたアセンブリ内の `IITemplateLoad` 実装をスキャンし、テンプレートをシステムに登録します。
2. `TemplateManagerWindow` と `TemplateEditorWindow` は、ユーザーがテンプレートを参照、作成、編集できるようにする役割を果たします。
3. 特定のビジネス アルゴリズムの UI クラスは通常、`DisplayAlgorithmBase` を継承し、エントリ `OpenTemplateCommand` のタイプを公開します。
4. これらのアルゴリズム UI は、`CVTemplateParam`、ファイル パス、デバイス情報、およびその他のパラメーターを `SendCommand(...)` にアセンブルします。
5. その後、パラメーターは `MQTTAlgorithm` または隣接するサービス チェーンを通じて実際の実行側に送信されます。
6. プロセステンプレートの場合、`TemplateFlow` + `FlowEngineToolWindow` + `FlowEngineLib` の実行チェーンに入ります。

これは、`Templates/*/Algorithm*.cs` に見られるクラスの多くは、現在、最終的な演算子自体よりも「アルゴリズム フロントエンド アダプター」に近い役割を担っていることを意味します。

## 現在のテンプレート システムの最も重要な部分

### テンプレートの登録と管理

この部分の主な焦点は次のとおりです。

- `ITemplate.cs`
- `TemplateContorl.cs`
- `TemplateManagerWindow.xaml(.cs)`
- `TemplateEditorWindow.xaml(.cs)`

これらは、テンプレートがどのように表示されるか、どのように開かれるか、どのように編集プロセスに入るかを決定します。

### フローテンプレート

`Templates/Flow/` は、通常のパラメータ テンプレートの単純な分岐ではなく、フローチャート、プロセス編集ウィンドウ、インポートとエクスポート、およびバッチ実行を接続する特別なテンプレート ファミリです。

現在の主要な入り口は次のとおりです。

- `TemplateFlow.cs`
- `FlowEngineToolWindow.xaml(.cs)`
- `DisplayFlow.xaml(.cs)`

### JSON テンプレート

`Templates/Jsons/` は現在、JSON 構成をコアとしてテンプレート実装のバッチを実行しています。その共通リンクは主に次のとおりです。

- `ITemplateJson<T>`: 共通ロジックをロード、保存、インポート、エクスポートします。
- `TemplateJsonParam`: JSON テンプレート パラメータの基本タイプ。
- `EditTemplateJson.xaml(.cs)`: デュアルモード編集コントロール。テキスト編集と属性編集の切り替えをサポートします。

これが、テンプレート システムに従来のパラメーター オブジェクトと JSON テキスト エディターの両方が表示される理由です。

### ビジネステンプレートファミリー

現在も直接表示される主なテンプレート ファミリには次のものがあります。

- `POI/`
- `ARVR/`
- `JND/`
- `LedCheck/`
- `Compliance/`
- `Jsons/` に基づく複数のビジネス テンプレートの実装

これらのディレクトリは、同時に同じルールに従って設計されたものではありません。読むときは、それらがまったく同じレベルの抽象化を持っている必要があると想定しないでください。

## 現時点で最も誤解されやすい点のいくつか

### 誤解 1: `Algorithm*.cs` を最終的なアルゴリズム実装として扱う

これらのクラスの多くが現在実行していることは次のとおりです。

- テンプレート編集ウィンドウを開く
- UI側の選択状態を維持
- メッセージパラメータを組み立てる
- `PublishAsyncClient(...)` に電話してください

実際の低レベルの処理は、多くの場合、デバイス サーバー、MQTT ピア、ネイティブ ライブラリ、またはその他のリンクで実行されます。

### 誤解 2: `POI` は単なる小さな独立したトピックであると考える

現在のコードから判断すると、POI は依然として複数の ARVR/測位/分析アルゴリズムによって共有される上流テンプレートの依存関係です。そのテンプレートとポイント データは、複数のアルゴリズム UI によって繰り返し参照されます。

### 誤解 3: テンプレート システムからフロー テンプレートを除外する

フロー テンプレートはプレゼンテーションがより複雑になっているだけですが、それでもテンプレート システムを通じてメイン プログラムに入り、その後の実行は隣接するウィンドウとプロセス ライブラリによって引き継がれます。

### 誤解 4: JSON テンプレートは単なる「一時的な互換性レイヤー」であると考える

現在の `Jsons/` ディレクトリと `ITemplateJson<T>` は依然として実際に使用されているメイン パスの 1 つであり、厳密に型指定されたテンプレートで完全に置き換えられたように記述すべきではありません。

## 推奨される読む順序

1. `Engine/ColorVision.Engine/Templates/TemplateContorl.cs`
2. `Engine/ColorVision.Engine/Templates/TemplateManagerWindow.xaml.cs`
3. `Engine/ColorVision.Engine/Templates/TemplateEditorWindow.xaml.cs`
4. `Engine/ColorVision.Engine/Templates/Flow/TemplateFlow.cs`
5. `Engine/ColorVision.Engine/Templates/Jsons/ITemplateJson.cs`
6. `Engine/ColorVision.Engine/Templates/Jsons/EditTemplateJson.xaml.cs`
7. `POI/`、`ARVR/`、`Jsons/`、`Algorithm*.cs` などの特定のビジネス アルゴリズム カタログ

## 続きを読む

- [アルゴリズムとテンプレートの概要](./README.md)
- [テンプレートモジュール分析](../../03-architecture/components/templates/analysis.md)
- [FlowEngineLib アーキテクチャ](../../03-architecture/components/engine/flow-engine.md)