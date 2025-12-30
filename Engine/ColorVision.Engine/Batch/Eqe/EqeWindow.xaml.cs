using ColorVision.Engine.Services.Devices.Spectrum.Views;
using ColorVision.UI.Menus;
using System.Collections.ObjectModel;
using System.Windows;

namespace ColorVision.Engine.Batch.Eqe
{

    public class EqeWindowMenu : MenuItemBase
    {
        public override string OwnerGuid => MenuItemConstants.View;
        public override int Order => 200;
        public override string Header => "EqeWindow";
        public override void Execute()
        {
            new EqeWindow(new ObservableCollection<ViewResultEqe>()) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.Show();
        }
    }

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
