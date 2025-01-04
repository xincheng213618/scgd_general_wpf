using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace ColorVision.UI.Views
{

    public class ViewGridManager
    {
        private static readonly int[] defaultViewIndexMap = new int[100]{
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
        private static readonly int[] defaultViewIndexMap1 = new int[100] {
               0, 2, 4, 9, 16, 25, 36, 49, 64, 81,
               1, 3, 5, 10, 17, 26, 37, 50, 65, 82,
               6, 7, 8, 11, 18, 27, 38, 51, 66, 83,
               12, 13, 14, 15, 19, 28, 39, 52, 67, 84,
               20, 21, 22, 23, 24, 29, 40, 53, 68, 85,
               30, 31, 32, 33, 34, 35, 41, 54, 69, 86,
               42, 43, 44, 45, 46, 47, 48, 55, 70, 87,
               56, 57, 58, 59, 60, 61, 62, 63, 71, 88,
               72, 73, 74, 75, 76, 77, 78, 79, 80, 89,
               90, 91, 92, 93, 94, 95, 96, 97, 98, 99
     };



        public event ViewMaxChangedHandler ViewMaxChangedEvent;

        public int ViewMax { get => _ViewMax; set { if (_ViewMax == value) return;ViewMaxChangedEvent?.Invoke(value); _ViewMax = value; } }
        private int _ViewMax;

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
        }

        public bool IsGridEmpty(int index)
        {
            if (index < 0 || index >= Grids.Count)
                return false;
            return Grids[index].Children.Count == 0;
        }

        public int AddView(int index,Control control)
        {
            if (control == null)
                return -1;

            if (Views.Contains(control))
                return Views.IndexOf(control);

            Views.Insert(0, control);

            if (control is IView view)
            {
                view.View.ViewGridManager = this;
                if (IsGridEmpty(view.View.ViewIndex))
                {
                    Grids[view.View.ViewIndex].Children.Add(control);
                }
                else
                {
                    view.View.ViewIndex = Views.IndexOf(control);
                }

            }
            return Views.IndexOf(control);
        }


        public int AddView(Control control)
        {
            if (control == null)
                return -1;

            if (Views.Contains(control))
                return Views.IndexOf(control);

            Views.Add(control);

            if (control is IView view)
            {
                view.View.ViewGridManager = this;
                if (IsGridEmpty(view.View.ViewIndex))
                {
                    Grids[view.View.ViewIndex].Children.Add(control);
                }
                else
                {
                    view.View.ViewIndex = Views.IndexOf(control);
                }

            }
            return Views.IndexOf(control);
        }

        public void RemoveView(int index)
        {
            Views.RemoveAt(index);
        }

        public void RemoveView(Control control)
        {
            if (control is IView view)
            {
                if (view.View.ViewType != ViewType.Hidden)
                {
                    SetViewIndex(control, -1);
                }
            }
            Views.Remove(control);
            MainView?.Children.Remove(control);
        }

        public void SetViewIndex(Control control, int viewIndex)
        {
            if (viewIndex >= 0)
            {
                if (viewIndex >= ViewMax)
                    return;

                if (control.Parent is Grid grid)
                    grid.Children.Remove(control);

                if (Grids.Count > viewIndex )
                {
                    if (Grids[viewIndex].Children.Count == 1 && Grids[viewIndex].Children[0] is IView view1)
                        view1.View.ViewIndex = -1;
                    if (Grids[viewIndex].Children.Count == 2 && Grids[viewIndex].Children[1] is IView view2)
                        view2.View.ViewIndex = -1;
                }



                Grids[viewIndex].Children.Clear();
                Grids[viewIndex].Children.Add(control);
            }
            else if (viewIndex == -1)
            {
                if (control.Parent is Grid grid)
                    grid.Children.Remove(control);
            }
            else if (viewIndex == -2)
            {
                SetSingleWindowView(control);
            }


        }  

        private void SetView(int nums)
        {
            for (int i = 0; i < nums; i++)
            {
                foreach (var item in Views)
                {
                    if (item is IView view1)
                    {
                        if (view1.View.ViewIndex == i)
                        {
                            if (item.Parent is Grid grid1)
                                grid1.Children.Remove(item);

                            Grids[i].Children.Clear();
                            Grids[i].Children.Add(item);
                            break;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 设置一共有几个窗口
        /// </summary>
        /// <param name="nums"></param>
        public void SetViewGrid(int nums)
        {
            GenViewGrid(nums);
            SetView(nums);

            for (int i = 0; i < nums; i++)
            {
                if (i< Views.Count)
                {
                    if (Views[i] is IView view1 && view1.View.ViewIndex < 0)
                    {
                        view1.View.ViewIndex = i;
                    }
                }
            }
        }
        public void SetViewGridTwo()
        {
            GenViewGrid(2, false);
            SetView(2);
        }

        public void SetViewGridThree(bool left =true)
        {
            GenViewGrid(3);
            if (left)
            {
                Grid.SetRowSpan(Grids[0], 2);
                MainView.Children.Remove(gridSplitters[0][1]);
                Grid.SetColumn(Grids[2],1);
            }
            else
            {
                Grid.SetRowSpan(Grids[1], 2);
                MainView.Children.Remove(gridSplitters[1][0]);
            }
            SetView(3);
        }



        /// <summary>
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

                    if (Views[i] is IView view)
                        view.View.ViewIndex = i;
                    Grids[i].Children.Clear();
                    Grids[i].Children.Add(Views[i]);
                }
                return;
            }

            if (Views.Count == 0) return;

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

        public void SetSingleWindowView(Control control)
        {
            if (control.Parent is Grid grid)
                grid.Children.Remove(control);

            if (control is IView view)
            {
                Window window = new() { Owner = Application.Current.MainWindow};
                Binding binding = new("Title") { Source = view.View };
                window.SetBinding(Window.TitleProperty, binding);
                Binding binding1 = new("Icon") { Source = view.View };
                window.SetBinding(Window.IconProperty, binding1);

                ViewIndexChangedHandler eventHandler = null;
                eventHandler = (e1,e2) =>
                {
                    window.Close();
                    view.View.ViewIndexChangedEvent -= eventHandler;
                };
                view.View.ViewIndexChangedEvent += eventHandler;

                if (control.Parent is Grid grid2)
                    grid2.Children.Remove(control);
                Views.Remove(control);
                Grid grid1 = new();
                grid1.Children.Add(control);
                window.Content = grid1;
                window.Closed += (s, e) =>
                {
                    view.View.ViewIndexChangedEvent -= eventHandler;
                    view.View.ViewIndex = IsGridEmpty(view.View.PreViewIndex)?view.View.PreViewIndex: -1;
                    Views.Add(control);
                };
                window.Show();

            }
        }

        public void SetOneView(int Main)
        {
            if (GetViewNums()!=1)
                GenViewGrid(1);

            if (Views[Main].Parent is Grid grid)
                grid.Children.Remove(Views[Main]);


            if (Views[Main] is IView view)
                view.View.ViewIndex = 0;

            Grids[0].Children.Clear();
            Grids[0].Children.Add(Views[Main]);

        }

        public void SetOneView(Control control)
        {
            if (GetViewNums() != 1)
                GenViewGrid(1);

            if (control.Parent is Grid grid)
                grid.Children.Remove(control);

            if (control is IView view)
                view.View.ViewIndex = 0;

            if (Grids[0].Children.Count>0 && Grids[0].Children[0] is IView view1)
            {
                view1.View.ViewIndex = -1;
            }

            Grids[0].Children.Clear();
            Grids[0].Children.Add(control);
        }

        public int GetViewNums()
        {
            return Grids.Count;
        }



        private List<List<GridSplitter>> gridSplitters = new();

        private void GenViewGrid(int Nums,bool defaultmap =true)
        {
            int[] maps = defaultmap ? defaultViewIndexMap : defaultViewIndexMap1;

            if (MainView == null)
                return;
            ViewMax = Nums;
            MainView.Children.Clear();
            MainView.ColumnDefinitions.Clear();
            MainView.RowDefinitions.Clear();
            gridSplitters.Clear();

            for (int i = 0; i < Nums; i++)
            {
                int location = Array.IndexOf(maps, i);
                int row = (location / 10);
                int col = (location % 10);
                if (MainView.ColumnDefinitions.Count <= col)
                {
                    ColumnDefinition columnDefinition = new() { Width = new GridLength(1, GridUnitType.Star) };
                    MainView.ColumnDefinitions.Add(columnDefinition);
                }
                if (MainView.RowDefinitions.Count <= row)
                {
                    RowDefinition rowDefinition = new() { Height = new GridLength(1, GridUnitType.Star) };
                    MainView.RowDefinitions.Add(rowDefinition);
                }
            }

            Grids.Clear();
            for (int i = 0; i < Nums; i++)
            {
                Grid grid = new() { Margin = new Thickness(0), };
                Grids.Add(grid);

                var text = new TextBlock { Text = (i + 1).ToString(),Foreground =Brushes.Red,HorizontalAlignment =HorizontalAlignment.Center,FontSize=30};
                Panel.SetZIndex(text,0);

                grid.Children.Add(text);
                int location = Array.IndexOf(maps, i);
                int row = (location / 10);
                int col = (location % 10);
                grid.SetValue(Grid.RowProperty, row);
                grid.SetValue(Grid.ColumnProperty, col);
                MainView.Children.Add(grid);

                gridSplitters.Add(new List<GridSplitter>());

                if (MainView.ColumnDefinitions.Count - 1 != col)
                {
                    GridSplitter gridSplitter = new()
                    {
                        Background = Brushes.LightGray,
                        Width = 2,
                        HorizontalAlignment = HorizontalAlignment.Right,
                    };
                    gridSplitter.SetValue(Grid.RowProperty, row);
                    gridSplitter.SetValue(Grid.ColumnProperty, col);
                    MainView.Children.Add(gridSplitter);
                    gridSplitters[i].Add(gridSplitter);
                }

                if (MainView.RowDefinitions.Count - 1 != row)
                {
                    GridSplitter gridSplitter1 = new()
                    {
                        Background = Brushes.LightGray,
                        Height = 2,
                        HorizontalAlignment = HorizontalAlignment.Stretch,
                        VerticalAlignment = VerticalAlignment.Bottom,
                    };

                    gridSplitter1.SetValue(Grid.RowProperty, row);
                    gridSplitter1.SetValue(Grid.ColumnProperty, col);
                    gridSplitters[i].Add(gridSplitter1);
                    MainView.Children.Add(gridSplitter1);
                }
            }

        }
    }
}
