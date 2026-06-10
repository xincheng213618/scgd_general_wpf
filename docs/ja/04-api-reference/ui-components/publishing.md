# UI DLL リリース手順

このページは `UI/` 配下の DLL/NuGet パッケージのリリース方法を説明します。UI library の保守、Engine 依存パッケージの提供、plugin missing DLL の調査に使います。

各 DLL の version、TFM、dependency、package resource、smoke test をすぐ確認する場合は [UI DLL リリースマトリクス](./release-matrix.md) を見ます。現場 DLL 交換、plugin dependency、Engine package fallback などの作業は [UI DLL リリースプレイブック](./ui-dll-release-playbook.md) で範囲を決めます。

## 対象

- base packages: `ColorVision.Common`、`ColorVision.Themes`、`ColorVision.UI`
- data and communication: `ColorVision.Database`、`ColorVision.SocketProtocol`、`ColorVision.Scheduler`
- image packages: `ColorVision.Core`、`ColorVision.ImageEditor`
- desktop shell packages: `ColorVision.UI.Desktop`、`ColorVision.Solution`

`ColorVision.UI.Desktop` は `WinExe` ですが package を生成します。主アプリ入口は root の `ColorVision/` です。

## build

```powershell
dotnet restore
dotnet build UI/ColorVision.Common/ColorVision.Common.csproj -c Release -p:Platform=x64
dotnet build UI/ColorVision.Themes/ColorVision.Themes.csproj -c Release -p:Platform=x64
dotnet build UI/ColorVision.UI/ColorVision.UI.csproj -c Release -p:Platform=x64
dotnet build UI/ColorVision.Core/ColorVision.Core.csproj -c Release -p:Platform=x64
dotnet build UI/ColorVision.Database/ColorVision.Database.csproj -c Release -p:Platform=x64
dotnet build UI/ColorVision.SocketProtocol/ColorVision.SocketProtocol.csproj -c Release -p:Platform=x64
dotnet build UI/ColorVision.Scheduler/ColorVision.Scheduler.csproj -c Release -p:Platform=x64
dotnet build UI/ColorVision.ImageEditor/ColorVision.ImageEditor.csproj -c Release -p:Platform=x64
dotnet build UI/ColorVision.UI.Desktop/ColorVision.UI.Desktop.csproj -c Release -p:Platform=x64
dotnet build UI/ColorVision.Solution/ColorVision.Solution.csproj -c Release -p:Platform=x64
```

多くの UI project は `GeneratePackageOnBuild=True` のため、Release build で `.nupkg` と `.snupkg` も生成します。

## release 前チェック

```powershell
rg -n "VersionPrefix|GeneratePackageOnBuild|PackageReadmeFile|PackagePath|CopyToOutputDirectory|TargetFrameworks|OutputType" UI -g "*.csproj"
Get-Content Directory.Build.props
```

| 項目 | 確認 |
| --- | --- |
| version | 各 `.csproj` の `VersionPrefix` |
| strong name | `ColorVision.snk` がある場合は署名を無効化しない |
| target framework | host、Engine、plugins、projects と互換か |
| README | package root に入るか |
| native runtime | `ColorVision.Core` の OpenCV DLL |
| resources | ImageEditor shader/colormap/CIE、Themes icons、UI.Desktop `aria2c.exe` |

## release 後検証

- `dotnet build ColorVision/ColorVision.csproj -c Release -p:Platform=x64`
- output directory の `ColorVision.*.dll` を確認。
- host を起動し、settings、PropertyGrid、ImageEditor、database、Socket、Scheduler、plugin manager を確認。
- `Core` または `ImageEditor` を含む場合は、normal image、pseudo-color、CIE、annotation import/export、3D、native DLL を追加確認。
- Engine package-only delivery では `UIProjectPackageVersion` を明示的に固定します。
