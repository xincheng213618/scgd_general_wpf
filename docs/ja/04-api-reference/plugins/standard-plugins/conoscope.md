# Conoscope プラグイン

Conoscope は現在のリポジトリに存在する VAM/コノスコープ画像分析プラグインです。ソースは `Plugins/Conoscope/` です。

## ロード情報

| 項目 | 値 |
| --- | --- |
| directory | `Plugins/Conoscope/` |
| project | `Conoscope.csproj` |
| manifest Id | `Conoscope` |
| version | `1.4.6.1` |
| dllpath | `Conoscope.dll` |

## 主な機能

- Tool menu `VAM`
- Conoscope window and Ribbon
- ImageEditor context menu `OpenByConoscope`
- focus point, reference axis, polar/curve display
- preprocess, color gamut, contrast analysis
- CSV export and MVS camera viewing

## Key Source Files

- `Plugins/Conoscope/manifest.json`
- `Plugins/Conoscope/ConoscopeMenuIBase.cs`
- `Plugins/Conoscope/ConoscopeWindow.xaml.cs`
- `Plugins/Conoscope/ConoscopeView.xaml.cs`
- `Plugins/Conoscope/ConoscopeView.FocusPoint.cs`
- `Plugins/Conoscope/Application/Analysis/ConoscopeAnalysisWorkflow.cs`
- `Plugins/Conoscope/Core/ConoscopeImageViewContextMenu.cs`

## Build

```powershell
dotnet build Plugins/Conoscope/Conoscope.csproj -c Release -p:Platform=x64
Scripts\package_plugin.bat Conoscope --no-upload
```
