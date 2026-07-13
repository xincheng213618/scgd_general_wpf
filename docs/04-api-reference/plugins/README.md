# 现有插件能力

本章说明当前 `Plugins/` 目录里真实存在的通用插件。插件开发方法见 [插件开发](../../02-developer-guide/plugin-development/README.md)，客户项目包见 [项目说明](../../00-projects/README.md)。

## 当前插件

| 插件 | 源码目录 | manifest Id | 能力 | 文档 |
| --- | --- | --- | --- | --- |
| Conoscope | `Plugins/Conoscope/` | `Conoscope` | VAM/锥镜图像观察、关注点、色域和对比度分析 | [Conoscope](./standard-plugins/conoscope.md) |
| Spectrum | `Plugins/Spectrum/` | `Spectrum` | 光谱仪连接、标定、测量、EQE、SQLite 结果 | [Spectrum](./standard-plugins/spectrum.md) |
| SystemMonitor | `Plugins/SystemMonitor/` | `SystemMonitor` | 性能监控、状态栏、磁盘/网络/进程信息 | [SystemMonitor](./standard-plugins/system-monitor.md) |
| EventVWR | `Plugins/EventVWR/` | `EventVWR` | Windows 事件错误查看、Dump 配置 | [EventVWR](./standard-plugins/eventvwr.md) |
| WindowsServicePlugin | `Plugins/WindowsServicePlugin/` | `WindowsServicePlugin` | CVWindowsService 安装、注册、MySQL/MQTT 配置 | [WindowsServicePlugin](./standard-plugins/windows-service.md) |

## 装载模型

插件由 `UI/ColorVision.UI/Plugins/PluginLoader.cs` 装载：

1. 扫描主程序输出目录下的 `Plugins/` 一级子目录。
2. 优先读取 `manifest.json`。
3. 根据 `dllpath` 找到插件 DLL。
4. 检查 `.deps.json` 中的 `ColorVision.*` 依赖版本。
5. 通过 `Assembly.LoadFrom(...)` 加载程序集。
6. 如果没有 manifest，才尝试“目录名同名 DLL”的兼容加载。

推荐交付形态：

```text
ColorVision/bin/x64/<Config>/net10.0-windows/Plugins/<PluginName>/
  <PluginName>.dll
  manifest.json
  README.md
  CHANGELOG.md
  PackageIcon.png        # 可选
```

## 打包

```powershell
Scripts\package_plugin.bat Spectrum
```

## 维护要求

- 新增插件时补 `Plugins/<Name>/README.md`、CHANGELOG、manifest 和本章插件页。
- 修改入口、权限、native 依赖、数据库、注册表或 Socket 行为时，同步更新对应插件页。
- 历史插件名不要写成当前能力；如果源码、项目文件和 manifest 不存在，就不出现在当前插件清单中。
