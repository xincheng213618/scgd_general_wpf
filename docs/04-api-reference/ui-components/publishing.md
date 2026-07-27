# UI DLL 发布

本页说明 `UI/` 下 DLL/NuGet 包如何发布、替换和排障。对象是一组 UI 类库，不是单个包；发布记录只需保存范围、版本、资源抽检、消费方验证、烟测结果和回退包位置。

## 发布对象

| 分组 | 项目 |
| --- | --- |
| 基础包 | `ColorVision.Common`、`ColorVision.Themes`、`ColorVision.UI` |
| 数据与通信 | `ColorVision.Database`、`ColorVision.SocketProtocol`、`ColorVision.Scheduler` |
| 图像 | `ColorVision.Core`、`ColorVision.ImageEditor` |
| 桌面壳层 | `ColorVision.UI.Desktop`、`ColorVision.Solution` |
| 应用工具 | `ColorVision.ImageTools`、`ColorVision.Rbac` |

`ColorVision.UI.Desktop` 是 `WinExe` 且启用打包，维护时要确认它是作为桌面辅助模块还是宿主依赖使用。

## 发布前决策

| 变更范围 | 必须同步验证 |
| --- | --- |
| 窗口样式或 XAML 资源 | 对应 UI 项目和宿主窗口 |
| `Common`、`Themes`、`UI` public 类型 | 所有上层 UI 项目、Engine、关键插件和项目包 |
| `Core` 或 `ImageEditor` | native runtime、shader、colormap、CIE 数据、OpenCvSharp runtime |
| `Database`、`SocketProtocol`、`Scheduler` | 数据库、Socket、调度器配置和历史库兼容性 |
| `Solution` 或 `UI.Desktop` | 设置、市场、下载、工作区、编辑器、终端、WebView2 |
| `ImageTools` 或 `Rbac` | 多图查看/融合入口、登录态、角色权限、会话和审计 |
| 外部 NuGet 环境 | 显式锁定 `UIProjectPackageVersion`，不要依赖 `*` 自动解析 |

## 构建和包资源

```powershell
dotnet restore
dotnet build ColorVision/ColorVision.csproj -c Release -p:Platform=x64
dotnet build UI/<Project>/<Project>.csproj -c Release -p:Platform=x64
```

多数 UI 项目设置了 `GeneratePackageOnBuild=True`，Release 构建会生成 `.nupkg` 和 `.snupkg`。实际包路径以构建日志和项目目标框架为准。

| 模块 | 必须检查 |
| --- | --- |
| `Common` / `Themes` / `UI` | README、共享接口、主题资源、图标、菜单、插件、属性编辑器资源 |
| `Core` / `ImageEditor` | `opencv_helper.dll`、OpenCV runtime、伪彩图、CIE 数据、shader、图标 |
| `Database` / `SocketProtocol` / `Scheduler` | README、资源、数据库/Socket/Quartz/SQLite 依赖 |
| `UI.Desktop` / `Solution` | `github-markdown.css`、`aria2c.exe`、AvalonEdit、AvalonDock、WebView2、WPFHexaEditor 依赖 |
| `ImageTools` / `Rbac` | README、多图查看/融合资源、登录和权限窗口资源、数据库依赖 |

缺 native DLL 时，图像/视频链会运行时报错；缺 README 时，插件市场或 DLL 版本窗口很难追溯包来源。

## 引用、签名和版本

| 项 | 说明 |
| --- | --- |
| Engine 源码环境 | 有 UI 源码时走 `ProjectReference` |
| Engine 包环境 | 源码不存在时部分模块回退到 `PackageReference` + `UIProjectPackageVersion` |
| 现场运行 | 最终看主程序输出目录和插件目录里的 DLL |
| 强名称 | `ColorVision.snk` 存在时 `SignAssembly=True`，正式发布不要手动关闭 |
| 版本 | 主要来自各 `.csproj` 的 `VersionPrefix` |

```powershell
rg -n "VersionPrefix|GeneratePackageOnBuild|PackageReadmeFile" UI -g "*.csproj"
```

## 发布后验证

```powershell
dotnet build ColorVision/ColorVision.csproj -c Release -p:Platform=x64
Get-ChildItem ColorVision/bin/x64/Release/net10.0-windows -Filter "ColorVision*.dll"
Get-ChildItem ColorVision/bin/x64/Release/net10.0-windows -Recurse -Filter "opencv*.dll"
```

| 范围 | 验证项 |
| --- | --- |
| 基础 UI | 属性编辑器、菜单、主题切换 |
| 图像 | 普通图片打开、伪彩色、CIE 资源、注释导入导出 |
| 数据/通信 | 数据库浏览器、Socket 管理窗口、调度器窗口 |
| 桌面壳层 | 设置、插件管理器 README/CHANGELOG、市场/下载/WebView2 |
| 应用工具 | 多图查看、图像融合、登录/登出、用户角色权限管理 |
| Engine 包环境 | `dotnet restore` / `dotnet build Engine/ColorVision.Engine/ColorVision.Engine.csproj -c Release -p:Platform=x64` |

## 常见问题

| 现象 | 第一检查点 |
| --- | --- |
| 构建成功但运行缺 DLL | 区分托管 DLL 和 native DLL；检查 `CopyToOutputDirectory`、`PackagePath`、runtime 目录 |
| 插件被跳过 | 主程序目录里的 `ColorVision.*.dll` 版本是否满足插件 `.deps.json` |
| 外部机器不能用 | 是否漏 native runtime、.NET Desktop Runtime、强名称和引用版本是否一致 |
| 只发布上层包后异常 | 底层依赖是否仍是旧版本，尤其 `Common`、`UI`、`ImageEditor`、`Database` |
