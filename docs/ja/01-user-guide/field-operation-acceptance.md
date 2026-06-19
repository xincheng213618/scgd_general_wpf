# 現場操作受入チェックリスト

初回納入、アップグレード、現場再テスト、オペレーター教育で ColorVision が実際に使えることを確認するためのチェックリストです。UI、デバイス、Flow、データ、外部システム、プラグイン、プロジェクトパッケージ、rollback 証跡を一つにまとめます。

迷う場合は [ユーザー操作ワークフローマトリクス](./operation-workflow-matrix.md) から始めます。失敗した項目はリンク先ページで詳しく確認します。

## 受入表

| 項目 | 最小操作 | 合格基準 | 失敗時 |
| --- | --- | --- | --- |
| Host 起動 | ColorVision を起動し main window を開く | main window、menu、status bar、log entry が見える | [メインウィンドウ](./interface/main-window.md)、[ログビューア](./interface/log-viewer.md) |
| UI 入口 | settings、log、DB、Socket、scheduler、marketplace を開く | 各 window が起動エラーなしで開く | [UI コンポーネント利用手順](./interface/ui-component-handbook.md) |
| 設定保存 | 安全な config を一つ変更し restart | 値が残り service state が正しい | [プロパティエディタ](./interface/property-editor.md) |
| デバイス | camera/motor/SMU/file service を確認 | device があり status refresh、最小動作成功 | [デバイスサービス概要](./devices/overview.md) |
| カメラ | 画像取得または live preview | image が生成され開ける | [カメラサービス](./devices/camera.md)、[画像エディター](./image-editor/overview.md) |
| Flow design | 現場 Flow template を開く | start node と key parameter が正しい | [Flow 設計](./workflow/design.md) |
| Flow run | 最小 Flow または project flow を実行 | 完了、または first failed node が明確 | [Flow 実行とデバッグ](./workflow/execution.md) |
| Image/overlay | result image と ROI/POI/overlay を確認 | image、layer、coordinate が合う | [画像エディター](./image-editor/overview.md) |
| DB write | SN/time/batch で result を検索 | SQLite/MySQL に record がある | [DB 操作](./data-management/database.md) |
| Export | CSV/Excel/PDF/image/project result を出力 | file があり顧客形式に合う | [データのエクスポートとインポート](./data-management/export-import.md) |
| Socket/MES/Modbus | 現場最小 command を送る | 外部が trigger し status/data を受信 | [SocketProtocol](../04-api-reference/ui-components/ColorVision.SocketProtocol.md) |
| Plugin | 現場 plugin を開き最小機能を実行 | menu、window、device connection、result/export が動く | [既存プラグイン能力](../04-api-reference/plugins/README.md) |
| Project package | project を開き SN で最小 Flow | customer result と response が project page と一致 | [プロジェクト説明](../00-projects/README.md) |
| Rollback | previous package/config/database backup を確認 | 前の動作状態へ戻せる | plugin/project release evidence |

## Device / Flow / Data

| 確認 | 合格基準 |
| --- | --- |
| Device resource | key device が作成され、name/code が現場実機を区別できる |
| Communication | IP、port、serial、baud、device id、file path が現場と一致 |
| Minimal action | camera capture、motor move/home、SMU read、file download/upload ができる |
| Flow reference | Flow node または project window が正しい device を選べる |
| Failure location | Flow 失敗時に first failed node と log を特定できる |
| Result review | result list、image、DB、export file が同一 run を指す |

Export が空の場合、export button を繰り返す前に source data が DB にあるか、表示中 batch と export target が同じかを確認します。

## External System

| 種別 | 最小証跡 |
| --- | --- |
| JSON Socket | `EventName`、SN、request JSON、response JSON、project window state |
| Text Socket | raw command 例 `T00XX,SN;`、return code、data |
| MES/serial | STX/ETX raw message、device id、return code、timeout |
| Modbus | IP、port、register、trigger value、completion write-back |
| File server | request path、file list、download/upload path |

## Handoff Record

```text
site/customer:
host version:
project package:
plugin package:
config folder:
device smoke result:
workflow smoke result:
image/overlay result:
database query result:
export file sample:
external protocol sample:
known failures:
rollback package/config:
operator trained:
owner/date:
```

## 続けて読む

- [ユーザー操作ワークフローマトリクス](./operation-workflow-matrix.md)
- [UI コンポーネント利用手順](./interface/ui-component-handbook.md)
- [デバイスサービス概要](./devices/overview.md)
- [Flow 実行とデバッグ](./workflow/execution.md)
- [データ管理](./data-management/README.md)
- [プロジェクト説明](../00-projects/README.md)
- [既存プラグイン能力](../04-api-reference/plugins/README.md)

