# デバイスサービス概要

このページはデバイス章の入口です。どのデバイスページを見るべきか、通常どの順番で設定するか、問題が出たとき最初にどこを見るかを整理します。

## デバイスサービスとは

ColorVision では、デバイスは多くの場合「サービス」として管理されます。メインアプリはデバイスサービス一覧を持ち、ユーザーはデバイス画面で表示、設定、保存、操作を行います。

デバイス関連の実装は主に次の場所にあります。

- `Engine/ColorVision.Engine/Services/`
- `Engine/ColorVision.Engine/Services/Devices/`

現在のコードで確認できる代表的な分類は次の通りです。

- Camera
- Calibration
- Motor
- FileServer
- FlowDevice
- Sensor
- SMU
- Spectrum

## この章の読み方

### 共通入口

- [デバイスの追加と設定](./configuration.md)

### 個別デバイス

- [カメラサービス](./camera.md)
- [カメラ管理](./camera-management.md)
- [カメラパラメータ設定](./camera-configuration.md)
- [キャリブレーションサービス](./calibration.md)
- [モーターサービス](./motor.md)
- [SMU サービス](./smu.md)
- [Flow デバイスサービス](./flow-device.md)
- [ファイルサーバー](./file-server.md)

## よくある利用順序

1. まず [デバイスの追加と設定](./configuration.md) で、追加と保存の基本手順を確認します。
2. 次に個別デバイスページで、そのデバイスのパラメータと操作を確認します。
3. カメラを使う場合は、[カメラ管理](./camera-management.md) と [カメラパラメータ設定](./camera-configuration.md) も確認します。
4. デバイスを自動化 Flow に入れる場合は、[ワークフロー概要](../workflow/README.md) へ進みます。

## 利用時によく起きること

- デバイスサービスは実機に接続する場合も、通信やファイル操作のサービスである場合もあります。
- デバイス一覧の順序、Enabled 状態、設定内容は、後続のウィンドウや Flow での可視性に影響します。
- 一部のデバイスには、基本設定とは別に物理デバイス管理、キャリブレーション、パラメータ設定画面があります。

## デバイス引き継ぎで記録すること

現場で「設定済み」とだけ残しても、次の担当者は復旧できません。識別、接続、最小動作、Flow 参照を記録します。

| 記録項目 | 書く内容 | 目的 |
| --- | --- | --- |
| デバイス識別 | name、Code、device type、対応する実機またはサービス | Flow が誤ったデバイスを参照するのを防ぐ |
| 通信設定 | IP、port、serial、baud、MQTT topic、file path | 現場接続条件を再現する |
| 最小動作 | camera capture、motor home、SMU point measure、file list | 一覧にあるだけでなく動作することを示す |
| Flow 参照 | どの Flow、node、project window が使うか | 手動は動くが Flow で失敗する場合の起点 |
| ログ証跡 | 接続成功時刻、error、timeout、permission issue | リモート調査の入口にする |
| rollback | 前バージョンの config、driver、firmware、project package | アップグレード後の復旧に使う |

## 現場最小受入

| 手順 | 操作 | 合格基準 |
| --- | --- | --- |
| 1 | デバイス一覧を開いて refresh | 対象デバイスが見え、name と Code が記録と一致 |
| 2 | 詳細または専用画面を開く | key parameter と保存状態が確認できる |
| 3 | 安全な最小動作を一つ実行 | response、log、state change を説明できる |
| 4 | 依存する Flow または project window を開く | 同じデバイスを選択できる |
| 5 | 最小 Flow または模擬 Flow を実行 | デバイス node まで到達し、結果または失敗理由を追える |

手順 3 が失敗する場合はデバイス層から確認します。手順 3 が通って手順 5 が失敗する場合は、Flow 参照、template parameter、project mapping を優先します。

## デバイス問題の切り分け

| 現象 | 先に見る | 次に見る |
| --- | --- | --- |
| 一覧に出ない | 作成/保存済みか、type が正しいか | plugin/project package が入口を提供しているか |
| あるが開けない | hardware online、driver、port/IP、permission | log の connection error と timeout |
| 手動は成功、Flow は失敗 | Flow node が参照する device Code | node parameter、template version、project window selection |
| 結果はあるが画像/データが違う | デバイス parameter と capture condition | downstream template、export field、database record |
| アップグレード後だけ異常 | config が上書きされたか | old package/config と new DLL が混在していないか |

## トラブルシューティング

### デバイスが一覧に出ない

- デバイス設定画面で作成して保存したか確認します。
- 実機、driver、通信環境などの依存条件を確認します。

### デバイスはあるが動作しない

- まず個別デバイスページのパラメータを確認します。
- 次に log と connection status を確認します。
- Flow からの呼び出しだけ失敗する場合は、[Flow 実行とデバッグ](../workflow/execution.md) も確認します。

## 説明

- このページは入口と利用経路を整理するページであり、デバイスサービスのコード解析は扱いません。
- 実装詳細は `Engine/ColorVision.Engine/Services/` 配下の実コードを基準にします。
- 現場納入では [現場操作受入チェックリスト](../field-operation-acceptance.md) に全体結果も記録します。
