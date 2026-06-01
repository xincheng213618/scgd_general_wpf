# テンプレート API リファレンス

このページは、現在のソース コード内で比較的安定したテンプレート ホスト エントリのみを保持し、「完全な署名マニュアル」を維持しようとはしません。理由は簡単です。テンプレートの動作の多くは、具体的なサブクラスのオーバーライド、データベースの状態、およびユーザー コントロール フックに依存しており、古いスタイルの API テーブルは簡単にドリフトする可能性があります。

## まず、どの入り口が最も知っておく価値があるかを見てみましょう。

現在のコードによれば、テンプレート システムで最も安定しており、優先順位を理解する価値があるのは次のタイプです。

- `ITemplate`
- `ITemplate<T>`
- `ITemplateJson<T>`
- `TemplateControl` / `IITemplateLoad`
- `ParamBase` / `ModelBase` / `ParamModBase`
- `TemplateModel<T>`
- `TemplateEditorWindow` / `TemplateCreate`

このページの焦点は、現在の実装においてこれらのエントリ ポイントがどのような責任を負っているかを説明することです。

## コアホストタイプ

### Iテンプレート

`ITemplate` は、すべてのテンプレートのホスト基本クラスです。現在の最も重要な責任には次のものがあります。

- 構築時に `TemplateControl.ITemplateNames` に登録してください
- `Load()`、`Save()`、`Import()`、`Export()`、`Delete()`、`Create()` などのライフサイクル フックを提供します。
- `ItemsSource`、`Count`、`GetValue(...)`、`GetParamValue(...)` の露出
- `IsSideHide`、`IsUserControl` などのホスト ウィンドウの動作を制御します。
- `HasCreateTemplateSource`、`ImportName`、`CreateDefault()`、およびウィンドウを作成するためのその他のソース機能を提供します。

`ITemplate` は現在、単なるインターフェイス定義ではなく、具体的な基本クラスであることに注意することが重要です。

### `ITemplate<T>`

`ITemplate<T>` は、`T : ParamModBase, new()` が通常のパラメーター テンプレートの最も一般的なジェネリック基本クラスです。現在主に扱っているのは以下の通りです。

- `ObservableCollection<TemplateModel<T>> TemplateParams`
- `ItemsSource`
- `Count`
- `GetTemplateNames()`
- `GetTemplateIndex(...)`
- `GetParamValue(...)`

これらの一般的なリストの動作は統一されています。

さらに、`TemplateDicId` に従って辞書テンプレートからデフォルトのパラメーター オブジェクトを生成する役割も担っているため、このレイヤーは単なるコレクション ラッパーではありません。

### `ITemplateJson<T>`

`ITemplateJson<T>` は、`T : TemplateJsonParam, new()` が存在する JSON テンプレート ブランチのホスト基本クラスです。 `ITemplate<T>` との主な違いは次のとおりです。

- データソースは`ModMasterModel.JsonVal`です
- デフォルト値を作成する場合は `SysDictionaryModModel.JsonVal` に進みます
- `.cfg` および ZIP 周りのインポートとエクスポート
- レプリケーション ロジックは JSON でシリアル化されたコピーに基づいています

テンプレートのコンテンツが本質的に JSON テキストである場合、このレイヤーは通常、`ITemplate<T>` よりも実際の実装に近くなります。

## 登録および検出ポータル

### テンプレートコントロール

`TemplateControl` は現在のテンプレート レジストリです。主に以下を維持します。

- `ITemplateNames`
- `AddITemplateInstance(...)`
- `ExitsTemplateName(...)`
- `FindDuplicateTemplate(...)`

そして、初期化時にすべての `IITemplateLoad` 実装をスキャンして、具体的なテンプレート タイプ自体がコンテンツを読み込めるようにします。

### IITemplateLoad

`IITemplateLoad` は、テンプレート読み込み拡張ポイントです。現在、多くのテンプレート クラスは、`TemplateControl.Init()` がスキャンされたときに独自の `Load()` を実行するためにこれを実装しています。

これは、現在のテンプレート システムとアプリケーションの起動シーケンスが結合されている重要な理由の 1 つでもあります。

## パラメータとモデルの基本クラス

### パラムベース

`ParamBase` は最も薄いレイヤーであり、次のもののみを提供します。

- `Id`
- `Name`

これは、すべてのテンプレート パラメーター オブジェクトの共通の親クラスとして適しています。

### モデルベース

現在の実装における `ModelBase` の値は、名前よりも具体的です。これは、`ModDetailModel` リストをシンボル名でインデックス付けされたパラメータ辞書にマップし、以下を提供します。

- `GetValue<T>(...)`
- `SetProperty(...)`
- `GetParameter(...)`
- `GetDetail(...)`
- `StringToDoubleArray(...)`
- `DoubleArrayToString(...)`

つまり、多くのテンプレート パラメーター属性が通常の C# 属性と同じように記述できるのは、最下層で実際に辞書マッピングと型変換が行われているためです。

### ParamModBase

`ParamModBase` 続いて、テンプレート マスター レコードとパラメーター詳細レコードを組み合わせると、これはほとんどのデータベース ドライバー テンプレート パラメーター オブジェクトの直接の基本クラスになります。

## UI ホスト関連のタイプ

### `TemplateModel<T>`

`TemplateModel<T>` は、現在のリスト項目ラッパー オブジェクトです。 `Value` に加えて、次のことも前提としています。

- `Key`
- `IsSelected`
- `IsEditMode`
- 右クリックメニュー
- 名前変更および名前コピーコマンド

したがって、ユーザーがリストに表示する「テンプレート項目」は、裸のパラメーター オブジェクトではなく、UI 状態を含むこのパッケージ化層です。

### テンプレートエディタウィンドウ

`TemplateEditorWindow` は、最も多用途なテンプレート編集ホストです。テンプレートが `IsUserControl` であるかどうかに応じて、右側に表示されます。

- `PropertyGrid`
- テンプレートのカスタマイズ `UserControl`

同時に、作成、コピー、保存、削除、名前変更、検索、並べ替え、選択の切り替えを統合された方法で引き継ぎます。

### テンプレートの作成

`TemplateCreate` は現在、テンプレート作成ソースの選択を担当しています。デフォルトのテンプレートに加えて、以下をサポートします。

- 現在のコピー
- SQLite サンプル ライブラリのサンプル

したがって、テンプレート名を入力するだけの小さなポップアップ ウィンドウではなくなりました。

## 現在、最もよくある間違いのいくつかが犯されています

### `ITemplate` は純粋なインターフェイスではありません

現在、登録、ウィンドウの作成、さまざまなライフサイクル メソッドなど、多くのデフォルト動作は `ITemplate` 基本クラスに直接記述されています。純粋に抽象的な契約として書くと、読者に誤解を与える可能性があります。

### 多くの動作は、特定のテンプレートがオーバーライドされた場合にのみ確立されます。

たとえば、`Import()`、`Export()`、`CreateDefault()`、`GetUserControl()` などのメソッドは、基本クラスに完全には実装されていない可能性があります。基本クラスのメソッド テーブルを直接「すべてのテンプレートで完全にサポートされる関数のリスト」とみなすことはできません。

### データモデルとUIモデルが混在している

`TemplateModel<T>`、`TemplateEditorWindow`、`TemplateCreate` これらのタイプは、現在のテンプレート システムが UI 状態を完全には削除していないことを示します。 API 解釈では、この現実境界を維持する必要があります。

### JSON テンプレートと通常のパラメーター テンプレートは 2 つのホスト ブランチです

これらはどちらもテンプレートに分類されますが、`ITemplate<T>` と `ITemplateJson<T>` のデフォルトの永続化、作成、インポートおよびエクスポートのパスは異なります。

## 推奨される読む順序

1. `Engine/ColorVision.Engine/Templates/ITemplate.cs`
2. `Engine/ColorVision.Engine/Templates/Jsons/ITemplateJson.cs`
3. `Engine/ColorVision.Engine/Templates/ModelBase.cs`
4. `Engine/ColorVision.Engine/Templates/ParamModBase.cs`
5. `Engine/ColorVision.Engine/Templates/TemplateModel.cs`
6. `Engine/ColorVision.Engine/Templates/TemplateEditorWindow.xaml.cs`
7. `Engine/ColorVision.Engine/Templates/TemplateCreate.xaml.cs`

## 続きを読む

- [テンプレート管理](./template-management.md)
- [JSON テンプレート](./json-templates.md)
- [プロセスエンジン](./flow-engine.md)