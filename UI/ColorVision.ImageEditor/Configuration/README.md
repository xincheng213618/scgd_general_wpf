# ColorVision.ImageEditor.Configuration 模块

## 概述

本模块提供 ImageEditor 的配置管理和命令模式实现，支持：

1. **统一配置接口** (`IEditorConfiguration`) - 类型安全的配置管理
2. **命令模式 + 差分存储** (`ICommandManager`) - 高效的 Undo/Redo 实现
3. **服务定位器** (`IServiceLocator`) - 依赖注入和解耦

## 核心组件

### 1. 配置管理

```csharp
// 初始化配置
var config = new ImageEditorConfiguration("MyConfig");
config.Settings.ShowGrid = true;
config.Settings.GridSize = 20;

// 注册到服务定位器
ServiceLocator.Instance.Register<IEditorConfiguration>(config);
```

### 2. 命令管理

```csharp
// 初始化命令管理器
ImageEditorInitializer.Initialize();

// 或使用自定义配置
var config = new ImageEditorConfiguration();
ImageEditorInitializer.Initialize(config);

// 获取命令管理器
var commandManager = ServiceLocator.Instance.GetCommandManager();
```

### 3. DrawCanvas 集成

```csharp
// 使用新的命令系统
var canvas = new DrawCanvas();
var cmdManager = new DrawCanvasCommandManager(canvas)
{
    UseNewCommandSystem = true,
    CommandManager = ServiceLocator.Instance.GetCommandManager()
};

// 添加 Visual（自动加入 Undo 栈）
cmdManager.AddVisual(visual);

// 撤销/重做
cmdManager.Undo();
cmdManager.Redo();
```

### 4. 事务支持

```csharp
// 批量操作使用事务
var transactional = ServiceLocator.Instance.GetService<ITransactionalCommandManager>();

transactional.BeginTransaction("批量添加");
try
{
    foreach (var visual in visuals)
    {
        var cmd = new AddVisualCommand(canvas, visual);
        transactional.Execute(cmd);
    }
    transactional.CommitTransaction();
}
catch
{
    transactional.RollbackTransaction();
    throw;
}
```

### 5. 属性变更命令

```csharp
// 创建属性变更命令
var command = new PropertyChangeCommand<MyClass, int>(
    target: myObject,
    propertyName: "Width",
    getter: () => myObject.Width,
    setter: (v) => myObject.Width = v,
    newValue: 100
);

// 执行
commandManager.Execute(command);
```

## 文件结构

```
Configuration/
├── IEditorConfiguration.cs      # 配置接口定义
├── ICommandManager.cs           # 命令管理器接口
├── DeltaCommandBase.cs          # 差分命令基类和实现
├── CommandManager.cs            # 命令管理器实现
├── EditorConfiguration.cs       # 配置基类实现
├── ServiceLocator.cs            # 服务定位器/依赖注入
├── ImageEditorConfiguration.cs  # ImageEditor 专用配置
├── ConfigurableViewModelBase.cs # 支持配置的 ViewModel 基类
├── DrawCanvasCommandAdapter.cs  # DrawCanvas 命令适配器
├── DrawCanvasCommandManager.cs  # DrawCanvas 命令管理器包装
└── ImageEditorInitializer.cs    # 初始化器
```

## 迁移指南

### 从旧版 ActionCommand 迁移

**旧代码：**
```csharp
canvas.AddVisualCommand(visual);  // 内部创建 ActionCommand
```

**新代码：**
```csharp
// 方式1: 使用适配器
var cmdManager = new DrawCanvasCommandManager(canvas)
{
    UseNewCommandSystem = true,
    CommandManager = ServiceLocator.Instance.GetCommandManager()
};
cmdManager.AddVisual(visual);

// 方式2: 直接使用命令
var command = new AddVisualCommand(canvas, visual);
commandManager.Execute(command);
```

### 配置迁移

**旧代码：**
```csharp
var config = new ImageViewConfig();
config.SetValue("key", value);
```

**新代码：**
```csharp
var config = new ImageEditorConfiguration();
config.Settings.ShowGrid = true;  // 强类型属性
// 或自定义配置项
config.SetItem(new MyConfigItem { Key = "key", ... });
```

## 架构图

```
┌─────────────────────────────────────────────────────────────┐
│                    ImageEditor 模块                          │
├─────────────────────────────────────────────────────────────┤
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐      │
│  │   DrawCanvas │  │  ViewModel   │  │   Config     │      │
│  └──────┬───────┘  └──────┬───────┘  └──────┬───────┘      │
│         │                 │                 │              │
│         └─────────────────┼─────────────────┘              │
│                           │                                │
│         ┌─────────────────┼─────────────────┐              │
│         │                 │                 │              │
│  ┌──────▼───────┐  ┌──────▼───────┐  ┌──────▼───────┐      │
│  │ ICommand     │  │ IEditor      │  │ IService     │      │
│  │ Manager      │  │ Configuration│  │ Locator      │      │
│  └──────────────┘  └──────────────┘  └──────────────┘      │
│         │                 │                 │              │
│         └─────────────────┼─────────────────┘              │
│                           │                                │
│                    ┌──────▼───────┐                        │
│                    │   具体实现    │                        │
│                    │ CommandManager│                        │
│                    │ EditorConfig  │                        │
│                    │ ServiceLocator│                        │
│                    └──────────────┘                        │
└─────────────────────────────────────────────────────────────┘
```

## 注意事项

1. **线程安全**：ServiceLocator 是线程安全的，但配置对象本身不是
2. **内存管理**：命令历史会自动限制大小（默认100条），可通过 `MaxHistorySize` 调整
3. **兼容性**：旧版 `ActionCommand` 系统仍然可用，通过 `DrawCanvasCommandManager` 可以逐步迁移
