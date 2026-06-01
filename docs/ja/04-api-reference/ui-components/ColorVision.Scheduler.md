#ColorVision.Scheduler

このページでは、現在 `UI/ColorVision.Scheduler/` に実装されているスケジューリング機能のみを説明しており、古いドキュメントにある「一般的な Quartz チュートリアル + 想像上のタスク プラットフォーム関数リスト」は維持されなくなります。

## モジュールの配置

`ColorVision.Scheduler` は現在、デスクトップ側のタスクのスケジューリングおよび監視モジュールです。中心となるのは「抽象的なタスク タイプのリスト」ではなく、次の 3 つの実際のチェーンです。

- `QuartzSchedulerManager` は、Quartz スケジューラとタスク リカバリを管理します
- `scheduler_tasks.json` タスク構成の保存
- `SchedulerHistory.db` 実行履歴と統計的回復データの保存

したがって、これは純粋な UI コントロールでも、単なる Quartz ラッピング レイヤーでもありません。

## 現時点で最も重要なファイル

プロジェクト ディレクトリを見て、最初に知っておくべき最も重要な点は次のとおりです。

- `QuartzSchedulerManager.cs`: スケジューラのメインエントランス
- `TaskViewerWindow.xaml(.cs)`: タスクの表示、フィルタリング、および右クリック操作ウィンドウ
- `CreateTask.xaml(.cs)`: タスク ウィンドウの作成と編集
- `TaskExecutionListener.cs`: 監視と統計の更新を実行します。
- `Data/SchedulerDbManager.cs`: 履歴 SQLite 永続性
- `MenuTaskViewer.cs`: メニューエントリとイニシャライザ
- `SchedulerInfo.cs`: タスクのプレゼンテーションと永続化モデル

## キー入力タイプ

### `QuartzSchedulerManager`

`QuartzSchedulerManager` は、現在のスケジューリング モジュールの中心的なオブジェクトです。それは次のことを担当します。

- Quartz スケジューラを起動します
- `IJob` タイプのロードされたアセンブリをスキャンします
- メンテナンス `TaskInfos`
- JSONファイルからタスク設定を読み込みます
- 起動後に過去のミッションを復元
- タスクを一時停止、再開、削除、更新、作成するメソッドを提供します

現在のタスク構成ファイルは、デフォルトで次の場所に配置されます。

- `%AppData%/ColorVision/scheduler_tasks.json`

これは、現在のタスク定義がデータベース内に完全に存在しているわけではなく、主に JSON 構成に基づいており、SQLite 履歴によって補足されていることを示しています。

### `TaskViewerWindow`

`TaskViewerWindow` は、現在のタスク管理メイン ウィンドウです。それは次のことを担当します。

- `TaskInfos` をバインドする
- 名前、グループ、ステータスによるフィルタリング
- 登録されたタスクの次回および最後の実行時刻をスケジューラから読み取ります
- 右クリック メニューから編集、プロパティの表示、一時停止、続行、即時実行、削除、履歴の表示を行う

このページの古い文書にある「大型で包括的な監視パネルの設計図」は、ここにある実際の窓ほど価値はありません。

### `CreateTask`

`CreateTask` ウィンドウは、新規作成および編集タスクを担当します。 `SchedulerInfo` と連携して、タスクが最終的にどのようにシリアル化、復元、更新されるかを決定します。

### `SchedulerDbManager`

実行履歴は同じ JSON ファイルではなく、別の SQLite データベースに保存されます。 `SchedulerDbManager` は現在、次のことを担当しています。

- `%AppData%/ColorVision/SchedulerHistory.db` の初期化
- 実行記録の書き込み
- 単一タスクまたは完全な実行履歴をクエリします
- 再起動後の回復のための統計を計算します
- 古い記録をクリーンアップする

再起動後も現在の「実行回数、成功・失敗回数、平均所要時間」などのデータを継続できるのもこのためです。

### `TaskExecutionListener`

実行時の統計更新と実行フィードバックは、ウィンドウ自体によるポーリングによって取得されるのではなく、リスナーを介してタスクのステータスと実行履歴を書き戻すことによって取得されます。

## 現在のランタイムのメインチェーン

スケジューリング モジュールは現在、次のチェーンに近いです。

1. `TaskViewerInitializer` またはメニューエントリにより `QuartzSchedulerManager.GetInstance()` がトリガーされます。
2. `QuartzSchedulerManager` は Quartz スケジューラを開始します。
3. 現在ロードされているアセンブリ内の `IJob` 型をスキャンし、タスク タイプ ディクショナリを構築します。
4. `%AppData%/ColorVision/scheduler_tasks.json` を読み取ります。
5. 起動後の既存タスクの回復を遅らせます。
6. `TaskExecutionListener` は、タスクの実行時にステータスと統計を更新します。
7. __​​IC_33__ は実行記録を `SchedulerHistory.db` に書き込みます。
8. `TaskViewerWindow` は、これらのステータス、履歴、統計をユーザーに表示します。

このリンクは、古いドキュメントの「タスク エディター/監視パネル/ログ ビューアーの 3 層アーキテクチャ」よりも既存の実装に近いものです。

## 現在の実装の境界は何ですか?

### タスク タイプは読み込まれたアセンブリから取得されます

現在、`QuartzSchedulerManager` は `AssemblyService.Instance.GetAssemblies()` を走査し、`IJob` を実装する型を収集し、表示名として `DisplayNameAttribute` を優先します。

したがって、新しいタスク タイプを追加することは、特定のタスク タイプ テーブルに登録するのではなく、アセンブリによってスキャンできる `IJob` 実装を追加することになります。

### 構成のリカバリーと実行履歴は 2 つのストレージのセットです

現在のタスクの定義とリカバリは主に JSON に依存しています。実行履歴と統計の回復は主に SQLite に依存します。この 2 つを 1 つのデータベース ディスパッチ センターに混在させないでください。

### タスク ウィンドウは、概略図ではなく、実際の管理の入り口です

現在、最も重要なユーザー ポータルは `TaskViewerWindow` と `CreateTask` です。コードが特定の実装に直接対応できない限り、多くの古い文書で捏造されている「バッチ エクスポート、統計レポート、および複雑なパネル分割」を既存の機能としてリストし続ける必要はありません。

## 現在このプロジェクトをどのように読むのがより適切ですか?

### スケジューラがどのように起動および復元されるかを確認したい

まずはご覧ください:

- `QuartzSchedulerManager.cs`
- `MenuTaskViewer.cs`

### タスクインターフェースと操作入口が見たい

まずはご覧ください:

- `TaskViewerWindow.xaml(.cs)`
- `CreateTask.xaml(.cs)`

### 実行履歴や統計情報を確認したい

まずはご覧ください:

- `Data/SchedulerDbManager.cs`
- `TaskExecutionListener.cs`
- `ExecutionHistoryWindow.xaml(.cs)`

## このページはそれ以上何もしません

このページでは、次のような危険性の高いコンテンツは管理されなくなります。

- Quartz全般のサンプルコード集
・未検証のシステムタスク/業務タスク/保守タスク分類表
- 仮想統合タスクプラットフォーム機能マトリックス
- 古いバージョン番号とターゲット フレームワーク リスト

後で特定のタスク タイプを追加する必要がある場合は、ここでチュートリアル スタイルのコンテンツを書き続けるのではなく、実際のタスク実装またはウィンドウ ページに直接移動する必要があります。

## 続きを読む

- [UIコンポーネントの概要](./README.md)
- [ColorVision.UI](./ColorVision.UI.md)
- [ColorVision.Database](./ColorVision.Database.md)