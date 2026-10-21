---
knowledge_id: "plugins.spectrum"
knowledge_type: "topic"
status: "current"
summary: "光谱仪软件 Spectrum 的连接、标定、单次测量和 CSV 导出；标定状态与测量前文件复核、EQE 输入及独立 ZIP/cvxp 发布版本来源。"
aliases: ["HoleMapping","孔位映射","编辑滤光轮配置","Spectrum 如何校准和发布","光谱测量结果不一致","Spectrum","Spectrum.bat","SpectrometerManager","SpectrumMeasurementResult","ViewResultManagerConfig","ViewResultSpectrum","SpectrumMeasurementProfile","光谱仪软件","连接光谱仪","单次测试","IsCalibrationReady","设备序列号未知，无法保存标定配置"]
code_paths: ["Plugins/Spectrum/README.md","Plugins/Spectrum/Spectrum.csproj","Plugins/Spectrum/manifest.json","Plugins/Spectrum/App.xaml.cs","Plugins/Spectrum/MainWindow.xaml.cs","Plugins/Spectrum/MainWindow.Chrome.cs","Plugins/Spectrum/MainWindow.PlotTheme.cs","Plugins/Spectrum/MainWindowConfig.cs","Plugins/Spectrum/Layout/","Plugins/Spectrum/MainWindow.xaml","Plugins/Spectrum/Properties/Resources.resx","Plugins/Spectrum/SpectrometerManager.cs","Plugins/Spectrum/Calibration/","Plugins/Spectrum/Configs/","Plugins/Spectrum/PropertyEditor/","Plugins/Spectrum/Data/","Plugins/Spectrum/Models/ViewResultSpectrum.cs","Plugins/Spectrum/SpectrumCsvExporter.cs","Plugins/Spectrum/DirectSpectrometer/","Plugins/Spectrum/Job/","Plugins/Spectrum/License/","Plugins/Spectrum/Update/","Scripts/Spectrum.bat","Scripts/build_spectrum.py"]
test_paths: ["Test/Spectrum.Tests/Spectrum.Tests.csproj","Test/Spectrum.Tests/FilterWheelHoleMappingEditSessionTests.cs","Test/Spectrum.Tests/ViewResultSpectrumTests.cs","Test/Spectrum.Tests/SpectrumCalibrationStateTests.cs","Test/Spectrum.Tests/SpectrumCsvExporterTests.cs","Scripts/tests/test_build_spectrum.py"]
related: ["plugins.index","plugins.capabilities","plugins.spectrum-socket","ui.documents","operations.main-window","ui.themes"]
---

# Spectrum 插件

Spectrum 提供光谱仪连接、标定分组、光谱测量、EQE 计算和结果导出。在 ColorVision 中从 **工具 → 光谱仪软件** 打开，也可运行完整独立包中的 `Spectrum.exe`；再次点击宿主菜单会激活已有窗口。

Spectrum 窗口的 **帮助 → 关于 Spectrum** 打开光谱展示页 `Help/SpectrumAboutWindow.xaml`。它使用插件自身版本、固定不透明背景和窗口内深浅配色，展示扭转光谱环与粒子，不连接设备。`MenuSpectrumAbout` 继承 `SpectrumMenuIBase`，只加入 Spectrum 菜单。共享渲染、动画暂停与关闭释放约束见[关于窗口展示](../../ui-components/about-exhibition.md)。

运行需要匹配的 Windows/x64 环境、ColorVision 公共库、原生 DLL、设备驱动、许可证和标定文件。项目与依赖以 `Spectrum.csproj`、`Plugins/Directory.Build.props` 为准，最低宿主要求见 `manifest.json`。连接、校零、快门、滤光轮、源表和测量会操作真实设备，以下步骤适用于已获授权的现场环境。

## 连接、测量与导出

1. 在 **光谱仪连接** 区域选择型号和连接方式；使用串口时配置串口与波特率，再点 **连接光谱仪**。确认型号、SN 和连接状态，连接失败先按下表排查。
2. 查看 **标定文件** 区域的当前分组、波长文件、幅值文件和状态。需要配置时点 **管理...**，保存后确认加载结果；已有配置可用 **加载分组** 或文件旁的 **加载** 重载。标定未就绪时先修复配置，不继续测量。
3. 设置 **积分时间 (ms)**、**平均次数** 及所需自动校零、自动积分等选项。自动校零的齿轮窗口可选择 **Shutter** 或 **滤色轮**：滤色轮模式必须配置实际遮光孔位并先连接滤色轮；启用 EQE 时核实电压、电流的实际来源，见下文。
4. 点 **单次测试**，成功后检查结果列表与曲线。后台测量与界面投影异步衔接，返回成功和界面刷新不是同一时刻。
5. 在结果列表选中需要导出的记录，点击列表右上方的保存图标，选择 CSV 路径。没有选中记录会提示先选择数据；导出优先使用记录保存的 Normal/EQE 类型，缺失时才使用当前模式。所选记录全部为普通光谱时不导出电压、电流；包含 EQE 时保留这两列，普通记录对应值留空。

连续测试在同一区域设置间隔与次数；远程调用见 [Spectrum Socket](./spectrum-socket.md)，定时调用见下文“Socket 和调度”。

## 窗口与工作区

Spectrum 使用与主程序相同的现代 AvalonDock 主题，资源与停靠控件由 `ColorVision.Solution/Themes` 统一维护；独立版通过项目引用随包携带该公共库及其传递依赖，不需要安装或启动 ColorVision 主程序。插件模式通过已加载主程序的 `ColorVision;component/Themes/` 资源入口装配主题，保留主程序的兼容资源入口；独立模式读取 `ColorVision.Solution;component/Themes/`。主题装配不直接引用新增的公共主题类型，避免宿主已加载旧版同名程序集时发生类型加载失败。运行时还依赖构建时的共享光谱计算、绘图和分析类型，最低宿主要求以 `manifest.json` 为准；涉及这些共享 API 的插件更新应配套更新主程序。面板的停靠、浮动、自动隐藏和原有布局恢复入口保持可用。

Windows build 22000 或更高版本固定尝试将菜单与原生最小化、最大化和关闭按钮合并到同一行。系统、DWM 或窗口条件不满足时保留原生标题栏。Spectrum 以源码链接复用主程序的 `CompactTitleBarChrome` 与可见性保护逻辑，不加载主程序可执行程序集；全屏时暂停紧凑外观，退出全屏后恢复。

左侧控制分组使用公共分组标题、圆角面板和按钮样式，可单独折叠，折叠不改变设备连接或测量状态。工作区外边距与主程序对齐，控制卡片使用紧凑间距；快门与滤色轮没有错误时折叠提示行，保留原有按钮尺寸和连接入口。默认控制区宽度为 360 DIP，底部日志高度为 180 DIP；已有保存布局继续恢复，使用重置布局才应用新的默认尺寸。曲线与结果列表之间的分隔条直接调整可用空间比例，结果工具栏为右侧操作预留独立列，查询等按钮在宽度不足时进入工具栏溢出菜单。

相对和绝对光谱使用纯白或近黑背景，坐标、网格与图例跟随全局深浅主题；换主题只更新现有绘图颜色，不重建曲线、重置坐标范围或清除选择。单条结果把 380–780 nm 可见范围按波长颜色填充到曲线与零基线之间，并叠加与背景反差明确的轮廓线；可见范围以外仍保留测量曲线但不伪造显示色。多结果比较保持折线显示，避免彩色填充相互遮挡。颜色只用于帮助识别波段，不改变相对/绝对光谱数值和后续计算。外观事件在窗口关闭时解除，设备、标定、数据库和测量生命周期仍由各自原有入口管理。

## 滤光轮孔位映射

在滤色轮设置的 PropertyGrid 中，`HoleMapping` 显示孔位数量，右侧 **编辑** 打开“编辑滤光轮配置”。窗口逐行编辑孔位索引和名称，支持添加、删除及滚动；Serial 和 BaudRate 仍使用各自原有属性编辑器。

`FilterWheelHoleMappingPropertiesEditor` 通过属性上的 `PropertyEditorType` 接入。`FilterWheelHoleMappingEditSession` 打开时复制每行，**提交** 校验成功才替换该属性；取消、Esc 或关闭窗口均丢弃副本。新行取当前草稿中最小的未使用非负索引，默认五行之后为 5，删除后可复用空缺。提交拒绝非 Int32 整数和重复索引，但不限制空名称、同名孔位、空集合或已有整数范围。配置编辑不扩大硬件能力：当前 `FilterWheelController.SetPositionAsync` 仍只接受 0–4。

每行的 `CalibrationGroupName` 不在此窗口展示，复制与提交时原样保留。该字段注释描述了自动切组用途，但当前 `SpectrometerManager.OnFilterWheelPositionChanged` 实际先按标定组的 `FilterWheelPosition` 查找，再按孔位的 ND 名称查找，并不调用 `GetCalibrationGroupName`。自动切组关联仍在“标定文件分组管理”维护，不能把此字段的保存误认为运行时已经使用它。

`FilterWheelHoleMappingEditSessionTests` 覆盖隔离编辑、提交快照、添加/删除、索引校验和标定组保留；窗口按钮、主题和滚动需要 WPF 预览验收。

## 先查什么

| 现场问题 | 第一检查点 |
| --- | --- |
| 工具菜单没有“光谱仪软件” | 插件目录、`manifest.json`、`Spectrum.dll`、宿主版本要求 |
| 窗口状态栏为空 | `LoadMenuForWindow("Spectrum", ...)`、`StatusBarManager.Init(..., "Spectrum")` |
| 连接失败 | 许可证同步、USB/COM 配置、native DLL、设备占用、驱动 |
| 已连接但测量按钮不可用 | `IsCalibrationReady`、`CalibrationStatus`、配置路径与已加载文件指纹 |
| SN 为空或标定失败 | 设备序列号、当前 SN 的标定分组、`WavaLength.dat` / `Magiude.dat`，以及失败后的旧标定恢复状态 |
| 自动校零失败 | 检查所选遮光方式：Shutter 的连接与开关确认，或滤色轮连接、遮光孔位和原孔位恢复；Socket/Job 同样要求所选控制器可用 |
| 测量超时或曲线不刷新 | 积分时间、同步频率模式、SDK 返回码、重试结果 |
| 结果列表有数据但数据库没有 | `ViewResultManager`、SQLite 路径、写入异常 |
| EQE 字段为 0 | SMU 配置、测量模式、EQE 回写 |
| Socket 无响应或请求结果难以判断 | [Spectrum Socket 业务契约](./spectrum-socket.md)及其公共传输层入口 |

## 运行链路

宿主按 `manifest.json` 加载 `Spectrum.dll`，`MenuSpectrumWindow` 在工具菜单提供“光谱仪软件”入口。`MainWindow` 是 WPF 组合点，负责生命周期、提示、结果列表和绘图；`SpectrometerManager` 是无窗口依赖的设备入口，管理光谱仪句柄、标定状态和测量流程。Shutter、滤光轮和 SMU 各自在控制器内串行化设备访问。

`IsConnected` 只表示通信已经建立。连接后按设备 SN 加载标定分组；`IsCalibrationReady` 检查已连接、没有加载或配置提交在途，以及已加载快照的分组名和路径与当前配置一致。这个属性不会重新读取文件内容。

测量入口的 `TryGetCalibrationNotReadyReason` 还会重算两个文件的 SHA-256。文件在加载后被替换，即使按钮先前可用，本次测量也会拒绝并要求重新加载；文件不存在或不可读也会使快照失效。排查时同时看 `CalibrationStatus` 和本次测量结果。

测量按配置执行暗场、自动积分、采集和 EQE 派生，然后把结果与测量画像放进同一数据库事务。Manager 返回 `SpectrumMeasurementResult`；MainWindow 只做异步 UI 投影，历史曲线在第一次查看时延迟生成。

独立 WPF 入口是 `App.Application_Startup`，仍初始化配置、许可证和公共 Socket 模块，依赖匹配的 ColorVision 库、原生 DLL/驱动及标定资源；不是只复制一个可执行文件就能测量。独立更新在 `Update/SpectrumUpdateService.cs` 内维持自身责任边界，不应依赖主程序 ServiceHost 才能交付独立版。

`MainWindow.PrepareForShutdownCoreAsync` 停止接收新测量、取消连续测量，等待在途测量结束（包括其保存路径），随后尝试断开光谱仪并关闭辅助设备。`CloseAuxiliaryDevicesAsync` 的 `12` 秒仅限制等待 `IsBusy` 消退的轮询，超时仍忙的设备会警告并跳过强制释放；后续非忙设备的关闭及前面的在途等待不受这个计时器限制。它不是窗口关闭的总截止时间，也不保证所有设备已经安全关闭。

## 设备、标定和测量

| 环节 | 要确认 |
| --- | --- |
| 标定分组 | 当前设备 SN 能找到活动分组 |
| 标定文件 | 两个文件通过预校验，加载快照与当前配置的路径、分组和 SHA-256 一致 |
| 自动校零 | UI 手动流程允许人工遮光；自动测量、Socket 和 Job 按配置使用 Shutter 或滤色轮。滤色轮会记录当前测量孔位，切到可配置的遮光孔位采集暗场，再恢复原孔位；任何切换或恢复失败都会阻止正式测量 |
| EQE | 已启用 EQE；确认本次电压、电流来自手工配置还是源表采集，且与被测样品一致 |

`CalibrationGroupWindow` 编辑独立副本。保存要求已知设备 SN；已连接时先加载两个候选文件，加载成功且请求仍有效才提交配置。未连接分支不加载 native，但 SN 缺失仍会拒绝保存。配置保存在 Windows 文档目录的 `Spectrometer/<SN>/CalibrationGroups.json`，以临时文件替换写入；保存失败会尝试恢复原标定，恢复失败时保持不可测量状态。

关闭有未保存修改的窗口会询问保存、不保存或取消；保存失败保持窗口打开，保存进行中也不允许关闭。

EQE 测量先取 `MainWindowConfig.EqeVoltage` / `EqeCurrentMA`。源表已打开且本次成功取得采样时，以实测电压、电流覆盖；未连接或没有取得本次采样时仍使用配置值。因此有 EQE 结果不等于本次读取过源表，配置值的来源必须与实验条件一致。

“重新计算 EQE”保留已记录的光通量，并据此计算光效；尚无光通量的记录使用原始测量值 `fPh` 初始化。防负亮度仅作用于亮度显示，不截断光通量或光效。

主程序与独立 Spectrum 共用 `cvColorVision.SpectrumColorRendering`，由有效相对光谱按 CIE 13.3-1995 计算 Ra、R1～R14，并输出日本肤色色样扩展 R15。Ra 只取 R1～R8 的平均值；Ri 保留负数，不裁剪到 0～100。计算使用 CIE 1931 2° 标准观察者、Robertson 光谱色温、5000 K 以下的黑体或不低于 5000 K 的 CIE D 系列参考光源、色适应与 CIE 1964 U*V*W* 色差。CRI 的参考色温从光谱独立计算，不使用设备旧 `fCCT` 选择参考光源。色样、日光基函数与色匹配函数的来源和许可随库保留在 `ColorRenderingData/NOTICE.txt`，运行时不依赖 Python 或联网。

当前 CRI 计算使用实测光谱中的 380～780 nm，不外推未测波段；光谱需覆盖该范围、采样间隔不超过 5 nm、数值有限且非负、能量非零，参考色温限定 1667～25000 K。与参考光源的 CIE 1960 uv 距离超过 0.0054 时不输出 CRI。负值光谱（包括 SG 的负旁瓣）不通过裁剪变成有效 CRI；界面以 `—` 和“显色性状态”说明不可用原因，CSV 对应数值留空并输出 `Color Rendering Status`。平均波长 `fLav` 是整个有效实测波段的光谱功率重心 `∫λS(λ)dλ / ∫S(λ)dλ`，以梯形积分计算，短波段也可独立输出平均波长。

主程序与插件的 CCT/Duv、主波长和兴奋纯度共用 `cvColorVision.SpectrumChromaticity`，输入为设备的 CIE 1931 2° `fx/fy`；CRI 使用实测光谱积分得到的白点，复用同一 Robertson 1968 内核选择参考色温。Duv 是 CIE 1960 UCS 中到黑体轨迹的有符号距离，不能用 CRI 的参考距离或 CIE 1976 u′v′ 距离代替。CCT 仅在 1667～25000 K 且 `|Duv| <= 0.05` 时输出；其它情况数值留空并提供状态，Duv 在温度范围内仍可用于诊断偏离。

主波长/兴奋纯度以 D65 `(0.3127, 0.3290)` 为默认参考白点，使用 380～780 nm 的 1 nm CIE 1931 轨迹与射线相交、段内插值。白点附近纯度为 0、主波长未定义；紫红色输出独立的补色波长和紫线边界纯度，不冒充主波长；非法/越界色度不再截成 0 或 100%。界面不可用数值显示 `—`，CSV 留空，另有波长/温度状态及参考白点字段。既有 `fLd` ABI 字段用负波长表示补色波长、NaN 表示不可用；界面/CSV/Socket 使用可空结果及独立补色字段。新测量保存更新后的派生字段，历史查看按保存的色度重新计算，不用旧纯度覆盖新结果。

主程序与插件的相对/绝对单条光谱共用渐变填充样式：颜色按波长位置变化，填充范围限于实测曲线下的 380～780 nm 正值部分，保留完整原始采样和直线段，不为视觉效果平滑尖峰或修改负数据。多条对比保留折线；主题切换只改配色。颜色按 Colour 的 CIE 1931 2° 绘图约定，使用 E 到 D65 的 CAT02 适配和固定 380～780 nm 范围的整体线性 RGB 归一化后进行 sRGB 编码，避免逐通道提前截满导致过饱和；缩放和测量范围变化不改变同一波长的示意色。颜色仅作波长示意，不代表显示器校准。主布局固定为上图下表，测量列表可通过工具栏面板图标收缩和恢复，收缩后工具栏仍保留；逐点数据通过导出查看。

工具栏的“分析”和结果行右键的“分析此光谱”打开独立 `SpectrumAnalysisWindow`。窗口捕获该条记录的完整采样、指标及测量模式，不跟随后续列表选择或新测量变化；可切换相对/绝对光谱、读取采样点，并查看 CCT、Duv、Ra、R9、亮度/光通量、峰值波长、FWHM 与平均波长。光通量取记录中的测量光通量，不用亮度保护值代替；无效或未记录的指标显示 `—`。显色性 R1～R15 在分析窗口默认展开，R9 单独强调，坐标保留负指数；无效显色性显示原因，不绘制零分柱。两端共用 `SpectrumAnalysisPanel` 与 `SpectrumRenderingChart`。

图上读数使用临时悬浮提示，不预留常驻说明行。它针对当前选中记录，在鼠标对应的最近原始采样点显示波长、相对值、绝对值和单位，并附带记录时间与关键指标摘要，同时绘制游标和点标记；多曲线比较时仍读取选中记录，不自动跳到其它曲线。读数使用完整采样数组，保留负值，不按展示表抽样或插值；只在实际绘图区和测量波段内显示，移出、清空或切换记录时关闭。亮度模式绝对值单位为 `W/(sr·m²·nm)`，光通量模式为 `W/nm`，已记录的模式优先于当前窗口配置，两端共用 `SpectrumPlotCursor` 与 `SpectrumHoverReadout`。

普通和 EQE 结果表均显示平均波长、Ra 与 R1～R15；两种 CSV 模式均导出这些数值与状态。新测量沿用既有字段保存，不改变原生结构布局或数据库表；历史记录查看时从保存的光谱重新计算，不覆盖历史数据库。独立参考光谱测试覆盖黑体、日光、荧光灯与 LED，以及无效光谱、负显色指数和两种导出模式；数值对照不代替实物标定和计量验收。

主测量使用 `CM_*` API，`DirectSpectrometer/` 诊断工具使用 `SA_*` API；两者共享原生驱动并具有全会话互斥关系，不能同时连接。

测量区域齿轮打开 **自动积分与数据采集配置**，在数据采集配置中选择 **平滑算法**。`GetDataConfig.SmoothingMethod` 默认 `Mean=0`（均值滤波）；旧配置缺少该字段时继续使用均值，`FilterBW` 原值、宽度小于等于 2 回退 5 点及偶数窗口规则保持兼容。`SavitzkyGolay=1`（SG，二阶，试验）在原始采样索引上复用预计算卷积核，窗口必须是 3～2047 的奇数且不超过实际点数。算法在每次普通或同步频率采集前应用；切回均值恢复原操作，测量画像记录所选算法。SG 保留零填充、负旁瓣和边缘过冲，目前只有合成数据验证，会改变测量结果，正式使用前需核对实测光谱与标定。

## 数据和文件

| 类别 | 入口 | 说明 |
| --- | --- | --- |
| 插件元数据 | [manifest.json](https://github.com/xincheng213618/scgd_general_wpf/blob/master/Plugins/Spectrum/manifest.json) | 插件身份、DLL 路径和最低宿主要求；版本由专用脚本按编译后的 `Spectrum.exe` 同步 |
| 构建与发布版本 | `Spectrum.csproj`、`Scripts/build_spectrum.py` | 工程的 `VersionPrefix` 生成版本；专用发布脚本读取编译后 `Spectrum.exe` 的四段 `FileVersion` 并同步源目录和输出目录的 manifest |
| 窗口 | `MainWindow.xaml(.cs)` | 生命周期、连接/测量按钮、EQE、结果列表、绘图和用户提示 |
| 设备状态 | `SpectrometerManager.cs` | 原生句柄、标定快照、设备操作门禁和一次完整测量；不创建窗口或文件对话框 |
| 标定 | `Calibration/` | 按光谱仪 SN 管理标定分组 |
| 辅助设备 | `Configs/*Controller.cs` | Shutter、滤光轮、SMU 各自的连接、命令和释放门禁 |
| 许可证 | `License/` | 许可证导入、同步和原生日志入口 |
| SQLite | `%APPDATA%\Spectromer\Config\Spectrum.db` | 本地结果库 |
| 光谱结果 | `SprectrumModel` | 光谱测量结果 |
| 测量画像 | `SpectrumMeasurementProfile` | 测量上下文和配置快照 |
| CSV | `SpectrumCsvExporter.cs` | 无 UI 的不可变快照、实际波长对齐和流式写入 |

主程序和插件的普通光谱列表不显示电压、电流，列菜单也不提供这两项；EQE 模式显示对应列。“自动调整列宽”不会恢复普通模式下的电压、电流列。CSV 按调用时选中结果建立不可变快照，Normal/EQE 模式使用各自固定字段，不按界面可见列生成。波长取各结果 `SpectralDatas` 的网格并集，先输出全部绝对值列，再输出对应 `sp` 相对值列；某条记录没有的波长留空。

结果行右键可单独导出鼠标所在记录的测量结果 CSV 或完整原始光谱 CSV，不使用其它已选记录；工具栏保存图标仍导出所选记录。保存图标打开导出菜单，分别提供“导出测量结果 CSV”和“导出完整原始光谱 CSV”。前者保留既有展示采样与结果字段；完整导出共用 `cvColorVision.SpectrumRawExporter`，每个采样点占一行，按记录分组输出索引、波长、原始相对值、绝对值及单位、起点/间隔/点数、原始数组容量、绝对值比例系数、测量时间、设备标识（已记录时）、测量参数与显色性结果。实际有效范围内的全部点均保留；0.1 nm 的 4001 点仍为 4001 行，不插值、抽样或截去负值。有效范围已知时裁掉 ABI 末尾填充容量；历史波长元数据无效时保留原数值及元数据，波长列留空并标记 `InvalidStoredMetadata`，不把显示层回退网格当作原始网格。缺少有效结束波长时标记 `StoredIntervalWithoutValidEnd`，不自行推断原数组中哪些值是填充。

完整导出的“原始”指保存在结果中的完整光谱，可能已经过 SDK 的均值/SG、暗场或标定处理，不是探测器 ADC 未处理数据。`SpectrumSource` 明确标记该含义。相对采样中的 NaN/Infinity 原样保留以便诊断，派生指标不可用时留空。插件按记录 ID 查询当次测量画像，导出已保存的参数/标定文件指纹及采集步骤信息；主程序使用记录或本地捕获快照的参数，缺失的历史信息留空，不使用当前设备设置补造。测量模式和绝对单位优先使用记录/画像，`ModeSource` 区分画像、已有 EQE 字段及导出上下文，避免把当前窗口的模式当成原始测量条件。新采集额外记录实际积分时间等上下文；不改变既有 `Params` 结构。

完整原始光谱导出按每条记录的测量类型处理参数：普通光谱移除可解析参数对象中的电压、电流字段（包括嵌套的参数 JSON），EQE 保留；数据库及内存中的原始参数不变。无法解析的历史参数文本原样保留。

导出在开始时捕获数组及指标快照，后台流式写入同目录临时文件，成功后才替换目标文件；取消或失败保留已有目标文件。CSV 使用不随系统区域改变的小数格式及标准引号转义。

`SpectralDatas` 按 `max(1, round(1 / fInterval))` 的步长取点并补上实际末端。例如 380–780 nm、0.1 nm 间隔的 4001 个原始点会形成 401 个导出采样点；不能把 CSV 列数当作原始 `fPL` 长度。排查导出为空时先确认已选中结果及有效的 `fSpect1/fSpect2/fInterval/fPL`。

保存前 `ViewResultSpectrum.NormalizeColorParam` 会规范化波长元数据并裁剪 `fPL`。有效起止波长和间隔下，点数按 `round((fSpect2 - fSpect1) / fInterval) + 1` 计算并限制在实际数组容量内；旧数据元信息无效时另有起点、间隔和最多 `4001` 点的初始回退规则，不能一律保存 native 数组全部容量。历史明细和曲线按需生成；`ViewResultManagerConfig.Count` 只限制内存显示集合，不删除 SQLite 历史记录，且 `Count <= 0` 不做该集合裁剪。

两种耗时终点不同：结果列表的“结果就绪耗时”累计到结果构造和 EQE 计算完成；`SpectrumMeasurementProfile.TotalDurationMs` 再累计到结果行插入完成，不包含测量画像行插入、事务提交和异步 UI 投影。不要用任一数值代替请求端到端耗时。

## Socket 和调度

五个 Socket handler 复用 Manager；准确的指令名、`Params`、返回字段、设备锁和 `30/60` 秒合作式取消边界仅在 [Spectrum Socket 业务契约](./spectrum-socket.md)维护。Spectrum 没有独立于 `ColorVision.SocketProtocol` 的另一套传输服务。

调度入口在 `Job/`：`SpectrumMeasureJob` 定时执行光谱测量，`SpectrumDarkCalibrationJob` 定时执行暗场/校零。调度任务失败时先看设备、标定 readiness、快门和 Scheduler 执行历史，不要把窗口是否打开作为业务前置条件。

## 验收

| 验收项 | 通过标准 |
| --- | --- |
| 插件装载 | 工具菜单出现“光谱仪软件”，能打开 `MainWindow` |
| 窗口扩展 | Spectrum 窗口菜单和状态栏出现，连接、型号、SN、标定组、模式可读 |
| 交付资源 | 包含 `Spectrum.dll`、manifest、README、CHANGELOG、标定文件和 native DLL |
| 许可证 | 连接前能同步许可证，异常能打开许可证管理或原生日志 |
| 设备连接 | 已知设备能读出型号和 SN；标定损坏时仍保持连接并明确显示不可测量状态，修复后可重新加载 |
| 单次测量 | 曲线刷新，结果列表新增记录，测量画像写入数据库 |
| EQE 测量 | 确认电压、电流来源，结果对象中的值、EQE 字段与导出结果一致 |
| 数据落库 | `Spectrum.db` 在同一事务中写入结果和测量画像；重置/删除不会发布数据库中已不存在的结果 |
| 标定切换 | 快速切组、无效候选和取消不会让声明快照与 native 状态错配；失败能恢复上一组或明确锁住测量 |
| Socket | 在宿主进程仍存活、服务启用且设备前提满足时验证无 Spectrum 窗口操作；区分连接与 readiness、忙与取消，校零要求快门 |
| 调度 | 在进程及 Scheduler 仍运行且设备前提满足时验证无窗口调用和执行历史；窗口关闭导致的断开不能视为保持可测量 |
| 双通道发布 | 插件 latest、独立 latest/latest-version、签名、下载大小和 SHA-256 全部通过专用脚本验收 |

## 本地构建与测试

以下命令只写本地编译/测试产物，不发布更新源。设备连接、暗场、快门和测量烟测会影响真实设备，须另外确认现场授权。

```powershell
dotnet build .\Plugins\Spectrum\Spectrum.csproj -c Release -p:Platform=x64
dotnet test .\Test\Spectrum.Tests\Spectrum.Tests.csproj -c Release -p:Platform=x64
```

`ViewResultSpectrumTests` 覆盖有效点数、采样端点和旧元信息回退；`SpectrumPlotFillTests` 覆盖共享渐变填充的原始采样、波段/零点裁剪、断点和重复刷新替换；`SpectrumChromaticityTests` 用独立参考数据验证 CCT/Duv、主/补色波长、纯度及双端导出边界；`SpectrumCalibrationStateTests` 检查标定快照、路径及文件哈希；`SpectrumCsvExporterTests` 检查展示导出的字段、采样网格、格式和调用时快照；`SpectrumRawExportTests` 检查完整采样、负值/异常值、ABI 填充、网格来源、双端一致性及原子写入；`SpectrumCursorTests` 检查原始网格读数、边界和不插值行为。这些用例不能代替原生设备、窗口关闭或 Socket 时序验证；引用测试文件也不表示测试已经运行。

## 双通道发布（需明确发布授权）

Spectrum 同时维护独立 ZIP 和 ColorVision `.cvxp` 更新源。只有用户明确要求发布 Spectrum 时运行下列命令；它会更新远端两个发布源，不是本地打包或测试命令。

```powershell
.\Scripts\Spectrum.bat --release-notes "本次变更说明"
```

不要用通用 `package_plugin.bat Spectrum` 代替正式发布。专用脚本会构建两种包、签名独立清单、按顺序提交两个更新源，并验证插件 latest、独立 latest/latest-version、下载大小和 SHA-256；全部远程验收通过后才删除本地 `.cvxp`。
