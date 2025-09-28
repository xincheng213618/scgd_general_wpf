# 属性编辑器


1. 该文件是属性编辑器窗口的实现，类名为 PropertyEditorWindow，继承自 WPF 的 Window 类。
2. 主要功能是展示和编辑传入的 ViewModelBase 类型的配置对象（Config），支持编辑和非编辑模式。
3. 通过反射获取配置对象的属性，按类别分组展示，支持多种数据类型的属性编辑控件生成，包括布尔值、数字、字符串、枚举类型、以及嵌套的 ViewModelBase 子对象。
4. 支持属性的重置和关闭操作，关闭时会触发 Submited 事件。
5. 通过资源管理器支持多语言显示属性名称。
6. 代码中有丰富的 WPF 数据绑定示例，采用 MVVM 模式中的 ViewModelBase 作为数据模型。
7. 通过属性特性（CategoryAttribute、BrowsableAttribute、DisplayNameAttribute、PropertyEditorTypeAttribute）控制属性的显示和编辑方式。
8. 支持特殊的编辑类型，如文件选择、文件夹选择、JSON 编辑、Cron 表达式生成器、串口选择和波特率选择。
9. 代码结构清晰，方法职责单一，符合良好的设计原则。
10. 该文件直接对应 UI/ColorVision.UI/PropertyEditor/PropertyEditorWindow.xaml 中定义的界面。
