# 首次运行指南

这页只保留第一次启动 ColorVision 最需要确认的事：怎么启动、先检查哪些状态、遇到问题先看哪里。菜单名会随版本和插件变化，现场以当前程序界面为准。

## 启动方式

| 场景 | 操作 |
| --- | --- |
| 已安装版本 | 从开始菜单或桌面快捷方式启动 `ColorVision` |
| 指定安装目录 | 在安装目录运行 `ColorVision.exe` |
| 源码调试 | 在仓库根目录运行 `dotnet run --project ColorVision/ColorVision.csproj` |

源码调试前先执行：

```powershell
dotnet restore
dotnet build ColorVision/ColorVision.csproj -p:Platform=x64
```

## 首次启动会做什么

| 动作 | 说明 |
| --- | --- |
| 初始化配置 | 创建用户配置、窗口配置、设备/流程相关状态 |
| 初始化日志 | 建立日志输出，后续排障先看日志 |
| 扫描插件 | 读取 `Plugins/` 下的插件目录和 manifest |
| 初始化数据库/服务 | 按配置连接 MySQL、MQTT 或本地 SQLite 模块 |
| 打开主窗口 | 加载菜单、工具栏、状态栏、工作区和已启用插件 |

具体目录以当前版本的 `Environments` 和配置文件为准，不在这里硬编码路径。

## 第一次先看这几项

| 检查项 | 通过标准 |
| --- | --- |
| 主窗口能打开 | 没有卡在启动页或直接退出 |
| 日志可访问 | 能找到本次启动日志，错误能定位到模块 |
| 插件管理正常 | 插件列表可打开，异常插件有明确错误 |
| 数据库状态明确 | 未配置时有提示，已配置时能连接或给出失败原因 |
| 设备列表正常 | 无设备时为空也可以，但不应报加载异常 |
| 图像打开正常 | 能打开一张普通图片并显示 |

## 最小操作

1. 打开主程序。
2. 打开插件管理或关于/帮助类入口，确认插件加载状态。
3. 打开日志窗口或日志目录，确认本次启动没有关键错误。
4. 打开一张普通图片，确认图像显示链可用。
5. 如果要跑流程，先确认数据库、MQTT、设备服务和流程模板都已配置。

## 常见问题

| 现象 | 先查 |
| --- | --- |
| 首次启动慢 | 插件扫描、数据库/MQTT 连接等待、日志中的加载耗时 |
| 启动后直接退出 | 最新日志、缺失 DLL、native runtime、配置文件格式 |
| 插件未加载 | 插件目录、`manifest.json`、`dllpath`、依赖版本、主程序 DLL 版本 |
| 图像打不开 | 文件格式、`ColorVision.Core` native DLL、OpenCV runtime |
| 数据库连接失败 | MySQL 配置、账号权限、网络、服务是否启动 |
| 界面显示异常 | DPI/缩放、窗口布局配置、主题资源 |

## 下一步

| 目标 | 入口 |
| --- | --- |
| 熟悉界面 | [主窗口导览](../01-user-guide/interface/main-window.md) |
| 图像操作 | [图像编辑器](../01-user-guide/image-editor/overview.md) |
| 设备使用 | [设备概览](../01-user-guide/devices/overview.md) |
| 工作流程 | [工作流程概览](../01-user-guide/workflow/README.md) |
| 常见问题 | [故障排除](../01-user-guide/troubleshooting/common-issues.md) |

## 技术支持

- 查看本次启动日志。
- 记录 ColorVision 版本、插件版本、复现步骤和错误截图。
- 到 [GitHub Issues](https://github.com/xincheng213618/scgd_general_wpf/issues) 提交问题。
