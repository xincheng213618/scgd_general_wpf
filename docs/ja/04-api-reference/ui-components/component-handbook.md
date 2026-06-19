# UI DLL コンポーネントハンドブック

このページは `UI/` 配下のリリース単位ごとに DLL の責務を説明します。目的は、引き継ぎ担当者が「何を担当するか、誰が参照するか、入口はどこか、リリース時に何を確認するか」を把握できるようにすることです。

具体的なコントロール、ウィンドウ、拡張ポイントを探す場合は [UI コンポーネントカタログ](./control-catalog.md) を参照してください。メニュー、設定、ImageEditor ツール、Socket handler、Solution editor が見つからない問題は [UI ランタイムコンポーネント引き継ぎ](./ui-runtime-handoff.md) を参照します。DLL または NuGet パッケージを出す場合は [UI DLL リリースマトリクス](./release-matrix.md) を使います。

## レイヤー

| レイヤー | DLL | 役割 |
| --- | --- | --- |
| 基礎契約 | `ColorVision.Common.dll` | MVVM、共有 interface、status bar metadata、initializer、権限、utility |
| テーマ資源 | `ColorVision.Themes.dll` | ResourceDictionary、base window、caption、shared controls |
| UI 基盤 | `ColorVision.UI.dll` | config、menu、plugin loader、property editor、hotkey、language、log、status bar |
| native image bridge | `ColorVision.Core.dll` | `HImage`、OpenCV helper P/Invoke、WPF bitmap bridge |
| data access | `ColorVision.Database.dll` | SqlSugar DAO、MySQL/SQLite config、database browser provider |
| desktop communication | `ColorVision.SocketProtocol.dll` | local TCP server、JSON/Text dispatch、message history |
| scheduler | `ColorVision.Scheduler.dll` | Quartz scheduler、task config、history、management window |
| image editing | `ColorVision.ImageEditor.dll` | `ImageView`、draw primitives、overlay、toolbar、pseudo-color、CIE、3D |
| desktop tools | `ColorVision.UI.Desktop.exe` / package | settings、wizard、marketplace、downloader、diagnostics |
| workspace | `ColorVision.Solution.dll` | `.cvsln`、explorer、editors、AvalonDock、terminal、local RBAC |

## どこを変更するか

| 目的 | 優先モジュール |
| --- | --- |
| ViewModel、Command、共有 interface | `ColorVision.Common` |
| theme、shared window style | `ColorVision.Themes` |
| menu、settings、status bar、PropertyGrid | `ColorVision.UI` |
| OpenCV/native image call | `ColorVision.Core` |
| database browser source or DAO | `ColorVision.Database` |
| Socket JSON event handling | `ColorVision.SocketProtocol` |
| scheduled task | `ColorVision.Scheduler` |
| image tool、draw primitive、overlay | `ColorVision.ImageEditor` |
| settings page、wizard、marketplace、downloader | `ColorVision.UI.Desktop` |
| workspace editor、explorer、terminal、RBAC | `ColorVision.Solution` |

## 境界

- `Common`、`Themes`、`Core` は上位 window、Engine business、customer project flow を直接知るべきではありません。
- `ImageEditor` は表示、tools、primitives、overlay を担当し、customer CSV/MES/Socket output を担当しません。
- `Solution` は workspace shell であり、device control や project workflow の中心ではありません。
- 新しい public window、Provider、PropertyEditor、EditorTool、IEditor を追加したら、このページまたは該当 DLL ページを更新します。
