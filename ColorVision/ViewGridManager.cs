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

        public ViewGridManager(Grid grid)
        {
            ViewGrids = new List<Grid>();
            Views = new List<Control>();
            MainView = grid;
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

        private Grid GetNewGrid(Control control)
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

            int newlist = 0;
            for (int i = 0; i < GridLists.Count; i++)
            {
                if (GridLists[i] != null)
                {
                    Grid grid = GridLists[i];
                    int location = Array.IndexOf(defaultViewIndexMap, newlist);
                    int row = (location / 10);
                    int col = (location % 10);
                    if (MainView.ColumnDefinitions.Count <= col)
                    {
                        ColumnDefinition columnDefinition = new ColumnDefinition() { Width = (GridLength)gridLengthConverter.ConvertFrom("*") };
                        MainView.ColumnDefinitions.Add(columnDefinition);
                    }
                    if (MainView.RowDefinitions.Count <= row)
                    {
                        RowDefinition rowDefinition = new RowDefinition() { Height = (GridLength)gridLengthConverter.ConvertFrom("*") };
                        MainView.RowDefinitions.Add(rowDefinition);
                    }

                    grid.SetValue(Grid.RowProperty, row);
                    grid.SetValue(Grid.ColumnProperty, col);
                    MainView.Children.Add(grid);



                    newlist++;
                }
            }

            for (int row = 0; row < MainView.RowDefinitions.Count; row++)
            {
                for (int col = 0; col < MainView.ColumnDefinitions.Count; col++)
                {
                    if (MainView.ColumnDefinitions.Count-1 != col)
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

                    if (MainView.RowDefinitions.Count-1 != row)
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
}
