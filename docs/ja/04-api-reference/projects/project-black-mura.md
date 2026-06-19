# ProjectBlackMura

`Projects/ProjectBlackMura/` は表示パネル Black Mura 検査パッケージで、`ProjectBlackMura.dll` として読み込まれます。

## 実行時 ID

| Field | Value |
| --- | --- |
| `Id` | `ProjectBlackMura` |
| `version` | `1.0` |
| `dllpath` | `ProjectBlackMura.dll` |
| `requires` | `1.3.15.10` |

## 業務範囲

PG power、PG picture switch、5 色 Flow、Engine result parse、POI overlay、Excel report、MES/Serial state をまとめた現場ワークフローです。

```text
None -> White -> Black -> Red -> Green -> Blue
```

## 主要コード

| ファイル | 役割 |
| --- | --- |
| `MainWindow.xaml(.cs)` | メインウィンドウとフロー制御 |
| `ProjectBlackMuraConfig.cs` | 設定 |
| `PluginConfig/BlackMuraProject.cs` | launcher |
| `PluginConfig/BlackMuraMenu.cs` | menu |
| `ExcelReportGenerator.cs` | Excel report |
| `HYMesManager.cs` | MES と PG Serial |

## 引き継ぎ注意

- 停止時は `CCPICompleted`、STX/ETX frame、`HYMesConfig.DeviceId` を先に確認します。
- Excel path は EPPlus に依存します。
- PG/MES は顧客現場境界であり、Engine へ移動しません。
- template 名変更時は main window の keyword matching を更新します。
