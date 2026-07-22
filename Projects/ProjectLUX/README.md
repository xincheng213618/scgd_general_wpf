# ProjectLUX

LUX 亮度测试系统 — 基于 ColorVision 平台的显示设备光学质量检测插件，适用于 AR/VR 头显、显示面板等设备的亮度、色彩、对比度、MTF、畸变等光学参数的自动化测试。

## 技术栈

| 项目 | 说明 |
|------|------|
| 框架 | .NET 10.0 / WPF (Windows x64) |
| 架构 | ColorVision 平台插件 |
| 版本 | 1.1.4.42 |
| 插件要求 | ColorVision >= 1.3.15.10 |
| 数据库 | MySQL (SqlSugar) — 批次/算法数据；SQLite — 本地测试结果 |
| 配置持久化 | JSON 文件 (ProcessGroups / Recipe / Fix / Summary) |

## 支持的测试项目

| 测试类型 | Process 类 | 测量参数 |
|----------|-----------|---------|
| White 255 | `White255Process` | 9 点 POI 亮度 (Lv)、CIE xy/u'v'、CCT、均匀性、FOV |
| White 255 AR | `White255ARProcess` | AR 变体白屏测试 |
| White 51 AR | `W51ARProcess` | 低亮度白屏测试 |
| Red / Green / Blue 255 | `RedProcess` / `GreenProcess` / `BlueProcess` | 单通道 POI 色度分析 |
| Chessboard 6×6 | `ChessboardProcess` | 对比度测量 |
| Chessboard 5×5 | `Chessboard55Process` | 5×5 网格对比度 |
| Chessboard AR 4×4 | `ChessboardARProcess` | AR 变体对比度 |
| MTF H/V | `MTFHVProcess` | 调制传递函数（水平+垂直） |
| MTF H/V AR | `MTFHVARProcess` | AR 变体 MTF |
| VR MTF H / V | `VRMTFHProcess` / `VRMTFVProcess` | VR 专用 MTF |
| Distortion | `DistortionProcess` | SMIA TV 畸变（水平 & 垂直） |
| Optical Center | `OpticCenterProcess` | X 倾斜、Y 倾斜、旋转对齐 |
| VID (虚像距) | `SocketControl` 内置 | 自动对焦位置测量 |
| 光通量 | `SocketControl` (T0031) | 光谱仪光通量测量 |

## 架构概览

```
ProjectLUX
├── App.xaml.cs                 # 入口：配置初始化 → 授权 → 主题 → 语言 → 加载引擎 → 启动窗口
├── LUXWindow.xaml/.cs          # 主测试窗口：SN 输入 → 流程选择 → 执行 → 结果处理
├── ProjectLUXConfig.cs         # 全局配置 (SN / 重试次数 / 结果路径 / 模板选择)
│
├── PluginConfig/               # 插件集成层
│   ├── ProjectLUXPlugin.cs     #   IFeatureLauncherBase — 注册为 "LUX" 功能
│   ├── ProjectLUXMenu.cs       #   MenuItemBase — Tools 菜单入口
│   └── ProjectWindowInstance.cs #   窗口单例持有
│
├── Process/                    # 测试流程框架（核心领域）
│   ├── IProcess.cs             #   核心接口：Execute / Render / GenText / GetRecipeConfig / GetFixConfig
│   ├── IProcessExecutionContext.cs # 执行上下文 (Batch / Result / Recipe / Fix / ImageView)
│   ├── ProcessManager.cs       #   反射发现所有 IProcess 实现，管理 ProcessGroup / ProcessMeta
│   ├── ProcessMeta.cs          #   流程元数据：名称、模板、SocketCode、配置编辑命令
│   ├── ProcessGroup            #   流程组：按产品/场景组织测试配置
│   └── [各测试子目录]          #   每个测试类型 4 文件：Process / TestResult / RecipeConfig / FixConfig
│
├── Recipe/                     # Recipe（合格/不合格限值）系统
│   ├── RecipeBase.cs           #   Min/Max 限值对
│   ├── RecipeConfig.cs         #   IRecipeConfig 字典容器
│   └── RecipeManager.cs        #   单例管理器，JSON 持久化
│
├── Fix/                        # 修正/校准因子系统
│   ├── FixConfig.cs            #   IFixConfig 字典容器
│   └── FixManager.cs           #   单例管理器，JSON 持久化
│
├── Services/
│   └── SocketControl.cs        # TCP Socket 命令分发器 (ISocketTextDispatcher)
│
├── ObjectiveTestResult.cs      # 测试结果聚合模型 + CSV 导出
├── ProjectLUXReuslt.cs         # 测试结果实体 (SQLite 持久化)
├── ViewResultManager.cs        # 结果查询与管理
├── Summary.cs                  # 生产摘要 (产线 / 工人 / 产能 / 良率)
└── TestResultViewWindow.xaml   # 测试结果查看器 (CSV/PDF 导出)
```

## 核心设计模式

- **策略模式** — 每个测试类型实现 `IProcess` 接口，Execute / Render / GenText 可插拔替换
- **服务定位器** — `RecipeConfig.GetRequiredService<T>()` / `FixConfig.GetRequiredService<T>()` 获取类型化配置
- **单例管理器** — ProcessManager / RecipeManager / FixManager / ViewResultManager / SummaryManager
- **反射发现** — ProcessManager 扫描所有已加载程序集发现 `IProcess` 实现
- **插件架构** — 实现 `IFeatureLauncherBase` / `MenuItemBase`，由 ColorVision 宿主运行时发现
- **MVVM** — WPF 数据绑定，ViewModelBase / RelayCommand / OnPropertyChanged()

## 测试执行流程

```
1. 输入 SN（或通过 Socket 命令 "T00XX,SN;" 接收）
2. InitTest(SN) → 重置 ObjectiveTestResult，创建结果目录
3. 选择 FlowTemplate（映射到可视化流程引擎）
4. 点击"测试"或接收 Socket 命令触发
5. RunTemplate():
   a. Refresh() → 加载 Base64 流程，绑定到 FlowEngineControl
   b. 在 MySQL 创建 MeasureBatchModel
   c. 执行 PreProcessors（如有）
   d. FlowControl.Start() → 运行可视化流程引擎
6. FlowCompleted 回调:
   a. "Completed" → Processing(SerialNumber)
   b. "OverTime" → 重试（最多 TryCountMax 次）
7. Processing():
   a. 从 MySQL 查询批次数据
   b. 按模板名称匹配 ProcessMeta
   c. 执行 IProcess.Execute(ctx):
      - 读取算法结果 → 应用 Fix 修正因子 → 对比 Recipe 限值 → 存储 ViewResultJson
   d. 导出 CSV 到 ResultSavePath
   e. 通过 ViewResultManager 保存到 SQLite
   f. 如有 ReturnCode，通过 Socket 返回结果
```

## Socket 协议

外部系统通过 TCP Socket 发送命令，格式：`T00XX,SN;`。

| 命令码 | 功能 | 说明 |
|--------|------|------|
| `T0000` | 握手/初始化 | 初始化测试，设置 SN |
| `T0001` | VID 虚像距 | 自动对焦测量，返回 VID 位置值 |
| `T0002` | 光学中心 | OC 测试（仅 AR 模式） |
| `T0031` | 光通量 | 光谱仪测量，返回光通量值 |
| `T00XX` | 流程执行 | 在当前活动组内按 SocketCode 匹配 ProcessMeta 执行对应测试 |

响应格式：`H03XX,SN,状态;[数据]`

## 依赖关系

### 项目引用
| 依赖 | 用途 |
|------|------|
| `ColorVision.Engine` | 核心引擎：设备服务、MQTT、批次处理、模板、流程引擎、数据库 |
| `FlowEngineLib.dll` | 可视化流程引擎（节点式测试流程执行） |
| `ST.Library.UI.dll` | 节点编辑器 UI 组件 |
| `CVCommCore.dll` | 通信核心库 |

### 主要 NuGet 包
SqlSugar、Newtonsoft.Json、log4net、HandyControl、AvalonDock、iText (PDF 导出)、CsvHelper、MathNet.Numerics、HelixToolkit (3D)、Markdig (Markdown 渲染)

## 配置文件说明

| 文件 | 说明 |
|------|------|
| `ProcessGroups.json` | 流程组配置 — 按产品/场景组织测试流程 |
| `ARVRRecipe.json` | Recipe 配置 — 各测试项的合格/不合格限值 |
| `ProjectARVRProFixConfig.json` | Fix 配置 — 各测试项的修正因子系数 |
| `ProjectLUXSummary.json` | 生产摘要 — 产线 / 工人 / 产能 / 良率 |

## 目录结构（快速参考）

| 目录/文件 | 说明 |
|-----------|------|
| `PluginConfig/` | 插件集成层（Plugin / Menu / WindowInstance） |
| `Process/` | 测试流程框架（接口 + 各测试类型实现） |
| `Process/W255/`, `Red/`, `Green/`, `Blue/` | 基础色彩测试 |
| `Process/Chessboard/`, `Chessboard55/` | 对比度测试 |
| `Process/AR/` | AR 专用测试（ChessboardAR / MTFHVAR / W255AR / W51AR） |
| `Process/VR/` | VR 专用测试（MTFH / MTFV） |
| `Process/MTFHV/` | 通用 MTF 测试 |
| `Process/Distortion/` | 畸变测试 |
| `Process/OpticCenter/` | 光学中心测试 |
| `Process/VID/` | 虚像距测试 |
| `Recipe/` | Recipe 管理（限值配置 + 编辑 UI） |
| `Fix/` | 修正因子管理（校准配置 + 编辑 UI） |
| `Services/` | Socket 通信服务 |
| `Properties/` | 多语言资源（zh-CN / en / fr / ja / ko / ru / zh-Hant） |

## 构建

```bash
# 构建
dotnet build Projects/ProjectLUX/ProjectLUX.csproj

# 构建 Release x64
dotnet build Projects/ProjectLUX/ProjectLUX.csproj -c Release -p:Platform=x64
```

## 维护者

ColorVision 项目团队
