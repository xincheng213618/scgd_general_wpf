---
knowledge_id: "ui.publishing"
knowledge_type: "guide"
status: "current"
summary: "说明 UI NuGet 构建、版本占用预检、显式 Release 发布与包消费验证。"
aliases: ["如何发布UI NuGet包","UIProjectPackageVersion","algorithms-v","verify_nuget_package_versions.py"]
code_paths: [".github/workflows/dotnet.yml","UI/Directory.Build.props","Scripts/verify_nuget_package_versions.py","UI/ColorVision.Algorithms/ColorVision.Algorithms.csproj"]
test_paths: ["Scripts/tests/test_verify_nuget_package_versions.py","Scripts/tests/test_algorithm_package_contract.py"]
related: ["ui.index","ui.package-boundaries","algorithms.platform"]
---

# UI DLL 发布

本页说明 `UI/` 下 DLL/NuGet 包如何发布、替换和排障。默认发布对象是一组 UI 类库；`ColorVision.Algorithms` 也提供显式的单包 Release 入口。发布记录需保存范围、版本、资源抽检、消费方验证、烟测结果和回退包位置。

## 发布对象

| 分组 | 项目 |
| --- | --- |
| 基础包 | `ColorVision.Common`、`ColorVision.Themes`、`ColorVision.UI` |
| 数据与通信 | `ColorVision.Database`、`ColorVision.SocketProtocol`、`ColorVision.Scheduler` |
| 图像 | `ColorVision.Core`、`ColorVision.Algorithms`、`ColorVision.ImageEditor` |
| 桌面壳层 | `ColorVision.UI.Desktop`、`ColorVision.Solution` |
| 应用工具 | `ColorVision.ImageTools`、`ColorVision.Rbac` |

`ColorVision.UI.Desktop` 是 `WinExe` 且启用打包，维护时要确认它是作为桌面辅助模块还是宿主依赖使用。

## 发布前决策

| 变更范围 | 必须同步验证 |
| --- | --- |
| 窗口样式或 XAML 资源 | 对应 UI 项目和宿主窗口 |
| `Common`、`Themes`、`UI` public 类型 | 所有上层 UI 项目、Engine、关键插件和项目包 |
| `Algorithms` | 同批发布时，先生成并发布中立契约包，再发布依赖它的 `ImageEditor` 包；单包发布仍须验证双目标框架资产和消费方契约 |
| `Core` 或 `ImageEditor` | native runtime、shader、colormap、CIE 数据、OpenCvSharp runtime |
| `Database`、`SocketProtocol`、`Scheduler` | 数据库、Socket、调度器配置和历史库兼容性 |
| `Solution` 或 `UI.Desktop` | 设置、市场、下载、工作区、编辑器、终端、WebView2 |
| `ImageTools` 或 `Rbac` | 多图查看/融合入口、登录态、角色权限、会话和审计 |
| 外部 NuGet 环境 | 显式锁定 `UIProjectPackageVersion`，不要依赖 `*` 自动解析 |

## 构建和包资源

```powershell
dotnet build UI/<Project>/<Project>.csproj -c Release -p:Platform=x64
```

多数 UI 项目设置了 `GeneratePackageOnBuild=True`，Release 构建会生成 `.nupkg` 和 `.snupkg`。实际包路径以构建日志和项目目标框架为准。

普通 `master` / `develop` push 和 pull request 只构建、测试、验包，不发布 NuGet。发布只能由显式发布的 GitHub Release 触发，并且在第一项 `dotnet nuget push` 前使用 `Scripts/verify_nuget_package_versions.py` 检查本次全部 `.nupkg` 的 ID/版本是否未占用。任一版本已存在或远端查询失败都会终止整批发布；发布命令不使用 `--skip-duplicate`，因此不能把“上层包被跳过、下层新包已上传”的半发布当作成功。

单独发布 `ColorVision.Algorithms` 时，创建并发布标签为 `algorithms-v<规范化 NuGet 版本>` 的 GitHub Release，例如 `algorithms-v1.5.8`（对应 `VersionPrefix=1.5.8.0` 生成的包版本）。这一入口仍先运行同一工作流的全部构建、Python 包契约、UI 和其他既有测试，再校验包内 ID/版本与标签完全一致，并对精确路径 `UI/ColorVision.Algorithms/bin/x64/Release/ColorVision.Algorithms.<版本>.nupkg` 执行版本占用预检和上传；不会发布 `ImageEditor` 或其他包。`algorithms-v` 前缀的无效标签会失败，不会回退为整批发布。其他 Release 标签继续走原有整批预检和发布，不能用单包入口绕过测试或覆盖已占用的版本。

`Scripts/tests/test_algorithm_package_contract.py` 的“clean”含义是：所有托管输出、原生输出、包、consumer restore cache 都在测试临时目录生成。测试先通过 Visual Studio MSBuild 和 `Microsoft.VisualStudio.Component.VC.Tools.x86.x64` 工作负载把 `opencv_helper.dll` 构建到隔离目录，验证它是非空 PE，再把该精确路径传给 ImageEditor pack；不会读取仓库 `bin/`、`x64/Release` 或 `Native/opencv_helper/x64/Release` 中的残留 DLL。`ColorVision.Algorithms` 的双 TFM pack/consumer 本身仍是纯托管步骤。缺少 C++ 工作负载时该 clean package 测试应明确失败，不能用零字节或陈旧 DLL 跳过门禁。CI 先配置 VS MSBuild、构建 Release|x64 solution，再运行 Python 门禁；package-contract 仍自行重建隔离 native，以证明测试不依赖前一步恰好留下的文件。

| 模块 | 必须检查 |
| --- | --- |
| `Common` / `Themes` / `UI` | README、共享接口、主题资源、图标、菜单、插件、属性编辑器资源 |
| `Algorithms` | README、`lib/net8.0`、`lib/net10.0`，且不得引入 WPF/OpenCvSharp/native/Flow 依赖 |
| `Core` / `ImageEditor` | `opencv_helper.dll`、OpenCV runtime、伪彩图、CIE 数据、shader、图标，以及对 `ColorVision.Algorithms` 的 NuGet 依赖 |
| `Database` / `SocketProtocol` / `Scheduler` | README、资源、数据库/Socket/Quartz/SQLite 依赖 |
| `UI.Desktop` / `Solution` | `github-markdown.css`、`aria2c.exe`、AvalonEdit、AvalonDock、WebView2、WPFHexaEditor 依赖 |
| `ImageTools` / `Rbac` | README、多图查看/融合资源、登录和权限窗口资源、数据库依赖 |

缺 native DLL 时，图像/视频链会运行时报错；缺 README 时，插件市场或 DLL 版本窗口很难追溯包来源。

## 引用、签名和版本

| 项 | 说明 |
| --- | --- |
| Engine 源码环境 | 有 UI 源码时走 `ProjectReference` |
| Engine 包环境 | 源码不存在时部分模块回退到 `PackageReference` + `UIProjectPackageVersion` |
| 图像包顺序 | `ColorVision.Algorithms` 必须先于依赖它的 `ColorVision.ImageEditor` 发布 |
| 现场运行 | 最终看主程序输出目录和插件目录里的 DLL |
| 强名称 | `ColorVision.snk` 存在时 `SignAssembly=True`，正式发布不要手动关闭 |
| 版本 | 多数 UI 包继承 `UI/Directory.Build.props` 的 `VersionPrefix`，`ColorVision.Themes` 在自己的 `.csproj` 中另有版本覆写；以各项目最终版本为准。整批 Release 前，所有待发布包的版本都必须未占用并完成重建；Algorithms 单包入口只要求该包的版本未占用，不要求提升其他包版本。包版本调整不要求改变强名 `AssemblyVersion`，除非另有明确的 ABI 策略。 |

```powershell
rg -n "VersionPrefix|GeneratePackageOnBuild|PackageReadmeFile" UI -g "*.csproj"
python Scripts/verify_nuget_package_versions.py "UI/*/bin/x64/Release/*.nupkg" "Engine/ColorVision.FileIO/bin/Release/*.nupkg" "Engine/cvColorVision/bin/x64/Release/*.nupkg"
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

## 验证入口与缺口

关联测试：`Scripts/tests/test_verify_nuget_package_versions.py`、`Scripts/tests/test_algorithm_package_contract.py`。

包契约可本地检查，GitHub Release 和 NuGet 上传是外部写入；未收到发布授权时不得触发，也不能把静态文档核对写成包发布成功。
