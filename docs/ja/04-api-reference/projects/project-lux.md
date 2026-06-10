# ProjectLUX

`Projects/ProjectLUX/` は光学自動化パッケージで、`ProjectLUX.dll` として読み込まれます。輝度、色、contrast、MTF、distortion、optic center、VID、luminous flux を扱います。

## 実行時 ID

| Field | Value |
| --- | --- |
| `Id` | `ProjectLUX` |
| `version` | `1.0` |
| `dllpath` | `ProjectLUX.dll` |
| `requires` | `1.3.15.10` |

## 業務範囲

LUX は ARVRPro と異なり text command を使います。

```text
T00XX,SN;
```

`XX` は active ProcessGroup の `ProcessMeta.SocketCode` に対応します。したがって引き継ぎでは FlowTemplate、active group、SocketCode、Recipe、Fix、return code を一緒に確認します。

## 主要コード

| ファイル/ディレクトリ | 役割 |
| --- | --- |
| `LUXWindow.xaml(.cs)` | メイン検査ウィンドウ |
| `Process/` | test framework と items |
| `Recipe/` | limit config |
| `Fix/` | correction factor |
| `Services/SocketControl.cs` | TCP text command |
| `ObjectiveTestResult.cs` | aggregated result |
| `ViewResultManager.cs` | SQLite result |

## 引き継ぎ注意

- Socket は text-based で、ARVRPro JSON ではありません。
- FlowTemplate を rename する時は `SocketCode` を確認します。
- `FixConfig` は最終値に影響するため、校正問題をすぐ algorithm 問題にしないでください。
- `%APPDATA%\ColorVision\Config\ProcessGroups.json` の保存を確認します。
