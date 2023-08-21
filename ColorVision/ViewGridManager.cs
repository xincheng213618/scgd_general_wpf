using ColorVision.MQTT;
using EnumsNET;
using log4net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace ColorVision
{
    public enum ViewType
    {
        Hidden,
        View,
        Window,
    }
    public interface IView
    {
        public ViewType ViewType { get; set; }
        public int ViewIndex { get; set; }
    }


    public class ViewGridManager
    {
        private static readonly int[] defaultViewIndexMap = new int[100]
             {
               0, 1, 4, 9, 16, 25, 36, 49, 64, 81,
               2, 3, 5, 10, 17, 26, 37, 50, 65, 82,
               6, 7, 8, 11, 18, 27, 38, 51, 66, 83,
               12, 13, 14, 15, 19, 28, 39, 52, 67, 84,
               20, 21, 22, 23, 24, 29, 40, 53, 68, 85,
               30, 31, 32, 33, 34, 35, 41, 54, 69, 86,
               42, 43, 44, 45, 46, 47, 48, 55, 70, 87,
               56, 57, 58, 59, 60, 61, 62, 63, 71, 88,
               72, 73, 74, 75, 76, 77, 78, 79, 80, 89,
               90, 91, 92, 93, 94, 95, 96, 97, 98, 99
             };

        public Grid MainView { get; set; }

        public List<Grid> Grids { get; set; }
        public List<Control> Views { get; set; }

        private static ViewGridManager _instance;
        private static readonly object _locker = new();
        public static ViewGridManager GetInstance() { lock (_locker) { return _instance ??= new ViewGridManager(); } }

        //这里些publlic 是故意的,希望这里可以复用，或者是这里后面可能需要重构一下
        public ViewGridManager()
        {
            Grids = new List<Grid>();
            Views = new List<Control>();
            ViewWindows = new List<Control>();
        }

        public int AddView(Control control)
        {
            if (Views.Contains(control))
                return Views.IndexOf(control);

            Views.Add(control);
            return Views.IndexOf(control);
        }

        public void RemoveView(int index)
        {
            Views.RemoveAt(index);
        }

        public void SetViewGrid(int nums)
        {
            GenViewGrid(nums);
            for (int i = 0; i < nums; i++)
            {
                if (i >= Views.Count)
                    break;
                if (Views[i].Parent is Grid grid)
                    grid.Children.Remove(Views[i]);

                Grids[i].Children.Clear();
                Grids[i].Children.Add(Views[i]);
            }
        }


        /// <summary>
        /// 保留固定数量的窗口视图，多余的会删除掉
        /// </summary>
        /// <param name="num"></param>
        public void SetViewNum(int num)
        {
            if (num == -1)
            {
                if (GetViewNums() != Views.Count)
                    GenViewGrid(Views.Count);

                for (int i = 0; i < Views.Count; i++)
                {
                    if (Views[i].Parent is Grid grid)
                        grid.Children.Remove(Views[i]);

                    Grids[i].Children.Clear();
                    Grids[i].Children.Add(Views[i]);
                }
                return;
            }


            if (GetViewNums() < num)
                GenViewGrid(num);

            for (int i = 0; i < num; i++)
            {
                if (Views[i].Parent is Grid grid)
                    grid.Children.Remove(Views[i]);

                Grids[i].Children.Clear();
                Grids[i].Children.Add(Views[i]);
            }
        }

        public Control? CurrentView {
            get
            {
                if (Grids.Count > 0)
                {
                    for (int i = 0; i < Views.Count; i++)
                    {
                        if (Views[i].Parent is Grid grid && grid == Grids[0])
                        {
                            return Views[i];
                        }
                    }
                }
                return null;

            }
        }
       public List<Control> ViewWindows { get; set; }


        public void SetSingleWindowView(Control control)
        {
            if (control.Parent is Grid grid)
                grid.Children.Remove(control);

            Views.Remove(control);
            ViewWindows.Add(control);
            Window window = new Window();
            Grid grid1 = new Grid();
            grid1.Children.Add(control);
            window.Content = grid1;
            window.Closed += (s, e) =>
            {
                ViewWindows.Remove(control);
                Views.Add(control);
            };
            window.Show();
        }

        public void SetOneView(int Main)
        {
            if (GetViewNums()!=1)
                GenViewGrid(1);

            if (Views[Main].Parent is Grid grid)
                grid.Children.Remove(Views[Main]);

            Grids[0].Children.Clear();
            Grids[0].Children.Add(Views[Main]);

        }

        public void SetOneView(Control control)
        {
            if (GetViewNums() != 1)
                GenViewGrid(1);

            if (control.Parent is Grid grid)
                grid.Children.Remove(control);

            Grids[0].Children.Clear();
            Grids[0].Children.Add(control);
        }


        public void SetViewNums(int Nums)
        {
            GenViewGrid(Nums);
        }

        public int GetViewNums()
        {
            return Grids.Count;
        }



        private void GenViewGrid(int Nums)
        {
            if (MainView == null)
                return;

            MainView.Children.Clear();
            MainView.ColumnDefinitions.Clear();
            MainView.RowDefinitions.Clear();

            for (int i = 0; i < Nums; i++)
            {
                int location = Array.IndexOf(defaultViewIndexMap, i);
                int row = (location / 10);
                int col = (location % 10);
                if (MainView.ColumnDefinitions.Count <= col)
                {
                    ColumnDefinition columnDefinition = new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) };
                    MainView.ColumnDefinitions.Add(columnDefinition);
                }
                if (MainView.RowDefinitions.Count <= row)
                {
                    RowDefinition rowDefinition = new RowDefinition() { Height = new GridLength(1, GridUnitType.Star) };
                    MainView.RowDefinitions.Add(rowDefinition);
                }
            }

            Grids.Clear();
            for (int i = 0; i < Nums; i++)
            {
                Grid grid = new Grid() { Margin = new Thickness(2, 2, 2, 2), };
                Grids.Add(grid);
                int location = Array.IndexOf(defaultViewIndexMap, i);
                int row = (location / 10);
                int col = (location % 10);
                grid.SetValue(Grid.RowProperty, row);
                grid.SetValue(Grid.ColumnProperty, col);
                MainView.Children.Add(grid);

                if (MainView.ColumnDefinitions.Count - 1 != col)
                {
                    GridSplitter gridSplitter = new GridSplitter()
                    {
                        Background = Brushes.LightGray,
                        Width = 2,
                        HorizontalAlignment = HorizontalAlignment.Right,
                    };

                    gridSplitter.SetValue(Grid.RowProperty, row);
                    gridSplitter.SetValue(Grid.ColumnProperty, col);
                    MainView.Children.Add(gridSplitter);
                }

                if (MainView.RowDefinitions.Count - 1 != row)
                {
                    GridSplitter gridSplitter1 = new GridSplitter()
                    {
                        Background = Brushes.LightGray,
                        Height = 2,
                        HorizontalAlignment = HorizontalAlignment.Stretch,
                        VerticalAlignment = VerticalAlignment.Bottom,
                    };

                    gridSplitter1.SetValue(Grid.RowProperty, row);
                    gridSplitter1.SetValue(Grid.ColumnProperty, col);

                    MainView.Children.Add(gridSplitter1);
                }
            }

        }
    }
}
