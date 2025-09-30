# ColorVision

## 目录

- [一、项目定位](#一项目定位)
- [二、功能总览](#二功能总览)
- [三、架构概览](#三架构概览)
- [四、目录结构示例](#四目录结构示例)
- [五、核心技术点](#五核心技术点)
- [六、安装与构建](#六安装与构建)
- [七、运行与快速开始](#七运行与快速开始)
- [八、配置系统](#八配置系统)
- [九、动态属性编辑器扩展](#九动态属性编辑器扩展)
- [十、插件开发指南](#十插件开发指南)
- [十一、任务调度](#十一任务调度)
- [十二、算法与结果展示](#十二算法与结果展示)
- [十三、常见图像交互](#十三常见图像交互)
- [十四、日志与调试](#十四日志与调试)
- [十五、国际化与本地化](#十五国际化与本地化)
- [十六、性能与优化](#十六性能与优化)
- [十七、安全与权限](#十七安全与权限)
- [十八、Roadmap](#十八roadmap)
- [十九、贡献指南](#十九贡献指南)
- [二十、许可证](#二十许可证)
- [二十一、致谢](#二十一致谢)
- [附录](#附录)

## 一、项目定位

ColorVision 是一个基于 WPF/.NET 的模块化桌面系统，专注于提供专业、高效的图像处理和视觉检测解决方案。

### 核心价值

- **模块化架构** - 清晰的分层设计，支持Engine、UI、Plugins、Projects多层解耦
- **可插拔插件体系** - 基于 IPlugin 接口的动态加载机制，支持Assembly扫描和运行时装载
- **图像算法可视化** - DrawingVisual图层叠加体系，支持算法结果的实时渲染和标注
- **调度/任务系统** - 集成Quartz.NET的企业级任务调度，支持Cron表达式和批量控制
- **硬件设备抽象** - MQTT/本地协议统一抽象，支持相机、光谱仪等多类设备集成

## 二、功能总览

### 核心框架
- **配置系统** - IConfig接口驱动的依赖注入配置持久化
- **MVVM** - 基于PropertyChanged的数据绑定和命令系统
- **插件加载** - Assembly扫描、manifest.json约定、后拷贝部署
- **日志** - log4net集成的分级日志管理

### 图像引擎 (ImageEditor)
- **图像打开** - 多格式图像加载和预览
- **绘图层 (DrawingVisual)** - 多图层叠加渲染系统
- **标注/测量** - ROI绘制、几何测量、文本标注
- **算法结果叠加** - ViewResultAlg结果可视化展示

### 动态属性编辑器
- **反射驱动** - 基于PropertyInfo的动态属性发现
- **分类** - Category特性支持的属性分组
- **Brush/Font/Enum/Command 支持** - 内置常用类型编辑器
- **自定义编辑器扩展点** - IPropertyEditor接口和PropertyEditorTypeAttribute

### 调度与任务系统 (Scheduler)
- **Quartz 集成** - 企业级任务调度引擎
- **任务管理** - 任务生命周期管理和状态监听
- **Cron 支持** - 灵活的时间表达式配置
- **监控** - 任务执行历史和性能监控

### 设备与算法服务
- **MQTT/本地** - 统一的设备通信抽象层
- **第三方算法接入** - 标准化的算法服务接口
- **结果管理** - 算法执行结果的存储和检索
- **POI模板** - 点位模板管理和批量处理

### 插件体系
- **Pattern** - 图案检测插件
- **WindowsServicePlugin** - Windows服务集成
- **SystemMonitor** - 系统性能监控
- **EventVWR** - 事件查看器

### 项目业务示例
- **ARVR/KB/LUX/BlackMura** - 面向特定应用场景的项目层扩展

## 三、架构概览

### 分层架构

系统采用清晰的分层架构设计：

- **UI层** - 用户界面和交互逻辑，包含主题管理、多语言支持、热键系统
- **Engine层** - 核心业务引擎，包含流程管理、设备服务、算法集成、MQTT通信
- **Plugins** - 可插拔的扩展功能模块
- **Projects** - 面向具体业务的项目实现
- **Core/Common** - 基础库和通用组件
- **Themes** - 主题资源和样式管理
- **Scheduler** - 基于Quartz.NET的任务调度
- **Solution管理** - 工程文件和解决方案管理

### 插件发现与加载机制

- **Assembly 扫描** - 自动扫描Plugins目录下的程序集
- **约定/manifest.json** - 支持插件元数据描述和依赖声明
- **后拷贝部署** - PostBuild事件自动复制插件到运行目录

### 配置与扩展点

- **IConfig** - 配置对象的统一接口，支持依赖注入
- **IPropertyEditor** - 自定义属性编辑器扩展点
- **IViewResult** - 算法结果视图接口
- **IResultHandleBase** - 结果处理器基类
- **菜单/上下文扩展** - 支持动态菜单和右键菜单扩展

### DrawingVisual 图像叠加体系

- **Rect/Line/Text** - 基础几何图元
- **自定义图元** - 继承DrawingVisualBase的自定义渲染对象
- **多层合成** - 支持图层优先级和透明度控制

### 多目标框架支持

- **net8.0-windows** - 主要目标框架
- **net6.0-windows** - 兼容支持
- **net48** - 部分兼容库支持

## 四、目录结构示例

```
ColorVision/
├── ColorVision/                    # 主程序入口
│   ├── App.xaml                   # 应用程序定义
│   ├── MainWindow.xaml            # 主窗口
│   └── EntryClass.cs              # 程序启动类
├── Engine/                        # 核心引擎
│   ├── ColorVision.Engine/        # 主引擎模块
│   │   ├── Services/              # 设备服务
│   │   ├── Templates/             # 算法模板
│   │   └── MQTT/                  # 通信协议
│   ├── cvColorVision/             # 视觉处理核心
│   └── FlowEngineLib/             # 流程引擎库
├── UI/                            # 用户界面
│   ├── ColorVision.UI/            # UI框架
│   │   ├── PropertyEditor/        # 属性编辑器
│   │   └── Plugins/               # 插件管理
│   ├── ColorVision.ImageEditor/   # 图像编辑器
│   │   └── Draw/                  # 绘图组件
│   ├── ColorVision.Themes/        # 主题管理
│   └── ColorVision.Scheduler/     # 调度器UI
├── Plugins/                       # 扩展插件
│   ├── Pattern/                   # 图案检测
│   ├── SystemMonitor/             # 系统监控
│   └── EventVWR/                  # 事件查看器
├── Projects/                      # 业务项目
│   ├── ProjectARVR/               # AR/VR项目
│   ├── ProjectKB/                 # KB项目
│   └── ProjectLUX/                # LUX项目
├── docs/                          # 文档资源
└── Scripts/                       # 构建脚本
```

## 五、核心技术点

### 动态属性编辑器机制

- **反射机制** - 运行时属性发现和类型分析
- **属性特性 (Attribute)** - PropertyEditorTypeAttribute、CategoryAttribute等
- **分类 (Category)** - 属性分组和层次化显示
- **可见性控制** - PropertyVisibilityAttribute动态控制属性显示
- **自定义编辑器缓存** - CustomEditorCache提高性能

```csharp
[PropertyEditorType(typeof(ColorEditor))]
[Category("外观")]
public Color BackgroundColor { get; set; }
```

### 插件部署

- **PostBuild 复制** - 自动复制插件到运行目录
- **运行时装载** - Assembly.LoadFrom动态加载
- **命名约定** - 基于接口约定的插件发现

### 图像组件

- **多图层叠加** - DrawingVisual层次化渲染
- **可视化算法结果** - ViewResultAlg结果展示
- **POI 标注** - 点位标记和批量处理

### 任务调度

- **Quartz Scheduler 封装** - 企业级调度引擎集成
- **任务状态监听** - 生命周期事件处理
- **批量控制** - 任务组管理和批量操作

### 算法结果浏览

- **ViewResultAlg** - 统一的结果数据模型
- **结果解析** - 多格式结果数据解析
- **CSV 导出** - 结构化数据导出
- **上下文菜单扩展** - 右键菜单动态扩展

### 扩展接口

- **IConfigSettingProvider** - 配置提供者接口
- **IImageContentMenuProvider** - 图像上下文菜单提供者
- **IPropertyEditor** - 属性编辑器接口
- **IResultHandleBase** - 结果处理器基类

## 六、安装与构建

### 环境要求

- **Windows 10/11** - 操作系统要求
- **.NET SDK (6.0/8.0)** - 开发和运行环境
- **Visual Studio 2022** - 推荐开发IDE
- **可选 MySQL** - 数据库支持

### 克隆与还原依赖

```bash
# 克隆仓库
git clone https://github.com/username/ColorVision.git
cd ColorVision

# 还原依赖
dotnet restore
```

### 构建命令

```bash
# 构建解决方案
dotnet build

# 构建发布版本
dotnet build -c Release

# 运行主程序
dotnet run --project ColorVision/ColorVision.csproj
```

### 自定义插件编译

```bash
# 构建插件项目
dotnet build Plugins/YourPlugin/YourPlugin.csproj

# 插件会自动复制到输出目录的Plugins文件夹
```

## 七、运行与快速开始

### 启动主程序

```bash
# 直接运行
.\ColorVision.exe

# 或通过dotnet运行
dotnet run --project ColorVision
```

### 初次运行配置

程序首次启动时会：
- 生成默认配置文件到用户文档目录
- 创建日志目录和初始日志文件
- 扫描并加载Plugins目录下的插件

### 基本操作流程

1. **打开图像** - 文件菜单 → 打开图像文件
2. **加载设备模拟** - 设备菜单 → 添加模拟设备
3. **运行示例任务** - 调度器 → 创建新任务
4. **插件验证** - 插件菜单 → 查看已加载插件

### 插件启用验证

检查以下插件是否正常加载：
- **SystemMonitor** - 系统性能监控面板
- **Pattern** - 图案检测工具
- **EventVWR** - Windows事件查看器

## 八、配置系统

### 配置持久化机制

基于 IConfig 接口和依赖注入的配置管理：

```csharp
public class AppConfig : IConfig
{
    public string DatabaseConnection { get; set; }
    public bool EnableLogging { get; set; }
}
```

### 修改方式

- **UI 设置窗口** - 图形化配置界面
- **JSON 配置文件** - 直接编辑配置文件
- **代码配置** - 程序内动态配置

### 典型配置项

- **调度任务配置** - 任务执行时间和参数
- **设备连接配置** - MQTT服务器地址和认证
- **图像显示参数** - 缩放比例、颜色空间
- **结果导出路径** - 算法结果保存位置

## 九、动态属性编辑器扩展

### 使用特性注册

```csharp
[PropertyEditorType(typeof(ColorPickerEditor))]
public Color ThemeColor { get; set; }

[Category("显示")]
[DisplayName("背景颜色")]
public Brush Background { get; set; }
```

### 支持类型

- **数值类型** - int, double, decimal 等
- **字符串** - 单行、多行文本编辑
- **布尔** - 复选框编辑
- **枚举** - 下拉选择
- **Brush** - 颜色和画刷选择
- **Font** - 字体选择器
- **命令** - ICommand 绑定

### 自定义 IPropertyEditor 示例

```csharp
public class CustomEditor : IPropertyEditor
{
    public FrameworkElement CreateEditor(PropertyInfo property, object instance)
    {
        var textBox = new TextBox();
        // 实现自定义编辑逻辑
        return textBox;
    }
    
    public bool CanEdit(Type propertyType) 
        => propertyType == typeof(MyCustomType);
}
```

### 可见性控制

```csharp
[PropertyVisibility(nameof(IsAdvancedMode))]
public string AdvancedSetting { get; set; }

public bool IsAdvancedMode { get; set; }
```

## 十、插件开发指南

### 创建插件项目

1. **新建 Class Library** 项目
2. **目标框架** 设置为 net8.0-windows
3. **启用 WPF** - `<UseWPF>true</UseWPF>`

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0-windows</TargetFramework>
    <UseWPF>true</UseWPF>
  </PropertyGroup>
</Project>
```

### manifest.json 配置

```json
{
  "Name": "MyPlugin",
  "Version": "1.0.0",
  "Description": "示例插件",
  "Dependencies": {
    "ColorVision.UI": "1.3.0"
  }
}
```

### PostBuild 复制设置

```xml
<Target Name="PostBuild" AfterTargets="PostBuildEvent">
  <Exec Command="xcopy &quot;$(TargetDir)*.*&quot; &quot;$(SolutionDir)ColorVision\bin\$(ConfigurationName)\net8.0-windows\Plugins\$(ProjectName)\&quot; /Y /S" />
</Target>
```

### 插件入口实现

```csharp
public class MyPlugin : IPluginBase
{
    public override string Header => "我的插件";
    public override string Description => "插件功能描述";
    
    public override void Execute()
    {
        // 插件执行逻辑
        MessageBox.Show("插件已执行！");
    }
}
```

### 测试加载与调试

1. **构建插件** - 编译后自动复制到Plugins目录
2. **启动主程序** - 插件会自动被发现和加载
3. **调试模式** - 可以直接在插件项目中设置断点

## 十一、任务调度 (Scheduler)

### 支持模式

- **Simple** - 简单的一次性任务
- **Interval** - 固定间隔重复任务
- **Cron** - 基于Cron表达式的复杂调度

### 任务生命周期

```csharp
public interface IJobListener
{
    void JobToBeExecuted(IJobExecutionContext context);
    void JobExecutionVetoed(IJobExecutionContext context);
    void JobWasExecuted(IJobExecutionContext context, JobExecutionException exception);
}
```

### 添加任务示例

```csharp
// 创建任务
var job = JobBuilder.Create<MyJob>()
    .WithIdentity("myJob", "group1")
    .Build();

// 创建触发器
var trigger = TriggerBuilder.Create()
    .WithIdentity("myTrigger", "group1")
    .WithCronSchedule("0 0 12 * * ?")
    .Build();

// 调度任务
scheduler.ScheduleJob(job, trigger);
```

### 常用 Cron 示例

- `0 0 12 * * ?` - 每天中午12点执行
- `0 15 10 ? * *` - 每天上午10:15执行
- `0 0/5 * * * ?` - 每5分钟执行一次
- `0 0 8-18 * * ?` - 每天8点到18点之间每小时执行

## 十二、算法与结果展示

### 结果模型

```csharp
public class ViewResultAlg : IViewResult
{
    public string Name { get; set; }
    public object Value { get; set; }
    public ResultType Type { get; set; }
    public Dictionary<string, object> Properties { get; set; }
}
```

### 自定义结果处理

```csharp
public class MyResultHandler : IResultHandleBase
{
    public bool CanHandle(IViewResult result)
        => result.Type == ResultType.MyCustomType;
    
    public void Handle(IViewResult result, DrawCanvas canvas)
    {
        // 自定义结果处理逻辑
        var visual = CreateCustomVisual(result);
        canvas.AddVisual(visual);
    }
}
```

### 叠加显示

```csharp
// 创建矩形图元
var rect = new DrawingVisualRect()
{
    Attribute = new RectangleProperties
    {
        Rect = new Rect(10, 10, 100, 50),
        Brush = Brushes.Red,
        Pen = new Pen(Brushes.Blue, 2)
    }
};

// 添加到画布
drawCanvas.AddVisual(rect);
```

### 导出功能

- **CSV 导出** - 结构化数据导出为表格
- **JSON 导出** - 完整的结果对象序列化

## 十三、常见图像交互

### 添加图元

```csharp
// 添加文本标注
var textVisual = new DrawingVisualText()
{
    Attribute = new TextProperties
    {
        Text = "标注文本",
        Location = new Point(100, 100),
        FontSize = 14,
        Foreground = Brushes.Black
    }
};
drawCanvas.AddVisual(textVisual);
```

### 选中与编辑

- **单击选中** - 点击图元进行选择
- **多选** - Ctrl+点击或框选
- **属性编辑** - 选中后在属性面板中编辑
- **拖拽移动** - 直接拖拽图元位置

### 删除操作

```csharp
// 删除选中的图元
var selectedVisuals = drawCanvas.GetSelectedVisuals();
foreach (var visual in selectedVisuals)
{
    drawCanvas.RemoveVisual(visual);
}
```

### 多层刷新机制

```csharp
// 触发重新渲染
drawCanvas.InvalidateVisual();

// 部分区域更新
drawCanvas.InvalidateRect(updateRect);
```

## 十四、日志与调试

### log4net 配置

系统使用 log4net 进行日志管理，配置文件位于 `log4net.config`：

```xml
<log4net>
  <appender name="FileAppender" type="log4net.Appender.FileAppender">
    <file value="logs\application.log" />
    <appendToFile value="true" />
    <layout type="log4net.Layout.PatternLayout">
      <conversionPattern value="%date [%thread] %-5level %logger - %message%newline" />
    </layout>
  </appender>
</log4net>
```

### 日志使用

```csharp
private static readonly ILog log = LogManager.GetLogger(typeof(MyClass));

log.Info("信息日志");
log.Warn("警告信息");
log.Error("错误信息", exception);
```

### 运行日志位置

- **默认路径** - `logs/application.log`
- **配置路径** - 可在配置文件中自定义

### 常见问题诊断

- **启动慢** - 检查插件加载和依赖项
- **插件不加载** - 验证manifest.json和依赖版本
- **属性不显示** - 检查PropertyEditor注册和可见性设置

## 十五、国际化与本地化

### ResourceManager 缓存

系统实现了 ResourceManager 的缓存机制以提高性能：

```csharp
public static ConcurrentDictionary<Type, Lazy<ResourceManager?>> ResourceManagerCache { get; set; } = new();
```

### DisplayName/Description 解析

支持多语言的属性显示名称和描述：

```csharp
[DisplayName("PropertyName_DisplayName")]
[Description("PropertyName_Description")]
public string MyProperty { get; set; }
```

对应的资源文件中：
```
PropertyName_DisplayName=属性名称
PropertyName_Description=属性描述信息
```

### 多语言资源管理

- **英文** - Resources.en.resx
- **简体中文** - Resources.zh-CN.resx
- **繁体中文** - Resources.zh-TW.resx
- **日语** - Resources.ja.resx
- **韩语** - Resources.ko.resx

## 十六、性能与优化

### 反射缓存

系统实现了多级缓存机制：

```csharp
// ResourceManager缓存
public static ConcurrentDictionary<Type, Lazy<ResourceManager?>> ResourceManagerCache { get; set; } = new();

// 自定义编辑器缓存
public static ConcurrentDictionary<Type, IPropertyEditor> CustomEditorCache { get; } = new();
```

### UI 绑定优化

```csharp
// 使用适当的UpdateSourceTrigger
<TextBox Text="{Binding Value, UpdateSourceTrigger=LostFocus}" />
```

### 大量图元渲染策略

- **延迟刷新** - 批量更新时避免频繁重绘
- **最小无效区域** - 只更新变化的区域
- **虚拟化** - 大量图元时的虚拟化显示

```csharp
// 批量更新时暂停刷新
drawCanvas.BeginUpdate();
try
{
    // 批量添加图元
    foreach (var item in items)
        drawCanvas.AddVisual(CreateVisual(item));
}
finally
{
    drawCanvas.EndUpdate(); // 统一刷新
}
```

## 十七、安全与权限

### RBAC 系统

系统支持基于角色的访问控制：

- **用户管理** - 用户账户和认证
- **角色定义** - 管理员、操作员、观察者等角色
- **权限控制** - 功能模块和操作权限

### 登录和审计

```csharp
public interface IAuthenticationService
{
    Task<bool> LoginAsync(string username, string password);
    Task LogoutAsync();
    Task<bool> HasPermissionAsync(string permission);
}
```

### 审计日志

系统记录关键操作的审计信息：
- 用户登录/登出
- 关键配置修改
- 设备操作记录
- 算法执行历史

## 十八、Roadmap

### 计划中的功能

- **脚本化工作流** - 支持Python/C#脚本的工作流定义
- **更多算法适配** - 扩展第三方算法库支持
- **跨平台评估** - 评估Linux/MacOS平台移植可能性
- **云端集成** - 支持云端算法服务和结果存储
- **移动端支持** - 开发配套的移动端监控应用

### 技术演进

- **.NET 9 升级** - 跟进最新.NET版本
- **WebAssembly 支持** - 部分组件的WebAssembly化
- **容器化部署** - Docker容器化部署支持

## 十九、贡献指南

### Issue / PR 规范

- **Issue模板** - 使用标准模板报告问题
- **PR检查清单** - 代码审查和测试要求
- **提交信息** - 遵循Conventional Commits规范

### 代码风格约定

- **命名规范** - PascalCase用于类和方法，camelCase用于变量
- **目录结构** - 按功能模块组织代码文件
- **注释要求** - 公共API必须有XML文档注释

### 分支策略

- **master** - 主分支，稳定版本
- **develop** - 开发分支，集成新功能
- **feature/** - 功能分支，单一功能开发
- **hotfix/** - 热修复分支，紧急问题修复

## 二十、许可证

本项目采用 **MIT 许可证**，允许自由使用、修改和分发。

## 二十一、致谢

### 核心开源项目

- **Quartz.NET** - 企业级任务调度库
- **HandyControl** - 现代化WPF控件库  
- **log4net** - .NET日志框架
- **Newtonsoft.Json** - JSON序列化库
- **MQTT.NET** - MQTT通信协议实现
- **OpenCV** - 计算机视觉库

### 特别感谢

感谢所有为ColorVision项目贡献代码、文档和建议的开发者和用户社区。

---

## 附录

### 常见特性(Attribute)速查表

| 特性 | 用途 | 示例 |
|------|------|------|
| `PropertyEditorTypeAttribute` | 指定自定义编辑器 | `[PropertyEditorType(typeof(ColorEditor))]` |
| `CategoryAttribute` | 属性分组 | `[Category("外观")]` |
| `DisplayNameAttribute` | 显示名称 | `[DisplayName("背景颜色")]` |
| `DescriptionAttribute` | 属性描述 | `[Description("设置控件背景颜色")]` |
| `PropertyVisibilityAttribute` | 可见性控制 | `[PropertyVisibility(nameof(IsVisible))]` |

### 扩展接口清单

| 接口 | 用途 | 实现要求 |
|------|------|----------|
| `IPlugin` | 插件入口 | 实现Execute方法 |
| `IConfig` | 配置对象 | 标记接口，支持依赖注入 |
| `IPropertyEditor` | 属性编辑器 | 实现CreateEditor和CanEdit方法 |
| `IViewResult` | 算法结果 | 定义结果数据结构 |
| `IResultHandleBase` | 结果处理器 | 实现Handle方法处理结果 |
| `IDrawingVisual` | 自定义图元 | 实现Render和GetRect方法 |

---

**ColorVision 开发团队**  
**视彩（上海）光电技术有限公司**









