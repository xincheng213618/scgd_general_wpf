using ColorVision.Common.Utilities;
using ColorVision.Properties;
using ColorVision.Services.Dao;
using ColorVision.UI.Sorts;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ColorVision.Services.Templates
{
    /// <summary>
    /// CalibrationTemplate.xaml 的交互逻辑
    /// </summary>
    public partial class WindowTemplate : Window 
    {
        public ITemplate ITemplate { get; set; }

        private bool IsReLoad { get; set; }

        public WindowTemplate(ITemplate template, bool isReLoad = true)
        {
            ITemplate = template;
            IsReLoad = isReLoad;
            if (IsReLoad) template.Load();
            InitializeComponent();
        }


        private void Window_Initialized(object sender, EventArgs e)
        {
            this.DataContext = TemplateConfig.Instance;
            if (ITemplate.IsSideHide)
            {
                GridProperty.Visibility = Visibility.Collapsed;
                Grid.SetColumnSpan(TemplateGrid, 2);
                Grid.SetRowSpan(TemplateGrid, 1);
                Grid.SetColumnSpan(CreateGrid, 2);
                Grid.SetColumn(CreateGrid, 0);

                MinWidth = 350;
                Width = 350;
            }

            if (ITemplate.IsUserControl)
            {
                GridProperty.Children.Clear();
                GridProperty.Margin = new Thickness(5, 5, 5, 5);
                GridProperty.Children.Add(ITemplate.GetUserControl());
                Width = Width + 200;
            }

            Title = ITemplate.Title;
            ListView1.ItemsSource = ITemplate.ItemsSource;
            ListView1.SelectedIndex = 0;
            if (ListView1.View is GridView gridView)
            {
                GridViewColumnVisibility.AddGridViewColumn(gridView.Columns, GridViewColumnVisibilitys);
                TemplateConfig.Instance.GridViewColumnVisibilitys.CopyToGridView(GridViewColumnVisibilitys);
                TemplateConfig.Instance.GridViewColumnVisibilitys = GridViewColumnVisibilitys;
                GridViewColumnVisibility.AdjustGridViewColumnAuto(gridView.Columns, GridViewColumnVisibilitys);
            }
            Closed += WindowTemplate_Closed;

        }


        private void WindowTemplate_Closed(object? sender, EventArgs e)
        {
            ITemplate.Load();
        }

        private void ListView1_PreviewMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is ListView listView && listView.SelectedIndex > -1)
            {
                ITemplate.PreviewMouseDoubleClick(listView.SelectedIndex);
            }
        }
        private MeasureMasterDao measureMaster = new();
        private MeasureDetailDao measureDetail = new();

        private void ListView1_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ListView listView && listView.SelectedIndex > -1)
            {
                if (ITemplate.IsUserControl)
                {
                    ITemplate.SetUserControlDataContext(listView.SelectedIndex);
                }
                else
                {
                    PropertyGrid1.SelectedObject = ITemplate.GetValue(listView.SelectedIndex);
                }

                //if (UserControl is MeasureParamControl mpc && MeasureParam.Params[listView.SelectedIndex].Value is MeasureParam mp)
                //{
                //    mpc.MasterID = mp.Id;
                //    List<MeasureDetailModel> des = measureDetail.GetAllByPid(mp.Id); 
                //    mpc.Reload(des);
                //    mpc.ModTypeConfigs.Clear();
                //    mpc.ModTypeConfigs.Add(new MParamConfig(-1,"关注点","POI"));
                //    List<SysModMasterModel> sysModMaster = SysModMasterDao.Instance.GetAllById(UserConfig.Instance.TenantId);
                //    foreach (SysModMasterModel Model in sysModMaster)
                //    {
                //        mpc.ModTypeConfigs.Add(new MParamConfig(Model));
                //    }
                //}
            }
        }


        private void Button_Save_Click(object sender, RoutedEventArgs e)
        {
            ITemplate.Save();
            Close();
        }

        private void Button_New_Click(object sender, RoutedEventArgs e)
        {
            int oldnum = ITemplate.Count;
            CreateTemplate createWindow = new CreateTemplate(ITemplate) { Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner };
            createWindow.ShowDialog();
            if (oldnum!= ITemplate.Count)
            {
                ListView1.SelectedIndex= ITemplate.Count-1;
                if (ListView1.View is GridView gridView)
                    GridViewColumnVisibility.AdjustGridViewColumnAuto(gridView.Columns, GridViewColumnVisibilitys);

            }
        }


        private void Button_Del_Click(object sender, RoutedEventArgs e)
        {
            if (ListView1.SelectedIndex > -1)
            {
                if (MessageBox.Show(Application.Current.GetActiveWindow(), $"是否删除{ITemplate.Code}模板{ITemplate.GetTemplateName(ListView1.SelectedIndex)},删除后无法恢复!", Application.Current.MainWindow.Title, MessageBoxButton.OKCancel, MessageBoxImage.Warning) == MessageBoxResult.OK)
                {
                    int index = ListView1.SelectedIndex;
                    ITemplate.Delete(ListView1.SelectedIndex);
                    if (index > ITemplate.Count)
                        index = ITemplate.Count - 1;
                    ListView1.SelectedIndex = index;
                }
            }
            else
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), "请先选择", "ColorVision");
            }
        }

        private void SCManipulationBoundaryFeedback(object sender, ManipulationBoundaryFeedbackEventArgs e)
        {
            e.Handled = true;
        }


        private void TextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox && textBox.Tag is TemplateModelBase templateModelBase)
            {
                templateModelBase.IsEditMode = false;
            }
        }

        private void TextBox_GotFocus(object sender, RoutedEventArgs e)
        {

        }

        private void TextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (sender is TextBox textBox && textBox.Tag is TemplateModelBase templateModelBase)
            {
                if (e.Key == Key.F2)
                {
                    templateModelBase.IsEditMode = true;
                }
                if (e.Key == Key.Escape || e.Key == Key.Enter)
                {
                    templateModelBase.IsEditMode = false;
                }
            }
        }

        private void TextBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is TextBox textBox && textBox.Tag is TemplateModelBase templateModelBase)
            {
                templateModelBase.IsEditMode = true;
            }
        }


        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is TemplateModelBase templateModelBase)  
            {
                templateModelBase.IsEditMode = true;
            }
        }

        private void Button_Export_Click(object sender, RoutedEventArgs e)
        {
            if (ListView1.SelectedIndex < 0)
            {
                MessageBox.Show("请选择您要导出的流程", "ColorVision", MessageBoxButton.OK, MessageBoxImage.None, MessageBoxResult.None, MessageBoxOptions.DefaultDesktopOnly);
                return;
            }

            ITemplate.Export(ListView1.SelectedIndex);

            //            System.Windows.Forms.SaveFileDialog ofd = new System.Windows.Forms.SaveFileDialog();
            //            ofd.DefaultExt = "stn";
            //            ofd.Filter = "*.stn|*.stn";
            //            ofd.AddExtension = false;
            //            ofd.RestoreDirectory = true;
            //            ofd.Title = "导出流程";
            //            ofd.FileName = FlowParam.Params[ListView1.SelectedIndex].Key;
            //            if (ofd.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
            //            Tool.Base64ToFile(FlowParam.Params[ListView1.SelectedIndex].Value.DataBase64, ofd.FileName);


        }

        private void Button_Import_Click(object sender, RoutedEventArgs e)
        {
            ITemplate.Import();

            //switch (TemplateType)
            //{
            //    case TemplateType.FlowParam:
            //        if (true)
            //        {
            //            System.Windows.Forms.OpenFileDialog ofd = new System.Windows.Forms.OpenFileDialog();
            //            ofd.Filter = "*.stn|*.stn";
            //            if (ofd.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
            //            string name = Path.GetFileNameWithoutExtension(ofd.FileName);
            //            FlowParam? flowParam = FlowParam.AddFlowParam(name);
            //            if (flowParam != null)
            //            {
            //                flowParam.DataBase64 = Tool.FileToBase64(ofd.FileName); ;
            //                CreateNewTemplate(FlowParam.Params, name, flowParam);

            //                TemplateControl.Save2DB(flowParam);
            //            }
            //            else MessageBox.Show("数据库创建流程模板失败", "ColorVision", MessageBoxButton.OK, MessageBoxImage.None, MessageBoxResult.None, MessageBoxOptions.DefaultDesktopOnly);
            //        }
            //        break;
            //    case TemplateType.MeasureParam:
            //        if (true)
            //        {
            //            System.Windows.Forms.OpenFileDialog ofd = new System.Windows.Forms.OpenFileDialog();
            //            ofd.Filter = "*.cfg|*.cfg";
            //            if (ofd.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
            //            string name = Path.GetFileNameWithoutExtension(ofd.FileName);
            //            MeasureParam? measureParam = JsonConvert.DeserializeObject<MeasureParam>(File.ReadAllText(ofd.FileName));         
            //            if (measureParam != null)
            //            {
            //                CreateNewTemplate(MeasureParam.Params, name, measureParam);
            //                TemplateControl.Save2DB(measureParam);
            //            }
            //            else MessageBox.Show("数据库创建流程模板失败", "ColorVision", MessageBoxButton.OK, MessageBoxImage.None, MessageBoxResult.None, MessageBoxOptions.DefaultDesktopOnly);
            //        }
            //        break;
            //    case TemplateType.Calibration:  
            //        if (true)
            //        {
            //        }
            //        break;
            //    case TemplateType.AoiParam:
            //        if (true)
            //        {
            //            System.Windows.Forms.OpenFileDialog ofd = new System.Windows.Forms.OpenFileDialog();
            //            ofd.Filter = "*.cfg|*.cfg";
            //            if (ofd.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
            //            string name = Path.GetFileNameWithoutExtension(ofd.FileName);
            //            AOIParam? aoiParam = JsonConvert.DeserializeObject<AOIParam>(File.ReadAllText(ofd.FileName));
            //            if (aoiParam != null)
            //            {
            //                CreateNewTemplate(TemplateControl.GetInstance().AoiParams, name, aoiParam);
            //                TemplateControl.Save2DB(aoiParam);
            //            }
            //            else MessageBox.Show("数据库创建流程模板失败", "ColorVision", MessageBoxButton.OK, MessageBoxImage.None, MessageBoxResult.None, MessageBoxOptions.DefaultDesktopOnly);
            //        }
            //        break;
            //    case TemplateType.PGParam:
            //        if (true)
            //        {
            //            System.Windows.Forms.OpenFileDialog ofd = new System.Windows.Forms.OpenFileDialog();
            //            ofd.Filter = "*.cfg|*.cfg";
            //            if (ofd.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
            //            string name = Path.GetFileNameWithoutExtension(ofd.FileName);
            //            PGParam? pGParam = JsonConvert.DeserializeObject<PGParam>(File.ReadAllText(ofd.FileName));
            //            if (pGParam != null)
            //            {
            //                CreateNewTemplate(PGParam.Params, name, pGParam);
            //                TemplateControl.Save2DB(pGParam);
            //            }
            //            else MessageBox.Show("数据库创建流程模板失败", "ColorVision", MessageBoxButton.OK, MessageBoxImage.None, MessageBoxResult.None, MessageBoxOptions.DefaultDesktopOnly);
            //        }
            //        break;
            //    case TemplateType.SMUParam:
            //        if (true)
            //        {
            //            System.Windows.Forms.OpenFileDialog ofd = new System.Windows.Forms.OpenFileDialog();
            //            ofd.Filter = "*.cfg|*.cfg";
            //            if (ofd.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
            //            string name = Path.GetFileNameWithoutExtension(ofd.FileName);
            //            SMUParam? sMUParam = JsonConvert.DeserializeObject<SMUParam>(File.ReadAllText(ofd.FileName));
            //            if (sMUParam != null)
            //            {
            //                CreateNewTemplate(SMUParam.Params, name, sMUParam);
            //                TemplateControl.Save2DB(sMUParam);
            //            }
            //            else MessageBox.Show("数据库创建源表模板失败", "ColorVision", MessageBoxButton.OK, MessageBoxImage.None, MessageBoxResult.None, MessageBoxOptions.DefaultDesktopOnly);
            //        }
            //        break;  
            //    case TemplateType.PoiParam:
            //        if (true)
            //        {
            //            System.Windows.Forms.OpenFileDialog ofd = new System.Windows.Forms.OpenFileDialog();
            //            ofd.Filter = "*.cfg|*.cfg";
            //            if (ofd.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
            //            string name = Path.GetFileNameWithoutExtension(ofd.FileName);
            //            PoiParam? poiParam = JsonConvert.DeserializeObject<PoiParam>(File.ReadAllText(ofd.FileName));
            //            if (poiParam != null)
            //            {
            //                CreateNewTemplate(PoiParam.Params, name, poiParam);
            //                PoiParam.Save2DB(poiParam);
            //            }
            //            else MessageBox.Show("数据库创建POI模板失败", "ColorVision", MessageBoxButton.OK, MessageBoxImage.None, MessageBoxResult.None, MessageBoxOptions.DefaultDesktopOnly);
            //        }
            //        break;
            //    case TemplateType.MTFParam:
            //        if (true)
            //        {
            //            System.Windows.Forms.OpenFileDialog ofd = new System.Windows.Forms.OpenFileDialog();
            //            ofd.Filter = "*.cfg|*.cfg";
            //            if (ofd.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
            //            string name = Path.GetFileNameWithoutExtension(ofd.FileName);
            //            MTFParam? mTFParam = JsonConvert.DeserializeObject<MTFParam>(File.ReadAllText(ofd.FileName));
            //            if (mTFParam != null)
            //            {
            //                CreateNewTemplate(MTFParam.MTFParams, name, mTFParam);
            //                TemplateControl.Save2DB(mTFParam);
            //            }
            //            else MessageBox.Show("数据库创建MTF模板失败", "ColorVision", MessageBoxButton.OK, MessageBoxImage.None, MessageBoxResult.None, MessageBoxOptions.DefaultDesktopOnly);
            //        }
            //        break;
            //    case TemplateType.SFRParam:
            //        if (true)
            //        {
            //            System.Windows.Forms.OpenFileDialog ofd = new System.Windows.Forms.OpenFileDialog();
            //            ofd.Filter = "*.cfg|*.cfg";
            //            if (ofd.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
            //            string name = Path.GetFileNameWithoutExtension(ofd.FileName);
            //            SFRParam? sFRParam = JsonConvert.DeserializeObject<SFRParam>(File.ReadAllText(ofd.FileName));
            //            if (sFRParam != null)
            //            {
            //                CreateNewTemplate(SFRParam.SFRParams, name, sFRParam);
            //                TemplateControl.Save2DB(sFRParam);
            //            }
            //            else MessageBox.Show("数据库创建SFR模板失败", "ColorVision", MessageBoxButton.OK, MessageBoxImage.None, MessageBoxResult.None, MessageBoxOptions.DefaultDesktopOnly);
            //        }
            //        break;
            //    case TemplateType.FOVParam:
            //        if (true)
            //        {
            //            System.Windows.Forms.OpenFileDialog ofd = new System.Windows.Forms.OpenFileDialog();
            //            ofd.Filter = "*.cfg|*.cfg";
            //            if (ofd.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
            //            FOVParam? fOVParam = JsonConvert.DeserializeObject<FOVParam>(File.ReadAllText(ofd.FileName));
            //            string name = Path.GetFileNameWithoutExtension(ofd.FileName);
            //            if (fOVParam != null)
            //            {
            //                CreateNewTemplate(FOVParam.FOVParams, name, fOVParam);
            //                TemplateControl.Save2DB(fOVParam);
            //            }
            //            else MessageBox.Show("数据库创建FOV模板失败", "ColorVision", MessageBoxButton.OK, MessageBoxImage.None, MessageBoxResult.None, MessageBoxOptions.DefaultDesktopOnly);
            //        }
            //        break;
            //    case TemplateType.GhostParam:
            //        if (true)
            //        {
            //            System.Windows.Forms.OpenFileDialog ofd = new System.Windows.Forms.OpenFileDialog();
            //            ofd.Filter = "*.cfg|*.cfg";
            //            GhostParam? ghostParam = JsonConvert.DeserializeObject<GhostParam>(File.ReadAllText(ofd.FileName));
            //            if (ofd.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
            //            string name = Path.GetFileNameWithoutExtension(ofd.FileName);
            //            if (ghostParam != null)
            //            {
            //                CreateNewTemplate(GhostParam.GhostParams, name, ghostParam);
            //                TemplateControl.Save2DB(ghostParam);
            //            }
            //            else MessageBox.Show("数据库创建Ghost模板失败", "ColorVision", MessageBoxButton.OK, MessageBoxImage.None, MessageBoxResult.None, MessageBoxOptions.DefaultDesktopOnly);
            //        }
            //        break;
            //    case TemplateType.DistortionParam:
            //        if (true)
            //        {
            //            System.Windows.Forms.OpenFileDialog ofd = new System.Windows.Forms.OpenFileDialog();
            //            ofd.Filter = "*.cfg|*.cfg";

            //            if (ofd.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
            //            string name = Path.GetFileNameWithoutExtension(ofd.FileName);
            //            DistortionParam? distortionParam = JsonConvert.DeserializeObject<DistortionParam>(File.ReadAllText(ofd.FileName));
            //            if (distortionParam != null)
            //            {
            //                CreateNewTemplate(DistortionParam.DistortionParams, name, distortionParam);
            //                TemplateControl.Save2DB(distortionParam);
            //            }
            //            else MessageBox.Show("数据库创建Distortion模板失败", "ColorVision", MessageBoxButton.OK, MessageBoxImage.None, MessageBoxResult.None, MessageBoxOptions.DefaultDesktopOnly);
            //        }
            //        break;
            //    case TemplateType.LedCheckParam:
            //        if (true)
            //        {
            //            System.Windows.Forms.OpenFileDialog ofd = new System.Windows.Forms.OpenFileDialog();
            //            ofd.Filter = "*.cfg|*.cfg";

            //            if (ofd.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
            //            string name = Path.GetFileNameWithoutExtension(ofd.FileName);
            //            LedCheckParam? ledCheckParam = JsonConvert.DeserializeObject<LedCheckParam>(File.ReadAllText(ofd.FileName));
            //            if (ledCheckParam != null)
            //            {
            //                CreateNewTemplate(LedCheckParam.LedCheckParams, name, ledCheckParam);
            //                TemplateControl.Save2DB(ledCheckParam);
            //            }
            //            else MessageBox.Show("数据库创建灯光检测模板失败", "ColorVision", MessageBoxButton.OK, MessageBoxImage.None, MessageBoxResult.None, MessageBoxOptions.DefaultDesktopOnly);
            //        }
            //        break;
            //    case TemplateType.FocusPointsParam:
            //        if (true)
            //        {
            //            System.Windows.Forms.OpenFileDialog ofd = new System.Windows.Forms.OpenFileDialog();
            //            ofd.Filter = "*.cfg|*.cfg";

            //            if (ofd.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
            //            string name = Path.GetFileNameWithoutExtension(ofd.FileName);
            //            FocusPointsParam? focusPointsParam = JsonConvert.DeserializeObject<FocusPointsParam>(File.ReadAllText(ofd.FileName));
            //            if (focusPointsParam != null)
            //            {
            //                CreateNewTemplate(FocusPointsParam.FocusPointsParams, name, focusPointsParam);
            //                TemplateControl.Save2DB(focusPointsParam);
            //            }
            //            else MessageBox.Show("数据库创建FocusPoints模板失败", "ColorVision", MessageBoxButton.OK, MessageBoxImage.None, MessageBoxResult.None, MessageBoxOptions.DefaultDesktopOnly);
            //        }
            //        break;
            //    case TemplateType.BuildPOIParmam:
            //        if (true)
            //        {
            //            System.Windows.Forms.OpenFileDialog ofd = new System.Windows.Forms.OpenFileDialog();
            //            ofd.Filter = "*.cfg|*.cfg";

            //            if (ofd.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
            //            string name = Path.GetFileNameWithoutExtension(ofd.FileName);
            //            BuildPOIParam? buildPOIParam = JsonConvert.DeserializeObject<BuildPOIParam>(File.ReadAllText(ofd.FileName));
            //            if (buildPOIParam != null)
            //            {
            //                CreateNewTemplate(BuildPOIParam.BuildPOIParams, name, buildPOIParam);
            //                TemplateControl.Save2DB(buildPOIParam);
            //            }
            //            else MessageBox.Show("数据库创建BuildPOI模板失败", "ColorVision", MessageBoxButton.OK, MessageBoxImage.None, MessageBoxResult.None, MessageBoxOptions.DefaultDesktopOnly);
            //        }
            //        break;
            //    case TemplateType.SensorHeYuan:
            //        if (true)
            //        {
            //            System.Windows.Forms.OpenFileDialog ofd = new System.Windows.Forms.OpenFileDialog();
            //            ofd.Filter = "*.cfg|*.cfg";

            //            if (ofd.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
            //            string name = Path.GetFileNameWithoutExtension(ofd.FileName);
            //            SensorHeYuan? sensorHeYuan = JsonConvert.DeserializeObject<SensorHeYuan>(File.ReadAllText(ofd.FileName));
            //            if (sensorHeYuan != null)
            //            {
            //                CreateNewTemplate(SensorHeYuan.SensorHeYuans, name, sensorHeYuan);
            //                    TemplateControl.Save2DB(sensorHeYuan);
            //            }
            //            else MessageBox.Show("数据库创建SensorHeYuan模板失败", "ColorVision", MessageBoxButton.OK, MessageBoxImage.None, MessageBoxResult.None, MessageBoxOptions.DefaultDesktopOnly);
            //        }
            //        break;
            //    case TemplateType.CameraExposureParam:
            //        if (true)
            //        {
            //            System.Windows.Forms.OpenFileDialog ofd = new System.Windows.Forms.OpenFileDialog();
            //            ofd.Filter = "*.cfg|*.cfg";

            //            if (ofd.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
            //            string name = Path.GetFileNameWithoutExtension(ofd.FileName);
            //            CameraExposureParam? cameraExposureParam = JsonConvert.DeserializeObject<CameraExposureParam>(File.ReadAllText(ofd.FileName));
            //            if (cameraExposureParam != null)
            //            {
            //                CreateNewTemplate(CameraExposureParam.Params, name, cameraExposureParam);
            //                TemplateControl.Save2DB(cameraExposureParam);
            //            }
            //            else MessageBox.Show("数据库创建CameraExposureParam模板失败", "ColorVision", MessageBoxButton.OK, MessageBoxImage.None, MessageBoxResult.None, MessageBoxOptions.DefaultDesktopOnly);
            //        }
            //        break;
            //    default:
            //        break;
            //}
        }
       



        private void Searchbox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                if (SearchNoneText.Visibility == Visibility.Visible)
                    SearchNoneText.Visibility = Visibility.Hidden;
                if (string.IsNullOrEmpty(textBox.Text))
                {
                    ListView1.ItemsSource = ITemplate.ItemsSource;
                }
                else
                {

                    var filteredResults = FilterHelper.FilterByKeywords<TemplateModelBase>(ITemplate.ItemsSource, textBox.Text);
                    // 更新 ListView 的数据源
                    ListView1.ItemsSource = filteredResults;

                    // 显示或隐藏无结果文本
                    if (filteredResults.Count == 0)
                    {
                        SearchNoneText.Visibility = Visibility.Visible;
                        SearchNoneText.Text = $"未找到{textBox.Text}相关模板";
                    }
                }
            }
        }

        private void ContextMenu_Opened(object sender, RoutedEventArgs e)
        {
            if (sender is ContextMenu contextMenu && contextMenu.Items.Count == 0 && ListView1.View is GridView gridView)
                GridViewColumnVisibility.GenContentMenuGridViewColumn(contextMenu, gridView.Columns, GridViewColumnVisibilitys);
        }
        public ObservableCollection<GridViewColumnVisibility> GridViewColumnVisibilitys { get; set; } = new ObservableCollection<GridViewColumnVisibility>();

        private void GridViewColumnSort(object sender, RoutedEventArgs e)
        {
            if (sender is GridViewColumnHeader gridViewColumnHeader && gridViewColumnHeader.Content != null && ListView1.ItemsSource is ObservableCollection<TemplateModelBase> results)
            {
                foreach (var item in GridViewColumnVisibilitys)
                {
                    if (item.ColumnName.ToString() == gridViewColumnHeader.Content.ToString())
                    {
                        if (item.ColumnName.ToString() == Resource.SerialNumber1)
                        {
                            item.IsSortD = !item.IsSortD;
                            results.SortByID(item.IsSortD);
                        }
                    }
                }
            }
        }


        private void MenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Control button && button.Tag is TemplateModelBase templateModelBase)
            {
                templateModelBase.IsEditMode = true;
            }
        }
    }
}
