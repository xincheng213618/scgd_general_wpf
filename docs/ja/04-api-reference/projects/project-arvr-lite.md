# ProjectARVRLite

`Projects/ProjectARVRLite/` は軽量 AR/VR クイック検査パッケージで、`ProjectARVRLite.dll` として読み込まれます。設定可能な測定項目、前処理、簡易納入を重視します。

## 実行時 ID

| Field | Value |
| --- | --- |
| `Id` | `ProjectARVRLite` |
| `version` | `1.0` |
| `dllpath` | `ProjectARVRLite.dll` |
| `requires` | `1.3.15.6` |

## 業務範囲

`ProjectARVRLiteTestTypeConfig.json` で有効測項を決めます。現在の実装分岐：

```text
W51, White, W25, Chessboard, MTFHV, Distortion, Ghost, OpticCenter
```

`DotMatrix`、白画面欠陥、黒画面欠陥は設定値がありますが、自動化分岐は未完成です。

## 主要コード

| ファイル | 役割 |
| --- | --- |
| `ARVRWindow.xaml(.cs)` | メインウィンドウ、状態機械、前処理、結果 |
| `TestTypeConfig.cs` | 有効測項設定 |
| `ObjectiveTestResult.cs` | 製品結果と CSV |
| `ARVRRecipeConfig.cs` | 各測項の制限 |
| `Services/SocketControl.cs` | Socket event |

## 引き継ぎ注意

- `%AppData%\ColorVision\Config\ProjectARVRLiteTestTypeConfig.json` を先に確認します。
- 前処理失敗は Flow 開始前に停止します。
- CSV は `ViewResultManager.Config.IsSaveCsv` と日付/SN 設定に依存します。
- テンプレート名は `White51`、`White255_Ghost_Test`、`MTF_HV` などの keyword と一致させます。
