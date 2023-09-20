using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace ColorVision.Draw
{
    /// <summary>
    /// DrawProperties.xaml 的交互逻辑
    /// </summary>
    public partial class DrawProperties : Window
    {
        public DrawProperties()
        {
            InitializeComponent();
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            ComboBoxBrush.ItemsSource = typeof(Brushes).GetProperties();
            ComboBoxFontFamily.ItemsSource = from FontFamily in Fonts.SystemFontFamilies
                                             select new KeyValuePair<FontFamily, string>(FontFamily, FontFamily.FamilyNames.TryGetValue(XmlLanguage.GetLanguage(CultureInfo.CurrentCulture.Name), out string fontName)? fontName: FontFamily.ToString());
            ComboBoxFontFamily.SelectedValuePath = "Key";
            StackPanelTextAttribute.DataContext = DefalutTextAttribute.Defalut;
        }

        private void ComboBoxBrush_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {            
            if (ComboBoxBrush.SelectedIndex > -1)
            {
                MessageBox.Show("!");
            }
        }

        private void ComboBoxFontFamily_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ComboBoxFontFamily.SelectedIndex > -1)
            {
                //MessageBox.Show("!");
            }
        }
    }
}
