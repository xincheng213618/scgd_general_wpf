using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Database;
using ColorVision.Engine.Services.Devices.ThirdPartyAlgorithms.Dao;
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

namespace ColorVision.Engine.Templates.Jsons
{
    public class ITemplateJson<T> : ITemplate where T: TemplateJsonParam,new()
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
            return [.. TemplateParams.Select(a => a.Key)];
        }

        public override string Title { get => $"{Code}{ColorVision.Engine.Properties.Resources.Edit}"; set { } }


        public override Type GetTemplateType => typeof(T);

        public int FindIndex(int id) => TemplateParams.ToList().FindIndex(a => a.Id == id);

        public override int Count => TemplateParams.Count;

        public override object GetValue() => TemplateParams;

        public override object GetParamValue(int index) => TemplateParams[index].Value;
        public override object GetValue(int index) => TemplateParams[index];

        public override IEnumerable ItemsSource { get => TemplateParams; }

        public override string GetTemplateName(int index) => TemplateParams[index].Key;

        public T? CreateTemp { get; set; }

        public override object CreateDefault()
        {
            var dictemplate = Db.Queryable<SysDictionaryModModel>().Where(x => x.Id == TemplateDicId).First();
            if (dictemplate ==null)
                return new T();

            ModMasterModel templateJson = new ModMasterModel();
            templateJson.Code = dictemplate.Code;
            templateJson.Pid = dictemplate.Id;
            templateJson.JsonVal = dictemplate.JsonVal;
            CreateTemp = (T)Activator.CreateInstance(typeof(T), new object[] { templateJson });

            if (ExportTemp != null)
                CreateTemp?.CopyFrom(ExportTemp);
            return CreateTemp ?? new T();
        }



        public override void Save()
        {
            if (SaveIndex.Count == 0) return;

            foreach (var index in SaveIndex)
            {
                if (index > -1 && index < TemplateParams.Count)
                {
                    var item = TemplateParams[index];
                    Db.Updateable(item.Value.TemplateJsonModel).ExecuteCommand();
                }
            }
        }

        public override void Load()
        {
            SaveIndex.Clear();
            var backup = TemplateParams.ToDictionary(tp => tp.Id, tp => tp);

            if (MySqlSetting.Instance.IsUseMySql && MySqlSetting.IsConnect)
            {
                List<ModMasterModel> templates = new List<ModMasterModel>();
                templates = Db.Queryable<ModMasterModel>().Where(x => x.Pid == TemplateDicId && x.IsDelete == false).ToList();

                foreach (var template in templates)
                {
                    if (Activator.CreateInstance(typeof(T), [template]) is T t)
                    {
                        if (backup.TryGetValue(t.Id, out var model))
                        {
                            model.Value = t;
                            model.Key = t.Name;
                        }
                        else
                        {
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
                string key = TemplateParams[index].Key;
                int ret = Db.Deleteable<ModMasterModel>().Where(it => it.Id == id).ExecuteCommand();
                log.Info($"Delete Tempate：{key},ret{ret}");
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
        public override bool CopyTo(int index)
        {
            string fileContent = TemplateParams[index].Value.ToJsonN();
            CreateTemp = JsonConvert.DeserializeObject<T>(fileContent);
            if (CreateTemp != null)
            {
                CreateTemp.Id = -1;
            }
            return true;
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
                sfd.Title = "导出模板";
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
                sfd.Title = "导出";
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
        public override bool Import()
        {
            System.Windows.Forms.OpenFileDialog ofd = new System.Windows.Forms.OpenFileDialog();
            ofd.Filter = "*.cfg|*.cfg";
            ofd.Title = "导入模板";
            ofd.RestoreDirectory = true;
            if (ofd.ShowDialog() != System.Windows.Forms.DialogResult.OK) return false;

            return ImportFile(ofd.FileName);
        }

        public override bool ImportFile(string filePath)
        {
            if (!File.Exists(filePath)) return false;
            byte[] fileBytes = File.ReadAllBytes(filePath);
            string fileContent = System.Text.Encoding.UTF8.GetString(fileBytes);
            CreateDefault();
            try
            {
                CreateTemp = JsonConvert.DeserializeObject<T>(fileContent);
                ExportTemp = CreateTemp;
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
            try
            {
                var dictemplate = Db.Queryable<SysDictionaryModModel>().Where(x => x.Id == TemplateDicId).First();
                if (dictemplate == null)
                {
                    log.Warn("模板字典未找到，ID=" + TemplateDicId);
                    MessageBox.Show(Application.Current.GetActiveWindow(), $"模板字典未找到，ID={TemplateDicId}", "ColorVision");
                    return;
                }

                ModMasterModel templateJson = new ModMasterModel();
                if (CreateTemp != null)
                {
                    templateJson.CopyFrom(CreateTemp.TemplateJsonModel);
                    templateJson.Name = templateName;
                    templateJson.Code = dictemplate.Code;
                    templateJson.Pid = dictemplate.Id;
                }
                else
                {
                    templateJson.Name = templateName;
                    templateJson.Code = dictemplate.Code;
                    templateJson.Pid = dictemplate.Id;
                    templateJson.JsonVal = dictemplate.JsonVal;
                }
                templateJson.Id = Db.Insertable(templateJson).ExecuteReturnIdentity();
                log.Info($"模板保存成功，Name={templateJson.Name}, Id={templateJson.Id}");

                ExportTemp = null;

                if (templateJson.Id > 0)
                {
                    var param = (T)Activator.CreateInstance(typeof(T), new object[] { templateJson });
                    var a = new TemplateModel<T>(templateName, param);
                    TemplateParams.Add(a);
                    log.Info($"模板参数添加成功，Name={templateName}");
                }
                else
                {
                    string msg = $"数据库创建{typeof(T)}模板失败";
                    MessageBox.Show(Application.Current.GetActiveWindow(), msg, "ColorVision");
                    log.Error(msg);

                    if (GetMysqlCommand() is IMysqlCommand mysqlCommand)
                    {
                        if (MessageBox.Show(Application.Current.GetActiveWindow(), $"是否重置数据库{typeof(T)}相关项", "ColorVision", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                        {
                            MySqlControl.GetInstance().BatchExecuteNonQuery(mysqlCommand.GetRecover());
                            log.Warn($"数据库{typeof(T)}相关项已重置");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                log.Error("模板创建异常：" + ex.Message, ex);
                MessageBox.Show(Application.Current.GetActiveWindow(), "模板创建发生异常：" + ex.Message, "ColorVision");
            }
        }
    }



}
