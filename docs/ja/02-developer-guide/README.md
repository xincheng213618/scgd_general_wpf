#開発ガイド

この章では、二次開発、拡張ポイント、配信プロセスに焦点を当てます。クラス ライブラリの詳細とモジュール設計については、それぞれ API リファレンスとアーキテクチャ設計を参照してください。

## 一般的なシナリオはここから

### 拡張メカニズムを理解する

- [拡張性の概要](./core-concepts/extensibility.md)

### エンジンまたはテンプレート関連の機能を変更する

- [エンジン開発ガイド](./engine-development/README.md)
- [アーキテクチャ設計](../03-architecture/README.md)
- [エンジン コンポーネント API](../04-api-reference/engine-components/README.md)

### プラグインを開発する

- [プラグイン開発の概要](./plugin-development/README.md)
- [プラグイン開発を始める](./plugin-development/getting-started.md)
- [プラグインのライフサイクル](./plugin-development/lifecycle.md)

### ビルド、デプロイ、更新

- [展開の概要](./deployment/overview.md)
- [自動更新システム](./deployment/auto-update.md)
- [ビルドおよびリリース スクリプト](./scripts/README.md)

### バックエンドおよび補助システム

- [プラグイン マーケット バックエンド](./backend/README.md)
- [パフォーマンス最適化の概要](./performance/overview.md)
- [ソケット通信モジュールの最適化ルート](./パフォーマンス/socket-protocol-optimization-roadmap.md)

## 推奨される読み取りパス

1. まず、[アーキテクチャ設計](../03-architecture/README.md) を参照して、モジュールの境界を確認します。
2. [拡張性の概要](./core-concepts/extensibility.md) をもう一度見て、拡張ポイントとプラグインのエントリを確認します。
3. 対象のトピックを入力します: プラグイン、エンジン、デプロイメント、またはバックエンド。
4. クラスとインターフェイスの詳細が必要な場合は、[API リファレンス](../04-api-reference/README.md) にアクセスしてください。

## 章の境界

- この章は API マニュアルの代わりとなるものではなく、「コードの入力方法」のパスを提供することを優先します。
- Engine サブディレクトリ内の細分化されたトピックの一部はまだ統合中であるため、メンテナンスされていない小さなページがメイン ナビゲーションに配置されるのを避けるために、デフォルトのエントリが概要ページに変更されています。
- AI/エージェントに関連する実験資料はサブディレクトリに残りますが、デフォルトの読み取りパスではなくなりました。

## 補助入口

- [プロジェクト構造の概要](../05-resources/project- Structure/README.md)
- [オンライン ウェアハウス](https://github.com/xincheng213618/scgd_general_wpf)
- [問題追跡](https://github.com/xincheng213618/scgd_general_wpf/issues)