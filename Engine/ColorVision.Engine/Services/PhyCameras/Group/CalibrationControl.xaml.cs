using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Controls;
using System.Windows.Input;

namespace ColorVision.Engine.Services.PhyCameras.Group
{
    /// <summary>
    /// 用于缓存校正参数的选中状态，在校正组切换时恢复上一次的选中状态
    /// </summary>
    sealed class TempCache
    {
        public string Cache { get; set; }

        // 基础校正项选中状态
        public bool DarkNoiseIsSelected { get; set; }
        public bool DefectPointIsSelected { get; set; }
        public bool DSNUIsSelected { get; set; }
        public bool DistortionIsSelected { get; set; }
        public bool ColorShiftIsSelected { get; set; }
        public bool ColorDiffIsSelected { get; set; }
        public bool LineArityIsSelected { get; set; }
        public bool UniformityIsSelected { get; set; }

        // 色彩校正项选中状态（互斥）
        public bool LumFourColorIsSelected { get; set; }
        public bool LumOneColorIsSelected { get; set; }
        public bool LumMultiColorIsSelected { get; set; }
        public bool LuminanceIsSelected { get; set; }

        /// <summary>
        /// 从 CalibrationParam 中快照当前选中状态
        /// </summary>
        public void SaveFrom(CalibrationParam param)
        {
            Cache = param.CalibrationMode;
            DarkNoiseIsSelected = param.Normal.DarkNoise.IsSelected;
            DefectPointIsSelected = param.Normal.DefectPoint.IsSelected;
            DSNUIsSelected = param.Normal.DSNU.IsSelected;
            DistortionIsSelected = param.Normal.Distortion.IsSelected;
            ColorShiftIsSelected = param.Normal.ColorShift.IsSelected;
            UniformityIsSelected = param.Normal.Uniformity.IsSelected;
            ColorDiffIsSelected = param.Normal.ColorDiff.IsSelected;
            LineArityIsSelected = param.Normal.LineArity.IsSelected;

            LuminanceIsSelected = param.Color.Luminance.IsSelected;
            LumFourColorIsSelected = param.Color.LumFourColor.IsSelected;
            LumMultiColorIsSelected = param.Color.LumMultiColor.IsSelected;
            LumOneColorIsSelected = param.Color.LumOneColor.IsSelected;
        }

        /// <summary>
        /// 将缓存的选中状态恢复到 CalibrationParam
        /// </summary>
        public void RestoreTo(CalibrationParam param)
        {
            param.Normal.DarkNoise.IsSelected = DarkNoiseIsSelected;
            param.Normal.DefectPoint.IsSelected = DefectPointIsSelected;
            param.Normal.DSNU.IsSelected = DSNUIsSelected;
            param.Normal.Distortion.IsSelected = DistortionIsSelected;
            param.Normal.ColorShift.IsSelected = ColorShiftIsSelected;
            param.Normal.Uniformity.IsSelected = UniformityIsSelected;
            param.Normal.ColorDiff.IsSelected = ColorDiffIsSelected;
            param.Normal.LineArity.IsSelected = LineArityIsSelected;

            param.Color.Luminance.IsSelected = LuminanceIsSelected;
            param.Color.LumFourColor.IsSelected = LumFourColorIsSelected;
            param.Color.LumMultiColor.IsSelected = LumMultiColorIsSelected;
            param.Color.LumOneColor.IsSelected = LumOneColorIsSelected;
        }

        /// <summary>
        /// 将所有选中状态重置为 false
        /// </summary>
        public static void ClearSelection(CalibrationParam param)
        {
            param.Normal.DarkNoise.IsSelected = false;
            param.Normal.DefectPoint.IsSelected = false;
            param.Normal.DSNU.IsSelected = false;
            param.Normal.Distortion.IsSelected = false;
            param.Normal.ColorShift.IsSelected = false;
            param.Normal.Uniformity.IsSelected = false;
            param.Normal.ColorDiff.IsSelected = false;
            param.Normal.LineArity.IsSelected = false;

            param.Color.Luminance.IsSelected = false;
            param.Color.LumFourColor.IsSelected = false;
            param.Color.LumMultiColor.IsSelected = false;
            param.Color.LumOneColor.IsSelected = false;
        }
    }

    /// <summary>
    /// 校正控件：管理物理相机的校正参数组（基础校正 + 色彩校正）。
    /// 校正流水线按固定顺序执行：DarkNoise → DefectPoint → DSNU → Uniformity → ColorShift → Distortion
    /// 色彩校正四选一：Luminance / LumOneColor / LumFourColor / LumMultiColor
    /// </summary>
    public partial class CalibrationControl : UserControl
    {
        public CalibrationParam CalibrationParam { get => _CalibrationParam; set { _CalibrationParam = value;} }
        private CalibrationParam _CalibrationParam;

        public PhyCamera PhyCamera { get; set; }

        public CalibrationControl(PhyCamera calibrationService)
        {
            PhyCamera = calibrationService;

            InitializeComponent();
            CalibrationParam = new CalibrationParam();
            DataContext = CalibrationParam;
        }

        public Dictionary<string, List<ZipCalibrationItem>> CalibrationModeList { get; set; }

        public CalibrationControl(PhyCamera calibrationService, CalibrationParam calibrationParam)
        {
            PhyCamera = calibrationService;
            InitializeComponent();
            CalibrationParam = calibrationParam;
            DataContext = CalibrationParam;
        }

        public ObservableCollection<GroupResource> groupResources { get; set; } = new ObservableCollection<GroupResource>();
        TempCache TempCache { get; set; } = new TempCache();

        /// <summary>
        /// 从 GroupResource 同步校正文件的存在状态到 CalibrationParam
        /// </summary>
        private static void SyncFileExistence(CalibrationParam param, GroupResource groupResource)
        {
            param.Normal.DarkNoise.IsExitFile = groupResource.DarkNoise?.IsValid ?? false;
            param.Normal.DefectPoint.IsExitFile = groupResource.DefectPoint?.IsValid ?? false;
            param.Normal.DSNU.IsExitFile = groupResource.DSNU?.IsValid ?? false;
            param.Normal.Distortion.IsExitFile = groupResource.Distortion?.IsValid ?? false;
            param.Normal.ColorShift.IsExitFile = groupResource.ColorShift?.IsValid ?? false;
            param.Normal.Uniformity.IsExitFile = groupResource.Uniformity?.IsValid ?? false;
            param.Normal.LineArity.IsExitFile = groupResource.LineArity?.IsValid ?? false;
            param.Normal.ColorDiff.IsExitFile = groupResource.ColorDiff?.IsValid ?? false;

            param.Color.Luminance.IsExitFile = groupResource.Luminance?.IsValid ?? false;
            param.Color.LumFourColor.IsExitFile = groupResource.LumFourColor?.IsValid ?? false;
            param.Color.LumMultiColor.IsExitFile = groupResource.LumMultiColor?.IsValid ?? false;
            param.Color.LumOneColor.IsExitFile = groupResource.LumOneColor?.IsValid ?? false;
        }

        /// <summary>
        /// 清空所有校正文件路径
        /// </summary>
        private static void ClearAllFilePaths(CalibrationParam param)
        {
            param.Normal.DarkNoise.FilePath = string.Empty;
            param.Normal.DefectPoint.FilePath = string.Empty;
            param.Normal.DSNU.FilePath = string.Empty;
            param.Normal.Distortion.FilePath = string.Empty;
            param.Normal.ColorShift.FilePath = string.Empty;
            param.Normal.Uniformity.FilePath = string.Empty;
            param.Normal.ColorDiff.FilePath = string.Empty;
            param.Normal.LineArity.FilePath = string.Empty;

            param.Color.Luminance.FilePath = string.Empty;
            param.Color.LumFourColor.FilePath = string.Empty;
            param.Color.LumMultiColor.FilePath = string.Empty;
            param.Color.LumOneColor.FilePath = string.Empty;
        }

        /// <summary>
        /// 从 GroupResource 同步校正文件路径和 ID 到 CalibrationParam
        /// </summary>
        private static void SyncFilePathsAndIds(CalibrationParam param, GroupResource groupResource)
        {
            SetCalibrationFile(param.Normal.DarkNoise, groupResource.DarkNoise);
            SetCalibrationFile(param.Normal.DefectPoint, groupResource.DefectPoint);
            SetCalibrationFile(param.Normal.DSNU, groupResource.DSNU);
            SetCalibrationFile(param.Normal.Distortion, groupResource.Distortion);
            SetCalibrationFile(param.Normal.ColorShift, groupResource.ColorShift);
            SetCalibrationFile(param.Normal.Uniformity, groupResource.Uniformity);
            SetCalibrationFile(param.Normal.LineArity, groupResource.LineArity);
            SetCalibrationFile(param.Normal.ColorDiff, groupResource.ColorDiff);

            SetCalibrationFile(param.Color.Luminance, groupResource.Luminance);
            SetCalibrationFile(param.Color.LumOneColor, groupResource.LumOneColor);
            SetCalibrationFile(param.Color.LumFourColor, groupResource.LumFourColor);
            SetCalibrationFile(param.Color.LumMultiColor, groupResource.LumMultiColor);
        }

        /// <summary>
        /// 将单个 CalibrationResource 的文件名和 ID 赋值到 CalibrationBase
        /// </summary>
        private static void SetCalibrationFile(CalibrationBase calibBase, CalibrationResource resource)
        {
            calibBase.FilePath = resource?.Name ?? string.Empty;
            calibBase.Id = resource?.Id ?? 0;
        }

        public void Initializedsss(CalibrationParam calibrationParam)
        {
            ComboBoxList.SelectionChanged -= ComboBox_SelectionChanged;

            CalibrationParam = calibrationParam;
            DataContext = CalibrationParam;

            TempCache.SaveFrom(calibrationParam);

            ComboBoxList.Text = calibrationParam.CalibrationMode;
            ComboBoxList.SelectionChanged += ComboBox_SelectionChanged;

            if (string.IsNullOrWhiteSpace(calibrationParam.CalibrationMode) && groupResources.Count > 0)
            {
                ComboBoxList.SelectedIndex = 0;
            }

            if (ComboBoxList.SelectedValue is GroupResource groupResource)
            {
                SyncFileExistence(CalibrationParam, groupResource);
            }
        }


        private void UserControl_Initialized(object sender, System.EventArgs e)
        {
            uploadbutton.DataContext = PhyCamera;

            void UpdateDefaultStyle()   
            {
                groupResources.Clear();
                ComboBoxList.ItemsSource = groupResources;
                foreach (var item in PhyCamera.VisualChildren)
                {
                    if (item is GroupResource groupResource)
                    {
                        groupResource.SetCalibrationResource();
                        groupResources.Add(groupResource);
                    }
                }
            }

            PhyCamera.VisualChildren.CollectionChanged += (s, e) =>UpdateDefaultStyle();
            UpdateDefaultStyle();
        }

        private void TextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                Common.NativeMethods.Keyboard.PressKey(0x09);
            }
        }



        private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox comboBox)
            {
                ClearAllFilePaths(CalibrationParam);

                if (comboBox.SelectedValue is GroupResource groupResource)
                {
                    if (groupResource.Name == TempCache.Cache)
                    {
                        TempCache.RestoreTo(CalibrationParam);
                    }
                    else
                    {
                        TempCache.ClearSelection(CalibrationParam);
                    }

                    SyncFileExistence(CalibrationParam, groupResource);
                    SyncFilePathsAndIds(CalibrationParam, groupResource);
                }
            }
        }

        private void Button_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            CalibrationEdit CalibrationEdit = new CalibrationEdit(PhyCamera, ComboBoxList.SelectedIndex);
            CalibrationEdit.Show();
        }

        private void Help_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            CalibrationHelpWindow.ShowHelp(System.Windows.Window.GetWindow(this));
        }


    }


}
