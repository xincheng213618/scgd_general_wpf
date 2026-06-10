# ユーザーガイド

この章は「まず使い方を学び、次に深く掘り下げる」という順序で構成されており、日常的な使用に関連するページを優先しています。

## 章の入り口

### 操作目標から探す

- [ユーザー操作ワークフローマトリクス](./operation-workflow-matrix.md)
- [現場操作受入チェックリスト](./field-operation-acceptance.md)

### インターフェースと基本的な対話

- [メインウィンドウナビゲーション](./interface/main-window.md)
- [UI コンポーネント利用手順](./interface/ui-component-handbook.md)
- [プロパティエディタ](./interface/property-editor.md)
- [ログビューア](./interface/log-viewer.md)
- [ターミナル](./interface/terminal.md)

### 画像エディター

- [イメージエディターの概要](./image-editor/overview.md)

### デバイス管理

- [デバイスサービスの概要](./devices/overview.md)
- [デバイスの追加と構成](./devices/configuration.md)
- [カメラサービス](./devices/camera.md)
- [カメラ管理](./devices/camera-management.md)
- [カメラパラメータ設定](./devices/camera-configuration.md)
- [キャリブレーションサービス](./devices/calibration.md)
- [モーターサービス](./devices/motor.md)
- [SMU サービス](./devices/smu.md)
- [プロセスデバイスサービス](./devices/flow-device.md)
- [ファイルサーバー](./devices/file-server.md)

### ワークフロー

- [ワークフロー概要](./workflow/README.md)
- [プロセス設計](./workflow/design.md)
- [プロセスの実行とデバッグ](./workflow/execution.md)

### データ管理

- [データ管理の概要](./data-management/README.md)
- [データベース操作](./data-management/database.md)
- [データのエクスポートとインポート](./data-management/export-import.md)

### トラブルシューティング

- [FAQ](./troubleshooting/common-issues.md)

## 推奨される読書ルート

1. どのページを見るべきか迷う場合は、[ユーザー操作ワークフローマトリクス](./operation-workflow-matrix.md) から始めます。
2. 現場納入または再テストでは、[現場操作受入チェックリスト](./field-operation-acceptance.md) に沿って確認します。
3. まず [メインウィンドウナビゲーション](./interface/main-window.md) でメイン画面の構成を理解します。
4. 次に [UI コンポーネント利用手順](./interface/ui-component-handbook.md) で、各ウィンドウ/部品の入口、成功基準、確認方向を把握します。
5. [プロパティエディタ](./interface/property-editor.md) と [イメージエディターの概要](./image-editor/overview.md) で基本操作を確認します。
6. ハードウェアが関係する場合は [デバイスサービスの概要](./devices/overview.md) と対応するデバイスページへ進みます。
7. 自動化が必要な場合は [ワークフロー概要](./workflow/README.md) へ進みます。
8. 例外が発生した場合は、まず [FAQ](./troubleshooting/common-issues.md) を確認してください。

## 章の境界

- 部分的な実装と拡張メカニズムの内容を [開発者ガイド](../02-developer-guide/README.md) に移動しました。
- 部分的なクラス ライブラリ、インターフェイス、およびモジュール レベルの説明の内容は、[API リファレンス](../04-api-reference/README.md) に移動されました。
- システム設計全体を理解したい場合は、[アーキテクチャ設計](../03-architecture/README.md)に直接アクセスしてください。
