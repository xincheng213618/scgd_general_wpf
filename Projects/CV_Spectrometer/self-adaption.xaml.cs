using System.Windows;

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
