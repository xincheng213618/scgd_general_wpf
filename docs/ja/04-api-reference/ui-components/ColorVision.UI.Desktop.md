#ColorVision.UI.デスクトップ

このページでは、現在実装されているデスクトップウィンドウとUI/ColorVision.UI.Desktopのサポート機能についてのみ説明しており、旧文書の「システム全体のメインプログラムの入口」の書き方は継続しておりません。

## モジュールの配置

`ColorVision.UI.Desktop` は現在、デスクトップ補助シェル関数セットに近く、主に以下を提供します。

- 設定ウィンドウ
- 設定ウィザード
・メニュー項目管理画面
・構成管理画面
- サードパーティアプリケーションへのアクセス
- DLL情報閲覧などの補助ウィンドウ

これは、倉庫全体への主要なアプリケーションの入り口ではありません。現在の実際のメイン プログラム プロジェクトは `ColorVision/` にあり、ここの `App.xaml.cs` と `MainWindow.xaml.cs` は非常に軽量です。

## 現在最も重要なディレクトリ

プロジェクト ディレクトリの中で、最初に読む価値のあるものは次のとおりです。

- `Settings/`: 統合設定ウィンドウ
- `Wizards/`: ウィザード ウィンドウ、ステップ検出、ウィンドウ構成
- `MenuItemManager/`: メニュー項目の管理と永続化
- `ThirdPartyApps/`: システム ツールとサードパーティ アプリケーションへの入り口
- `Marketplace/`: DLL バージョン表示およびその他の補助ウィンドウ
- `ConfigManagerWindow.xaml(.cs)`: 構成管理ウィンドウ
- `Feedback/`、`Download/`、`TimedButtons/`、`WebViewService.cs`: その他のデスクトップ補助機能

## キー入力タイプ

### アプリとメインウィンドウ

現在、`App.xaml.cs` は非常に軽い部分的な `Application` であり、`MainWindow.xaml.cs` は基本的な構築ロジックのみを保持しています。

これは次のことを意味します。

- このプロジェクトには `App` と `MainWindow` があります
- ただし、古いドキュメントで説明されているような、完全な起動プロセスとシステム初期化ロジックを実行する中心的なファイルではありません。

このプロジェクトを読むときは、空のシェルの入り口に焦点を当てるのではなく、最初にさまざまな関数ウィンドウとマネージャーに注目することは非常に価値があります。

### 設定ウィンドウ

`Settings/SettingWindow.xaml.cs` は、現在のセットアップ システムのメイン デスクトップ エントリです。それは次のことを担当します。

- `ConfigSettingManager.GetInstance().GetAllSettings()` を読む
- グループごとにタブを作成
- `ConfigSettingType` に基づいて、タブ ページ、プロパティ ページ全体のタイプ、または単一のプロパティ コントロールを生成するかどうかを決定します。
- `ViewType` で遅延読み込みを実行し、ウィンドウの初期化中にすべてのビューが一度にインスタンス化されるのを回避します。

したがって、このページの古いドキュメントの「設定ウィンドウの統合」の方向性は正しいですが、実装の詳細は `ConfigSettingManager` + 遅延読み込みに該当するはずです。

### WizardManager / WizardWindow / WizardWindowConfig

現在のウィザード チェーンは次のタイプのグループです。

- `WizardManager`: 反射スキャン `IWizardStep`
- `WizardWindow`: マルチステップ ウィンドウと完了ロジック
- `WizardWindowConfig`: ウィンドウの構成と完了ステータス

`WizardManager` はアセンブリを走査して `IWizardStep` をインスタンス化し、`Order` で並べ替えます。 `WizardWindow` は進行状況バーを表示し、前のステップと次のステップを切り替えて、検証を完了します。

この部分は、現在のプロジェクトで最も明確な「デスクトップ補助プロセス チェーン」です。

### MenuItemManagerConfig と MenuItemManagerWindow

現在、`MenuItemManagerConfig` はメニュー項目設定の永続化を担当し、`MenuItemManagerWindow` は視覚的な管理インターフェイスを提供します。これらは、グローバル メニュー ランタイム自体ではなく、UI シェル構成ツールに属します。

### ConfigManagerウィンドウ

`ConfigManagerWindow` は、より集中的な観点から構成アイテムを管理するために使用されるもう 1 つのデスクトップ側管理ウィンドウです。これは `SettingWindow` と完全に重複するわけではなく、基本インターフェイス層ではなくデスクトップ ツール層に属します。

### ViewDllVersionsウィンドウ

`Marketplace/ViewDllVersionsWindow.xaml.cs` は現在、読み込まれたアセンブリを走査し、以下を収集します。

- 名前
- 組み立てバージョン
- ファイルバージョン
- 製品バージョン
- 会社情報
- パス

これは、コア プラグインの更新プロセス自体というよりも、実行時の診断とトラブルシューティングのウィンドウです。

### SystemAppProvider と WebViewService

- `ThirdPartyApps/SystemAppProvider.cs` は、システム ツールとサードパーティ アプリケーションの入り口を担当します。
- `WebViewService.cs` は、このプロジェクトがいくつかのデスクトップ WebView 関連機能も備えていることを示します。

## 現在のランタイムのメインチェーン

このプロジェクトには現在単一のメイン チェーンがありませんが、いくつかのデスクトップ補助チェーンが共存しています。さらに注目に値するのは次のとおりです。

1. 設定チェーン: `SettingWindow` -> `ConfigSettingManager` -> 構成ページ/プロパティ ページの遅延読み込み。
2. ウィザード チェーン: `WizardManager` -> `IWizardStep` 検出 -> `WizardWindow` の切り替えと完了。
3. 管理チェーン: `MenuItemManagerWindow` / `ConfigManagerWindow` / `ViewDllVersionsWindow` は、さまざまな側にデスクトップ管理ウィンドウを提供します。

## 現在の実装の境界は何ですか?

### システム全体のメインの入り口ではありません

これは、このページで最もよくある間違いです。現在のプロジェクトの `App` と `MainWindow` は非常に軽いため、製品全体の唯一のスタートアップ センターとして `ColorVision.UI.Desktop` について話し続けることはできません。

### すべての機能が MainWindow を中心に展開しているわけではありません

このプロジェクトは、ウィンドウと管理ツールのコレクションに似ています。価値の多くは、1 つの巨大なメイン​​ ウィンドウ オーケストレーション レイヤーではなく、独立したウィンドウから得られます。

### 古いドキュメントに記載されている SystemInitializer はこのプロジェクトには存在しません

現在、`UI/ColorVision.UI.Desktop` ディレクトリには実際の `SystemInitializer` 実装はありません。古いドキュメントでは、それが既存のコンポーネントとしてリストされており、存在しないコントロール ポイントを見つけるよう読者を直接誤解させます。

## このモジュールの読み方は現在、より適切です

### 設定および構成ウィンドウを見たい

まずはご覧ください:

- `Settings/SettingWindow.xaml.cs`
- `ConfigManagerWindow.xaml.cs`

### ウィザードと初回構成プロセスを確認したい

まずはご覧ください:

- `Wizards/WizardWindow.xaml.cs`
- `Wizards/WizardWindowConfig.cs`

### メニュー管理とデスクトップ補助ウィンドウを表示したい

まずはご覧ください:

- `MenuItemManager/MenuItemManagerConfig.cs`
- `MenuItemManager/MenuItemManagerWindow.xaml.cs`
- `Marketplace/ViewDllVersionsWindow.xaml.cs`
- `ThirdPartyApps/SystemAppProvider.cs`

## このページはそれ以上何もしません

このページでは、次のような危険性の高いコンテンツは管理されなくなります。

- このプロジェクトをシステム全体のメインプログラムの入り口として記述します
- コンポーネントの説明が存在しません (例: `SystemInitializer`)
- 大きなバージョン番号と疑似 API リスト
- 軽量の `App` / `MainWindow` を完全な起動プロセス センターに拡張します。

## 続きを読む

- [UIコンポーネントの概要](./README.md)
- [ColorVision.UI](./ColorVision.UI.md)
- [ColorVision.Solution](./ColorVision.Solution.md)