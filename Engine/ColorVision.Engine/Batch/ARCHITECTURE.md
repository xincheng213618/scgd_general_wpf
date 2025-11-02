# IBatchProcess 元数据系统架构图

```
┌─────────────────────────────────────────────────────────────────────┐
│                    IBatchProcess 元数据系统                           │
└─────────────────────────────────────────────────────────────────────┘

┌──────────────────────────────────────────────────────────────────────┐
│ 1. 实现层 (Implementation Layer)                                      │
├──────────────────────────────────────────────────────────────────────┤
│                                                                       │
│  ┌───────────────────────────────────────────────────────┐           │
│  │ [BatchProcess("IVL完整处理", "处理IVL批次数据...")]    │           │
│  │ public class IVLProcess : IBatchProcess               │           │
│  │ {                                                     │           │
│  │     public bool Process(IBatchContext ctx) { ... }   │           │
│  │ }                                                     │           │
│  └───────────────────────────────────────────────────────┘           │
│         ↑                                                             │
│         │ Uses BatchProcessAttribute                                 │
│         │                                                             │
└─────────┼─────────────────────────────────────────────────────────────┘
          │
┌─────────┼─────────────────────────────────────────────────────────────┐
│ 2. 元数据层 (Metadata Layer)                        │                │
├──────────────────────────────────────────────────────────────────────┤
│                                                                       │
│  ┌────────────────────────────┐   ┌──────────────────────────────┐  │
│  │ BatchProcessAttribute      │   │ BatchProcessMetadata         │  │
│  ├────────────────────────────┤   ├──────────────────────────────┤  │
│  │ + DisplayName: string      │   │ + DisplayName: string        │  │
│  │ + Description: string      │   │ + Description: string        │  │
│  │ + Category: string         │   │ + Category: string           │  │
│  │ + Order: int               │   │ + Order: int                 │  │
│  └────────────────────────────┘   │ + TypeName: string           │  │
│         ↓ Defines                  │ + ShortTypeName: string      │  │
│                                    │                              │  │
│                                    │ + FromProcess(process)       │  │
│                                    │ + FromType(type)             │  │
│                                    │ + GetDisplayText()           │  │
│                                    │ + GetTooltipText()           │  │
│                                    └──────────────────────────────┘  │
│                                             ↑                         │
│                                             │ Extracts via Reflection │
└─────────────────────────────────────────────┼─────────────────────────┘
                                              │
┌─────────────────────────────────────────────┼─────────────────────────┐
│ 3. 模型层 (Model Layer)                      │                        │
├──────────────────────────────────────────────────────────────────────┤
│                                                                       │
│  ┌──────────────────────────────────────────────────────┐            │
│  │ BatchProcessMeta : ViewModelBase                     │            │
│  ├──────────────────────────────────────────────────────┤            │
│  │ + Name: string                                       │            │
│  │ + TemplateName: string                               │            │
│  │ + BatchProcess: IBatchProcess                        │            │
│  │                                                      │            │
│  │ // 新增元数据属性                                     │            │
│  │ + Metadata: BatchProcessMetadata                     │            │
│  │ + ProcessDisplayName: string  ← Metadata.DisplayName │            │
│  │ + ProcessDescription: string  ← Metadata.Description │            │
│  │ + ProcessCategory: string     ← Metadata.Category    │            │
│  │ + ProcessTypeName: string                            │            │
│  └──────────────────────────────────────────────────────┘            │
│                                    ↓                                  │
│                                    │ Data Binding                     │
└────────────────────────────────────┼──────────────────────────────────┘
                                     │
┌────────────────────────────────────┼──────────────────────────────────┐
│ 4. 视图模型层 (ViewModel Layer)     │                                 │
├──────────────────────────────────────────────────────────────────────┤
│                                                                       │
│  ┌──────────────────────────────────────────────────────┐            │
│  │ BatchManager                                         │            │
│  ├──────────────────────────────────────────────────────┤            │
│  │ + Processes: ObservableCollection<IBatchProcess>     │            │
│  │ + ProcessMetas: ObservableCollection<BatchProcessMeta>│           │
│  │                                                      │            │
│  │ - LoadProcesses()                                    │            │
│  │   ├─ Scan assemblies for IBatchProcess               │            │
│  │   ├─ Extract metadata                                │            │
│  │   └─ Sort by Order, then DisplayName                 │            │
│  └──────────────────────────────────────────────────────┘            │
│                                    ↓                                  │
│                                    │ Data Context                     │
└────────────────────────────────────┼──────────────────────────────────┘
                                     │
┌────────────────────────────────────┼──────────────────────────────────┐
│ 5. 视图层 (View Layer)              │                                 │
├──────────────────────────────────────────────────────────────────────┤
│                                                                       │
│  ┌──────────────────────────────────────────────────────┐            │
│  │ BatchProcessManagerWindow.xaml                       │            │
│  ├──────────────────────────────────────────────────────┤            │
│  │                                                      │            │
│  │ ComboBox: 处理类选择                                  │            │
│  │ ├─ ItemsSource="{Binding Processes}"                 │            │
│  │ ├─ Converter: TypeNameConverter                      │            │
│  │ │   → 显示 DisplayName 而非类名                       │            │
│  │ └─ ToolTip: ProcessTooltipConverter                  │            │
│  │     → 显示完整元数据信息                              │            │
│  │                                                      │            │
│  │ ListView: 已配置的处理列表                            │            │
│  │ ├─ 列1: 名称 (Name)                                  │            │
│  │ ├─ 列2: 流程模板 (TemplateName)                       │            │
│  │ ├─ 列3: 处理类 (ProcessDisplayName) ← 新增           │            │
│  │ └─ 列4: 描述 (ProcessDescription) ← 新增              │            │
│  └──────────────────────────────────────────────────────┘            │
│                                                                       │
│  用户看到的效果:                                                       │
│  ┌────────────────────────────────────────────────┐                  │
│  │ 处理类: [IVL完整处理 ▼]                        │                  │
│  │         └─ ToolTip:                            │                  │
│  │            IVL完整处理                         │                  │
│  │            处理IVL批次数据，包含Camera和...      │                  │
│  │            类型: ColorVision.Engine.Batch.IVL...│                  │
│  └────────────────────────────────────────────────┘                  │
│                                                                       │
└───────────────────────────────────────────────────────────────────────┘


┌───────────────────────────────────────────────────────────────────────┐
│ 数据流向 (Data Flow)                                                   │
└───────────────────────────────────────────────────────────────────────┘

1. 开发者定义实现:
   [BatchProcess("显示名称", "描述")] ← 声明元数据
   public class MyProcess : IBatchProcess { }

2. BatchManager 启动时:
   LoadProcesses() → 扫描程序集 → 发现实现类 → 创建实例
                                    ↓
                           提取 BatchProcessMetadata
                                    ↓
                           按 Order 和 DisplayName 排序
                                    ↓
                           添加到 Processes 集合

3. 用户打开管理窗口:
   BatchProcessManagerWindow ← DataContext = BatchManager
                                    ↓
                           ComboBox 绑定到 Processes
                                    ↓
                           TypeNameConverter 转换显示
                                    ↓
                           用户看到友好名称

4. 用户选择处理类:
   SelectedProcess → 创建 BatchProcessMeta
                           ↓
                  BatchProcessMeta.Metadata 自动填充
                           ↓
                  UI 显示 DisplayName 和 Description


┌───────────────────────────────────────────────────────────────────────┐
│ 扩展点 (Extension Points)                                              │
└───────────────────────────────────────────────────────────────────────┘

1. 添加新属性到 BatchProcessAttribute:
   - Icon (图标路径)
   - Version (版本号)
   - Author (作者)
   - Tags (标签)

2. UI 增强:
   - Category 分组显示
   - 搜索和过滤
   - 图标显示
   - 拖拽排序

3. 功能扩展:
   - 导出/导入配置
   - 批处理模板
   - 执行历史
   - 性能统计
