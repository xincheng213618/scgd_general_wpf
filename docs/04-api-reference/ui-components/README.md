# UI组件概览

本章现在只保留和当前代码实现一致的 UI 模块导读页，不再继续维护旧版总览里那种“版本兼容矩阵 + 示例代码 + 扩展教程”的混合写法。

## 怎么读这一章

如果你是第一次进入这个仓库，建议按下面顺序建立认知：

1. 先看 [ColorVision.UI](./ColorVision.UI.md)，理解配置、插件、菜单、属性编辑器和快捷键这些横切基础设施。
2. 再看 [ColorVision.Solution](./ColorVision.Solution.md) 和 [ColorVision.UI.Desktop](./ColorVision.UI.Desktop.md)，理解工作区壳层与桌面辅助窗口。
3. 与图像相关的能力，再顺着 [ColorVision.Core](./ColorVision.Core.md) -> [ColorVision.ImageEditor](./ColorVision.ImageEditor.md) 往上看。
4. 某个独立子系统需要深挖时，再进入对应单页。

## 模块地图

### 基础层

- [ColorVision.Common](./ColorVision.Common.md)：MVVM、共享接口、状态栏元数据和粗粒度权限基础。
- [ColorVision.Core](./ColorVision.Core.md)：原生图像/视频能力桥接层，负责 `HImage` 和 P/Invoke 导出面。

### 功能层

- [ColorVision.Database](./ColorVision.Database.md)：数据库浏览器、Provider 注册、SQLite 日志与通用 DAO。
- [ColorVision.ImageEditor](./ColorVision.ImageEditor.md)：`ImageView`、`DrawCanvas`、编辑器工具、打开器和图像交互主链。
- [ColorVision.Scheduler](./ColorVision.Scheduler.md)：Quartz 调度器、任务恢复、执行历史和管理窗口。
- [ColorVision.SocketProtocol](./ColorVision.SocketProtocol.md)：本地 TCP 服务、请求分发、消息历史和管理窗口。

### 壳层与工作区

- [ColorVision.Solution](./ColorVision.Solution.md)：工作区、编辑器、终端、多图查看和 Solution 侧本地 RBAC。
- [ColorVision.UI](./ColorVision.UI.md)：UI 基础设施集合，涵盖配置、插件、菜单、属性编辑器、多语言和日志等横切能力。
- [ColorVision.UI.Desktop](./ColorVision.UI.Desktop.md)：设置窗口、向导、菜单管理、配置管理和其他桌面辅助窗口。

### 主题层

- [ColorVision.Themes](./ColorVision.Themes.md)：主题资源字典、主题切换入口和窗口外观支持。

## 当前几个容易混淆的边界

- `ColorVision.UI` 不是单一控件库，而是横切的 UI 基础设施集合。
- `ColorVision.Solution` 不是“只有解决方案文件树”，它还承接工作区壳层和本地 RBAC 子模块。
- `ColorVision.UI.Desktop` 不是整个产品主入口，它更像桌面辅助窗口和管理工具集合。
- `ColorVision.Core` 不是高层托管图像框架，而是原生互操作层。
- `ColorVision.ImageEditor` 不是纯显示控件，它会把打开器、工具、图元、overlay 和运行时服务编排在一起。

## 继续阅读建议

### 想看配置、菜单、权限和插件

先看：

- [ColorVision.UI](./ColorVision.UI.md)
- [ColorVision.Solution](./ColorVision.Solution.md)
- [ColorVision.Common](./ColorVision.Common.md)

### 想看图像链路

先看：

- [ColorVision.Core](./ColorVision.Core.md)
- [ColorVision.ImageEditor](./ColorVision.ImageEditor.md)
- [ColorVision.Themes](./ColorVision.Themes.md)

### 想看桌面工具和运维辅助功能

先看：

- [ColorVision.Database](./ColorVision.Database.md)
- [ColorVision.Scheduler](./ColorVision.Scheduler.md)
- [ColorVision.SocketProtocol](./ColorVision.SocketProtocol.md)
- [ColorVision.UI.Desktop](./ColorVision.UI.Desktop.md)