using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Linq;
using ColorVision.Solution.V;
using ColorVision.Util;

namespace ColorVision.Solution
{


    /// <summary>
    /// TreeViewControl.xaml 的交互逻辑
    /// </summary>
    public partial class TreeViewControl : UserControl
    {

        public TreeViewControl()
        {
            Window window = Application.Current.MainWindow;
            if (window != null)
                window.Closing += Window_Closed;
            InitializeComponent();
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
                        OpenSolution(fn);
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
                            OpenSolution(item.FullName);
                            break;
                        }
                    }
                }
            }
        }

        public bool OpenSolution(string FullName)
        {
            bool sucess = false;
            if (!string.IsNullOrEmpty(FullName))
            {
                SolutionFullName = FullName;
                TreeViewInitialized(FullName);
                sucess = true;
            }
            return sucess;
        }

        bool IsFirstLoad = true;
        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            this.AllowDrop = true;
            this.Drop += TreeViewControl_Drop;

            SolutionTreeView.ContextMenu = new ContextMenu();
            SolutionTreeView.ContextMenuOpening += SolutionTreeView_ContextMenuOpening;
        }


        private void SolutionTreeView_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            //if (SolutionTreeView.SelectedItems.Count == 1)
            //{
            //    if (SelectedTreeViewItem != null && SelectedTreeViewItem.DataContext is GrifFile VObject)
            //    {
            //        SolutionTreeView.ContextMenu = VObject.ContextMenu;
            //    }
            //}
            //if (SolutionTreeView.SelectedItems.Count > 1)
            //{
            //    SolutionTreeView.ContextMenu = new ContextMenu();
            //    MenuItem menuItem = new MenuItem() { Header = "删除(_D)" };
            //    menuItem.Click +=(s,e) =>
            //    {
            //        Dictionary<GrifFile, Action<object>> Actionlist = new Dictionary<GrifFile, Action<object>>();
            //        foreach (VObject item in SolutionTreeView.SelectedItems)
            //        {
            //            if (item is GrifFile VObject)
            //            {
            //                Actionlist.Add(VObject, VObject.DeleteCommand.execute);
            //            }
            //        }

            //        foreach (var item in Actionlist)
            //        {
            //            item.Key.DeleteShowDialog = false;
            //            item.Value(item.Key);
            //        }
            //    };
            //    SolutionTreeView.ContextMenu.Items.Add(menuItem);
            //}



        }


        public ObservableCollection<SolutionExplorer> SolutionExplorers { get; set; } = new ObservableCollection<SolutionExplorer>();


        private Point SelectPoint;

        private VObject LastReNameObject;
        private TreeViewItem? SelectedTreeViewItem;
        private TreeViewItem? LastSelectedTreeViewItem;

        private string solutionDir;

        public string SolutionDir
        {
            get { return solutionDir; }
            set
            {
                if (value != null && value != solutionDir)
                {
                    solutionDir = value;
                }
            }
        }

        private string solutionFullName;

        public string SolutionFullName
        {
            get { return solutionFullName; }
            set
            {
                if (solutionFullName != value)
                {
                    solutionFullName = value;
                }
            }
        }

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
                    //这里因为考虑到和lambda接轨，所以暂时不拆出来，合并类和基类的扩展中
                    //if (item.DataContext is GrifFile grifFile)
                    //{
                    //    grifFile.OpenFileCommand.Execute(grifFile);
                    //}
                    //else if (item.DataContext is ProjectFile projectFile1)
                    //{
                    //    LambdaControl.Trigger("SolutionpProjectFileOpen", this, projectFile1.FullName);
                    //}
                    //if (item.DataContext is SeriesProjectManager seriesProjectManager1)
                    //{
                    //    seriesProjectManager1.OpenCommand.Execute(seriesProjectManager1);
                    //}
                }
            }
            else
            {
                SelectedTreeViewItem = null;
            }



            //if (e.RightButton == MouseButtonState.Pressed)   vbv
            //{
            //    HitTestResult result = VisualTreeHelper.HitTest(TreeView1, SelectPoint);
            //}
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            OpenSolution();
        }


        private void OpenSolution()
        {

        }




        private void TreeViewInitialized(string FilePath, bool init = true)
        {
            SolutionTreeView.ItemsSource = SolutionExplorers;
            if (init)
                SolutionExplorers.Clear();

        }



        private void Save_Click(object sender, RoutedEventArgs e)
        {

        }


        private void Config_Set_Click(object sender, RoutedEventArgs e)
        {
        }


        private void UserControl_Initialized(object sender, EventArgs e)
        {


        }




        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            NewCreat();
        }

        private void NewCreat()
        {

        }


        private void Close_Click(object sender, RoutedEventArgs e)
        {
            SolutionClose();
        }
        private void SolutionClose()
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
