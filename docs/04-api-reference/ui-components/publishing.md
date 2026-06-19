# UI DLL 发布手册

这页专门说明 `UI/` 下 DLL/NuGet 包的发布方式。它面向维护 UI 类库、交付 Engine 依赖包、排查插件缺 DLL 的人员。

需要快速确认每个 DLL 的版本、TFM、依赖、包内资源和烟测范围时，先看 [UI DLL 发布矩阵](./release-matrix.md)。如果你接到的是“现场替换 DLL”“插件缺 DLL”“Engine 只拿 NuGet 包”等具体任务，先按 [UI DLL 发布场景手册](./ui-dll-release-playbook.md) 判断范围。发布后要留下可追溯证据时，按 [UI DLL 发布证据与现场核查表](./dll-release-evidence.md) 填写。本页负责展开通用发布流程。

## 发布对象

UI 发布对象不是一个单包，而是一组项目：

- 基础包：`ColorVision.Common`、`ColorVision.Themes`、`ColorVision.UI`
- 数据与通信包：`ColorVision.Database`、`ColorVision.SocketProtocol`、`ColorVision.Scheduler`
- 图像包：`ColorVision.Core`、`ColorVision.ImageEditor`
- 桌面壳层包：`ColorVision.UI.Desktop`、`ColorVision.Solution`

其中 `ColorVision.UI.Desktop` 的输出类型是 `WinExe`，但项目仍启用了打包；交接时要确认它是作为桌面辅助模块使用，还是作为主程序依赖被打进宿主输出。

## 发布前决策

| 问题 | 决策 |
| --- | --- |
| 只是改了窗口样式或 XAML 资源 | 构建对应 UI 项目，并验证宿主窗口 |
| 改了 `Common`、`Themes`、`UI` 的 public 类型 | 同步构建所有上层 UI 项目、Engine、关键插件和项目包 |
| 改了 `Core` 或 `ImageEditor` | 必须抽检 native runtime、shader、colormap、CIE 数据和 OpenCvSharp runtime |
| 改了 `Database`、`SocketProtocol`、`Scheduler` | 同步验证数据库、Socket、调度器配置和历史库兼容性 |
| 改了 `Solution` 或 `UI.Desktop` | 验证设置、市场、下载、工作区、编辑器、终端和 WebView2 |
| 要给外部环境只交 NuGet 包 | 锁定 `UIProjectPackageVersion`，不要依赖 `*` 自动解析 |

## 构建命令

在仓库根目录执行：

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

多数 UI 项目设置了 `GeneratePackageOnBuild=True`，所以 Release 构建会同步生成 `.nupkg` 和 `.snupkg`。

如果这次只发布某个上层包，也要先确认它的底层依赖是否已经使用预期版本。例如发布 `ColorVision.Solution` 前，至少确认 `ColorVision.ImageEditor`、`ColorVision.UI.Desktop`、`ColorVision.UI` 和 `ColorVision.Database` 的输出不是旧版本。

## 包输出位置

默认 SDK 风格项目会把包放在项目自己的输出目录附近，常见路径是：

```text
UI/<Project>/bin/Release/ColorVision.<Name>.<Version>.nupkg
UI/<Project>/bin/Release/ColorVision.<Name>.<Version>.snupkg
UI/<Project>/bin/x64/Release/<TFM>/ColorVision.<Name>.dll
```

实际路径受项目目标框架和平台影响。交接时以构建日志和 `.csproj` 为准。

## 包内必须带的文件

| 模块 | 必须检查 |
| --- | --- |
| `ColorVision.Common` | `README.md`、资源文件、共享接口 |
| `ColorVision.Themes` | 主题资源、图标、`uploadbg.avif`、README |
| `ColorVision.UI` | README、菜单/插件/属性编辑器资源 |
| `ColorVision.Core` | `opencv_helper.dll`、OpenCV 运行库、README |
| `ColorVision.Database` | README、资源、SqlSugar 依赖 |
| `ColorVision.SocketProtocol` | README、资源、Database/UI 依赖 |
| `ColorVision.Scheduler` | README 或 `Properties/README.md`、Quartz 依赖 |
| `ColorVision.ImageEditor` | 伪彩色图、CIE 数据、shader、图标、OpenCV runtime、README |
| `ColorVision.UI.Desktop` | README、`github-markdown.css`、`aria2c.exe`、WebView2 依赖 |
| `ColorVision.Solution` | README、AvalonEdit/AvalonDock/WebView2/WPFHexaEditor 依赖 |

如果包内缺 README，插件市场或 DLL 版本窗口里的说明会很难追溯；如果 `runtimes/win-x64/native` 缺 native DLL，图像和视频链路会在运行时失败。

更完整的资源矩阵见：[UI DLL 发布矩阵](./release-matrix.md#包内资源验收)。

## 被 Engine 引用的方式

`Engine/ColorVision.Engine/ColorVision.Engine.csproj` 对 UI 包有两种引用模式：

- 源码存在时使用 `ProjectReference`，例如 `..\..\UI\ColorVision.ImageEditor\ColorVision.ImageEditor.csproj`。
- 源码不存在时部分模块回退到 `PackageReference Include="ColorVision.ImageEditor" Version="$(UIProjectPackageVersion)"`。

因此发布 UI DLL 时要同时考虑源码开发环境和只拿包的交付环境。`UIProjectPackageVersion` 当前默认是 `*`，交付给外部环境时建议显式锁定版本，避免解析到不匹配的 UI 包。

插件和项目包多为 `ProjectReference` 开发形态，但现场运行时最终还是看主程序输出目录和插件目录里的 DLL。交付时不要只检查插件目录，必须同时检查主程序根目录中的 `ColorVision.*.dll`。

## 强名称与版本

全局签名规则在根目录 `Directory.Build.props`：

- `AssemblyOriginatorKeyFile` 指向 `ColorVision.snk`。
- key 存在时 `SignAssembly=True`。
- key 不存在时 `SignAssembly=False`。

发布正式 DLL 时不要手动关闭签名；如果某台构建机没有 key，应先确认这是否是预期的非正式构建。

版本号主要来自每个 `.csproj` 的 `VersionPrefix`。发布前至少检查：

```powershell
rg -n "VersionPrefix|GeneratePackageOnBuild|PackageReadmeFile" UI -g "*.csproj"
```

## 发布后验证

1. 构建主程序：

```powershell
dotnet build ColorVision/ColorVision.csproj -c Release -p:Platform=x64
```

2. 检查主程序输出目录是否包含必要 DLL：

```powershell
Get-ChildItem ColorVision/bin/x64/Release/net10.0-windows -Filter "ColorVision*.dll"
```

3. 检查 native runtime：

```powershell
Get-ChildItem ColorVision/bin/x64/Release/net10.0-windows -Recurse -Filter "opencv*.dll"
```

4. 打开主程序后至少验证：

- 属性编辑器能打开。
- 图像编辑器能打开普通图片。
- 数据库浏览器能打开。
- Socket 管理窗口能启动。
- 插件管理器能读取插件 README/CHANGELOG。

5. 如果本次发版包含 `Core` 或 `ImageEditor`，额外验证：

- 普通图片打开。
- 伪彩色切换。
- CIE 图或 CIE 相关资源加载。
- 注释导入导出。
- 3D 表面或模型查看窗口能打开。
- 视频或 OpenCV 相关功能没有 native DLL 报错。

6. 如果本次发版要供 Engine 包引用环境使用，额外验证：

```powershell
rg -n 'UIProjectPackageVersion|PackageReference Include="ColorVision' Engine/ColorVision.Engine/ColorVision.Engine.csproj
dotnet restore Engine/ColorVision.Engine/ColorVision.Engine.csproj
dotnet build Engine/ColorVision.Engine/ColorVision.Engine.csproj -c Release -p:Platform=x64
```

## 常见问题

### 构建成功但运行时缺 DLL

先看缺的是托管 DLL 还是 native DLL。托管 DLL 通常来自项目引用或 NuGet 包解析；native DLL 通常来自 `Content`、`Resource`、`CopyToOutputDirectory`、`PackagePath` 配置。

### 插件依赖版本不足

插件目录中如果只有一个 `.deps.json`，`PluginLoader` 会检查 `ColorVision.*` 依赖版本。主程序目录里的实际 DLL 版本低于插件要求时，插件会被跳过。

### 包能构建但外部机器不能用

优先检查三个点：

- 是否发布了 `.nupkg` 但漏了 native runtime。
- 目标机器是否安装 .NET Desktop Runtime。
- 强名称签名和引用版本是否一致。

## 推荐维护习惯

- 修改 UI 公共 API 时同步更新模块 README 和本页的包清单。
- 修改资源打包规则时运行一次 Release x64 构建并检查 `.nupkg` 内容。
- 修改 `ColorVision.Core` 或 `ImageEditor` 时额外验证 OpenCV、视频和 RGB48 图像链路。
- 交付插件或项目包前，先确认主程序输出目录里的 `ColorVision.*.dll` 版本满足插件 `.deps.json`。
- 每次发布留下发布矩阵记录：范围、版本、资源抽检、消费方验证、烟测结果和回退包位置。

## 继续阅读

- [UI DLL 发布场景手册](./ui-dll-release-playbook.md)
- [UI DLL 发布矩阵](./release-matrix.md)
- [UI DLL 发布证据与现场核查表](./dll-release-evidence.md)
- [UI DLL 组件手册](./component-handbook.md)
