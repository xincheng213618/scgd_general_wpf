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

        private static EqeWindow Instance { get; set; }

        public static EqeWindow GetEqeWindow(ObservableCollection<ViewResultEqe> viewResults)
        {
            if (Instance == null)
            {
                Instance = new EqeWindow(viewResults);
            }
            foreach (var viewResult in viewResults)
            {
                Instance.AddViewResultEqe(viewResult);
            }
            return Instance;
        }


        public ViewEqe ViewEqe { get; private set; }

        public EqeWindow(ObservableCollection<ViewResultEqe> viewResults)
        {
            InitializeComponent();

            // Create and initialize ViewEqe control
            ViewEqe = new ViewEqe();
            
            // Populate with data
            foreach (var result in viewResults)
            {
                ViewEqe.AddViewResultEqe(result);
            }

            // Set the content
            ViewEqeContent.Content = ViewEqe;
            Instance = this;
        }

        public void AddViewResultEqe(ViewResultEqe viewResultEqe) => ViewEqe.AddViewResultEqe(viewResultEqe);


        private void Window_Closed(object sender, System.EventArgs e)
        {
            Instance = null;
        }
    }
}
