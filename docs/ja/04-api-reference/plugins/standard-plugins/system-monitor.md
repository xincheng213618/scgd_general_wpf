# SystemMonitor プラグイン

このページでは、現在のウェアハウスに実際に存在する SystemMonitor プラグインの実装についてのみ説明し、「バージョン情報 + チューニング マニュアル + 理想的なアーキテクチャ図」という古いドラフトは維持されなくなります。

## まず、このプラグインが現在どのようなものであるかを見てみましょう

現在のソース コードの状況によると、SystemMonitor は軽量のシステム監視プラグインです。コアは独立したアプリケーション シェルではなく、シングルトン監視サービスを中心とした一連の統合ポイントです。

- `SystemMonitors`: データとコマンドを監視するための中央シングルトン。
- `SystemMonitorProvider`: プラグインを設定ページとツール メニューに接続します。
- `SystemMonitorIStatusBarProvider`: オプションの監視項目をメイン プログラムのステータス バーに接続します。
- `SystemMonitorControl`: 監視データを実際に表示する WPF コントロール。

したがって、重い独立したウィンドウプログラムではなく、「システム監視サービス + UI アクセス層」に近いものになります。

## 現在最も重要なファイル

- `Plugins/SystemMonitor/manifest.json`
- `Plugins/SystemMonitor/SystemMonitors.cs`
- `Plugins/SystemMonitor/SystemMonitorControl.xaml(.cs)`
- `Plugins/SystemMonitor/SystemMonitorIStatusBarProvider.cs`

その中で、`SystemMonitors.cs` は実際のランタイム ロジックの大部分を担当します。他の 2 つのタイプは主に、ホスト UI への接続を担当します。

## 現在の機能領域には実際には何が含まれますか?

`SystemMonitors` の実装から判断すると、このプラグインが現在カバーしている監視領域は、古いドキュメントの「時間 + RAM」よりも明らかに広いです。

### パフォーマンスカウンター

プラグインは Windows パフォーマンス カウンターを非同期的に初期化し、定期的に更新します。

- システムCPU使用率
- 現在のプロセスの CPU 使用率
- システムRAMの使用量
- 現在のプロセスのプライベート ワーキング セット

パフォーマンス カウンターの初期化が失敗した場合、現在の実装ではプラグイン全体を中止するのではなく、これらの値を更新しないようにフォールバックします。

### ディスクとネットワーク

プラグインは現在アクティブにロードされ、維持されています。

- すべての準備完了ディスクの容量、使用済みスペース、空きスペース、占有率
- 非ループバック/トンネルネットワークインターフェース情報
- IPv4 アドレス、MAC アドレス、リンク速度、ネットワーク インターフェイスのステータス

データのこの部分はステータス バー スイッチに依存しません。ステータス バーは、その一部をメイン ウィンドウの下部に投影するかどうかを決定するだけです。

### プロセスとランタイム環境

現在、次のものも収集しています:

- メモリ使用量の多いプロセスのトップ 10
- 現在のプロセスのスレッドとハンドルの数
- システム起動時間、アプリケーション実行時間、システム実行時間
- CPU名、ホスト名、.NETランタイム、システムアーキテクチャ、ユーザー名
- ホーム画面の解像度

### GPU とキャッシュ

また、プラグインは `ConfigCuda.Instance` を読み取り、利用可能な場合は CUDA デバイス名とビデオ メモリ情報を表示します。また、キャッシュ サイズの統計とクリーンアップ コマンドも提供します。

## 現在ホストに接続されている 3 つのチェーン

### 設定ページ

`SystemMonitorProvider` は `IConfigSettingProvider` を実装し、設定ページのデータ ソースとして `SystemMonitors.GetInstance()` を使用し、表示コントロールとして `SystemMonitorControl` を使用します。

つまり、設定ページと別個のポップアップ ウィンドウには、実際には 2 セットの監視インスタンスではなく、同じ 1 つのインスタンス データが表示されます。

### ツールメニュー

同じ `SystemMonitorProvider` は `IMenuItemProvider` も実装しています。これにより、現在 `Tool` メニューの下に「パフォーマンス監視」エントリが挿入され、`SystemMonitorControl` をホストする通常の WPF ウィンドウが開きます。

### ステータスバー

`SystemMonitorIStatusBarProvider` は、構成スイッチに基づいてステータス バー項目が存在するかどうかを動的に決定する `IStatusBarProviderUpdatable` を実装します。現在ステータス バーに投影されている項目は次のとおりです。

- 時間
- アプリケーションの実行時間
- CPUテキスト
- RAMテキスト
- ディスクアイコンと残り容量

したがって、古いドキュメントのような 2 つの固定項目を備えた静的なステータス バー プロバイダーではありません。

## 現在の構成モデル

`SystemMonitorSetting` には現在、少なくとも次のスイッチとパラメータが含まれています。

- `UpdateSpeed`
- `DefaultTimeFormat`
- `IsShowTime`
- `IsShowRAM`
- `IsShowCPU`
- `IsShowUptime`
- `IsShowDisk`

古いドキュメントには時間と RAM についてのみ書かれており、完全にはカバーされていません。

## 現在のコマンド サーフェス

`SystemMonitors` 現在公開されているユーザー コマンドには主に次のものがあります。

- `ClearCacheCommand`
- `RefreshDrivesCommand`
- `RefreshNetworkCommand`
- `RefreshProcessesCommand`

これらのコマンドに対応する実際のアクションは、アプリケーション データとログ ディレクトリのクリーンアップ、ディスク リストのリロード、ネットワーク インターフェイス リストのリロード、および高占有プロセス リストのリロードです。

## 現在、最もよくある間違いのいくつかが犯されています

### これはスタンドアロンのウィンドウ プログラム中心のプラグインではありません

メニューからウィンドウが開きますが、そのウィンドウには`SystemMonitorControl`のみがマウントされています。本当に継続的に実行されるコア オブジェクトは、`SystemMonitors` シングルトンです。

### 単なるステータスバー時間プラグインではありません

現在のステータス バーは、3 つの統合チェーンのうちの 1 つにすぎません。実際には、ディスク、ネットワーク、GPU、プロセス リスト、キャッシュ統計などの完全な監視制御に大量のデータが使用されます。

### `IStatusBarProviderUpdatable` は重要です

ステータス バーの表示項目の更新は現在、構成変更をリッスンした後にトリガーされる `SystemMonitorIStatusBarProvider` に依存しています。 `StatusBarItemsChanged`。誤って通常の静的プロバイダーとして記述すると、現在の動的リフレッシュ チェーンが偏って書き込まれます。

### 型の名前付けと名前空間を当然のことと考えないでください

`SystemMonitors` と `SystemMonitorSetting` は現在、プラグイン自体の `SystemMonitor` 名前空間ではなく、`ColorVision.UI.Configs` 名前空間に存在します。これは現在のコードの一部です。許可なくこれを「プラグインの内部自己完結型システム」と言い換えないでください。

## 推奨される読む順序

1. `Plugins/SystemMonitor/SystemMonitors.cs`
2. `Plugins/SystemMonitor/SystemMonitorControl.xaml.cs`
3. `Plugins/SystemMonitor/SystemMonitorIStatusBarProvider.cs`
4. `Plugins/SystemMonitor/manifest.json`

これにより、最初に実際のコントロール サーフェスをキャプチャしてから、メニュー、ステータス バー、および情報の読み込みに戻ることができます。

## 続きを読む

- [Plugins/README.md](../../../../../Plugins/README.md)
- [docs/02-developer-guide/plugin-development/overview.md](../../../02-developer-guide/plugin-development/overview.md)
- [既存プラグイン機能](../README.md)
