# ProjectKB

`Projects/ProjectKB/` はキーボードバックライト検査パッケージで、`ProjectKB.dll` として読み込まれます。FlowEngine、KB template、POI luminance、Recipe、backlight autotune、PLC/Modbus、MES DLL、CSV/summary を組み合わせます。

## 実行時 ID

| Field | Value |
| --- | --- |
| `Id` | `ProjectKB` |
| `version` | `1.0` |
| `dllpath` | `ProjectKB.dll` |
| `requires` | `1.3.15.10` |

## Entry modes

| Entry | 内容 |
| --- | --- |
| Manual | operator が SN と FlowTemplate を選択して実行 |
| Modbus | PLC が holding register に `1` を書き、完了時に `0` を書き戻す |
| MES | `FunTestDll.dll` の `CheckWIP` と `Collect_test` を使用 |

## 主要コード

| ファイル | 役割 |
| --- | --- |
| `ProjectKBWindow.xaml(.cs)` | Flow、結果、CSV/MES/Modbus |
| `KBRecipeConfig.cs` | luminance、uniformity、local contrast、autotune limits |
| `BacklightAutotuneService.cs` | Q1/Q3 と sigmoid 補正 |
| `KBItemMaster.cs` | master result |
| `Modbus/ModbusControl.cs` | Modbus TCP |
| `MesDll.cs` | `FunTestDll.dll` P/Invoke |

## 引き継ぎ注意

- `FunTestDll.dll` と `FunTestDllConfig.INI` は納入検証に必須です。
- `CheckWIP` の戻り規約は顧客 DLL 版に依存します。
- `KBLVSacle` は calibration と履歴値解釈に影響します。
- POI 名称とサイズは KB template と一致させます。
- Modbus、Socket、MES は別経路です。現場の使用経路を先に確認します。
