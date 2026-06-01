#RBACモジュール

このページでは、現在のウェアハウスに実装されている RBAC サブシステムについて説明します。完全なエンタープライズ セキュリティ プラットフォームにそれを書き込んだドラフト ドキュメントは維持されなくなります。

## モジュールの場所

現在の RBAC 実装は、`Engine/` ではなく、`UI/ColorVision.Solution/Rbac/` に重点を置いています。

これは、現在の権限システムが、エンジン層の統合セキュリティ カーネルではなく、デスクトップ ソリューション側のローカル ユーザーおよび権限モジュールに近いことを示しているため、重要です。

## 初期化中に何が起こるか

`RbacManager` は、このサブシステムの一般的な入り口です。現在の初期化チェーンはおおよそ次のとおりです。

1. `%AppData%/ColorVision/Config/` ディレクトリを作成します。
2. ローカル SQLite データベース `Rbac.db` を開くか作成します。
3. SqlSugar CodeFirst を使用してエンティティ テーブルを初期化します。
4. `AuthService`、`UserService`、`RoleService`、`PermissionService`、`AuditLogService`、`SessionService`、`PermissionChecker`、`TenantService` を初期化します。
5. デフォルトの管理者ロールと管理者ユーザーを作成します。
6. プリセット権限を書き込み、すべての権限を `admin` ロールに割り当てます。
7. ログイン キャッシュがある場合は、現在のユーザーの `PermissionMode` を同期してグローバル認証状態に戻します。

このチェーンは、RBAC の現在の起動がローカルでブートストラップされており、外部認証サーバーに依存していないことを示しています。

## 現在存在するコアエンティティ

`Entity/` ディレクトリの最も重要なテーブル モデルには、現在次のものが含まれています。

- `UserEntity` は `sys_user` に対応します
- `UserDetailEntity` は `sys_user_detail` に対応します
- `RoleEntity` は `sys_role` に対応します
- `PermissionEntity` は `sys_permission` に対応します
- `RolePermissionEntity` は `sys_role_permission` に対応します
- `SessionEntity` は `sys_session` に対応します
- `AuditLogEntity` は `sys_audit_log` に対応します

`TenantEntity` と `UserTenantEntity` もあり、この実装がテナント ディメンションを予約していることを示していますが、これは現在のデスクトップ権限エントリ ページの主な読み取り対象ではありません。

## これらのエンティティは現在何を担当していますか?

### ユーザーとユーザーの詳細

`UserEntity` ユーザー名、パスワード ハッシュ、有効ステータス、および論理的な削除ステータスを保存します。

`UserDetailEntity` 追加のデポジット:

- `PermissionMode`
- メールアドレス、電話番号、住所、会社、部署、役職
- ユーザーのアバターとメモ

ここで特別な注意を払う必要があります。現在のグローバルで粗いアクセス許可レベルは、ロール テーブルからすぐに導出されるのではなく、`UserDetailEntity.PermissionMode` に直接保存され、ログイン後に `Authorization.Instance.PermissionMode` に同期されます。

### 役割と権限

`RoleEntity` はロールの基本情報を管理し、`PermissionEntity` は権限コードを管理し、`RolePermissionEntity` はロールと権限の関連付けを管理します。

現在の権限サービスには、事前に設定された一連の権限コードがすでに存在します。次に例を示します。

- `user.create`
- `user.edit`
- `role.assign_permissions`
- `permission.manage`
- `audit.view`

これは、現在のきめ細かい権限が単なる「管理者/ゲスト」ではなく、アクションコーディングによって制御され始めていることを示しています。

### セッション

`SessionEntity` 保存:

- `SessionToken`
・ユーザーID
- デバイス情報とIP
- 作成時刻、有効期限、最終アクティブ時刻
- 取り消されているかどうか

`SessionService` は、64 バイトのランダム トークンの生成、セッションの検証、アクティブ時間の更新、セッションの取り消し、および期限切れのセッションのクリーンアップを担当します。

### 監査

`AuditLogEntity` 現在の記録:

- ユーザーID/ユーザー名
- アクションコード
- 詳しい説明
- 時間
-IP

`AuditLogService` は現在、すべてのビジネス モジュールをカバーする統合監査バスではなく、RBAC 側のローカル監査レコードを提供します。

## 現在のログイン チェーンを通過する方法

現在の認証チェーンは大まかに次のとおりです。

1. ユーザーは、`LoginWindow` にユーザー名とパスワードを入力します。
2. `AuthService.LoginAndGetDetailAsync(...)` 有効化されているが論理的に削除されていないユーザーを照会します。
3. パスワードは `PasswordHasher` 検証に合格し、古い形式のパスワードはログイン中にアップグレードされます。
4. システムは `UserDetailEntity` が存在することを確認し、役割リストをロードします。
5. ログイン結果を`LoginResultDto`に書き込みます。
6. `SessionService` はさらに、SessionToken を作成および維持できます。
7. ログインに成功したら、`UserDetailEntity.PermissionMode` を `Authorization.Instance.PermissionMode` に同期します。

したがって、現在のログイン結果は、RBAC サブシステムの内部状態に影響を与えるだけでなく、グローバル UI の粗粒度の許可判定にも直接影響します。

## 現在の権限を確認する方法

現在、権限チェックには 2 つのレベルがあります。

### 粒度の粗いエントリー判断

多くのウィンドウでは、まず次のことを直接決定します。

- `Authorization.Instance.PermissionMode > PermissionMode.Administrator`

たとえば、ユーザー管理ウィンドウと権限管理ウィンドウは、このステップを最初にブロックします。

### きめ細かいパーミッションコードの判定

より詳細な権限の検証は `PermissionChecker` によって実行されます。それは次のことを行います:

- ユーザーに関連付けられたロール ID を照会します。
- 表を組み合わせて、対応する許可コードを見つけます。
- 有効期限付きキャッシュと LRU エビクションを使用して結果を保存

そのため、現状のシステムは「RBACのみ」「PermissionModeのみ」ではなく、シックレイヤとシンレイヤが混在しています。

## 現在表示されている管理インターフェイス

モジュール ディレクトリから判断すると、RBAC には現在、明確なデスクトップ ウィンドウのセットがあります。

- `LoginWindow`
- `RegisterWindow`
- `ChangePasswordWindow`
- `UserManagerWindow`
- `PermissionManagerWindow`
- `RbacManagerWindow`

その中には:

- `UserManagerWindow` は、ユーザー リスト、ロールの表示、開始/停止、削除、パスワードのリセットなどの管理アクションを担当します。
- `PermissionManagerWindow` は、ロールごとに権限を割り当て、保存後に権限キャッシュを無効にする責任があります。

## 現在の設計で最も注意が必要な境界

### これはローカル権限システムであり、統合されたリモート ID プラットフォームではありません

現在の実装は、外部の認証局ではなく、ネイティブ SQLite とローカル ウィンドウに依存しています。

### 粗粒度 `PermissionMode` は完全には置き換えられていません

多くのキー エントリは、RBAC 管理ロジックに入る前に `PermissionMode` を参照します。

### 詳細な権限アクセスはローカルです

現在確認できるきめ細かい権限機能は、主に RBAC 独自の管理ウィンドウとサービス層に集中しています。製品全体が許可コード制御に完全にアクセスできるとはまだ言えません。

## このページはそれ以上何もしません

このページには、現在の実装と矛盾するコンテンツは維持されなくなります。

- 架空の汎用 `AuthService`/`AuditService` プラットフォーム層図
- すべてのビジネス モジュールが RBAC によって均一にインターセプトされると仮定します。
- 多要素認証、ネットワーク証明書、IP ホワイトリスト、およびまだ実装されていないその他のセキュリティ機能

将来的にシステム全体のセキュリティ アーキテクチャを拡張する場合は、実際のアクセス ポイントに基づいて別の特別なページを作成する必要があります。

## 推奨される読む順序

1. `UI/ColorVision.Solution/Rbac/RbacManager.cs`
2. `UI/ColorVision.Solution/Rbac/Entity/`
3. `UI/ColorVision.Solution/Rbac/Services/Auth/AuthService.cs`
4. `UI/ColorVision.Solution/Rbac/Services/SessionService.cs`
5. `UI/ColorVision.Solution/Rbac/Services/PermissionChecker.cs`
6. `UI/ColorVision.Solution/Rbac/UserManagerWindow.xaml.cs`
7. `UI/ColorVision.Solution/Rbac/PermissionManagerWindow.xaml.cs`

## 続きを読む

- [セキュリティとアクセス許可の制御](./overview.md)
- [アーキテクチャ ランタイム](../overview/runtime.md)
- [コンポーネントの相互作用](../overview/component-interactions.md)