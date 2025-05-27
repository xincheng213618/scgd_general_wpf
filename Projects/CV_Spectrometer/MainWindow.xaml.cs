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
    /// MarkdownViewWindow.xaml 的交互逻辑
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


        public void AddTable(COLOR_PARA data)
        {
            float[] cutData = CutFplData(data);
            double cie2015XData = CalCIE2015Data(localCIE2015Data.CIE2015_X, cutData);
            double cie2015YData = CalCIE2015Data(localCIE2015Data.CIE2015_Y, cutData);
            double cie2015ZData = CalCIE2015Data(localCIE2015Data.CIE2015_Z, cutData);

            AddViewResultSpectrum(new ViewResultSpectrum(data));
        }

        // 计算绝对光谱
        private double CalcAbsSpData(float fPL, float fPlambda)
        {
            return fPL * fPlambda / 1000.0;
        }

        public double CalCIE2015Data(double[] cie2015Source, float[] sp100Source)
        {
            double resultData = 0;
            if (sp100Source.Length == cie2015Source.Length)
            {
                for (int i = 0; i < cie2015Source.Length; i++)
                {
                    resultData += cie2015Source[i] * sp100Source[i];
                }
                resultData *= 683.2;
            }
            return resultData;
        }

        public float[] CutFplData(COLOR_PARA data)
        {
            int part1Length = 401; // 10 to 400 inclusive
            float[] part1Data = new float[part1Length];

            for (int i = 0; i < part1Length; i++)
            {
                part1Data[i] = Convert.ToSingle(CalcAbsSpData(data.fPL[i + 10], data.fPlambda));
            }

            float[] part2Data = new float[50]; // Already initialized to 0
            float[] cutData = new float[part1Data.Length + part2Data.Length];

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
                    AddTable(data);
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
                float fIntTime = 0;
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
            SpectralCorrection spec = new SpectralCorrection(Manager.IntTime, Manager.Average, Manager.GetDataConfig.FilterBW, Manager.EnableAutoIntegration, Manager.IntLimitTime, Manager.IntTimeConfig.AutoIntTimeB);
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
