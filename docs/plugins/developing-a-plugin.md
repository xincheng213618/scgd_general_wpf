# Developing a Plugin

---
**Metadata:**
- Title: Developing a Plugin - Step-by-Step Guide
- Status: draft
- Updated: 2024-09-28
- Author: ColorVision Development Team
---

## 简介

本文档提供创建 ColorVision 插件的完整指南，从最小插件示例到复杂功能实现，包括最佳实践、调试技巧和发布流程。

## 目录

1. [最小插件示例](#最小插件示例)
2. [开发环境设置](#开发环境设置)
3. [插件项目结构](#插件项目结构)
4. [核心功能实现](#核心功能实现)
5. [用户界面集成](#用户界面集成)
6. [调试与测试](#调试与测试)
7. [打包与发布](#打包与发布)
8. [最佳实践](#最佳实践)

## 最小插件示例

### Hello World 插件

以下是一个最简单的 ColorVision 插件示例：

```csharp
using ColorVision.UI.Plugins;
using System;
using System.Threading.Tasks;

[Plugin("hello.world", "Hello World Plugin")]
public class HelloWorldPlugin : IPlugin
{
    public string Id => "com.example.hello.world";
    public string Name => "Hello World Plugin";
    public Version Version => new Version(1, 0, 0);
    public string Description => "A simple hello world plugin";
    public string Author => "Your Name";
    
    public Task InitializeAsync(IPluginContext context)
    {
        // 插件初始化逻辑
        context.Logger.Information("Hello World Plugin initializing...");
        return Task.CompletedTask;
    }
    
    public Task StartAsync()
    {
        // 插件启动逻辑
        System.Windows.MessageBox.Show("Hello from ColorVision Plugin!");
        return Task.CompletedTask;
    }
    
    public Task StopAsync()
    {
        // 插件停止逻辑
        return Task.CompletedTask;
    }
    
    public Task ShutdownAsync()
    {
        // 插件清理逻辑
        return Task.CompletedTask;
    }
}
```

### 插件清单文件 (plugin.json)

```json
{
  "plugin": {
    "id": "com.example.hello.world",
    "name": "Hello World Plugin",
    "version": "1.0.0",
    "description": "A simple hello world plugin for demonstration",
    "author": "Your Name",
    "website": "https://example.com",
    "license": "MIT",
    
    "compatibility": {
      "minHostVersion": "3.0.0",
      "targetFramework": "net8.0-windows"
    },
    
    "assembly": {
      "fileName": "HelloWorldPlugin.dll",
      "entryPoint": "HelloWorldPlugin"
    },
    
    "ui": {
      "menuItems": [
        {
          "text": "Hello World",
          "command": "hello.world.show",
          "icon": "hello.png"
        }
      ]
    }
  }
}
```

## 开发环境设置

### 必需工具

1. **Visual Studio 2022** 或 **Visual Studio Code**
2. **.NET 8.0 SDK**
3. **ColorVision SDK** (NuGet 包)

### 创建插件项目

#### 使用项目模板

```bash
# 安装 ColorVision 插件模板
dotnet new install ColorVision.Plugin.Templates

# 创建新插件项目
dotnet new colorvision-plugin -n MyAwesomePlugin -o ./MyAwesomePlugin
```

#### 手动创建项目

```xml
<!-- MyAwesomePlugin.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0-windows</TargetFramework>
    <UseWPF>true</UseWPF>
    <OutputType>Library</OutputType>
    <CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="ColorVision.UI" Version="3.0.0" />
    <PackageReference Include="ColorVision.Engine" Version="3.0.0" />
    <PackageReference Include="ColorVision.Common" Version="3.0.0" />
  </ItemGroup>

  <ItemGroup>
    <Content Include="plugin.json">
      <CopyToOutputDirectory>Always</CopyToOutputDirectory>
    </Content>
    <Content Include="Assets\**\*">
      <CopyToOutputDirectory>Always</CopyToOutputDirectory>
    </Content>
  </ItemGroup>

  <Target Name="PostBuild" AfterTargets="PostBuildEvent">
    <Copy SourceFiles="$(TargetPath)" 
          DestinationFolder="$(SolutionDir)\ColorVision\bin\Debug\Plugins\$(ProjectName)\" />
    <Copy SourceFiles="plugin.json" 
          DestinationFolder="$(SolutionDir)\ColorVision\bin\Debug\Plugins\$(ProjectName)\" />
  </Target>
</Project>
```

## 插件项目结构

### 推荐目录结构

```
MyAwesomePlugin/
├── src/
│   ├── MyAwesomePlugin.cs          # 主插件类
│   ├── Services/                   # 服务类
│   │   ├── IMyService.cs
│   │   └── MyService.cs
│   ├── ViewModels/                 # 视图模型
│   │   └── MainViewModel.cs
│   ├── Views/                      # WPF 视图
│   │   ├── MainView.xaml
│   │   └── MainView.xaml.cs
│   └── Commands/                   # 命令实现
│       └── MyCommand.cs
├── Assets/                         # 资源文件
│   ├── Icons/
│   │   └── plugin-icon.png
│   └── Styles/
│       └── PluginStyles.xaml
├── Config/                         # 配置文件
│   └── default.config
├── plugin.json                     # 插件清单
├── README.md                       # 插件说明
└── MyAwesomePlugin.csproj          # 项目文件
```

### 命名空间约定

```csharp
namespace ColorVision.Plugins.MyAwesome
{
    // 主插件类
    public class MyAwesomePlugin : IPlugin { }
    
    namespace Services
    {
        // 服务接口和实现
        public interface IMyAwesomeService { }
        public class MyAwesomeService : IMyAwesomeService { }
    }
    
    namespace ViewModels
    {
        // 视图模型类
        public class MyAwesomeViewModel : ViewModelBase { }
    }
    
    namespace Commands
    {
        // 命令实现
        public class MyAwesomeCommand : ICommand { }
    }
}
```

## 核心功能实现

### 插件基类实现

```csharp
using ColorVision.UI.Plugins;
using ColorVision.Common.MVVM;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using System;
using System.Threading.Tasks;
using System.Windows.Input;

namespace ColorVision.Plugins.MyAwesome
{
    [Plugin("my.awesome.plugin", "My Awesome Plugin")]
    public class MyAwesomePlugin : ViewModelBase, IPlugin
    {
        private IPluginContext _context;
        private IMyAwesomeService _service;
        private ILogger _logger;
        
        public string Id => "com.example.myawesome";
        public string Name => "My Awesome Plugin";
        public Version Version => new Version(1, 0, 0);
        public string Description => "An awesome plugin that does amazing things";
        public string Author => "Your Name";
        
        // Commands
        public ICommand ShowMainWindowCommand { get; private set; }
        public ICommand ProcessImageCommand { get; private set; }
        
        public async Task InitializeAsync(IPluginContext context)
        {
            _context = context;
            _logger = context.Logger;
            
            _logger.Information("Initializing {PluginName} v{Version}", Name, Version);
            
            // 注册服务
            RegisterServices(context.ServiceProvider);
            
            // 初始化命令
            InitializeCommands();
            
            // 加载配置
            await LoadConfigurationAsync();
            
            // 初始化服务
            _service = context.ServiceProvider.GetService<IMyAwesomeService>();
            await _service.InitializeAsync();
            
            _logger.Information("{PluginName} initialized successfully", Name);
        }
        
        public async Task StartAsync()
        {
            _logger.Information("Starting {PluginName}", Name);
            
            // 启动后台服务
            await _service.StartAsync();
            
            // 注册事件处理器
            RegisterEventHandlers();
            
            _logger.Information("{PluginName} started successfully", Name);
        }
        
        public async Task StopAsync()
        {
            _logger.Information("Stopping {PluginName}", Name);
            
            // 取消事件注册
            UnregisterEventHandlers();
            
            // 停止服务
            await _service?.StopAsync();
            
            _logger.Information("{PluginName} stopped successfully", Name);
        }
        
        public async Task ShutdownAsync()
        {
            _logger.Information("Shutting down {PluginName}", Name);
            
            // 清理资源
            await _service?.DisposeAsync();
            
            // 保存配置
            await SaveConfigurationAsync();
            
            _logger.Information("{PluginName} shutdown completed", Name);
        }
        
        private void RegisterServices(IServiceProvider serviceProvider)
        {
            // 如果使用 DI 容器，在这里注册服务
            var services = new ServiceCollection();
            services.AddSingleton<IMyAwesomeService, MyAwesomeService>();
            // 其他服务注册...
        }
        
        private void InitializeCommands()
        {
            ShowMainWindowCommand = new RelayCommand(ShowMainWindow, CanShowMainWindow);
            ProcessImageCommand = new RelayCommand<string>(ProcessImage, CanProcessImage);
        }
        
        private void ShowMainWindow(object parameter)
        {
            var viewModel = new MainViewModel(_service, _logger);
            var view = new MainView { DataContext = viewModel };
            view.Show();
        }
        
        private bool CanShowMainWindow(object parameter)
        {
            return _service?.IsReady == true;
        }
        
        private async void ProcessImage(string imagePath)
        {
            try
            {
                await _service.ProcessImageAsync(imagePath);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to process image: {ImagePath}", imagePath);
            }
        }
        
        private bool CanProcessImage(string imagePath)
        {
            return !string.IsNullOrEmpty(imagePath) && _service?.IsReady == true;
        }
    }
}
```

### 服务层实现

```csharp
public interface IMyAwesomeService : IAsyncDisposable
{
    bool IsReady { get; }
    Task InitializeAsync();
    Task StartAsync();
    Task StopAsync();
    Task ProcessImageAsync(string imagePath);
    event EventHandler<ProcessingCompletedEventArgs> ProcessingCompleted;
}

public class MyAwesomeService : IMyAwesomeService
{
    private readonly ILogger _logger;
    private readonly IEngineService _engineService;
    private bool _isReady;
    
    public bool IsReady => _isReady;
    public event EventHandler<ProcessingCompletedEventArgs> ProcessingCompleted;
    
    public MyAwesomeService(ILogger logger, IEngineService engineService)
    {
        _logger = logger;
        _engineService = engineService;
    }
    
    public async Task InitializeAsync()
    {
        _logger.Information("Initializing MyAwesome service");
        
        // 初始化算法引擎
        await InitializeAlgorithmEngine();
        
        _isReady = true;
        _logger.Information("MyAwesome service initialized");
    }
    
    public async Task StartAsync()
    {
        if (!_isReady)
            throw new InvalidOperationException("Service not initialized");
            
        _logger.Information("Starting MyAwesome service");
        
        // 订阅引擎事件
        _engineService.TemplateExecuted += OnTemplateExecuted;
        
        _logger.Information("MyAwesome service started");
    }
    
    public async Task StopAsync()
    {
        _logger.Information("Stopping MyAwesome service");
        
        // 取消事件订阅
        _engineService.TemplateExecuted -= OnTemplateExecuted;
        
        _logger.Information("MyAwesome service stopped");
    }
    
    public async Task ProcessImageAsync(string imagePath)
    {
        if (!_isReady)
            throw new InvalidOperationException("Service not ready");
            
        _logger.Information("Processing image: {ImagePath}", imagePath);
        
        try
        {
            // 使用 ColorVision 引擎处理图像
            var template = await _engineService.GetTemplateAsync("awesome-algorithm");
            var result = await _engineService.ExecuteTemplateAsync(template, new
            {
                ImagePath = imagePath,
                Parameters = GetProcessingParameters()
            });
            
            // 处理结果
            var eventArgs = new ProcessingCompletedEventArgs
            {
                ImagePath = imagePath,
                Result = result,
                Success = result.Success
            };
            
            ProcessingCompleted?.Invoke(this, eventArgs);
            
            _logger.Information("Image processing completed: {ImagePath}", imagePath);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Image processing failed: {ImagePath}", imagePath);
            throw;
        }
    }
    
    private void OnTemplateExecuted(object sender, TemplateExecutedEventArgs e)
    {
        _logger.Information("Template executed: {TemplateId}", e.TemplateId);
    }
    
    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        _isReady = false;
    }
}
```

## 用户界面集成

### WPF 视图实现

```xml
<!-- MainView.xaml -->
<Window x:Class="ColorVision.Plugins.MyAwesome.Views.MainView"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
        xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
        mc:Ignorable="d"
        Title="My Awesome Plugin" Height="450" Width="800"
        WindowStartupLocation="CenterOwner">
    
    <Window.Resources>
        <Style TargetType="Button" BasedOn="{StaticResource {x:Type Button}}">
            <Setter Property="Margin" Value="5"/>
            <Setter Property="Padding" Value="10,5"/>
            <Setter Property="MinWidth" Value="100"/>
        </Style>
    </Window.Resources>
    
    <Grid Margin="10">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="*"/>
            <RowDefinition Height="Auto"/>
        </Grid.RowDefinitions>
        
        <!-- Header -->
        <StackPanel Grid.Row="0" Orientation="Horizontal" Margin="0,0,0,10">
            <TextBlock Text="My Awesome Plugin" FontSize="18" FontWeight="Bold"/>
            <TextBlock Text="{Binding Version}" Margin="10,0,0,0" VerticalAlignment="Bottom"/>
        </StackPanel>
        
        <!-- Main Content -->
        <TabControl Grid.Row="1">
            <TabItem Header="Image Processing">
                <Grid Margin="10">
                    <Grid.RowDefinitions>
                        <RowDefinition Height="Auto"/>
                        <RowDefinition Height="*"/>
                        <RowDefinition Height="Auto"/>
                    </Grid.RowDefinitions>
                    
                    <!-- File Selection -->
                    <StackPanel Grid.Row="0" Orientation="Horizontal" Margin="0,0,0,10">
                        <TextBox x:Name="ImagePathTextBox" Width="400" 
                                 Text="{Binding SelectedImagePath, UpdateSourceTrigger=PropertyChanged}"/>
                        <Button Content="Browse..." Click="BrowseButton_Click"/>
                    </StackPanel>
                    
                    <!-- Image Preview -->
                    <Border Grid.Row="1" BorderBrush="Gray" BorderThickness="1" Margin="0,0,0,10">
                        <Image x:Name="ImagePreview" 
                               Source="{Binding PreviewImage}" 
                               Stretch="Uniform"/>
                    </Border>
                    
                    <!-- Processing Controls -->
                    <StackPanel Grid.Row="2" Orientation="Horizontal">
                        <Button Content="Process Image" 
                                Command="{Binding ProcessImageCommand}"
                                CommandParameter="{Binding SelectedImagePath}"/>
                        <Button Content="Save Result" 
                                Command="{Binding SaveResultCommand}"
                                IsEnabled="{Binding HasResult}"/>
                        <ProgressBar x:Name="ProcessingProgress" 
                                     Width="200" Height="20" Margin="10,0"
                                     Value="{Binding ProcessingProgress}"
                                     IsIndeterminate="{Binding IsProcessing}"/>
                    </StackPanel>
                </Grid>
            </TabItem>
            
            <TabItem Header="Settings">
                <StackPanel Margin="10">
                    <TextBlock Text="Algorithm Settings" FontWeight="Bold" Margin="0,0,0,10"/>
                    
                    <StackPanel Orientation="Horizontal" Margin="0,5">
                        <TextBlock Text="Threshold:" Width="100" VerticalAlignment="Center"/>
                        <Slider x:Name="ThresholdSlider" 
                                Width="200" Minimum="0" Maximum="1" 
                                Value="{Binding ThresholdValue}" 
                                TickFrequency="0.1" IsSnapToTickEnabled="True"/>
                        <TextBlock Text="{Binding ThresholdValue, StringFormat=F2}" 
                                   Margin="10,0" VerticalAlignment="Center"/>
                    </StackPanel>
                    
                    <CheckBox Content="Enable GPU Acceleration" 
                              IsChecked="{Binding EnableGpuAcceleration}" 
                              Margin="0,10"/>
                              
                    <CheckBox Content="Save Processing Log" 
                              IsChecked="{Binding SaveProcessingLog}" 
                              Margin="0,5"/>
                </StackPanel>
            </TabItem>
        </TabControl>
        
        <!-- Status Bar -->
        <StatusBar Grid.Row="2">
            <StatusBarItem>
                <TextBlock Text="{Binding StatusMessage}"/>
            </StatusBarItem>
            <StatusBarItem HorizontalAlignment="Right">
                <TextBlock Text="{Binding LastProcessingTime, StringFormat='Last: {0:HH:mm:ss}'}"/>
            </StatusBarItem>
        </StatusBar>
    </Grid>
</Window>
```

### 视图模型实现

```csharp
public class MainViewModel : ViewModelBase
{
    private readonly IMyAwesomeService _service;
    private readonly ILogger _logger;
    
    public string Version => "v1.0.0";
    
    public string SelectedImagePath
    {
        get => _selectedImagePath;
        set { _selectedImagePath = value; OnPropertyChanged(); UpdatePreview(); }
    }
    private string _selectedImagePath;
    
    public BitmapImage PreviewImage
    {
        get => _previewImage;
        set { _previewImage = value; OnPropertyChanged(); }
    }
    private BitmapImage _previewImage;
    
    public double ThresholdValue
    {
        get => _thresholdValue;
        set { _thresholdValue = value; OnPropertyChanged(); }
    }
    private double _thresholdValue = 0.5;
    
    public bool EnableGpuAcceleration
    {
        get => _enableGpuAcceleration;
        set { _enableGpuAcceleration = value; OnPropertyChanged(); }
    }
    private bool _enableGpuAcceleration = true;
    
    public bool IsProcessing
    {
        get => _isProcessing;
        set { _isProcessing = value; OnPropertyChanged(); }
    }
    private bool _isProcessing;
    
    public string StatusMessage
    {
        get => _statusMessage;
        set { _statusMessage = value; OnPropertyChanged(); }
    }
    private string _statusMessage = "Ready";
    
    // Commands
    public ICommand ProcessImageCommand { get; }
    public ICommand SaveResultCommand { get; }
    public ICommand BrowseImageCommand { get; }
    
    public MainViewModel(IMyAwesomeService service, ILogger logger)
    {
        _service = service;
        _logger = logger;
        
        ProcessImageCommand = new AsyncRelayCommand(ProcessImageAsync, CanProcessImage);
        SaveResultCommand = new RelayCommand(SaveResult, CanSaveResult);
        BrowseImageCommand = new RelayCommand(BrowseImage);
        
        // 订阅服务事件
        _service.ProcessingCompleted += OnProcessingCompleted;
    }
    
    private async Task ProcessImageAsync()
    {
        try
        {
            IsProcessing = true;
            StatusMessage = "Processing image...";
            
            await _service.ProcessImageAsync(SelectedImagePath);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Processing failed");
            StatusMessage = $"Processing failed: {ex.Message}";
        }
        finally
        {
            IsProcessing = false;
        }
    }
    
    private void OnProcessingCompleted(object sender, ProcessingCompletedEventArgs e)
    {
        StatusMessage = e.Success ? "Processing completed successfully" : "Processing failed";
        LastProcessingTime = DateTime.Now;
    }
}
```

### 菜单和工具栏集成

```csharp
public class MenuIntegration
{
    public static void RegisterMenuItems(IPluginContext context)
    {
        // 添加主菜单项
        var mainMenu = context.GetService<IMainMenu>();
        var pluginMenu = new MenuItem
        {
            Header = "My Awesome Plugin",
            Icon = LoadIcon("plugin-icon.png")
        };
        
        // 子菜单项
        pluginMenu.Items.Add(new MenuItem
        {
            Header = "Open Main Window",
            Command = new RelayCommand(() => ShowMainWindow(context)),
            InputGestureText = "Ctrl+Shift+A"
        });
        
        pluginMenu.Items.Add(new Separator());
        
        pluginMenu.Items.Add(new MenuItem
        {
            Header = "Quick Process",
            Command = new RelayCommand(() => QuickProcess(context))
        });
        
        mainMenu.Items.Add(pluginMenu);
        
        // 注册快捷键
        RegisterShortcuts(context);
    }
    
    private static void RegisterShortcuts(IPluginContext context)
    {
        var shortcutService = context.GetService<IShortcutService>();
        
        shortcutService.RegisterShortcut(
            new KeyGesture(Key.A, ModifierKeys.Control | ModifierKeys.Shift),
            "MyAwesome.ShowMainWindow",
            () => ShowMainWindow(context));
    }
}
```

## 调试与测试

### 调试配置

```json
// .vscode/launch.json (Visual Studio Code)
{
  "version": "0.2.0",
  "configurations": [
    {
      "name": "Debug Plugin",
      "type": "coreclr",
      "request": "launch",
      "program": "${workspaceFolder}/../ColorVision/bin/Debug/ColorVision.exe",
      "args": ["--plugin-debug", "--plugin-path", "${workspaceFolder}/bin/Debug"],
      "cwd": "${workspaceFolder}/../ColorVision/bin/Debug",
      "env": {
        "PLUGIN_DEBUG": "true"
      },
      "sourceFileMap": {
        "/Views": "${workspaceFolder}/Views"
      }
    }
  ]
}
```

### 单元测试

```csharp
using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using ColorVision.Plugins.MyAwesome;

public class MyAwesomeServiceTests
{
    private readonly Mock<ILogger> _mockLogger;
    private readonly Mock<IEngineService> _mockEngineService;
    private readonly MyAwesomeService _service;
    
    public MyAwesomeServiceTests()
    {
        _mockLogger = new Mock<ILogger>();
        _mockEngineService = new Mock<IEngineService>();
        _service = new MyAwesomeService(_mockLogger.Object, _mockEngineService.Object);
    }
    
    [Fact]
    public async Task InitializeAsync_ShouldSetIsReadyToTrue()
    {
        // Act
        await _service.InitializeAsync();
        
        // Assert
        Assert.True(_service.IsReady);
    }
    
    [Fact]
    public async Task ProcessImageAsync_WithValidPath_ShouldCompleteSuccessfully()
    {
        // Arrange
        await _service.InitializeAsync();
        var imagePath = "test-image.jpg";
        var mockResult = new ProcessingResult { Success = true };
        
        _mockEngineService.Setup(x => x.ExecuteTemplateAsync(It.IsAny<object>(), It.IsAny<object>()))
                          .ReturnsAsync(mockResult);
        
        // Act & Assert
        await _service.ProcessImageAsync(imagePath);
        
        _mockEngineService.Verify(x => x.ExecuteTemplateAsync(It.IsAny<object>(), It.IsAny<object>()), Times.Once);
    }
}
```

### 集成测试

```csharp
[Collection("Plugin Integration Tests")]
public class PluginIntegrationTests : IClassFixture<PluginTestFixture>
{
    private readonly PluginTestFixture _fixture;
    
    public PluginIntegrationTests(PluginTestFixture fixture)
    {
        _fixture = fixture;
    }
    
    [Fact]
    public async Task Plugin_ShouldLoadAndInitialize()
    {
        // Arrange
        var pluginManager = _fixture.PluginManager;
        var descriptor = _fixture.CreatePluginDescriptor();
        
        // Act
        var success = await pluginManager.LoadPluginAsync(descriptor);
        
        // Assert
        Assert.True(success);
        Assert.True(pluginManager.IsPluginLoaded("com.example.myawesome"));
    }
}
```

## 打包与发布

### MSBuild 打包脚本

```xml
<!-- Build.targets -->
<Project>
  <Target Name="PackagePlugin" AfterTargets="Build">
    <PropertyGroup>
      <PackageDir>$(OutputPath)Package\</PackageDir>
    </PropertyGroup>
    
    <!-- 创建包目录 -->
    <MakeDir Directories="$(PackageDir)" />
    
    <!-- 复制插件文件 -->
    <ItemGroup>
      <PluginFiles Include="$(OutputPath)**\*.*" Exclude="$(OutputPath)**\*.pdb;$(OutputPath)**\*.xml" />
    </ItemGroup>
    
    <Copy SourceFiles="@(PluginFiles)" 
          DestinationFiles="@(PluginFiles->'$(PackageDir)%(RecursiveDir)%(Filename)%(Extension)')" />
    
    <!-- 创建 ZIP 包 -->
    <ZipDirectory SourceDirectory="$(PackageDir)" 
                  DestinationFile="$(OutputPath)$(ProjectName)-$(Version).zip" />
  </Target>
</Project>
```

### 自动化发布脚本

```powershell
# Deploy-Plugin.ps1
param(
    [Parameter(Mandatory=$true)]
    [string]$Version,
    
    [Parameter(Mandatory=$false)]
    [string]$Configuration = "Release"
)

# 构建插件
dotnet build -c $Configuration

# 运行测试
dotnet test -c $Configuration --no-build

if ($LASTEXITCODE -ne 0) {
    Write-Error "Tests failed. Deployment aborted."
    exit 1
}

# 打包插件
dotnet msbuild -t:PackagePlugin -p:Configuration=$Configuration

# 上传到插件仓库
$packagePath = "bin\$Configuration\MyAwesomePlugin-$Version.zip"
Upload-PluginPackage -Path $packagePath -Version $Version

Write-Host "Plugin deployed successfully: $Version"
```

## 最佳实践

### 错误处理

```csharp
public class PluginErrorHandler
{
    private readonly ILogger _logger;
    
    public async Task<T> ExecuteWithErrorHandlingAsync<T>(Func<Task<T>> operation, string operationName)
    {
        try
        {
            return await operation();
        }
        catch (OperationCanceledException)
        {
            _logger.Information("Operation cancelled: {OperationName}", operationName);
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Operation failed: {OperationName}", operationName);
            
            // 根据异常类型决定是否重试
            if (IsRetriableException(ex))
            {
                return await RetryOperation(operation, operationName);
            }
            
            throw;
        }
    }
}
```

### 性能优化

```csharp
public class PerformanceOptimizer
{
    // 使用对象池减少内存分配
    private readonly ObjectPool<StringBuilder> _stringBuilderPool;
    
    // 缓存昂贵的计算结果
    private readonly MemoryCache _computationCache;
    
    // 异步操作避免阻塞UI线程
    public async Task<ProcessingResult> ProcessImageAsync(string imagePath)
    {
        return await Task.Run(() => ProcessImageCore(imagePath));
    }
    
    // 合理使用ConfigureAwait
    private async Task<string> LoadConfigurationAsync()
    {
        var content = await File.ReadAllTextAsync(ConfigPath).ConfigureAwait(false);
        return content;
    }
}
```

### 资源管理

```csharp
public class ResourceManager : IDisposable
{
    private readonly List<IDisposable> _disposables = new();
    private bool _disposed;
    
    public void RegisterDisposable(IDisposable disposable)
    {
        _disposables.Add(disposable);
    }
    
    public void Dispose()
    {
        if (_disposed) return;
        
        foreach (var disposable in _disposables)
        {
            try
            {
                disposable?.Dispose();
            }
            catch (Exception ex)
            {
                // 记录但不抛出异常
                _logger.Warning(ex, "Error disposing resource");
            }
        }
        
        _disposed = true;
    }
}
```

### 配置管理

```csharp
public class PluginConfiguration
{
    public double DefaultThreshold { get; set; } = 0.5;
    public bool EnableGpuAcceleration { get; set; } = true;
    public string OutputDirectory { get; set; } = "./Output";
    
    public static async Task<PluginConfiguration> LoadAsync(string configPath)
    {
        if (!File.Exists(configPath))
        {
            var defaultConfig = new PluginConfiguration();
            await SaveAsync(defaultConfig, configPath);
            return defaultConfig;
        }
        
        var json = await File.ReadAllTextAsync(configPath);
        return JsonSerializer.Deserialize<PluginConfiguration>(json);
    }
    
    public static async Task SaveAsync(PluginConfiguration config, string configPath)
    {
        var json = JsonSerializer.Serialize(config, new JsonSerializerOptions
        {
            WriteIndented = true
        });
        
        await File.WriteAllTextAsync(configPath, json);
    }
}
```

### 本地化支持

```csharp
// Resources/Strings.resx (默认语言)
// Resources/Strings.zh-CN.resx (中文)
// Resources/Strings.en-US.resx (英文)

public static class Strings
{
    private static readonly ResourceManager ResourceManager = 
        new ResourceManager("ColorVision.Plugins.MyAwesome.Resources.Strings", 
                           typeof(Strings).Assembly);
    
    public static string GetString(string name)
    {
        return ResourceManager.GetString(name) ?? $"[Missing: {name}]";
    }
    
    public static string ProcessingCompleted => GetString(nameof(ProcessingCompleted));
    public static string ProcessingFailed => GetString(nameof(ProcessingFailed));
}
```

---

*最后更新: 2024-09-28 | 状态: draft*