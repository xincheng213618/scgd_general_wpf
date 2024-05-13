using LiveChartsCore.VisualElements;
using Microsoft.Xaml.Behaviors;
using Microsoft.Xaml.Behaviors.Layout;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using ColorVision.Adorners;


namespace ColorVision.Solution.Searches
{
    /// <summary>
    /// HomePage.xaml 的交互逻辑
    /// </summary>
    public partial class HomePage : Page
    {
        public Frame Frame { get; set; }

        public HomePage(Frame frame)
        {
            Frame = frame;
            InitializeComponent();
        }

        private void Page_Initialized(object sender, EventArgs e) 
        {
            FluidMoveBehavior fluidMoveBehavior = new()
            {
                AppliesTo = FluidMoveScope.Children,
                Duration = TimeSpan.FromSeconds(0.1)
            };

            Interaction.GetBehaviors(ContentStackPanel).Add(fluidMoveBehavior);
            //ContentStackPanel.AddAdorners(this);

        }
        private void Page_Loaded(object sender, RoutedEventArgs e)
        {

        }

        private void UserControl_PreviewKeyUp(object sender, KeyEventArgs e)
        {
        }

        private void UserControl_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is UserControl userControl)
            {
                switch (userControl.Tag)
                {
                    case "ArchivePage":
                        Frame.Navigate(new ArchivePage(Frame));
                        break;
                    case "DataSummaryPage":
                        Frame.Navigate(new DataSummaryPage(Frame));
                        break;
                    default:
                        break;
                }
            }
        }
    }
}
