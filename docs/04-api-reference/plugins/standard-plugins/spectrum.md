# Spectrum Plugin - 光谱仪测试工具

## 概述

Spectrum 是 ColorVision 的光谱仪测试与色彩分析插件，提供完整的光谱仪设备控制、光谱数据采集、色度计算和数据管理功能。支持 SP100（CMvSpectra）和 SP10（LightModule）两种光谱仪型号，具备 CIE 色度分析、显色指数 Ra 计算、EQE 外量子效率计算等专业光学测量能力。

**版本信息**：
- 当前版本：v2.1.4.0
- 最低要求：ColorVision ≥ 1.3.15.8
- 最后更新：2026-03-29

## 主要功能

### 1. 设备管理

- **多型号支持**：
  - CMvSpectra（SP100）- 高精度光谱仪
  - LightModule（SP10）- 轻量级光谱模块

- **串口通信**：
  - 可配置 COM 端口和波特率（默认 9600）
  - 支持光谱仪、快门、ND 滤光轮、滤光轮等多设备串口控制
  - 自动积分时间配置

- **附件控制**：
  - 快门开关（可自定义指令和延迟时间）
  - ND 滤光轮（自动 ND 选择、暗测量端口）
  - 滤光轮位置切换

### 2. 光谱数据采集

- **测量模式**：
  - 单次测量
  - 批量连续测量
  - 自适应校零

- **波长范围**：380–780nm（可见光波段），1nm 步进
- **数据同步**：支持频率同步（默认 1000Hz）和滤波带宽配置
- **安全机制**：测量过程中禁用其他操作按钮，防止冲突

### 3. 色度分析

- **CIE 色度空间**：
  - CIE 1931（2° 标准观察者）色度图绘制
  - CIE 1976（10° 标准观察者）色度图绘制
  - CIE 2015 色彩空间支持

- **色度参数计算**：
  - 色坐标（x, y）计算
  - 相关色温 CCT（McCamy 公式）
  - 主波长计算
  - 兴奋纯度
  - 颜色表示

- **显色指数**：
  - Ra（一般显色指数）计算
  - 基于 CIE 13.3-1995 标准
  - TCS01–TCS08 测试色样光谱反射率
  - CIE 1931 2° 观察者颜色匹配函数

### 4. 数据可视化

- **ScottPlot 图表**：
  - 相对光谱曲线
  - 绝对光谱曲线
  - CIE 色度图叠加显示

- **波长转 RGB**：
  - 将可见光波长（380–780nm）转换为显示颜色
  - 基于 CIE 色度近似算法

### 5. 校正管理

- **校正组**：
  - 多组校正文件管理（每组包含波长和幅度校正）
  - 按设备序列号独立存储
  - 滤光轮位置关联

- **校正文件验证**：
  - 波长标定文件（WavaLength.dat）二进制格式验证
  - 幅度标定文件（Magiude.dat）二进制格式验证
  - 数据点计数、曝光时间、Lv 系数检查

### 6. 数据持久化

- **数据库存储**：
  - 基于 SqlSugar ORM 的光谱数据存储
  - 色度参数 JSON 序列化
  - 可配置查询数量和排序方式

- **CSV 导出**：
  - 光谱数据导出至桌面
  - 自定义导出路径

### 7. 其他功能

- **EQE 计算**：外量子效率计算，支持电压和电流参数配置
- **许可证管理**：本地与全局（AppData）许可证双向同步
- **面板布局持久化**：基于 AvalonDock 的面板布局保存与恢复
- **多语言支持**：英语、法语、日语、韩语、繁体中文、俄语
- **内置帮助系统**：包含专业术语和使用指南

## 快速开始

### 启用插件

1. 确保 ColorVision 版本 ≥ 1.3.15.8
2. 插件会在启动时自动加载
3. 在菜单栏查找 **光谱仪测试** 选项

### 连接光谱仪

1. 打开 **光谱仪测试** 窗口
2. 在设备配置中选择光谱仪型号（SP100 或 SP10）
3. 配置串口参数（COM 端口、波特率）
4. 点击连接按钮

### 执行测量

1. 确保光谱仪已连接
2. 选择校正组（如需要）
3. 配置积分时间或启用自动积分
4. 点击 **测量** 按钮开始采集
5. 测量完成后在图表和列表中查看结果

## 配置说明

### 光谱仪配置（SpectrumConfig）

| 配置项 | 类型 | 默认值 | 说明 |
|--------|------|--------|------|
| SpectrometerType | enum | CMvSpectra | 光谱仪型号（CMvSpectra/LightModule） |
| IsComPort | bool | false | 是否使用 COM 端口 |
| SzComName | string | COM1 | 串口名称 |
| BaudRate | int | 9600 | 波特率 |

### 快门配置（ShutterConfig）

| 配置项 | 类型 | 默认值 | 说明 |
|--------|------|--------|------|
| SzComName | string | COM1 | 快门串口 |
| BaudRate | int | 9600 | 波特率 |
| OpenCmd | string | a | 快门打开指令 |
| CloseCmd | string | b | 快门关闭指令 |
| DelayTime | int | 1000 | 响应延迟（ms） |

### ND 滤光轮配置（NDConfig）

| 配置项 | 类型 | 默认值 | 说明 |
|--------|------|--------|------|
| IsNDPort | bool | false | 是否启用 ND 滤光轮 |
| EnableResetND | bool | false | 启用 ND 自动复位 |
| NDMaxExpTime | double | - | ND 最大曝光时间 |
| NDMinExpTime | double | - | ND 最小曝光时间 |
| DarkNDPort | int | -1 | 暗测量端口（-1 表示未设置） |

### 数据管理配置（ViewResultManagerConfig）

| 配置项 | 类型 | 默认值 | 说明 |
|--------|------|--------|------|
| Count | int | 50 | 查询数量限制 |
| OrderByType | enum | Desc | 排序方式 |
| AutoRefresh | bool | true | 自动刷新 |
| Height | double | 300 | 视图高度 |
| ViewImageReadDelay | int | 1000 | 图像加载延迟（ms） |
| SavePathCsv | string | Desktop/Spectrum | CSV 导出路径 |

### 窗口配置（MainWindowConfig）

| 配置项 | 类型 | 默认值 | 说明 |
|--------|------|--------|------|
| LogControlVisibility | bool | true | 日志面板可见性 |
| IsFull | bool | false | 全屏状态 |
| EqeEnabled | bool | false | EQE 功能开关 |
| EqeVoltage | float | 5.0 | EQE 电压（V） |
| EqeCurrentMA | float | 20.0 | EQE 电流（mA） |
| CiePointEnabled | bool | true | CIE 色度点显示 |

## 技术架构

### 模块结构

```
Plugins/Spectrum/
├── MainWindow.xaml(.cs)              # 主窗口入口
├── MainWindow.Chart.cs               # 图表可视化（ScottPlot）
├── MainWindow.Connection.cs          # 设备连接管理
├── MainWindow.Measurement.cs         # 光谱测量逻辑
├── MainWindow.Export.cs              # 数据导出
├── MainWindow.ListView.cs            # 结果列表管理
├── MainWindow.Eqe.cs                 # EQE 外量子效率计算
├── MainWindowConfig.cs               # 窗口配置持久化
├── SpectrometerManager.cs            # 光谱仪核心管理器
├── SpectrometerType.cs               # 设备类型枚举
│
├── Assets/Image/                     # CIE 色度图参考图
│   ├── CIE-1931.jpg                  # CIE 1931 2° 标准观察者
│   └── CIE-1976.jpg                  # CIE 1976 10° 观察者
│
├── Calibration/                      # 校正管理界面
│   ├── CalibrationGroupWindow.xaml   # 校正组管理窗口
│   └── GenerateAmplitudeWindow.xaml  # 幅度校正生成窗口
│
├── Configs/                          # 设备配置
│   ├── SpectrumConfig.cs             # 光谱仪主配置
│   ├── CalibrationGroupConfig.cs     # 校正组配置
│   ├── CalibrationFileValidator.cs   # 校正文件验证器
│   ├── FilterWheelConfig.cs          # 滤光轮配置
│   ├── FilterWheelController.cs      # 滤光轮控制器
│   ├── ShutterController.cs          # 快门控制器
│   ├── SmuConfig.cs                  # SMU 配置
│   └── SmuController.cs              # SMU 控制器
│
├── Data/                             # 数据管理
│   └── ViewResultManager.cs          # 结果存储与查询（SqlSugar）
│
├── Help/                             # 帮助系统
│   ├── HelpWindow.xaml               # 帮助窗口
│   └── HelpContent.cs                # 帮助内容（术语和指南）
│
├── Layout/                           # 布局管理
│   └── DockLayoutManager.cs          # AvalonDock 布局持久化
│
├── License/                          # 许可证管理
│   ├── LicenseManagerWindow.xaml     # 许可证管理窗口
│   └── LicenseSync.cs               # 许可证双向同步
│
├── Menus/                            # 菜单系统
│   ├── SpectrumMenuIBase.cs          # 菜单基类
│   └── LayoutMenuItems.cs            # 布局菜单项
│
├── Models/                           # 数据模型
│   ├── SpectralData.cs               # 光谱数据点
│   └── ViewResultSpectrum.cs         # 测量结果模型
│
├── Properties/                       # 多语言资源
│   ├── Resources.resx                # 默认语言
│   ├── Resources.en.resx             # 英语
│   ├── Resources.fr.resx             # 法语
│   ├── Resources.ja.resx             # 日语
│   ├── Resources.ko.resx             # 韩语
│   ├── Resources.ru.resx             # 俄语
│   └── Resources.zh-Hant.resx        # 繁体中文
│
├── PropertyEditor/                   # 属性编辑器
│   └── TextSerialPortPropertiesEditor.cs  # 串口属性编辑器
│
└── View/                             # 色度计算
    ├── ColorimetryHelper.cs          # CIE 色度空间计算
    ├── RaCalculator.cs               # 显色指数 Ra 计算
    └── WavelengthToColor.cs          # 波长转 RGB 颜色
```

### 依赖关系

```
Spectrum Plugin
├── ColorVision.UI           # UI 框架（菜单、设置、属性编辑器）
├── ColorVision.Database     # 数据库访问（SqlSugar ORM）
├── cvColorVision            # 视觉处理核心（COLOR_PARA 等数据结构）
├── AvalonDock               # 可停靠面板布局
├── ScottPlot.WPF            # 光谱图表可视化
├── OpenCvSharp4             # 图像处理
└── System.IO.Ports          # 串口通信
```

### 核心数据流

```
光谱仪设备 (SP100/SP10)
    │
    ▼ 串口通信 (System.IO.Ports)
SpectrometerManager  ←── SpectrumConfig (设备配置)
    │                ←── CalibrationGroupConfig (校正参数)
    ▼
MainWindow.Measurement  (数据采集)
    │
    ▼
SpectralData[]  (380-780nm, 1nm 步进)
    │
    ├──▶ MainWindow.Chart  (ScottPlot 图表渲染)
    ├──▶ ColorimetryHelper  (CIE 色坐标、CCT、主波长)
    ├──▶ RaCalculator  (显色指数 Ra)
    ├──▶ ViewResultSpectrum  (结果模型)
    │       │
    │       ├──▶ MainWindow.ListView  (UI 列表显示)
    │       ├──▶ ViewResultManager  (数据库存储)
    │       └──▶ MainWindow.Export  (CSV 导出)
    │
    └──▶ MainWindow.Eqe  (EQE 外量子效率)
```

## 色度计算说明

### CIE 色坐标计算

基于 CIE 1931 2° 标准观察者颜色匹配函数，在 380–780nm 范围内以 5nm 间隔进行积分：

```
X = ∫ S(λ) × x̄(λ) dλ
Y = ∫ S(λ) × ȳ(λ) dλ
Z = ∫ S(λ) × z̄(λ) dλ

x = X / (X + Y + Z)
y = Y / (X + Y + Z)
```

### CCT 计算（McCamy 公式）

```
n = (x - 0.3320) / (y - 0.1858)
CCT = -449n³ + 3525n² - 6823.3n + 5520.33
```

### Ra 显色指数计算

基于 CIE 13.3-1995 标准：
1. 计算测试光源的色坐标和 CCT
2. 确定参考照明体（普朗克辐射体或 CIE D 系列）
3. 使用 TCS01–TCS08 测试色样计算颜色偏移
4. Ra = 100 - 4.6 × ΔEi（8 个色样的平均值）

## 校正文件格式

### 波长标定文件（WavaLength.dat）

```
[uint64 DataLength]          // 数据点数量
[double[] wavelengths]       // 波长值数组
```

### 幅度标定文件（Magiude.dat）

```
[uint64 DataLength]          // 数据长度
[float MagExpTm]             // 标定曝光时间
[int LvCoffe]                // Lv 系数
[uint64 nCount]              // 数据点数量
[double[] wavelengths]       // 波长值数组
[double[] coefficients]      // 幅度系数数组
```

## 版本历史

### v2.1.4.0（2026-03-29）

**架构重构**：
- ✅ 采用模块化设计，将 MainWindow 拆分为 Chart、Connection、Measurement、Export、ListView、Eqe 部分类
- ✅ 新增 Configs、Models、View、Layout、Data、License、Help、Menus、Calibration、PropertyEditor 子模块
- ✅ 新增 CalibrationFileValidator 校正文件二进制验证
- ✅ 新增多语言支持（英语、法语、日语、韩语、繁体中文、俄语）
- ✅ 新增内置帮助文档系统

### v2.0.0.0（2026-03-23）

**重大更新**：
- 更新到 .NET 10
- 优化 UI 界面布局
- 增加兴奋纯度、颜色表示、校正组、状态栏显示、显色指数 Ra 计算
- 测量过程中禁用其他按钮

### v1.1.4.8（2025-09-09）

- 更新第三方 UI 库
- 取图失败自动重连（6 次）
- 修复许可证获取问题
- 增加数据库存储支持

### v1.1.3.4（2025-07-11）

- 增加 CIE 2015 色彩空间支持
- 增加变更日志

### v1.1.1.10（2025-06-25）

- 许可证全局保存与同步
- 许可证管理窗口
- 桌面日志优化

详见 [CHANGELOG.md](../../../../Plugins/Spectrum/CHANGELOG.md)

## 相关资源

- [插件开发概览](../../../02-developer-guide/plugin-development/overview.md)
- [插件开发入门](../../../02-developer-guide/plugin-development/getting-started.md)
- [插件生命周期](../../../02-developer-guide/plugin-development/lifecycle.md)
- [设备服务概览](../../../01-user-guide/devices/overview.md)
- [CHANGELOG](../../../../Plugins/Spectrum/CHANGELOG.md)
- [源代码](../../../../Plugins/Spectrum/)

## 许可证

本插件继承 ColorVision 主项目许可证。

**版权**：Copyright (C) 2025-present ColorVision Development Team
**作者**：xincheng

---

*最后更新：2026-03-29 | 文档版本：2.1.4.0*
