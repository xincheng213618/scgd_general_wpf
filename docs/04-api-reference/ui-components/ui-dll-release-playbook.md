# UI DLL 发布场景手册

这页面向实际负责发版、现场替换 DLL、排查缺依赖、给 Engine 或插件准备 UI 包的人员。它不按类库源码讲解，而是按“接到一个发布任务后怎么判断、怎么构建、怎么验收、怎么留下交接记录”组织。

如果你只是想知道每个 DLL 负责什么，读 [UI DLL 组件手册](./component-handbook.md)。如果你已经知道要发布什么，需要查版本、资源和烟测范围，本页配合 [UI DLL 发布矩阵](./release-matrix.md) 使用。需要交付给现场或留下审计记录时，再按 [UI DLL 发布证据与现场核查表](./dll-release-evidence.md) 保存构建、包内容、输出目录和回退证据。

## 使用方法

1. 先用 [发布范围判断](#发布范围判断) 确定这次是单 DLL、底层包、图像包、桌面壳层包，还是要连带 Engine/插件/项目包一起验证。
2. 按 [构建与产物确认](#构建与产物确认) 生成 DLL、`.nupkg` 和 `.snupkg`。
3. 按 [场景处理](#场景处理) 做资源抽检和运行时验收。
4. 最后填写 [发布交接记录](#发布交接记录)，不要只留下一个压缩包。

## 发布范围判断

| 接到的任务 | 最小发布范围 | 必须连带验证 |
| --- | --- | --- |
| 只改 `README.md`、说明文本或文档资源 | 对应 UI 项目的包 | `.nupkg` 中 README 是否进入包根 |
| 改 `ColorVision.Common` 的接口、命令、权限或工具类 | `Common` 以及所有引用它的上层 UI 包 | 主程序启动、插件加载、PropertyGrid、ImageEditor |
| 改 `ColorVision.Themes` 的主题、窗口基类、控件样式 | `Themes`、`UI`、使用主题的窗口包 | 主窗口、设置页、插件窗口、项目包窗口 |
| 改 `ColorVision.UI` 的菜单、配置、插件加载、PropertyGrid、状态栏 | `UI` 以及插件/项目包消费方 | 插件管理器、设置页、属性编辑器、状态栏 |
| 改 `ColorVision.Core` 的 `HImage`、OpenCV helper、native bridge | `Core`、`ImageEditor`、Engine 图像链路 | native DLL、普通图片、视频/伪彩/OpenCV 调用 |
| 改 `ColorVision.ImageEditor` 的绘图、overlay、伪彩、CIE、3D | `ImageEditor`，通常连带 `Core` | 图像打开、结果 overlay、CIE、3D、资源文件 |
| 改 `Database`、`SocketProtocol`、`Scheduler` | 对应包及其 UI 入口 | 数据库浏览器、Socket 管理窗口、任务管理窗口 |
| 改 `UI.Desktop`、`Solution` | 桌面工具层和工作区层 | 插件市场、下载器、WebView2、`.cvsln`、终端 |
| 给现场替换某个插件或项目包 | 主程序目录的 `ColorVision.*.dll` 加插件/项目目录 | 插件 `.deps.json`、manifest、主程序根目录 DLL 版本 |
| 给外部环境只交 NuGet 包，不交源码 | 所有被 Engine/package fallback 使用的 UI 包 | `UIProjectPackageVersion`、restore/build、包源版本锁定 |

## 构建与产物确认

先确认发布配置来自真实项目文件：

```powershell
rg -n "VersionPrefix|GeneratePackageOnBuild|PackageReadmeFile|PackagePath|CopyToOutputDirectory|TargetFrameworks|OutputType" UI -g "*.csproj"
Get-Content Directory.Build.props
```

这些字段决定了 DLL 版本、目标框架、是否生成 `.nupkg`、README 是否进入包、资源是否进入输出目录或 NuGet 包。

推荐先恢复依赖，再按底层到上层构建：

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

常见产物位置：

```text
UI/<Project>/bin/Release/ColorVision.<Name>.<Version>.nupkg
UI/<Project>/bin/Release/ColorVision.<Name>.<Version>.snupkg
UI/<Project>/bin/x64/Release/<TFM>/ColorVision.<Name>.dll
ColorVision/bin/x64/Release/net10.0-windows/
```

交接时以构建日志和实际目录为准，不要只看旧包名。

## 场景处理

### 场景 A：只发布基础 UI DLL

适用：`ColorVision.Common`、`ColorVision.Themes`、`ColorVision.UI`。

处理步骤：

1. 确认 `VersionPrefix` 是否更新。
2. 构建被改项目和上层依赖项目。
3. 构建主程序，确认输出目录里 `ColorVision.Common.dll`、`ColorVision.Themes.dll`、`ColorVision.UI.dll` 是同一批时间戳。
4. 启动主程序，至少打开设置页、菜单、PropertyGrid、插件管理器。

重点验收：

| 改动点 | 验收 |
| --- | --- |
| `Common` 接口或工具 | 插件/项目包不出现 `MissingMethodException` 或类型加载失败 |
| `Themes` 资源 | 主窗口、插件窗口、项目窗口主题正常，图标和背景资源不丢 |
| `UI` 插件加载 | 插件能读取 manifest、README、CHANGELOG，状态栏和菜单正常 |

### 场景 B：发布 Core 或 ImageEditor

适用：OpenCV、`HImage`、图像显示、结果 overlay、伪彩、CIE、3D、视频链路。

处理步骤：

1. 先构建 `ColorVision.Core`，再构建 `ColorVision.ImageEditor`。
2. 抽检 `ColorVision.Core.*.nupkg` 是否包含 `runtimes/win-x64/native`。
3. 抽检 `ColorVision.ImageEditor.*.nupkg` 是否包含 shader、colormap、CIE CSV、图标资源。
4. 构建主程序、Engine、引用 ImageEditor 的关键插件和项目包。
5. 打开一张普通图片，切换伪彩，打开 CIE 或 3D 相关窗口，确认结果 overlay 能显示。

Core native 抽检示例：

```powershell
$pkg = Get-ChildItem UI/ColorVision.Core/bin -Recurse -Filter "ColorVision.Core.*.nupkg" |
  Sort-Object LastWriteTime -Descending |
  Select-Object -First 1
$tmp = Join-Path $env:TEMP "cv-core-nupkg"
Remove-Item $tmp -Recurse -Force -ErrorAction SilentlyContinue
New-Item $tmp -ItemType Directory | Out-Null
Copy-Item $pkg.FullName "$tmp/core.zip"
Expand-Archive "$tmp/core.zip" "$tmp/core"
Get-ChildItem "$tmp/core/runtimes/win-x64/native"
```

ImageEditor 资源抽检示例：

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
  Where-Object { $_.Name -match "colorscale_|CIE_cc|\.ps$|CIE1931" }
```

### 场景 C：发布 Database、SocketProtocol 或 Scheduler

适用：数据库浏览器、Socket 管理窗口、消息历史、Quartz 任务。

处理步骤：

1. 构建对应 UI 包和主程序。
2. 打开数据库浏览器，确认 MySQL/SQLite Provider 能列出库表。
3. 打开 Socket 管理窗口，确认端口配置、JSON/Text 模式、消息历史库正常。
4. 打开任务管理窗口，确认任务配置 JSON 和 `SchedulerHistory.db` 能读取。

重点排查：

| 现象 | 优先检查 |
| --- | --- |
| 数据库窗口空白 | `ColorVision.Database`、SqlSugar 依赖、连接配置 |
| Socket 服务启动失败 | 端口占用、`SocketConfig`、协议模式 |
| 任务列表不加载 | `scheduler_tasks.json`、Quartz job 扫描、历史库路径 |

### 场景 D：发布 UI.Desktop 或 Solution

适用：设置、向导、插件市场、DLL 版本窗口、下载器、工作区、文本编辑器、终端、RBAC。

处理步骤：

1. 构建 `ColorVision.UI.Desktop`、`ColorVision.Solution` 和主程序。
2. 检查 `UI.Desktop` 包内是否包含 `README.md`、`Assets/css/github-markdown.css`、`Assets/Tool/aria2c.exe`。
3. 打开插件市场和 DLL 版本窗口，确认 README 预览、版本列表、下载器路径正常。
4. 打开或新建 `.cvsln`，确认文件树、文本编辑、图像编辑、终端、布局恢复可用。

### 场景 E：现场替换后提示缺 DLL 或插件不加载

处理步骤：

1. 先确认缺的是托管 `ColorVision.*.dll`，还是 native DLL。
2. 对比插件目录 `.deps.json` 里的 `ColorVision.*` 依赖版本和主程序根目录实际 DLL 版本。
3. 不要只替换插件目录；插件运行时通常还依赖主程序根目录的一组 UI DLL。
4. 如果缺的是 OpenCV/native DLL，回到 [场景 B](#场景-b：发布-core-或-imageeditor) 抽检 `Core` 包和输出目录。

版本检查示例：

```powershell
$out = "ColorVision/bin/x64/Release/net10.0-windows"
Get-ChildItem $out -Filter "ColorVision*.dll" |
  Select-Object Name, LastWriteTime, @{Name="FileVersion";Expression={$_.VersionInfo.FileVersion}}
```

### 场景 F：给 Engine 或外部环境交 UI NuGet 包

`Engine/ColorVision.Engine/ColorVision.Engine.csproj` 在 UI 源码存在时优先走 `ProjectReference`，源码不存在时会回退到 `PackageReference Include="ColorVision.*"`。这意味着外部交付不能只说“源码环境能编译”。

处理步骤：

1. 检查 Engine 中的 fallback 引用：

```powershell
rg -n 'UIProjectPackageVersion|PackageReference Include="ColorVision' Engine/ColorVision.Engine/ColorVision.Engine.csproj
```

2. 外部包环境建议锁定明确版本，不要长期依赖 `*`。
3. 用只有包源的环境执行 `restore` 和 `build`。
4. 用同一组 UI DLL 构建主程序、Engine、关键插件和关键项目包。

## 每个发布单元的抽检重点

| 发布单元 | 项目文件里的关键证据 | 发版必须抽检 |
| --- | --- | --- |
| `ColorVision.Common` | `VersionPrefix`、`PackageReadmeFile=README.md`、cursor resource | README、`Assets/Cursor/eraser.cur`、共享接口兼容 |
| `ColorVision.Themes` | `HandyControl`、主题图标和 `uploadbg.avif` resource | 主题资源、图标、上传背景 |
| `ColorVision.UI` | `PluginLoader`、菜单、配置、PropertyGrid 所在包 | 插件加载、设置页、属性编辑器、状态栏 |
| `ColorVision.Core` | `opencv_helper.dll`、OpenCV runtime `Content Pack=true` | `runtimes/win-x64/native` 完整性 |
| `ColorVision.Database` | `SqlSugarCore`、`ColorVision.UI` 引用 | MySQL/SQLite Provider、数据库浏览器 |
| `ColorVision.SocketProtocol` | `ColorVision.Database`、`ColorVision.UI` 引用 | Socket 管理窗口、消息历史、JSON/Text 分发 |
| `ColorVision.Scheduler` | `Quartz`、`SqlSugarCore`、`Properties/README.md` pack | 任务配置、执行历史、README 包根 |
| `ColorVision.ImageEditor` | shader、colormap、CIE CSV、OpenCvSharp runtime | 图片、伪彩、CIE、3D、overlay |
| `ColorVision.UI.Desktop` | `OutputType=WinExe`、WebView2、`aria2c.exe` | 插件市场、DLL 版本窗口、下载器 |
| `ColorVision.Solution` | AvalonDock、AvalonEdit、WebView2、WPFHexaEditor | `.cvsln`、编辑器、终端、RBAC |

## 发布交接记录

每次 UI DLL 发布至少留下下面的信息：

| 项 | 填写内容 |
| --- | --- |
| 发布日期 | 例如 `2026-06-10` |
| 发布范围 | 哪些 UI 项目、是否包含 `Core` 或 `ImageEditor` |
| 版本 | 每个 `.csproj` 的 `VersionPrefix`、主程序输出目录实际 `FileVersion` |
| 构建命令 | 执行过哪些 `dotnet build`、是否构建主程序/Engine/插件/项目包 |
| 包抽检 | README、native runtime、shader、colormap、CIE、CSS、`aria2c.exe` 是否进入包 |
| 运行验收 | 主程序、PropertyGrid、ImageEditor、数据库、Socket、Scheduler、Solution、插件加载 |
| 外部交付 | 是否锁定 `UIProjectPackageVersion`，包源位置和校验方式 |
| 回退方式 | 上一批 DLL、`.nupkg`、`.snupkg`、插件目录备份位置 |
| 已知限制 | 未验证的模块、现场环境差异、管理员权限或 runtime 依赖 |

## 继续阅读

- [UI DLL 发布手册](./publishing.md)
- [UI DLL 发布矩阵](./release-matrix.md)
- [UI DLL 发布证据与现场核查表](./dll-release-evidence.md)
- [UI DLL 组件手册](./component-handbook.md)
- [UI 组件目录](./control-catalog.md)
- [插件能力与交接矩阵](../plugins/plugin-capability-matrix.md)
- [项目能力与交接矩阵](../projects/project-capability-matrix.md)
