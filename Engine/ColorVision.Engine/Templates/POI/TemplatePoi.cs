using ColorVision.Database;
using ColorVision.Database;
using ColorVision.Engine.Rbac;
using ColorVision.Engine.Templates.POI.Dao;
using ColorVision.UI.Extension;
using Newtonsoft.Json;
using NPOI.SS.Formula.Functions;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;

namespace ColorVision.Engine.Templates.POI
{
    public class TemplatePoi : ITemplate<PoiParam>, IITemplateLoad
    {
        public static ObservableCollection<TemplateModel<PoiParam>> Params { get; set; } = new ObservableCollection<TemplateModel<PoiParam>>();

        public TemplatePoi()
        {
            IsSideHide = true;
            TemplateDicId = -1;
            Title = "关注点设置";
            Code = "POI";
            TemplateParams = Params;
        }
        public EditPoiParam EditWindow { get; set; }
        public override void PreviewMouseDoubleClick(int index)
        {
            EditWindow = new EditPoiParam(Params[index].Value) { Owner = Application.Current.GetActiveWindow() };
            EditWindow.ShowDialog();
        }

        public override void Load()
        {
            var backup = Params.ToDictionary(tp => tp.Id, tp => tp);
            if (MySqlSetting.Instance.IsUseMySql && MySqlSetting.IsConnect)
            {
                List<PoiMasterModel> poiMasters = PoiMasterDao.Instance.GetAllByParam(new Dictionary<string, object>() { { "tenant_id", UserConfig.Instance.TenantId }, { "is_delete", 0 } });
                foreach (var dbModel in poiMasters)
                {
                    var poiparam = new PoiParam(dbModel);
                    if (backup.TryGetValue(poiparam.Id, out var model))
                    {
                        model.Value = poiparam;
                        model.Key = poiparam.Name;
                    }
                    else
                    {
                        Params.Add(new TemplateModel<PoiParam>(dbModel.Name ?? "default", poiparam));
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
                    PoiMasterDao.Instance.Save(new PoiMasterModel(item.Value));
                }
            }
            SaveIndex.Clear();
        }
        public override void Delete(int index)
        {
            PoiMasterDao poiMasterDao = new();
            poiMasterDao.DeleteById(TemplateParams[index].Value.Id);
            TemplateParams.RemoveAt(index);
        }


        public override bool CopyTo(int index)
        {
            PoiParam.LoadPoiDetailFromDB(TemplateParams[index].Value);
            string fileContent = TemplateParams[index].Value.ToJsonN();
            ImportTemp = JsonConvert.DeserializeObject<PoiParam>(fileContent);
            if (ImportTemp != null)
            {
                ImportTemp.Id = -1;
                foreach (var item in ImportTemp.PoiPoints)
                {
                    item.Id = -1;
                }
            }
            return true;
        }


        public override void Create(string templateName)
        {
            PoiParam? AddPoiParam(string templateName)
            {
                if(ImportTemp != null)
                {
                    ImportTemp.Name = templateName;
                    PoiMasterModel poiMasterModel = new PoiMasterModel(ImportTemp);
                    PoiMasterDao.Instance.Save(poiMasterModel);
                    List<PoiDetailModel> poiDetails = new List<PoiDetailModel>();
                    foreach (PoiPoint pt in ImportTemp.PoiPoints)
                    {
                        PoiDetailModel poiDetail = new PoiDetailModel(poiMasterModel.Id, pt);
                        poiDetails.Add(poiDetail);
                    }

                    var db = MySqlControl.GetInstance().DB;
                    Stopwatch sw2 = Stopwatch.StartNew();
                    db.Deleteable<PoiDetailModel>().Where(x => x.Pid == poiMasterModel.Id).ExecuteCommand();
                    int count = MySqlControl.GetInstance().DB.Fastest<PoiDetailModel>().BulkCopy(poiDetails);
                    sw2.Stop();


                    ImportTemp.Id = poiMasterModel.Id;
                    return ImportTemp;
                }
                else
                {
                    PoiMasterModel poiMasterModel = new PoiMasterModel() { Name =templateName, TenantId = UserConfig.Instance.TenantId };
                    PoiMasterDao.Instance.Save(poiMasterModel);

                    int pkId = poiMasterModel.Id;
                    if (pkId > 0)
                    {
                        PoiMasterModel model = PoiMasterDao.Instance.GetById(pkId);
                        if (model != null) return new PoiParam(model);
                        else return null;
                    }
                    return null;
                }


            }


            PoiParam? param = AddPoiParam(templateName);
            if (param != null)
            {
                var a = new TemplateModel<PoiParam>(templateName, param);
                TemplateParams.Add(a);
            }
            else
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), $"数据库创建{typeof(PoiParam)}模板失败", "ColorVision");
            }
        }


        public override void Export(int index)
        {
            PoiParam.LoadPoiDetailFromDB(TemplateParams[index].Value);
            base.Export(index);
        }

        public override bool Import()
        {
            System.Windows.Forms.OpenFileDialog ofd = new System.Windows.Forms.OpenFileDialog();
            ofd.Filter = "*.cfg|*.cfg";
            ofd.Title = "导入模板";
            ofd.RestoreDirectory = true;
            if (ofd.ShowDialog() != System.Windows.Forms.DialogResult.OK) return false;
            //if (TemplateParams.Any(a => a.Key.Equals(System.IO.Path.GetFileNameWithoutExtension(ofd.FileName), StringComparison.OrdinalIgnoreCase)))
            //{
            //    MessageBox.Show(Application.Current.GetActiveWindow(), "模板名称已存在", "ColorVision");
            //    return false;
            //}
            byte[] fileBytes = File.ReadAllBytes(ofd.FileName);
            string fileContent = System.Text.Encoding.UTF8.GetString(fileBytes);
            try
            {
                ImportTemp = JsonConvert.DeserializeObject<PoiParam>(fileContent);
                if (ImportTemp !=null)
                {
                    ImportTemp.Id = -1;
                    foreach (var item in ImportTemp.PoiPoints)
                    {
                        item.Id = -1;
                    }
                }
                return true;
            }
            catch (JsonException ex)
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), $"解析模板文件时出错: {ex.Message}", "ColorVision");
                return false;
            }
        }

    }

}
