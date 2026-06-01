# モジュールとドキュメントの比較表

このドキュメントには、現在のウェアハウス構造と有効なドキュメント エントリのみが保持されます。これは、「コードがどこにあるか、最初にどのドキュメントを読み取るか」をすばやく特定するために使用されます。

## コード領域からドキュメントエントリへ

|コードエリア |見どころ |推奨ドキュメント エントリ |補助入口 |
| --- | --- | --- | --- |
| `ColorVision/` |メインプログラム入口、メインウィンドウ、アプリケーション起動 | [スタート ガイド](../../00-getting-started/README.md) | [メイン ウィンドウ ナビゲーション](../../01-user-guide/interface/main-window.md) |
| `UI/` | WPF UI フレームワーク、テーマ、エディタ | [UI コンポーネントの概要](../../04-api-reference/ui-components/README.md) | [ユーザーガイド](../../01-user-guide/README.md) |
| `UI/ColorVision.SocketProtocol/` | TCPサービス、JSON/Text配信、メッセージ履歴、管理画面 | [ColorVision.SocketProtocol](../../04-api-reference/ui-components/ColorVision.SocketProtocol.md) | [ソケット通信モジュールの最適化ルート](../../02-developer-guide/performance/socket-protocol-optimization-roadmap.md) |
| `Engine/ColorVision.Engine/Services/` |デバイスサービス、サービス連携 | [デバイスサービスの概要](../../01-user-guide/devices/overview.md) | [エンジン開発ガイド](../../02-developer-guide/engine-development/README.md) |
| `Engine/ColorVision.Engine/Templates/` |テンプレートシステム、パラメータ化されたアルゴリズム、結果処理 | [アルゴリズムの概要](../../04-api-reference/algorithms/README.md) | [テンプレート アーキテクチャ デザイン](../../03-architecture/components/templates/design.md) |
| `Engine/FlowEngineLib/` |プロセス ノード、実行モデル、ビジュアル プロセス | [FlowEngineLib アーキテクチャ](../../03-architecture/components/engine/flow-engine.md) | [FlowNode 開発](../../04-api-reference/extensions/flow-node.md) |
| `Engine/cvColorVision/` | OpenCV 統合、基盤となるビジュアル処理 | [エンジン コンポーネントの概要](../../04-api-reference/engine-components/README.md) | [cvColorVision](../../04-api-reference/engine-components/cvColorVision.md) |
| `Plugins/` |ランタイム プラグインと拡張機能 | [プラグイン開発の概要](../../02-developer-guide/plugin-development/overview.md) | [標準プラグインのトピック](../../04-api-reference/plugins/standard-plugins/pattern.md) |
| `Projects/` |顧客プロジェクト、カスタム ビジネス アセンブリ | [コンポーネントの相互作用](../../03-architecture/overview/component-interactions.md) | [プロジェクト構造の概要](./README.md) |
| `ColorVisionSetup/` |インストーラーとアップデートのプロセス | [展開の概要](../../02-developer-guide/deployment/overview.md) | [自動更新システム](../../02-developer-guide/deployment/auto-update.md) |
| `Backend/marketplace/` |プラグイン マーケット バックエンド | [プラグイン マーケット バックエンド](../../02-developer-guide/backend/README.md) | [開発ガイド](../../02-developer-guide/README.md) |
| `Scripts/` |スクリプトのビルド、パッケージ化、リリース | [スクリプトのビルドとリリース](../../02-developer-guide/scripts/README.md) | [展開の概要](../../02-developer-guide/deployment/overview.md) |
| `docs/` |現在のドキュメント サイトのソース コード | [付録とリソース](../README.md) |現在のドキュメント |

## タスクから検索

### デバイスサービスを追加したい

1. まず、[デバイス サービスの概要](../../01-user-guide/devices/overview.md) を確認します。
2. [エンジン開発ガイド](../../02-developer-guide/engine-development/README.md) をもう一度読んでください。
3. 最後に、[エンジン コンポーネントの概要](../../04-api-reference/engine-components/README.md) と入力して、特定のモジュール ページを見つけます。

### プラグインを開発したい

1. [拡張性の概要](../../02-developer-guide/core-concepts/extensibility.md)
2. [プラグイン開発の概要](../../02-developer-guide/plugin-development/overview.md)
3. [プラグイン開発のスタート](../../02-developer-guide/plugin-development/getting-started.md)

### テンプレートやプロセスを理解したい

1. [アルゴリズムの概要](../../04-api-reference/algorithms/README.md)
2. [FlowEngineLib アーキテクチャ](../../03-architecture/components/engine/flow-engine.md)
3. [テンプレート アーキテクチャ設計](../../03-architecture/components/templates/design.md)
4. [テンプレート API リファレンス](../../04-api-reference/algorithms/templates/api-reference.md)

### UIを変更したい、またはプロパティを編集したい

1. [ユーザーガイド](../../01-user-guide/README.md)
2. [UIコンポーネント概要](../../04-api-reference/ui-components/README.md)
3. [プロパティエディタ](../../01-user-guide/interface/property-editor.md)

### ビルド、リリース、アップデートを確認したい

1. [展開の概要](../../02-developer-guide/deployment/overview.md)
2. [自動更新システム](../../02-developer-guide/deployment/auto-update.md)
3. [スクリプトのビルドとリリース](../../02-developer-guide/scripts/README.md)

## 使用原則

- まず章のホームページから入り、次に特定のトピックのページにジャンプします。
- 歴史の下書き、孤立した文書、古いパスのページは、メインの入り口として機能しなくなりました。
- 正確に対応するトピック ページが見つからない場合は、古いディレクトリ名に依存し続けるのではなく、最初に概要ページに戻ってください。