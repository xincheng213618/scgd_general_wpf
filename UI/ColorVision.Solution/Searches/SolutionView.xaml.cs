using ColorVision.UI.Views;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
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



        public static LayoutDocument? FindDocumentActive(object parent)
        {
            if (parent is ILayoutContainer container)
            {
                foreach (var child in container.Children)
                {
                    if (child is LayoutDocument document && document.IsActive)
                    {

                        return document;
                    }
                    else
                    {
                        var found = FindDocumentActive(child);
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

        public static LayoutRoot layoutRoot { get; set; }

        public static LayoutDocumentPane LayoutDocumentPane { get; set; }

        public static void SelectContentId(string ContentId)
        {
            var existingDocument = SolutionViewExtensions.FindDocumentById(layoutRoot, ContentId);

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

        public static event  EventHandler<string>? ContentIdSelected;

        public static void OnContentIdSelected(string contentId)
        {
            ContentIdSelected?.Invoke(SolutionView, contentId);
        }

        public static List <Action> DealyLoad { get; set; } = new List<Action>();
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
            SolutionViewExtensions.layoutRoot = _layoutRoot;
            SolutionViewExtensions.LayoutDocumentPane = LayoutDocumentPane;
            CommandBindings.Add(new CommandBinding(ApplicationCommands.Close, (sender, e) => Colsed()));
            InputBindings.Add(new KeyBinding(ApplicationCommands.Close, new KeyGesture(Key.W, ModifierKeys.Control)));

            foreach (var action in SolutionViewExtensions.DealyLoad)
            {
                action();
            }
            SolutionViewExtensions.DealyLoad.Clear();
        }

        public void Colsed()
        {
            var pannel = SolutionViewExtensions.FindDocumentActive(LayoutDocumentPane);
            pannel.Close();
        }


        public View View { get; set; }
        private void UserControl_Initialized(object sender, System.EventArgs e)
        {
            View = new View();
        }  
    }
}
