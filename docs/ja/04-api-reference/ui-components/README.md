# UIコンポーネントの概要

この章では、現在のコード実装と一致する UI モジュールの紹介ページのみが維持され、旧バージョンの概要にあった「バージョン互換性マトリックス + サンプル コード + 拡張チュートリアル」という混合記述方法は維持されなくなりました。

## この章の読み方

この倉庫に初めて入る場合は、次の順序で認識を確立することをお勧めします。

1. まず [現在の UI DLL 文書カバレッジ](./current-ui-dll-coverage.md) を読み、現在の 10 個の UI プロジェクト、対応文書、リリース証跡を確認します。
2. 次に [UI DLL コンポーネントハンドブック](./component-handbook.md) を読み、DLL 単位の境界を確認します。
3. DLL をリリースまたは置換する場合は、[UI DLL リリースプレイブック](./ui-dll-release-playbook.md) と [UI DLL リリースマトリクス](./release-matrix.md) を読みます。
4. menu、settings、plugin loading、ImageEditor、Socket、Scheduler、Solution workspace の問題は [UI ランタイムコンポーネント引き継ぎ](./ui-runtime-handoff.md) から確認します。
5. 具体的な control や window を探す場合は [UI コンポーネントカタログ](./control-catalog.md) を使います。
6. 最後に個別 DLL ページへ進みます。

## リリースと引き継ぎ入口

- [UI DLL コンポーネントハンドブック](./component-handbook.md): DLL ごとの責務と境界。
- [現在の UI DLL 文書カバレッジ](./current-ui-dll-coverage.md): 現在の UI プロジェクト、文書ページ、リリース証跡、保守ルール。
- [UI コンポーネントカタログ](./control-catalog.md): control、window、Provider、extension point からソースを探す。
- [UI ランタイムコンポーネント引き継ぎ](./ui-runtime-handoff.md): menu、settings、PropertyGrid、ImageEditor、Socket、Scheduler、Solution の discovery と debug。
- [UI DLL リリースプレイブック](./ui-dll-release-playbook.md): release scenario ごとの build/acceptance。
- [UI DLL リリースマトリクス](./release-matrix.md): version、target framework、dependency、resource、smoke test。
- [UI DLL リリース証跡と現地確認表](./dll-release-evidence.md): `.csproj`、package content、host output、consumer validation、rollback record。
- [UI DLL リリース手順](./publishing.md): 共通 build command、package check、post-release verification。

## モジュールマップ

### ベースレイヤー

- [ColorVision.Common](./ColorVision.Common.md): MVVM、共有インターフェイス、ステータス バーのメタデータ、および粗粒度のアクセス許可の基本。
- [ColorVision.Core](./ColorVision.Core.md): `HImage` および P/Invoke エクスポート サーフェスを担当する、ネイティブの画像/ビデオ機能ブリッジング レイヤ。

### 機能層

- [ColorVision.Database](./ColorVision.Database.md): データベース ブラウザ、プロバイダ登録、SQLite ログ、および一般的な DAO。
- [ColorVision.ImageEditor](./ColorVision.ImageEditor.md): `ImageView`、`DrawCanvas`、エディタ ツール、オープナー、および画像インタラクション メイン チェーン。
- [ColorVision.Scheduler](./ColorVision.Scheduler.md): Quartz スケジューラ、タスク回復、実行履歴および管理ウィンドウ。
- [ColorVision.SocketProtocol](./ColorVision.SocketProtocol.md): ローカル TCP サービス、リクエスト分散、メッセージ履歴および管理ウィンドウ。

### シェルとワークスペース

- [ColorVision.Solution](./ColorVision.Solution.md): ワークスペース、エディター、ターミナル、マルチイメージ表示、およびソリューション側のローカル RBAC。
- [ColorVision.UI](./ColorVision.UI.md): 構成、プラグイン、メニュー、プロパティ エディタ、多言語、ロギングなどの横断的な機能をカバーする UI インフラストラクチャのコレクション。
- [ColorVision.UI.Desktop](./ColorVision.UI.Desktop.md): 設定ウィンドウ、ウィザード、メニュー管理、構成管理、その他のデスクトップ補助ウィンドウ。

### テーマレイヤー

- [ColorVision.Themes](./ColorVision.Themes.md): テーマ リソース ディクショナリ、テーマの切り替え入り口、およびウィンドウの外観のサポート。

## 現在混乱を招くいくつかの境界線

- `ColorVision.UI` は単一のコントロール ライブラリではなく、UI インフラストラクチャの横断的なコレクションです。
- `ColorVision.Solution` は「単なるソリューション ファイル ツリー」ではなく、ワークスペース シェルとローカル RBAC サブモジュールもホストします。
- `ColorVision.UI.Desktop` は製品全体のメインの入り口ではなく、デスクトップの補助ウィンドウと管理ツールのコレクションのようなものです。
- `ColorVision.Core` は、高レベルのマネージド イメージ フレームワークではなく、ネイティブの相互運用レイヤーです。
- `ColorVision.ImageEditor` は純粋な表示コントロールではなく、オープナー、ツール、プリミティブ、オーバーレイ、およびランタイム サービスをまとめて配置します。

## 提案を読み続ける

### 設定、メニュー、権限、プラグインを確認したい

まずはご覧ください:

- [ColorVision.UI](./ColorVision.UI.md)
- [ColorVision.Solution](./ColorVision.Solution.md)
- [ColorVision.Common](./ColorVision.Common.md)

### 画像リンクを見たい

まずはご覧ください:

- [ColorVision.Core](./ColorVision.Core.md)
- [ColorVision.ImageEditor](./ColorVision.ImageEditor.md)
- [ColorVision.Themes](./ColorVision.Themes.md)

### デスクトップツールや運用保守補助機能を知りたい

まずはご覧ください:

- [ColorVision.Database](./ColorVision.Database.md)
- [ColorVision.Scheduler](./ColorVision.Scheduler.md)
- [ColorVision.SocketProtocol](./ColorVision.SocketProtocol.md)
- [ColorVision.UI.Desktop](./ColorVision.UI.Desktop.md)
