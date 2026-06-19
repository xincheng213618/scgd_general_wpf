# ProjectARVRPro

`Projects/ProjectARVRPro/` は現在の主要 AR/VR プロジェクトパッケージで、`ProjectARVRPro.dll` として読み込まれます。現代の AR/VR 顧客ワークフローを引き継ぐ時は、このフォルダを中心に読みます。

## 実行時 ID

| Field | Value |
| --- | --- |
| `Id` | `ProjectARVRPro` |
| `version` | `1.1.7.7` |
| `dllpath` | `ProjectARVRPro.dll` |
| `requires` | `1.3.15.15` |

## 業務範囲

ARVRPro は輝度、均一性、色、FOFO、Chessboard、MTF、Distortion、OpticCenter、OLED AOI を扱います。中心モデルは `ProcessGroup` と `ProcessMeta` です。

ARVRPro は JSON `EventName` dispatch を使い、`ProjectARVRInit` -> `SwitchPGCompleted` -> `ProjectARVRResult` の流れで画像切替と測定を進めます。各 step は `PictureSwitchConfig` を持てます。

## 主要コード

| ファイル/ディレクトリ | 役割 |
| --- | --- |
| `ARVRWindow.xaml(.cs)` | メイン検査ウィンドウ |
| `Process/` | 測定 step framework と実装 |
| `Recipe/` | 制限と補正設定 |
| `Services/SocketControl.cs` | TCP JSON event dispatch |
| `Services/RunAllSocket.cs` | RunAll |
| `Services/SwitchGroupSocket.cs` | ProcessGroup 切替 |
| `SocketRelay/` | AOI relay |
| `ObjectiveTestResult.cs` | 集約結果 |
| `ViewResultManager.cs` | 結果検索と保存 |

## 引き継ぎ注意

- `ProcessGroup` は製品/シナリオ単位のワークフローです。
- `ProcessMeta` は FlowTemplate、有効状態、画像切替、private config を持ちます。
- 顧客判定は generic Engine ではなくプロジェクト `IProcess` に置きます。
- `UseLegacyARVROutput` は CSV と Socket `Data` の両方に影響します。
- `SocketRelay/` は通常 `127.0.0.1:9200`。主 Socket 接続だけでは relay 動作は保証できません。
