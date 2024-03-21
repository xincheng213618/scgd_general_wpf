using ColorVision.RC;
using ColorVision.Services.Dao;
using ColorVision.Services.Terminal;
using ColorVision.Settings;
using Newtonsoft.Json;
using System;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Services.Type
{
    /// <summary>
    /// TypeServiceControl.xaml 的交互逻辑
    /// </summary>
    public partial class TypeServiceControl : UserControl
    {
        public TypeService TypeService { get; set; }
        public TypeServiceControl(TypeService typeService)
        {
            this.TypeService = typeService;
            InitializeComponent();
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            this.DataContext = TypeService;
            ListViewService.ItemsSource = TypeService.VisualChildren;
        }

        private void ListViewService_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ListView listView && listView.SelectedIndex > -1)
            {
                if (TypeService.VisualChildren[listView.SelectedIndex] is TerminalService serviceTerminal)
                {
                    if (this.Parent is Grid grid)
                    {
                        grid.Children.Clear();
                        grid.Children.Add(serviceTerminal.GenDeviceControl());
                    }
                    
                }
            }
        }
    }
}
 