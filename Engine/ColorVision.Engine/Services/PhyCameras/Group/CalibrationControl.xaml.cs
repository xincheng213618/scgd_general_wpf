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
        private readonly Dictionary<string, bool> selectionStates = new();

        /// <summary>
        /// 从 CalibrationParam 中快照当前选中状态
        /// </summary>
        public void SaveFrom(CalibrationParam param)
        {
            Cache = param.CalibrationMode;
            selectionStates.Clear();
            foreach (var slot in CalibrationSlotDefinitions.AllSlots)
            {
                selectionStates[slot.Key] = slot.ParamGetter(param).IsSelected;
            }
        }

        /// <summary>
        /// 将缓存的选中状态恢复到 CalibrationParam
        /// </summary>
        public void RestoreTo(CalibrationParam param)
        {
            foreach (var slot in CalibrationSlotDefinitions.AllSlots)
            {
                if (selectionStates.TryGetValue(slot.Key, out var isSelected))
                {
                    slot.ParamGetter(param).IsSelected = isSelected;
                }
            }
        }

        /// <summary>
        /// 将所有选中状态重置为 false
        /// </summary>
        public static void ClearSelection(CalibrationParam param)
        {
            foreach (var slot in CalibrationSlotDefinitions.AllSlots)
            {
                slot.ParamGetter(param).IsSelected = false;
            }
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
            foreach (var slot in CalibrationSlotDefinitions.AllSlots)
            {
                slot.ParamGetter(param).IsExitFile = slot.GroupGetter(groupResource)?.IsValid ?? false;
            }
        }

        /// <summary>
        /// 清空所有校正文件路径
        /// </summary>
        private static void ClearAllFilePaths(CalibrationParam param)
        {
            foreach (var slot in CalibrationSlotDefinitions.AllSlots)
            {
                slot.ParamGetter(param).FilePath = string.Empty;
            }
        }

        /// <summary>
        /// 从 GroupResource 同步校正文件路径和 ID 到 CalibrationParam
        /// </summary>
        private static void SyncFilePathsAndIds(CalibrationParam param, GroupResource groupResource)
        {
            foreach (var slot in CalibrationSlotDefinitions.AllSlots)
            {
                SetCalibrationFile(slot.ParamGetter(param), slot.GroupGetter(groupResource));
            }
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

    }


}
