# ColorVision.Engine

本页只描述当前仓库里真实可用的 `ColorVision.Engine` 模块，不再继续维护“完整 API 表 + 统一分层蓝图 + 伪示例”式旧稿。

## 先看这个模块现在是什么

按当前源码状态，`ColorVision.Engine` 不是一个单纯的算法库，而是 ColorVision 主程序最核心的引擎拼装层。它当前至少负责：

- 设备与服务对象的宿主侧抽象。
- 模板系统的加载、编辑和持久化。
- MQTT 请求、心跳和消息记录。
- FlowEngineLib 在主程序中的 UI 与模板桥接。
- 算法显示层与模板编辑器之间的连接。

因此它更接近“运行时引擎宿主层”，而不是把所有业务都直接算在本地的单体模块。

## 当前最关键的文件

- `Engine/ColorVision.Engine/Templates/TemplateContorl.cs`
- `Engine/ColorVision.Engine/Templates/Jsons/ITemplateJson.cs`
- `Engine/ColorVision.Engine/Templates/Jsons/EditTemplateJson.xaml.cs`
- `Engine/ColorVision.Engine/Templates/Flow/FlowEngineManager.cs`
- `Engine/ColorVision.Engine/Templates/Flow/DisplayFlow.xaml.cs`
- `Engine/ColorVision.Engine/Services/DeviceService.cs`
- `Engine/ColorVision.Engine/Services/Devices/DeviceServiceFactory.cs`
- `Engine/ColorVision.Engine/Services/Core/MQTTServiceBase.cs`
- `Engine/ColorVision.Engine/Services/RC/MQTTRCService.cs`
- `Engine/ColorVision.Engine/Templates/POI/AlgorithmImp/AlgorithmPOI.cs`
- `Engine/ColorVision.Engine/Templates/ARVR/MTF/AlgorithmMTF.cs`

如果只是想弄清主引擎怎么组织模板、设备、消息链和流程，这些代码已经覆盖主干。

## 当前控制面怎么分块

### 模板加载与模板注册

`TemplateControl` 是当前模板体系的总入口。它会在 MySQL 可用后扫描所有程序集里的 `IITemplateLoad` 实现并执行 `Load()`，再把模板实例注册到 `ITemplateNames`。

这意味着模板系统当前不是手写静态列表，而是靠：

- 初始化器触发
- 程序集扫描
- 模板实例注册表

三步串起来的。

### JSON 模板编辑

`ITemplateJson<T>` 展示了当前 JSON 模板的真实落点：

- 模板数据从 MySQL 读取
- 模板对象通过 `Activator.CreateInstance` 包装成参数对象
- 保存与删除也直接回写数据库

对应的编辑器 `EditTemplateJson` 则提供：

- 文本模式
- 属性编辑模式
- 注释视图切换
- 外部 JSON 校验网站快捷入口

这说明引擎层当前并不只是存模板，还直接承载了模板编辑 UI 的一部分。

### 流程桥接层

`FlowEngineManager` 和 `DisplayFlow` 是 `ColorVision.Engine` 与 `FlowEngineLib` 的桥接面。它们当前负责：

- 初始化 Flow 的 MQTT 默认配置
- 维护流程模板列表和当前选择
- 用 Base64 数据把模板加载进 `FlowEngineControl`
- 结合 `MqttRCService` 的 service token 刷新可用服务节点
- 提供流程编辑、模板编辑、批量记录查看等 UI 操作

所以主程序里的流程功能不是由 `FlowEngineLib` 单独完成的，而是要经过这层桥接代码才能真正进入窗口和模板体系。

### 设备与服务抽象

`DeviceService` 是当前宿主侧设备对象的基础抽象，负责：

- 树节点行为
- 图标与上下文菜单
- 导入导出配置
- 重置、重启和属性命令
- 与 MQTT 服务对象或显示控件的挂接

而 `DeviceServiceFactoryRegistry` 则把 Camera、PG、Spectrum、SMU、Sensor 等服务类型统一注册成工厂。

这说明当前设备实例化已经不再是 scattered switch-case，而是中心化工厂注册。

### MQTT 运行时

`MQTTServiceBase` 是当前消息链最重要的宿主基类。它负责：

- 订阅/发布 MQTT 消息
- 维护 `MsgRecord`
- 基于心跳判断 `IsAlive`
- 处理超时和回包状态

`MqttRCService` 则进一步承担注册中心客户端角色，负责：

- RC 主题构建
- 重新注册
- 服务令牌缓存
- RC 连接状态

引擎层很多“服务是否在线、流程能否刷新、设备 token 从哪里来”的问题，最终都要回到这层。

## 算法在这一层当前扮演什么角色

从 `AlgorithmPOI` 和 `AlgorithmMTF` 这些实现看，`ColorVision.Engine` 里的算法类当前更多是：

- 打开模板编辑器
- 组织模板选择状态
- 组装 MQTT 参数
- 调用设备服务发布命令

也就是说，这一层的算法对象通常是“显示和命令适配器”，而不是直接在本地完成图像计算的纯算法内核。

## 业务交接验收表

接手 `ColorVision.Engine` 时，不能只看主项目能否编译。这个模块把数据库、设备、模板、MQTT、Flow、结果和 UI 都接在一起，最小交接要按链路验。

| 验收项 | 要看哪里 | 通过标准 |
| --- | --- | --- |
| 主目标框架 | `Directory.Build.props`、`ColorVision.Engine.csproj` | 主宿主按 `net10.0-windows`、x64 路径构建，UI/Engine 项目引用或 NuGet 包引用能解析 |
| UI 与 Engine 依赖 | `ColorVision.Database`、`SocketProtocol`、`ImageEditor`、`Scheduler`、`Solution`、`UI.Desktop` | 主程序能打开数据库、Socket、图像、调度、项目工作区相关入口 |
| 模板初始化 | `TemplateInitializer`、`TemplateControl.GetInstance()`、`MySqlInitializer` | MySQL 连接后能扫描 `IITemplateLoad`，`ITemplateNames` 有真实模板项 |
| JSON 模板编辑 | `ITemplateJson<T>`、`EditTemplateJson.xaml.cs`、`Templates/Jsons/**/*.schema.json` | 模板能读取、编辑、保存、删除，schema 文件随输出发布 |
| 设备工厂 | `DeviceServiceFactoryRegistry`、`ServiceTypes`、`Services/Devices/**` | Camera、PG、Spectrum、SMU、Sensor、Algorithm、Calibration 等类型能从资源创建服务对象 |
| MQTT 命令链 | `MQTTServiceBase`、`MsgRecord`、`MessagesListManager` | 发命令后能看到 `MsgRecord` 状态，Success/Fail/Timeout 能被 UI 或调用方感知 |
| RC 服务链 | `MqttRCService`、service token 缓存、RC 主题 | 注册中心连接后能刷新可用服务，Flow 节点能拿到对应服务 token |
| Flow 桥接 | `FlowEngineManager`、`DisplayFlow`、`TemplateFlow` | 流程模板能从 Base64 加载、编辑、保存、运行，并能刷新服务节点 |
| 结果展示 | `IViewResult`、`IResultHandleBase`、`Services/Devices/Algorithm/Views/AlgorithmView.xaml.cs` | 历史结果能打开，ImageEditor overlay、表格明细和结果类型匹配 |
| 交付资源 | `Assets/Image/*`、`Templates/Jsons/**/*.schema.json`、工具 exe | 发布输出包含图标、schema、Everything/WinRAR/串口/USB 工具等需要的文件 |

## 现场故障首查

| 现象 | 先查哪里 | 判断要点 |
| --- | --- | --- |
| 模板列表为空 | MySQL 连接、`TemplateInitializer.Dependencies`、`Application.Current.GetAssemblies()` | `TemplateControl` 只有在 MySQL 可用后才扫描 `IITemplateLoad` |
| 新模板类写了但界面看不到 | 是否实现 `IITemplateLoad`、是否调用 `TemplateControl.AddITemplateInstance` | 只写参数类不够，必须进入模板注册表 |
| JSON 模板保存失败 | `ITemplateJson<T>` 数据库读写、schema 输出、模板名重复 | 先确认表数据和 schema 是否存在，再看编辑器 |
| 设备资源存在但设备树没有服务对象 | `SysResourceModel.Type`、`ServiceTypes`、`DeviceServiceFactoryRegistry` | 没有注册 factory 时资源不会稳定变成设备服务 |
| 设备命令一直 Timeout | `MQTTServiceBase` 主题、服务 token、`MqttRCService` 连接状态、`MsgRecord` | 先看消息记录和 RC token，再查设备业务窗口 |
| Flow 打开了但节点没有服务 | `FlowEngineManager`、`MqttRCService` 服务列表、`DisplayFlow` 刷新逻辑 | Flow 节点可视化不等于服务 token 已绑定 |
| Flow 运行结束但项目拿不到结果 | `FlowCompleted`、`ViewResultAlg`、项目 `Process/` 映射 | Engine 负责通用结果，客户字段通常在项目包映射 |
| 结果窗口打开但 overlay 不对 | `IResultHandleBase`、模板目录 DAO、`ColorVision.ImageEditor` 图元 | 先确认 handler 命中结果类型，再查坐标转换和图像路径 |
| 打包后图标、schema 或工具缺失 | `ColorVision.Engine.csproj` 的 `Resource` / `None Update` | 本地源码存在不代表发布输出存在 |

## 当前几个最容易写错的点

### 它不是“所有算法都在本地执行”的模块

当前很多算法类做的其实是把模板、文件名和设备信息组装成 MQTT 请求，再交给设备或服务端处理。继续把这层写成纯本地算法实现，会和真实控制链不符。

### 模板系统离不开初始化和数据库

`TemplateControl` 依赖 MySQL 初始化之后的程序集扫描；`ITemplateJson<T>` 也直接和数据库交互。把它写成“完全本地静态模板集”会丢失关键前提。

### 流程功能并不全在 FlowEngineLib

主程序里真正能编辑、选择和运行 Flow 模板，还需要 `Templates/Flow/` 这层桥接代码。只描述 FlowEngineLib，会漏掉宿主侧的实际控制面。

### 设备服务实例化当前是注册中心化的

`DeviceServiceFactoryRegistry` 已经是当前真实的实例化入口。继续沿用旧文档里的分散构造描述，会把扩展点写偏。

## 推荐阅读顺序

1. `Engine/ColorVision.Engine/Templates/TemplateContorl.cs`
2. `Engine/ColorVision.Engine/Templates/Jsons/ITemplateJson.cs`
3. `Engine/ColorVision.Engine/Services/DeviceService.cs`
4. `Engine/ColorVision.Engine/Services/Devices/DeviceServiceFactory.cs`
5. `Engine/ColorVision.Engine/Services/Core/MQTTServiceBase.cs`
6. `Engine/ColorVision.Engine/Services/RC/MQTTRCService.cs`
7. `Engine/ColorVision.Engine/Templates/Flow/FlowEngineManager.cs`
8. `Engine/ColorVision.Engine/Templates/Flow/DisplayFlow.xaml.cs`

这样能先看到模板与服务宿主层，再连接消息链和流程桥接层。

## 继续阅读

- [docs/04-api-reference/engine-components/FlowEngineLib.md](./FlowEngineLib.md)
- [docs/03-architecture/components/templates/analysis.md](../../03-architecture/components/templates/analysis.md)
- [docs/04-api-reference/algorithms/overview.md](../algorithms/overview.md)
