# ColorVision.UI

ColorVision.UI 是 ColorVision 系统的底层控件库，提供了丰富的 UI 组件、系统功能和框架支持。它是整个应用程序 UI 层的基础，包含菜单管理、配置系统、多语言支持、热键管理、日志系统等核心功能。

## 🎨 核心功能

### 菜单管理系统
- **动态菜单**: 支持运行时动态添加和移除菜单项
- **插件菜单**: 自动发现和集成插件菜单
- **菜单配置**: 支持菜单的可见性和权限控制
- **快捷键集成**: 菜单项与快捷键的自动关联

### 配置管理
- **配置持久化**: 自动保存和加载应用程序配置
- **设置界面**: 可视化的设置管理窗口
- **导入导出**: 配置的备份和恢复功能
- **多环境配置**: 支持开发、测试、生产环境配置

### 多语言支持
- **动态语言切换**: 运行时切换界面语言
- **资源本地化**: 支持文本、图像等资源本地化
- **语言包管理**: 插件化的语言包支持
- **区域设置**: 支持不同地区的格式化设置

### 热键系统
- **全局热键**: 系统级别的快捷键支持
- **局部热键**: 窗口或控件级别的快捷键
- **热键配置**: 用户自定义快捷键设置
- **冲突检测**: 自动检测和解决快捷键冲突

### 属性编辑器
- **PropertyGrid**: 强大的属性编辑控件
- **自定义编辑器**: 支持各种数据类型的编辑器
- **分组显示**: 属性的分类和分组显示
- **实时验证**: 属性值的实时验证和错误提示

## 🛠️ 系统封装功能

提供对于菜单，配置，设置，视窗，语言，主题，日志，热键，命令，工具栏，状态栏，对话框，下载，CUDA，加密等的封装，用户可以按照需求实现对映的UI，也可以直接使用封装好的UI。

- **窗口管理**: 视窗操作和状态管理
- **工具栏**: 可自定义的工具栏组件
- **状态栏**: 应用状态显示
- **对话框**: 标准化的对话框控件
- **下载管理**: 文件下载功能封装
- **CUDA支持**: GPU计算功能集成
- **加密功能**: 数据加密和解密工具

## 🚀 快速开始

### 基础初始化

```csharp
//读取配置
ConfigHandler.GetInstance();
//设置权限
Authorization.Instance = ConfigService.Instance.GetRequiredService<Authorization>();
//设置日志级别
LogConfig.Instance.SetLog();
//设置主题
this.ApplyTheme(ThemeConfig.Instance.Theme);
//设置语言
Thread.CurrentThread.CurrentUICulture = new System.Globalization.CultureInfo(LanguageConfig.Instance.UICulture);
```

### 窗口拖拽功能

设置窗口的实现移动到框架中来实现

```csharp
//设置窗口可拖动
this.MouseLeftButtonDown += (s, e) =>
{
    if (e.ButtonState == MouseButtonState.Pressed)
        this.DragMove();
};
```

### 属性编辑器使用

属性编辑窗口 PropertyGrid - 提供对于对象属性的编辑功能，支持属性分类，属性排序，属性过滤，属性编辑器自定义等功能。

## 📦 主要组件

### 基础架构
- **ConfigHandler**: 配置处理器
- **AssemblyHandler**: 程序集处理器  
- **FileProcessorFactory**: 文件处理器工厂

### UI组件
- **PropertyEditor**: 属性编辑器
- **Views**: 视图组件
- **Graphics**: 图形组件
- **Adorners**: 装饰器

### 系统服务
- **MenuManager**: 菜单管理器
- **LanguageManager**: 语言管理器
- **HotKeyManager**: 热键管理器
- **LogManager**: 日志管理器

## 📚 文档资源

- [详细技术文档](../../docs/ui-components/ColorVision.UI.md)
- [UI组件概览](../../docs/ui-components/UI组件概览.md)
- [用户界面指南](../../docs/user-interface-guide/)
- [主题开发指南](../../docs/ui-components/ColorVision.Themes.md)
    