using ColorVision.Database;
using ColorVision.Engine.Services.Types;
using ColorVision.Themes;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Windows;
using System.Windows.Controls;


namespace ColorVision.Engine.Services.PhyCameras.Group
{
    /// <summary>
    /// CalibrationEdit.xaml 的交互逻辑
    /// </summary>
    public partial class CalibrationEdit : Window
    {
        public PhyCamera PhyCamera { get; set; }

        private int Index;

        public CalibrationEdit(PhyCamera calibrationService , int index = 0)
        {
            PhyCamera = calibrationService;
            Index = index;

            InitializeComponent();
            this.ApplyCaption();
        }
        public ObservableCollection<GroupResource> groupResources { get; set; } = new ObservableCollection<GroupResource>();

        public ObservableCollection<CalibrationResource> DarkNoiseList { get; set; } = new ObservableCollection<CalibrationResource>();
        public ObservableCollection<CalibrationResource> DefectPointList { get; set; } = new ObservableCollection<CalibrationResource>();
        public ObservableCollection<CalibrationResource> DSNUList { get; set; } = new ObservableCollection<CalibrationResource>();
        public ObservableCollection<CalibrationResource> UniformityList { get; set; } = new ObservableCollection<CalibrationResource>();
        public ObservableCollection<CalibrationResource> DistortionList { get; set; } = new ObservableCollection<CalibrationResource>();
        public ObservableCollection<CalibrationResource> ColorShiftList { get; set; } = new ObservableCollection<CalibrationResource>();
        public ObservableCollection<CalibrationResource> LuminanceList { get; set; } = new ObservableCollection<CalibrationResource>();
        public ObservableCollection<CalibrationResource> LumOneColorList { get; set; } = new ObservableCollection<CalibrationResource>();
        public ObservableCollection<CalibrationResource> LumFourColorList { get; set; } = new ObservableCollection<CalibrationResource>();
        public ObservableCollection<CalibrationResource> LumMultiColorList { get; set; } = new ObservableCollection<CalibrationResource>();
        public ObservableCollection<CalibrationResource> ColorDiffList { get; set; } = new ObservableCollection<CalibrationResource>();
        public ObservableCollection<CalibrationResource> LineArityList { get; set; } = new ObservableCollection<CalibrationResource>();


        public void Init()
        {
            groupResources.Clear();
            foreach (var item in PhyCamera.VisualChildren)
            {
                if (item is GroupResource groupResource)
                {
                    groupResource.SetCalibrationResource();
                    groupResources.Add(groupResource);
                }
                if (item is CalibrationResource calibrationResource)
                {
                    switch ((ServiceTypes)calibrationResource.SysResourceModel.Type)
                    {
                        case ServiceTypes.DarkNoise:
                            DarkNoiseList.Add(calibrationResource);
                            break;
                        case ServiceTypes.DefectPoint:
                            DefectPointList.Add(calibrationResource);
                            break;
                        case ServiceTypes.DSNU:
                            DSNUList.Add(calibrationResource);
                            break;
                        case ServiceTypes.Uniformity:
                            UniformityList.Add(calibrationResource);
                            break;
                        case ServiceTypes.Distortion:
                            DistortionList.Add(calibrationResource);
                            break;
                        case ServiceTypes.ColorShift:
                            ColorShiftList.Add(calibrationResource);
                            break;
                        case ServiceTypes.Luminance:
                            LuminanceList.Add(calibrationResource);
                            break;
                        case ServiceTypes.LumOneColor:
                            LumOneColorList.Add(calibrationResource);
                            break;
                        case ServiceTypes.LumFourColor:
                            LumFourColorList.Add(calibrationResource);
                            break;
                        case ServiceTypes.LumMultiColor:
                            LumMultiColorList.Add(calibrationResource);
                            break;
                        case ServiceTypes.ColorDiff:
                            ColorDiffList.Add(calibrationResource);
                            break;
                        case ServiceTypes.LineArity:
                            LineArityList.Add(calibrationResource);
                            break;
                        default:
                            break;
                    }
                }
            }

            ListView1.ItemsSource = groupResources;
            if (groupResources.Count > 0)
            {
                ListView1.SelectedIndex = 0;
                StackPanelCab.DataContext = groupResources[0];
            }

            ComboBoxDarkNoise.ItemsSource = DarkNoiseList;
            ComboBoxDefectPoint.ItemsSource = DefectPointList;
            ComboBoxDSNU.ItemsSource = DSNUList;
            ComboBoxUniformity.ItemsSource = UniformityList;
            ComboBoxDistortion.ItemsSource = DistortionList;
            ComboBoxColorShift.ItemsSource = ColorShiftList;
            ComboBoxLuminance.ItemsSource = LuminanceList;
            ComboBoxLumOneColor.ItemsSource = LumOneColorList;
            ComboBoxLumFourColor.ItemsSource = LumFourColorList;
            ComboBoxLumMultiColor.ItemsSource = LumMultiColorList;
            ComboBoxColorDiff.ItemsSource = ColorDiffList;
            ComboBoxLineArity.ItemsSource = LineArityList;

            ListView1.SelectedIndex = Index;
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            Init();
            DataContext = PhyCamera;
            PhyCamera.VisualChildren.CollectionChanged +=(s,e) => Init();
        }

        private void ListView1_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ListView1.SelectedIndex > -1)
            {
                StackPanelCab.DataContext = groupResources[ListView1.SelectedIndex];
            }
        }

        private void Button_Add_Click(object sender, RoutedEventArgs e)
        {
            string calue = NewCreateFileName("title");
            var group = GroupResource.AddGroupResource(PhyCamera, calue);
            if (group == null)
            {
                MessageBox.Show("创建失败");
            }

        }

        public string NewCreateFileName(string FileName)
        {
            var list = groupResources.Select(g => g.Name).Distinct().ToList();
            for (int i = 1; i < 9999; i++)
            {
                if (!list.Contains($"{FileName}{i}"))
                    return $"{FileName}{i}";
            }
            return FileName;
        }

        private void Button_Delete_Click(object sender, RoutedEventArgs e)
        {
            if (ListView1.SelectedItems.Count > 0)
            {
                // Create a SysDictionaryModDetaiModels to hold the items to be removed
                List<GroupResource> itemsToRemove = new List<GroupResource>();

                foreach (var selectedItem in ListView1.SelectedItems)
                {
                    GroupResource groupResource = selectedItem as GroupResource;
                    if (groupResource != null)
                    {
                        MySqlControl.GetInstance().DB.Deleteable<SysResourceModel>().Where(it => it.Id == groupResource.SysResourceModel.Id).ExecuteCommand();
                        itemsToRemove.Add(groupResource);
                    }
                }

                // Remove the items from the original SysDictionaryModDetaiModels and the visual children
                foreach (var item in itemsToRemove)
                {
                    groupResources.Remove(item);
                    PhyCamera.VisualChildren.Remove(item);
                }

                MessageBox.Show("删除成功");
            }
            else
            {
                MessageBox.Show("请选择要删除的项");
            }
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            foreach (var item in groupResources)
            {
                item.Save();
            }
        }

        private void TextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox && textBox.Tag is GroupResource groupResource)
            {
                groupResource.IsEditMode = false;
            }
        }

        private void Export_Click(object sender, RoutedEventArgs e)
        {
            string zipFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "output.zip");
            using (System.Windows.Forms.SaveFileDialog saveFileDialog = new System.Windows.Forms.SaveFileDialog())
            {
                saveFileDialog.Filter = "ZIP files (*.zip)|*.zip";
                saveFileDialog.DefaultExt = "zip";
                saveFileDialog.AddExtension = true;
                saveFileDialog.FileName =  $"{PhyCamera.Code}.zip";
                saveFileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                if (saveFileDialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                    return;
                 zipFilePath = saveFileDialog.FileName;
            }


            // 创建或打开ZIP文件
            using (FileStream zipToOpen = new FileStream(zipFilePath, FileMode.Create))
            {
                using (ZipArchive archive = new ZipArchive(zipToOpen, ZipArchiveMode.Update))
                {
                    Dictionary<string, List<ZipCalibrationItem>> keyValuePairs = new Dictionary<string, List<ZipCalibrationItem>>();
                    List<ZipCalibrationItem> calibrationItems = new List<ZipCalibrationItem>();
                    keyValuePairs.Add("Calibration", calibrationItems);
                    foreach (var item in PhyCamera.VisualChildren)
                    {
                        if (item is CalibrationResource calibrationResource)
                        {
                            ZipCalibrationItem zipCalibrationItem = new ZipCalibrationItem();
                            zipCalibrationItem.CalibrationType = ((ServiceTypes)calibrationResource.SysResourceModel.Type).ToCalibrationType();
                            zipCalibrationItem.Title = calibrationResource.Config.Title;
                            zipCalibrationItem.FileName = calibrationResource.Config.FileName;
                            calibrationItems.Add(zipCalibrationItem);

                            var serviceType = (ServiceTypes)calibrationResource.SysResourceModel.Type;
                            if (calibrationResource.GetAncestor<PhyCamera>() is PhyCamera phyCamera)
                            {
                                if (Directory.Exists(phyCamera.Config.FileServerCfg.FileBasePath))
                                {
                                    string path = calibrationResource.SysResourceModel.Value ?? string.Empty;
                                    string filepath = Path.Combine(phyCamera.Config.FileServerCfg.FileBasePath, phyCamera.Code, "cfg", path);

                                    // 确保文件存在
                                    if (File.Exists(filepath))
                                    {
                                        string entryPath = Path.Combine("Calibration", serviceType.ToString(), calibrationResource.Config.FileName);
                                        archive.CreateEntryFromFile(filepath, entryPath);
                                    }
                                }
                            }
                        }

                        if (item is GroupResource groupResource)
                        {
                            List<ZipCalibrationItem> zipCalibrationItems = new List<ZipCalibrationItem>();
                            foreach (var cc in groupResource.VisualChildren)
                            {
                                if (cc is CalibrationResource caesource)
                                {
                                    ZipCalibrationItem zipCalibrationItem = new ZipCalibrationItem();
                                    zipCalibrationItem.CalibrationType = ((ServiceTypes)caesource.SysResourceModel.Type).ToCalibrationType();
                                    zipCalibrationItem.Title = caesource.Config.Title;
                                    zipCalibrationItem.FileName = caesource.Config.FileName;
                                    zipCalibrationItems.Add(zipCalibrationItem);
                                }
                            }

                            // 序列化为 JSON 使用 Newtonsoft.Json
                            string json = JsonConvert.SerializeObject(zipCalibrationItems, Formatting.Indented);

                            // 添加 JSON 到 ZIP
                            string entryName = Path.Combine("Calibration", $"{groupResource.Name}.cfg");
                            ZipArchiveEntry jsonEntry = archive.CreateEntry(entryName);
                            using (StreamWriter writer = new StreamWriter(jsonEntry.Open()))
                            {
                                writer.Write(json);
                            }
                        }
                    }


                    // 序列化为 JSON 使用 Newtonsoft.Json
                    string json1 = JsonConvert.SerializeObject(keyValuePairs, Formatting.Indented);

                    // 添加 JSON 到 ZIP
                    string entryName1 = $"Calibration.cfg";
                    ZipArchiveEntry jsonEntry1 = archive.CreateEntry(entryName1);
                    using (StreamWriter writer = new StreamWriter(jsonEntry1.Open()))
                    {
                        writer.Write(json1);
                    }

                    string phyconfig = JsonConvert.SerializeObject(PhyCamera.Config, Formatting.Indented);
                    ZipArchiveEntry jsonEntry2 = archive.CreateEntry("Camera.cfg");
                    using (StreamWriter writer = new StreamWriter(jsonEntry2.Open()))
                    {
                        writer.Write(phyconfig);
                    }
                    if (PhyCamera.CameraLicenseModel != null)
                    {
                        ZipArchiveEntry jsonEntry3 = archive.CreateEntry($"{PhyCamera.Code}.lic");
                        using (StreamWriter writer = new StreamWriter(jsonEntry3.Open()))
                        {
                            writer.Write(PhyCamera.CameraLicenseModel.LicenseValue);
                        }
                    }
                }
            }
            MessageBox.Show($"{PhyCamera.Code}导出成功");
        }

    }
}
