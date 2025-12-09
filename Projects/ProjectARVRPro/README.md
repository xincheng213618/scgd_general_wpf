# ProjectARVRPro

[![Version](https://img.shields.io/badge/version-1.0.4.7-blue.svg)](./ProjectARVRPro.csproj)
[![.NET](https://img.shields.io/badge/.NET-8.0-purple.svg)](https://dotnet.microsoft.com/)
[![Platform](https://img.shields.io/badge/platform-Windows-lightgrey.svg)](https://www.microsoft.com/windows)

## 🎯 功能定位

ARVR Pro 专业版测试系统 - 针对AR/VR显示设备的全面光学性能测试解决方案。

## 📋 作用范围

专为AR/VR显示设备提供专业级光学测试，涵盖显示质量、光学性能、缺陷检测等全方位测试需求。

## ✨ 主要功能点

### 测试模式处理

- **初始化管理** - 通过ProjectARVRProInit进行完整的测试参数初始化
- **多模式处理** - 支持多种测试模式和图像处理算法：

| 处理模块 | 功能描述 | 支持的测试项 |
|---------|---------|-------------|
| **White255Process** | 白色255灰度测试处理 | 亮度、均匀性 |
| **White51Process** | 白色51灰度测试处理 | 低亮度测试 |
| **W25Process** | W25测试模式处理 | 色度测试 |
| **BlackProcess** | 黑色画面测试处理 | 对比度、暗电流 |
| **ChessboardProcess** | 棋盘格测试处理 | 像素缺陷检测 |
| **DistortionProcess** | 畸变测试处理 | 光学畸变分析 |
| **MTFHVProcess** | 水平垂直MTF测试处理 | 清晰度/解析力(0F/0.3F/0.6F/0.8F) |
| **MTFHV058Process** | 058产品MTF测试处理 | 清晰度/解析力(0F/0.5F/0.8F) |
| **OpticCenterProcess** | 光轴中心测试处理 | 光轴对准 |
| **RedProcess** | 红色通道测试处理 | 红色色度 |
| **GreenProcess** | 绿色通道测试处理 | 绿色色度 |
| **BlueProcess** | 蓝色通道测试处理 | 蓝色色度 |

### 配置管理

- **Recipe管理** - 完整的测试配方管理系统，支持参数化配置
- **Fix修正** - 数据分析和修正功能，支持校准系数配置
- **ProcessConfig** - 每个Process模块独立的配置，支持自定义解析Key

### 流程管理

- **流程管理** - 基于大流程模板的测试执行
  - **ProcessMeta管理** - 支持多流程配置和选择性执行
  - **IsEnabled属性** - 可选择性启用/禁用特定测试步骤
  - **配置独立性** - 每个ProcessMeta拥有独立的Process实例和Config配置
- **结果管理** - 测试结果的存储、查询和展示

## 🔗 与主程序的依赖关系

### 引用的程序集

| 程序集 | 功能 |
|-------|------|
| `ColorVision.Engine` | 核心引擎功能 |
| `ColorVision.Engine.Templates` | 模板管理支持 |
| `ColorVision.Engine.Templates.Jsons.LargeFlow` | 大流程模板支持 |
| `ColorVision.UI` | 基础UI组件 |
| `CVCommCore` | 通信核心组件 |

### 被引用方式

- 作为插件集成到主程序
- 支持独立窗口和大流程模式

## 🚀 使用方式

### 引用方式

作为独立插件项目，通过插件系统加载。

### 在主程序中的启用

- 插件自动加载和注册
- 支持模板编辑器和流程工具集成

### 初始化流程

传入`ProjectARVRProInit`参数，系统会自动初始化整个测试参数信息，包括：
- 测试配方配置
- 算法参数设定
- 硬件设备初始化
- 流程模板加载

## 🛠️ 开发调试

```bash
# 构建项目
dotnet build Projects/ProjectARVRPro/ProjectARVRPro.csproj

# 或使用VS2022打开解决方案
# 选择 x64 平台配置
```

## 📁 目录说明

### 核心文件

| 文件 | 说明 |
|-----|------|
| `ProjectARVRProConfig.cs` | 项目主配置类 |
| `ARVRWindow.xaml/.cs` | 主测试窗口 |
| `ViewResultManager.cs` | 结果视图管理 |
| `ObjectiveTestResult.cs` | 客观测试结果数据模型 |
| `TestResultViewWindow.xaml/.cs` | 测试结果查看窗口 |

### 子目录

| 目录 | 说明 |
|-----|------|
| `Process/` | 测试流程处理模块 |
| `Recipe/` | 测试配方管理 |
| `Fix/` | 修正参数管理 |
| `Services/` | 服务模块(Socket通信等) |
| `PluginConfig/` | 插件配置 |

### Process目录结构

```
Process/
├── IProcess.cs              # 流程接口定义
├── IProcessConfig.cs        # 流程配置基类
├── IProcessExecutionContext.cs  # 执行上下文
├── ProcessManager.cs        # 流程管理器
├── ProcessManagerWindow.xaml/.cs  # 流程管理窗口
├── ProcessMeta.cs           # 流程元数据
├── ProcessMetaPersist.cs    # 流程持久化
├── Black/                   # 黑屏测试
├── Blue/                    # 蓝色测试
├── Chessboard/              # 棋盘格测试
├── Distortion/              # 畸变测试
├── Green/                   # 绿色测试
├── MTFHV/                   # MTF水平垂直测试
├── MTFHV058/                # 058产品MTF测试
├── OpticCenter/             # 光轴中心测试
├── Red/                     # 红色测试
├── W25/                     # W25测试
└── W255/                    # W255测试
```

## ⚙️ 配置说明

### 测试配方管理 (Recipe)

- 支持多种测试配方的创建和编辑
- 参数化的测试流程配置
- 结果数据的自动分析和修正

### 大流程支持 (LargeFlow)

- 基于LargeFlow模板的复杂测试流程
- 支持模板编辑和流程可视化配置
- ProcessMeta选择性执行：通过IsEnabled属性控制步骤是否执行，系统自动跳过禁用步骤

### ProcessMeta管理

- **流程配置**：支持创建、更新、删除和排序多个测试流程
- **选择性执行**：每个ProcessMeta都有IsEnabled属性（默认启用）
- **智能跳过**：执行时自动跳过已禁用的步骤
- **完成检测**：仅基于启用的步骤判断测试是否完成
- **配置独立性**：每个ProcessMeta拥有独立的Process实例和Config配置
- **示例**：如果配置了8个步骤(0-7)，仅启用第0和第7步，执行流程将是：0→7（跳过1-6）

### ProcessConfig可配置Key

从v1.0.4.4开始，MTFHVProcess和MTFHV058Process支持用户自定义配置解析使用的变量名：

```csharp
// MTFHVProcessConfig 示例
public class MTFHVProcessConfig : ProcessConfigBase
{
    [Category("解析配置")]
    [DisplayName("Center_0F解析Key")]
    [Description("用于解析Center_0F数据的Key")]
    public string Key_Center_0F { get; set; } = "0F_MTF_HV_Center";
    
    // ... 更多配置项
}
```

**支持的配置项：**
- `Key_Center_0F` - 中心0F位置解析Key
- `Key_LeftUp_0_3F/0_5F/0_6F/0_8F` - 左上角各视场角解析Key
- `Key_LeftDown_0_3F/0_5F/0_6F/0_8F` - 左下角各视场角解析Key
- `Key_RightUp_0_3F/0_5F/0_6F/0_8F` - 右上角各视场角解析Key
- `Key_RightDown_0_3F/0_5F/0_6F/0_8F` - 右下角各视场角解析Key

## 🏗️ 架构设计

### IProcess接口

```csharp
public interface IProcess
{
    // 执行测试流程
    bool Execute(IProcessExecutionContext ctx);
    
    // 渲染结果到图像视图
    void Render(IProcessExecutionContext ctx);
    
    // 生成测试结果文本
    string GenText(IProcessExecutionContext ctx);
    
    // 获取配方配置
    IRecipeConfig GetRecipeConfig();
    
    // 获取修正配置
    IFixConfig GetFixConfig();
    
    // 获取流程配置
    object GetProcessConfig();
    
    // 设置流程配置（从JSON）
    void SetProcessConfig(string configJson);
    
    // 创建新实例
    IProcess CreateInstance();
}
```

### ProcessBase泛型基类

```csharp
public abstract class ProcessBase<TConfig> : IProcess 
    where TConfig : ViewModelBase, new()
{
    public TConfig Config { get; set; } = new TConfig();
    
    // 自动实现配置序列化/反序列化
}
```

## 📚 相关文档链接

- [IsEnabled功能说明](./Process/README_IsEnabled_Feature.md) - 流程步骤启用/禁用功能
- [CHANGELOG](./CHANGELOG.md) - 版本更新日志
- [流程引擎文档](../../docs/04-api-reference/engine-components/README.md)

## 📝 版本历史

查看完整的版本更新记录请参阅 [CHANGELOG.md](./CHANGELOG.md)

### 最新版本 v1.0.4.7

- 文档更新和优化

### v1.0.4.4

- 新增MTFHV058Process模块
- MTF ProcessConfig支持可配置Key
- ProcessManager配置独立性修复

## 🔮 开发路线图

详见 [ROADMAP.md](./ROADMAP.md)

## 👥 维护者

ColorVision 项目团队

---

*如有问题或建议，请提交 [GitHub Issues](https://github.com/xincheng213618/scgd_general_wpf/issues)*