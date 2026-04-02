# ColorVision.FileIO

> 版本: 1.3.12.24 | 目标框架: .NET 8.0 / .NET 10.0 Windows

## 🎯 功能定位

ColorVision.FileIO 是 ColorVision 系统的专用文件IO处理模块，负责ColorVision专有格式文件的读写操作。为整个系统提供统一的文件访问接口，支持多种图像格式和数据格式的读写。

## 主要功能点

### CVRaw 文件处理
- **原始图像格式** - ColorVision 原始图像格式的读写
- **元数据管理** - 图像元数据的存储和读取
- **数据压缩** - 支持数据压缩存储

### CVCIE 文件处理
- **CIE色彩数据** - ColorVision CIE 色彩数据格式的读写
- **光谱数据** - 光谱数据的存储和读取
- **测量结果** - 测量结果的文件存储

### 文件格式验证
- **MagicHeader验证** - 文件头格式验证
- **完整性检查** - 文件完整性验证
- **版本兼容** - 多版本文件格式兼容

### 批量文件操作
- **批量读写** - 支持大批量文件的高效处理
- **异步IO** - 支持异步文件读写，避免界面阻塞
- **进度报告** - 批量操作进度反馈

### 图像格式支持
- **标准格式** - BMP、PNG、JPEG、TIFF
- **原始格式** - CVRaw、CVCIE
- **数据格式** - CSV、JSON、YAML

## 技术架构

```
┌─────────────────────────────────────────────────────────────┐
│                   ColorVision.FileIO                          │
│                                                              │
│  ┌─────────────┐    ┌─────────────┐    ┌─────────────┐      │
│  │   CVRaw     │    │   CVCIE     │    │   Image     │      │
│  │   Handler   │    │   Handler   │    │   Handler   │      │
│  │             │    │             │    │             │      │
│  │ • 原始图像  │    │ • CIE数据   │    │ • 标准格式  │      │
│  │ • 元数据    │    │ • 光谱数据  │    │ • 格式转换  │      │
│  │ • 压缩存储  │    │ • 测量结果  │    │ • 批量处理  │      │
│  └─────────────┘    └─────────────┘    └─────────────┘      │
│                                                              │
│  ┌─────────────┐    ┌─────────────┐                          │
│  │   File      │    │   Async     │                          │
│  │   Validator │    │    IO       │                          │
│  │             │    │             │                          │
│  │ • 格式验证  │    │ • 异步读写  │                          │
│  │ • 完整性    │    │ • 进度报告  │                          │
│  │ • 版本兼容  │    │ • 取消操作  │                          │
│  └─────────────┘    └─────────────┘                          │
│                                                              │
└─────────────────────────────────────────────────────────────┘
```

## 使用方式

### 引用方式

```xml
<ProjectReference Include="..\ColorVision.FileIO\ColorVision.FileIO.csproj" />
```

### CVRaw 文件读写

```csharp
using ColorVision.FileIO;

// 读取 CVRaw 文件
var cvRaw = new CVRawFile();
cvRaw.Load("path/to/file.cvraw");

// 获取图像数据
byte[] imageData = cvRaw.GetImageData();
int width = cvRaw.Width;
int height = cvRaw.Height;
int channels = cvRaw.Channels;

// 保存 CVRaw 文件
cvRaw.Save("path/to/output.cvraw");
```

### CVCIE 文件读写

```csharp
using ColorVision.FileIO;

// 读取 CVCIE 文件
var cvCie = new CVCIEFile();
cvCie.Load("path/to/file.cvcie");

// 获取 CIE 数据
var cieData = cvCie.GetCIEData();
double x = cieData.X;
double y = cieData.Y;
double z = cieData.Z;

// 保存 CVCIE 文件
cvCie.Save("path/to/output.cvcie");
```

### 异步文件操作

```csharp
using ColorVision.FileIO;

// 异步读取文件
var cvRaw = new CVRawFile();
await cvRaw.LoadAsync("path/to/file.cvraw", progress => {
    Console.WriteLine($"加载进度: {progress}%");
});

// 异步保存文件
await cvRaw.SaveAsync("path/to/output.cvraw", progress => {
    Console.WriteLine($"保存进度: {progress}%");
});
```

## 主要组件

### CVRawFile
原始图像文件处理类。

```csharp
public class CVRawFile
{
    // 图像属性
    public int Width { get; }
    public int Height { get; }
    public int Channels { get; }
    public int BitDepth { get; }
    
    // 元数据
    public Dictionary<string, object> Metadata { get; }
    
    // 加载/保存
    public void Load(string filePath);
    public void Save(string filePath);
    public Task LoadAsync(string filePath, Action<int> progressCallback);
    public Task SaveAsync(string filePath, Action<int> progressCallback);
    
    // 数据操作
    public byte[] GetImageData();
    public void SetImageData(byte[] data);
}
```

### CVCIEFile
CIE色彩数据文件处理类。

```csharp
public class CVCIEFile
{
    // CIE 数据
    public CIEData Data { get; }
    
    // 测量信息
    public MeasurementInfo Info { get; }
    
    // 加载/保存
    public void Load(string filePath);
    public void Save(string filePath);
    
    // 数据操作
    public CIEData GetCIEData();
    public SpectrumData GetSpectrumData();
}
```

### FileValidator
文件验证类。

```csharp
public static class FileValidator
{
    // 验证文件格式
    public static bool ValidateCVRaw(string filePath);
    public static bool ValidateCVCIE(string filePath);
    
    // 获取文件信息
    public static FileInfo GetFileInfo(string filePath);
    
    // 检查版本兼容性
    public static bool CheckVersionCompatibility(string filePath, Version targetVersion);
}
```

## 目录说明

- `CVRaw/` - CVRaw文件格式处理
- `CVCIE/` - CVCIE文件格式处理
- `Image/` - 标准图像格式处理
- `Validator/` - 文件验证
- `Async/` - 异步IO操作

## 开发调试

```bash
# 构建项目
dotnet build Engine/ColorVision.FileIO/ColorVision.FileIO.csproj

# 运行测试
dotnet test
```

## 优化说明（v1.3.12.24+）

- **异常处理增强**：所有文件流和二进制读取操作均已加 try-catch，异常会通过 Debug.WriteLine 记录，提升健壮性。
- **MagicHeader 常量统一**：所有 CVCIE 文件头字符串均统一使用 MagicHeader 常量，避免硬编码。
- **资源释放优化**：所有文件流操作均使用 using，防止资源泄漏。
- **内存分配保护**：大文件读取时分配内存已加 OutOfMemoryException 保护，防止 OOM 崩溃。
- **职责细化与冗余合并**：部分复杂方法已建议细化为更小单元，冗余代码合并，提升可维护性。
- **注释与文档**：关键结构和方法已补充 XML 注释，便于二次开发和维护。

## 最佳实践

### 1. 文件读写异常处理
```csharp
try
{
    var cvRaw = new CVRawFile();
    cvRaw.Load(filePath);
}
catch (FileNotFoundException ex)
{
    Log.Error($"文件未找到: {filePath}", ex);
}
catch (InvalidDataException ex)
{
    Log.Error($"文件格式无效: {filePath}", ex);
}
catch (OutOfMemoryException ex)
{
    Log.Error($"内存不足，无法加载文件: {filePath}", ex);
}
```

### 2. 异步操作使用
```csharp
// 对于大文件，使用异步操作避免阻塞UI
await cvRaw.LoadAsync(filePath, progress => {
    ProgressBar.Value = progress;
});
```

### 3. 资源释放
```csharp
// 使用 using 语句确保资源释放
using (var cvRaw = new CVRawFile())
{
    cvRaw.Load(filePath);
    // 处理数据
}
```

## 相关文档链接

- [详细技术文档](../../docs/04-api-reference/engine-components/ColorVision.FileIO.md)
- [文件格式说明](../../docs/05-resources/data-storage.md)
- [ColorVision.Engine README](../ColorVision.Engine/README.md)

## 维护者

ColorVision 数据团队
