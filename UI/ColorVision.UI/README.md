# ColorVision.UI

> 目标框架：.NET 8 / .NET 10 Windows（以 `ColorVision.UI.csproj` 为准）

## 功能定位

核心 UI 框架库，提供插件系统、属性编辑器、快捷键系统、菜单系统、多语言支持等基础服务。是所有 UI 模块的基础依赖。

## 主要功能

### 插件系统 (Plugins/)
- **PluginLoader** — 从 Plugins 目录自动扫描和加载插件
- **PluginManifest** — 基于 manifest.json 的插件元数据
- **PluginExtractor** — 插件包解压
- **依赖检查** — 自动验证插件依赖的 ColorVision 版本

### 属性编辑器 (PropertyEditor/)
- **PropertyEditorHelper** — 属性编辑器工厂，根据类型自动选择编辑器
- **BoolPropertiesEditor** / **EnumPropertiesEditor** / **TextboxPropertiesEditor** — 基础类型编辑器
- **DictionaryEditor** / **ListEditor** — 集合类型编辑器
- **PropertyTreeNode** — 属性树节点，支持层次化展示

### 快捷键系统 (HotKey/)
- **GlobalHotKeyManager** — 系统级全局快捷键
- **WindowHotKeyManager** — 窗口级快捷键
- **HotKeys / Hotkey** — 快捷键定义（名称 + 按键 + 回调）
- **HotKeysSetting** — 编辑键位草稿，页面保存重新注册并更新配置内存，不直接写文件

定义身份、冲突、重载范围和清理责任见[快捷键契约](../../docs/04-api-reference/ui-components/hotkeys.md)。

### 产品搜索 (Serach/)

`SearchControl` 汇合静态候选、动态 Flow 节点和外部搜索入口；不是仓库全文或 AI 知识检索。刷新、过滤、执行和配置关闭行为见[搜索契约](../../docs/04-api-reference/ui-components/search.md)。

### 菜单系统 (Menus/)
- **MenuManager** — 菜单管理器，扫描 IMenuItem 实现构建菜单树
- **MenuItemBase** — 菜单项基类（Command 自动绑定 Execute()）
- **GlobalMenuBase** — 全局菜单基类（TargetName = Global）
- **MenuItemConstants** — 标准菜单 GUID（File/Edit/View/Tool/Help）
- **MenuFile / MenuEdit** — 标准文件和编辑菜单（打开/保存/复制/粘贴等）

### 多语言 (Languages/)

`LanguageManager` 发现资源文化，并在用户确认后保存配置、重启应用；不是统一的窗口热翻译服务。当前模块保留简体中文中性资源、en 和 zh-Hant，实际候选仍取决于部署资源和系统文化。回退与副作用见[界面语言契约](../../docs/04-api-reference/ui-components/localization.md)。

### 状态栏 (StatusBar/)

`StatusBarManager` 组合全局 provider 与活动文档项目，`StatusBarControl` 呈现绑定文本、图标和弹层。缓存、刷新、隐藏与关闭责任见[状态栏契约](../../docs/04-api-reference/ui-components/status-bar.md)。

### 日志 (LogImp/)
- **WindowLog** — 日志查看窗口
- **LogLoadState** — 日志加载状态

### Shell 集成 (Shell/)
- **JumpListManager** — Windows 跳转列表
- **ArgumentParser** — 命令行参数解析
- **TrayIconManager** — 系统托盘图标

### 文件处理
- **FileProcessorFactory** — 文件处理器工厂
- **AssemblyHandler** — 程序集加载处理

### 其他
- **Environments** — 环境变量管理
- **Adorners/** — 拖拽插入装饰器
- **Json/** — JSON 工具
- **Graphics/** — 图形工具
- **Pages/** — 页面组件
- **Sorts/** — 排序工具

## 依赖关系

- **引用**: ColorVision.Common, ColorVision.Themes, log4net, Newtonsoft.Json
- **被引用**: ColorVision.Database, ColorVision.Scheduler, ColorVision.SocketProtocol, ColorVision.ImageEditor, ColorVision.UI.Desktop, ColorVision.Solution

## 构建

```powershell
dotnet build .\UI\ColorVision.UI\ColorVision.UI.csproj -p:Platform=x64
```
