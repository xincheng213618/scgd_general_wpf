using ColorVision.Common.Utilities;
using ColorVision.Solution.V;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace ColorVision.Solution
{
    public class TreeViewItemMarginConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            double num = 0.0;
            UIElement uIElement = value as TreeViewItem;
            while (uIElement != null && uIElement.GetType() != typeof(TreeView))
            {
                uIElement = (UIElement)VisualTreeHelper.GetParent(uIElement);
                if (uIElement is TreeViewItem)
                {
                    num += 12.0;
                }
            }

            return new Thickness(num, 0.0, 0.0, 0.0);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    /// <summary>
    /// TreeViewControl.xaml 的交互逻辑
    /// </summary>
    public partial class TreeViewControl : UserControl
    {
        public static SolutionManager SolutionManager => SolutionManager.GetInstance();
        public TreeViewControl()
        {
            InitializeComponent();
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            this.DataContext = SolutionManager;
            SolutionTreeView.ItemsSource = SolutionManager.SolutionExplorers;

            SolutionManager.SolutionExplorers[0].VisualChildrenEventHandler += (s, e) => SearchBar1TextChanged();
            IniCommand();
        }

        private void TreeViewControl_Drop(object sender, DragEventArgs e)
        {
            var b = e.Data.GetDataPresent(DataFormats.FileDrop);

            if (b)
            {
                var sarr = e.Data.GetData(DataFormats.FileDrop);
                var a = sarr as string[];
                var fn = a?.First();

                if (File.Exists(fn))
                {
                    if (fn.Contains(".gprj"))
                    {
                        SolutionManager.OpenSolution(fn);
                    }
                    else
                    {
                        MessageBox.Show("文件的格式不受支持");
                    }
                }
                else if (Directory.Exists(fn))
                {
                    DirectoryInfo directoryInfo = new(fn);
                    foreach (var item in directoryInfo.GetFiles())
                    {
                        if (item.Extension == ".cvproj")
                        {
                            SolutionManager.OpenSolution(item.FullName);
                            break;
                        }
                    }
                }
            }
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            AllowDrop = true;
            Drop += TreeViewControl_Drop;

            SolutionTreeView.ContextMenu = new ContextMenu();
            SolutionTreeView.ContextMenuOpening += SolutionTreeView_ContextMenuOpening;
        }


        private void SolutionTreeView_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
        }

        private Point SelectPoint;

        private VObject LastReNameObject;
        private TreeViewItem? SelectedTreeViewItem;


        //第一次的点击逻辑
        protected override void OnPreviewMouseDown(MouseButtonEventArgs e)
        {
            base.OnPreviewMouseDown(e);

            SelectPoint = e.GetPosition(SolutionTreeView);
            HitTestResult result = VisualTreeHelper.HitTest(SolutionTreeView, SelectPoint);
            if (result != null)
            {
                TreeViewItem item = ViewHelper.FindVisualParent<TreeViewItem>(result.VisualHit);
                if (item == null)
                    return;
                if (item.DataContext is VObject vObject)
                {
                    vObject.IsSelected = true;
                }
                if (SelectedTreeViewItem != null && SelectedTreeViewItem != item && SelectedTreeViewItem.DataContext is VObject vobj)
                {
                    vobj.IsEditMode = false;
                }
                SelectedTreeViewItem = item;
                if (e.ClickCount == 2)
                {
                }
            }
            else
            {
                SelectedTreeViewItem = null;
            }
        }
        private readonly char[] Chars = new[] { ' ' };

        public void SearchBar1TextChanged()
        {
            string text = SearchBar1.Text;
            if (string.IsNullOrWhiteSpace(text))
            {
                SolutionTreeView.ItemsSource = SolutionManager.GetInstance().SolutionExplorers;
            }
            else
            {
                var keywords = text.Split(Chars, StringSplitOptions.RemoveEmptyEntries);
                var filteredResults = SolutionManager.GetInstance().SolutionExplorers.
                    SelectMany(explorer => explorer.VisualChildren.GetAllVisualChildren())
                    .OfType<VObject>()
                    .Where(template => keywords.All(keyword =>
                        template.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                        ))
                    .ToList();

                // 更新 ListView 的数据源
                SolutionTreeView.ItemsSource = filteredResults;
            }
        }

        private void SearchBar1_TextChanged(object sender, TextChangedEventArgs e)
        {
            SearchBar1TextChanged();
        }


        private void TreeViewItem_RequestBringIntoView(object sender, RequestBringIntoViewEventArgs e)
        {
            e.Handled = true;
        }
    }



}
