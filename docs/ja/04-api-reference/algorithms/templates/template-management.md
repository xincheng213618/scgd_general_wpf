# テンプレート管理

このページでは、現在のウェアハウスで実際に利用可能なテンプレート ホスト チェーンのみについて説明します。「統合フレームワーク ブループリント + 理想的な MVVM レイヤ化 + 大規模な疑似サンプル」の古いドラフトは維持されなくなります。

## まず、このページで今話していることを見てみましょう。

現在のソース コードの状況によると、テンプレート管理は単一のバックエンド サービスではなく、`ITemplate` 基本クラス、グローバル レジストリ、管理ウィンドウ、編集ウィンドウ、および作成ウィンドウで構成されるホスト チェーンです。現在、次のことを担当しています。

- 起動時に特定のテンプレート タイプをスキャンして登録します。
- メイン プログラム内の名前空間ごとにテンプレート エントリを整理します。
- ウィンドウの一般的な編集、作成、インポートとエクスポート、コピー、名前変更を提供します。
- JSON テンプレート、プロセス テンプレート、POI テンプレート、辞書テンプレートなどでホスト インターフェイスを共有します。
- SQLite サンプル ライブラリとグローバル検索アクセスを提供します。

したがって、このページで実際に説明しているのは「テンプレート理論」ではなく、メイン プログラムが現在どのようにさまざまなテンプレートをホストしているかについてです。

## 現時点で最も重要なファイル

- `Engine/ColorVision.Engine/Templates/TemplateContorl.cs`
- `Engine/ColorVision.Engine/Templates/ITemplate.cs`
- `Engine/ColorVision.Engine/Templates/TemplateManagerWindow.xaml.cs`
- `Engine/ColorVision.Engine/Templates/TemplateEditorWindow.xaml.cs`
- `Engine/ColorVision.Engine/Templates/TemplateCreate.xaml.cs`
- `Engine/ColorVision.Engine/Templates/TemplateSearchProvider.cs`
- `Engine/ColorVision.Engine/Templates/TemplateSampleLibrary.cs`
- `Engine/ColorVision.Engine/Templates/TemplateSampleSaveWindow.xaml.cs`

これらのいくつかのポイントを読むだけで、現在のテンプレート システムの主要なメンタル モデルを確立するのに十分です。

## 現在のメインチェーンを実行する方法

### 初期化と登録

`TemplateInitializer` は起動後に `TemplateControl.GetInstance()` をトリガーします。 `TemplateControl` は、アセンブリ内のすべての `IITemplateLoad` 実装をスキャンし、`Load()` を実行します。

一方、 `ITemplate` コンストラクター自体も、テンプレート インスタンスを `TemplateControl.ITemplateNames` に非同期的に登録します。したがって、現在のテンプレート検出は、並行して動作する 2 層のメカニズムです。

- テンプレート オブジェクトはグローバル レジストリに構築されます。
- MySQL が利用可能になった後、具体的なテンプレート ローダーがコンテンツを更新します。

これが、初期化とデータベースの前提条件なしでは多くのテンプレート ページを理解できない理由です。

### テンプレート管理画面

`MenuTemplateManagerWindow` は `TemplateManagerWindow` を開きます。このウィンドウは現在単純なリストではありませんが、次のとおりです。

- `TemplateControl.ITemplateNames` を読む
- タイプ名前空間によるグループ化
- 検索とフィルタリングをサポート
-カードごとのテンプレート表示をサポート
- テンプレートを選択した後、対応するエディタを直接開きます

したがって、これは単なるメニュー ポップアップ ウィンドウではなく、「テンプレート エントリ アグリゲーター」の役割を果たします。

### テンプレート編集ウィンドウ

`TemplateEditorWindow` は、現在最も多用途なテンプレート ホスト ウィンドウです。これは `template.Load()` で始まり、テンプレートの種類に応じて 2 つのパスをたどります。

- 通常のテンプレート: `PropertyGrid` を右側に配置
- カスタム テンプレート: `GetUserControl()` を呼び出し、テンプレートに適切な領域を自動的に引き継がせます。

ウィンドウも均一に接続されています。

- コマンドの作成、コピー、保存、削除
- `SetSaveIndex(...)` 選択を切り替えるとき
- `SetUserControlDataContext(...)` または `GetParamValue(...)`
- 列の並べ替え、検索、ダブルクリックの動作

これは、インターフェイスが大きく異なるにもかかわらず、現在のさまざまなテンプレートが依然として同じホスト シェルを共有できる理由でもあります。

### テンプレート作成画面

`TemplateCreate` 「名前入力ボックスが 1 つだけ表示される」ウィンドウではなくなりました。現在実装されているように、新しいテンプレート用に複数のソースが提供されます。

- システムのデフォルトのテンプレート
- 現在のコピー (コピー後に一時的に保存されたテンプレートのコンテンツ)
- SQLite サンプル ライブラリ内の過去の例

これらのソースはカードにレンダリングされ、グループごとにフィルターされます。最後に、`ApplyTemplateSource(...)` は、選択したソースを作成するテンプレートに挿入します。

これは、現在のテンプレート作成チェーンが単なる「CreateDefault() + 手動入力パラメーター」ではなくなっていることを示しています。

### 検索とサンプル ライブラリ

`TemplateSearchProvider` は、すべてのテンプレート名をグローバル検索エントリに登録します。 `TemplateSampleLibrary` は、テンプレート サンプルをユーザー ドキュメント ディレクトリの SQLite ライブラリに保存します。

- `.../Templates/TemplateSamples.db`

現在保持しているものは次のとおりです。

- テンプレートコードとテンプレートの種類
- グループ名とサンプル名
- 説明テキスト
- シリアル化されたテンプレート コンテンツ

そのため、テンプレート管理には、MySQL メイン ストレージに加えて、ローカル サンプル再利用チェーンが追加されました。

## 現在、最もよくある間違いのいくつかが犯されています

### これは純粋なサービス層システムではありません

現在、`TemplateManagerWindow`、`TemplateEditorWindow`、`TemplateCreate` など、多くの主要なロジックが WPF ウィンドウに直接書き込まれています。 「ホストは ViewModel のみをバインドし、ロジックはすべてサービス層にある」と説明し続けますが、これは実際のコードと矛盾します。

### さまざまなテンプレートの永続化メソッドは均一ではありません。

一部のテンプレートは主に MySQL に依存しており、一部のテンプレートはファイルのインポートとエクスポートをサポートし、一部のテンプレートは SQLite サンプル ライブラリも使用します。ドキュメントでは、すべてのテンプレートが同じストレージ モデルを持つと想定できなくなりました。

### `IsUserControl` と `IsSideHide` は動作を大幅に変更します

現在のテンプレート ホストは固定レイアウトではありません。 `IsUserControl` は右側をテンプレート カスタム コントロールに変更し、`IsSideHide` はウィンドウ レイアウトとダブルクリック動作も変更します。これら 2 つのスイッチを無視すると、多くのテンプレート ページが説明不能になります。

### テンプレートの登録とデータベース接続は依然として結合されています

`ITemplate` コンストラクトはインスタンスを登録しますが、特定のテンプレート コンテンツの多くは、MySQL 接続が実際にロードされるまで待つ必要があります。テンプレート システムを「純粋にローカルな静的登録」として記述すると、重要な前提が欠けています。

## 推奨される読む順序

1. `Engine/ColorVision.Engine/Templates/ITemplate.cs`
2. `Engine/ColorVision.Engine/Templates/TemplateContorl.cs`
3. `Engine/ColorVision.Engine/Templates/TemplateManagerWindow.xaml.cs`
4. `Engine/ColorVision.Engine/Templates/TemplateEditorWindow.xaml.cs`
5. `Engine/ColorVision.Engine/Templates/TemplateCreate.xaml.cs`
6. `Engine/ColorVision.Engine/Templates/TemplateSearchProvider.cs`
7. `Engine/ColorVision.Engine/Templates/TemplateSampleLibrary.cs`

## 続きを読む

- [JSON テンプレート](./json-templates.md)
- [プロセスエンジン](./flow-engine.md)
- [テンプレート分析の概要](../../../03-architecture/components/templates/analysis.md)