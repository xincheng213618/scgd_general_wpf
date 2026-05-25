# JSONテンプレート

このページでは、現在のウェアハウスで実際に利用可能な JSON テンプレート ホスト チェーンのみを説明しており、「ユニバーサル アルゴリズム DSL プラットフォーム + クロスプロジェクト構成フレームワーク」の古いドラフトは維持されません。

## まず、このモジュールが現在どのようなものであるかを見てみましょう

現在のソース コードの状況によると、JSON テンプレート システムはデータベースから独立して存在する構成プラットフォームではなく、`ColorVision.Engine` テンプレート システムの特定のブランチです。現在の中心的な目標は次のとおりです。

- JSON コンテンツを `ModMasterModel.JsonVal` でテンプレート アイテムとしてホストします。
- ユニバーサル エディタ `EditTemplateJson` によるテキスト編集と属性編集の 2 つのモードを提供します。
- 特定のテンプレート タイプで、`ITemplateJson<T>` の形式で同じロード、保存、インポート、エクスポート ロジックのセットを再利用できるようにします。
- `PoiAnalysis`、`SFRFindROI` などの JSON ドライバー テンプレートに統合ホストを提供します。

したがって、完全に独立した構成サブシステムというよりは、「データベース内の JSON テンプレート フォーク」に似ています。

## 現時点で最も重要なファイル

- `Engine/ColorVision.Engine/Templates/Jsons/ITemplateJson.cs`
- `Engine/ColorVision.Engine/Templates/Jsons/TemplateJsonParam.cs`
- `Engine/ColorVision.Engine/Templates/Jsons/EditTemplateJson.xaml`
- `Engine/ColorVision.Engine/Templates/Jsons/EditTemplateJson.xaml.cs`
- `Engine/ColorVision.Engine/Templates/Jsons/PoiAnalysis/TemplatePoiAnalysis.cs`
- `Engine/ColorVision.Engine/Templates/Jsons/SFRFindROI/TemplateSFRFindROI.cs`

「JSON テンプレートを今すぐ保存する方法、編集する方法、テンプレート ウィンドウにハングアップする方法」だけを知りたい場合は、これらのファイルで本体はすでにカバーされています。

## 現在のメインチェーンを実行する方法

### ホスト基本クラス

`ITemplateJson<T>` は、JSON テンプレート ブランチのユニバーサル ホストです。現在、次のことを担当しています。

- `TemplateDicId` を使用して MySQL から `ModMasterModel` を読み取ります
- 各レコードを `TemplateModel<T>` にラップします
- 保存、削除、コピー、インポート、エクスポートを提供します
- 新しいテンプレートを作成するときに、辞書テンプレートのデフォルト JSON から初期コンテンツを生成します

これは、JSON テンプレートはプレーン テキスト編集のように見えますが、現時点では依然としてテンプレート ディクショナリとデータベース レコードに大きく依存していることを意味します。

### パラメータオブジェクト

`TemplateJsonParam` は、現在最も基本的な JSON テンプレート パラメーター オブジェクトです。それは以下を保持します:

- `TemplateJsonModel`
- `ResetCommand`
- `CheckCommand`
- `JsonValueChanged` イベント

ここで、`JsonValue` の実際のセマンティクスは次のとおりです。

- 読み込み時に`JsonHelper.BeautifyJson(...)`でフォーマット
- JSON が有効な場合のみライトバックします `TemplateJsonModel.JsonVal`

`ResetValue()` は、ローカル テキストを単にクリアするのではなく、辞書テンプレートに記録されているデフォルトの JSON に戻ります。

### エディター コントロール

`EditTemplateJson` は、現在の実際の編集エントリです。現在は両方をサポートしています。

- AvalonEditテキストモード
- `JsonPropertyEditorControl` 属性モード
- 説明アノテーションビュー切り替え
- 確認ボタン
- 外部JSON Webサイト補助入口

右下隅にある `json` ボタンの現在の実際の動作は非常に明確です。

- `https://www.json.cn/` を開きます
- 現在の JSON をクリップボードにコピーします

これは、他の隠しコマンドではなく、現在アクティブなファイルの `Button_Click_1` の実際の機能です。

### モードの切り替えと同期

`EditTemplateJson` は現在、単純なテキストボックス ラッパーではありません。それは次のことを行います:

- テキストの変更をデバウンスタイマーと同期します
- `IEditTemplateJson.JsonValueChanged` を介したリバース リフレッシュ インターフェイス
- テキストモードと属性モードを切り替えるときに JSON コンテンツを同期します
- `EditTemplateJsonConfig` を使用して幅とデフォルトの編集モードを記憶します

したがって、ここでの複雑さは主に、アルゴリズム自体ではなく、「両方の編集側で同じ JSON の一貫性を保つ」ことにあります。

## 現在、最もよくある間違いのいくつかが犯されています

### これはユニバーサル ファイル テンプレート プラットフォームではありません

JSON テンプレートの現在のプライマリ ストレージは、ウェアハウス内の任意の JSON ファイルのセットではなく、MySQL の `ModMasterModel.JsonVal` です。そのまま「ディスク構成ディレクトリを読み込む」と書き続けると、実際の実装から外れてしまいます。

### すべての JSON テンプレートが同じビジネス スキーマを共有するわけではありません

`ITemplateJson<T>` はホスト チェーンのみを提供します。それぞれの特定のテンプレートに必要な実際のフィールドは、それぞれの JSON 規約によって決まります。ドキュメントでは、特定のタイプの JSON 構造をシステム全体の統一仕様に書き込むことができなくなりました。

### エディターは単なるテキストエディターではなくなりました

現在、`EditTemplateJson` は属性モードと説明モードの間の切り替えをすでにサポートしています。 AvalonEdit テキスト ボックスについて説明するだけでは、ユーザーが実際に見る機能の半分が失われます。

### 「検証」は現在主にイベントトリガーであり、完全なコンパイラーではありません

`CheckCommand` は `JsonValueChanged` イベント チェーンをトリガーし、特定の応答は呼び出し元によって異なります。スタンドアロンの JSON ルール エンジンとして記述しないでください。

## 推奨される読む順序

1. `Engine/ColorVision.Engine/Templates/Jsons/ITemplateJson.cs`
2. `Engine/ColorVision.Engine/Templates/Jsons/TemplateJsonParam.cs`
3. `Engine/ColorVision.Engine/Templates/Jsons/EditTemplateJson.xaml.cs`
4. `Engine/ColorVision.Engine/Templates/Jsons/PoiAnalysis/TemplatePoiAnalysis.cs`
5. `Engine/ColorVision.Engine/Templates/Jsons/SFRFindROI/TemplateSFRFindROI.cs`

## 続きを読む

- [テンプレート API リファレンス](./api-reference.md)
- [テンプレート管理](./template-management.md)
- [ColorVision.Engine](../../engine-components/ColorVision.Engine.md)