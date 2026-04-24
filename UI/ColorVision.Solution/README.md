# ColorVision.Solution

> 版本: 1.5.5.1 | 目标框架: .NET 10.0 Windows

## 功能定位

解决方案和工程文件管理模块，类似 Visual Studio 的解决方案资源管理器。提供项目文件组织、多编辑器集成、RBAC 权限控制和终端集成。

## 主要功能

### 解决方案管理
- **创建/打开/保存** — .cvsln 格式解决方案文件
- **文件树视图** — 树形结构展示工程文件
- **文件监控** — FileSystemWatcher 实时同步

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

## 依赖关系

- **引用**: ColorVision.UI, ColorVision.UI.Desktop, ColorVision.Database, ColorVision.ImageEditor, AvalonEdit, WPFHexaEditor, WebView2, Markdig
- **被引用**: 作为顶层 UI 模块

## 构建

```bash
dotnet build UI/ColorVision.Solution/ColorVision.Solution.csproj
```
