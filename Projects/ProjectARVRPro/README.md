# ProjectARVRPro

AR/VR 显示设备光学性能专业测试系统 — 基于 ColorVision 平台的全面光学质量检测插件，涵盖亮度、均匀性、色彩、对比度、MTF（清晰度）、畸变、缺陷检测等全方位测试。

## 技术栈

| 项目 | 说明 |
|------|------|
| 框架 | .NET 10.0 / WPF (Windows x64) |
| 架构 | ColorVision 平台插件 |
| 版本 | 1.1.7.48 |
| 插件要求 | ColorVision >= 1.3.15.15 |
| 数据库 | MySQL (SqlSugar) — 批次/算法数据；SQLite — 本地测试结果 |
| 配置持久化 | JSON 文件 (ProcessGroups / Recipe / Summary) |

## 支持的测试项目

| 测试类型 | Process 类 | 测量参数 |
|----------|-----------|---------|
| White 255 | `White255Process` | 全白亮度、均匀性、色度 (CIE xyuv) |
| White 51 | `White51Process` | 低亮度白屏测试 |
| Black | `BlackProcess` | 暗电流、FOFO 对比度（依赖 W255 结果） |
| 亮色度测试 | `LuminanceChromaticityProcess` | 按可配置 Key 输出亮度、色度与均匀性；默认 Key 为 White，W25 使用 `Key=W25`、`CenterKey=P_9` |
| Chessboard | `ChessboardProcess` | 像素缺陷检测、棋盘格对比度 |
| Distortion | `DistortionProcess` | SMIA TV 光学畸变 |
| MTF H/V | `MTFHVProcess` | 调制传递函数 0F/0.3F/0.6F/0.8F |
| MTF H/V 048 | `MTFHV048Process` | 048 产品 MTF 0F/0.5F/0.8F |
| MTF H/V 058 | `MTFHV058Process` | 058 产品 MTF 0F/0.5F/0.8F |
| MTF H | `MTFHProcess` | 水平方向 MTF |
| MTF V | `MTFVProcess` | 垂直方向 MTF |
| Optical Center | `OpticCenterProcess` | 光轴中心对准 |
| OLED AOI | `OLEDAOIProcess` | OLED 自动光学检测 |

## 架构概览

```
ProjectARVRPro
├── App.xaml.cs                     # 入口：配置初始化 → 授权 → 主题 → 语言 → 加载引擎 → 启动窗口
├── ARVRWindow.xaml/.cs             # 主测试窗口：SN 输入 → 流程组选择 → 执行 → 结果处理
├── ProjectARVRProConfig.cs         # 全局配置 (SN / 重试次数 / 结果路径 / 模板 / Legacy 输出开关)
│
├── PluginConfig/                   # 插件集成层
│   ├── ProjectARVRPlugin.cs        #   IFeatureLauncherBase — 注册为 "ARVRPro" 功能
│   ├── ProjectARVRMenu.cs          #   MenuItemBase — Tools 菜单 "模组检测"
│   └── ProjectWindowInstance.cs    #   窗口单例持有
│
├── SocketRelay/                    # Socket 中转功能
│   ├── SocketRelayWindow.xaml/.cs  #   中转服务器 UI
│   ├── SocketRelayManager.cs       #   TCP 中转服务器（流程引擎 ↔ 外部客户端双向转发）
│   ├── AOITestSwitchImageCompleteHandler.cs # 外部 Client 切图完成后回传给 Flow 的中转处理器
│   ├── SocketRelayMenu.cs          #   MenuItemBase — Tools 菜单 "Socket中转服务器"
│   └── SocketRelayInitializer.cs   #   IMainWindowInitialized — 主窗口初始化时按配置自动启动中转
│
├── Process/                        # 测试流程框架（核心领域）
│   ├── IProcess.cs                 #   核心接口：Execute / Render / GenText / GetRecipeConfig
│   ├── IProcessExecutionContext.cs  #   执行上下文 (Batch / Result / Recipe / ImageView)
│   ├── ProcessManager.cs           #   单例：管理 ProcessGroup / ProcessMeta，反射发现 IProcess
│   ├── ProcessMeta.cs              #   流程元数据：名称、模板、IsEnabled、PictureSwitchConfig、配置
│   ├── ProcessGroup.cs             #   流程组：按产品/场景组织测试方案
│   ├── PictureSwitchConfig.cs      #   每步执行前的雷鸟切图配置
│   └── [各测试子目录]              #   每个测试类型按需包含 Process / ProcessConfig / TestResult / RecipeConfig
│
├── Recipe/                         # Recipe（限值 + 一次函数修正）系统
│   ├── RecipeBase.cs               #   Min/Max/K/B 判定与修正项
│   └── RecipeConfig.cs             #   IRecipeConfig 字典容器
│
├── Services/                       # 通信服务层
│   ├── SocketControl.cs            #   TCP Socket 命令分发器 (ISocketJsonHandler)
│   ├── SwitchGroupSocket.cs        #   Socket 处理器：SwitchGroup 事件
│   ├── RunAllSocket.cs             #   Socket 处理器：RunAll 一键执行
│   └── [其他通用 Socket 处理器]     #   非中转专属的协议处理
│
├── ThunderbirdSerialController.cs  # Thunderbird 设备串口控制器
├── SerialPortHelper.cs             # 串口通信辅助类
├── ObjectiveTestResult.cs          # 测试结果聚合模型 + CSV 导出
├── ProjectARVRReuslt.cs            # 测试结果实体 (SQLite 持久化)
├── ViewResultManager.cs            # 结果查询与管理
├── Summary.cs                      # 生产摘要 (产线 / 工人 / 产能 / 良率)
├── TestResultViewWindow.xaml       # 测试结果查看器 (CSV/PDF 导出)
├── ThunderbirdSerialDebugWindow.xaml # 串口调试 UI
└── LegacyARVR/                     # 向后兼容：旧版扁平输出格式
```

## 核心设计模式

- **策略模式** — 每个测试类型实现 `IProcess` 接口，Execute / Render / GenText 可插拔替换
- **服务定位器** — `ProcessManager.RecipeConfig.GetRequiredService<T>()` 获取 Recipe 类型化配置
- **类型化流程基类** — `ProcessBase<TProcessConfig, TRecipeConfig>` 统一声明并获取共享 Recipe，流程类无需重复实现 `GetRecipeConfig()`
- **单例管理器** — ProcessManager / ViewResultManager / SummaryManager
- **反射发现** — ProcessManager 扫描 `IProcess` 实现，Recipe 配置按流程类型延迟创建
- **插件架构** — 实现 `IFeatureLauncherBase` / `MenuItemBase`，由 ColorVision 宿主运行时发现
- **MVVM** — WPF 数据绑定，ViewModelBase / RelayCommand / OnPropertyChanged()
- **配置分层** — RecipeBase 承载限值与修正系数，ProcessConfig 保留行为配置

## 测试执行流程

### 外部触发模式（产线自动化）

```
1. 外部系统发送 "ProjectARVRInit" + SN
   → ARVRWindow.InitTest(SN)，返回第一个启用步骤的 SwitchPG 信息

2. 外部系统切换图案完成，发送 "SwitchPGCompleted"
   → ARVRWindow.SwitchPGCompleted()
   → 查找下一个启用的 ProcessMeta
   → 运行 FlowEngine 模板 → 执行 IProcess.Execute()
   → 解析结果、应用 RecipeBase 的 y = Kx + B 修正
   → 返回 "SwitchPG"（下一步）或 "ProjectARVRResult"（全部完成）

3. 重复直到所有启用步骤完成
   → 最终结果保存到 SQLite + CSV/Text 导出
```

### 一键执行模式 (RunAll)

```
1. Socket "RunAll" 或 UI 按钮 → RunAllAsync()
2. 顺序遍历所有启用的 ProcessMeta
3. 每步：按 PictureSwitchConfig 切图（如启用）→ 运行模板 → 执行流程 → 收集结果
4. 聚合结果保存
```

### Socket 中转服务器

`SocketRelayManager` 作为 TCP 中转/桥接，连接流程引擎（客户端）和外部系统（通过 SocketControl.Current.Stream），双向转发消息并记录完整日志。

## Socket 协议

ARVRPro 作为 **TCP 服务器**（默认端口 6666），外部系统（产线 PLC 等）作为客户端连接。

**协议格式** — JSON over TCP，UTF-8 编码：
- 请求：`{Version, MsgID, EventName, SerialNumber, Params}`
- 响应：`{Version, MsgID, EventName, SerialNumber, Code, Msg, Data}`

**注册的事件处理器：**

| EventName | Handler | 功能 |
|-----------|---------|------|
| `ProjectARVRInit` | `FlowInit` | 初始化测试（设置 SN），返回第一个启用步骤的 SwitchPG |
| `SwitchPGCompleted` | `SwitchPGSocket` | 外部确认图案切换完成，触发下一步测试执行 |
| `SwitchGroup` | `SwitchGroupSocket` | 按名称切换当前激活的流程组 |
| `RunAll` | `RunAllSocket` | 一键执行所有启用的步骤 |
| `AOITestSwitchImageComplete` | `AOITestSwitchImageCompleteHandler` | 中转 AOI 图像切换完成事件 |

## ProcessGroup（流程组）

`ProcessManager` 管理多个 `ProcessGroup`，每个组包含独立的 `ProcessMeta` 列表：

- 按产品/场景组织不同的测试方案（如 "产品A"、"产品B"、"调试"）
- 支持组的添加、删除、重命名、复制
- 通过 UI ComboBox 或 Socket `SwitchGroup` 事件切换
- 旧版 `ProcessMetas.json` 自动迁移为 Default 组，完全向后兼容

## PictureSwitchConfig（执行前切图）

每个 `ProcessMeta` 可独立配置执行前切图，目前支持雷鸟串口：

| 配置 | 说明 |
|------|------|
| `IsEnabled` | 是否在执行该步骤前切图 |
| `SendCommand` | 雷鸟切图指令，例如 `PIC1` |
| `ExpectedResponse` | 期望返回值，默认 `succeed` |
| `TimeoutMs` | 切图等待超时时间 |
| `SuccessDelayMs` | 切图成功后的稳定等待时间 |

## 依赖关系

### 项目引用
| 依赖 | 用途 |
|------|------|
| `ColorVision.Engine` | 核心引擎：设备服务、MQTT、批次处理、模板、流程引擎、数据库 |
| `FlowEngineLib.dll` | 可视化流程引擎（节点式测试流程执行） |
| `ST.Library.UI.dll` | 节点编辑器 UI 组件 |
| `CVCommCore.dll` | 通信核心库 |

### 主要 NuGet 包
SqlSugar、Newtonsoft.Json、log4net、HandyControl、AvalonDock、iText (PDF 导出)、CsvHelper、MathNet.Numerics、HelixToolkit (3D)、Markdig (Markdown 渲染)、Quartz.NET (调度)、GLWpfControl (OpenGL)

## 配置文件说明

| 文件 | 位置 | 说明 |
|------|------|------|
| `ProcessGroups.json` | `%APPDATA%/ColorVision/Config/` | 流程组与 Recipe 配置 — 测试流程、合格限值及 y = Kx + B 修正系数 |
| `ProjectARVRLiteSummary.json` | `%APPDATA%/ColorVision/Config/` | 生产摘要 — 产线 / 工人 / 产能 / 良率 |
| `ProjectARVRPro.db` | `%APPDATA%/ColorVision/Config/` | SQLite 数据库 — 测试结果持久化 |

## ProcessConfig 可配置 Key

MTFHVProcess / MTFHV048Process / MTFHV058Process 支持用户自定义解析 Key：

```csharp
public class MTFHVProcessConfig : ProcessConfigBase
{
    [Category("解析配置")]
    [DisplayName("Center_0F解析Key")]
    [Description("用于解析Center_0F数据的Key")]
    public string Key_Center_0F { get; set; } = "0F_MTF_HV_Center";
    // ... 更多视场角 Key 配置
}
```

## Legacy 兼容模式

通过 `ProjectARVRProConfig.UseLegacyARVROutput` 开关，可切换为旧版扁平 CSV 输出格式（`LegacyARVRObjectiveTestResult`），确保与老系统兼容。

## 构建

```bash
# 构建
dotnet build Projects/ProjectARVRPro/ProjectARVRPro.csproj

# 构建 Release x64
dotnet build Projects/ProjectARVRPro/ProjectARVRPro.csproj -c Release -p:Platform=x64
```

## 相关文档

| 文档 | 说明 |
|------|------|
| [CHANGELOG.md](./CHANGELOG.md) | 版本更新日志 |
| [ROADMAP.md](./ROADMAP.md) | 开发路线图 |
| [DESIGN_ProcessGroup.md](./DESIGN_ProcessGroup.md) | ProcessGroup 功能设计文档 |
| [ARVRPRO TCP 通讯协议手册.md](./ARVRPRO%20TCP%20通讯协议手册.md) | TCP 通信协议详细说明 |

## 维护者

ColorVision 项目团队
