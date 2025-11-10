#pragma warning disable CA1720,CS8601,CS8602,CS8603
using ColorVision.Common.MVVM;
using ColorVision.Database;
using Newtonsoft.Json.Linq;
using SqlSugar;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Engine.Archive.Dao
{
    [SugarTable("t_scgd_archived_detail")]
    public class ArchivedDetailModel 
    {
        [SugarColumn(IsIgnore = true)]
        public RelayCommand ExportCommand { get; set; }
        [SugarColumn(IsIgnore = true)]
        public ContextMenu ContextMenu { get; set; }

        public ArchivedDetailModel()
        {
            ExportCommand = new RelayCommand(a=> Export());
            ContextMenu = new ContextMenu();
            ContextMenu.Items.Add(new MenuItem() { Header = Properties.Resources.Export, Command = ExportCommand });
        }

        public void Export()
        {
            ConfigArchivedModel configArchivedModel = ConfigArchivedDao.Instance.GetById(1);
            switch (DetailType)
            {
                case "Camera_Img":
                    // 解析 JSON
                    JObject json = JObject.Parse(OutputValue);

                    // 获取文件名
                    string fileName = json["FileName"].ToString();
                    string filepath = json["FilePath"].ToString();

                    string fullName = Path.Combine(configArchivedModel.Path + "\\" + filepath, fileName);
                    if (!File.Exists(fullName))
                    {
                        MessageBox.Show("找不到文件");
                        return;
                    }

                    // 使用 SaveFileDialog 让用户选择导出路径
                    using (System.Windows.Forms.SaveFileDialog saveFileDialog = new System.Windows.Forms.SaveFileDialog())
                    {
                        saveFileDialog.FileName = fileName;
                        saveFileDialog.Filter = "All files (*.tif)|*.*";
                        saveFileDialog.Title = "选择导出文件位置";
                        saveFileDialog.FileName = fileName;
                        if (saveFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                        {
                            string exportPath = saveFileDialog.FileName;
                            File.Copy(fullName, exportPath);
                        }
                    }
                    break;
                case "Algorithm_Calibration":
                    // 解析 JSON
                    json = JObject.Parse(OutputValue);

                    // 获取文件名
                    fileName = json["FileName"].ToString();
                    filepath = json["FilePath"].ToString();

                    fullName = Path.Combine(configArchivedModel.Path + "\\" + filepath, fileName);
                    if (!File.Exists(fullName))
                    {
                        MessageBox.Show(ColorVision.Engine.Properties.Resources.FileNotFound);
                        return;
                    }

                    // 使用 SaveFileDialog 让用户选择导出路径
                    using (System.Windows.Forms.SaveFileDialog saveFileDialog = new System.Windows.Forms.SaveFileDialog())
                    {
                        saveFileDialog.FileName = fileName;
                        saveFileDialog.Filter = "All files (*.tif)|*.*";
                        saveFileDialog.Title = ColorVision.Engine.Properties.Resources.SelectExportFileLocation;
                        saveFileDialog.FileName = fileName;
                        if (saveFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                        {
                            string exportPath = saveFileDialog.FileName;
                            File.Copy(fullName, exportPath);
                        }
                    }
                    break;
                case "Algorithm_POI_XYZ":
                    // 使用 SaveFileDialog 让用户选择导出路径
                    using (System.Windows.Forms.SaveFileDialog saveFileDialog = new System.Windows.Forms.SaveFileDialog())
                    {
                        saveFileDialog.Filter = "All files (*.json)|*.*";
                        saveFileDialog.Title = ColorVision.Engine.Properties.Resources.SelectExportFileLocation;
                        saveFileDialog.FileName = Guid + ".json";
                        if (saveFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                        {
                            string exportPath = saveFileDialog.FileName;
                            File.WriteAllText(exportPath, OutputValue);
                        }
                    }
                    break;
                default:
                    MessageBox.Show(DetailType);
                    break;
            }



        }


        public void Save(string FullPath)
        {
            ConfigArchivedModel configArchivedModel = ConfigArchivedDao.Instance.GetById(1);
            switch (DetailType)
            {
                case "Camera_Img":
                    // 解析 JSON
                    JObject json = JObject.Parse(OutputValue);

                    // 获取文件名
                    string fileName = json["FileName"].ToString();
                    string filepath = json["FilePath"].ToString();

                    string fullName = Path.Combine(configArchivedModel.Path + "\\" + filepath, fileName);
                    if (!File.Exists(fullName))
                    {
                        return;
                    }

                    string exportPath = Path.Combine(FullPath, fileName);
                    File.Copy(fullName, exportPath);
                    break;
                case "Algorithm_Calibration":
                    // 解析 JSON
                    json = JObject.Parse(OutputValue);

                    // 获取文件名
                    fileName = json["FileName"].ToString();
                    filepath = json["FilePath"].ToString();

                    fullName = Path.Combine(configArchivedModel.Path + "\\" + filepath, fileName);
                    if (!File.Exists(fullName))
                    {
                        return;
                    }
                    string exportPath1 = Path.Combine(FullPath, fileName);
                    File.Copy(fullName, exportPath1);

                    break;
                case "Algorithm_POI_XYZ":

                    string exportPath2 = Path.Combine(FullPath, Guid);
                    File.WriteAllText(exportPath2, OutputValue);
                    break;
                default:
                    string exportPath3 = Path.Combine(FullPath, Guid);
                    File.WriteAllText(exportPath3, OutputValue);
                    break;
            }
        }

        [SugarColumn(ColumnName = "guid",IsPrimaryKey = true)]
        [DisplayName("Guid")]
        public string Guid { get; set; }
        [SugarColumn(ColumnName ="p_guid"),DisplayName("Name")]
        public string PGuid { get; set; }
        [SugarColumn(ColumnName ="detail_type"), DisplayName("detail_type")]
        public string DetailType { get; set; }
        [SugarColumn(ColumnName ="z_index")]
        public int? ZIndex { get; set; }
        [SugarColumn(ColumnName ="output_value"),DisplayName("Data")]
        public string OutputValue { get; set; }
        [SugarColumn(ColumnName ="device_code"), DisplayName("DeviceName")]
        public string DeviceCode { get; set; }
        [SugarColumn(ColumnName ="device_cfg")]
        public string DeviceCfg { get; set; }

        [SugarColumn(ColumnName ="input_cfg"),DisplayName("ParamInput")]
        public string InputCfg { get; set; }

    }
}
