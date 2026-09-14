---
knowledge_id: "plugins.pattern"
knowledge_type: "topic"
status: "current"
summary: "Pattern 图卡、用户默认值、模板文件管理，以及内置图片投影的预览和全屏切换；区分当前参数、已生成图片和实际投影。"
aliases: ["Pattern", "ImageProjector", "图卡生成工具", "图片投影工具", "四象限线栅", "QuadrantGrating", "按数量排列", "按像素排列", "PatternBrushPropertiesEditor", "PatternHostCopy", "PatternUserDefaultManager", "保存到默认", "重置默认配置", "UserDefaults", "生成所有模板图片", "清空模板列表", "投影图片不切换", "ImageStretchMode", "FullscreenImageWindow"]
code_paths: ["Plugins/Pattern", "scgd_general_wpf.sln"]
test_paths: ["Test/Pattern.Tests/Pattern.Tests.csproj", "Test/Pattern.Tests/PatternMigrationTests.cs"]
related: ["plugins.index", "plugins.capabilities", "plugins.getting-started", "ui.property-grid", "ui.image-editor"]
---

# 图卡生成与图片投影

`Plugins/Pattern` 生成测试图卡，并通过内部 `Projection/` 功能区将图片显示到指定显示器。图卡生成和图片投影使用同一个 `Pattern` 程序集、插件 ID、版本、WPF App 与发布包；投影源码仍保留独立窗口、配置和 `ImageProjector` 命名空间，避免与图卡生成代码混杂并兼容已有配置节名称。

Pattern 进入开发解决方案 `scgd_general_wpf.sln`，不进入主发布 `build.sln`。代码位于主仓库不表示插件已安装或发布；普通构建也不会自动将它加入主程序输出或安装包。

## 图卡入口与生成

宿主“工具 → 图卡生成工具”由 `ExportTestPatternWpf` 提供，另有 `PatternFeatureLauncher`；独立 App 启动 `PatternWindow`。`PatternManager` 在已装载程序集中发现非抽象 `IPattern` 实现，使用 `DisplayName` / `Description` / `Category` 元数据，`Browsable(false)` 类型不列入。

当前源码有 11 类图案：纯色、隔行点亮、环形、线对 MTF、九点、点阵、十字网格、十字、棋盘格、噪声和四象限线栅。增加图案沿用 `IPatternBase<T>` 与属性编辑元数据；不要按历史文档中的 MTF/SFR 名称假定存在独立类。

界面语言跟随主程序，仅交付简体中文默认资源、英文和繁体中文。未生成图片时只显示模板库和图卡参数，生成后向右展开纯图像预览；正常窗口按可用图像高度乘以实际图片宽高比调整宽度，超出当前屏幕工作区时等比缩小。修改尚未生成的尺寸不会改变现有预览，清除图像后收回预览区；最大化时保持窗口状态，在剩余区域内等比显示。模板库边界可拖动，模板名称在可用宽度内省略，悬停显示完整名称；搜索不限于短关键词。模板库菜单集中提供目录、导入、导出和清空入口，批量生成位于列表底部；参数菜单提供重置、保存默认值、完整属性窗口和重置默认配置。宽高与常用分辨率双向同步，不匹配预设时清除预设选择。宽高与预设使用紧凑单行。“保存到模板”“生成图卡”以及图片格式、“另存为”和输出目录菜单固定在参数区底部，上方参数独立滚动，切换图案类型不改变操作栏位置，尚未生成图片时“另存为”禁用。投影工具位于参数区顶部，主题和配置位于模板库顶部。

`Gen(height, width)` 的参数顺序先高后宽，返回调用方负责释放的 OpenCV `Mat`。选择图案、设置宽高和参数后点击“生成图卡”，再保存图片；修改参数或点击重置不会自动更新已生成的 `currentMat`，保存使用的是这份图片，需先重新生成。

支持 BMP 1/4/8/24 位、PNG、JPEG、TIFF。格式由窗口的格式配置决定，保存对话框的扩展名选择不会反向修改该配置；索引 BMP 路径先转灰度并量化，不能当作彩色原样保存。单张保存对话框的建议位置使用模板路径；“图卡生成路径”用于批量输出。像素尺寸、缩放方式和显示器输出不能替代真实光学测量。

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

颜色快选直接更新当前配置；选色窗口只有确认才复制所选颜色，取消或关闭不写回。Pattern 打开的通用属性窗口使用 `Immediate` 模式，字段编辑直接作用于传入对象；关闭不是整轮撤销。通用“重置”和“恢复到默认”的含义见[属性编辑会话](../../ui-components/property-grid.md#编辑与持久化不是同一动作)。

### 用户默认值与当前参数

图案实例的 `IPatternBase<T>.Config` 初始为 `new T()`，并不是 `ConfigService` 中按类型查出的对象。管理器保留图案实例；切换图案只更换编辑器，不读取用户默认文件。当前参数、用户默认 JSON 和模板 JSON 是三种不同内容。

| 入口 | 读取或写入 | 图片预览 |
| --- | --- | --- |
| 保存到默认 | 把当前图案配置直接写入该类型的用户默认 JSON；不包含窗口宽高 | 不重新生成 |
| 重置 | 有可读取的用户默认文本时调用 `SetConfig`；不存在或读取失败时采用新图案的类默认配置 | 重建参数编辑器，不重新生成 |
| 重置默认配置 | 先采用新图案的类默认配置，再覆盖该类型的用户默认 JSON；不是删除默认文件 | 重建参数编辑器，不重新生成 |
| 属性窗口“恢复到默认” | 使用可构造的类默认对象更新当前编辑对象；不读写用户默认文件 | 不重新生成 |
| 保存到模板 | 保存图案类型稳定 ID、窗口宽高及配置 JSON，供模板列表选择 | 不重新生成 |

保存默认后，在所需图案上点击“重置”才会载入它；不能承诺下次选择或启动会自动应用。`LoadUserDefault` 只读文本，读取失败记日志并返回 null；文本存在但 JSON 损坏时，`SetConfig` 异常进入“重置失败”，没有第二次回退出厂值。保留损坏文件并核对格式；如要取消某类型的自定义默认，可在确认类型并备份后移除对应文件。保存一份类默认 JSON 与移除文件不同：以后类默认改变时，保存的旧 JSON 仍会参与重置。

默认文件固定在用户文档的 `ColorVision\Pattern\UserDefaults`，不随 `PatternPath` 改动。文件名由图案类型全名生成，非法字符处理后截到 200 字符再加 `.json`；内置类型可分别保存，但不能据此保证任意扩展类型永不重名。首次解析该目录会尝试创建它。写入不是临时文件原子替换，失败会向保存入口报错，不保证保留旧默认内容。

### 模板、目录与持久化

`PatternManagerConfig`、窗口宽高 `PatternWindowConfig` 和投影配置由 `ConfigService` 管理。独立入口程序集名为 `Pattern`、`Company=ColorVision`；共享 `ConfigHandler` 优先使用运行工作目录下已有的 `Config` 文件夹，否则使用 `%APPDATA%\ColorVision\Config`。宿主运行使用宿主入口配置，切换工作目录或运行方式可能选中不同配置文件。投影配置仍使用完整类型名 `ImageProjector.ImageProjectorConfig`，合并程序集不会改变已有配置节名称。

`PatternPath` 默认是用户文档的 `ColorVision\Pattern`，批量生成目录默认是桌面的 `Pattern`。模板只从模板目录顶层 `.json` 文件加载，新模板的 `PatternName` 保存图案类型稳定 ID，加载仍兼容旧版中文 `DisplayName` 和类型名，再应用宽高和配置；切换语言不影响模板识别。`IsSwitchCreate` 默认 true，控制选择模板后是否生成图像，不控制普通图案切换或用户默认加载。

“生成所有模板图片”遍历完整模板集合，不限于搜索结果，按模板文件名写到生成目录；异常记日志后继续，没有逐项成功汇总。`SetTemplatePattern` 自身捕获错误，外层批量循环仍可能用先前参数继续生成，不能把输出目录打开或有文件当作全部模板应用成功。核对每项模板、输出和日志后再使用批量结果。

| 文件操作 | 实际范围与失败边界 |
| --- | --- |
| 导出 ZIP | 打包整个模板目录及子目录，默认位置下包括 `UserDefaults`；已有目标 ZIP 先删除 |
| 导入 ZIP | 先递归删除当前模板目录，再解压；失败无原子恢复，可能只留下部分内容 |
| 清空模板列表 | 工具栏提示虽为“列表”，命令会递归删除模板目录并重建，没有确认分支 |
| 清空输出目录 | 确认后递归删除配置的生成目录并重建 |
| 模板删除/重命名 | 直接删除或移动文件，不是只改列表 |

列表使用 `ListCollectionView` 筛选；复制、删除与重命名按当前选中的模板对象定位文件，搜索结果下标不再用于索引原集合。导入和清空前应备份实际配置目录，默认目录内的用户默认文件也可能受影响。

## 图片投影

宿主“工具 → 图片投影工具”由 Pattern 程序集中的 `MenuImageProjector` 提供；图卡窗口通过 `OpenImageProjectorCommand` 打开同一个 `ImageProjectorWindow`，不是复制另一份投影实现。独立 `Pattern.exe` 可先进入图卡窗口，再从投影入口打开该窗口；不再提供独立 `ImageProjector.exe`。

1. 添加图片并核对预览。列表保存的是文件路径，不复制图片；移除列表项不删除原文件。
2. 选择目标显示器与显示模式。默认优先第一个非主屏，否则第一个屏幕；保存的显示器名称仍存在时恢复该选择。
3. 点击投影，在所选屏幕创建全屏窗口；再次投影会关闭本窗口已有的全屏实例并重建。
4. 投影中的“上一张/下一张”会更新全屏图片，到列表边界不循环。直接选中列表项只更新预览；更改显示器只影响下一次创建的全屏窗口，不迁移当前投影。
5. 用“停止”、全屏窗口中的 Esc，或关闭控制窗口结束投影。清空列表不会自动关闭已有全屏窗口。

| 显示模式 | WPF Stretch | 效果 |
| --- | --- | --- |
| 适应 | `Uniform` | 保持比例完整显示，可能留边 |
| 拉伸 | `Fill` | 填满目标，可改变比例 |
| 居中 | `None` | 不缩放；受图片 DPI 与 WPF 布局影响，不承诺设备像素一比一 |
| 填充 | `UniformToFill` | 保持比例铺满，可能裁剪 |

改变显示模式会同步现有全屏窗口。列表、选中索引、显示器名称和模式通过 `ConfigService.SaveConfigs()` 尝试保存；捕获到的异常写日志，界面仍可继续使用，不保证保存成功。多个控制窗口共用配置集合，但各自持有预览和全屏实例。

**加载失败可能留下旧图：** `LoadImage` 在文件缺失或解码失败时提示错误，但不清除先前的 `_currentImage`；选中项已经变化不证明预览/全屏已经加载新文件。先核对预览与错误，再重新投影，不把按钮可用或状态文字当成图片一致性的验收。

`FullscreenImageWindow` 按目标屏幕 bounds 与 DPI 布置真实窗口。多屏混合 DPI、屏幕断开重连和实际投影尺寸需真实显示器验收；当前控制窗口没有屏幕变化的自动重载流程。

## 构建、独立运行与交付

前提是 Windows x64、.NET 10 SDK 及当前仓库的 UI/native 依赖。图卡和投影共用 `Pattern.csproj` 的 `VersionPrefix`，仓库签名 key 存在时条件签名；不通过本机旧 NuGet 缓存决定共享 UI ABI。Pattern manifest 的 `requires` 声明最低目标宿主 `1.4.14.1`，用于当前源码引用的交付边界；部分加载入口并不强制拦截该字段，不能以其存在代替实际宿主运行验证。后续正式发布按 Pattern 插件版本流程升版。

以下命令在仓库根运行，只构建项目输出和项目引用，不上传：

```powershell
dotnet build .\Plugins\Pattern\Pattern.csproj -c Release -p:Platform=x64
dotnet test .\Test\Pattern.Tests\Pattern.Tests.csproj -c Release -p:Platform=x64
```

完整本地输出包含可启动的 `Pattern.exe`、runtimeconfig 和依赖；投影实现编译进 `Pattern.dll`，不再生成 `ImageProjector.dll` 或 `ImageProjector.exe`。启动应用会读取/可能保存用户配置并打开窗口，需使用预期工作目录；测试投影还需确认目标屏幕。`.cvxp` 会剥离宿主共享文件，不是上述完整输出的替代品。

普通构建不执行 HostCopy。需要接入开发宿主时，显式提供有效 `SolutionDir` 和 `EnablePatternHostCopy=true`。这会写入该目录下当前 `Configuration` 的 `ColorVision/bin/x64/<Configuration>/net10.0-windows/Plugins/Pattern`，不双写 Debug/Release；调试期间写入的插件可能被后续发布输出收集，正式发布前应核对目录。

Pattern 的 opt-in target 仅复制包含图卡和投影实现的 `Pattern.dll`、Pattern 卫星资源及自身 manifest/README/CHANGELOG；不复制整个依赖目录，也不替换宿主的 UI/native DLL。`RestorePatternPackageMetadata` 保证输出保留 Pattern 包身份。

正式发布仍是单独动作，需用户明确授权后在根目录运行 `Scripts\package_plugin.bat Pattern`；wrapper 会构建和上传。不得把 `.cvxp` 生成成功当作现场加载/投影成功。共享清单、包版本和安装验收统一见[插件产物与交付](../../../02-developer-guide/plugin-development/getting-started.md)。从旧版独立 ImageProjector 升级时还应移除宿主的旧 `Plugins/ImageProjector` 目录，避免旧程序集继续提供重复菜单。

## 验证范围

`PatternMigrationTests` 覆盖默认 2×2 像素图、按数量/按像素分格、余边、两种视场模式与颜色、旧 JSON、当前属性编辑器及快选按钮、合并后的程序集/入口/配置身份、投影资源和默认目录。测试不启动完整 App，不导入/清空模板，不写用户配置，不投影，不上传；不覆盖用户默认损坏、模板筛选后的文件操作、批量失败后继续生成或投影预览同步。

本地构建和测试只验证代码与当前依赖。真实宿主发现两个菜单、旧独立插件清理、多屏 DPI、图卡导出后的光学效果仍是独立验收边界。
