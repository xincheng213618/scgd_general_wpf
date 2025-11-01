using ColorVision.Common.Utilities;
using ColorVision.Database;
using ColorVision.Engine.Templates.Menus;
using ColorVision.UI.Extension;
using ColorVision.UI.Menus;
using Newtonsoft.Json;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Windows;

namespace ColorVision.Engine.Templates.Flow
{
    public class MenuTemplateFlow : MenuItemBase
    {
        public override string OwnerGuid => nameof(MenuTemplate);
        public override int Order => 0;
        public override string Header => Properties.Resources.MenuFlow;
        public override void Execute()
        {
            new TemplateEditorWindow(new TemplateFlow()) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog(); ;
        }
    }

    public class TemplateFlow : ITemplate<FlowParam>, IITemplateLoad
    {
        public static ObservableCollection<TemplateModel<FlowParam>> Params { get; set; } = new ObservableCollection<TemplateModel<FlowParam>>();


        public TemplateFlow()
        {
            IsSideHide = true;
            Title = "流程引擎模板管理";
            Code = "flow";
            TemplateParams = Params;
        }

        public override void PreviewMouseDoubleClick(int index)
        {
            new FlowEngineToolWindow(TemplateParams[index].Value) { Owner = Application.Current.GetActiveWindow() }.Show();
        }
        public override bool ExitsTemplateName(string templateName)
        {
            return Params.Any(a => a.Key.Equals(templateName, StringComparison.OrdinalIgnoreCase));
        }

        public override void Load()
        {
            
            var backup = TemplateParams.ToDictionary(tp => tp.Id, tp => tp);
            if (MySqlSetting.Instance.IsUseMySql && MySqlSetting.IsConnect)
            {
                List<ModMasterModel> flows = MySqlControl.GetInstance().DB.Queryable<ModMasterModel>().Where(x => x.Pid == 11).Where(x => x.TenantId == 0).Where(x => x.IsDelete == false).ToList();
                foreach (var dbModel in flows)
                {
                    var details = Db.Queryable<ModDetailModel>().Where(x=>x.Pid == dbModel.Id)
                        .Select(it => new ModDetailModel
                        {
                            SysPid = it.SysPid,
                            Pid = it.Pid,
                            ValueA = it.ValueA,
                            ValueB = it.ValueB,
                            IsEnable = it.IsEnable,
                            IsDelete = it.IsDelete,
                            Value = SqlFunc.Subqueryable<SysResourceModel>()
                                .Where(r => r.Id == SqlFunc.ToInt32(it.ValueA))
                                .Select(r => r.Value)     
                        })
                        .ToList();



                    var param = new FlowParam(dbModel, details);

                    if (backup.TryGetValue(param.Id, out var model))
                    {
                        model.Value = param;
                        model.Key = param.Name;

                    }
                    else
                    {
                        var item = new TemplateModel<FlowParam>(dbModel.Name ?? "default", param);
                        TemplateParams.Add(item);
                    }
                }
            }
            SaveIndex.Clear();
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
                foreach (ModDetailModel model in de)
                {
                    string code = Cryptography.GetMd5Hash(model.ValueA + model.Id);
                    ret = Db.Deleteable<SysResourceModel>().Where(x => x.Code == code).ExecuteCommand();
                }
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

        public override void Save()
        {
            if (SaveIndex.Count == 0) return;

            foreach (var index in SaveIndex)
            {
                if (index > -1 && index < TemplateParams.Count)
                {
                    var item = TemplateParams[index];
                    Save2DB(item.Value);
                }
            }
        }

        public static void Save2DB(FlowParam flowParam)
        {
            var db = MySqlControl.GetInstance().DB;

            flowParam.ModMaster.Name = flowParam.Name;
            db.Updateable(flowParam.ModMaster).ExecuteCommand();

            List<ModDetailModel> details = new();
            flowParam.GetDetail(details);
            if (details.Count > 0)
            {
                var model = details[0];
                SysResourceModel res = null;
                int id = 0;
                bool hasId = int.TryParse(model.ValueA, out id);
                if (hasId)
                {
                    res = db.Queryable<SysResourceModel>().InSingle(id);
                }

                if (res != null)
                {
                    // 资源已存在，更新
                    res.Code = flowParam.Id + Cryptography.GetMd5Hash(flowParam.DataBase64);
                    res.Name = flowParam.Name;
                    res.Value = flowParam.DataBase64;
                    db.Updateable(res).ExecuteCommand();
                    model.ValueA = res.Id.ToString();
                }
                else
                {
                    // 新建资源
                    res = new SysResourceModel
                    {
                        Name = flowParam.Name,
                        Type = 101,
                        Value = flowParam.DataBase64,
                        Code = hasId
                            ? (flowParam.Id + Cryptography.GetMd5Hash(flowParam.DataBase64))
                            : Cryptography.GetMd5Hash(flowParam.DataBase64)
                    };
                    db.Insertable(res).ExecuteCommand();
                    // 获取新资源id（SqlSugar自动回写Id）
                    model.ValueA = res.Id.ToString();
                }

                // 3. 更新明细表
                db.Updateable(details)
                    .Where(md => md.Pid == flowParam.Id)
                    .ExecuteCommand();
            }
        }


        public override void Export(int index)
        {
            int selectedCount = TemplateParams.Count(item => item.IsSelected);
            if (selectedCount == 1) index = TemplateParams.IndexOf(TemplateParams.First(item => item.IsSelected));

            if (selectedCount <= 1)
            {
                System.Windows.Forms.SaveFileDialog sfd = new System.Windows.Forms.SaveFileDialog();
                sfd.DefaultExt = "stn";
                sfd.Filter = "*.stn|*.stn";
                sfd.AddExtension = true;
                sfd.RestoreDirectory = true;
                sfd.Title = "导出流程";
                sfd.FileName = Tool.SanitizeFileName(TemplateParams[index].Key);
                if (sfd.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
                byte[] fileBytes = Convert.FromBase64String(TemplateParams[index].Value.DataBase64);
                File.WriteAllBytes(sfd.FileName, fileBytes);
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
                        string filePath = Path.Combine(tempDirectory, $"{Tool.SanitizeFileName(kvp.Key)}.stn");
                        byte[] fileBytes = Convert.FromBase64String(TemplateParams[index].Value.DataBase64);
                        File.WriteAllBytes(filePath, fileBytes);
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

        public override bool Import()
        {
            System.Windows.Forms.OpenFileDialog ofd = new System.Windows.Forms.OpenFileDialog();
            ofd.Filter = "*.stn|*.stn";
            ofd.Title = "导入流程";
            ofd.RestoreDirectory = true;
            if (ofd.ShowDialog() != System.Windows.Forms.DialogResult.OK) return false;
            return ImportFile(ofd.FileName);
        }

        public override bool ImportFile(string filePath)
        {
            if (!File.Exists(filePath)) return false;
            byte[] fileBytes = File.ReadAllBytes(filePath);
            string base64 = Convert.ToBase64String(fileBytes);
            FlowParam param = new FlowParam();
            param.DataBase64 = base64;
            ImportTemp = param;
            return true;
        }



        public override bool CopyTo(int index)
        {
            if (index > -1 && index < TemplateParams.Count)
            {
                string fileContent = TemplateParams[index].Value.ToJsonN();
                ImportTemp = JsonConvert.DeserializeObject<FlowParam>(fileContent);
                if (ImportTemp != null)
                {
                    ImportTemp.Id = -1;
                }
                return true;
            }
            return false;
        }

        public override void Create(string templateName)
        {
            FlowParam? param = AddFlowParam(templateName);
            if (param != null)
            {
                if (ImportTemp != null)
                {
                    param.DataBase64 = ImportTemp.DataBase64;
                    Save2DB(param);
                    ImportTemp = null;
                }
                var a = new TemplateModel<FlowParam>(templateName, param);
                TemplateParams.Add(a);
            }
            else
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), $"数据库创建{typeof(FlowParam)}模板失败", "ColorVision");
            }
        }
        public FlowParam? AddFlowParam(string templateName)
        {
            var flowMaster = new ModMasterModel() { Pid = 11, Name = templateName, TenantId = 0};
            int id = Db.Insertable(flowMaster).ExecuteReturnIdentity(); // 自增id自动回写
            flowMaster.Id = id;

            List<ModDetailModel> list = new List<ModDetailModel>();
            foreach (var item in SysDictionaryModDetailDao.Instance.GetAllByPid(flowMaster.Pid))
                list.Add(new ModDetailModel() { SysPid = item.Id, Pid = flowMaster.Id, ValueA = item.DefaultValue });

            Db.Deleteable<ModDetailModel>().Where(x => x.Pid == flowMaster.Id).ExecuteCommand();
            Db.Insertable(list).ExecuteCommand();

            int pkId = flowMaster.Id;
            if (pkId > 0)
            {
                var flowDetail = Db.Queryable<ModDetailModel>().Where(it => it.Pid == pkId).ToList();

                if (flowDetail.Count > 0 && int.TryParse(flowDetail[0].ValueA, out int sid))
                {
                    var sysResourceModeldefault = Db.Queryable<SysResourceModel>().InSingle(sid);
                    if (sysResourceModeldefault != null)
                    {
                        flowDetail[0].Value = sysResourceModeldefault.Value;
                        var sysResourceModel = new SysResourceModel
                        {
                            Name = flowMaster.Name,
                            Code = pkId.ToString() + sysResourceModeldefault.Code,
                            Type = sysResourceModeldefault.Type,
                            Value = sysResourceModeldefault.Value
                        };
                        id = Db.Insertable(sysResourceModel).ExecuteReturnIdentity();

                        flowDetail[0].ValueA = id.ToString();
                        Db.Updateable(flowDetail[0]).ExecuteCommand();

                    }
                }
                return new FlowParam(flowMaster, flowDetail);
            }
            return null;
        }
    }
}
