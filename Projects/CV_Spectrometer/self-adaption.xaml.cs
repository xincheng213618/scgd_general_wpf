using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace CV_Spectrometer
{
    /// <summary>
    /// self_adaption.xaml 的交互逻辑
    /// </summary>
    public partial class self_adaption : Window
    {
        //public string aver { get; set; }
        public class DARK_PARA
        {
            float fIntTime;
            int iAveNum;
            int iFilterBW;
            public float[] fDarkData;
        };
        public DARK_PARA [] m_dark;
        public self_adaption()
        {
            InitializeComponent();
        }
        public self_adaption(String aver)
        {
            InitializeComponent();
            textBox3.Text = aver;
        }
    }
}
