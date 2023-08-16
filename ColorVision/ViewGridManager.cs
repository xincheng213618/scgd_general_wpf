using ColorVision.MQTT;
using log4net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ColorVision
{
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

        private List<Grid> ViewGrids {get;set;}

        public Grid MainView { get; set; }

        public List<Control> Views { get; set; }


        private static readonly ILog log = LogManager.GetLogger(typeof(MQTTControl));

        private static ViewGridManager _instance;
        private static readonly object _locker = new();
        public static ViewGridManager GetInstance() { lock (_locker) { return _instance ??= new ViewGridManager(); } }

        //这里些publlic 是故意的,希望这里可以复用，或者是这里后面可能需要重构一下
        public ViewGridManager()
        {
            ViewGrids = new List<Grid>();
            Views = new List<Control>();
        }

        public void AddView(Control control)
        {
            Views.Add(control);
            Grid grid = GetNewGrid(control);
            ViewGrids.Add(grid);
            GridSort(ViewGrids);
        }

        public void RemoveView(int index)
        {
            ViewGrids.RemoveAt(index);
            GridSort(ViewGrids);
            Views.RemoveAt(index);
        }

        /// <summary>
        /// 保留固定数量的窗口视图，多余的会删除掉
        /// </summary>
        /// <param name="num"></param>
        public void SetViewNum(int num)
        {
            if (Views.Count > num)
            {
                for (int i = Views.Count - 1; i > num-1; i--)
                {
                    ViewGrids.RemoveAt(i);
                    Views.RemoveAt(i);
                }
                GridSort(ViewGrids);
            }
            else if (Views.Count < num)
            {
                for (int i = Views.Count; i < num ; i++)
                {
                    AddView(new ImageView());
                }
                GridSort(ViewGrids);
            }
        }

        public void SetOneView(int Main)
        {
            if (Main >= 0 && Main< ViewGrids.Count)
            {
                List<Grid> newGrids = new List<Grid>();
                newGrids.Add(ViewGrids[Main]);
                GridSort(newGrids);
            }
        }

        public void SetFourView(int Main)
        {
            if (Main >= 0 && Main < ViewGrids.Count)
            {
                List<Grid> newGrids = new List<Grid>();
                newGrids.Add(ViewGrids[Main]);
                if (Main - 1 >= 0)
                {
                    newGrids.Add(ViewGrids[Main - 1]);
                }
                if (Main + 1 < ViewGrids.Count)
                {
                    newGrids.Add(ViewGrids[Main + 1]);
                }
                if (Main - 10 >= 0)
                {
                    newGrids.Add(ViewGrids[Main - 10]);
                }
                if (Main + 10 < ViewGrids.Count)
                {
                    newGrids.Add(ViewGrids[Main + 10]);
                }
                GridSort(newGrids);
            }
        }





        private static Grid GetNewGrid(Control control)
        {
            Grid grid = new Grid()
            {
                Margin = new Thickness(2, 2, 2, 2),
            };

            grid.Children.Add(control);
            return grid;
        }


        static GridLengthConverter gridLengthConverter = new GridLengthConverter();
        private void GridSort(List<Grid> GridLists)
        {
            MainView.Children.Clear();
            MainView.ColumnDefinitions.Clear();
            MainView.RowDefinitions.Clear();

            for (int i = 0; i < GridLists.Count; i++)
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


            for (int i = 0; i < GridLists.Count; i++)
            {
                Grid grid = GridLists[i];
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
