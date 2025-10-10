# ScreenRecorder 插件

## 1. 插件概述

- **名称**: ScreenRecorder (屏幕录像机)
- **类型**: 多媒体工具
- **功能**: 高性能屏幕录制工具，支持多录制源、实时预览和高级编码选项
- **适用 ColorVision 版本**: ≥ 1.3.12.34
- **依赖的核心模块**: ColorVision.UI, ColorVision.Common, ScreenRecorderLib

## 2. 功能特性

### 核心功能
- **多源录制**: 支持显示器、窗口、摄像头、图片和视频作为录制源
- **灵活组合**: 可同时录制多个源并自定义布局位置
- **高质量编码**: 支持 H.264、H.265、AV1 等现代编码格式
- **音频录制**: 支持系统音频和麦克风音频同时录制
- **鼠标效果**: 可选显示鼠标指针和点击效果
- **实时预览**: 录制过程中显示帧率和录制时长
- **快照功能**: 录制过程中可保存屏幕快照

### 高级特性
- **自定义输出**: 支持自定义输出分辨率和源区域
- **覆盖层**: 支持添加摄像头、视频、图片和文本覆盖层
- **暂停/恢复**: 录制过程中可暂停和恢复
- **流输出**: 支持录制到内存流或文件
- **硬件加速**: 利用 GPU 硬件编码提升性能
- **日志记录**: 可选启用详细日志用于调试

## 3. 目录结构

```
ScreenRecorder/
├── manifest.json                      # 插件清单文件
├── ScreenRecorder.csproj             # 项目文件
├── README.md                          # 本说明文档
├── MainWindow.xaml                    # 主界面 XAML
├── MainWindow.xaml.cs                 # 主界面代码
├── App.xaml                           # 应用程序定义
├── App.xaml.cs                        # 应用程序入口
├── ScreenRecorderLib.dll              # 录制核心库
├── Sources/                           # 录制源模型
│   ├── ICheckableRecordingSource.cs   # 录制源接口
│   ├── CheckableRecordableDisplay.cs  # 显示器录制源
│   ├── CheckableRecordableWindow.cs   # 窗口录制源
│   ├── CheckableRecordableCamera.cs   # 摄像头录制源
│   ├── CheckableRecordableImage.cs    # 图片录制源
│   └── CheckableRecordableVideo.cs    # 视频录制源
├── OverlayModel.cs                    # 覆盖层模型
├── OverlayTemplateSelector.cs         # 覆盖层模板选择器
├── RecordingSourceTemplateSelector.cs # 录制源模板选择器
├── BytesToKilobytesConverter.cs       # 字节转换器
├── ExportEventWindow.cs               # 导出事件窗口
├── AssemblyInfo.cs                    # 程序集信息
└── Properties/                        # 资源文件
    └── Resources.resx                 # 资源定义
```

### manifest.json 字段说明
- `manifest_version`: 清单版本 (1)
- `Id`: 插件唯一标识符 ("ScreenRecorder")
- `name`: 插件显示名称 ("录像插件")
- `version`: 插件版本 ("1.0")
- `description`: 插件描述
- `dllpath`: 主程序集路径 ("ScreenRecorder.dll")
- `requires`: 最低 ColorVision 版本要求 ("1.3.12.34")

## 4. 架构与设计

### 核心类说明

#### MainWindow
主窗口类，负责：
- 录制控制（开始、暂停、停止）
- 录制源管理和选择
- 编码参数配置
- 实时状态显示
- 事件处理和资源管理

#### ICheckableRecordingSource
录制源统一接口，定义：
- `IsSelected`: 是否被选中
- `IsCheckable`: 是否可勾选
- `OutputSize`: 输出尺寸
- `Position`: 输出位置
- `SourceRect`: 源区域
- 自定义尺寸、位置和区域的启用标志

#### CheckableRecordable* 系列类
包装 ScreenRecorderLib 的录制源，提供：
- WPF 属性变更通知
- 自定义输出参数支持
- 数据绑定友好的接口

### 录制流程

1. **初始化阶段**
   - 刷新录制源列表（显示器、窗口、摄像头）
   - 加载默认配置（帧率60fps、音频启用）
   - 初始化覆盖层

2. **配置阶段**
   - 选择录制源（可多选）
   - 配置编码参数（格式、码率、质量）
   - 设置音频选项
   - 配置鼠标效果

3. **录制阶段**
   - 创建 Recorder 实例
   - 注册事件处理器
   - 开始录制
   - 实时更新进度和帧率

4. **结束阶段**
   - 停止录制
   - 保存输出文件
   - 清理资源
   - 显示结果路径

### 事件处理机制

```csharp
// 录制完成事件
_rec.OnRecordingComplete += (sender, e) => {
    // 更新 UI，显示输出路径
    // 清理资源
};

// 录制失败事件
_rec.OnRecordingFailed += (sender, e) => {
    // 显示错误信息
    // 清理资源
};

// 状态变更事件
_rec.OnStatusChanged += (sender, e) => {
    // 更新 UI 状态（录制中、暂停、完成）
    // 启用/禁用控件
};

// 帧录制事件
_rec.OnFrameRecorded += (sender, e) => {
    // 更新帧率统计
    // 更新进度显示
};
```

## 5. 主要功能实现

### 录制源管理

#### 刷新录制源
```csharp
private void RefreshCaptureTargetItems()
{
    RecordingSources.Clear();
    
    // 添加显示器
    foreach (var display in Recorder.GetDisplays())
    {
        RecordingSources.Add(new CheckableRecordableDisplay(display));
    }
    
    // 添加窗口
    foreach (var window in Recorder.GetWindows())
    {
        RecordingSources.Add(new CheckableRecordableWindow(window));
    }
    
    // 添加摄像头
    foreach (var camera in Recorder.GetSystemVideoCaptureDevices())
    {
        RecordingSources.Add(new CheckableRecordableCamera(camera));
    }
}
```

#### 创建录制源配置
```csharp
private List<RecordingSourceBase> CreateSelectedRecordingSources()
{
    var sourcesToRecord = RecordingSources.Where(x => x.IsSelected).ToList();
    
    return sourcesToRecord.Select(x => {
        if (x is CheckableRecordableDisplay disp)
        {
            return new DisplayRecordingSource(disp)
            {
                OutputSize = disp.IsCustomOutputSizeEnabled ? disp.OutputSize : null,
                SourceRect = disp.IsCustomOutputSourceRectEnabled ? disp.SourceRect : null,
                Position = disp.IsCustomPositionEnabled ? disp.Position : null
            };
        }
        // 其他录制源类型...
    }).ToList();
}
```

### 录制控制

#### 开始录制
```csharp
private void RecordButton_Click(object sender, RoutedEventArgs e)
{
    if (IsRecording)
    {
        _rec.Stop();
        return;
    }
    
    // 配置录制选项
    RecorderOptions.SourceOptions.RecordingSources = CreateSelectedRecordingSources();
    RecorderOptions.OutputOptions.RecorderMode = RecorderMode.Video;
    
    // 创建或更新录制器
    if (_rec == null)
    {
        _rec = Recorder.CreateRecorder(RecorderOptions);
        // 注册事件处理器
    }
    else
    {
        _rec.SetOptions(RecorderOptions);
    }
    
    // 开始录制
    string videoPath = GetOutputPath();
    _rec.Record(videoPath);
    IsRecording = true;
}
```

#### 暂停/恢复
```csharp
private void PauseButton_Click(object sender, RoutedEventArgs e)
{
    if (_rec.Status == RecorderStatus.Paused)
    {
        _rec.Resume();
    }
    else
    {
        _rec.Pause();
    }
}
```

### 编码配置

#### 视频编码选项
```csharp
// H.264 编码
RecorderOptions.VideoEncoderOptions.Encoder = VideoEncoder.H264;
RecorderOptions.VideoEncoderOptions.Framerate = 60;
RecorderOptions.VideoEncoderOptions.Quality = 70;
RecorderOptions.VideoEncoderOptions.BitrateMode = H264BitrateControlMode.Quality;

// H.265 编码
RecorderOptions.VideoEncoderOptions.Encoder = VideoEncoder.H265;
RecorderOptions.VideoEncoderOptions.BitrateMode = H265BitrateControlMode.Quality;
```

#### 音频编码选项
```csharp
RecorderOptions.AudioOptions.IsAudioEnabled = true;
RecorderOptions.AudioOptions.IsInputDeviceEnabled = true;
RecorderOptions.AudioOptions.IsOutputDeviceEnabled = true;
RecorderOptions.AudioOptions.AudioInputDevice = selectedMicrophone;
RecorderOptions.AudioOptions.AudioOutputDevice = selectedSpeaker;
```

### 覆盖层配置

#### 添加摄像头覆盖层
```csharp
var cameraOverlay = new VideoCaptureOverlay
{
    AnchorPoint = Anchor.TopLeft,
    Offset = new ScreenSize(100, 100),
    Size = new ScreenSize(0, 250), // 高度250，宽度自适应
    Device = selectedCamera
};
Overlays.Add(new OverlayModel { Overlay = cameraOverlay, IsEnabled = true });
```

#### 添加文本覆盖层
```csharp
var textOverlay = new TextOverlay
{
    AnchorPoint = Anchor.BottomRight,
    Text = "Recording...",
    FontSize = 20,
    FontColor = "#FFFFFF",
    Offset = new ScreenSize(10, 10)
};
```

## 6. 安装与部署

### 自动部署
插件编译后会自动通过 PostBuild 事件复制到主程序的 Plugins 目录。

### 手动部署
将以下文件复制到 `ColorVision\bin\x64\Release\net8.0-windows\Plugins\ScreenRecorder\` 目录：
- ScreenRecorder.dll
- ScreenRecorderLib.dll
- manifest.json
- README.md (可选)

## 7. 构建说明

### 基本构建
```bash
dotnet build Plugins/ScreenRecorder/ScreenRecorder.csproj -c Release
```

### 目标框架
- 主要目标: .NET 8.0-windows
- 平台支持: x64
- WPF 应用程序: 启用
- 版本: 1.2.0.3

### 依赖项
- ColorVision.Common: 通用工具库
- ColorVision.Themes: 主题支持
- ScreenRecorderLib.dll: 录制核心库（第三方）

## 8. 使用指南

### 基本录制流程

1. **启动插件**
   - 通过主程序菜单或快捷方式启动 ScreenRecorder

2. **选择录制源**
   - 在"Recording Sources"列表中选择要录制的源
   - 可多选显示器、窗口或摄像头
   - 设置每个源的输出位置和尺寸（可选）

3. **配置编码参数**
   - 选择视频编码格式（H.264/H.265/AV1）
   - 设置帧率（建议30-60fps）
   - 选择质量或码率控制模式
   - 配置输出分辨率

4. **配置音频**
   - 启用音频录制
   - 选择麦克风设备
   - 选择系统音频设备

5. **开始录制**
   - 点击"Record"按钮开始
   - 使用"Pause"按钮暂停/恢复
   - 点击"Stop"停止录制

6. **保存结果**
   - 录制完成后，文件路径显示在界面上
   - 默认保存到用户视频文件夹

### 高级功能

#### 多源组合录制
```
场景：同时录制主显示器和摄像头
1. 选中主显示器作为主录制源
2. 选中摄像头
3. 在摄像头设置中启用"Custom Position"
4. 设置摄像头位置为右下角 (Position: 1600, 900)
5. 设置摄像头尺寸 (Size: 320, 240)
6. 开始录制
```

#### 自定义输出区域
```
场景：只录制屏幕的一部分区域
1. 选择显示器
2. 启用"Custom Output Source Rect"
3. 设置 SourceRect (X: 0, Y: 0, Width: 1280, Height: 720)
4. 这将只录制左上角 1280x720 区域
```

#### 添加覆盖层
```
场景：在录制中添加Logo水印
1. 在 Overlays 配置中添加 ImageOverlay
2. 设置 SourcePath 为 Logo 图片路径
3. 设置 AnchorPoint 为 BottomRight
4. 设置 Offset 控制距离边缘的距离
5. 启用该覆盖层
```

### 配置文件位置
- 录制输出路径: `%USERPROFILE%\Videos\`
- 日志文件路径: 可在界面配置
- 快照保存路径: 可在界面配置

## 9. 配置项说明

### 录制选项

| 配置项 | 类型 | 默认值 | 说明 |
|--------|------|--------|------|
| Framerate | int | 60 | 录制帧率 |
| IsAudioEnabled | bool | true | 启用音频录制 |
| Encoder | VideoEncoder | H264 | 视频编码器 |
| Quality | int | 70 | 编码质量 (0-100) |
| Bitrate | int | 8000 | 码率 (kbps) |
| IsMousePointerEnabled | bool | true | 显示鼠标指针 |
| IsMouseClicksDetected | bool | false | 显示鼠标点击效果 |

### 输出选项

| 配置项 | 类型 | 默认值 | 说明 |
|--------|------|--------|------|
| RecorderMode | RecorderMode | Video | 录制模式 |
| OutputFileFormat | string | ".mp4" | 输出文件格式 |
| SourceRect | ScreenRect | null | 源区域（null=全屏） |
| OutputFrameSize | ScreenSize | null | 输出尺寸（null=原始） |

### 音频选项

| 配置项 | 类型 | 默认值 | 说明 |
|--------|------|--------|------|
| IsInputDeviceEnabled | bool | false | 启用麦克风 |
| IsOutputDeviceEnabled | bool | false | 启用系统音频 |
| AudioInputDevice | string | null | 麦克风设备ID |
| AudioOutputDevice | string | null | 音频输出设备ID |

## 10. 日志与诊断

### 启用日志
```csharp
IsLogToFileEnabled = true;
LogFilePath = @"C:\Logs\ScreenRecorder.log";
RecorderOptions.LogOptions.IsLogEnabled = true;
RecorderOptions.LogOptions.LogFilePath = LogFilePath;
```

### 日志级别
- **Info**: 录制开始、结束等关键事件
- **Debug**: 详细的帧率、编码信息
- **Error**: 错误和异常信息

### 常见问题排查

1. **录制失败**
   - 检查磁盘空间是否充足
   - 验证输出路径是否有写入权限
   - 查看日志文件中的错误信息
   - 确认录制源是否有效（窗口未关闭）

2. **音频无声音**
   - 检查"IsAudioEnabled"是否为 true
   - 验证音频设备是否正确选择
   - 确认系统音频设备是否正常工作
   - 检查音频驱动是否正常

3. **帧率低**
   - 降低录制分辨率
   - 使用硬件编码器
   - 减少录制源数量
   - 关闭不必要的覆盖层

4. **编码错误**
   - 确认系统支持所选编码器
   - 尝试使用不同的编码格式
   - 检查 GPU 驱动是否最新
   - 降低码率或质量设置

## 11. 性能优化建议

### 推荐配置

#### 高质量录制
```
分辨率: 1920x1080
帧率: 60fps
编码器: H.265 (HEVC)
质量: 85
码率模式: Quality
```

#### 平衡模式
```
分辨率: 1920x1080
帧率: 30fps
编码器: H.264
质量: 70
码率模式: Quality
```

#### 低资源模式
```
分辨率: 1280x720
帧率: 30fps
编码器: H.264
质量: 60
码率模式: UnconstrainedVBR
```

### 优化技巧

1. **使用硬件加速**
   - 启用 GPU 硬件编码
   - 优先选择支持的现代编码器

2. **合理选择帧率**
   - 游戏录制: 60fps
   - 教程录制: 30fps
   - 演示录制: 24-30fps

3. **控制录制源数量**
   - 避免同时录制过多源
   - 优化覆盖层使用

4. **定期清理**
   - 及时删除测试录制文件
   - 清理日志文件

## 12. 代码优化建议

### 当前代码的优点
1. **清晰的职责分离**: Sources 类独立封装
2. **良好的事件处理**: 完整的录制生命周期事件
3. **灵活的配置**: 支持丰富的录制选项
4. **资源管理**: 实现了资源清理机制

### 改进方向

#### 1. 减少代码重复
CheckableRecordable* 类中存在大量重复代码，建议：
- 创建基类 `CheckableRecordingSourceBase` 提取公共属性
- 使用泛型减少代码重复

#### 2. 增强错误处理
```csharp
// 当前
_rec.Record(videoPath);

// 建议
try
{
    if (!Directory.Exists(Path.GetDirectoryName(videoPath)))
    {
        Directory.CreateDirectory(Path.GetDirectoryName(videoPath));
    }
    _rec.Record(videoPath);
}
catch (Exception ex)
{
    MessageBox.Show($"录制失败: {ex.Message}", "错误", 
        MessageBoxButton.OK, MessageBoxImage.Error);
    CleanupResources();
}
```

#### 3. 改进资源释放
```csharp
// 实现 IDisposable
public partial class MainWindow : Window, INotifyPropertyChanged, IDisposable
{
    public void Dispose()
    {
        CleanupResources();
        _progressTimer?.Stop();
        GC.SuppressFinalize(this);
    }
}
```

#### 4. 添加 XML 文档注释
```csharp
/// <summary>
/// 创建选定的录制源列表
/// </summary>
/// <returns>录制源配置列表</returns>
private List<RecordingSourceBase> CreateSelectedRecordingSources()
{
    // ...
}
```

#### 5. 使用配置模式
```csharp
// 创建配置构建器
public class RecorderOptionsBuilder
{
    private RecorderOptions _options = RecorderOptions.Default;
    
    public RecorderOptionsBuilder WithFramerate(int fps)
    {
        _options.VideoEncoderOptions.Framerate = fps;
        return this;
    }
    
    public RecorderOptionsBuilder WithEncoder(VideoEncoder encoder)
    {
        _options.VideoEncoderOptions.Encoder = encoder;
        return this;
    }
    
    public RecorderOptions Build() => _options;
}
```

## 13. 版本与变更

### 当前版本
- **插件版本**: 1.0
- **项目版本**: 1.2.0.3
- **兼容版本**: ColorVision ≥ 1.3.12.34

### 主要特性
- 多源录制支持
- 高级编码选项
- 实时预览和控制
- 覆盖层支持
- 音频录制

### 未来规划
- [ ] 添加直播推流功能
- [ ] 支持区域选择工具
- [ ] 添加预设配置管理
- [ ] 实现定时录制功能
- [ ] 添加更多覆盖层效果

## 14. 兼容性

### 运行要求
- **操作系统**: Windows 10/11 x64
- **.NET 运行时**: .NET 8.0 Desktop Runtime
- **显卡**: 支持 DirectX 11+ (硬件加速)
- **权限要求**: 读写文件系统权限

### 编码器支持
| 编码器 | Windows 10 | Windows 11 | 硬件加速 |
|--------|-----------|-----------|----------|
| H.264  | ✓         | ✓         | ✓        |
| H.265  | ✓         | ✓         | ✓        |
| AV1    | 部分支持   | ✓         | 需GPU支持 |
| VP9    | ✓         | ✓         | ✓        |

## 15. 示例代码

### 简单录制示例
```csharp
// 创建录制器
var options = RecorderOptions.Default;
options.VideoEncoderOptions.Framerate = 30;
options.AudioOptions.IsAudioEnabled = true;

var recorder = Recorder.CreateRecorder(options);

// 配置录制源
var display = Recorder.GetDisplays().First();
options.SourceOptions.RecordingSources = new List<RecordingSourceBase>
{
    new DisplayRecordingSource(display)
};

// 开始录制
recorder.Record(@"C:\Videos\output.mp4");

// 停止录制
recorder.Stop();
```

### 多源录制示例
```csharp
var sources = new List<RecordingSourceBase>();

// 添加主显示器
var mainDisplay = Recorder.GetDisplays().First();
sources.Add(new DisplayRecordingSource(mainDisplay));

// 添加摄像头（画中画）
var camera = Recorder.GetSystemVideoCaptureDevices().First();
sources.Add(new VideoCaptureRecordingSource(camera)
{
    Position = new ScreenPoint(1600, 900),
    OutputSize = new ScreenSize(320, 240)
});

options.SourceOptions.RecordingSources = sources;
recorder.Record(@"C:\Videos\pip_output.mp4");
```

## 16. 许可说明

本插件继承主项目 ColorVision 的许可证条款。

**版权**: Copyright (C) 2025 ColorVision Corporation  
**公司**: ColorVision Corp.

## 17. 技术支持

### 问题反馈
- GitHub Issues: [提交问题](https://github.com/xincheng213618/scgd_general_wpf/issues)
- 文档问题: 请在 PR 中提出

### 参考资源
- [ScreenRecorderLib 文档](https://github.com/sskodje/ScreenRecorderLib)
- [WPF 开发指南](https://docs.microsoft.com/wpf)
- [ColorVision 插件开发](../../../docs/plugins/developing-a-plugin.md)

---

*文档版本: 1.0 | 最后更新: 2025-01-10 | 状态: 正式发布*
