using ColorVision.Common.Utilities;
using ColorVision.Solution.Editor;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Xceed.Wpf.AvalonDock.Layout;

namespace ColorVision.Solution.Searches
{

    [FolderEditor("图片编辑器")]
    public class SolutionEditor : EditorBase
    {
        public override void Open(string filePath)
        {
            string GuidId = Tool.GetMD5(filePath);
            var existingDocument = SolutionViewExtensions.FindDocumentById(SolutionViewExtensions.layoutRoot, GuidId.ToString());

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
                SolutionViewExtensions.LayoutDocumentPane.Children.Add(layoutDocument);
                SolutionViewExtensions.LayoutDocumentPane.SelectedContentIndex = SolutionViewExtensions.LayoutDocumentPane.IndexOf(layoutDocument);
                layoutDocument.IsActiveChanged += (s, e) =>
                {
                    if (layoutDocument.IsActive)
                    {
                        SolutionViewExtensions.OnContentIdSelected(filePath);
                    }
                };
                bool isclear = true;
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
            MainFrame.Navigate(SolutionPageManager.Instance.GetPage("HomePage", MainFrame));

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
