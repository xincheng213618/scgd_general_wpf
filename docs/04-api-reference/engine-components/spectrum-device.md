---
knowledge_id: "engine.spectrum-device"
knowledge_type: "topic"
status: "current"
summary: "主程序光谱仪的全连接方式搜索、设备配置、许可证读取和四色校正单次采集入口；区分本机搜索、服务端刷新与实际连接。"
aliases: ["光谱仪搜不到", "光谱仪连接方式", "光谱单次采集", "SpectrumColorMeasurement", "CaptureColorMeasurementAsync", "ConfigSpectrum", "InfoSpectrum", "GetSpectrSerialNumberAsync", "SpectrumDeviceDiscovery", "CMvSpectra", "Gaolitong", "光谱仪配置分类"]
code_paths: ["Engine/ColorVision.Engine/Services/Devices/Spectrum", "Engine/ColorVision.Engine/Services/PhyCameras/Licenses/LicenseManagerWindow.xaml.cs"]
test_paths: ["Test/Spectrum.Tests/SpectrumDeviceDiscoveryTests.cs", "Test/Spectrum.Tests/SpectrumCalibrationGroupConfigTests.cs"]
related: ["engine.devices", "engine.native-bindings", "ui.property-grid", "plugins.spectrum"]
---

# 主程序光谱仪搜索与配置

本页适用于 ColorVision 设备树中光谱仪的属性页和许可证管理窗口。独立光谱仪软件的连接、测量与标定见 [Spectrum 插件](../plugins/standard-plugins/spectrum.md)。

主程序光谱卡片与 `ViewSpectrum` 结果详情的登记、首次显示和首结果入口见[设备详情视图按需初始化](./device-service-chain.md#设备详情视图按需初始化)。

## 搜索设备

在光谱仪属性页的 **设备与连接 → 搜索光谱仪** 查询本机设备。搜索依次遍历 `CMvSpectra`、`LightModule`、`Gaolitong`，不受当前配置选中的连接方式限制。每种方式先查询 USB；配置串口大于 0 时，另对 `CMvSpectra` 和 `LightModule` 查询该串口。高利通只能通过 USB 枚举，连接时使用的串口不能传给其枚举接口。

结果按连接方式和端口显示序列号；将对应连接方式和 SN 填入 **修改配置**。搜索不修改已保存的连接方式、不创建连接句柄、不重启服务。查询在线设备仍可能受供应商驱动或设备占用影响，发现序列号不等于连接成功。

查询在后台依次执行，运行期间禁止从同一入口重复启动。某种驱动异常或原生返回值不为 `1` 时保留该项错误并继续查询其余方式。原生响应 `{"number":1,"ID":["SN"]}` 中只读取 `ID`，数量字段不作为设备序列号；同一结果内过滤空值和重复序列号。

许可证窗口的 **获取光谱仪许可** 使用同一搜索逻辑，对三种方式查询 USB，不再为读取 SN 打开和关闭设备。**刷新设备列表** 仍使用服务端 `CM_GetAllSnID` 刷新资源并获取许可证，和本机搜索是不同操作。

## 配置入口

| 分类 | 操作 |
| --- | --- |
| 设备与连接 | 修改配置、搜索光谱仪、刷新设备列表 |
| 校准与校正 | 标定分组管理、应用当前分组、光谱校正、自适应校零、校零设置、SP100 暗电流设置 |
| 采集与显示 | 编辑显示配置 |
| 数据与日志 | 文件保存位置、光谱仪日志 |
| 服务与维护 | 重启服务、上传许可证、重置、删除 |

属性页与其他设备共用 [GenCommand 自动生成机制](../ui-components/property-grid.md#命令属性页自动生成)，通过 `CommandDisplay`、`Category` 和 `Description` 元数据定义入口，不单独维护光谱仪布局或刷新数字角标。入口复用原有命令及权限检查；重置和删除保留原有确认。分类之间使用细分隔线，紧凑操作项随可用宽度换列，窄窗口可纵向滚动；色彩和交互状态跟随更新窗口的浅色、深色主题。

设备属性编辑器通过 `Category` 和 `DisplayName` 元数据组织连接、标定和采集参数，快门、ND、标定分组、SP100 和显示配置使用本地化名称。配置属性名、枚举数值、保存格式和默认值保持兼容。

## 四色校正的单次采集

`DeviceSpectrum.CaptureColorMeasurementAsync` 复用现有 `GetData` 指令、设备状态检查、动态超时和本次 `MasterId` 数据库查询，一次返回原始 `fPh`、`fx`、`fy`、相对光谱及 `fIp` 峰值 AD、积分时间、ND、设备 Code、结果 ID 与时间。它与普通测量、连续测量和光谱校正共用设备测量门禁；忙碌时拒绝并发，不另发第二次采集。该色度接口拒绝光通量 / EQE 结果与非有限的 Y/x/y 或光谱值，有限负数原样保留。波长优先使用结果中的起始波长和正间隔，缺少时按点数回退 380 nm 与 0.1/1 nm。

`GetRecentColorMeasurementsAsync` 只读查询当前设备最近 100 条非光通量结果的摘要；`LoadColorMeasurementAsync` 再次按设备 Code、ID 和测量类型读取原始值及完整光谱。两者不触发硬件采集，已有结果和现场采集共用 `CreateColorMeasurement` 转换，不从已做亮度 / 负值裁剪的显示字符串回读数据。当前提供数据库历史选择，不解析外部 CSV / Excel。

历史结果缺少采集时间时保留未知状态，不以当前时间补齐；窗口要求核对其色块归属。峰值 AD 缺失保持 null；四色校正窗口按 AD / 65535 × 100% 检查 IP 30%～95%，非法 AD 拒绝，缺失或范围异常需复核，详见[四色校正采集](../../01-user-guide/devices/calibration.md#四色校正采集)。

该接口只完成光谱仪的一次采集，不切换 ND、画面或外部机构。四色校正窗口负责将结果绑定到当前 R/G/B/W 样本；现场仍需核对设备连接、当前 ND、被测画面与数据库结果归属。

波长文件和幅值文件是当前标定分组的兼容镜像。属性窗口修改这两个文件时同步更新当前分组；分组窗口保存或应用分组时再把分组文件同步回这两个字段。加载已有配置时以非空的当前文件修复活动分组中的旧路径，避免 `Default` 分组在后续应用时恢复过期文件。切换分组窗口中的浏览选择不会在保存前改变活动分组。

## 验证边界

`SpectrumDeviceDiscoveryTests` 注入模拟查询，覆盖全部类型、USB/串口组合、单驱动失败后继续、原生返回码和序列号解析。测试不加载供应商驱动，也不能替代现场真机搜索与连接验收。
