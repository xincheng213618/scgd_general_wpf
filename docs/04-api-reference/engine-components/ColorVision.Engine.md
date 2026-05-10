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