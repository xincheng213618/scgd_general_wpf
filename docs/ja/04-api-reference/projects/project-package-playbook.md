# プロジェクト実行と引き継ぎプレイブック

このページは、顧客プロジェクトの現場問題、プロトコル、結果出力、`.cvxp` 納入を扱う保守者向けです。個別プロジェクトページの代替ではなく、症状から調査入口を選ぶためのページです。リリース、現地置換、rollback では [プロジェクトパッケージリリース証跡とバージョン確認表](./project-release-evidence.md) も記入します。

## シナリオ入口

| 問題 | 先に確認するもの | 代表プロジェクト |
| --- | --- | --- |
| メニューまたはウィンドウが開かない | A | 全プロジェクト |
| 外部コマンドで検査が始まらない | B | ARVR, ARVRLite, ARVRPro, LUX, KB, BlackMura, Heyuan |
| Flow は始まるが項目やテンプレートが違う | C | ARVRPro, LUX, KB, Shiyuan |
| 結果はあるが PASS/FAIL や顧客フィールドが違う | D | ARVRPro, LUX, KB, BlackMura, Heyuan |
| CSV/XLSX/PDF/SQLite/MES が欠ける | E | ARVR 系, LUX, KB, BlackMura, Heyuan, Shiyuan |
| Serial、Modbus、MES、PG、画像切替が異常 | F | BlackMura, Heyuan, KB, ARVRPro |
| `.cvxp` を納入する | G | IntegrationDemo 以外 |
| 顧客が連携 Demo だけ必要 | H | IntegrationDemo |

## 共通実行モデル

```text
manifest / PluginConfig
  -> project window
  -> external command or manual start
  -> active ProcessGroup / fixed workflow
  -> FlowTemplate
  -> Engine Flow
  -> IProcess reads result and applies Recipe/Fix
  -> ObjectiveTestResult
  -> SQLite / CSV / XLSX / PDF / MES / Socket
```

Exporter や CSV だけを見ないでください。多くの問題は、コマンド不一致、active group、`FlowTemplate` 名、Recipe/Fix 読み込み、`IProcess.Execute()` 未実行で起きます。

## 調査チェック

| シナリオ | チェック項目 |
| --- | --- |
| A | `Plugins/<ProjectName>/`, `manifest.json`, `PluginConfig/`, `Menu*.cs`, ホストログ |
| B | Socket/Serial/Modbus サービス、ウィンドウ存在、SN、状態、ProcessGroup |
| C | `ProcessGroups.json`, `ProcessMeta.FlowTemplate`, `ProcessTypeFullName`, `SocketCode` |
| D | Flow 完了、`IProcess.Execute()`, Recipe/Fix, `ObjectiveTestResult` |
| E | Local result、Legacy switch、file output、Socket/MES、SQLite |
| F | 生コマンド、return code、timeout、DeviceId、register、切替期待応答 |
| G | manifest、README/CHANGELOG、config、外部 DLL、output path、rollback |
| H | sample JSON、online connect、partial/sticky packet、CSV export |

## 引き継ぎ記録

引き継ぎごとに、プロジェクト、顧客/現場、バージョン、ビルドコマンド、プロトコル、ProcessGroup、Recipe/Fix、出力、受入結果、rollback、既知制限を残します。

## 続けて読む

- [プロジェクト能力と引き継ぎマトリクス](./project-capability-matrix.md)
- [プロジェクトパッケージリリース証跡とバージョン確認表](./project-release-evidence.md)
- [プロジェクト引き継ぎマニュアル](./project-handoff.md)
