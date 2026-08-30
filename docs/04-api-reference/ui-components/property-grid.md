---
knowledge_id: "ui.property-grid"
knowledge_type: "topic"
status: "current"
summary: "属性面板的字段生成、编辑器选择和 Flow 适配；区分直接修改、工作副本、关闭、重置与宿主持久化。"
aliases: ["属性面板", "修改参数", "编辑器显示成文本", "如何新增属性编辑器", "属性编辑器为什么不显示", "IPropertyEditor", "GenProperties", "PropertyEditorType", "PropertyVisibility", "PropertyEditSession", "PropertyEditorWindow", "FlowPropertyEditorRegistry", "FlowNodePropertyEditorRegistration", "FlowNodePropertyEditorAttribute", "取消修改", "关闭回滚"]
code_paths: ["UI/ColorVision.UI/PropertyEditor/PropertyEditors.cs", "UI/ColorVision.UI/PropertyEditor/PropertyEditorHelper.cs", "UI/ColorVision.UI/PropertyEditor/PropertyEditorRegistry.cs", "UI/ColorVision.UI/PropertyEditor/PropertyEditorTypeAttribute.cs", "UI/ColorVision.UI/PropertyEditor/PropertyEditSession.cs", "UI/ColorVision.UI/PropertyEditor/PropertyEditorWindow.xaml.cs", "Engine/FlowEngineLib/PropertyEditor/FlowNodePropertyEditors.cs", "Engine/ColorVision.Engine/PropertyEditor/FlowNodePropertyEditorRegistration.cs"]
test_paths: ["Test/ColorVision.UI.Tests/PropertyEditorContractTests.cs", "Test/ColorVision.UI.Tests/PropertyEditSessionTests.cs", "Test/ColorVision.UI.Tests/ListEditorTests.cs", "Test/ColorVision.UI.Tests/AlgorithmNodeTemplateMappingTests.cs", "Test/ColorVision.UI.Tests/CameraNodeTemplateMappingTests.cs"]
related: ["ui.index", "ui.configuration", "ui.discovery", "flow.templates", "algorithms.template-management"]
---

# PropertyGrid 属性编辑契约

ColorVision 的通用属性面板由属性元数据、`PropertyEditorHelper` 和 `IPropertyEditor` 组成。回答“新增编辑器”“编辑器不命中”“保存为何没生效”时，从本页定位契约，再核对被编辑对象和实际宿主；不要为每个配置对象新建独立编辑框架。

## 任务路由

| 问题 | 核对入口 |
| --- | --- |
| 给一个属性指定编辑器 | `PropertyEditorTypeAttribute` 和 `IPropertyEditor.GenProperties` |
| 同一类型统一使用编辑器 | `PropertyEditorHelper.RegisterEditor<TEditor>(Type)` |
| 一组类型按条件匹配 | `RegisterEditor<TEditor>(Func<Type, bool>)`，注意注册顺序 |
| 面板没有显示属性 | 属性可读写、非索引属性、`Browsable`、元数据 Provider 和嵌套对象规则 |
| 编辑后原对象没变化 | `PropertyEditSession.Mode`、工作副本、`Commit()` 与宿主保存动作 |
| 关闭或取消后参数仍然变化 | `PropertyEditorWindow` 默认 `Immediate`，关闭不执行回滚 |
| Flow 模板选择器退化成文本 | `FlowNodePropertyEditorAttribute`、`FlowPropertyEditorRegistry` 与 Engine 注册入口 |

## 接口与注册

`UI/ColorVision.UI/PropertyEditor/PropertyEditors.cs` 在 `System.ComponentModel` 命名空间定义唯一入口：

```csharp
public interface IPropertyEditor
{
    DockPanel GenProperties(PropertyInfo property, object obj);
}
```

属性级选择使用 `[PropertyEditorType(typeof(MyEditor))]`；`MyEditor` 在此只是占位类型名，实际实现应从现有编辑器复制必要模式。可参考 `Editor/TextSelectFilePropertiesEditor.cs`：每次调用创建新面板，使用 `CreateLabel`、`CreateTwoWayBinding(obj, property)` 和共享小控件样式。

类型级注册通过 `PropertyEditorHelper.RegisterEditor<TEditor>(typeof(TargetType))` 或匹配谓词完成。注册不是给每个对象存一份编辑器实例；通用注册表按编辑器类型缓存实例，编辑器必须可构造并实现 `IPropertyEditor`。

## 选择顺序与失败行为

`PropertyEditorHelper` 的生成链按下列顺序尝试：

| 顺序 | 入口 | 未选出可用面板时 |
| --- | --- | --- |
| 1 | 当前 `IPropertyEditorMetadataProvider.GetEditorType(property)` | 返回空或选中编辑器生成失败时继续属性标注；回调自身抛异常则终止该行 |
| 2 | 属性上的 `PropertyEditorTypeAttribute.EditorType` | 继续尝试属性类型注册 |
| 3 | `PropertyEditorRegistry.Find(property.PropertyType)` | 无类型注册时尝试嵌套对象 |
| 4 | 可生成的嵌套对象属性面板 | 无有效控件则不生成该行 |

类型注册内部先找精确类型，再按注册顺序寻找第一个命中的谓词。谓词抛异常会记录警告并跳过该谓词。`TryGenerateEditor` 在构造或 `GenProperties` 失败时记录错误并返回空，让外层按上述路径继续；**不保证任意未知类型或所有编辑器失败后都会出现文本框**。

异常边界不能混为一谈：元数据 Provider 的 `GetEditorType` 回调在 `TryGenerateEditor` 之外。该回调自身抛异常时，外层 `TryCreatePropertyDockPanel` 记录错误并返回 `false`，不会继续尝试属性标注；可见性绑定等外层步骤抛异常也会终止该行。新增 Provider 时需单独验证这些异常路径。

`PropertyEditorContractTests.FailingAttributedEditor_FallsBackToStandardTypeEditor` 锁定属性指定编辑器失败后回到标准类型编辑器的情况；不要把该测试扩大解释成全部失败路径都会成功降级。

## 实例、绑定与可见性

- `PropertyEditorRegistry.GetOrCreate` 复用编辑器实例。不要把某个窗口、属性对象或生成的 `DockPanel` 保存到编辑器实例字段；本次调用的状态应放在新建控件、局部变量或适当释放的订阅中。
- `CreateTwoWayBinding(obj, property)` 默认逐属性变化回写，启用异常与数据错误验证；属性标注可以指定 `UpdateSourceTrigger`。只读属性或 `[ReadOnly(true)]` 使用单向绑定，生成控件也按只读元数据禁用。
- 标题、类别、描述优先用 `DisplayName`、`Category`、`Description` 和现有资源解析；显示条件用 `PropertyVisibility`，永久隐藏用 `Browsable(false)`。
- 布尔、枚举、数值、日期、集合、字典、Brush/Color 等内置映射以 `PropertyEditorBuiltIns.cs` 为准。先检查能否复用，不把“新业务字段”自动等同于“需要新编辑器”。

`PropertyEditorWindow` 将当前编辑对象的字段按类别展示，并提供搜索与排序；复杂对象可展开为嵌套面板。字段缺失时先确认宿主传入了哪个对象、当前搜索条件和元数据，再查编辑器匹配，不根据界面控件外观猜属性类型。嵌入式属性面板不一定具有这个窗口的搜索、按钮或编辑会话，需核对实际宿主。

## 编辑与持久化不是同一动作

`PropertyEditSession` 有两种模式：`Immediate` 直接使用原对象；`Transactional` 使用配置数据的工作副本，调用 `Commit()` 才复制回原对象。它不是数据库事务或外部设备操作的回滚机制；WPF `DispatcherObject` 等运行时引用仍可共享，不能理解成整个运行环境完全隔离。

`PropertyEditorWindow(object config)` **默认是 `Immediate`**。按钮语义由窗口和会话代码共同决定：

| 动作 | 实际行为与边界 |
| --- | --- |
| 编辑字段 | 通常按绑定触发条件写入 `EditableObject`；`Immediate` 下它就是原对象 |
| 确定 | 先 `Commit()`，再触发 `Submitted`，最后关闭；是否落盘或保存模板取决于宿主订阅 |
| 取消／关闭 | `Cancel_Click` 只关闭窗口，未调用恢复；`Immediate` 下按钮显示“关闭”，已写入原对象的值不会因此撤销 |
| 重置 | `Reset()` 将打开时的初始快照复制到当前编辑对象；`Immediate` 下同样会修改原对象 |
| 恢复默认 | `ResetToDefaults()` 用可构造的默认对象覆盖当前编辑对象；不是从配置备份文件恢复 |

编辑器只负责把控件值写入传入的 `obj`。判断“配置保存失败”需分别核对字段绑定、会话提交和宿主持久化，不能把界面值变化或确定按钮当成文件／数据库已保存的证据。[软件配置](./configuration.md)与[设备资源配置](../../01-user-guide/devices/configuration.md)有不同的保存/发布边界。只读诊断不要用改值、重置或恢复默认来探测设备配置；实际验证应使用隔离对象，并经授权检查保存后重开与宿主副作用。

## Flow 适配边界

`Engine/FlowEngineLib/PropertyEditor/FlowNodePropertyEditors.cs` 定义 `FlowNodePropertyEditorAttribute`、`FlowPropertyEditorRegistry` 和代理编辑器。`Engine/ColorVision.Engine/PropertyEditor/FlowNodePropertyEditorRegistration.cs` 将代理类型注册为具体设备/模板选择器，让 FlowEngineLib 不直接依赖 Engine 业务 UI。

普通设备和模板字段应沿这条属性映射链扩展。只有多模板族或随节点算法类型变化的补充面板才使用 `FlowProcessing/Editor/NodeConfiguration/`。未注册的 `FlowPropertyEditorProxy` 有自己的文本编辑器回退；它与上面的通用 PropertyGrid 失败规则不是同一个契约。代理内部不一定复用实例，不能把通用缓存规则直接套给所有宿主。

## 验证入口与缺口

| 测试文件，均在 `Test/ColorVision.UI.Tests/` | 覆盖契约 |
| --- | --- |
| `PropertyEditorContractTests.cs` | 更新触发与验证、失败降级、只读、精确类型优先、实例复用、标准类型和兼容入口 |
| `PropertyEditSessionTests.cs` | 配置数据工作副本隔离、嵌套提交、重置、直接写入模式，以及 WPF 运行时引用保留 |
| `ListEditorTests.cs` | 集合转换器的既有回归样例，不代表所有集合形态 |
| `AlgorithmNodeTemplateMappingTests.cs` | ARVR POI 使用原生属性编辑行 |
| `CameraNodeTemplateMappingTests.cs` | 相机和校准的模板类型映射 |

可在 Windows/x64 上运行最接近的测试，例如：

```powershell
dotnet test Test/ColorVision.UI.Tests/ColorVision.UI.Tests.csproj -p:Platform=x64 --filter "FullyQualifiedName~PropertyEditorContractTests|FullyQualifiedName~PropertyEditSessionTests|FullyQualifiedName~ListEditorTests"
```

这些是验证入口，不是运行通过记录；会话单元测试不等于窗口关闭、`Submitted` 订阅或持久化链路的端到端验证。自定义编辑器仍需在目标宿主检查样式、键盘操作、错误提示、订阅释放和保存重开；新增公开签名还要验证实际插件 DLL 的二进制兼容性。
