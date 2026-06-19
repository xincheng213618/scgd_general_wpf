# UI DLL 发布证据与现场核查表

这页面向真正要把 UI DLL 发出去或替换到现场的人。它不重复介绍组件能力，而是把“怎么证明这批 DLL 是完整的、同一批的、能被 Engine/插件/项目包消费”写成可核查证据。

## 要留下哪些证据

| 证据 | 要保存什么 | 作用 |
| --- | --- | --- |
| 项目配置证据 | `TargetFrameworks`、`VersionPrefix`、`GeneratePackageOnBuild`、`PackageReadmeFile`、资源项 | 证明发版依据来自当前 `.csproj` |
| 构建证据 | `dotnet restore`、每个 UI 项目 `dotnet build`、主程序/Engine 构建日志 | 证明 DLL、`.nupkg`、`.snupkg` 是同一轮生成 |
| 包内容证据 | 展开的 `.nupkg` 目录清单 | 证明 README、native runtime、shader、CIE、CSS、工具 exe 进包 |
| 输出目录证据 | 主程序输出目录 `ColorVision.*.dll` 的文件版本和时间戳 | 证明现场运行时拿到的是预期 DLL |
| 消费方证据 | Engine、关键插件、关键项目包的 restore/build 或运行烟测 | 证明不是只发了 UI 包，消费方也能加载 |
| 回退证据 | 上一批 DLL、`.nupkg`、`.snupkg`、插件目录备份位置 | 出现现场问题时能快速回退 |

## 项目配置核查

先在仓库根目录执行：

```powershell
rg -n "TargetFrameworks|VersionPrefix|GeneratePackageOnBuild|PackageReadmeFile|PackagePath|CopyToOutputDirectory|OutputType" UI -g "*.csproj"
Get-Content Directory.Build.props
```

重点确认：

| 项 | 要看什么 |
| --- | --- |
| `TargetFrameworks` | 是否是当前宿主和插件能加载的 Windows TFM |
| `VersionPrefix` | 是否和发布记录、包名、现场 DLL 文件版本一致 |
| `GeneratePackageOnBuild` | Release 构建是否应该生成 `.nupkg` 和 `.snupkg` |
| `PackageReadmeFile` | README 是否会进入包根 |
| `PackagePath` / `Pack` | README、native DLL、CSS、工具 exe 是否进入 NuGet 包 |
| `CopyToOutputDirectory` | 运行时必须存在的文件是否会复制到输出目录 |
| `OutputType` | `ColorVision.UI.Desktop` 是 `WinExe`，但仍作为包和依赖参与发布 |

## DLL 与资源证据表

| 发布单元 | 必须核对的 `.csproj` 证据 | 必须核对的包/输出证据 |
| --- | --- | --- |
| `ColorVision.Common` | `TargetFrameworks=net8.0-windows7.0;net10.0-windows7.0`、`VersionPrefix=1.5.5.2`、`Assets/Cursor/eraser.cur` 为 Resource | README 在包根，cursor 资源可加载 |
| `ColorVision.Themes` | `VersionPrefix=1.5.5.3`、`HandyControl`、`Assets/Image/*.ico`、`uploadbg.avif` | 主题 XAML、图标、上传背景进入资源或包 |
| `ColorVision.UI` | `VersionPrefix=1.5.5.3`、引用 `Common`/`Themes`、`PackageReadmeFile=README.md` | 插件、菜单、配置、PropertyGrid 类型来自同一版本 DLL |
| `ColorVision.Core` | `opencv_helper.dll`、OpenCV `4130` DLL、可选 `opencv_cuda.dll` 的 `PackagePath=runtimes/win-x64/native` | `.nupkg` 中存在 `runtimes/win-x64/native`，输出目录能找到 `opencv*.dll` |
| `ColorVision.Database` | `VersionPrefix=1.5.5.3`、`SqlSugarCore`、引用 `ColorVision.UI` | MySQL/SQLite Provider 和数据库浏览器能打开 |
| `ColorVision.SocketProtocol` | `VersionPrefix=1.5.5.2`、引用 `Database` 和 `UI` | Socket 管理窗口、端口配置、消息历史库正常 |
| `ColorVision.Scheduler` | `VersionPrefix=1.5.5.2`、`Quartz`、`SqlSugarCore`、`Properties/README.md` 打包 | 任务 JSON、`SchedulerHistory.db`、任务窗口能读写 |
| `ColorVision.ImageEditor` | `VersionPrefix=1.5.5.5`、OpenCvSharp、shader `*.ps`、`Assets/Colormap/*.jpg`、`CIE_cc_1931_2deg.csv` | 普通图片、伪彩、CIE、3D、overlay、注释导入导出能打开 |
| `ColorVision.UI.Desktop` | `VersionPrefix=1.5.5.3`、`OutputType=WinExe`、WebView2、`github-markdown.css`、`aria2c.exe` | 插件市场 README 预览、下载器、DLL 版本窗口正常 |
| `ColorVision.Solution` | `VersionPrefix=1.5.5.2`、AvalonDock、AvalonEdit、WebView2、WPFHexaEditor | `.cvsln`、文件树、编辑器、终端、RBAC 管理能打开 |

## 构建顺序证据

建议把实际执行过的命令保存到发布记录里。排查单包失败时按依赖从底层到上层构建：

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
dotnet build ColorVision/ColorVision.csproj -c Release -p:Platform=x64
```

如果这次影响 Engine 的包引用环境，还要记录：

```powershell
rg -n 'UIProjectPackageVersion|PackageReference Include="ColorVision' Engine/ColorVision.Engine/ColorVision.Engine.csproj
dotnet restore Engine/ColorVision.Engine/ColorVision.Engine.csproj
dotnet build Engine/ColorVision.Engine/ColorVision.Engine.csproj -c Release -p:Platform=x64
```

`Engine/ColorVision.Engine` 当前对多个 UI 包是“源码存在用 `ProjectReference`，源码不存在用 `PackageReference` + `UIProjectPackageVersion`”。外部只拿包的环境不能用源码环境构建成功来替代验证。

## 包内容抽检命令

### Core native runtime

```powershell
$pkg = Get-ChildItem UI/ColorVision.Core/bin -Recurse -Filter "ColorVision.Core.*.nupkg" |
  Sort-Object LastWriteTime -Descending |
  Select-Object -First 1
$tmp = Join-Path $env:TEMP "cv-core-nupkg"
Remove-Item $tmp -Recurse -Force -ErrorAction SilentlyContinue
New-Item $tmp -ItemType Directory | Out-Null
Copy-Item $pkg.FullName "$tmp/core.zip"
Expand-Archive "$tmp/core.zip" "$tmp/core"
Get-ChildItem "$tmp/core/runtimes/win-x64/native" |
  Select-Object Name, Length
```

必须能看到 `opencv_helper.dll` 和 OpenCV `4130` 系列 DLL。`opencv_cuda.dll` 是条件文件，只有源文件存在时才进入包。

### ImageEditor 资源

```powershell
$pkg = Get-ChildItem UI/ColorVision.ImageEditor/bin -Recurse -Filter "ColorVision.ImageEditor.*.nupkg" |
  Sort-Object LastWriteTime -Descending |
  Select-Object -First 1
$tmp = Join-Path $env:TEMP "cv-imageeditor-nupkg"
Remove-Item $tmp -Recurse -Force -ErrorAction SilentlyContinue
New-Item $tmp -ItemType Directory | Out-Null
Copy-Item $pkg.FullName "$tmp/imageeditor.zip"
Expand-Archive "$tmp/imageeditor.zip" "$tmp/imageeditor"
Get-ChildItem "$tmp/imageeditor" -Recurse |
  Where-Object { $_.Name -match "colorscale_|CIE_cc|\.ps$|CIE1931|ColorVision" } |
  Select-Object FullName, Length
```

### UI.Desktop 辅助资源

```powershell
$pkg = Get-ChildItem UI/ColorVision.UI.Desktop/bin -Recurse -Filter "ColorVision.UI.Desktop.*.nupkg" |
  Sort-Object LastWriteTime -Descending |
  Select-Object -First 1
$tmp = Join-Path $env:TEMP "cv-uidesktop-nupkg"
Remove-Item $tmp -Recurse -Force -ErrorAction SilentlyContinue
New-Item $tmp -ItemType Directory | Out-Null
Copy-Item $pkg.FullName "$tmp/uidesktop.zip"
Expand-Archive "$tmp/uidesktop.zip" "$tmp/uidesktop"
Get-ChildItem "$tmp/uidesktop" -Recurse |
  Where-Object { $_.Name -in @("README.md","github-markdown.css","aria2c.exe") } |
  Select-Object FullName, Length
```

## 输出目录核查

现场最终运行看的不是 `.nupkg`，而是主程序目录和插件目录实际加载的 DLL。

```powershell
$out = "ColorVision/bin/x64/Release/net10.0-windows"
Get-ChildItem $out -Filter "ColorVision*.dll" |
  Select-Object Name, LastWriteTime, @{Name="FileVersion";Expression={$_.VersionInfo.FileVersion}}

Get-ChildItem $out -Recurse -Filter "opencv*.dll" |
  Select-Object FullName, LastWriteTime, Length
```

如果现场是插件问题，还要对比插件 `.deps.json`：

```powershell
Get-ChildItem ColorVision/bin/x64/Release/net10.0-windows/Plugins -Recurse -Filter "*.deps.json" |
  Select-String -Pattern "ColorVision\\."
```

常见事故是：插件目录换了新插件，主程序根目录仍是旧 `ColorVision.UI.dll` 或旧 `ColorVision.ImageEditor.dll`。

## 最小烟测证据

| 范围 | 必须留下的烟测结果 |
| --- | --- |
| `Common` / `Themes` / `UI` | 主程序启动、菜单出现、设置窗口可保存、PropertyGrid 可打开、插件管理器能读 README |
| `Core` / `ImageEditor` | 普通图片、伪彩、CIE、3D、overlay、注释导入导出至少各一次 |
| `Database` | MySQL/SQLite Provider 能列库表，数据库浏览器能打开 |
| `SocketProtocol` | 服务能启停，JSON/Text 能分发，消息历史能落库 |
| `Scheduler` | 任务列表能加载，任务执行后历史能记录 |
| `UI.Desktop` | 插件市场、DLL 版本窗口、下载器、设置窗口能打开 |
| `Solution` | `.cvsln`、文件树、文本编辑器、图像编辑器、终端、RBAC 管理能打开 |
| Engine/插件/项目包消费 | Engine build 通过，关键插件加载，关键项目包菜单/Socket/结果链路能跑 |

## 交接记录模板

```text
发布日期：
发布人：
发布范围：
UI 项目版本：
构建命令：
生成的 nupkg/snupkg：
主程序输出目录：
包内容抽检：
native runtime 抽检：
资源抽检：
Engine/插件/项目包验证：
烟测结果：
已知限制：
回退包位置：
```

## 继续阅读

- [UI DLL 发布场景手册](./ui-dll-release-playbook.md)
- [UI DLL 发布矩阵](./release-matrix.md)
- [UI DLL 发布手册](./publishing.md)
- [UI DLL 组件手册](./component-handbook.md)

