---
knowledge_id: "plugins.camera-test"
knowledge_type: "topic"
status: "current"
summary: "CameraTest 相机生产调试：直接 SDK 取图、BMW 四边 SFR、RGB 位移、调焦趋势、可选判定与按设备编号存档；不初始化数据库或服务。"
aliases: ["CameraTest", "相机生产调试", "相机出厂检测", "BMW 四边实时分析", "独立相机测试", "StandaloneCameraSession"]
code_paths: ["Plugins/CameraTest", "Engine/ColorVision.Engine/Services/Devices/Camera/Local/StandaloneCameraSession.cs", "Scripts/package_camera_test.ps1"]
test_paths: ["Test/CameraTest.Tests/CameraTestContractsTests.cs", "Test/CameraTest.Tests/CameraTestWindowTests.cs", "Test/CameraTest.Tests/ProductionArchiveTests.cs", "Test/CameraTest.Tests/ChromaticAberrationTests.cs", "Test/CameraTest.Tests/JudgmentAndFocusTests.cs", "Scripts/tests/test_package_camera_test.ps1"]
related: ["operations.camera", "engine.opencv-helper-api"]
---

# 相机生产调试

CameraTest 用于相机生产过程中的调焦、参数记录和数据存档，同时提供独立 EXE 和宿主“工具 → 相机生产调试”入口。插件 ID 仍为 CameraTest，与早期“相机出厂检测”版本保持升级连续性。启动只加载本地界面与配置，不运行 Engine 的 `IInitializer` 发现链，不创建数据库连接、MQTT、注册中心或业务服务。Engine 项目引用用于复用 `StandaloneCameraSession`，该类不创建 `DeviceCamera` 或模板/结果 DAO。

本文描述本地 1.0.0.3 源码。已发布首版 1.0.0.1 提供取图、SFR 分析和生产存档；RGB 位移、趋势、阈值判定和编辑器画框同步属于后续本地测试版，尚未在线发布。

## 输入和分析

三个入口组成两种模式：实时分析；取图/文件共用的单帧分析。支持 8/16 位灰度或彩色 BMP、PNG、JPEG、TIFF。四通道文件的 alpha 丢弃，不参与光学计算。用户先框出包含一个 BMW 靶标的搜索区域，可添加多个区域；算法在原分辨率内定位四条边，保存固定区域 ID，失败边不删除、不填零。坐标、质量和曲线契约沿用 Core 的 BMW 与 SFR 接口。

下方矩形绘图工具与左侧“框选新增”共用搜索区域：画出的矩形自动加入左侧列表，点击“开始分析”运行检测；移动、缩放、删除或撤销会同步区域并清除旧结果。区域身份在编辑与撤销期间保持稳定，重复名称自动分配唯一身份。旋转矩形使用其轴对齐外接范围作为搜索区域，不会旋转原图。小于 40×40 像素或越界的框保留在列表中并提示修正，修正前禁止分析。加载配置会恢复可编辑矩形并清空旧绘图撤销记录。分析期间锁定绘图，避免修改中的范围混入结果。

Y 显示名对应接口 L，为解码后 `0.213R + 0.715G + 0.072B` 的相对组合信号。彩色输入计算 R/G/B/L；灰度输入只有 L。MTF50/10 和频率查询使用 cycles/pixel，Nyquist 为 0.5；编码未知保留警告。没有产品规格时不输出产品合格结论。

“色差”页通过 Core `SfrChromaticAberration` 显示 R−G、R−B、G−B 的 50% 边缘位置差，单位为输入像素。先将各通道独立居中的 ESF 恢复到同一 ROI 坐标，再在公共法线方向比较。正负号为前一通道减后一通道，方向 X/Y 表示对应原图正轴分量；导出另含轴向差、公共法线向量和对齐曲线。通道无效、缺失、方向不一致或交点不唯一时保留空值与原因，不能用零代替。该量包含整个成像链的偏移，不等于 ΔE、面积色差或纯镜头径向色差；概念可参阅 [Imatest 色差定义](https://www.imatest.com/imaging/chromatic-aberration/)，不声明实现与其数值一致。

“判定标准”可选 MTF50/10、指定频率响应下限及三组位移绝对值上限，默认全部留空。SFR 阈值作用于全部区域四边的指定通道；位移阈值独立选择通道对。结果区分未设置标准、合格、不合格和无法判定；缺测、未定位、未知编码或质量警告不能作为合格证据。只要存在确定超限项，总结为不合格，其余缺测项仍单独展示。

搜索区域与图像尺寸一起保存，换分辨率不自动缩放。最多 64 框；单帧预算 512 MiB，单区域仍服从 BMW 接口限制。结果 JSON schema 2 保留帧 ID、来源、时间、尺寸、位深、参数、目标/边状态、位移、判定标准和曲线；CSV 包含指标、位移、判定与完整 MTF 采样；保存原图输出无损 PNG，不包含显示滤镜或叠图。配置 schema 1 保持兼容，旧配置缺少判定标准时默认不检查。

当前 BMW 定位面向完整黑色对角扇区、近水平/竖直（约 ±15°）的靶标，半径至少约 55 像素。不支持任意旋转、严重透视或遮挡；较小靶标也可能因单边区域不足而返回无效。

## 相机与实时模式

相机设置包含型号、BV_MODE/LV_MODE、ID、位深、曝光、增益和 `sys.cfg` 路径。调用现有 cvCamera SDK，用户需提供与本机驱动/许可证匹配的配置；不安装驱动、不申请许可证。框架只做 RAW 成像，不执行滤轮、CIE 校正或运动。

“连接”“取图分析”“开始实时分析”和“查找相机”访问硬件；启动应用或打开图片不访问硬件。切换测量/Live 模式会先关闭旧会话。SDK 回调内复制帧，后台只保留最新等待帧；UI 同一时间只执行一次分析，丢弃停止后的过期结果。完整图像与结果属于同一次分析帧。没有 ROI 时实时模式先预览；停止并冻结后编辑区域。停止释放 Live 连接，当前图像留在界面。

“调焦趋势”保留本轮最近 300 个分析帧的标量指标，可按区域、边、通道查看指定频率 MTF 或 MTF50，显示当前值及窗口内峰值。无效测量处断开，重复帧不重复记录；这不是全部相机帧的录像。停止后保留趋势，开始新一轮、换图、换配置、修改区域或分析设置时清空，避免混合不同条件。峰值仅是调焦参考；可导出 CSV，不能据此自动断言最佳焦点。

## 生产数据存档

先在“存档信息”填写设备生产编号 / SN，可同时填写操作员、批次/工单、备注和本地存档目录。设备生产编号独立于 SDK 连接 ID，不用作目录名。“记录并存档”保存当前冻结帧；尚未框选或分析时也可保存，记录明确标记为未分析。

每条记录写入独立时间/随机 ID 目录，包含 `record.json`、无损 `image.png` 和 `profile.json`；存在同帧结果时附带 `results.json`、`metrics.csv`，有本轮调焦记录时另存 `focus.csv`。记录包含来源、尺寸、位深、时间、源像素哈希及各文件 SHA-256。文件先写入 `.pending` 目录，全部完成后改为正式目录；失败保留的 `.pending` 不算完成存档。正常关闭窗口会等待正在执行的存档完成。无需数据库。

相机采集帧保存当时请求 SDK 的型号、模式、连接 ID、位深、曝光和增益快照，后续修改配置不会改变此快照；这些参数未经过硬件回读确认。历史图片的拍摄参数未知，记录中不填入当前相机设置，时间明确为导入时间。`profile.json` 保存当前工作配置，可用于复测；它不代替原始采集参数。现阶段不提供自动跨设备对比或存档检索数据库。

## 构建与交付

在仓库根目录 PowerShell 运行 `dotnet build .\Plugins\CameraTest\CameraTest.csproj -c Release -p:Platform=x64`。完整输出包含 `CameraTest.exe`、runtimeconfig 与托管/native 依赖；可传入一个本地图像路径自动打开。程序需要 Windows x64/.NET 10 Desktop Runtime。普通构建不复制到宿主、不上传；`.cvxp` 依赖宿主提供的共享文件，不代替独立运行包。

测试阶段使用 `pwsh -NoProfile -File .\Scripts\package_camera_test.ps1` 制作独立 ZIP，打包机需 PowerShell 7 和 .NET 10 SDK，接收端只需已配置的 .NET 10 Desktop Runtime x64 与相机驱动。脚本始终 `--self-contained false`，使用全新临时 artifacts/publish 目录，不读取旧 bin、用户配置或数据目录；不上传、不生成插件包，也不接入在线更新。版本从编译后 CameraTest DLL 的 PE FileVersion 读取并与源码清单核对，ZIP 名为 `CameraTest-<版本>-test-win-x64-<时间>.zip`，避免覆盖同版本的先前测试包。

ZIP 内只保留一个 `CameraTest.exe` 入口、已声明的托管/native DLL、Windows x64 运行资产、仓库提供的 SDK 默认配置和版权文件，另生成使用说明及逐文件 SHA-256 版本清单。不复制其他 EXE、PDB、用户配置、LIC、日志、数据库文件、图片或归档。共享 Engine 的数据库程序集可能仍在依赖中，保留程序集不代表初始化数据库。过滤后依据发布 deps.json 检查依赖完整性；不能照抄 Spectrum 的相机 DLL 排除名单。ZIP 外保留 SHA-256、打包报告和构建日志，手动分发只需 ZIP。`-OutputDirectory` 改输出目录，`-KeepBuild` 保留构建现场；默认成功后只清理本次创建且路径已核验的临时目录，失败保留现场。

## 验证范围

运行 `dotnet test .\Test\CameraTest.Tests\CameraTest.Tests.csproj -c Release -p:Platform=x64`。测试检查配置身份/边界、帧布局和内存预算、16 位显示及存档往返、文件哈希、采集参数快照与来源区分、失败边保留与 JSON 坐标，以及未连接会话不初始化 SDK。位移用例通过真实 SFR DLL 检查 8/16 位、两个方向、正反极性的已知亚像素偏移；判定与趋势用例检查缺测、绝对值阈值、窗口淘汰和重复帧。窗口用例验证离线打开图像、各页布局，以及通过绘图工具同一新增命令创建矩形后分析、修改、删除、撤销及配置恢复；可用 `CAMERATEST_SMOKE_IMAGE` 和 `CAMERATEST_SMOKE_PROFILE` 指定匹配的现场样本及配置。它们不操作硬件，不验证驱动/许可证、持续取流、SDK RAW 通道排列或量产重复性。BMW/SFR 算法另按其专项用例和现场样本验证；本工作区不宣称 ISO 或 Imatest 一致性。

打包规则用 `pwsh -NoProfile -File .\Scripts\tests\test_package_camera_test.ps1` 检查，包含相机 DLL 保留、历史数据排除、缺失依赖/文化资源和意外内置 .NET 的拒绝行为。分发前另将最终 ZIP 解压至新目录，验证启动、离线打开图像、BMW/SFR 分析与存档；仅在开发输出目录运行成功不能代替压缩包验收。
