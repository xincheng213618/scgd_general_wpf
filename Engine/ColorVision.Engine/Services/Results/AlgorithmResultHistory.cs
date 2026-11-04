using log4net;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ColorVision.Engine.Services.Results;

internal static class AlgorithmResultHistory
{
    internal const int BatchIdsPerQuery = 500;
    private static readonly ILog Log = LogManager.GetLogger(typeof(AlgorithmResultHistory));

    internal static List<ViewResultAlg> CreateViews(IReadOnlyList<AlgResultMasterModel> rows, SqlSugarClient? database)
    {
        Dictionary<int, string?> batchCodes = new();
        if (database != null)
        {
            int[] batchIds = rows.Where(row => row.BatchId > 0).Select(row => row.BatchId!.Value).Distinct().ToArray();
            foreach (int[] ids in batchIds.Chunk(BatchIdsPerQuery))
            {
                try
                {
                    var batches = database.Queryable<MeasureBatchModel>().Where(batch => ids.Contains(batch.Id))
                        .Select(batch => new { batch.Id, batch.Code }).ToList();
                    foreach (var batch in batches) batchCodes[batch.Id] = batch.Code;
                }
                catch (Exception ex)
                {
                    // Batch metadata is optional, as in the legacy per-row DAO lookup.
                    // A failed lookup must not discard readable algorithm results.
                    Log.Error("Unable to load batch codes for algorithm history.", ex);
                }
            }
        }

        List<ViewResultAlg> results = new(rows.Count);
        foreach (AlgResultMasterModel row in rows)
        {
            string? code = row.BatchId is int id ? batchCodes.GetValueOrDefault(id) : null;
            results.Add(new ViewResultAlg(row, code));
        }
        return results;
    }
}
