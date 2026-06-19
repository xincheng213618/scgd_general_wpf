# UI DLL リリース証跡と現地確認表

このページは、UI DLL をリリースまたは現地で置換する担当者向けです。機能説明ではなく、「この DLL セットが完全で、同じバージョン群で、Engine・プラグイン・プロジェクトから消費できる」ことを証明する証跡を整理します。

## 残す証跡

| 証跡 | 残す内容 | 目的 |
| --- | --- | --- |
| プロジェクト設定 | `TargetFrameworks`、`VersionPrefix`、`GeneratePackageOnBuild`、`PackageReadmeFile`、リソース項目 | 現在の `.csproj` に基づくリリースであること |
| ビルドログ | `dotnet restore`、UI project build、host/Engine build | DLL、`.nupkg`、`.snupkg` が同じビルドで生成されたこと |
| パッケージ内容 | 展開した `.nupkg` の一覧 | README、native runtime、shader、CIE、CSS、tool exe が入っていること |
| 出力ディレクトリ | host 出力の `ColorVision.*.dll` バージョンと時刻 | 実行時に意図した DLL が読まれること |
| 消費側 | Engine、重要プラグイン、重要プロジェクトの build または smoke test | UI パッケージ単体ではなく消費側も動くこと |
| ロールバック | 直前の DLL と package | 現地問題時に戻せること |

## 設定確認

```powershell
rg -n "TargetFrameworks|VersionPrefix|GeneratePackageOnBuild|PackageReadmeFile|PackagePath|CopyToOutputDirectory|OutputType" UI -g "*.csproj"
Get-Content Directory.Build.props
```

`ColorVision.UI.Desktop` は `WinExe` ですが、package と依存関係の確認対象です。TFM、Version、README packing、native DLL/CSS/tool exe の `Pack` と `CopyToOutputDirectory` を確認します。

## DLL 証跡マトリクス

| Unit | `.csproj` 証跡 | package/output 証跡 |
| --- | --- | --- |
| `ColorVision.Common` | net8/net10 Windows、`VersionPrefix=1.5.5.2`、cursor resource | README が package root にある、cursor が読める |
| `ColorVision.Themes` | `VersionPrefix=1.5.5.3`、`HandyControl`、icon、`uploadbg.avif` | theme XAML、icon、upload background |
| `ColorVision.UI` | `VersionPrefix=1.5.5.3`、`Common`/`Themes` reference | plugin、menu、config、PropertyGrid が同じ DLL セット |
| `ColorVision.Core` | `opencv_helper.dll`、OpenCV `4130` DLL、任意 `opencv_cuda.dll` が `runtimes/win-x64/native` | package と host output に native runtime |
| `ColorVision.Database` | `SqlSugarCore`、`VersionPrefix=1.5.5.3` | MySQL/SQLite provider と database browser |
| `ColorVision.SocketProtocol` | `Database`/`UI` reference、`VersionPrefix=1.5.5.2` | socket window、port config、message history |
| `ColorVision.Scheduler` | `Quartz`、`SqlSugarCore`、README packing | task JSON、`SchedulerHistory.db`、task window |
| `ColorVision.ImageEditor` | OpenCvSharp、shader、colormap、CIE CSV | image、pseudo color、CIE、3D、overlay、annotation |
| `ColorVision.UI.Desktop` | `OutputType=WinExe`、WebView2、CSS、`aria2c.exe` | marketplace README preview、downloader、DLL version window |
| `ColorVision.Solution` | AvalonDock、AvalonEdit、WebView2、WPFHexaEditor | `.cvsln`、explorer、editors、terminal、RBAC windows |

## 現地確認コマンド

```powershell
$out = "ColorVision/bin/x64/Release/net10.0-windows"
Get-ChildItem $out -Filter "ColorVision*.dll" |
  Select-Object Name, LastWriteTime, @{Name="FileVersion";Expression={$_.VersionInfo.FileVersion}}
Get-ChildItem $out -Recurse -Filter "opencv*.dll" |
  Select-Object FullName, LastWriteTime, Length
```

Plugin 問題では、plugin folder だけでなく host root の `ColorVision.*.dll` も確認します。

## 引き継ぎテンプレート

```text
Release date:
Owner:
Scope:
UI project versions:
Build commands:
Generated nupkg/snupkg:
Host output directory:
Package content checks:
Native runtime checks:
Resource checks:
Engine/plugin/project validation:
Smoke tests:
Known limits:
Rollback location:
```

