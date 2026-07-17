using AvalonDock.Layout;
using ColorVision.Common.Utilities;
using ColorVision.Solution.Editor;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace ColorVision.Solution.Workspace
{

    public class SolutionEditor : EditorBase
    {
        public override void Open(string filePath)
        {
            EditorDocumentService.Open(
                filePath,
                GetType(),
                Properties.Resources.Sol_Workspace_Home,
                () => new SoloutionEditorControl());
        }
    }


    /// <summary>
    /// SoloutionEditorControl.xaml 的交互逻辑
    /// </summary>
    public partial class SoloutionEditorControl : UserControl
    {
        public SoloutionEditorControl()
        {
            InitializeComponent();
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            if (Application.Current.FindResource("MenuItem4FrameStyle") is Style style)
            {
                ContextMenu content1 = new() { ItemContainerStyle = style };
                content1.SetBinding(ItemsControl.ItemsSourceProperty, new Binding { Path = new PropertyPath("BackStack"), Source = MainFrame });
                BackStack.ContextMenu = content1;

                ContextMenu content2 = new() { ItemContainerStyle = style };
                content2.SetBinding(ItemsControl.ItemsSourceProperty, new Binding { Path = new PropertyPath("ForwardStack"), Source = MainFrame });
                BrowseForward.ContextMenu = content2;
            }
        }
    }
}
