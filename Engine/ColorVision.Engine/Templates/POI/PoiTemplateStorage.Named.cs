using Newtonsoft.Json;
using ColorVision.Database;
using SqlSugar;
using System;

namespace ColorVision.Engine.Templates.POI;

public sealed partial class PoiTemplateStorage
{
    internal PoiParam ReadLocalByName(string name) => Decode(local.ReadByName(Kind, name));

    internal SqlSugarClient OpenTemplateDatabase(MySqlConnectedEndpoint? batchEndpoint) => source.OpenTemplateDatabase(batchEndpoint);

    internal TResult UpdateLocalByName<TResult>(string name, Func<PoiParam, TResult> update)
    {
        TResult result = default!;
        local.UpdateByName(Kind, name, document =>
        {
            var template = Decode(document);
            result = update(template);
            template.Id = -1; // The SQLite row owns the template identity.
            return JsonConvert.SerializeObject(template);
        });
        return result;
    }
}
