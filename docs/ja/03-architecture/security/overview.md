# セキュリティと権限の制御

この章では、現在のウェアハウスに実装されている権限とセッションの実装についてのみ説明します。私たちは、ネットワーク、データ、監査、認証のリンク全体をカバーする一連の一般的なセキュリティ ホワイト ペーパーとして ColorVision を維持することはなくなります。

## 現在存在する 2 レベルの権限境界

コードの観点から見ると、現在のセキュリティ関連機能は主に 2 つの層に分かれています。

- `UI/ColorVision.Common/Authorizations/` の下の粗粒度 `PermissionMode`
- `UI/ColorVision.Solution/Rbac/` 下のローカル RBAC サブシステム

これら 2 つのレベルは相互に排他的ではなく、共存します。

## 最初のレベル: グローバルな粗粒度の権限

`Authorization.Instance.PermissionMode` は、現在の多くのウィンドウと操作の最初の境界です。

提供されるレベルは次のとおりです。

- `SuperAdministrator`
- `Administrator`
- `PowerUser`
- `User`
- `Guest`

現在、多くの UI ポータルはこのレイヤーを直接利用して、「管理者のみがユーザー管理や権限管理のウィンドウを開くことができる」などの判断を行っています。

したがって、RBAC サービス層のみに注目すると、現在のシステムが達成しているきめ細かいアクセスの範囲を過大評価するのは簡単です。

## 第 2 層: ソリューション側のローカル RBAC

より詳細なユーザー、役割、権限、セッション、監査機能は現在 `UI/ColorVision.Solution/Rbac/` に集中しています。

このサブシステムの現在の機能は次のとおりです。

- ローカル SQLite データベースを使用する
- データベースはデフォルトで `%AppData%/ColorVision/Config/Rbac.db` にあります
- SqlSugar CodeFirst を使用してテーブル構造を初期化する
- ログイン、ユーザー管理、権限管理、セッションおよび監査サービスを提供します

これは、製品全体のすべてのセキュリティ機能への唯一の一般的な入り口というよりは、「ソリューション側のローカル アカウントと権限モジュール」に似ています。

## 現在の安全に関する章で最も注意すべき点は何ですか?

### ログインとセッション

現在、`AuthService` はユーザー名とパスワードのログインと、SessionToken に基づく自動ログイン回復を担当し、`SessionService` はセッションの作成、検証、取り消し、およびクリーンアップを担当します。

この部分は、現在のコードで最も明示的な認証チェーンです。

### 役割と権限

現在、`RbacManager` はロール、権限、ユーザー、およびロールと権限のマッピングを初期化し、`PermissionChecker` を通じて詳細な権限コード検証を実行します。

### 監査

現在、`AuditLogService` はすでに存在しますが、アプリケーション全体のすべての操作をカバーするグローバル監査プラットフォームではなく、RBAC 関連のアクションに関するローカル監査ログを記録します。

## コンテンツは現在証拠によって裏付けられていません

この章では、以下をコンピテンシーとして引き続き記述しないでください。

- 多要素認証
- グローバルネットワーク通信暗号化ポリシー
- 証明書検証システム
- IP ホワイトリスト
- ファイアウォールポリシー
- すべてのモジュールをカバーする統合された監査および傍受チェーン

これらの機能が将来実際に実装される場合は、事前にアーキテクチャ概要に書き込むのではなく、実際のコードに基づいて別のトピック ページを開く必要があります。

## 推奨される読む順序

次の行を読むことをお勧めします。

1. `UI/ColorVision.Common/Authorizations/PermissionMode.cs`
2. `UI/ColorVision.Common/Authorizations/AccessControl.cs`
3. `UI/ColorVision.Solution/Rbac/RbacManager.cs`
4. `UI/ColorVision.Solution/Rbac/Services/Auth/AuthService.cs`
5. `UI/ColorVision.Solution/Rbac/Services/SessionService.cs`
6. `UI/ColorVision.Solution/Rbac/Services/PermissionChecker.cs`
7. `UI/ColorVision.Solution/Rbac/UserManagerWindow.xaml.cs`
8. `UI/ColorVision.Solution/Rbac/PermissionManagerWindow.xaml.cs`

## 続きを読む

- [RBACモジュール](./rbac.md)
- [アーキテクチャ ランタイム](../overview/runtime.md)
- [コンポーネントの相互作用](../overview/component-interactions.md)

## 説明

- このページには、現在の実装における検証可能な権限とセッション境界のみが保持され、一般化されたセキュリティ機能のリストは保持されなくなります。