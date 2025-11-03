# SystemMonitor Plugin - 性能监控插件

## 概述

SystemMonitor 是 ColorVision 的系统性能监控插件，提供实时的系统资源监控功能，包括 CPU 使用率、内存占用、磁盘空间和系统时间显示。

**版本信息**:
- 当前版本: v1.0.1
- 最低要求: ColorVision ≥ 1.3.12.23
- 最后更新: 2025-10-10

## 主要功能

### 1. 性能监控
- **CPU 监控**: 
  - 系统总体 CPU 使用率
  - 当前应用程序 CPU 使用率
  - 实时百分比显示

- **内存监控**:
  - 系统总内存和可用内存
  - 当前应用程序内存占用
  - 内存使用百分比可视化

- **磁盘监控**:
  - 多磁盘驱动器支持
  - 磁盘总容量和可用空间
  - 支持各种磁盘格式

### 2. 状态栏集成
- 可选显示当前系统时间
- 可选显示当前应用内存使用
- 自定义时间格式
- 热更新配置

### 3. 缓存管理
- 一键清理应用缓存
- 清理系统日志文件
- 详细的清理反馈

## 快速开始

### 启用插件

1. 确保 ColorVision 版本 ≥ 1.3.12.23
2. 插件会在启动时自动加载
3. 在菜单栏查找 **工具 → 性能监控** 选项

### 配置监控

1. 打开 **设置 → 性能监控**
2. 配置以下选项：
   - **更新速度**: 设置监控数据刷新间隔（最小 100ms，推荐 500-1000ms）
   - **日期格式**: 自定义时间显示格式
   - **显示时间**: 在状态栏显示系统时间
   - **显示RAM**: 在状态栏显示内存使用情况

### 查看监控数据

#### 方法一：通过菜单
1. 点击 **工具 → 性能监控**
2. 在弹出窗口查看详细监控信息

#### 方法二：通过状态栏
1. 启用状态栏显示选项
2. 在主窗口底部状态栏查看关键指标

## 配置说明

### 配置项详解

| 配置项 | 类型 | 默认值 | 说明 | 限制 |
|--------|------|--------|------|------|
| UpdateSpeed | int | 1000 | 更新间隔（毫秒） | 最小 100ms |
| DefaultTimeFormat | string | "yyyy/MM/dd HH:mm:ss" | 时间格式 | 有效的 .NET 日期格式 |
| IsShowTime | bool | false | 状态栏显示时间 | - |
| IsShowRAM | bool | false | 状态栏显示内存 | - |

### 时间格式示例

常用的时间格式字符串：
- `yyyy/MM/dd HH:mm:ss` - 2025/10/10 16:30:45
- `HH:mm:ss` - 16:30:45
- `yyyy-MM-dd` - 2025-10-10
- `MM/dd HH:mm` - 10/10 16:30

更多格式请参考 [.NET DateTime 格式说明](https://docs.microsoft.com/dotnet/standard/base-types/custom-date-and-time-format-strings)

## 性能优化建议

### 更新频率设置

根据使用场景选择合适的更新频率：

| 场景 | 推荐频率 | 说明 |
|------|---------|------|
| 日常监控 | 1000-2000ms | 平衡性能和实时性 |
| 性能调试 | 500-1000ms | 更频繁的更新 |
| 低功耗模式 | 2000-5000ms | 降低系统负载 |
| 后台运行 | 3000-10000ms | 最小化影响 |

**重要提示**: 
- 更新频率低于 500ms 可能导致显著的 CPU 占用增加
- 建议不要低于 100ms（系统强制限制）

### 资源使用优化

1. **CPU 优化**:
   - 在不需要实时监控时关闭状态栏显示
   - 适当增加更新间隔
   - 避免同时运行多个性能监控工具

2. **内存优化**:
   - 插件已实现完善的资源清理机制
   - 定期使用缓存清理功能
   - 长期运行建议重启应用

## 故障排除

### 常见问题

#### 1. 性能计数器初始化失败

**症状**: CPU/内存数据不显示或显示为 0

**解决方案**:
```powershell
# 方案 1: 重建性能计数器数据库
lodctr /r

# 方案 2: 检查性能计数器服务
services.msc  # 查找 "Performance Logs & Alerts"
```

**权限要求**: 可能需要管理员权限

#### 2. 状态栏不显示

**检查清单**:
- [ ] IsShowTime / IsShowRAM 是否已启用
- [ ] 性能计数器是否初始化成功
- [ ] ColorVision 主窗口是否已完全加载

#### 3. 时间格式错误

**症状**: 状态栏显示异常字符或错误

**解决方案**:
- 检查 DefaultTimeFormat 配置是否为有效的 .NET 日期格式
- 重置为默认值: `yyyy/MM/dd HH:mm:ss`

#### 4. 缓存清理失败

**可能原因**:
- 文件被其他进程占用
- 权限不足
- 磁盘空间不足

**解决方案**:
- 关闭可能占用文件的进程
- 以管理员身份运行 ColorVision
- 检查调试输出中的详细错误信息

## 技术架构

### 核心组件

```
SystemMonitor Plugin
├── SystemMonitorSetting        # 配置类
│   ├── UpdateSpeed            # 更新频率
│   ├── DefaultTimeFormat      # 时间格式
│   ├── IsShowTime            # 显示时间开关
│   └── IsShowRAM             # 显示内存开关
│
├── SystemMonitors             # 监控主类
│   ├── Performance Counters   # Windows 性能计数器
│   │   ├── CPU Total         # 系统 CPU
│   │   ├── CPU This          # 应用 CPU
│   │   ├── RAM Available     # 可用内存
│   │   └── RAM This          # 应用内存
│   │
│   ├── Timer                 # 定时更新
│   ├── DriveInfos           # 磁盘信息
│   └── ClearCache           # 缓存清理
│
└── SystemMonitorControl       # UI 控件
    ├── Configuration Panel   # 配置面板
    ├── Drive List           # 磁盘列表
    └── Progress Indicators  # 进度显示
```

### 线程安全

插件实现了完善的线程安全机制：
- 性能计数器访问使用锁保护
- Timer 回调检查 disposal 状态
- 资源清理使用双重检查锁定

### 资源管理

```csharp
// 正确的资源清理流程
public void Dispose()
{
    if (_isDisposed) return;
    _isDisposed = true;
    
    // 1. 停止定时器
    timer?.Dispose();
    
    // 2. 释放性能计数器
    lock (_perfCounterLock)
    {
        PCCPU?.Dispose();
        PCCPUThis?.Dispose();
        PCRAM?.Dispose();
        PCRAMThis?.Dispose();
    }
    
    GC.SuppressFinalize(this);
}
```

## 版本历史

### v1.0.1 (2025-10-10)

**重要修复**:
- ✅ 修复资源泄漏问题
- ✅ 修复线程安全问题
- ✅ 修复空引用异常
- ✅ 完整实现 CPU/RAM 监控

**改进**:
- 📈 添加更新频率限制
- 📝 完善错误日志
- 📚 添加 XML 文档
- 🔧 优化初始化流程

详见 [CHANGELOG.md](../../../Plugins/SystemMonitor/CHANGELOG.md)

### v1.0.0 (2025-01-01)

初始发布版本

## 最佳实践

### 1. 性能监控
```csharp
// 推荐配置
{
    "UpdateSpeed": 1000,        // 1秒更新
    "IsShowTime": true,         // 显示时间
    "IsShowRAM": true,          // 显示内存
    "DefaultTimeFormat": "HH:mm:ss"  // 简洁格式
}
```

### 2. 低功耗场景
```csharp
// 节能配置
{
    "UpdateSpeed": 5000,        // 5秒更新
    "IsShowTime": false,        // 关闭时间
    "IsShowRAM": false          // 关闭内存显示
}
```

### 3. 性能调试
```csharp
// 调试配置
{
    "UpdateSpeed": 500,         // 500ms更新
    "IsShowTime": true,
    "IsShowRAM": true,
    "DefaultTimeFormat": "HH:mm:ss.fff"  // 包含毫秒
}
```

## 相关资源

- [插件开发指南](../developing-a-plugin.md)
- [插件生命周期](../plugin-lifecycle.md)
- [ColorVision 文档](../../README.md)
- [CHANGELOG](../../../Plugins/SystemMonitor/CHANGELOG.md)
- [源代码](../../../Plugins/SystemMonitor/)

## 许可证

本插件继承 ColorVision 主项目许可证。

**版权**: Copyright (C) 2025 ColorVision Corporation  
**作者**: xincheng

---

*最后更新: 2025-10-10 | 文档版本: 1.0.1*
