# ProjectShiyuan

`Projects/ProjectShiyuan/` は Shiyuan 顧客向けパッケージで、`ProjectShiyuan.dll` として読み込まれます。

## 実行時 ID

| Field | Value |
| --- | --- |
| `Id` | `ProjectShiyuan` |
| `name` | `视源项目` |
| `version` | `1.0` |
| `dllpath` | `ProjectShiyuan.dll` |
| `requires` | `1.3.15.10` |

## 業務範囲

現在は FlowEngine template 実行、JND/POI result extraction、顧客 data directory への出力、pseudo-color image 保存が中心です。Heyuan や BlackMura のような完全な Serial/MES upload chain ではなく、「Flow 実行 -> result summary -> customer files」の形です。

## 主要コード

| ファイル | 役割 |
| --- | --- |
| `ShiyuanProjectWindow.xaml(.cs)` | メインウィンドウ |
| `ShiyuanProjectExport.cs` | launcher/menu |
| `ProjectShiYuanConfig.cs` | config |
| `TempResult.cs`, `NumSet.cs` | temporary result and range |
| `SerialMsg.cs` | retained serial model |

## 引き継ぎ注意

- `UploadSN` handler は現在空で、自動 SN upload 実装済みとは書かないでください。
- `SerialMsg.cs` は構造の保持であり、完全な MES chain ではありません。
- `C:\Windows\System32\pic\` は現場依存 path です。
- `DataPath` 変更時は JND CSV、POI CSV、image copy、pseudo-color の説明を同時に更新します。
