#pragma warning disable CA1822
using ColorVision.Common.Utilities;
using ColorVision.Engine.MySql;
using ColorVision.Engine.MySql.ORM;
using ColorVision.Engine.Rbac;
using ColorVision.Engine.Services.Dao;
using ColorVision.Engine.Templates.SysDictionary;
using ColorVision.UI.Extension;
using CVCommCore;
using Dm.util;
using Newtonsoft.Json;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Windows;
using static iText.StyledXmlParser.Jsoup.Select.Evaluator;

namespace ColorVision.Engine.Templates.Flow
{
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

        private static ModMasterDao masterFlowDao = new ModMasterDao(11);
        public override void Load()
        {
            
            var backup = TemplateParams.ToDictionary(tp => tp.Id, tp => tp);
            if (MySqlSetting.Instance.IsUseMySql && MySqlSetting.IsConnect)
            {
                List<ModMasterModel> flows = masterFlowDao.GetAll(UserConfig.Instance.TenantId);
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
            ModMasterDao modMasterDao = new ModMasterDao(11);
            modMasterDao.Save(flowParam.ModMaster);

            List<Templates.ModDetailModel> details = new();
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
                        Type = (int)PhysicalResourceType.FlowFile,
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

            var flowMaster = new ModMasterModel(11, templateName, UserConfig.Instance.TenantId);
            int id =Db.Insertable(flowMaster).ExecuteReturnIdentity(); // 自增id自动回写
            flowMaster.Id = id;

            List<Templates.ModDetailModel> list = new();
            List<SysDictionaryModDetaiModel> sysDic = SysDictionaryModDetailDao.Instance.GetAllByPid(flowMaster.Pid);
            foreach (var item in sysDic)
            {
                list.Add(new Templates.ModDetailModel(item.Id, flowMaster.Id, item.DefaultValue));
            }
            ModDetailDao.Instance.SaveByPid(flowMaster.Id, list);

            int pkId = flowMaster.Id;
            if (pkId > 0)
            {
                var flowDetail = Db.Queryable<ModDetailModel>()
                    .Where(it => it.Pid == pkId)
                    .ToList();

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

                        flowDetail[0].ValueA = id.toString();
                        Db.Updateable(flowDetail[0]).ExecuteCommand();

                    }
                }
                return new FlowParam(flowMaster, flowDetail);
            }
            return null;
        }
    }
}
