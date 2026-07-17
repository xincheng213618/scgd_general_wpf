# ColorVision.Solution

> 版本: 1.5.5.1 | 目标框架: .NET 10.0 Windows

## 功能定位

解决方案和工程文件管理模块，类似 Visual Studio 的解决方案资源管理器。提供项目文件组织、多编辑器集成、RBAC 权限控制和终端集成。

## 主要功能

### 解决方案管理
- **创建/打开/保存** — .cvsln 格式解决方案文件
- **VS 解决方案导入** — 通过微软官方 SolutionPersistence 模型只读打开 .sln/.slnx，项目、虚拟文件夹和解决方案项映射到私有工作区；源控制结构禁止本地增删/移动，源文件变化时自动重新导入并保留本地启动项/活动配置
- **导入配置合并** — 记录源项目配置基线；未修改映射随源更新，本地覆盖保留，源新增/删除的配置自动同步
- **异步工作区切换** — 外部解决方案解析和刷新支持取消；并发打开以后到请求为准，新工作区完全就绪前保留当前工作区
- **工作区关闭事务** — 切换或关闭解决方案前统一处理工作区文档；未保存文档取消关闭时保留当前工作区，并提供全局“关闭解决方案”命令
- **工程 Provider** — 原生支持 .cvproj，并以只读方式接入 SDK/传统 MSBuild 工程（.csproj、.fsproj、.vbproj）
- **文件树视图** — 树形结构展示工程文件
- **文件监控** — 根级递归 FileSystemWatcher 实时同步，避免每个文件夹单独创建监听器
- **缓存加载** — SolutionCache 缓存目录结构，减少大型工程的重复枚举

### 编辑器系统 (Editor/)
| 编辑器 | 文件扩展名 | 说明 |
|--------|-----------|------|
| TextEditor | .txt, .log, .cs, ... | AvalonEdit 代码编辑 |
| ImageEditor | .png, .jpg, .bmp, ... | 图像查看/编辑 |
| HexEditor | .bin, .dat, ... | 十六进制编辑 |
| WebEditor | .html, .url, ... | WebView2 预览 |
| Model3DEditor | .obj, .stl | 3D 模型查看（嵌入 ModelViewer3DControl） |
| ProjectEditor | .cvsln, .json | 项目配置编辑 |

**Model3DEditor 内存管理**
- 使用命名委托 + `Closing` 事件中主动取消订阅，打破 lambda 闭包引用链
- 关闭时调用 `DisposeViewer()` 释放 3D 资源（网格缓冲区、材质纹理）
- 置空 `LayoutDocument.Content` 断开内容引用，确保 GC 可回收

### RBAC 权限系统 (Rbac/)
- **用户/角色/权限** — 完整的 RBAC 模型
- **多租户** — TenantEntity 支持
- **会话管理** — SessionEntity + 审计日志
- **登录/注册窗口** — LoginWindow / RegisterWindow
- **密码安全** — PasswordHashing (BCrypt)

### 多图像查看器 (MultiImageViewer/)
- 文件夹内多图预览
- 缩略图缓存管理 (ThumbnailCacheManager)

### 最近文件 (RecentFile/)
- 最近打开的解决方案列表
- 注册表持久化 (RegistryPersister)

### 终端集成 (Terminal/)
- 基于 Windows ConPTY 的内置终端
- VT100/xterm 转义序列解析
- 持久化命令历史

### 其他
- **MarkdownViewWindow** — Markdown 预览（Markdig）
- **EditableTextBlock** — 可点击编辑的文本块
- **WorkspaceManager** — 多工作区管理

## 稳定性加固记录

### 2026-05-11
- **权限安全默认值**: 未登录或登录态无效时保持 `Guest` 权限，新建普通用户默认 `User`，避免配置缺失导致管理员权限。
- **工程切换生命周期**: 打开新解决方案前释放旧 `SolutionExplorer`、节点树、缓存和文件监听器，防止旧工程继续响应文件事件。
- **文件树同步**: 改为单个根级递归 `FileSystemWatcher`，统一处理创建、删除、重命名和变更事件；删除目录时同步清理缓存子项。
- **资源释放**: `TerminalControl` 实现 `IDisposable`，终端面板在应用退出时释放 shell 和计时器；多图像查看器释放流程改为幂等。
- **缩略图加载**: 缩略图并发加载加入限流，避免大目录一次性占满线程池和磁盘 IO。
- **插件反射容错**: 编辑器、文件元数据、文件夹元数据和模板注册统一处理 `ReflectionTypeLoadException`，单个插件加载失败不会阻断全局注册。
- **构建可移植性**: 移除项目文件中的本机 NuGet 绝对路径，降低跨机器构建失败风险。

## 依赖关系

- **引用**: ColorVision.UI, ColorVision.UI.Desktop, ColorVision.Database, ColorVision.ImageEditor, AvalonEdit, WPFHexaEditor, WebView2, Markdig
- **被引用**: 作为顶层 UI 模块

## 构建

```bash
dotnet build UI/ColorVision.Solution/ColorVision.Solution.csproj
```
