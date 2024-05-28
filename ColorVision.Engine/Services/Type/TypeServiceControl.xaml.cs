using ColorVision.Services.Terminal;
using System;
using System.Windows.Controls;

namespace ColorVision.Services.Types
{
    /// <summary>
    /// TypeServiceControl.xaml 的交互逻辑
    /// </summary>
    public partial class TypeServiceControl : UserControl
    {
        public TypeService TypeService { get; set; }
        public TypeServiceControl(TypeService typeService)
        {
            TypeService = typeService;
            InitializeComponent();
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            DataContext = TypeService;
            ListViewService.ItemsSource = TypeService.VisualChildren;
        }

        private void ListViewService_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ListView listView && listView.SelectedIndex > -1)
            {
                if (TypeService.VisualChildren[listView.SelectedIndex] is TerminalService serviceTerminal)
                {
                    if (Parent is Grid grid)
                    {
                        grid.Children.Clear();
                        grid.Children.Add(serviceTerminal.GenDeviceControl());
                    }
                    
                }
            }
        }
    }
}
 