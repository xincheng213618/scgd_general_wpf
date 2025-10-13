# 状态栏信息接口文档

## 概述

本文档集提供了关于状态栏信息接口（`IStatusBarInfoProvider`）的完整文档，包括设计理念、使用指南和架构说明。

## 文档目录

### 1. [状态栏信息接口指南](./状态栏信息接口指南.md)
详细的使用指南，包括：
- 接口定义和数据模型
- 实现步骤和完整示例
- 最佳实践和常见问题解决
- 适用场景和扩展方法

**适合读者**：开发人员、希望为自定义控件添加状态栏信息的实现者

### 2. [架构设计](./架构设计.md)
系统架构和设计说明，包括：
- 系统架构图和数据流程图
- 类图关系和组件职责
- 设计模式应用
- 性能考虑和测试策略

**适合读者**：架构师、技术负责人、希望深入了解设计思想的开发人员

## 快速开始

### 第一步：理解接口

状态栏信息接口允许控件在被选中时显示自己的状态信息。接口定义如下：

```csharp
public interface IStatusBarInfoProvider
{
    ObservableCollection<StatusBarInfoItem> GetStatusBarInfo();
}
```

### 第二步：实现接口

在你的控件类中实现接口：

```csharp
public partial class MyControl : UserControl, IDisPlayControl, IStatusBarInfoProvider
{
    private ObservableCollection<StatusBarInfoItem> _statusBarInfo;
    
    public ObservableCollection<StatusBarInfoItem> GetStatusBarInfo()
    {
        if (_statusBarInfo == null)
        {
            _statusBarInfo = new ObservableCollection<StatusBarInfoItem>
            {
                new StatusBarInfoItem
                {
                    Key = "Name",
                    Label = "名称:",
                    Value = "我的控件",
                    Order = 0,
                    IsVisible = true
                }
            };
        }
        return _statusBarInfo;
    }
}
```

### 第三步：测试

运行应用程序，选择你的控件，应该能在状态栏看到显示的信息。

## 核心概念

### 状态栏信息项（StatusBarInfoItem）

每个信息项包含以下属性：
- **Key**：唯一标识符，用于查找和更新
- **Label**：显示标签（如 "状态:"）
- **Value**：实际值（如 "已连接"）
- **DisplayText**：完整显示文本（Label + Value）
- **IsVisible**：是否显示
- **Order**：显示顺序

### 工作流程

1. 用户点击控件
2. 控件的 `Selected` 事件被触发
3. `MainWindow` 检查控件是否实现 `IStatusBarInfoProvider`
4. 如果实现了，调用 `GetStatusBarInfo()` 获取信息
5. 将信息显示在状态栏中
6. 当信息项的 `Value` 改变时，UI 自动更新

## 使用场景示例

### 图像查看器控件
显示：图像名称、分辨率、比特率、颜色空间、通道数

### 文本编辑器控件
显示：当前行、当前列、总行数、字符数、编码格式

### 设备控制器
显示：设备名称、连接状态、工作参数、测量模式

### 数据表格控件
显示：选中行数、总行数、当前页、总页数

## 相关资源

### 代码位置
- 接口定义：`UI/ColorVision.Common/Interfaces/StatusBar/IStatusBarInfoProvider.cs`
- 数据模型：`UI/ColorVision.Common/Interfaces/StatusBar/StatusBarInfoItem.cs`
- 主窗口集成：`ColorVision/MainWindow.xaml.cs`
- 示例实现：`Engine/ColorVision.Engine/Services/Devices/Spectrum/DisplaySpectrum.xaml.cs`

### 相关文档
- [主窗口导览](../main-window/主窗口导览.md) - 了解主窗口的整体结构
- [IDisPlayControl 接口](../../UI/ColorVision.Common/Interfaces/IDisPlayControl.cs) - 控件基础接口

## 设计原则

该接口遵循以下设计原则：

1. **开闭原则**：对扩展开放，对修改关闭
2. **接口隔离原则**：接口小而专注
3. **依赖倒置原则**：依赖抽象而非具体实现
4. **单一职责原则**：每个类只负责一个功能

## 贡献指南

如果你想改进这个接口或添加新功能：

1. 阅读架构设计文档，了解当前设计
2. 提出改进建议，说明动机和好处
3. 确保改进不破坏现有实现
4. 更新相关文档
5. 添加单元测试

## 常见问题

### Q: 为什么我的状态栏没有显示信息？
A: 检查以下几点：
1. 控件是否实现了 `IStatusBarInfoProvider` 接口
2. 控件是否被正确选中（`IsSelected = true`）
3. `GetStatusBarInfo()` 是否返回了有效的信息集合
4. 信息项的 `IsVisible` 属性是否为 `true`

### Q: 如何动态更新状态信息？
A: 直接修改 `StatusBarInfoItem` 的 `Value` 属性即可，它会自动触发 UI 更新：
```csharp
var item = _statusBarInfo.FirstOrDefault(i => i.Key == "Status");
if (item != null)
{
    item.Value = "新状态";  // UI 自动更新
}
```

### Q: 可以显示图标吗？
A: 当前版本只支持文本显示。如果需要图标，可以扩展 `StatusBarInfoItem` 类添加 `Icon` 属性。参考[架构设计](./架构设计.md)文档中的扩展点章节。

### Q: 如何控制信息项的显示顺序？
A: 使用 `Order` 属性，数值越小越靠前：
```csharp
new StatusBarInfoItem { Key = "First", Order = 0 },
new StatusBarInfoItem { Key = "Second", Order = 1 },
new StatusBarInfoItem { Key = "Third", Order = 2 }
```

## 版本历史

### v1.0 (2025-10-13)
- 初始版本发布
- 实现基础接口和数据模型
- 集成到主窗口
- 提供 DisplaySpectrum 示例实现
- 完整文档

## 反馈和支持

如有问题或建议，请：
1. 查看文档中的常见问题部分
2. 在项目仓库中提交 Issue
3. 联系开发团队

---

**最后更新时间**：2025-10-13  
**维护者**：ColorVision 开发团队
