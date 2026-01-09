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
            // Fix: Use the static method instead of 'new' to ensure singleton behavior
            var window = EqeWindow.GetEqeWindow(new ObservableCollection<ViewResultEqe>());
            window.Owner = Application.Current.GetActiveWindow();
            window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            window.Show();
        }
    }



    /// <summary>
    /// EqeWindow.xaml 的交互逻辑
    /// </summary>
    public partial class EqeWindow : Window
    {

        private static EqeWindow _instance;

        public static EqeWindow GetEqeWindow(ObservableCollection<ViewResultEqe> viewResults)
        {
            // Check if instance exists and is actually usable (Loaded)
            if (_instance == null || !_instance.IsLoaded)
            {
                _instance = new EqeWindow();
            }
            // Bring to front if it was already open
            if (_instance.WindowState == WindowState.Minimized)
                _instance.WindowState = WindowState.Normal;

            _instance.Activate();

            // Add new data
            if (viewResults != null && viewResults.Count > 0)
            {
                foreach (var result in viewResults)
                {
                    _instance.AddViewResultEqe(result);
                }
            }

            return _instance;
        }


        public ViewEqe ViewEqe { get; private set; }
        private EqeWindow()
        {
            InitializeComponent();
            ViewEqe = new ViewEqe();
            ViewEqeContent.Content = ViewEqe;
        }

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
        }

        public void AddViewResultEqe(ViewResultEqe viewResultEqe) => ViewEqe.AddViewResultEqe(viewResultEqe);


        private void Window_Closed(object sender, System.EventArgs e)
        {
            _instance = null;
        }
    }
}
