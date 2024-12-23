using ColorVision.UI;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;


namespace ColorVision.Solution.Searches
{
    public interface ISolutionPage
    {
        public string PageTitle { get; }
    }

    public class SolutionPageManager
    {
        public static SolutionPageManager Instance { get; set; } = new SolutionPageManager();
        public SolutionPageManager()
        {
            Pages = new Dictionary<string, Type>(); 
            foreach (var assembly in AssemblyHandler.GetInstance().GetAssemblies())
            {
                foreach (var type in assembly.GetTypes())
                {
                    if (typeof(ISolutionPage).IsAssignableFrom(type) && !type.IsInterface)
                    {
                        if (Activator.CreateInstance(type) is ISolutionPage page)
                        {
                            Pages.Add(page.PageTitle, type);
                        }
                    }
                }
            }
        }
        public Page GetPage(string? pageTitle, Frame frame)
        {
            if (string.IsNullOrEmpty(pageTitle))
            {
                return new HomePage(frame);
            }
            if (Pages.TryGetValue(pageTitle, out Type type))
            {
                if (Activator.CreateInstance(type, frame) is Page page)
                {
                    return page;
                }
            }
            return new HomePage(frame);
        }

        public Dictionary<string,Type> Pages { get; set; }


    }


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
                Frame.Navigate(SolutionPageManager.Instance.GetPage(userControl.Tag.ToString(), Frame));
            }
        }
    }
}
