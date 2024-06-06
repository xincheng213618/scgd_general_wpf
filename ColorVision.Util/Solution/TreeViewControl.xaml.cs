using ColorVision.Common.Utilities;
using ColorVision.Solution.V;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ColorVision.Solution
{
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
            IniCommand();
            Window window = Application.Current.MainWindow;
            if (window != null)
                window.Closing += Window_Closed;
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
        private TreeViewItem? LastSelectedTreeViewItem;


        private void Window_Closed(object? sender, EventArgs e)
        {

        }

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
                if (SelectedTreeViewItem != null && SelectedTreeViewItem != item && SelectedTreeViewItem.DataContext is VObject viewModeBase)
                {
                    viewModeBase.IsEditMode = false;
                    if (LastSelectedTreeViewItem?.DataContext is VObject VObject)
                    {
                        viewModeBase.IsSelected = true;
                        VObject.IsSelected = true;

                    }
                }
                SelectedTreeViewItem = item;

                LastSelectedTreeViewItem = item;



                if (SolutionManager.SolutionExplorers.Count != 1 && item.DataContext is SolutionExplorer solutionExplorer)
                {

                }
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
        private void SearchBar1_TextChanged(object sender, TextChangedEventArgs e)
        {
            string text = SearchBar1.Text;
            if (string.IsNullOrWhiteSpace(text))
            {
                SolutionTreeView.ItemsSource = SolutionManager.GetInstance().SolutionExplorers;
            }
            else
            {
                var keywords = text.Split(Chars, StringSplitOptions.RemoveEmptyEntries);

                var filteredResults = SolutionManager.GetInstance().SolutionExplorers[0].VisualChildren.
                    SelectMany(explorer => GetAllVisualChildren(explorer.VisualChildren))
                    .OfType<VObject>()
                    .Where(template => keywords.All(keyword =>
                        template.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase) 
                        ))
                    .ToList();

                // 更新 ListView 的数据源
                SolutionTreeView.ItemsSource = filteredResults;
            }
        }
        private IEnumerable<VObject> GetAllVisualChildren(IEnumerable<VObject> visualChildren)
        {
            foreach (var child in visualChildren)
            {
                yield return child;

                foreach (var grandChild in GetAllVisualChildren(child.VisualChildren))
                {
                    yield return grandChild;
                }
            }
        }
    }



}
