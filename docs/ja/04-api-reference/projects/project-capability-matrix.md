# プロジェクト能力と引き継ぎマトリクス

このページは `Projects/` 配下の顧客プロジェクトを横断比較します。現場課題、外部トリガー、主要コード、納入時の確認点を一枚で把握するための入口です。リリース、現地置換、rollback の証跡は [プロジェクトパッケージリリース証跡とバージョン確認表](./project-release-evidence.md) に記録します。

## プロジェクトサマリ

| プロジェクト | 業務役割 | 外部トリガー | 主な出力 | 最初に読むコード |
| --- | --- | --- | --- | --- |
| `ProjectARVR` | 初期 AR/VR 固定画像切替検査 | JSON Socket: `ProjectARVRInit`, `SwitchPGCompleted` | `ObjectiveTestResult`, CSV, Socket | `ARVRWindow.xaml.cs`, `Services/SocketControl.cs` |
| `ProjectARVRLite` | 軽量 AR/VR クイック検査 | JSON Socket、自動ウィンドウ起動 | CSV, Socket | `ARVRWindow.xaml.cs`, `TestTypeConfig.cs` |
| `ProjectARVRPro` | 主要 AR/VR ProcessGroup パッケージ | JSON Socket, `RunAll`, `SwitchGroup`, Serial 切替 | SQLite, CSV, Legacy CSV, XLSX, Socket | `Process/`, `Recipe/`, `Services/` |
| `ProjectARVRPro.IntegrationDemo` | 顧客側 TCP/JSON サンプル | `6666` へ接続して JSON 送信 | JSON, 結果表, CSV | `Program.cs`, `MainWindow.xaml.cs`, `Contracts/` |
| `ProjectBlackMura` | パネル Black Mura 検査 | PG/MES Serial: `CON`, `CCPI`, `CSN`, `CGI` | Excel, POI, Mura | `MainWindow.xaml.cs`, `HYMesManager.cs` |
| `ProjectHeyuan` | Heyuan 4 点 WBRO | STX/ETX Serial | WBRO CSV, MES | `ProjectHeyuanWindow.xaml.cs`, `TempResult.cs` |
| `ProjectKB` | キーボードバックライト | Modbus, MES DLL, optional Socket | SQLite, CSV, summary, MES | `ProjectKBWindow.xaml.cs`, `Modbus/`, `MesDll.cs` |
| `ProjectLUX` | LUX 光学自動化 | Text Socket: `T00XX,SN;` | SQLite, CSV, PDF | `LUXWindow.xaml.cs`, `Process/`, `Recipe/`, `Fix/` |
| `ProjectShiyuan` | JND/POI 出力 | 現在は主に手動 Flow | JND CSV, POI CSV, pseudo-color | `ShiyuanProjectWindow.xaml.cs` |

## プロトコル別

| 種別 | プロジェクト | 引き継ぎ重点 |
| --- | --- | --- |
| JSON Socket | ARVR, ARVRLite, ARVRPro | `EventName`, SN, 画像切替完了、最終結果 |
| Text Socket | LUX | `T00XX` と `ProcessMeta.SocketCode` の対応 |
| Serial/MES | BlackMura, Heyuan | STX/ETX, DeviceId, return code, NG 承認 |
| PLC/Modbus | KB | register、値 `1`、完了書き戻し `0`、SN 来源 |
| Customer demo | IntegrationDemo | 公開 JSON contract のみ。内部業務 DLL を入れない |
| Manual/offline | Shiyuan | `DataPath`、テンプレート選択、固定画像パス |

## 最小スモーク受入

| プロジェクト | 確認点 |
| --- | --- |
| ARVR | `ProjectARVRInit` から `OpticCenter` まで進み、CSV と Socket 結果が出る |
| ARVRLite | 有効測項、前処理、CSV、Socket 結果を確認 |
| ARVRPro | ProcessGroup 切替、`RunAll`、画像切替、Recipe、CSV/Legacy/Socket |
| IntegrationDemo | サンプル JSON、オンライン接続、半包/粘包処理 |
| BlackMura | 5 色 PG 切替、`<SN>.xlsx`、POI overlay |
| Heyuan | Serial 接続、4 POI、WBRO CSV、MES |
| KB | Modbus `1` で開始、完了 `0`、CSV/summary/MES 一致 |
| LUX | `T00XX,SN;` が `SocketCode` と一致し CSV/SQLite を出す |
| Shiyuan | 手動 Flow で JND/POI CSV と pseudo-color を出す |
