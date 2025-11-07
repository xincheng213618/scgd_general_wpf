using ColorVision.Database;
using ColorVision.UI.Extension;
using Newtonsoft.Json;
using System;
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
            Title = ColorVision.Engine.Properties.Resources.POISetting;
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
                List<PoiMasterModel> poiMasters = PoiMasterDao.Instance.GetAllByParam(new Dictionary<string, object>() { { "tenant_id", 0}, { "is_delete", 0 } });
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
            Db.Deleteable<PoiMasterModel>().Where(it => it.Id == TemplateParams[index].Value.Id).ExecuteCommand();
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
                    int count = MySqlControl.GetInstance().DB.Insertable(poiDetails).ExecuteCommand();
                    sw2.Stop();


                    ImportTemp.Id = poiMasterModel.Id;
                    return ImportTemp;
                }
                else
                {
                    PoiMasterModel poiMasterModel = new PoiMasterModel() { Name =templateName, TenantId = 0};
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

        public override bool SwapTemplateOrder(int index1, int index2)
        {
            if (index1 < 0 || index1 >= TemplateParams.Count || index2 < 0 || index2 >= TemplateParams.Count)
                return false;

            if (index1 == index2)
                return true;

            try
            {
                var template1 = TemplateParams[index1];
                var template2 = TemplateParams[index2];

                // Get the IDs from database
                int id1 = template1.Value.Id;
                int id2 = template2.Value.Id;

                // Swap the IDs in the database using a three-step process to avoid constraint violations
                // Use int.MinValue plus a hash-based offset incorporating both IDs to minimize collision risk
                int tempId = int.MinValue + Math.Abs((id1 ^ id2).GetHashCode());

                // Step 1: Move template1 to temporary ID
                var poiMaster1 = Db.Queryable<PoiMasterModel>().InSingle(id1);
                if (poiMaster1 != null)
                {
                    poiMaster1.Id = tempId;
                    Db.Updateable(poiMaster1).ExecuteCommand();
                }

                var poiDetails1 = Db.Queryable<PoiDetailModel>().Where(x => x.Pid == id1).ToList();
                foreach (var detail in poiDetails1)
                {
                    detail.Pid = tempId;
                }
                if (poiDetails1.Count > 0)
                    Db.Updateable(poiDetails1).ExecuteCommand();

                // Step 2: Move template2 to id1
                var poiMaster2 = Db.Queryable<PoiMasterModel>().InSingle(id2);
                if (poiMaster2 != null)
                {
                    poiMaster2.Id = id1;
                    Db.Updateable(poiMaster2).ExecuteCommand();
                }

                var poiDetails2 = Db.Queryable<PoiDetailModel>().Where(x => x.Pid == id2).ToList();
                foreach (var detail in poiDetails2)
                {
                    detail.Pid = id1;
                }
                if (poiDetails2.Count > 0)
                    Db.Updateable(poiDetails2).ExecuteCommand();

                // Step 3: Move template1 from temporary to id2
                poiMaster1 = Db.Queryable<PoiMasterModel>().InSingle(tempId);
                if (poiMaster1 != null)
                {
                    poiMaster1.Id = id2;
                    Db.Updateable(poiMaster1).ExecuteCommand();
                }

                poiDetails1 = Db.Queryable<PoiDetailModel>().Where(x => x.Pid == tempId).ToList();
                foreach (var detail in poiDetails1)
                {
                    detail.Pid = id2;
                }
                if (poiDetails1.Count > 0)
                    Db.Updateable(poiDetails1).ExecuteCommand();

                // Update the in-memory values
                template1.Value.Id = id2;
                template2.Value.Id = id1;

                // Swap the items in the ObservableCollection using proper swap
                var temp = TemplateParams[index1];
                TemplateParams[index1] = TemplateParams[index2];
                TemplateParams[index2] = temp;

                return true;
            }
            catch (System.Exception)
            {
                // Let the caller handle the error display
                return false;
            }
        }
    }

}
