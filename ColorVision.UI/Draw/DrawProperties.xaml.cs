#pragma warning disable CS8604, CS8605
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;

namespace ColorVision.UI.Draw
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
            ComboBoxBrush.ItemsSource = from Brushes in typeof(Brushes).GetProperties()
                                        select new KeyValuePair<Brush, string>((Brush)Brushes.GetValue(null), Brushes.Name);
            ComboBoxBrush.SelectedValuePath = "Key";
            ComboBoxBrush.DisplayMemberPath = "Value";

            ComboBoxFontFamily.ItemsSource = from FontFamily in Fonts.SystemFontFamilies
                                             select new KeyValuePair<FontFamily, string>(FontFamily, FontFamily.FamilyNames.TryGetValue(XmlLanguage.GetLanguage(CultureInfo.CurrentCulture.Name), out string fontName)? fontName: FontFamily.ToString());
            ComboBoxFontFamily.SelectedValuePath = "Key";

            ComboBoxFontWeight.ItemsSource = from FontWeight in typeof(FontWeights).GetProperties()
                                             select new KeyValuePair<FontWeight, string>((FontWeight)FontWeight.GetValue(null), FontWeight.Name);
            ComboBoxFontWeight.SelectedValuePath = "Key";

            ComboBoxFontStyle.ItemsSource = from FontStyle in typeof(FontStyles).GetProperties()
                                            select new KeyValuePair<FontStyle, string>((FontStyle)FontStyle.GetValue(null), FontStyle.Name);
            ComboBoxFontStyle.SelectedValuePath = "Key";


            ComboBoxFlowDirection.ItemsSource = from FlowDirection in Enum.GetValues(typeof(FlowDirection)).Cast<FlowDirection>()
                                                select new KeyValuePair<FlowDirection, string>(FlowDirection,FlowDirection.ToString());
            ComboBoxFlowDirection.SelectedValuePath = "Key";

            ComboBoxFontStretch.ItemsSource = from FontStretch in typeof(FontStretches).GetProperties()
                                               select new KeyValuePair<FontStretch, string>((FontStretch)FontStretch.GetValue(null), FontStretch.Name);
            ComboBoxFontStretch.SelectedValuePath = "Key";



            DataContext = DefalutTextAttribute.Defalut;

            Resources = null;
        }

        private void ComboBoxBrush_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {            
            if (ComboBoxBrush.SelectedIndex > -1 && ComboBoxBrush.SelectedValue is SolidColorBrush solidColorBrush)
            {
                ColorPicker1.SelectedBrush = solidColorBrush;
            }
        }

        private void ComboBoxFontFamily_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ComboBoxFontFamily.SelectedIndex > -1)
            {
                //MessageBox.IsShow("!");
            }
        }

        private void ComboBoxFontStyle_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }

        private void ComboBoxFlowDirection_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }

        private void ComboBoxFontStretch_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }

        private void ColorPicker_SelectedColorChanged(object sender, HandyControl.Data.FunctionEventArgs<Color> e)
        {
            DefalutTextAttribute.Defalut.Brush = ColorPicker1.SelectedBrush;
        }
    }
}
