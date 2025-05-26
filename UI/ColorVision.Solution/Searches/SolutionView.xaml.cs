using ColorVision.Common.Utilities;
using ColorVision.UI.Views;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Xceed.Wpf.AvalonDock.Layout;

namespace ColorVision.Solution.Searches
{
    public static class SolutionViewExtensions
    {
        public static LayoutDocument? FindDocumentById(object parent, string contentId)
        {
            if (parent is ILayoutContainer container)
            {
                foreach (var child in container.Children)
                {
                    if (child is LayoutDocument document && document.ContentId == contentId)
                    {
                        return document;
                    }
                    else
                    {
                        var found = FindDocumentById(child, contentId);
                        if (found != null)
                        {
                            return found;
                        }
                    }
                }
            }
            return null;
        }
        public static ILayoutContainer? FindParentContainer(object parent, LayoutDocument targetDocument)
        {
            if (parent is ILayoutContainer container)
            {
                foreach (var child in container.Children)
                {
                    if (child == targetDocument)
                    {
                        return container;
                    }
                    else
                    {
                        var found = FindParentContainer(child, targetDocument);
                        if (found != null)
                        {
                            return found;
                        }
                    }
                }
            }
            return null;
        }

        public static SolutionView SolutionView { get; set; }

        public static event  EventHandler<string>? ContentIdSelected;

        public static void OnContentIdSelected(string contentId)
        {
            ContentIdSelected?.Invoke(SolutionView, contentId);
        }
    }



    /// <summary>
    /// SolutionView.xaml 的交互逻辑
    /// </summary>
    public partial class SolutionView : UserControl, IView
    {
        public SolutionView()
        {
            InitializeComponent();
            SolutionViewExtensions.SolutionView = this;
        }
        public View View { get; set; }
        public void SelectContentId(string ContentId)
        {
            var existingDocument = SolutionViewExtensions.FindDocumentById(_layoutRoot, ContentId);

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
        }
        public void Open(string FullPath)
        {
            string GuidId = Tool.GetMD5(FullPath);
            var existingDocument = SolutionViewExtensions.FindDocumentById(_layoutRoot, GuidId.ToString());

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
                var IEditor = EditorManager.Instance.OpenFile(FullPath);
                if (IEditor != null)
                {
                    Control control = IEditor.Open(FullPath);
                    if (control != null)
                    {
                        LayoutDocument layoutDocument = new LayoutDocument() { ContentId = GuidId, Title = Path.GetFileName(FullPath) };
                        layoutDocument.Content = control;
                        LayoutDocumentPane.Children.Add(layoutDocument);
                        LayoutDocumentPane.SelectedContentIndex = LayoutDocumentPane.IndexOf(layoutDocument);
                        layoutDocument.IsActiveChanged += (s, e) =>
                        {
                            if (layoutDocument.IsActive)
                            {
                                SolutionViewExtensions.OnContentIdSelected(FullPath);
                            }
                        };
                        layoutDocument.Closing += (s, e) =>
                        {
                            if (control is IDisposable disposable)
                            {
                                disposable.Dispose();
                            }
                        };
                    }
                }
            }
        }

        private void UserControl_Initialized(object sender, System.EventArgs e)
        {
            View = new View();
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
