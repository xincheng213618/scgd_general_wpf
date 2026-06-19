# ProjectHeyuan

`Projects/ProjectHeyuan/` は Heyuan 顧客向けパッケージで、`ProjectHeyuan.dll` として読み込まれます。

## 実行時 ID

| Field | Value |
| --- | --- |
| `Id` | `ProjectHeyuan` |
| `version` | `1.0` |
| `dllpath` | `ProjectHeyuan.dll` |
| `requires` | `1.3.15.10` |

## 業務範囲

4 点の色/輝度検査と顧客 Serial pass-through を扱います。固定順序：

```text
White, Blue, Red, Orange
```

これらは `TempResult` に集約され、PASS/FAIL、CSV、MES upload に使われます。

## 主要コード

| ファイル | 役割 |
| --- | --- |
| `ProjectHeyuanWindow.xaml(.cs)` | メインウィンドウ |
| `MenuItemHeyuan.cs` | launcher/menu |
| `HYMesManager.cs` | MES/Serial |
| `SerialMsg.cs` | message model |
| `TempResult.cs` | 4 点結果 |
| `NumSet.cs` | limit |

## 引き継ぎ注意

- Serial message は `0x02 + ASCII + 0x03` です。
- `CSN`、`CMI`、`CGI`、`CPT` の return code は現場プロトコル境界です。
- Flow 出力が 4 POI 未満なら業務データエラーです。
- 色順、`TestName`、field format を変えたら CSV と protocol を同時に更新します。
