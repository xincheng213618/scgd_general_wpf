using ColorVision.Database;
using ColorVision.Engine.Templates;
using ColorVision.Engine.Templates.POI;
using ColorVision.Engine.Templates.POI.AlgorithmImp;
using SqlSugar;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Conoscope.ApplicationServices.FocusPoints
{
    /// <summary>
    /// Owns the database boundary for focus-point POI templates. The view decides
    /// how failures are presented and never constructs a database connection.
    /// </summary>
    internal static class FocusPoiTemplateRepository
    {
        public static bool IsAvailable => MySqlControl.GetInstance().IsConnect;

        public static ObservableCollection<TemplateModel<PoiParam>> Load()
        {
            new TemplatePoi().Load();
            return TemplatePoi.Params.CreateEmpty();
        }

        public static PoiParam? GetOrCreate(PoiParam? selectedTemplate, string templateName)
        {
            if (selectedTemplate?.Id > 0)
            {
                return selectedTemplate;
            }

            TemplatePoi templates = new();
            templates.Load();
            templates.Create(templateName);
            templates.Load();
            return TemplatePoi.Params.LastOrDefault(item => item.Key == templateName)?.Value;
        }

        public static void LoadDetails(PoiParam template)
        {
            PoiParam.LoadPoiDetailFromDB(template);
        }

        public static bool Save(PoiParam template)
        {
            PoiMasterModel master = new(template);
            if (PoiMasterDao.Instance.Save(master) == -1)
            {
                return false;
            }

            if (template.Id <= 0 && master.Id > 0)
            {
                template.Id = master.Id;
            }

            List<PoiDetailModel> details = template.PoiPoints
                .Select(point => new PoiDetailModel(template.Id, point) { Id = 0 })
                .ToList();
            using SqlSugarClient database = new(new ConnectionConfig
            {
                ConnectionString = MySqlControl.GetConnectionString(),
                DbType = DbType.MySql,
                IsAutoCloseConnection = true
            });

            database.Ado.BeginTran();
            try
            {
                database.Deleteable<PoiDetailModel>().Where(item => item.Pid == template.Id).ExecuteCommand();
                if (details.Count > 0)
                {
                    database.Insertable(details).ExecuteCommand();
                }

                database.Ado.CommitTran();
                return true;
            }
            catch
            {
                database.Ado.RollbackTran();
                throw;
            }
        }
    }
}
