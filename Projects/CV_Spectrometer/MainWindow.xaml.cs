using ColorVision.Common.MVVM;
using ColorVision.Themes;
using ColorVision.UI;
using ColorVision.UI.Menus;
using ColorVision.UI.Sorts;
using cvColorVision;
using log4net;
using Microsoft.Xaml.Behaviors;
using ScottPlot;
using ScottPlot.Plottables;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace CV_Spectrometer
{
    public class ScrollToEndBehavior : Behavior<TextBox>
    {
        protected override void OnAttached()
        {
            base.OnAttached();
            AssociatedObject.TextChanged += OnTextChanged;
        }

        protected override void OnDetaching()
        {
            base.OnDetaching();
            AssociatedObject.TextChanged -= OnTextChanged;
        }

        private void OnTextChanged(object sender, TextChangedEventArgs e)
        {
            AssociatedObject.SelectionStart = AssociatedObject.Text.Length;
            AssociatedObject.ScrollToEnd();
        }
    }
    public class MainWindowConfig : WindowConfig,IConfig
    {
        public static MainWindowConfig Instance => ConfigService.Instance.GetRequiredService<MainWindowConfig>();

        public ObservableCollection<GridViewColumnVisibility> GridViewColumnVisibilitys { get; set; } = new ObservableCollection<GridViewColumnVisibility>();
    }

    public class MainWindowResult : ViewModelBase, IConfig
    {
        public static MainWindowResult Instance => ConfigService.Instance.GetRequiredService<MainWindowResult>();
        public ObservableCollection<ViewResultSpectrum> ViewResultSpectrums { get; set; } = new ObservableCollection<ViewResultSpectrum>();



    }



    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : System.Windows.Window
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(App));

        public static ObservableCollection<ViewResultSpectrum> ViewResultSpectrums => MainWindowResult.Instance.ViewResultSpectrums;

        public static MainWindowConfig Config => MainWindowConfig.Instance;

        public MainWindow()
        {
            InitializeComponent();
            Config.SetWindow(this);
            this.SizeChanged += (s, e) => Config.SetConfig(this);
            this.ApplyCaption();
            this.Closed += (s, e) => ConfigService.Instance.SaveConfigs();

        }
        float fIntTime = 0;
        int testType = 0;
        int picType = 0;
        bool start = false;
        int testid = 0;



        public void addtable(COLOR_PARA data)
        {
            float[] cutdata = CutFplData(data);
            double Cie2015XData = CalCIE2015Data(localCIE2015Data.CIE2015_X, cutdata);
            double Cie2015YData = CalCIE2015Data(localCIE2015Data.CIE2015_Y, cutdata);
            double Cie2015ZData = CalCIE2015Data(localCIE2015Data.CIE2015_Z, cutdata);

            AddViewResultSpectrum(new ViewResultSpectrum(data));
        }

        //计算绝对光谱
        private double calcAbsSpdata(float fPL, float fPlambda)
        {
            double da = fPL * fPlambda / 1000;
            return da;
        }


        public double CalCIE2015Data(double[] Cie2015Source, float[] sp100Source) 
        {
            double reusltData = 0;
            if (sp100Source.Length== Cie2015Source.Length)
            {
                for (int i = 0; i < Cie2015Source.Length; i++)
                {
                    reusltData = reusltData + Cie2015Source[i] * sp100Source[i];
                }
                reusltData = reusltData * 683.2;
            }

            return reusltData;
        }

        public float[] CutFplData(COLOR_PARA data)
        {

            float[] part1Data = new float[]
                {
                           Convert.ToSingle(calcAbsSpdata(data.fPL[10], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[11], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[12], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[13], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[14], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[15], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[16], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[17], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[18], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[19], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[20], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[21], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[22], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[23], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[24], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[25], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[26], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[27], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[28], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[29], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[30], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[31], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[32], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[33], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[34], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[35], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[36], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[37], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[38], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[39], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[40], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[41], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[42], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[43], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[44], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[45], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[46], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[47], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[48], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[49], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[50], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[51], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[52], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[53], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[54], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[55], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[56], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[57], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[58], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[59], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[60], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[61], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[62], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[63], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[64], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[65], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[66], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[67], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[68], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[69], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[70], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[71], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[72], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[73], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[74], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[75], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[76], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[77], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[78], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[79], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[80], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[81], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[82], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[83], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[84], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[85], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[86], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[87], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[88], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[89], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[90], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[91], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[92], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[93], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[94], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[95], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[96], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[97], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[98], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[99], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[100], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[101], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[102], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[103], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[104], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[105], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[106], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[107], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[108], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[109], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[110], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[111], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[112], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[113], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[114], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[115], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[116], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[117], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[118], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[119], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[120], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[121], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[122], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[123], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[124], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[125], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[126], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[127], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[128], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[129], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[130], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[131], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[132], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[133], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[134], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[135], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[136], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[137], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[138], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[139], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[140], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[141], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[142], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[143], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[144], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[145], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[146], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[147], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[148], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[149], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[150], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[151], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[152], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[153], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[154], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[155], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[156], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[157], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[158], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[159], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[160], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[161], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[162], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[163], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[164], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[165], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[166], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[167], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[168], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[169], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[170], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[171], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[172], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[173], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[174], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[175], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[176], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[177], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[178], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[179], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[180], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[181], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[182], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[183], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[184], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[185], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[186], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[187], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[188], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[189], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[190], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[191], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[192], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[193], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[194], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[195], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[196], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[197], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[198], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[199], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[200], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[201], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[202], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[203], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[204], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[205], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[206], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[207], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[208], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[209], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[210], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[211], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[212], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[213], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[214], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[215], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[216], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[217], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[218], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[219], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[220], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[221], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[222], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[223], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[224], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[225], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[226], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[227], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[228], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[229], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[230], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[231], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[232], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[233], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[234], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[235], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[236], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[237], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[238], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[239], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[240], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[241], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[242], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[243], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[244], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[245], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[246], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[247], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[248], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[249], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[250], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[251], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[252], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[253], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[254], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[255], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[256], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[257], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[258], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[259], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[260], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[261], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[262], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[263], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[264], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[265], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[266], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[267], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[268], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[269], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[270], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[271], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[272], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[273], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[274], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[275], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[276], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[277], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[278], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[279], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[280], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[281], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[282], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[283], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[284], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[285], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[286], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[287], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[288], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[289], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[290], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[291], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[292], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[293], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[294], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[295], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[296], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[297], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[298], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[299], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[300], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[301], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[302], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[303], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[304], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[305], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[306], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[307], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[308], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[309], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[310], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[311], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[312], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[313], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[314], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[315], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[316], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[317], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[318], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[319], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[320], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[321], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[322], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[323], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[324], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[325], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[326], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[327], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[328], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[329], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[330], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[331], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[332], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[333], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[334], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[335], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[336], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[337], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[338], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[339], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[340], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[341], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[342], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[343], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[344], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[345], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[346], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[347], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[348], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[349], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[350], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[351], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[352], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[353], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[354], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[355], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[356], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[357], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[358], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[359], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[360], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[361], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[362], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[363], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[364], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[365], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[366], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[367], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[368], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[369], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[370], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[371], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[372], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[373], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[374], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[375], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[376], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[377], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[378], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[379], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[380], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[381], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[382], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[383], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[384], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[385], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[386], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[387], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[388], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[389], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[390], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[391], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[392], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[393], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[394], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[395], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[396], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[397], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[398], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[399], data.fPlambda)),
                           Convert.ToSingle(calcAbsSpdata(data.fPL[400], data.fPlambda)),
            };
            float[] part2Data = new float[50];
            float[] cutData = new float[part1Data.Length+ part2Data.Length];
            for (int i = 0; i < part2Data.Length; i++)
            {
                part2Data[i] = 0;
            }
            Array.Copy(part1Data, 0, cutData, 0, part1Data.Length);
            Array.Copy(part2Data, 0, cutData, part1Data.Length, part2Data.Length);

            return cutData;
        }

        public IntPtr SpectrometerHandle => Manager.Handle;

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Manager.Handle = Spectrometer.CM_CreateEmission(0);

                int ncom = int.Parse(Manager.SzComName.Replace("COM", ""));
                int iR = Spectrometer.CM_Emission_Init(SpectrometerHandle, ncom, Manager.BaudRate);
                log.Info($"CM_Emission_Init:{iR}");
                if (iR == 1)
                {
                    Manager.IsConnected = true;
                    iR = Spectrometer.CM_Emission_LoadWavaLengthFile(SpectrometerHandle, Manager.WavelengthFile);
                    log.Info($"CM_Emission_LoadWavaLengthFile{Manager.WavelengthFile},ret{iR}");
                    iR = Spectrometer.CM_Emission_LoadMagiudeFile(SpectrometerHandle, Manager.MaguideFile);
                    log.Info($"CM_Emission_LoadMagiudeFile{Manager.MaguideFile},ret{iR}");

                    log.Info($"CM_SetEmissionSP100:IsEnabled{SetEmissionSP100Config.Instance.IsEnabled},nStartPos{SetEmissionSP100Config.Instance.nStartPos},nEndPos{SetEmissionSP100Config.Instance.nEndPos},dMeanThreshold{SetEmissionSP100Config.Instance.dMeanThreshold}");
                    int ret = Spectrometer.CM_SetEmissionSP100(SpectrometerHandle, SetEmissionSP100Config.Instance.IsEnabled, SetEmissionSP100Config.Instance.nStartPos, SetEmissionSP100Config.Instance.nEndPos, SetEmissionSP100Config.Instance.dMeanThreshold);
                    log.Info($"CM_SetEmissionSP100 ret:{ret}");

                    State2.Text = CV_Spectrometer.Properties.Resources.连接成功;
                    State4.Text = "SP-100";
                    button3.IsEnabled = true;
                    button4.IsEnabled = true;
                    button5.IsEnabled = true;
                    button6.IsEnabled = true;
                }
                else
                {
                    Manager.IsConnected = false;
                    MessageBox.Show(CV_Spectrometer.Properties.Resources.连接失败);
                }
            }
            catch(Exception ex)
            {
                log.Error(ex);
                MessageBox.Show(ex.Message);
            }

        }
        int ret;

        //断开连接
        private void Button8_Click(object sender, RoutedEventArgs e)
        {
            testid = 0;
            ret = Manager.Disconnect();
            if (ret == 1)
            {
                MessageBox.Show(CV_Spectrometer.Properties.Resources.已成功断开连接);
                State2.Text = CV_Spectrometer.Properties.Resources.未连接;
                State4.Text = CV_Spectrometer.Properties.Resources.未连接;
            }
        }
           
        //单次校零
        private void Button3_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                int iR = Spectrometer.CM_Emission_DarkStorage(SpectrometerHandle, Manager.IntTime, Manager.Average, 0, Manager.fDarkData);
                if (iR == 1)
                {
                    MessageBox.Show("校零成功");
                }
                else
                {
                    MessageBox.Show("校零失败");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("校零异常" + ex.Message);
            }
        }
        //处理测量数据
        public void TestResult(COLOR_PARA data, float intTime, int resultCode)
        {
            if (resultCode == 1)
            {
                if (data.fPh < 1)
                {
                    data.fPh = (float)Math.Round((float)data.fPh, 4);
                }
                else
                {
                    data.fPh = (float)Math.Round((float)data.fPh, 2);
                }
                Application.Current.Dispatcher.Invoke(() =>
                {
                    addtable(data);
                });
            }
            else
            {
                MessageBox.Show("结果错误");
            }
        }




        public void Measure()
        {
            if (Manager.EnableAutoIntegration)
            {
                ret = Spectrometer.CM_Emission_GetAutoTimeEx(SpectrometerHandle, ref fIntTime, Manager.IntTimeConfig.IntLimitTime, Manager.IntTimeConfig.AutoIntTimeB, Manager.Max);
                if (ret == 1)
                {
                    Manager.IntTime = fIntTime;
                }
                else
                {
                    MessageBox.Show("自动积分获取失败：" + ret);
                    return;
                }
            }
            if (Manager.EnableAutodark)
            {
                ret = Spectrometer.CM_Emission_AutoDarkStorage(SpectrometerHandle, Manager.IntTime, Manager.Average, 0, Manager.fDarkData);
                if (ret == 0)
                {
                    isstartAuto = false;
                    MessageBox.Show("请先做一次自适应校零");
                    return;
                }
            }

            float fDx = 0;
            float fDy = 0;
            COLOR_PARA cOLOR_PARA = new COLOR_PARA();
            ret = Spectrometer.CM_Emission_GetData(SpectrometerHandle, 0, Manager.IntTime, Manager.Average, Manager.GetDataConfig.FilterBW, Manager.fDarkData, fDx, fDy, Manager.GetDataConfig.SetWL1, Manager.GetDataConfig.SetWL2, ref cOLOR_PARA);
            TestResult(cOLOR_PARA, Manager.IntTime, ret);
        }

        //单次测量
        private void Button5_Click(object sender, RoutedEventArgs e)
        {
            Measure();
        }
        //自适应校零
        private void Button4_Click_1(object sender, RoutedEventArgs e)
        {
            Task.Run(() =>
            {
                ret = Spectrometer.CM_Emission_Init_Auto_Dark(SpectrometerHandle, Manager.fTimeStart, Manager.nStepTime, Manager.nStepCount, Manager.Average);
                if (ret == 1)
                {
                    MessageBox.Show("自适应校零成功");
                }
                else
                {
                    MessageBox.Show("自适应校零失败");
                }
            });
        }
        //绝对光谱校正
        private void juedui_Click(object sender, RoutedEventArgs e)
        {
            SpectralCorrection spec = new SpectralCorrection(fIntTime, Manager.Average, Manager.GetDataConfig.FilterBW, Manager.EnableAutoIntegration, Manager.IntLimitTime, Manager.IntTimeConfig.AutoIntTimeB);
            spec.ShowDialog();
        }


        //连续测量
        private void Button6_Click(object sender, RoutedEventArgs e)
        {
            isstartAuto = true;
            button6.Visibility = Visibility.Collapsed;
            button7.Visibility = Visibility.Visible;
            Task.Run(()=> LoopMeasure());
        }
        public async void LoopMeasure()
        {
            while (isstartAuto)
            {
                if (Manager.MeasurementNum > 0)
                {
                    if (Manager.LoopMeasureNum >= Manager.MeasurementNum)
                    {
                        isstartAuto=false;
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            button6.Visibility = Visibility.Visible;
                            button7.Visibility = Visibility.Collapsed;
                            Manager.LoopMeasureNum = 0;
                            MessageBox.Show(Application.Current.MainWindow,"连续测试执行完毕");
                        });
                        break;
                    }
                    Manager.LoopMeasureNum++;
                }
                Measure();
                await Task.Delay(Manager.MeasurementInterval);
            }

        }

        //停止连续测量
        private void Button7_Click(object sender, RoutedEventArgs e)
        {
            isstartAuto = false;
            button6.Visibility = Visibility.Visible;
            button7.Visibility = Visibility.Collapsed;
            Manager.LoopMeasureNum = 0;
        }
        //清空数据
        private void Cleartable_Click(object sender, RoutedEventArgs e)
        {
            ViewResultSpectrums.Clear();
            ScatterPlots.Clear();
            listView2.ItemsSource = new ObservableCollection<SpectralData>();
            if (ViewResultSpectrums.Count > 0)
            {
                ViewResultList.SelectedIndex = 0;
            }
            else
            {
                wpfplot1.Plot.Clear();
                wpfplot1.Refresh();
            }
            ReDrawPlot();
        }
        //导出data数据至excel
        private void Export_Click(object sender, RoutedEventArgs e)
        {

            using (var dialog = new System.Windows.Forms.SaveFileDialog())
            {
                dialog.Filter = "CSV files (*.csv) | *.csv";
                dialog.FileName = DateTime.Now.ToString("光谱仪导出yyyy-MM-dd-HH-mm-ss");
                dialog.RestoreDirectory = true;
                if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;


                var csvBuilder = new StringBuilder();

                List<string> properties = new List<string>();
                properties.Add("序号");
                properties.Add("IP");
                properties.Add("亮度Lv(cd/m2)");
                properties.Add("蓝光");
                properties.Add("色度x");
                properties.Add("色度y");
                properties.Add("色度u");
                properties.Add("色度v");
                properties.Add("相关色温(K)");
                properties.Add("主波长Ld(nm)");
                properties.Add("色纯度(%)");
                properties.Add("峰值波长Lp(nm");
                properties.Add("显色性指数Ra");
                properties.Add("半波宽");

                for (int i = 380; i <= 780; i++)
                {
                    properties.Add(i.ToString());
                }
                for (int i = 380; i <= 780; i++)
                {
                    properties.Add("sp" + i.ToString());
                }
                // 写入列头
                for (int i = 0; i < properties.Count; i++)
                {
                    // 添加列名
                    csvBuilder.Append(properties[i]);

                    // 如果不是最后一列，则添加逗号
                    if (i < properties.Count - 1)
                        csvBuilder.Append(',');
                }
                // 添加换行符
                csvBuilder.AppendLine();

                var selectedItemsCopy = new List<object>();
                foreach (var item in ViewResultSpectrums)
                {
                    selectedItemsCopy.Add(item);
                }

                foreach (var item in selectedItemsCopy)
                {
                    if (item is ViewResultSpectrum result)
                    {
                        csvBuilder.Append(result.Id + ",");
                        csvBuilder.Append(result.IP + ",");
                        csvBuilder.Append(result.Lv + ",");
                        csvBuilder.Append(result.Blue + ",");
                        csvBuilder.Append(result.fx + ",");
                        csvBuilder.Append(result.fy + ",");
                        csvBuilder.Append(result.fu + ",");
                        csvBuilder.Append(result.fv + ",");
                        csvBuilder.Append(result.fCCT + ",");
                        csvBuilder.Append(result.fLd + ",");
                        csvBuilder.Append(result.fPur + ",");
                        csvBuilder.Append(result.fLp + ",");
                        csvBuilder.Append(result.fRa + ",");
                        csvBuilder.Append(result.fHW + ",");

                        for (int i = 0; i < result.SpectralDatas.Count; i++)
                        {
                            csvBuilder.Append(result.SpectralDatas[i].AbsoluteSpectrum);
                            csvBuilder.Append(',');
                        }
                        for (int i = 0; i < result.SpectralDatas.Count; i++)
                        {
                            csvBuilder.Append(result.SpectralDatas[i].RelativeSpectrum);
                            if (i < result.SpectralDatas.Count - 1)
                                csvBuilder.Append(',');
                        }
                        csvBuilder.AppendLine();
                    }
                }
                File.WriteAllText(dialog.FileName, csvBuilder.ToString(), Encoding.UTF8);

            };



        }

        bool isstartAuto;

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
        }

        private void GenerateAmplitude_Click(object sender, RoutedEventArgs e)
        {
            new GenerateAmplitudeWindow(SpectrometerHandle).ShowDialog();
        }
        public SpectrometerManager Manager => SpectrometerManager.Instance;


        private void Window_Initialized(object sender, EventArgs e)
        {
            MenuManager.GetInstance().Menu = menu;
            MenuManager.GetInstance().LoadMenuItemFromAssembly();
            ComboBoxSpectrometerType.ItemsSource = from e1 in Enum.GetValues(typeof(SpectrometerType)).Cast<SpectrometerType>()
                                                   select new KeyValuePair<SpectrometerType, string>(e1, e1.ToString());

            SetEmissionSP100Config.Instance.EditChanged += (s, e) =>
            {
                if (SpectrometerHandle != IntPtr.Zero)
                {
                    log.Info($"CM_SetEmissionSP100:IsEnabled{SetEmissionSP100Config.Instance.IsEnabled},nStartPos{SetEmissionSP100Config.Instance.nStartPos},nEndPos{SetEmissionSP100Config.Instance.nEndPos},dMeanThreshold{SetEmissionSP100Config.Instance.dMeanThreshold}");
                    int ret = Spectrometer.CM_SetEmissionSP100(SpectrometerHandle, SetEmissionSP100Config.Instance.IsEnabled, SetEmissionSP100Config.Instance.nStartPos, SetEmissionSP100Config.Instance.nEndPos, SetEmissionSP100Config.Instance.dMeanThreshold);
                    log.Info($"CM_SetEmissionSP100 ret:{ret}");
                }

            };

            List<int> BaudRates = new List<int>() { 115200, 38400, 9600, 300, 600, 1200, 2400, 4800, 14400, 19200, 57600 };
            List<string> Serials = new  List<string>() { "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9", "COM10" };
            ComboBoxPort.ItemsSource = BaudRates;
            ComboBoxSerial.ItemsSource = Serials;

            string title = "相对光谱曲线";
            wpfplot1.Plot.XLabel("波长[nm]");
            wpfplot1.Plot.YLabel("相对光谱");
            wpfplot1.Plot.Axes.Title.Label.Text = title;
            wpfplot1.Plot.Axes.Title.Label.FontName = Fonts.Detect(title);
            wpfplot1.Plot.Axes.Title.Label.Text = title;
            wpfplot1.Plot.Axes.Left.Label.FontName = Fonts.Detect(title);
            wpfplot1.Plot.Axes.Bottom.Label.FontName = Fonts.Detect(title);

            wpfplot1.Plot.Axes.SetLimitsX(380, 780);
            wpfplot1.Plot.Axes.SetLimitsY(0, 1);
            wpfplot1.Plot.Axes.Bottom.Min = 370;
            wpfplot1.Plot.Axes.Bottom.Max = 1000;
            wpfplot1.Plot.Axes.Left.Min = 0;
            wpfplot1.Plot.Axes.Left.Max = 1;

            if (ViewResultSpectrums.Count != 0)
            {
                foreach (var item in ViewResultSpectrums)
                {
                    item.Gen();
                    ScatterPlots.Add(item.ScatterPlot);
                }

            }

            ViewResultList.ItemsSource = ViewResultSpectrums;

            if (ViewResultList.View is GridView gridView)
            {
                GridViewColumnVisibility.AddGridViewColumn(gridView.Columns, GridViewColumnVisibilitys);
                Config.GridViewColumnVisibilitys.CopyToGridView(GridViewColumnVisibilitys);
                Config.GridViewColumnVisibilitys = GridViewColumnVisibilitys;
                GridViewColumnVisibility.AdjustGridViewColumnAuto(gridView.Columns, GridViewColumnVisibilitys);
            }
            this.DataContext = Manager;
        }



        private ObservableCollection<GridViewColumnVisibility> GridViewColumnVisibilitys { get; set; } = new ObservableCollection<GridViewColumnVisibility>();

        private void ContextMenu_Opened(object sender, RoutedEventArgs e)
        {
            if (sender is ContextMenu contextMenu && contextMenu.Items.Count == 0 && ViewResultList.View is GridView gridView)
                GridViewColumnVisibility.GenContentMenuGridViewColumn(contextMenu, gridView.Columns, GridViewColumnVisibilitys);
        }
        private void GridViewColumnSort(object sender, RoutedEventArgs e)
        {
            if (sender is GridViewColumnHeader gridViewColumnHeader && gridViewColumnHeader.Content != null)
            {
                Type type = typeof(ViewResultSpectrum);

                var properties = type.GetProperties();
                foreach (var property in properties)
                {
                    var attribute = property.GetCustomAttribute<DisplayNameAttribute>();
                    if (attribute != null)
                    {
                        string displayName = attribute.DisplayName;
                        displayName = Properties.Resources.ResourceManager?.GetString(displayName, Thread.CurrentThread.CurrentUICulture) ?? displayName;
                        if (displayName == gridViewColumnHeader.Content.ToString())
                        {
                            var item = GridViewColumnVisibilitys.FirstOrDefault(x => x.ColumnName.ToString() == displayName);
                            if (item != null)
                            {
                                item.IsSortD = !item.IsSortD;
                                ViewResultSpectrums.SortByProperty(property.Name, item.IsSortD);
                            }
                        }
                    }
                }
            }
        }

        private List<Scatter> ScatterPlots { get; set; } = new List<Scatter>();

        bool MulComparison;
        Scatter? LastMulSelectComparsion;

        private void DrawPlot()
        {
            if (ViewResultList.SelectedIndex < 0) return;
            wpfplot1.Plot.Axes.SetLimitsX(380, 780);
            wpfplot1.Plot.Axes.SetLimitsY(0, 1);
            wpfplot1.Plot.Axes.Bottom.Min = ViewResultSpectrums[ViewResultList.SelectedIndex].fSpect1;
            wpfplot1.Plot.Axes.Bottom.Max = ViewResultSpectrums[ViewResultList.SelectedIndex].fSpect2;
            wpfplot1.Plot.Axes.Left.Min = 0;
            wpfplot1.Plot.Axes.Left.Max = 1;

            if (ScatterPlots.Count > 0)
            {
                if (MulComparison)
                {
                    if (LastMulSelectComparsion != null)
                    {
                        LastMulSelectComparsion.Color = Color.FromColor(System.Drawing.Color.DarkGoldenrod);
                        LastMulSelectComparsion.LineWidth = 1;
                        LastMulSelectComparsion.MarkerSize = 1;
                    }

                    LastMulSelectComparsion = ScatterPlots[ViewResultList.SelectedIndex];
                    LastMulSelectComparsion.LineWidth = 3;
                    LastMulSelectComparsion.MarkerSize = 3;
                    LastMulSelectComparsion.Color = Color.FromColor(System.Drawing.Color.Red);
                    wpfplot1.Plot.PlottableList.Add(LastMulSelectComparsion);

                }
                else
                {
                    var temp = ScatterPlots[ViewResultList.SelectedIndex];
                    temp.Color = Color.FromColor(System.Drawing.Color.DarkGoldenrod);
                    temp.LineWidth = 1;
                    temp.MarkerSize = 1;

                    wpfplot1.Plot.PlottableList.Add(temp);
                    wpfplot1.Plot.Remove(LastMulSelectComparsion);
                    LastMulSelectComparsion = temp;

                }
            }

            wpfplot1.Refresh();
        }

        private void ReDrawPlot()
        {
            if (ViewResultList.SelectedIndex < 0) return;

            wpfplot1.Plot.Clear();

            LastMulSelectComparsion = null;
            if (MulComparison)
            {
                ViewResultList.SelectedIndex = ViewResultList.Items.Count > 0 && ViewResultList.SelectedIndex == -1 ? 0 : ViewResultList.SelectedIndex;
                for (int i = 0; i < ViewResultSpectrums.Count; i++)
                {
                    if (i == ViewResultList.SelectedIndex)
                        continue;
                    var plot = ScatterPlots[i];
                    plot.Color = Color.FromColor(System.Drawing.Color.DarkGoldenrod);
                    plot.LineWidth = 1;
                    plot.MarkerSize = 1;

                    wpfplot1.Plot.PlottableList.Add(plot);
                }
            }
            DrawPlot();
        }

        public void AddViewResultSpectrum(ViewResultSpectrum viewResultSpectrum)
        {
            ViewResultSpectrums.Add(viewResultSpectrum);
            ScatterPlots.Add(viewResultSpectrum.ScatterPlot);
            ViewResultList.SelectedIndex = ViewResultSpectrums.Count - 1;
            ViewResultList.ScrollIntoView(viewResultSpectrum);
        }

        private void listView1_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ListView listview && listview.SelectedIndex > -1)
            {
                DrawPlot();
                listView2.ItemsSource = ViewResultSpectrums[listview.SelectedIndex].SpectralDatas;
            }
        }

        private void listView1_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete && ViewResultList.SelectedIndex > -1)
            {
                int temp = ViewResultList.SelectedIndex;
                ViewResultSpectrums.RemoveAt(ViewResultList.SelectedIndex);

                if (ViewResultList.Items.Count > 0)
                {
                    ViewResultList.SelectedIndex = temp - 1; ;
                    DrawPlot();
                }
                else
                {
                    wpfplot1.Plot.Clear();
                    wpfplot1.Refresh();
                }

            }
        }

        Marker markerPlot1;
        private void listView2_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            wpfplot1.Plot.Remove(markerPlot1);
            if (listView2.SelectedIndex > -1)
            {
                markerPlot1 = new Marker
                {
                    X = listView2.SelectedIndex + 380,
                    Y = ViewResultSpectrums[ViewResultList.SelectedIndex].fPL[listView2.SelectedIndex * 10],
                    MarkerShape = MarkerShape.FilledCircle,
                    MarkerSize = 10f,
                    Color = Color.FromColor(System.Drawing.Color.Orange),
                };
                wpfplot1.Plot.PlottableList.Add(markerPlot1);
            }
            wpfplot1.Refresh();
        }

        private void ContextMenu1_Opened(object sender, RoutedEventArgs e)
        {

        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            log.Info($"CM_SetEmissionSP100:IsEnabled{SetEmissionSP100Config.Instance.IsEnabled},nStartPos{SetEmissionSP100Config.Instance.nStartPos},nEndPos{SetEmissionSP100Config.Instance.nEndPos},dMeanThreshold{SetEmissionSP100Config.Instance.dMeanThreshold}");
            int ret = Spectrometer.CM_SetEmissionSP100(SpectrometerHandle, SetEmissionSP100Config.Instance.IsEnabled, SetEmissionSP100Config.Instance.nStartPos, SetEmissionSP100Config.Instance.nEndPos, SetEmissionSP100Config.Instance.dMeanThreshold);
            log.Info($"CM_SetEmissionSP100 ret:{ret}");
            string a = ret != 1 ? "失败" : "成功";
            MessageBox.Show("CM_SetEmissionSP100："  + a );
        }

        private void GridViewColumnSort1(object sender, RoutedEventArgs e)
        {

        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            ConfigService.Instance.SaveConfigs();
        }

        private void GridSplitter_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {
            ViewResultList.Height = ListRow2.ActualHeight - 32;
            ListRow2.Height = GridLength.Auto;
            ListRow1.Height = new GridLength(1, GridUnitType.Star);
        }

        private void ButtonMul_Click(object sender, RoutedEventArgs e)
        {
            MulComparison = !MulComparison;
            if (ViewResultList.SelectedIndex <= -1)
            {
                if (ViewResultList.Items.Count == 0)
                    return;
                ViewResultList.SelectedIndex = 0;
            }
            ReDrawPlot();
        }
    }


}
