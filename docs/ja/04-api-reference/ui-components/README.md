# UIコンポーネントの概要

この章では、現在のコード実装と一致する UI モジュールの紹介ページのみが維持され、旧バージョンの概要にあった「バージョン互換性マトリックス + サンプル コード + 拡張チュートリアル」という混合記述方法は維持されなくなりました。

## この章の読み方

この倉庫に初めて入る場合は、次の順序で認識を確立することをお勧めします。

1. まず [ColorVision.UI](./ColorVision.UI.md) を見て、構成、プラグイン、メニュー、プロパティ エディタ、ショートカット キーの横断的なインフラストラクチャを理解します。
2. [ColorVision.Solution](./ColorVision.Solution.md) と [ColorVision.UI.Desktop](./ColorVision.UI.Desktop.md) をもう一度見て、ワークスペース シェルとデスクトップ補助ウィンドウを理解します。
3. 画像関連の機能については、[ColorVision.Core](./ColorVision.Core.md) -> [ColorVision.ImageEditor](./ColorVision.ImageEditor.md) に従って検索します。
4. 独立したサブシステムをさらに詳しく調べる必要がある場合は、対応する単一のページに入ります。

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