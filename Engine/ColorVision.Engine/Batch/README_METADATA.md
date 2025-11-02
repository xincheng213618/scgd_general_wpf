# IBatchProcess 元数据系统使用指南

## 概述

`IBatchProcess` 接口现在支持通过 `BatchProcessAttribute` 属性提供丰富的元数据信息，让用户能够更好地理解和选择批处理过程。

## 基本用法

### 1. 为批处理实现添加元数据

使用 `BatchProcessAttribute` 装饰您的 `IBatchProcess` 实现类：

```csharp
using ColorVision.Engine.Batch;

namespace YourNamespace
{
    [BatchProcess("显示名称", "详细描述")]
    public class YourBatchProcess : IBatchProcess
    {
        public bool Process(IBatchContext ctx)
        {
            // 您的处理逻辑
            return true;
        }
    }
}
```

### 2. 完整的元数据示例

```csharp
[BatchProcess("IVL完整处理", "处理IVL批次数据，包含Camera和Spectrum数据的导出")]
public class IVLProcess : IBatchProcess
{
    public bool Process(IBatchContext ctx)
    {
        // 实现代码
    }
}
```

## BatchProcessAttribute 属性

| 属性 | 类型 | 描述 | 默认值 |
|------|------|------|--------|
| `DisplayName` | string | 用户界面中显示的友好名称 | 空字符串 |
| `Description` | string | 处理过程的详细说明 | 空字符串 |
| `Category` | string | 用于分组相关处理的类别 | 空字符串 |
| `Order` | int | 显示顺序（数值越小越靠前） | 0 |

## 构造函数重载

### 默认构造函数
```csharp
[BatchProcess()]
public class MyProcess : IBatchProcess { }
// 将使用类名作为 DisplayName
```

### 仅指定显示名称
```csharp
[BatchProcess("我的处理")]
public class MyProcess : IBatchProcess { }
```

### 指定显示名称和描述
```csharp
[BatchProcess("我的处理", "这是一个示例处理")]
public class MyProcess : IBatchProcess { }
```

### 使用命名参数指定所有属性
```csharp
[BatchProcess(
    DisplayName = "高级处理", 
    Description = "执行复杂的数据转换",
    Category = "数据处理",
    Order = 10
)]
public class AdvancedProcess : IBatchProcess { }
```

## UI 中的显示效果

1. **ComboBox 下拉列表**: 显示 `DisplayName` 而不是类名
2. **Tooltip**: 显示包含 DisplayName、Description、Category 和完整类型名称的详细信息
3. **ListView**: 在"处理类"列显示 `DisplayName`，在"描述"列显示 `Description`
4. **排序**: 按 `Order` 值升序，然后按 `DisplayName` 字母顺序

## 现有实现示例

### IVL 处理系列
```csharp
[BatchProcess("IVL完整处理", "处理IVL批次数据，包含Camera和Spectrum数据的导出")]
public class IVLProcess : IBatchProcess { }

[BatchProcess("IVL相机处理", "仅处理IVL批次中的Camera数据并导出")]
public class IVLCameraProcess : IBatchProcess { }

[BatchProcess("IVL光谱处理", "仅处理IVL批次中的Spectrum数据并导出")]
public class IVLSprectrumProcess : IBatchProcess { }
```

### POI 处理
```csharp
[BatchProcess("POI处理", "处理POI批次数据并导出CIE xyuv数据")]
public class IPoiProcess : IBatchProcess { }
```

## 向后兼容性

如果不添加 `BatchProcessAttribute`，处理类仍然可以正常工作：
- `DisplayName` 将自动回退到类名（如 "MyBatchProcess"）
- `Description` 将为空字符串
- `Category` 将为空字符串
- `Order` 将为 0

## 最佳实践

1. **始终提供 DisplayName**: 使用简短、清晰的中文名称
2. **添加有意义的 Description**: 帮助用户理解此处理的具体功能
3. **使用 Category 分组**: 如果有多个相关的处理，使用相同的 Category
4. **合理设置 Order**: 将最常用的处理排在前面（较小的 Order 值）
5. **保持一致性**: 在同一模块中使用相似的命名和描述风格

## API 参考

### BatchProcessMetadata 类

提供从 `IBatchProcess` 实例或类型中提取元数据的静态方法：

```csharp
// 从实例获取元数据
var metadata = BatchProcessMetadata.FromProcess(processInstance);

// 从类型获取元数据
var metadata = BatchProcessMetadata.FromType(typeof(MyProcess));

// 访问元数据属性
string displayName = metadata.DisplayName;
string description = metadata.Description;
string category = metadata.Category;
int order = metadata.Order;
string typeName = metadata.TypeName;
string shortTypeName = metadata.ShortTypeName;

// 获取格式化的显示文本
string displayText = metadata.GetDisplayText(); // "DisplayName - Description"
string tooltip = metadata.GetTooltipText(); // 包含所有信息的多行文本
```

### BatchProcessMeta 新增属性

`BatchProcessMeta` 类现在包含以下与元数据相关的属性：

```csharp
public BatchProcessMetadata Metadata { get; }
public string ProcessDisplayName { get; }  // 来自元数据
public string ProcessDescription { get; }  // 来自元数据
public string ProcessCategory { get; }     // 来自元数据
```

## 故障排除

### 问题：元数据不显示
- 确保已添加 `[BatchProcess]` 属性
- 检查属性参数是否正确
- 验证类是否实现了 `IBatchProcess` 接口

### 问题：显示的是类名而不是 DisplayName
- 确认 `DisplayName` 参数不为 null 或空字符串
- 检查属性是否正确应用到类上

### 问题：排序不正确
- 检查 `Order` 属性值
- 值越小越靠前
- 相同 `Order` 值时按 `DisplayName` 字母顺序排序

## 未来扩展建议

1. 支持多语言的 DisplayName 和 Description
2. 根据 Category 在 UI 中分组显示
3. 添加图标支持（IconPath 属性）
4. 添加版本信息（Version 属性）
5. 添加作者信息（Author 属性）
