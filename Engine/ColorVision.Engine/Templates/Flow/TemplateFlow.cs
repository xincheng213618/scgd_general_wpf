﻿#pragma warning disable CA1822
using ColorVision.Common.Utilities;
using ColorVision.Engine.MySql;
using ColorVision.Engine.MySql.ORM;
using ColorVision.Engine.Rbac;
using ColorVision.Engine.Services.Dao;
using ColorVision.Engine.Templates.SysDictionary;
using ColorVision.UI.Extension;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Windows;

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

        private static ModMasterDao masterFlowDao = new ModMasterDao(11);
        public override void Load()
        {
            var backup = TemplateParams.ToDictionary(tp => tp.Id, tp => tp);
            if (MySqlSetting.Instance.IsUseMySql && MySqlSetting.IsConnect)
            {
                List<ModMasterModel> flows = masterFlowDao.GetAll(UserConfig.Instance.TenantId);
                foreach (var dbModel in flows)
                {
                    List<ModFlowDetailModel> flowDetails = ModFlowDetailDao.Instance.GetAllByPid(dbModel.Id);


                    var param = new FlowParam(dbModel, flowDetails);

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
                    FlowParam.Save2DB(item.Value);
                }
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
            ExportTemp = param;
            return true;
        }



        public override bool CopyTo(int index)
        {
            if (index > -1 && index < TemplateParams.Count)
            {
                string fileContent = TemplateParams[index].Value.ToJsonN();
                ExportTemp = JsonConvert.DeserializeObject<FlowParam>(fileContent);
                if (ExportTemp != null)
                {
                    ExportTemp.Id = -1;
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
                if (ExportTemp != null)
                {
                    param.DataBase64 = ExportTemp.DataBase64;
                    param.Save();
                    ExportTemp = null;
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
            ModMasterModel flowMaster = new ModMasterModel(11, templateName, UserConfig.Instance.TenantId);
            ModMasterDao.Instance.Save(flowMaster);
            List<ModDetailModel> list = new();
            List<SysDictionaryModDetaiModel> sysDic = SysDictionaryModDetailDao.Instance.GetAllByPid(flowMaster.Pid);
            foreach (var item in sysDic)
            {
                list.Add(new ModDetailModel(item.Id, flowMaster.Id, item.DefaultValue));
            }
            ModDetailDao.Instance.SaveByPid(flowMaster.Id, list);

            int pkId = flowMaster.Id;
            if (pkId > 0)
            {
                List<ModFlowDetailModel> flowDetail = ModFlowDetailDao.Instance.GetAllByPid(pkId);
                if (int.TryParse(flowDetail[0].ValueA, out int id))
                {
                    SysResourceModel sysResourceModeldefault = VSysResourceDao.Instance.GetById(id);
                    if (sysResourceModeldefault != null)
                    {
                        SysResourceModel sysResourceModel = new SysResourceModel();
                        sysResourceModel.Name = flowMaster.Name;
                        sysResourceModel.Code = pkId.ToString()+ sysResourceModeldefault.Code;
                        sysResourceModel.Type = sysResourceModeldefault.Type;
                        sysResourceModel.Value = sysResourceModeldefault.Value;
                        SysResourceDao.Instance.Save(sysResourceModel);

                        flowDetail[0].ValueA = sysResourceModel.Id.ToString();
                        ModFlowDetailDao.Instance.Save(flowDetail[0]);
                    }
                }
                if (flowMaster != null) return new FlowParam(flowMaster, flowDetail);
                else return null;
            }
            return null;
        }



    }
}
