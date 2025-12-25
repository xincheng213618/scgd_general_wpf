using AvalonDock.Layout;
using ColorVision.Common.Utilities;
using ColorVision.Solution.Editor;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace ColorVision.Solution.Workspace
{

    [FolderEditor("SolutionEditor")]
    public class SolutionEditor : EditorBase
    {
        public override void Open(string filePath)
        {
            string GuidId = Tool.GetMD5(filePath);
            var existingDocument = WorkspaceManager.FindDocumentById(WorkspaceManager.layoutRoot, GuidId.ToString());

            if (existingDocument != null)
            {
                if (existingDocument.Parent is LayoutDocumentPane layoutDocumentPane)
                {
                    layoutDocumentPane.SelectedContentIndex = layoutDocumentPane.IndexOf(existingDocument); ;
                }
                else if (existingDocument.Parent is LayoutFloatingWindow layoutFloatingWindow)
                {
                    var window = Window.GetWindow(layoutFloatingWindow);
                    if (window != null)
                    {
                        window.Activate();
                    }
                }
            }
            else
            {
                LayoutDocument layoutDocument = new LayoutDocument() { ContentId = GuidId, Title ="  主页 " };
                layoutDocument.Content = new SoloutionEditorControl();
                WorkspaceManager.LayoutDocumentPane.Children.Add(layoutDocument);
                WorkspaceManager.LayoutDocumentPane.SelectedContentIndex = WorkspaceManager.LayoutDocumentPane.IndexOf(layoutDocument);
                layoutDocument.IsActiveChanged += (s, e) =>
                {
                    if (layoutDocument.IsActive)
                    {
                        WorkspaceManager.OnContentIdSelected(filePath);
                    }
                };
            }
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
