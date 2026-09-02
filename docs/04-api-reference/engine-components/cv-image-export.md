---
knowledge_id: "engine.cv-image-export"
knowledge_type: "topic"
status: "current"
summary: "CVRAW/CVCIE 原生导出的窗口、命令行参数、通道和命名规则，以及覆盖、部分失败和退出码边界。"
aliases: ["CVRAW转TIFF", "CVCIE导出", "ColorVision命令行导出", "Python批量转换CVRAW", "导出返回0但没有文件", "CVRawFileExporter", "VExportCIE", "ExportCVCIE", "SaveToTif"]
code_paths: ["Engine/ColorVision.Engine/Media/FileProcessorCVRaw.cs", "Engine/ColorVision.Engine/Media/FileCVCIE.cs", "Engine/ColorVision.Engine/Media/Export/VExportCIE.cs", "Engine/ColorVision.Engine/Media/Export/ExportCVCIE.xaml", "Engine/ColorVision.Engine/Media/Export/ExportCVCIE.xaml.cs", "UI/ColorVision.UI/FileProcessorFactory.cs", "UI/ColorVision.UI/Shell/ArgumentParser.cs", "ColorVision/App.xaml.cs", "ColorVision/Copilot/Skills/colorvision-batch-image-conversion"]
test_paths: ["Test/ColorVision.UI.Tests/ExportCieTests.cs"]
related: ["engine.file-io", "ui.image-editor", "algorithms.platform", "copilot.skills"]
---

# CVRAW / CVCIE 图像导出

原生导出将 CVRAW 原图或 CVCIE 的选定通道写为普通图像，由 `CVRawFileExporter`、`ExportCVCIE` 和 `VExportCIE` 实现。它与 [Copilot 批量图像工具](../../02-developer-guide/core-concepts/image-algorithm-platform-v1.md#copilot-批量图像工具)使用不同的输出路径；批量工具的一源一图和同名编号规则不能套用到这里。专有文件解码与关联源图的读取边界见 [FileIO](./ColorVision.FileIO.md)。

## 使用导出窗口

在解决方案中选中存在的 `.cvraw` 或 `.cvcie` 文件，使用右键“导出”；已打开的 CV 图像也提供“导出”入口。窗口用于处理单个文件：

1. 核对源文件、尺寸、位深与通道数。
2. CVCIE 可选择 Y；三通道文件另可选择 X、Z，关联原图可读取时才显示“原图”。CVRAW 直接导出原图。
3. 设置名称、输出目录和格式。CVRAW 提供 TIFF、PNG、JPEG，CVCIE 窗口提供 TIFF；TIFF 可选 LZW 或 ZIP。PNG 使用自动压缩，JPEG 使用质量 100。
4. 点击导出。工作在后台执行，期间表单和按钮禁用；成功提示后关闭窗口，异常提示后保留窗口。进度条不表示通道完成比例。

名称默认取源文件主干，可在窗口中修改。目录可新建，最近导出位置通过 `recent-image-export-locations.json` 保存。导出会直接写目标文件，没有同名覆盖确认或整组回滚；需要保留已有文件时选择新的空目录。

## 命令行导出

使用已安装、具备对应 Engine/FileIO 与原生图像运行依赖的 ColorVision。命令会启动应用的初始化链并写输出文件，不是纯读取或独立解码器；`-q` 也不保证启动过程的所有错误都无弹窗。

以下是从 PowerShell 调用单个文件的参数形状。替换实际路径后才执行；自动化包装器还需设置进程超时并收集实际输出证据。

```powershell
$colorVisionExe = 'C:\Program Files\ColorVision Inc\ColorVision\ColorVision.exe'
& $colorVisionExe -e 'C:\Images\sample.cvraw' -o 'C:\Exports\sample-run' -q -t tif -mx 5
```

| 参数 | 含义与默认值 |
| --- | --- |
| `-e` / `--export` | 一个现存 `.cvraw` 或 `.cvcie` 文件；不是目录或通配符批量入口 |
| `-o` / `--output` | 输出目录，按需创建；未提供时使用源文件目录 |
| `-q` / `--quiet` | 直接执行导出并退出；未提供时打开导出窗口 |
| `-t` / `--type` | 精确小写 `tif`、`png`、`jpg`；输出后缀分别为 `.tiff`、`.png`、`.jpg`；省略或其它值回退 TIFF |
| `-mx` / `--mx` | 当前编码器的压缩/质量值，见下表；非整数值不覆盖默认设置 |

| 编码器 | `-mx` 行为 |
| --- | --- |
| TIFF | 默认 5（LZW），8 为 ZIP；0 映射回 5，其它整数传给编码器，CLI 不限于窗口的两项 |
| PNG | 默认自动；显式值限制在 0–9。需要自动时省略参数：解析器不会把以 `-` 开头的下一个参数当作数值，因此不要依赖 `-mx -1` |
| JPEG | 默认 100，显式值限制在 0–100 |

格式值与 Copilot 工具的 `tiff` / `jpeg` 枚举不同。不要把 `-t jpeg` 当作 JPEG：它会回退 TIFF。CVCIE 的静默 CLI 可以传 PNG/JPEG，但窗口只提供 TIFF；编码器是否能接收相应位深和通道需看实际结果。需要保留浮点测量通道时使用 TIFF；JPEG 路径转换为 8 位，不能作为原始测量数值的保存格式。

## 通道与输出名称

CLI 不提供自定义名称或通道选择参数。默认名称为源文件主干，默认导出原图；CVCIE 另默认选中 X、Y、Z，但只写文件实际具有的通道。输出器先将 `Name` 与下列后缀组合，再调用 `Path.ChangeExtension` 设置格式后缀。

| 源内容 | 文件名示例，默认名称为 `sample` |
| --- | --- |
| CVRAW 原图 | `sampleSrc.tiff` |
| CVCIE 三通道 | `sample_X.tiff`、`sample_Y.tiff`、`sample_Z.tiff` |
| CVCIE 单通道 | `sample_Y.tiff` |
| CVCIE 可用的关联原图 | 另写 `sample_Src.tiff` |

带点的名称会被 `Path.ChangeExtension` 再次解释为含扩展名，可能缩短名称甚至让不同通道落到同一路径。窗口可使用不含点的名称；CLI 包装器必须检查实际文件和所需通道，不能仅凭预计文件名或退出码判定完整。当前实现未提供通道输出冲突检测。

CVCIE 关联原图不存在或不能读取时可跳过原图，继续导出可用内嵌通道；只有一个输出成功也可能满足内部“至少导出一个图像”的条件。需要完整 X/Y/Z 或原图集合时，应逐项确认。多通道处理中发生异常不会撤销已经写出的文件，同名文件也可能已经被覆盖。

## 自动化的完成判据与排障

真正到达 CVRAW/CVCIE 静默分支后，`SaveToTif` 正常返回 0 对应进程退出码 0，捕获导出异常返回 -1 对应退出码 1。但源文件不存在、导出器分派失败或初始化异常可以先返回到 `App.Application_Startup` 的错误路径；该路径显示错误对话框后仍调用 `Environment.Exit(0)`。这是当前退出码契约的缺口。

因此，包装器应枚举明确的输入范围，默认顺序处理，每个源文件、每次运行使用独立空输出目录，并同时记录退出码、超时、非空输出与请求所需通道。`-t tif` 应检查 `.tiff`。超时、非零退出码、无输出或缺少请求通道均记为失败；保留已生成文件用于诊断，不把部分结果算作全部成功。若调用多个进程，应分别隔离输出，避免同名源文件或通道相互覆盖。

| 现象 | 检查位置 |
| --- | --- |
| 返回 0，没有输出或停在弹窗 | 源文件是否存在、导出器是否加载、是否真正进入静默分支；查看应用日志，不能只依赖 stdout/stderr |
| 请求 PNG/JPEG 却生成 TIFF | 检查 `-t` 的精确值与大小写；交互窗口的 CVCIE 格式范围另有限制 |
| 少了原图或 XYZ 通道 | 检查真实通道数、关联源图、选择项、名称中的点和同名覆盖；一张输出不证明整组完成 |
| 只找到 `.tif` 搜索结果为空 | 原生 TIFF 输出扩展名是 `.tiff` |
| 失败后目录仍有文件 | 按实际输出判断部分完成，当前路径没有原子整组提交或自动回滚 |

## 源码与验证

`FileProcessorCVRaw.cs` 负责 CLI 参数和静默退出；`VExportCIE` 负责名称、通道、编码与路径；`ExportCVCIE.xaml.cs` 负责窗口状态和提示；`FileProcessorFactory.TryExportFile` 与 `App.Application_Startup` 决定分派和上层失败语义。随包的 `colorvision-batch-image-conversion` 技能保留脚本执行所需的最小参数与失败规则，不能只依赖未随包交付的仓库文档。

`ExportCieTests` 覆盖目录创建、16 位 RAW 的 TIFF/PNG 像素、选定浮点通道、相对关联原图、JPEG 转换及窗口选项策略。这些测试入口不证明进程退出码、启动弹窗、超时、带点名称冲突或整组输出的实机行为已通过验证；文档核对不需要启动导出、覆盖文件或读取现场样本。
