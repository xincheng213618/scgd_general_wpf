
using ColorVision.UI;
using System.Windows;
using System.Windows.Controls;

namespace SerialPlugin
{




    public class MyPlugin : IPlugin
    {
        public string Name => "SerialPlugin";

        public string Description => "Test";

        public void Execute()
        {
            MenuItem menuItem = new() { Header = "SerialPlugin" };
            menuItem.Click += (s, e) =>
            {
                MessageBox.Show("SerialPlugin");
            };

            MenuManager.GetInstance().AddMenuItem(menuItem);
        }
    }
    


}
