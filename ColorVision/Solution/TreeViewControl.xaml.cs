using ColorVision.Common.Utilities;
using ColorVision.Solution.V;
using System;
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
        public SolutionManager SolutionManager { get; set; }
        public TreeViewControl()
        {
            InitializeComponent();
        }
        public ObservableCollection<SolutionExplorer> SolutionExplorers { get => SolutionManager.SolutionExplorers; }
        private void UserControl_Initialized(object sender, EventArgs e)
        {
            SolutionManager = SolutionManager.GetInstance();
            SolutionTreeView.ItemsSource = SolutionExplorers;
            IniCommand();

            Window window = Application.Current.MainWindow;
            if (window != null)
                window.Closing += Window_Closed;
        }
        
        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            SolutionExplorers[0].Refresh();
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
                    DirectoryInfo directoryInfo = new DirectoryInfo(fn);
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
            this.AllowDrop = true;
            this.Drop += TreeViewControl_Drop;

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



                if (SolutionExplorers.Count != 1 && item.DataContext is SolutionExplorer solutionExplorer)
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

        private void OpenSolution(object sender, RoutedEventArgs e)
        {
            SolutionManager.OpenSolutionWindow();
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {

        }


        private void Config_Set_Click(object sender, RoutedEventArgs e)
        {
        }


        private void SolutionNewCreat(object sender, RoutedEventArgs e)
        {
            SolutionManager.NewCreateWindow();
        }


        private void Close_Click(object sender, RoutedEventArgs e)
        {
        }


        private unsafe void Button_Click_1(object sender, RoutedEventArgs e)
        {
            HandyControl.Controls.Growl.Info("此功能在测试中");
        }

        private void Button_Click_4(object sender, RoutedEventArgs e)
        {
            HandyControl.Controls.Growl.Info("此功能还在开发中，暂停使用");
        }

        private void Button_Click_5(object sender, RoutedEventArgs e)
        {
            HandyControl.Controls.Growl.Info("此功能在测试中");
        }


    }



}
