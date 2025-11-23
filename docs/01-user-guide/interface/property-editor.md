# 属性编辑器

## 概述

属性编辑器（PropertyEditor）是 ColorVision 中用于动态编辑配置对象的强大工具。它通过反射和元数据驱动，自动生成适合各种数据类型的编辑界面。

## 主要功能

1. **动态属性展示** - 根据对象属性自动生成编辑界面
2. **类型感知** - 为不同数据类型提供专门的编辑控件
3. **分组显示** - 按类别组织属性，便于管理
4. **多语言支持** - 通过资源管理器支持多语言显示
5. **数据验证** - 支持输入验证和格式检查
6. **集合编辑** - 支持多种集合和字典类型的可视化编辑

## 支持的数据类型

### 基础数据类型
- **数值类型**: 整数、浮点数、双精度等
- **文本**: 字符串
- **开关**: 布尔值
- **选项**: 枚举类型

### 集合类型（完整支持）
- **List<T>** - 标准列表
- **ObservableCollection<T>** - 可观察集合
- **Collection<T>** - 通用集合
- **IList<T>**, **ICollection<T>**, **IEnumerable<T>** - 集合接口

### 字典类型
- **Dictionary<TKey, TValue>** - 键值对字典
- **IDictionary<TKey, TValue>** - 字典接口

### 特殊类型
- **文件路径** - 带浏览按钮的文件选择器
- **文件夹路径** - 文件夹选择器
- **Cron 表达式** - 定时任务表达式编辑器
- **串口和波特率** - 串口通信参数选择

## 编辑模式

属性编辑器支持两种编辑模式：

### 1. 编辑模式
- 允许修改属性值
- 提供确定和取消按钮
- 可以重置属性到默认值

### 2. 查看模式
- 只读显示属性值
- 不提供修改功能
- 用于查看配置信息

## 集合和字典编辑

### JSON 文本编辑

所有集合和字典类型都可以通过 JSON 格式的文本框直接编辑：

**列表示例**：
```json
[1, 2, 3, 4, 5]
["apple", "banana", "cherry"]
```

**字典示例**：
```json
{"key1": "value1", "key2": "value2"}
{"one": 1, "two": 2}
```

输入 JSON 后失焦即可自动保存，系统会自动验证 JSON 格式的正确性。

### 可视化编辑器

点击属性旁边的"编辑"按钮，可以打开专用的可视化编辑窗口：

#### 列表/集合编辑器
![列表编辑器示例]

功能包括：
- **添加** - 添加新项到列表末尾
- **编辑** - 修改选中的项（双击或点击编辑按钮）
- **删除** - 删除选中的项（带确认提示）
- **上移** - 将选中项向上移动一位
- **下移** - 将选中项向下移动一位

操作步骤：
1. 在列表中选择要操作的项
2. 点击相应的操作按钮
3. 完成编辑后点击"确定"保存，或点击"取消"放弃修改

#### 字典编辑器
![字典编辑器示例]

功能包括：
- **添加** - 添加新的键值对
- **编辑** - 修改键或值（双击或点击编辑按钮）
- **删除** - 删除选中的键值对

特殊功能：
- **键唯一性验证** - 自动检查键是否重复，防止覆盖现有数据
- **类型支持** - 支持各种键值类型（字符串、数字、枚举等）

操作步骤：
1. 点击"添加"创建新键值对
2. 在弹出的编辑窗口中分别设置键和值
3. 系统会自动验证键的唯一性
4. 点击"确定"保存修改

## 属性控制特性

开发人员可以通过特性（Attributes）控制属性的显示和编辑方式：

### CategoryAttribute
将属性分组显示：
```csharp
[Category("基本设置")]
public string Name { get; set; }
```

### DisplayNameAttribute
设置属性的显示名称：
```csharp
[DisplayName("用户名")]
public string UserName { get; set; }
```

### DescriptionAttribute
添加属性说明（显示为工具提示）：
```csharp
[Description("用户的登录名称")]
public string UserName { get; set; }
```

### BrowsableAttribute
控制属性是否显示：
```csharp
[Browsable(false)]
public string InternalProperty { get; set; }
```

## 使用场景

属性编辑器在 ColorVision 中广泛应用于：

1. **设备配置** - 编辑设备连接参数和工作参数
2. **算法参数** - 调整图像处理算法的参数
3. **系统设置** - 修改应用程序的系统设置
4. **模板编辑** - 编辑和配置各种模板参数
5. **项目配置** - 管理项目相关的配置信息

## 操作技巧

1. **快速编辑** - 双击列表/字典项可直接打开编辑窗口
2. **JSON 输入** - 对于熟悉 JSON 格式的用户，可以直接输入 JSON 文本
3. **批量操作** - 通过 JSON 文本可以快速批量修改数据
4. **数据验证** - 输入错误的 JSON 格式会显示错误提示
5. **取消修改** - 在编辑窗口中点击"取消"可放弃所有修改

## 注意事项

1. **数据类型** - 确保输入的数据类型与属性类型匹配
2. **JSON 格式** - JSON 文本必须符合标准格式，注意引号和逗号
3. **键唯一性** - 字典中的键必须唯一，系统会自动验证
4. **嵌套编辑** - 支持嵌套对象的递归编辑
5. **保存确认** - 修改后记得点击"确定"按钮保存

## 相关文档

- [PropertyGrid 开发指南](../../02-developer-guide/ui-development/property-grid.md) - 开发者详细文档
- [PropertyEditor 集合支持](../../PropertyEditor-Collection-Support.md) - 集合类型使用指南
- [XAML 和 MVVM](../../02-developer-guide/ui-development/xaml-mvvm.md) - MVVM 模式介绍

## 技术实现

属性编辑器窗口的实现位于 `UI/ColorVision.UI/PropertyEditor/PropertyEditorWindow.xaml`。

主要特点：
- 基于 WPF 的 Window 类
- 支持 ViewModelBase 类型的配置对象
- 采用 MVVM 模式进行数据绑定
- 通过反射获取和操作属性
- 支持自定义编辑器扩展

源代码位置：
- 核心代码: `UI/ColorVision.UI/PropertyEditor/`
- 列表编辑器: `UI/ColorVision.UI/PropertyEditor/Editor/List/`
- 字典编辑器: `UI/ColorVision.UI/PropertyEditor/Editor/Dictionary/`
