#pragma warning disable CS8602
using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Database;
using ColorVision.UI.Extension;
using log4net;
using Newtonsoft.Json;
using SqlSugar;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Windows;

namespace ColorVision.Engine.Templates.BuzProduct
{
    public class ITemplateBuzProduc<T> : ITemplate where T: TemplateBuzProductParam, new()
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(ITemplate));
        public ObservableCollection<TemplateModel<T>> TemplateParams { get; set; } = new ObservableCollection<TemplateModel<T>>();

        public override int GetTemplateIndex(string templateName)
        {
            return TemplateParams
                    .Select((template, index) => new { template, index })
                    .FirstOrDefault(t => t.template.Key == templateName)?.index ?? -1;
        }

        public override List<string> GetTemplateNames()
        {
            return TemplateParams.Select(a => a.Key).ToList();
        }

        public override string Title { get => Code + ColorVision.Engine.Properties.Resources.Edit; set { } }

        public override Type GetTemplateType => typeof(T);

        public int FindIndex(int id) => TemplateParams.ToList().FindIndex(a => a.Id == id);

        public override int Count => TemplateParams.Count;

        public override IEnumerable GetValue() => TemplateParams;

        public override object GetParamValue(int index) => TemplateParams[index].Value;
        public override object GetValue(int index) => TemplateParams[index];

        public override IEnumerable ItemsSource { get => TemplateParams; }

        public override string GetTemplateName(int index) => TemplateParams[index].Key;

        public T? CreateTemp { get; set; }

        public override object CreateDefault()
        {

            BuzProductMasterModel model = new BuzProductMasterModel();
            CreateTemp = (T)Activator.CreateInstance(typeof(T), new object[] { model });

            if (ExportTemp != null)
                CreateTemp?.CopyFrom(ExportTemp);
            return CreateTemp ?? new T();
        }



        public virtual void Save(TemplateModel<T> item)
        {
            BuzProductMasterDao.Instance.Save(item.Value.BuzProductMasterModel);
        }  

        public override void Save()
        {
            if (SaveIndex.Count == 0) return;
            using var Db = new SqlSugarClient(new ConnectionConfig { ConnectionString = MySqlControl.GetConnectionString(), DbType = SqlSugar.DbType.MySql, IsAutoCloseConnection = true });
            foreach (var index in SaveIndex)
            {
                if (index > -1 && index < TemplateParams.Count)
                {
                    var item = TemplateParams[index];
                    item.Value.BuzProductMasterModel.Name = item.Value.Name;
                    Db.Updateable(item.Value.BuzProductMasterModel).ExecuteCommand();

                    Db.Updateable(item.Value.BuzProductDetailModels).ExecuteCommand();
                }
            }
        }

        public override void Load()
        {
            SaveIndex.Clear();
            var backup = TemplateParams.ToDictionary(tp => tp.Id, tp => tp);

            if (MySqlSetting.IsConnect)
            {


                var templates = BuzProductMasterDao.Instance.GetAllByParam(new Dictionary<string, object>() { { "is_delete", 0 } });
                foreach (var template in templates)
                {
                    if (backup.TryGetValue(template.Id, out var model))
                    {
                    }
                    else
                    {
                        if (Activator.CreateInstance(typeof(T), [template]) is T t)
                        {
                            t.BuzProductDetailModels.Clear();
                            foreach (var item in BuzProductDetailDao.Instance.GetAllByPid(t.Id))
                            {
                                t.BuzProductDetailModels.Add(item);
                            }
                            var templateModel = new TemplateModel<T>(template.Name ?? "default", t);
                            TemplateParams.Add(templateModel);
                        }

                    }

                }
            }
        }


        public override void Delete(int index)
        {
            int selectedCount = TemplateParams.Count(item => item.IsSelected);
            if (selectedCount == 1) index = TemplateParams.IndexOf(TemplateParams.First(item => item.IsSelected));
            void DeleteSingle(int id)
            {
                using var Db = new SqlSugarClient(new ConnectionConfig { ConnectionString = MySqlControl.GetConnectionString(), DbType = SqlSugar.DbType.MySql, IsAutoCloseConnection = true });
                Db.Deleteable<BuzProductMasterModel>().Where(x => x.Id == id).ExecuteCommand();
                Db.Deleteable<BuzProductDetailModel>().Where(x => x.Pid == id).ExecuteCommand();
                TemplateParams.RemoveAt(index);
            }

            if (selectedCount <= 1)
            {
                int id = TemplateParams[index].Value.Id;
                DeleteSingle(id);
            }
            else
            {
                foreach (var item in TemplateParams.Where(item => item.IsSelected == true).ToList())
                {
                    DeleteSingle(item.Id);
                }
            }
        }


        public override void Export(int index)
        {
            int selectedCount = TemplateParams.Count(item => item.IsSelected);
            if (selectedCount == 1) index = TemplateParams.IndexOf(TemplateParams.First(item => item.IsSelected));

            if (selectedCount <= 1)
            {
                using System.Windows.Forms.SaveFileDialog sfd = new System.Windows.Forms.SaveFileDialog();
                sfd.DefaultExt = "cfg";
                sfd.Filter = "*.cfg|*.cfg";
                sfd.AddExtension = false;
                sfd.RestoreDirectory = true;
                sfd.Title = ColorVision.Engine.Properties.Resources.Engine_Dlg_ExportTemplate;
                sfd.FileName = Tool.SanitizeFileName(TemplateParams[index].Key);
                if (sfd.FileName.Contains('.'))
                    sfd.FileName = sfd.FileName + ".cfg";
                if (sfd.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
                TemplateParams[index].Value.ToJsonNFile(sfd.FileName);
            }
            else
            {
                using System.Windows.Forms.SaveFileDialog sfd = new System.Windows.Forms.SaveFileDialog();
                sfd.DefaultExt = "zip";
                sfd.Filter = "*.zip|*.zip";
                sfd.AddExtension = true;
                sfd.RestoreDirectory = true;
                sfd.Title = ColorVision.Engine.Properties.Resources.Export;
                sfd.FileName = $"{Code}.zip";
                if (sfd.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

                string tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
                Directory.CreateDirectory(tempDirectory);
                try
                {
                    // 导出所有模板文件到临时目录
                    foreach (var kvp in TemplateParams.Where(item => item.IsSelected == true))
                    {
                        string filePath = Path.Combine(tempDirectory, $"{Tool.SanitizeFileName(kvp.Key)}.cfg");
                        kvp.Value.ToJsonNFile(filePath);
                    }

                    // 创建压缩文件
                    using (FileStream zipToOpen = new FileStream(sfd.FileName, FileMode.Create))
                    {
                        using (ZipArchive archive = new ZipArchive(zipToOpen, ZipArchiveMode.Create))
                        {
                            foreach (string filePath in Directory.GetFiles(tempDirectory))
                            {
                                archive.CreateEntryFromFile(filePath, Path.GetFileName(filePath));
                            }
                        }
                    }
                }
                finally
                {
                    // 清理临时目录
                    Directory.Delete(tempDirectory, true);
                }
            }
        }

        public T? ExportTemp { get; set; }

        public override bool HasCreateTemplateSource => ExportTemp != null || !string.IsNullOrWhiteSpace(ImportName);

        public override bool CopyTo(int index)
        {
            if (index < 0 || index >= TemplateParams.Count) return false;

            T source = TemplateParams[index].Value;
            source.BuzProductDetailModels.Clear();
            foreach (var item in BuzProductDetailDao.Instance.GetAllByPid(source.Id))
            {
                source.BuzProductDetailModels.Add(item);
            }

            string fileContent = source.ToJsonN();
            ExportTemp = JsonConvert.DeserializeObject<T>(fileContent);
            ResetExportTempIds();
            return ExportTemp != null;
        }

        public override void ClearCreateTemplateSource()
        {
            ImportName = string.Empty;
            CreateTemp = null;
            ExportTemp = null;
        }

        public override bool ImportJsonContent(string templateName, string jsonContent)
        {
            if (string.IsNullOrWhiteSpace(jsonContent)) return false;

            try
            {
                ImportName = templateName;
                ExportTemp = JsonConvert.DeserializeObject<T>(jsonContent);
                ResetExportTempIds();
                return ExportTemp != null;
            }
            catch (JsonException ex)
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), $"解析模板内容时出错: {ex.Message}", "ColorVision");
                return false;
            }
        }

        public override bool Import()
        {
            System.Windows.Forms.OpenFileDialog ofd = new System.Windows.Forms.OpenFileDialog();
            ofd.Filter = "*.cfg|*.cfg";
            ofd.Title = ColorVision.Engine.Properties.Resources.Engine_Dlg_ImportTemplate;
            ofd.RestoreDirectory = true;
            if (ofd.ShowDialog() != System.Windows.Forms.DialogResult.OK) return false;
            //if (TemplateParams.Any(a => a.Key.Equals(System.IO.Path.GetFileNameWithoutExtension(sfd.FileName), StringComparison.OrdinalIgnoreCase)))
            //{
            //    MessageBox.Show(Application.Current.GetActiveWindow(), "模板名称已存在", "ColorVision");
            //    return false;
            //}
            byte[] fileBytes = File.ReadAllBytes(ofd.FileName);
            string fileContent = System.Text.Encoding.UTF8.GetString(fileBytes);
            CreateDefault();
            try
            {
                T Temp = JsonConvert.DeserializeObject<T>(fileContent);
                ExportTemp = Temp;
                ResetExportTempIds();
                return true;
            }
            catch (JsonException ex)
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), $"解析模板文件时出错: {ex.Message}", "ColorVision");
                return false;
            }
        }
        public override void Create(string templateName)
        {
            if (CreateTemp == null && ExportTemp != null)
                CreateDefault();

            T? AddParamMode()
            {
                BuzProductMasterModel buzProductMasterModel = new BuzProductMasterModel();
                if (CreateTemp != null)
                {
                    buzProductMasterModel.CopyFrom(CreateTemp.BuzProductMasterModel);
                    buzProductMasterModel.Name = templateName;
                }
                else
                {
                    buzProductMasterModel.Name = templateName;
                }
                BuzProductMasterDao.Instance.Save(buzProductMasterModel);
                if (buzProductMasterModel.Id > 0)
                {
                    T result = (T)Activator.CreateInstance(typeof(T), new object[] { buzProductMasterModel });
                    if (CreateTemp != null)
                    {
                        foreach (BuzProductDetailModel detail in CreateTemp.BuzProductDetailModels)
                        {
                            BuzProductDetailModel detailModel = new BuzProductDetailModel();
                            detailModel.CopyFrom(detail);
                            detailModel.Id = -1;
                            detailModel.Pid = buzProductMasterModel.Id;
                            BuzProductDetailDao.Instance.Save(detailModel);
                            result.BuzProductDetailModels.Add(detailModel);
                        }
                    }
                    return result;
                }
                return null;
            }
            T? param = AddParamMode();
            if (ExportTemp != null) ExportTemp = null;
            if (param != null)
            {
                var a = new TemplateModel<T>(templateName, param);
                TemplateParams.Add(a);
            }
            else
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), $"数据库创建{typeof(T)}模板失败", "ColorVision");
                if (GetMysqlCommand() is IMysqlCommand mysqlCommand)
                {
                    if (MessageBox.Show(Application.Current.GetActiveWindow(), $"是否重置数据库{typeof(T)}相关项", "ColorVision", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                    {
                        MySqlControl.BatchExecuteNonQuery(mysqlCommand.GetRecover());
                    }
                }
            }
        }

        private void ResetExportTempIds()
        {
            if (ExportTemp == null) return;

            ExportTemp.Id = -1;
            ExportTemp.BuzProductMasterModel.Id = -1;
            foreach (BuzProductDetailModel detail in ExportTemp.BuzProductDetailModels)
            {
                detail.Id = -1;
                detail.Pid = -1;
            }
        }



    }

}
