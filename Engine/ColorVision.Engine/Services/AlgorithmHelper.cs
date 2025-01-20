#pragma warning disable CS8604,CS0168,CS8629,CA1822,CS8602
using ColorVision.Themes.Controls;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Engine.Services
{
    public static class AlgorithmHelper
    {
        public static bool IsTemplateSelected(ComboBox comboBox, string errorMessage)
        {
            if (comboBox.SelectedIndex == -1)
            {
                MessageBox1.Show(Application.Current.GetActiveWindow(), errorMessage, "ColorVision");
                return false;
            }
            return true;
        }
    }
}
