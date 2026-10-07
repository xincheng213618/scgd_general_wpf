using ColorVision.Database;
using ColorVision.Engine.Templates.Flow;
using ColorVision.Engine.Templates.POI;
using log4net;
using SqlSugar;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace ColorVision.Engine.Templates;

/// <summary>Persist ordering before publishing new identities to the shared template collection.</summary>
internal static class TemplateOrderSwap
{
    private static readonly ILog log = LogManager.GetLogger(typeof(TemplateOrderSwap));

    internal static async Task<bool> SwapAsync<T>(ITemplate template, ObservableCollection<TemplateModel<T>> items,
        int firstIndex, int secondIndex, bool background) where T : ParamBase
    {
        if (firstIndex < 0 || secondIndex < 0 || firstIndex >= items.Count || secondIndex >= items.Count) return false;
        if (firstIndex == secondIndex) return true;
        var first = items[firstIndex];
        var second = items[secondIndex];
        int firstId = first.Id, secondId = second.Id;
        var saved = template.SaveIndex.Where(index => index >= 0 && index < items.Count).Select(index => items[index]).ToArray();
        try
        {
            Action persist;
            bool renumber = firstId > 0 && secondId > 0;
            if (first.Value is FlowParam localFlow && second.Value is FlowParam
                && LocalFlowTemplateStorage.IsLocalId(firstId) && LocalFlowTemplateStorage.IsLocalId(secondId))
                persist = () => (localFlow.LocalStorage ?? LocalFlowTemplateStorage.Default).SwapOrder(firstId, secondId);
            else if (first.Value is PoiParam localPoi && second.Value is PoiParam
                && PoiTemplateStorage.IsLocalId(firstId) && PoiTemplateStorage.IsLocalId(secondId))
                persist = () => (localPoi.Storage ?? PoiTemplateStorage.Default).SwapLocalOrder(firstId, secondId);
            else if (renumber)
            {
                string connection = MySqlControl.GetConnectionString();
                string firstName = first.Key, secondName = second.Key;
                bool poi = first.Value is PoiParam;
                persist = () =>
                {
                    using var db = new SqlSugarClient(new ConnectionConfig { ConnectionString = connection, DbType = DbType.MySql, IsAutoCloseConnection = true });
                    SwapRecords(db, poi, firstId, secondId, firstName, secondName);
                };
            }
            else return false;

            if (background) await Task.Run(persist);
            else persist();

            if (renumber)
            {
                Remap(first.Value, secondId);
                Remap(second.Value, firstId);
                first.NotifyIdChanged();
                second.NotifyIdChanged();
            }
            // Move preserves unique objects throughout the swap, including ComboBox selections.
            int low = Math.Min(firstIndex, secondIndex), high = Math.Max(firstIndex, secondIndex);
            items.Move(high, low);
            if (high > low + 1) items.Move(low + 1, high);
            template.SaveIndex.Clear();
            foreach (var item in saved) template.SetSaveIndex(items.IndexOf(item));
            return true;
        }
        catch (Exception ex)
        {
            log.Error("Swapping template database order failed.", ex);
            return false;
        }
    }

    private static void Remap(ParamBase value, int id)
    {
        value.Id = id;
        if (value is ParamModBase model)
        {
            if (model.ModMaster != null) model.ModMaster.Id = id;
            foreach (var detail in model.ModDetailModels) detail.Pid = id;
        }
        if (value is PoiParam poi)
            foreach (var point in poi.PoiPoints) point.Pid = id;
    }

    internal static void SwapRecords(SqlSugarClient db, bool poi, int first, int second, string firstName, string secondName)
    {
        if (first <= 0 || second <= 0 || first == second) throw new ArgumentException("Two distinct server template IDs are required.");
        string master = poi ? "t_scgd_algorithm_poi_template_master" : "t_scgd_mod_param_master";
        string detail = poi ? "t_scgd_algorithm_poi_template_detail" : "t_scgd_mod_param_detail";
        db.Ado.BeginTran();
        try
        {
            // Lock in ID order. Explicitly update the old primary key; Updateable(entity)
            // uses the entity's NEW ID as its WHERE clause and overwrites the other row.
            string locking = db.CurrentConnectionConfig.DbType == DbType.MySql ? " FOR UPDATE" : "";
            var records = db.Ado.SqlQuery<OrderRecord>($"SELECT id,name FROM `{master}` WHERE id IN (@first,@second) ORDER BY id{locking}",
                new SugarParameter("@first", first), new SugarParameter("@second", second));
            if (records.Count != 2 || records.Single(row => row.Id == first).Name != firstName || records.Single(row => row.Id == second).Name != secondName)
                throw new InvalidOperationException("模板已被修改或删除，请重新打开模板管理后排序。");

            int temporary = -Random.Shared.Next(1, int.MaxValue);
            if (db.Ado.GetInt($"SELECT COUNT(*) FROM `{master}` WHERE id=@temporary", new SugarParameter("@temporary", temporary)) != 0)
                throw new InvalidOperationException("临时排序序号已被占用，请重试。");
            MoveId(first, temporary);
            MoveId(second, first);
            MoveId(temporary, second);
            int expectedDetails = db.Ado.GetInt($"SELECT COUNT(*) FROM `{detail}` WHERE pid IN (@first,@second)",
                new SugarParameter("@first", first), new SugarParameter("@second", second));
            int changedDetails = db.Ado.ExecuteCommand($"UPDATE `{detail}` SET pid=CASE WHEN pid=@first THEN @second ELSE @first END WHERE pid IN (@first,@second)",
                new SugarParameter("@first", first), new SugarParameter("@second", second));
            if (changedDetails != expectedDetails) throw new InvalidOperationException("模板明细交换未完成，已回滚。");
            db.Ado.CommitTran();
        }
        catch
        {
            db.Ado.RollbackTran();
            throw;
        }

        void MoveId(int from, int to)
        {
            int changed = db.Ado.ExecuteCommand($"UPDATE `{master}` SET id=@to WHERE id=@from", new SugarParameter("@to", to), new SugarParameter("@from", from));
            if (changed != 1) throw new InvalidOperationException("模板序号交换未完成，已回滚。");
        }
    }

    private sealed class OrderRecord
    {
        public int Id { get; set; }
        public string? Name { get; set; }
    }
}
