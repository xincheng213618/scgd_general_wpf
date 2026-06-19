# ProjectARVRPro.IntegrationDemo

`Projects/ProjectARVRPro.IntegrationDemo/` 是給客戶、MES、PLC 或上位機驗證 ARVRPro TCP/JSON 協議的最小示例。它不是 ColorVision 外掛，也不應依賴 ColorVision 內部算法 DLL。

## 定位

| 項 | 值 |
| --- | --- |
| Target framework | .NET Framework 4.8 |
| 形態 | WPF Demo 視窗 + CLI 參數 |
| ColorVision 依賴 | 無 |
| 用途 | 演示 ARVRPro TCP 連線、命令、結果解析和 CSV 匯出 |

## 能力

- 連接 ARVRPro TCP 埠，通常是 `6666`。
- 發送 `ProjectARVRInit`、`SwitchPGCompleted`、`RunAll`、`AOITestSwitchImageComplete`。
- 載入樣例 JSON 或現場保存的 `ProjectARVRResult`。
- 顯示 `ObjectiveTestResult` 和扁平測項表。
- 保存原始 JSON 並匯出 CSV。
- 示範半包/黏包的 JSON 讀取。

## 公開合約邊界

客戶可複用的合約在 `Contracts/` 下，只描述 JSON 欄位，不應引入 ARVRPro 的流程、算法、資料庫或主程式 UI 邏輯。

## 發布

```powershell
dotnet publish Projects/ProjectARVRPro.IntegrationDemo/ProjectARVRPro.IntegrationDemo.csproj -f net48 -c Release -p:Platform=x64 -o artifacts/ProjectARVRPro.IntegrationDemo
```

## 交接注意

- 保持它是客戶側示例，而不是內部業務專案。
- 公開欄位變更時，同步更新 `Contracts/`、樣例 JSON、README 和本頁。
- 客戶 TCP Reader 必須處理半包/黏包。
