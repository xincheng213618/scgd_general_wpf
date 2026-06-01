# ユーザーガイド

この章は「まず使い方を学び、次に深く掘り下げる」という順序で構成されており、日常的な使用に関連するページを優先しています。

## 章の入り口

### インターフェースと基本的な対話

- [メインウィンドウナビゲーション](./interface/main-window.md)
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

1. まず、[メイン ウィンドウ ナビゲーション](./interface/main-window.md) を読んで、メイン インターフェイスのレイアウトを理解します。
2. [プロパティ エディタ](./interface/property-editor.md) と [イメージ エディタの概要](./image-editor/overview.md) を参照して、基本的な操作パスを確立します。
3. ハードウェアが関係する場合は、[デバイス サービスの概要](./devices/overview.md) および対応するデバイスの特別ページに移動します。
4. 自動化が必要な場合は、[ワークフロー概要](./workflow/README.md)を入力します。
5. 例外が発生した場合は、まず [FAQ](./troubleshooting/common-issues.md) を確認してください。

## 章の境界

- 部分的な実装と拡張メカニズムの内容を [開発者ガイド](../02-developer-guide/README.md) に移動しました。
- 部分的なクラス ライブラリ、インターフェイス、およびモジュール レベルの説明の内容は、[API リファレンス](../04-api-reference/README.md) に移動されました。
- システム設計全体を理解したい場合は、[アーキテクチャ設計](../03-architecture/README.md)に直接アクセスしてください。