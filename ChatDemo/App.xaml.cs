using ColorVision.Themes;
using System.Configuration;
using System.Data;
using System.Windows;

namespace ChatDemo
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public App()
        {
            this.ForceApplyTheme(Theme.Light);
            ChatGPTWindow chatGPTWindow = new ChatGPTWindow();
            chatGPTWindow.Show();
        }
    }

}
