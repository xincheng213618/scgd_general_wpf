using ColorVision.Themes;
using ColorVision.UI.Menus;
using System;
using System.Windows;

namespace ColorVision.Engine.Rbac
{
    public class ExportLogin : MenuItemBase
    {
        public override string OwnerGuid => MenuItemConstants.Help;
        public override string GuidId => "Login";
        public override int Order => 3;
        public override string Header => Properties.Resources.MenuLogin;
        public override void Execute()
        {
            new LoginWindow() { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
        }
    }



    /// <summary>
    /// LoginWindow.xaml 的交互逻辑
    /// </summary>
    public partial class LoginWindow : Window
    {
        public LoginWindow()
        {
            InitializeComponent();
            this.ApplyCaption();
        }

        private void Window_Initialized(object sender, EventArgs e)
        {

        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            UserDao userDao = new UserDao();
            if (userDao.Checklogin(Account1.Text, PasswordBox1.Password))
            {
                Close();
            }
            else
            {
                PasswordBox1.Password = "";
                MessageBox.Show(Application.Current.MainWindow,"用户名或者密码不正确", "ColorVision");
            }
        }
    }
}
