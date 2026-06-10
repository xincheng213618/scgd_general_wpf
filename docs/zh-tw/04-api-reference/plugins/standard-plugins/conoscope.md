# Conoscope 外掛

Conoscope 是目前倉庫中真實存在的 VAM/錐鏡圖像分析外掛，原始碼位於 `Plugins/Conoscope/`。

## 裝載資訊

| 欄位 | 值 |
| --- | --- |
| 目錄 | `Plugins/Conoscope/` |
| 工程 | `Conoscope.csproj` |
| manifest Id | `Conoscope` |
| manifest version | `1.4.6.1` |
| dllpath | `Conoscope.dll` |

## 主要能力

- Tool 菜單 `VAM` 入口。
- Conoscope 主窗口與 Ribbon/快捷操作區。
- ImageEditor 右鍵 `OpenByConoscope`。
- 關注點、參考軸、極座標/曲線顯示。
- 預處理、色域、對比度分析。
- CSV 匯出和 MVS 相機觀察鏈路。

## 關鍵原始碼

| 檔案 | 作用 |
| --- | --- |
| `Plugins/Conoscope/manifest.json` | 外掛清單 |
| `Plugins/Conoscope/ConoscopeMenuIBase.cs` | Tool 菜單入口 |
| `Plugins/Conoscope/ConoscopeWindow.xaml.cs` | 主窗口 |
| `Plugins/Conoscope/ConoscopeView.xaml.cs` | 圖像視圖中心 |
| `Plugins/Conoscope/ConoscopeView.FocusPoint.cs` | 關注點操作 |
| `Plugins/Conoscope/Application/Analysis/ConoscopeAnalysisWorkflow.cs` | 分析流程 |
| `Plugins/Conoscope/Core/ConoscopeImageViewContextMenu.cs` | ImageEditor 右鍵集成 |

## 交接重點

- 不要只檢查 `Conoscope.dll`，還要確認 MVS SDK 和 `MvCameraControl.dll`。
- 關注點和參考軸是插件本地邏輯，坐標問題要先查視圖縮放和圖像尺寸。
- 匯出問題要同時查分析結果和 CSV 欄位映射。

## 構建與打包

```powershell
dotnet build Plugins/Conoscope/Conoscope.csproj -c Release -p:Platform=x64
Scripts\package_plugin.bat Conoscope --no-upload
```
