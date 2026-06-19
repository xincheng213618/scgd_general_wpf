# ProjectARVR

`Projects/ProjectARVR/` は初期 AR/VR 光学検査パッケージで、実行時は `ProjectARVR.dll` として読み込まれます。固定 PG 切替、FlowEngine 実行、`ObjectiveTestResult`、CSV、Socket 応答を一つの流れにします。

## 実行時 ID

| Field | Value |
| --- | --- |
| `Id` | `ProjectARVR` |
| `version` | `1.0` |
| `dllpath` | `ProjectARVR.dll` |
| `requires` | `1.3.9.10` |

## 業務範囲

現在の自動化順序は固定で、実質的に `OpticCenter` で完了します。

```text
White2 -> White -> White1 -> Black -> Chessboard -> MTFH -> MTFV -> Distortion -> OpticCenter -> ProjectARVRResult
```

`Ghost`、`DotMatrix` などの enum はありますが、現在の `SwitchPGCompleted()` チェーンではテンプレート実行されません。

## 主要コード

| ファイル | 役割 |
| --- | --- |
| `ARVRWindow.xaml(.cs)` | メインウィンドウ、画像切替状態機械、Flow、結果、Socket |
| `ProjectARVRConfig.cs` | 設定とテンプレート編集 |
| `ObjectiveTestResult.cs` | 製品結果 DTO と CSV |
| `ARVRRecipeConfig.cs` | 各測定項目の制限 |
| `ObjectiveTestResultFix.cs` | 補正係数 |
| `ViewResultManager.cs` | 結果リスト、保存、CSV path |
| `Services/SocketControl.cs` | Socket event |

## 引き継ぎ注意

- `ProjectARVRInit` はウィンドウが既に開いていることを要求します。
- テンプレート照合は `White255`、`MTF_H`、`OpticCenter` などの keyword に依存します。
- 後続 enum を実装済み自動化として説明しないでください。
- 新規 AR/VR 納入では ProjectARVRPro または ProjectARVRLite を先に検討します。
