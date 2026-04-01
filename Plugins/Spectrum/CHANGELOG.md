#   CHANGELOG

## [2.1.7.0] 2026.03.30

1.重构项目架构，采用模块化设计：将 MainWindow 拆分为 Chart、Connection、Measurement、Export、ListView、Eqe 等部分类

2.新增 Configs 子目录：SpectrumConfig、CalibrationGroupConfig、CalibrationFileValidator、FilterWheelConfig、FilterWheelController、ShutterController、SmuConfig、SmuController

3.新增 Models 子目录：SpectralData、ViewResultSpectrum 数据模型独立管理

4.新增 View 子目录：ColorimetryHelper（CIE色度计算）、RaCalculator（显色指数Ra计算）、WavelengthToColor（波长转RGB）

5.新增 Layout 子目录：DockLayoutManager 实现 AvalonDock 面板布局持久化

6.新增 Data 子目录：ViewResultManager 数据库存储与管理，基于 SqlSugar ORM

7.新增 License 子目录：LicenseSync 实现本地与全局许可证双向同步

8.新增 Help 子目录：内置帮助文档系统，包含专业术语和使用指南

9.新增 Menus 子目录：SpectrumMenuIBase 菜单基类，LayoutMenuItems 布局菜单项

10.新增 Calibration 子目录：CalibrationGroupWindow、GenerateAmplitudeWindow 校正组管理界面

11.新增 PropertyEditor 子目录：TextSerialPortPropertiesEditor 串口属性编辑器

12.新增多语言支持：英语、法语、日语、韩语、繁体中文、俄语

13.新增 CalibrationFileValidator 校正文件二进制验证

14.优化 EQE（外量子效率）计算模块

## [2.0.0.0] 2026.03.23

1.更新到.NET 10

2.优化UI界面布局

3.增加兴奋纯度，增加颜色表示，增加校正组，增加状态栏显示，增加显色指数Ra计算

4.现在测量过程中，其他按钮是禁用的，防止出现问题

## [1.1.4.8] 2025.09.09

1.更新第三方UI库

2.现在在取图失败的时候，会尝试重连6次

3.修复无法获取到许可证的BUG

4.修复自适应校零和单次校零失败之后，单次测试会提示正在运行中的问题

5.增加数据库的存储支持


## [1.1.4.1] 2025.08.18

1.当积分时间设置小于当前同步频率下的最短曝光时间，不勾选自动积分的情况下，每次测试积分时间都会逐渐减小


## [1.1.3.4] 2025.07.11

1.增加变更日志

2.增加CIE2015 色彩空间 测量的显示和导出

3.移除CIE2015 uv 的显示

## [1.1.2.2] 2025.07.04

1.更新SDK线性度

## [1.1.1.10] 2025.06.25

1.更新DLL

2.添加桌面日志，优化操作

3.优化UI布局,删除之前版本的相关代码

4.现在许可证全局保存，会在软件打开时同步

5.增加一个许可证管理的窗口