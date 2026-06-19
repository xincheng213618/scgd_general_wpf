# ProjectARVRPro.IntegrationDemo

`Projects/ProjectARVRPro.IntegrationDemo/` は ARVRPro TCP/JSON プロトコルを顧客、MES、PLC、上位機が検証するための最小サンプルです。ColorVision プラグインではなく、内部アルゴリズム DLL に依存しない設計です。

## 位置づけ

| 項目 | 値 |
| --- | --- |
| Target framework | .NET Framework 4.8 |
| 形態 | WPF demo + CLI arguments |
| ColorVision dependency | なし |
| 目的 | TCP 接続、command、result parse、CSV export の例 |

## 能力

- ARVRPro TCP port、通常 `6666` に接続する。
- `ProjectARVRInit`、`SwitchPGCompleted`、`RunAll`、`AOITestSwitchImageComplete` を送信する。
- sample JSON または現場保存の `ProjectARVRResult` を読み込む。
- `ObjectiveTestResult` と flat item table を表示する。
- raw JSON 保存と CSV export を行う。
- partial/sticky packet の読み取りを示す。

## 引き継ぎ注意

- 顧客側サンプルとして維持し、内部業務ロジックを入れません。
- 公開フィールド変更時は `Contracts/`、sample JSON、README、このページを同時に更新します。
