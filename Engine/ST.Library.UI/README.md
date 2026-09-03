# ST.Library.UI

`ST.Library.UI` 是 ColorVision 的 WPF 节点编辑器库，目标框架为
`net8.0-windows` 和 `net10.0-windows`。

## 架构

- `STNodeEditor` 直接继承 WPF `Control`，可在 XAML 中使用，不需要
  `WindowsFormsHost`。
- 节点、端口和连线使用 `System.Drawing` 绘制协议，因此已有节点的
  `OnDrawNode` 自定义外观以及 `.stn`、`.cvflow` 数据保持兼容。
- WPF 控件负责输入、焦点、拖放、调度和位图呈现。
- `STNodeTreeView` 提供节点搜索、拖放和 WPF 预览；
  `STNodePropertyGrid` 使用 WPF 内联编辑器编辑节点属性。
- `STNodeEditorPannel` 使用 WPF `GridSplitter`
  组合布局。

## 在 XAML 中使用

```xml
<Window
    xmlns:st="clr-namespace:ST.Library.UI.NodeEditor;assembly=ST.Library.UI">
    <st:STNodeEditor x:Name="Editor"
                     HorizontalAlignment="Stretch"
                     VerticalAlignment="Stretch" />
</Window>
```

```csharp
var node = new MyNode
{
    Left = 100,
    Top = 100
};
node.Create();
Editor.Nodes.Add(node);
```

## 画布操作

- 滚轮：以指针位置为中心缩放。
- 中键拖动或空白区域左键拖动：平移画布。
- 左键拖动节点或端口：移动节点或连接端口。
- 右键：由宿主应用提供节点和画布菜单。

## 验证

```powershell
dotnet build .\Engine\ST.Library.UI\ST.Library.UI.csproj -f net10.0-windows
dotnet build .\Engine\FlowEngineLib\FlowEngineLib.csproj -f net10.0-windows
```

## 知识入口

[ST.Library.UI 契约](../../docs/04-api-reference/engine-components/ST.Library.UI.md)维护画布、类型注册、兼容边界与测试定位。此相对链接用于源码仓库；使用程序集时应核对匹配版本的源码，不能把另一版本的说明当作兼容保证。
