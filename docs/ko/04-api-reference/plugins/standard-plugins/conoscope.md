# Conoscope 플러그인

Conoscope 는 현재 저장소에 존재하는 VAM/코노스코프 이미지 분석 플러그인입니다. 소스는 `Plugins/Conoscope/` 입니다.

## 로딩 정보

| 항목 | 값 |
| --- | --- |
| directory | `Plugins/Conoscope/` |
| project | `Conoscope.csproj` |
| manifest Id | `Conoscope` |
| version | `1.4.6.1` |
| dllpath | `Conoscope.dll` |

## 주요 기능

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
