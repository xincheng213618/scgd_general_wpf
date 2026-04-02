# Spectrum Plugin - 光谱仪测试工具

> 版本: 2.1.4.0 | 目标框架: .NET 8.0 / .NET 10.0 Windows | 最低ColorVision版本: ≥ 1.3.15.8

## 🎯 功能定位

Spectrum 是 ColorVision 的光谱仪测试与色彩分析插件，提供完整的光谱仪设备控制、光谱数据采集、色度计算和数据管理功能。支持 SP100（CMvSpectra）和 SP10（LightModule）两种光谱仪型号，具备 CIE 色度分析、显色指数 Ra 计算、EQE 外量子效率计算等专业光学测量能力。

## 主要功能点

### 设备管理
- **多型号支持** - CMvSpectra（SP100）高精度光谱仪、LightModule（SP10）轻量级光谱模块
- **串口通信** - 可配置 COM 端口和波特率（默认 9600）
- **附件控制** - 快门开关、ND 滤光轮、滤光轮位置切换
- **自动积分时间配置** - 根据光强自动调整积分时间

### 光谱数据采集
- **测量模式** - 单次测量、批量连续测量、自适应校零
- **波长范围** - 380–780nm（可见光波段），1nm 步进
- **数据同步** - 支持频率同步（默认 1000Hz）和滤波带宽配置
- **安全机制** - 测量过程中禁用其他操作按钮，防止冲突

### 色度分析
- **CIE 色度空间** - CIE 1931（2° 标准观察者）、CIE 1976（10° 标准观察者）、CIE 2015 色彩空间
- **色度参数计算** - 色坐标（x, y）、相关色温 CCT（McCamy 公式）、主波长、兴奋纯度
- **显色指数** - Ra（一般显色指数）计算，基于 CIE 13.3-1995 标准

### 数据可视化
- **ScottPlot 图表** - 相对光谱曲线、绝对光谱曲线
- **CIE 色度图** - CIE 1931 和 CIE 1976 色度图叠加显示
- **波长转 RGB** - 将可见光波长（380–780nm）转换为显示颜色

### 校正管理
- **校正组** - 多组校正文件管理（每组包含波长和幅度校正）
- **校正文件验证** - 波长标定文件（WavaLength.dat）和幅度标定文件（Magiude.dat）验证
- **按设备序列号存储** - 不同设备使用独立校正数据

### 数据持久化
- **数据库存储** - 基于 SqlSugar ORM 的光谱数据存储
- **CSV 导出** - 光谱数据导出至桌面
- **配置持久化** - 窗口布局、设备配置自动保存

### 其他功能
- **EQE 计算** - 外量子效率计算，支持电压和电流参数配置
- **许可证管理** - 本地与全局（AppData）许可证双向同步
- **面板布局持久化** - 基于 AvalonDock 的面板布局保存与恢复
- **多语言支持** - 英语、法语、日语、韩语、繁体中文、俄语

## 技术架构

```
┌─────────────────────────────────────────────────────────────┐
│                      Spectrum Plugin                          │
│                                                              │
│  ┌─────────────┐    ┌─────────────┐    ┌─────────────┐      │
│  │ MainWindow  │    │Spectrometer │    │  ViewResult │      │
│  │             │    │  Manager    │    │  Manager    │      │
│  │ • 图表显示  │    │             │    │             │      │
│  │ • 设备连接  │    │ • 设备控制  │    │ • 数据存储  │      │
│  │ • 测量控制  │    │ • 数据采集  │    │ • 结果查询  │      │
│  └─────────────┘    └─────────────┘    └─────────────┘      │
│                                                              │
│  ┌─────────────┐    ┌─────────────┐    ┌─────────────┐      │
│  │ Colorimetry │    │    Ra       │    │ Calibration │      │
│  │   Helper    │    │ Calculator  │    │   Manager   │      │
│  │             │    │             │    │             │      │
│  │ • CIE计算   │    │ • 显色指数  │    │ • 校正文件  │      │
│  │ • 色温计算  │    │ • Ra计算    │    │ • 验证管理  │      │
│  │ • 主波长    │    │ • TCS色样   │    │ • 组管理    │      │
│  └─────────────┘    └─────────────┘    └─────────────┘      │
│                                                              │
└─────────────────────────────────────────────────────────────┘
```

## 使用方式

### 连接光谱仪

```csharp
// 获取光谱仪管理器
var manager = SpectrometerManager.Instance;

// 设置光谱仪类型
manager.SpectrometerType = SpectrometerType.CMvSpectra;

// 配置串口
manager.SzComName = "COM3";
manager.BaudRate = 9600;

// 连接设备
manager.Connect();
```

### 执行测量

```csharp
// 设置积分时间（毫秒）
manager.ExpTime = 100;

// 执行测量
manager.Measure();

// 获取光谱数据
var spectralData = manager.GetSpectralData();
```

### 色度计算

```csharp
// 计算色坐标
var (x, y) = ColorimetryHelper.CalculateChromaticity(spectralData);

// 计算色温
var cct = ColorimetryHelper.CalculateCCT(x, y);

// 计算显色指数
var ra = RaCalculator.CalculateRa(spectralData);
```

## 主要组件

### MainWindow
主窗口类，提供完整的用户界面。

```csharp
public partial class MainWindow : Window
{
    public static SpectrometerManager Manager => SpectrometerManager.Instance;
    public static ObservableCollection<ViewResultSpectrum> ViewResultSpectrums { get; }
    
    // 图表显示
    private void UpdateChart(SpectralData data);
    
    // 设备连接
    private void ConnectDevice();
    
    // 执行测量
    private void ExecuteMeasurement();
}
```

### SpectrometerManager
光谱仪核心管理器，负责设备控制和数据采集。

```csharp
public class SpectrometerManager : ViewModelBase
{
    public static SpectrometerManager Instance => SpectrometerManager.Instance;
    
    // 光谱仪类型
    public SpectrometerType SpectrometerType { get; set; }
    
    // 串口配置
    public string SzComName { get; set; }
    public int BaudRate { get; set; }
    
    // 积分时间
    public int ExpTime { get; set; }
    
    // 连接/断开
    public void Connect();
    public void Disconnect();
    
    // 测量
    public void Measure();
    
    // 获取光谱数据
    public SpectralData GetSpectralData();
}
```

### SpectrometerType
光谱仪类型枚举。

```csharp
public enum SpectrometerType
{
    [Description("SP100")]
    CMvSpectra = 0,    // 高精度光谱仪
    
    [Description("SP10")]
    LightModule = 1    // 轻量级光谱模块
}
```

### MainWindowConfig
窗口配置类。

```csharp
public class MainWindowConfig : WindowConfig, IConfig, IConfigSettingProvider
{
    public static MainWindowConfig Instance => ConfigService.Instance.GetRequiredService<MainWindowConfig>();
    
    // 日志面板可见性
    public bool LogControlVisibility { get; set; }
    
    // EQE功能开关
    public bool EqeEnabled { get; set; }
    
    // EQE参数
    public float EqeVoltage { get; set; }
    public float EqeCurrentMA { get; set; }
    
    // CIE色度点显示
    public bool CiePointEnabled { get; set; }
}
```

## 目录说明

- `MainWindow.xaml/cs` - 主窗口
- `MainWindow.Chart.cs` - 图表可视化（ScottPlot）
- `MainWindow.Connection.cs` - 设备连接管理
- `MainWindow.Measurement.cs` - 光谱测量逻辑
- `MainWindow.Export.cs` - 数据导出
- `MainWindow.ListView.cs` - 结果列表管理
- `MainWindow.Eqe.cs` - EQE 外量子效率计算
- `MainWindowConfig.cs` - 窗口配置持久化
- `SpectrometerManager.cs` - 光谱仪核心管理器
- `SpectrometerType.cs` - 设备类型枚举
- `SpectrumStatusBarProvider.cs` - 状态栏提供者
- `Assets/Image/` - CIE 色度图参考图
- `Calibration/` - 校正管理界面
- `Configs/` - 设备配置
- `Data/` - 数据管理
- `Help/` - 帮助系统
- `Layout/` - 布局管理
- `License/` - 许可证管理
- `Menus/` - 菜单系统
- `Models/` - 数据模型
- `Properties/` - 多语言资源
- `PropertyEditor/` - 属性编辑器
- `View/` - 色度计算

## 开发调试

```bash
# 构建项目
dotnet build Plugins/Spectrum/Spectrum.csproj

# 运行测试
dotnet test
```

## 最佳实践

### 1. 设备连接
- 确保串口未被其他程序占用
- 检查波特率配置与设备匹配
- 连接前验证设备电源状态

### 2. 测量设置
- 根据光强选择合适的积分时间
- 使用自适应校零功能提高精度
- 测量前确保光源稳定

### 3. 数据管理
- 定期备份校正文件
- 使用有意义的文件名保存测量结果
- 及时导出重要数据

### 4. 校正维护
- 定期验证校正文件有效性
- 设备更换后重新进行校正
- 保存校正历史记录

## 相关文档链接

- [详细技术文档](../../docs/04-api-reference/plugins/standard-plugins/spectrum.md)
- [CIE 色度标准](https://cie.co.at/)
- [ColorVision.UI README](../ColorVision.UI/README.md)

## 更新日志

### v2.1.4.0（2026-03-29）
- 采用模块化设计，将 MainWindow 拆分为多个部分类
- 新增多语言支持（英语、法语、日语、韩语、繁体中文、俄语）
- 新增内置帮助文档系统
- 新增校正文件二进制验证

### v2.0.0.0（2026-03-23）
- 更新到 .NET 10
- 增加显色指数 Ra 计算
- 增加 EQE 外量子效率计算
- 优化 UI 界面布局

### v1.1.4.8（2025-09-09）
- 取图失败自动重连（6 次）
- 增加数据库存储支持

## 维护者

ColorVision 插件团队
