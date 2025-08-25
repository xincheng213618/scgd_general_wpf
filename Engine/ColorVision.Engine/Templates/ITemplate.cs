#pragma warning disable CS8602
using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Database;
using ColorVision.Engine.Rbac;
using ColorVision.Engine.Services.Dao;
using ColorVision.Engine.Templates.SysDictionary;
using ColorVision.Solution;
using ColorVision.UI.Extension;
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
using System.Windows.Controls;

namespace ColorVision.Engine.Templates
{
    public class ITemplate
    {
        public static SqlSugarClient Db => MySqlControl.GetInstance().DB;
        public ITemplate()
        {
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                Name = Name?? Code ?? this.GetType().ToString();
                TemplateControl.AddITemplateInstance(Name, this);
            });
        }

        public int TemplateDicId { get; set; } = -1;

        public string Name { get ; set; }
        public virtual IEnumerable ItemsSource { get; }
        public virtual List<string> GetTemplateNames()
        {
            return new List<string>();
        }

        public virtual string Title { get; set; }

        public string Code { get; set; }

        public virtual int Count { get; }

        public virtual string GetTemplateName(int index)
        {
            throw new NotImplementedException();
        }
        public virtual int GetTemplateIndex(string templateName)
        {
            throw new NotImplementedException();
        }

        public virtual IMysqlCommand? GetMysqlCommand()
        {
            return null;
        }

        public List<int> SaveIndex { get; set; } = new List<int>();

        public void SetSaveIndex(int Index)
        {
            if (!SaveIndex.Contains(Index))
            {
                SaveIndex.Add(Index);
            }
        }

        public virtual object GetValue()
        {
            throw new NotImplementedException();
        }

        public virtual object CreateDefault()
        {
            throw new NotImplementedException();
        }
        public virtual object GetValue(int index)
        {
            throw new NotImplementedException();
        }

        public virtual object GetParamValue(int index)
        {
            throw new NotImplementedException();
        }

        public string NewCreateFileName(string FileName)
        {
            for (int i = 1; i < 9999; i++)
            {
                if (!TemplateControl.ExitsTemplateName($"{FileName}{i}"))
                    return $"{FileName}{i}";
            }
            return FileName;
        }



        public virtual void Save()
        {

        }
        public virtual Type GetTemplateType { get; }
        public virtual void Export(int index)
        {

        }

        public virtual bool CopyTo(int index)
        {
            return false;
        }

        public virtual bool Import()
        {
            throw new NotImplementedException();
        }
        public virtual bool ImportFile(string filePath)
        {
            throw new NotImplementedException();
        }

        public bool IsSideHide { get; set; }

        public virtual void PreviewMouseDoubleClick(int index)
        {

        }

        public virtual string InitialDirectory { get; set; } 

        public virtual void Load() { }

        public virtual void Delete(int index)
        {
        }

        public virtual void Create(string templateName)
        {

        }

        public virtual void OpenCreate()
        {
            TemplateCreate createWindow = new TemplateCreate(this) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner };
            createWindow.ShowDialog();
        }

        public virtual bool ExitsTemplateName(string templateName) => TemplateControl.ExitsTemplateName(templateName);

        public virtual void Create(string templateCode, string templateName)
        {

        }

        public bool IsUserControl { get; set; }


        public virtual UserControl GetUserControl()
        {
            throw new NotImplementedException();
        }
        public virtual UserControl CreateUserControl()
        {
            return new UserControl();
        }

        public virtual void SetUserControlDataContext(int index)
        {
            throw new NotImplementedException();
        }
    }

    public class ITemplate<T> : ITemplate where T : ParamModBase, new() 
    {

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
            List<ModDetailModel> list = new();
            List<SysDictionaryModDetaiModel> sysDic = SysDictionaryModDetailDao.Instance.GetAllByPid(TemplateDicId);
            foreach (var item in sysDic)
            {
                list.Add(new ModDetailModel() { SysPid = item.Id, Pid = -1, ValueA = item.DefaultValue });
            }

            ModMasterModel modMaster = new ModMasterModel() { Pid = TemplateDicId, Name = "", TenantId = UserConfig.Instance.TenantId };
            CreateTemp = (T)Activator.CreateInstance(typeof(T), new object[] { modMaster, list });

            if (ImportTemp != null)
                CreateTemp?.CopyFrom(ImportTemp);
            return CreateTemp ?? new T();
        }


        public virtual void Save(TemplateModel<T> item)
        {
            item.Value.ModMaster.Name = item.Value.Name;
            Db.Updateable(item.Value.ModMaster).ExecuteCommand();
            var details = new List<ModDetailModel>();
            item.Value.GetDetail(details);
            Db.Updateable(details).ExecuteCommand();
        }

        public override void Save()
        {
            if (SaveIndex.Count == 0) return;

            foreach (var index in SaveIndex)
            {
                if(index >-1 && index < TemplateParams.Count)
                {
                    var item = TemplateParams[index];

                    item.Value.ModMaster.Name = item.Value.Name;
                    Db.Updateable(item.Value.ModMaster).ExecuteCommand();

                    var details = new List<ModDetailModel>();
                    item.Value.GetDetail(details);
                    Db.Updateable(details).ExecuteCommand();
                }
            }
        }

        public override void Load()
        { 
            SaveIndex.Clear();
            var backup = TemplateParams.ToDictionary(tp => tp.Id, tp => tp);

            if (MySqlSetting.Instance.IsUseMySql && MySqlSetting.IsConnect)
            {
                List<ModMasterModel> smus = Db.Queryable<ModMasterModel>().Where(x => x.Pid == TemplateDicId).Where(x => x.TenantId == UserConfig.Instance.TenantId).Where(x => x.IsDelete == false).ToList();
                foreach (var dbModel in smus)
                {
                    
                    List<ModDetailModel> smuDetails = Db.Queryable<ModDetailModel>().Where(x => x.Pid == dbModel.Id).ToList();
                    if (dbModel != null && smuDetails != null)
                    {
                        if (Activator.CreateInstance(typeof(T), new object[] { dbModel, smuDetails }) is T t)
                        {
                            if (backup.TryGetValue(t.Id, out var model))
                            {
                                model.Value = t;
                                model.Key = t.Name;
                            }
                            else
                            {
                                var templateModel = new TemplateModel<T>(dbModel.Name ?? "default", t);
                                TemplateParams.Add(templateModel);
                            }
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
                List<ModDetailModel> de = Db.Queryable<ModDetailModel>().Where(x => x.Pid == id).ToList();
                int ret = Db.Deleteable<ModMasterModel>().Where(x => x.Id == id).ExecuteCommand();
                Db.Deleteable<ModDetailModel>().Where(x => x.Pid == id).ExecuteCommand();
            }

            if (selectedCount <= 1)
            {
                int id = TemplateParams[index].Value.Id;
                DeleteSingle(id);
                TemplateParams.RemoveAt(index);
            }
            else
            {
                foreach (var item in TemplateParams.Where(item => item.IsSelected == true).ToList())
                {
                    DeleteSingle(item.Id);
                    TemplateParams.Remove(item);
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
                sfd.Title = "导出模板";
                sfd.InitialDirectory = SolutionManager.GetInstance().CurrentSolutionExplorer.DirectoryInfo.FullName;
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
        public override bool CopyTo(int index)
        {
            string fileContent = TemplateParams[index].Value.ToJsonN();
            ImportTemp = JsonConvert.DeserializeObject<T>(fileContent);
            if (ImportTemp != null)
            {
                ImportTemp.Id = -1;
            }
            return true;
        }

        public T? ImportTemp { get; set; }

        public override bool Import()
        {
            System.Windows.Forms.OpenFileDialog ofd = new System.Windows.Forms.OpenFileDialog();
            ofd.Filter = "*.cfg|*.cfg";
            ofd.Title = "导入模板";
            ofd.RestoreDirectory = true;
            ofd.InitialDirectory = SolutionManager.GetInstance().CurrentSolutionExplorer.DirectoryInfo.FullName;
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
                T Temp = JsonConvert.DeserializeObject<T>(fileContent);
                foreach (var item in Temp.ModDetailModels)
                {
                    CreateTemp.ModDetailModels.First(a => a.SysPid == item.SysPid).ValueA = item.ValueA;
                }
                ImportTemp = CreateTemp;
                return true;
            }
            catch (JsonException ex)
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), $"解析模板文件时出错: {ex.Message}", "ColorVision");
                return false;
            }
        }


        public T? AddParamMode(string Name, int resourceId = -1)
        {
            ModMasterModel modMaster = new ModMasterModel() { Pid = TemplateDicId, Name = Name, TenantId = UserConfig.Instance.TenantId };
            if (resourceId > 0)
                modMaster.ResourceId = resourceId;
            int id = Db.Insertable(modMaster).ExecuteReturnIdentity();
            modMaster.Id = id;

            List<ModDetailModel> details = new();
            foreach (var item in SysDictionaryModDetailDao.Instance.GetAllByPid(TemplateDicId))
            {
                details.Add(new ModDetailModel() { SysPid = item.Id, Pid = modMaster.Id, ValueA = item.DefaultValue });
            }
            Db.Deleteable<ModDetailModel>().Where(x => x.Pid == modMaster.Id).ExecuteCommand();
            Db.Insertable(details).ExecuteCommand();


            if (modMaster.Id > 0)
            {
                ModMasterModel modMasterModel = Db.Queryable<ModMasterModel>().InSingle(modMaster.Id);
                List<ModDetailModel> modDetailModels = Db.Queryable<ModDetailModel>().Where(x => x.Pid == modMaster.Id).ToList();
                if (modMasterModel != null)
                    return (T)Activator.CreateInstance(typeof(T), new object[] { modMasterModel, modDetailModels });
            }
            return null;
        }

        public override void Create(string templateName)
        {
            T? AddParamMode()
            {
                ModMasterModel modMaster = new ModMasterModel() { Pid = TemplateDicId, Name = templateName, TenantId = UserConfig.Instance.TenantId };
                int id = Db.Insertable(modMaster).ExecuteReturnIdentity();
                modMaster.Id = id;

                List<ModDetailModel> details = new();
                if (CreateTemp != null)
                {
                    CreateTemp.GetDetail(details);
                    foreach (var item in details)
                    {
                        item.Pid = modMaster.Id;
                    }
                }
                else
                {
                    List<SysDictionaryModDetaiModel> sysDic = SysDictionaryModDetailDao.Instance.GetAllByPid(TemplateDicId);
                    foreach (var item in sysDic)
                    {
                        details.Add(new ModDetailModel() { SysPid = item.Id, Pid = -1, ValueA = item.DefaultValue });
                    }
                }

                Db.Deleteable<ModDetailModel>().Where(x => x.Pid == modMaster.Id).ExecuteCommand();
                Db.Insertable(details).ExecuteCommand();

                List<ModDetailModel> modDetailModels = Db.Queryable<ModDetailModel>().Where(x => x.Pid == modMaster.Id).ToList();
                return (T)Activator.CreateInstance(typeof(T), new object[] { modMaster, modDetailModels });

            }
            T? param = AddParamMode();
            if (ImportTemp != null) ImportTemp = null;
            if (param != null)
            {
                var a = new TemplateModel<T>(templateName, param);
                TemplateParams.Add(a);
            }
            else
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), $"数据库创建{typeof(T)}模板失败", "ColorVision");
                if (GetMysqlCommand() is IMysqlCommand  mysqlCommand)
                {
                    if (MessageBox.Show(Application.Current.GetActiveWindow(), $"是否重置数据库{typeof(T)}相关项", "ColorVision", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                    {
                        MySqlControl.GetInstance().BatchExecuteNonQuery(mysqlCommand.GetRecover());
                    }
                }
            }
        }
    }
}
