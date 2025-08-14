#pragma warning disable CA1720,CS8601,CS8602,CS8603
using ColorVision.Common.MVVM;
using ColorVision.Engine.MySql;
using ColorVision.Engine.MySql.ORM;
using log4net;
using MySql.Data.MySqlClient;
using Newtonsoft.Json.Linq;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Engine.Archive.Dao
{
    [SugarTable("t_scgd_archived_detail")]
    public class ArchivedDetailModel : PKModel
    {
        public RelayCommand ExportCommand { get; set; }
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
                case "Algorithm_POI_XYZ":
                    // 使用 SaveFileDialog 让用户选择导出路径
                    using (System.Windows.Forms.SaveFileDialog saveFileDialog = new System.Windows.Forms.SaveFileDialog())
                    {
                        saveFileDialog.Filter = "All files (*.json)|*.*";
                        saveFileDialog.Title = "选择导出文件位置";
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
        [Column("guid"), DisplayName("Guid")]
        public string Guid { get; set; }
        [Column("p_guid"),DisplayName("名称")]
        public string PGuid { get; set; }
        [Column("detail_type"), DisplayName("detail_type")]
        public string DetailType { get; set; }
        [Column("z_index")]
        public int? ZIndex { get; set; }
        [Column("output_value"),DisplayName("Data")]
        public string OutputValue { get; set; }
        [Column("device_code"), DisplayName("设备名称")]
        public string DeviceCode { get; set; }
        [Column("device_cfg")]
        public string DeviceCfg { get; set; }

        [Column("input_cfg"),DisplayName("输入参数")]
        public string InputCfg { get; set; }

    }
    public class ArchivedDetailDao : BaseTableDao<ArchivedDetailModel>
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(ArchivedDetailDao));

        public static ArchivedDetailDao Instance { get; set; } = new ArchivedDetailDao();
        public override MySqlConnection CreateConnection()
        {
            MySqlConfig MySqlConfig = GlobleCfgdDao.Instance.GetArchMySqlConfig();
            if (MySqlConfig != null)
            {
                try
                {
                    string connStr = $"server={MySqlConfig.Host};port={MySqlConfig.Port};uid={MySqlConfig.UserName};pwd={MySqlConfig.UserPwd};database={MySqlConfig.Database};charset=utf8;Connect Timeout={3};SSL Mode =None;Pooling=true";
                    using MySqlConnection connection = new MySqlConnection(connStr);
                    connection.Open();
                    return connection;

                }
                catch (Exception ex)
                {
                    log.Error(ex);
                }
            }
            return null;

        }

        public List<ArchivedDetailModel> ConditionalQuery(string batchCode)
        {
            return ConditionalQuery(new Dictionary<string, object>() { { "p_guid", batchCode } });
        }
    }
}
