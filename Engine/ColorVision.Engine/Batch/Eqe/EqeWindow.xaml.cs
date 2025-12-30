using ColorVision.Engine.Services.Devices.Spectrum.Views;
using System.Collections.ObjectModel;
using System.Windows;

namespace ColorVision.Engine.Batch.Eqe
{
    /// <summary>
    /// EqeWindow.xaml 的交互逻辑
    /// </summary>
    public partial class EqeWindow : Window
    {
        public EqeWindow(ObservableCollection<ViewResultEqe> viewResults)
        {
            InitializeComponent();

            // Create and initialize ViewEqe control
            var viewEqe = new ViewEqe();
            
            // Populate with data
            foreach (var result in viewResults)
            {
                viewEqe.AddViewResultEqe(result);
            }

            // Set the content
            ViewEqeContent.Content = viewEqe;
        }
    }
}
