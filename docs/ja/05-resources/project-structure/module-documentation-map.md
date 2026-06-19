# モジュールとドキュメントの比較表

このドキュメントには、現在のウェアハウス構造と有効なドキュメント エントリのみが保持されます。これは、「コードがどこにあるか、最初にどのドキュメントを読み取るか」をすばやく特定するために使用されます。

## コード領域からドキュメントエントリへ

|コードエリア |見どころ |推奨ドキュメント エントリ |補助入口 |
| --- | --- | --- | --- |
| `ColorVision/` |メインプログラム入口、メインウィンドウ、アプリケーション起動 | [スタート ガイド](../../00-getting-started/README.md) | [メイン ウィンドウ ナビゲーション](../../01-user-guide/interface/main-window.md) |
| `UI/` | WPF UI フレームワーク、テーマ、エディタ | [UI コンポーネントの概要](../../04-api-reference/ui-components/README.md) | [ユーザーガイド](../../01-user-guide/README.md) |
| `UI/ColorVision.SocketProtocol/` | TCPサービス、JSON/Text配信、メッセージ履歴、管理画面 | [ColorVision.SocketProtocol](../../04-api-reference/ui-components/ColorVision.SocketProtocol.md) | [ソケット通信モジュールの最適化ルート](../../02-developer-guide/performance/socket-protocol-optimization-roadmap.md) |
| `Engine/ColorVision.Engine/Services/` |デバイスサービス、サービス連携 | [デバイスサービスの概要](../../01-user-guide/devices/overview.md) | [エンジン開発ガイド](../../02-developer-guide/engine-development/README.md) |
| `Engine/ColorVision.Engine/Templates/` |テンプレートシステム、パラメータ化されたアルゴリズム、結果処理 | [アルゴリズムの概要](../../04-api-reference/algorithms/README.md) | [現在のアルゴリズムテンプレートカバレッジ](../../04-api-reference/algorithms/current-algorithm-template-coverage.md)、[BuzProduct 製品業務パラメータテンプレート](../../04-api-reference/algorithms/templates/buz-product-template.md)、[Validate 判定ルールテンプレート](../../04-api-reference/algorithms/templates/validate-rules.md)、[Compliance 結果引き継ぎ](../../04-api-reference/algorithms/templates/compliance-results.md)、[DataLoad データロードテンプレート](../../04-api-reference/algorithms/templates/data-load-template.md)、[Matching テンプレートマッチング](../../04-api-reference/algorithms/templates/matching-template.md)、[SysDictionary システム辞書テンプレート](../../04-api-reference/algorithms/templates/sys-dictionary-template.md)、[FocusPoints フォーカスポイントテンプレート](../../04-api-reference/algorithms/templates/focus-points-template.md)、[ImageCropping 画像クロッピングテンプレート](../../04-api-reference/algorithms/templates/image-cropping-template.md)、[テンプレートメニュー入口](../../04-api-reference/algorithms/templates/template-menu-entries.md)、[テンプレート アーキテクチャ デザイン](../../03-architecture/components/templates/design.md) |
| `Engine/FlowEngineLib/` |プロセス ノード、実行モデル、ビジュアル プロセス | [FlowEngineLib アーキテクチャ](../../03-architecture/components/engine/flow-engine.md) | [FlowNode 開発](../../04-api-reference/extensions/flow-node.md) |
| `Engine/cvColorVision/` | OpenCV 統合、基盤となるビジュアル処理 | [エンジン コンポーネントの概要](../../04-api-reference/engine-components/README.md) | [cvColorVision](../../04-api-reference/engine-components/cvColorVision.md) |
| `Engine/ColorVision.ShellExtension/` | `.cvraw` / `.cvcie` ファイルの Explorer サムネイル拡張 | [ColorVision.ShellExtension](../../04-api-reference/engine-components/ColorVision.ShellExtension.md) | [エンジン コンポーネントの概要](../../04-api-reference/engine-components/README.md) |
| `Plugins/` | ランタイムプラグインと拡張機能 | [既存プラグイン機能](../../04-api-reference/plugins/README.md) | [現在のプラグイン文書カバレッジ](../../04-api-reference/plugins/current-plugin-coverage.md)、[プラグイン機能と引き継ぎマトリクス](../../04-api-reference/plugins/plugin-capability-matrix.md)、[プラグイン開発の概要](../../02-developer-guide/plugin-development/overview.md) |
| `Projects/` | 顧客プロジェクト、業務カスタマイズ、連携デモ | [プロジェクト説明](../../00-projects/README.md) | [プロジェクトパッケージ概要](../../04-api-reference/projects/README.md)、[現在のプロジェクト文書カバレッジ](../../04-api-reference/projects/current-project-coverage.md)、[プロジェクト能力と引き継ぎマトリクス](../../04-api-reference/projects/project-capability-matrix.md)、[プロジェクト実行と引き継ぎプレイブック](../../04-api-reference/projects/project-package-playbook.md) |
| `ColorVisionSetup/` |インストーラーとアップデートのプロセス | [展開の概要](../../02-developer-guide/deployment/overview.md) | [自動更新システム](../../02-developer-guide/deployment/auto-update.md) |
| `Web/Backend/` |プラグイン マーケット バックエンド | [プラグイン マーケット バックエンド](../../02-developer-guide/backend/README.md) | [開発ガイド](../../02-developer-guide/README.md) |
| `Scripts/` |スクリプトのビルド、パッケージ化、リリース | [スクリプトのビルドとリリース](../../02-developer-guide/scripts/README.md) | [展開の概要](../../02-developer-guide/deployment/overview.md) |
| `Test/` | xUnit、native helper、バックエンド、スクリプト検証 | [テストと検証の引き継ぎ](../../02-developer-guide/testing.md) | [開発ガイド](../../02-developer-guide/README.md) |
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

### 顧客プロジェクトを保守したい

1. [プロジェクト説明](../../00-projects/README.md)
2. [プロジェクトパッケージ概要](../../04-api-reference/projects/README.md)
3. [現在のプロジェクト文書カバレッジ](../../04-api-reference/projects/current-project-coverage.md)
4. [プロジェクト能力と引き継ぎマトリクス](../../04-api-reference/projects/project-capability-matrix.md)
5. [プロジェクト実行と引き継ぎプレイブック](../../04-api-reference/projects/project-package-playbook.md)

### テンプレートやプロセスを理解したい

1. [アルゴリズムの概要](../../04-api-reference/algorithms/README.md)
2. [現在のアルゴリズムテンプレートカバレッジ](../../04-api-reference/algorithms/current-algorithm-template-coverage.md)
3. [FlowEngineLib アーキテクチャ](../../03-architecture/components/engine/flow-engine.md)
4. [テンプレート アーキテクチャ設計](../../03-architecture/components/templates/design.md)
5. [テンプレート API リファレンス](../../04-api-reference/algorithms/templates/api-reference.md)
6. 具体的な引き継ぎページは [FindLightArea 発光領域テンプレート](../../04-api-reference/algorithms/templates/find-light-area.md)、[JND テンプレート](../../04-api-reference/algorithms/templates/jnd-template.md)、[LED 検出テンプレート](../../04-api-reference/algorithms/templates/led-detection.md)、[BuzProduct 製品業務パラメータテンプレート](../../04-api-reference/algorithms/templates/buz-product-template.md)、[Validate 判定ルールテンプレート](../../04-api-reference/algorithms/templates/validate-rules.md)、[Compliance 結果引き継ぎ](../../04-api-reference/algorithms/templates/compliance-results.md)、[DataLoad データロードテンプレート](../../04-api-reference/algorithms/templates/data-load-template.md)、[Matching テンプレートマッチング](../../04-api-reference/algorithms/templates/matching-template.md)、[SysDictionary システム辞書テンプレート](../../04-api-reference/algorithms/templates/sys-dictionary-template.md)、[FocusPoints フォーカスポイントテンプレート](../../04-api-reference/algorithms/templates/focus-points-template.md)、[ImageCropping 画像クロッピングテンプレート](../../04-api-reference/algorithms/templates/image-cropping-template.md)、[テンプレートメニュー入口](../../04-api-reference/algorithms/templates/template-menu-entries.md)

### UIを変更したい、またはプロパティを編集したい

1. [ユーザーガイド](../../01-user-guide/README.md)
2. [UIコンポーネント概要](../../04-api-reference/ui-components/README.md)
3. [プロパティエディタ](../../01-user-guide/interface/property-editor.md)

### ビルド、リリース、アップデートを確認したい

1. [展開の概要](../../02-developer-guide/deployment/overview.md)
2. [自動更新システム](../../02-developer-guide/deployment/auto-update.md)
3. [スクリプトのビルドとリリース](../../02-developer-guide/scripts/README.md)

### テストと受け入れコマンドを選びたい

1. [テストと検証の引き継ぎ](../../02-developer-guide/testing.md)
2. 変更モジュールに応じて `Test/ColorVision.UI.Tests/`、`Test/opencv_helper_test/`、バックエンドテスト、スクリプトテスト、または `npm run docs:build` を選びます

## 使用原則

- まず章のホームページから入り、次に特定のトピックのページにジャンプします。
- 歴史の下書き、孤立した文書、古いパスのページは、メインの入り口として機能しなくなりました。
- 正確に対応するトピック ページが見つからない場合は、古いディレクトリ名に依存し続けるのではなく、最初に概要ページに戻ってください。
