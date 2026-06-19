# 插件开发概览

本页只说明当前仓库里真实可用的插件开发模型，避免继续沿用旧的通用化接口示例。

## 当前插件模型

ColorVision 的插件以独立目录部署在主程序运行目录下的 `Plugins/` 中。主程序启动时会扫描每个插件目录，读取 `manifest.json`，再按清单指定的 DLL 装载程序集。

当前代码中可以直接确认的几个关键点：

- 插件基础接口位于 `UI/ColorVision.Common/Interfaces/IPlugin.cs`
- 插件 manifest 模型位于 `UI/ColorVision.UI/Plugins/PluginManifest.cs`
- 插件装载逻辑位于 `UI/ColorVision.UI/Plugins/PluginLoader.cs`

## 关键组成

### 1. 插件接口

仓库里当前可见的基础接口非常轻量：

```csharp
public interface IPlugin
{
    string Header { get; }
    string Description { get; }
    void Execute();
}
```

如果只是做一个简单插件入口，通常从 `IPluginBase` 开始更方便。

### 2. manifest.json

插件目录通常需要提供 `manifest.json`。当前清单对象至少包含这些字段：

- `id`
- `manifest_version`
- `name`
- `version`
- `requires`
- `description`
- `dllpath`
- `author`
- `url`
- `entry_point`
- `icon`

其中最核心的是插件标识、描述和 DLL 路径；`entry_point` 在需要显式指定入口类型时使用。

### 3. 装载流程

主程序启动后，`PluginLoader` 会：

1. 扫描 `Plugins/` 下的插件目录。
2. 读取每个目录的 `manifest.json`。
3. 根据清单计算 DLL 路径。
4. 校验依赖与版本。
5. 装载程序集，并把插件信息写入内部缓存。

如果插件目录没有清单，平台仍会尝试按“目录名同名 DLL”的方式装载，但这不再是推荐形态。

## 推荐目录结构

```text
Plugins/
└── MyPlugin/
    ├── manifest.json
    ├── MyPlugin.csproj
    ├── MyPlugin.dll
    ├── README.md
    ├── CHANGELOG.md
    ├── Assets/
    └── Sources/ 或 *.cs/*.xaml
```

## 开发建议

### 平台内开发

- 在仓库内新建 `Plugins/<PluginId>/` 项目。
- 构建后把输出复制到主程序输出目录下的 `Plugins/<PluginId>/`。
- 优先参考现有标准插件的目录和打包方式。

### 外部独立开发

- 插件最终交付物应保持为一个可直接复制的完整目录。
- 不要重复打包平台主程序已自带的 `ColorVision.*.dll`。
- 第三方运行时依赖应和插件产物一起发布。

## 建议阅读顺序

1. 先看 [插件开发手册](./README.md)
2. 再看 [插件开发入门](./getting-started.md)
3. 需要理解装载和运行阶段时，再看 [插件生命周期](./lifecycle.md)
4. 想参考现成插件时，先看 [现有插件能力说明](../../04-api-reference/plugins/README.md)，再进入 [Conoscope](../../04-api-reference/plugins/standard-plugins/conoscope.md)、[Spectrum](../../04-api-reference/plugins/standard-plugins/spectrum.md) 或 [SystemMonitor](../../04-api-reference/plugins/standard-plugins/system-monitor.md)

## 说明

- 旧版文档里出现的 `IPluginContext`、异步生命周期接口和独立插件宿主模型，并不是当前仓库主路径里直接可见的基础接口。
- 如果文档和代码不一致，以 `UI/ColorVision.Common/Interfaces/IPlugin.cs`、`UI/ColorVision.UI/Plugins/PluginLoader.cs` 和现有插件项目为准。

