# 架构运行时

本页只描述当前代码里能看见的主程序运行时链路，不维护脱离实现的统一启动时序图。

## 运行时分支

当前桌面程序不是“一次性初始化完再显示主界面”的单一模型。常见分支是：

| 分支 | 行为 |
| --- | --- |
| 命令行文件处理 | `input`、`export` 等分支处理完成后直接返回 |
| 正常桌面启动 | 初始化配置、日志、主题、语言、插件，再进入向导或启动窗口 |
| 异常恢复 | 上次异常退出后，先询问是否禁用插件再继续启动 |

## 主程序启动链路

从 `ColorVision/App.xaml.cs` 看，常见启动顺序是：设置工作目录并预加载 DLL，初始化配置/日志/主题/语言，解析命令行，处理文件分支，执行单实例检查，必要时清理僵尸进程，按上次启动状态决定是否禁用插件，初始化 WinForms 视觉样式，最后显示 `WizardWindow` 或 `StartWindow`。

这里最重要的不是记住所有步骤，而是知道启动并不总会直接进主窗口。

## 插件加载

插件会在进入向导或启动窗口前决定是否加载。关键点：

| 动作 | 说明 |
| --- | --- |
| 扫描 | 扫描 `Plugins/` 目录 |
| 读取 | 读取 `manifest.json` 和可选 `.deps.json` |
| 校验 | 检查 `ColorVision.*` 依赖版本 |
| 装载 | 用 `Assembly.LoadFrom(...)` 装载插件程序集 |
| 恢复 | 上次异常退出时，启动会先询问是否禁用插件 |

## 主工作区对象

| 对象 | 作用 | 失败时表现 |
| --- | --- | --- |
| `ServiceManager` | 数据库连接可用后加载服务树，组织 `TypeServices`、`TerminalServices`、`DeviceServices`、`GroupResources` | 设备树为空、设备控件未生成 |
| `MQTTRCService` | 保持注册中心连接，查询服务令牌，更新服务状态并同步设备服务对象 | 流程跑不起来、设备在线但状态不更新 |
| `TemplateControl` | 数据库连接可用后扫描已加载程序集中的 `IITemplateLoad`，调用 `Load()` 注册模板 | 模板不可见、模板不能编辑 |

模板是否可见依赖两个前提：相关程序集已经加载，数据库连接已建立。

## 流程执行链

当用户进入流程窗口后，运行时主链延伸为：

```text
DisplayFlow -> FlowControl -> FlowEngineLib -> MQTTRCService -> 设备/算法服务
```

执行过程中持续更新当前运行节点、执行日志、批次进度、节点记录和消息记录。

## 常见失败点

| 阶段 | 先查 |
| --- | --- |
| 启动 | 插件依赖、上次异常恢复分支、命令行分支是否提前返回 |
| 服务准备 | 数据库是否连通，服务树或模板是否装载，注册中心/MQTT 是否准备好 |
| 执行 | 流程模板是否选中，起始节点是否存在，设备状态是否已同步为可执行 |

## 代码入口

| 主题 | 入口 |
| --- | --- |
| 主启动 | `ColorVision/App.xaml.cs` |
| 插件加载 | `UI/ColorVision.UI/Plugins/PluginLoader.cs` |
| 服务树 | `Engine/ColorVision.Engine/Services/ServiceManager.cs` |
| 注册中心 | `Engine/ColorVision.Engine/Services/RC/MQTTRCService.cs` |
| 模板注册 | `Engine/ColorVision.Engine/Templates/TemplateContorl.cs` |
