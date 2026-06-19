# UI DLL 릴리스 증거 및 현장 확인표

이 페이지는 UI DLL 을 릴리스하거나 현장에서 교체하는 담당자를 위한 문서입니다. 기능 설명이 아니라, 이 DLL 세트가 완전하고 같은 버전이며 Engine, 플러그인, 프로젝트 패키지에서 소비될 수 있음을 증명하는 증거를 정리합니다.

## 남겨야 할 증거

| 증거 | 저장할 내용 | 목적 |
| --- | --- | --- |
| 프로젝트 설정 | `TargetFrameworks`, `VersionPrefix`, `GeneratePackageOnBuild`, `PackageReadmeFile`, resource 항목 | 현재 `.csproj` 기준 릴리스임을 증명 |
| 빌드 로그 | `dotnet restore`, UI project build, host/Engine build | DLL, `.nupkg`, `.snupkg` 가 같은 빌드 산출물임을 증명 |
| 패키지 내용 | 펼친 `.nupkg` 목록 | README, native runtime, shader, CIE, CSS, tool exe 포함 확인 |
| 출력 디렉터리 | host 출력의 `ColorVision.*.dll` 버전과 시간 | 런타임이 의도한 DLL 세트를 로드함을 확인 |
| 소비자 | Engine, 주요 플러그인, 주요 프로젝트 build 또는 smoke test | UI 패키지 단독이 아니라 소비자도 로드 가능함을 확인 |
| 롤백 | 이전 DLL 과 package | 현장 문제 시 빠른 복구 |

## 설정 확인

```powershell
rg -n "TargetFrameworks|VersionPrefix|GeneratePackageOnBuild|PackageReadmeFile|PackagePath|CopyToOutputDirectory|OutputType" UI -g "*.csproj"
Get-Content Directory.Build.props
```

`ColorVision.UI.Desktop` 은 `WinExe` 이지만 package 와 dependency 릴리스 확인 대상입니다. TFM, Version, README packing, native DLL/CSS/tool exe 의 `Pack` 과 `CopyToOutputDirectory` 를 확인합니다.

## DLL 증거 매트릭스

| Unit | `.csproj` 증거 | package/output 증거 |
| --- | --- | --- |
| `ColorVision.Common` | net8/net10 Windows, `VersionPrefix=1.5.5.2`, cursor resource | README 가 package root 에 있고 cursor 로드 가능 |
| `ColorVision.Themes` | `VersionPrefix=1.5.5.3`, `HandyControl`, icon, `uploadbg.avif` | theme XAML, icon, upload background |
| `ColorVision.UI` | `VersionPrefix=1.5.5.3`, `Common`/`Themes` reference | plugin, menu, config, PropertyGrid 가 같은 DLL 세트 |
| `ColorVision.Core` | `opencv_helper.dll`, OpenCV `4130` DLL, optional `opencv_cuda.dll` in `runtimes/win-x64/native` | package 와 host output 에 native runtime |
| `ColorVision.Database` | `SqlSugarCore`, `VersionPrefix=1.5.5.3` | MySQL/SQLite provider 와 database browser |
| `ColorVision.SocketProtocol` | `Database`/`UI` reference, `VersionPrefix=1.5.5.2` | socket window, port config, message history |
| `ColorVision.Scheduler` | `Quartz`, `SqlSugarCore`, README packing | task JSON, `SchedulerHistory.db`, task window |
| `ColorVision.ImageEditor` | OpenCvSharp, shader, colormap, CIE CSV | image, pseudo color, CIE, 3D, overlay, annotation |
| `ColorVision.UI.Desktop` | `OutputType=WinExe`, WebView2, CSS, `aria2c.exe` | marketplace README preview, downloader, DLL version window |
| `ColorVision.Solution` | AvalonDock, AvalonEdit, WebView2, WPFHexaEditor | `.cvsln`, explorer, editors, terminal, RBAC windows |

## 현장 확인 명령

```powershell
$out = "ColorVision/bin/x64/Release/net10.0-windows"
Get-ChildItem $out -Filter "ColorVision*.dll" |
  Select-Object Name, LastWriteTime, @{Name="FileVersion";Expression={$_.VersionInfo.FileVersion}}
Get-ChildItem $out -Recurse -Filter "opencv*.dll" |
  Select-Object FullName, LastWriteTime, Length
```

플러그인 문제는 플러그인 폴더뿐 아니라 host root 의 `ColorVision.*.dll` 도 비교해야 합니다.

## 인수인계 템플릿

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

