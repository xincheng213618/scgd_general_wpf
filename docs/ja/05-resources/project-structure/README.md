# プロジェクト構造の概要

このドキュメントは、現在の倉庫のメイン ディレクトリの役割分担を簡単に説明し、各ディレクトリに最適なドキュメント エントリを提供するために使用されます。

## ホームディレクトリのパーティション

|目次 |機能 |最初に読むことをお勧めします |
| --- | --- | --- |
| `ColorVision/` |メインの WPF アプリケーションの入り口とメイン ウィンドウ | [スタート ガイド](../../00-getting-started/README.md) / [メイン ウィンドウのナビゲーション](../../01-user-guide/interface/main-window.md) |
| `UI/` | UI フレームワーク、テーマ、プロパティ エディター、イメージ エディター | [UI コンポーネントの概要](../../04-api-reference/ui-components/README.md) |
| `UI/ColorVision.SocketProtocol/` |ローカル TCP サービス、メッセージ履歴、管理ウィンドウ | [ColorVision.SocketProtocol](../../04-api-reference/ui-components/ColorVision.SocketProtocol.md) / [ソケット通信最適化ロードマップ](../../02-developer-guide/performance/socket-protocol-optimization-roadmap.md) |
| `Engine/` |コアエンジン、デバイスサービス、テンプレートシステム、プロセス実行 | [エンジン開発ガイド](../../02-developer-guide/engine-development/README.md) / [エンジン コンポーネントの概要](../../04-api-reference/engine-components/README.md) |
| `Plugins/` |ランタイム プラグインと拡張機能 | [プラグイン開発の概要](../../02-developer-guide/plugin-development/overview.md) |
| `Projects/` |顧客プロジェクトパッケージとビジネスカスタマイズの実装 | [コンポーネントの相互作用](../../03-architecture/overview/component-interactions.md) |
| `Backend/marketplace/` |プラグインマーケットバックエンドサービス | [プラグイン マーケット バックエンド](../../02-developer-guide/backend/README.md) |
| `Scripts/` |スクリプトのビルド、パッケージ化、リリース | [スクリプトのビルドとリリース](../../02-developer-guide/scripts/README.md) |
| `ColorVisionSetup/` |インストーラーと更新プログラム | [自動更新システム](../../02-developer-guide/deployment/auto-update.md) |
| `Test/` |テストプロジェクト | [開発者ガイド](../../02-developer-guide/README.md) |
| `docs/` | VitePress ドキュメントのソース コード |現在のドキュメント / [モジュールとドキュメントの比較表](./module-documentation-map.md) |

## 役割ごとに読み取る

### 新しいユーザーまたは実装クラスメイト

1. [はじめに](../../00-getting-started/README.md)
2. [ユーザーガイド](../../01-user-guide/README.md)
3. [FAQ](../../01-user-guide/troubleshooting/common-issues.md)

### エンジンまたはアルゴリズムの開発

1. [アーキテクチャ設計](../../03-architecture/README.md)
2. [エンジン開発ガイド](../../02-developer-guide/engine-development/README.md)
3. [アルゴリズムの概要](../../04-api-reference/algorithms/README.md)

### プラグイン開発

1. [拡張性の概要](../../02-developer-guide/core-concepts/extensibility.md)
2. [プラグイン開発の概要](../../02-developer-guide/plugin-development/overview.md)
3. [標準プラグインのトピック](../../04-api-reference/plugins/standard-plugins/pattern.md)

### 文書の保守

1. [付録とリソース](../README.md)
2. [モジュールとドキュメントの比較表](./module-documentation-map.md)

## 説明

- ここで提供されるものは「どこから始めるべきか」のエントリであり、詳細な API やトピック ページに代わるものではありません。
- ディレクトリに独立した文書が存在しない場合は、新しい個別のページ索引を展開し続けるのではなく、隣接する章の概要ページから入ることが優先されます。