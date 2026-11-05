using SqlSugar;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ColorVision.Engine.Templates;

/// <summary>One load's detail snapshot, with legacy queries for failed or order-sensitive groups.</summary>
internal sealed class TemplateDetailReader
{
    internal const int ParentsPerBatch = 500;
    private readonly SqlSugarClient database;
    private readonly Func<int, string?> resolveSymbol;
    private readonly Dictionary<int, List<ModDetailModel>> details = new();
    private readonly HashSet<int> failedParents = new();

    internal TemplateDetailReader(SqlSugarClient database, IEnumerable<int> parentIds, Func<int, string?> resolveSymbol, bool preserveDetailReadOrder)
    {
        this.database = database;
        this.resolveSymbol = resolveSymbol;
        // Ordered collections must retain the original per-parent query, with no eager reads.
        if (preserveDetailReadOrder) return;

        foreach (int[] ids in parentIds.Distinct().Chunk(ParentsPerBatch))
        {
            foreach (int id in ids) details.Add(id, []);
            List<ModDetailModel> rows;
            try
            {
                rows = database.Queryable<ModDetailModel>().Where(item => ids.Contains(item.Pid)).ToList();
            }
            catch
            {
                // Retry at the original parent boundary, so later failures do not skip earlier publication.
                failedParents.UnionWith(ids);
                continue;
            }
            foreach (ModDetailModel row in rows) details[row.Pid].Add(row);
        }
    }

    internal List<ModDetailModel> Read(int parentId)
    {
        bool failed = failedParents.Remove(parentId);
        // Parameter objects keep the records; release this load's list reference as each parent is consumed.
        if (!details.Remove(parentId, out List<ModDetailModel>? rows) || failed || RequiresOriginalOrder(rows))
            return database.Queryable<ModDetailModel>().Where(item => item.Pid == parentId).ToList();
        return rows;
    }

    private bool RequiresOriginalOrder(List<ModDetailModel> rows)
    {
        // Empty/single-row groups cannot collide and need not initialize the business symbol cache.
        if (rows.Count < 2) return false;
        HashSet<int> fields = new();
        foreach (ModDetailModel row in rows)
            if (!fields.Add(row.SysPid)) return true;

        HashSet<string> symbols = new(StringComparer.Ordinal);
        foreach (ModDetailModel row in rows)
        {
            string? symbol;
            try { symbol = resolveSymbol(row.SysPid); }
            catch { return true; } // Leave symbol failures to the original parameter constructor.
            if (symbol != null && !symbols.Add(symbol)) return true;
        }
        return false;
    }
}
