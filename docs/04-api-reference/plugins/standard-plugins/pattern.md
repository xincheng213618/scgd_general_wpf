---
knowledge_id: "plugins.pattern"
knowledge_type: "topic"
status: "current"
summary: "Pattern 图卡生成、四象限线栅排列/视场、颜色与模板，及 ImageProjector 图片投影；源码同库维护但仍独立构建交付。"
aliases: ["Pattern", "ImageProjector", "图卡生成工具", "图片投影工具", "四象限线栅", "QuadrantGrating", "按数量排列", "按像素排列", "PatternBrushPropertiesEditor", "PatternHostCopy"]
code_paths: ["Plugins/Pattern", "Plugins/ImageProjector", "scgd_general_wpf.sln"]
test_paths: ["Test/Pattern.Tests/Pattern.Tests.csproj", "Test/Pattern.Tests/PatternMigrationTests.cs"]
related: ["plugins.index", "plugins.capabilities", "plugins.getting-started", "ui.property-grid", "ui.image-editor"]
---

# 图卡生成与图片投影

`Plugins/Pattern` 生成测试图卡，`Plugins/ImageProjector` 将图片显示到指定显示器。两个模块在主仓库维护，通过项目引用使用当前 `ColorVision.UI` / `ColorVision.ImageEditor`，保留各自程序集、插件 ID、版本及独立 WPF App；Pattern 同时引用 ImageProjector。

它们进入开发解决方案 `scgd_general_wpf.sln`，不进入主发布 `build.sln`。代码位于同一仓库不表示已安装、已发布或可以脱离共享依赖单独运行；普通构建也不会自动将它们加入主程序输出或安装包。

## 图卡入口与生成

宿主“工具 → 图卡生成工具”由 `ExportTestPatternWpf` 提供，另有 `PatternFeatureLauncher`；独立 App 启动 `PatternWindow`。`PatternManager` 在已装载程序集中发现非抽象 `IPattern` 实现，使用 `DisplayName` / `Description` / `Category` 元数据，`Browsable(false)` 类型不列入。

当前源码有 11 类图案：纯色、隔行点亮、环形、线对 MTF、九点、点阵、十字网格、十字、棋盘格、噪声和四象限线栅。增加图案沿用 `IPatternBase<T>` 与属性编辑元数据；不要按历史文档中的 MTF/SFR 名称假定存在独立类。

`Gen(height, width)` 的参数顺序先高后宽，返回调用方负责释放的 OpenCV `Mat`。窗口提供常用分辨率与宽高输入、生成预览和保存；支持 BMP 1/4/8/24 位、PNG、JPEG、TIFF。索引 BMP 路径先转灰度并量化，不能当作彩色原样保存。像素尺寸、缩放方式和显示器输出也不能替代真实光学测量。

### 四象限线栅

`PatternQuadrantGrating` 在相邻单元格之间交替绘制水平/垂直线栅，左上格为水平。名称保留“四象限”，但排列并不局限于四格。

| 参数 | 默认 | 行为 |
| --- | --- | --- |
| `LineWidth` | 2 像素 | 线条和间隔等宽，每格从间隔颜色开始；最小按 1 处理 |
| `LayoutMode` | `ByGridCount` | `Columns` / `Rows` 默认 2×2，整数边界分配整个视场，不留下余边 |
| `ByCellSize` | 单元格 320×240 | 使用 `CellWidth` / `CellHeight`，右侧和底部的不足一格区域仍绘制 |
| `SizeMode` | `ByFieldOfView` | 视场系数 X/Y 默认 1，分别限定在 0–1，结果尺寸至少 1 像素 |
| `ByPixelSize` | 640×480 | 使用 `PixelWidth` / `PixelHeight`，限定在输出尺寸内 |
| `MainBrush` / `AltBrush` | 黑 / 白 | 线条与间隔颜色 |
| `BackGroundBrush` | 黑 | 视场外背景；较小视场居中，奇数差值的余量落在右/下侧 |

排列模式与视场尺寸模式相互独立，相关属性按 `PropertyVisibility` 显示。旧 JSON 未记录行列参数时仍使用默认 2×2。生成前拒绝非正图像宽高；配置行列数、单元格尺寸和线宽会按当前视场约束。

### 颜色与配置

`PatternBrushPropertiesEditor` 提供选色器和 R/G/B/W/K 按钮。G 对应 `Colors.Lime`；`ToColorTag` 将标准色编码为字母，其余为 ARGB 十六进制字符串。派生 Tag 属性不显示在属性表中，旧 JSON 的 Tag 字段不会覆盖当前颜色。

配置仍按原类型全名由 `ConfigService` 管理。`PatternManagerConfig.PatternPath` 默认是用户文档的 `ColorVision\Pattern`；生成目录默认是桌面的 `Pattern`；`PatternUserDefaultManager` 固定使用文档 `ColorVision\Pattern\UserDefaults`，按图案类型全名保存 JSON。源码目录收回不改变这些路径、类型或文件名。

独立启动保留 `Pattern` / `ImageProjector` 入口程序集名和 `Company=ColorVision`。共享 `ConfigHandler` 仍优先使用运行工作目录下已有的 `Config` 文件夹，否则写入 `%APPDATA%\ColorVision\Config`；在主宿主中运行时使用宿主的配置入口。切换启动工作目录或宿主/独立运行方式，可能改变实际配置文件选择，不能误诊为迁入丢失配置。

导入 ZIP 会先删除现有模板目录，再解压，并非原子合并；清空模板/生成目录也会删除内容。`UserDefaults` 位于默认模板目录内，可能一起受影响。迁入保留该既有行为；导入/清空前必须备份，不应在普通烟测中执行。

## 图片投影

宿主“工具 → 图片投影工具”由 `MenuImageProjector` 提供；Pattern 通过 `OpenImageProjectorCommand` 打开同一个 `ImageProjectorWindow`，不是复制另一份投影实现。独立 App 同样启动该窗口。

图片列表支持添加、删除、排序和预览。配置保存图片列表、上次选中索引、显示器名称和 `ImageStretchMode`：适应为 `Uniform`、拉伸为 `Fill`、居中为 `None`、填充为 `UniformToFill`。投影中可切换图片，Esc 关闭全屏窗口。

`FullscreenImageWindow` 依据目标屏幕 bounds 与 DPI 布置窗口；它直接操作显示器上的窗口，不是离屏渲染。多屏混合 DPI、屏幕断开重连和实际投影尺寸需要真实显示器验收，单元测试不能证明这些场景。

## 构建、独立运行与交付

前提是 Windows x64、.NET 10 SDK 及当前仓库的 UI/native 依赖。两个项目使用 `VersionPrefix` 保留独立版本，仓库签名 key 存在时条件签名；不通过本机旧 NuGet 缓存决定共享 UI ABI。两个 manifest 的 `requires` 声明最低目标宿主 `1.4.14.1`，用于当前源码引用的交付边界；部分加载入口并不强制拦截该字段，不能以其存在代替实际宿主运行验证。后续正式发布仍须按各插件版本流程升版。

以下命令在仓库根运行，只构建项目输出和项目引用，不上传：

```powershell
dotnet build .\Plugins\Pattern\Pattern.csproj -c Release -p:Platform=x64
dotnet build .\Plugins\ImageProjector\ImageProjector.csproj -c Release -p:Platform=x64
dotnet test .\Test\Pattern.Tests\Pattern.Tests.csproj -c Release -p:Platform=x64
```

完整本地输出包含可启动的 `Pattern.exe` / `ImageProjector.exe`、runtimeconfig 和依赖。启动应用会读取/可能保存用户配置并打开窗口，需使用预期工作目录；测试投影还需确认目标屏幕。`.cvxp` 会剥离宿主共享文件，不是上述完整输出的替代品。

普通构建不执行 HostCopy。需要接入开发宿主时，显式提供有效 `SolutionDir` 和 `EnablePatternHostCopy=true` 或 `EnableImageProjectorHostCopy=true`。这会写入该目录下当前 `Configuration` 的 `ColorVision/bin/x64/<Configuration>/net10.0-windows/Plugins/<Id>`，不双写 Debug/Release；调试期间写入的插件可能被后续发布输出收集，正式发布前应核对目录。

Pattern 的 opt-in target 仅复制 `Pattern.dll`、私有 `ImageProjector.dll`、两者卫星资源和 Pattern 自身 manifest/README/CHANGELOG；ImageProjector 的 target 复制自己的 DLL、卫星资源和元数据。它们不复制整个依赖目录，也不替换宿主的 UI/native DLL。`RestorePatternPackageMetadata` 保证引用项目输出的同名 manifest 不会覆盖 Pattern 包身份。

正式发布仍是单独动作，需用户明确授权后在根目录运行 `Scripts\package_plugin.bat Pattern` 或 `Scripts\package_plugin.bat ImageProjector`；wrapper 会构建和上传。不得把 `.cvxp` 生成成功当作现场加载/投影成功。共享清单、包版本和安装验收统一见[插件产物与交付](../../../02-developer-guide/plugin-development/getting-started.md)。

## 验证范围

`PatternMigrationTests` 覆盖默认 2×2 像素图、按数量/按像素分格、余边、两种视场模式与颜色、旧 JSON、当前属性编辑器及快选按钮、程序集/入口/配置身份和默认目录。测试不启动两个完整 App，不导入/清空模板，不写用户配置，不投影，不上传。

本地构建和测试只验证代码与当前依赖。真实宿主发现菜单、不同版本 ABI、独立 App 生命周期、多屏 DPI、图卡导出后的光学效果仍是独立验收边界。
