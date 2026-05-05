# ImageEditor 下一步迭代图

## 当前基线

已经完成的收口：

- 统一设置窗口：`ImageViewSettingsWindow`
- 工作台页：工具栏可见性 + `IEditorTool` 清单 + `IImageOpen` 清单
- 模块化 provider：`IImageViewSettingProvider`
- 打开器专用配置样例：`TifOpenConfig`
- 伪彩色当前值 / 默认值拆分

## 迭代目标

下一轮不再回到旧 `Configuration` 实验层，而是在现有真实链路上继续做三件事：

1. 给 `IEditorTool` / `IImageOpen` 增加正式描述信息。
2. 把“仅展示已加载状态”推进到“可配置加载策略”。
3. 让更多模块按 provider 方式自带设置页，而不是继续把逻辑堆回 `ImageView`。

## 迭代图

```mermaid
flowchart TD
    A[ImageView] --> B[统一设置入口<br/>ImageViewSettingsWindow]
    A --> C[运行态上下文<br/>EditorContext]
    A --> D[工具/打开器发现<br/>EditorToolFactory]

    B --> B1[显示]
    B --> B2[默认值]
    B --> B3[工作台]
    B --> B4[加载器]
    B --> B5[工具专属页<br/>如伪彩色]

    B3 --> C1[工具栏可见性]
    B3 --> C2[IEditorTool 已加载清单]
    B3 --> C3[IImageOpen 已注册清单]

    B4 --> D1[TifOpenConfig]
    B4 --> D2[后续更多 LoaderConfig]

    D --> E[下一步：描述层 Descriptor]
    E --> E1[IEditorToolDescriptor]
    E --> E2[IImageOpenDescriptor]

    E1 --> F[下一步：加载策略]
    E2 --> F
    F --> F1[启用/禁用工具]
    F --> F2[启用/禁用打开器]
    F --> F3[按扩展名/模块过滤]

    F --> G[后续：模块自带设置页]
    G --> G1[工具自己注册 provider]
    G --> G2[打开器自己注册 provider]
    G --> G3[当前值与默认值继续拆分]

    H[明确不再继续的方向] --> H1[恢复旧 Configuration 并行框架]
    H --> H2[再造第二套 ServiceLocator]
    H --> H3[在未接入控制路径前预建抽象]
```

## 建议的实现顺序

### 第 1 步：描述层

先给 `IEditorTool` 和 `IImageOpen` 补充统一元数据来源，例如：

- 显示名称
- 所属分组
- 描述
- 来源模块/程序集
- 默认是否启用

目标不是先做开关，而是先把“展示名”和“可管理对象”变成稳定实体。

### 第 2 步：加载策略

在 `EditorToolFactory` 的发现阶段前置策略判断：

- 哪些 `IEditorTool` 不实例化
- 哪些 `IImageOpen` 不注册
- 哪些扩展名映射要被覆盖或禁用

这一步做完，设置页里“已加载列表”才会变成真正有控制力的“已启用列表”。

### 第 3 步：模块继续下沉

把更多格式特例和复杂工具继续从 `ImageView` 中拆出去：

- 打开器特定行为进入各自 `LoaderConfig`
- 工具特定行为进入各自 provider
- 当前值 / 默认值继续严格分层

## 验收标准

如果下一轮完成得对，应该看到这些结果：

- `ImageView.xaml.cs` 不再继续膨胀。
- 新增工具/打开器时，设置页无需再手写集中式 if/else。
- “是否加载”在发现阶段就能控制，而不是实例化后再隐藏。
- 文档能直接指出真实控制路径，而不是再出现未接入的平行抽象。