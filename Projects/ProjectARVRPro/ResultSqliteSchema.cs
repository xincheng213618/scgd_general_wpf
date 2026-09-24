using SqlSugar;

namespace ProjectARVRPro;

internal static class ResultSqliteSchema
{
    private const string ObjectiveTable = "ObjectiveTestResultRecord";
    private const string FlowTable = "ARVRReuslt";

    public static void EnsureCreated(SqlSugarClient db)
    {
        ArgumentNullException.ThrowIfNull(db);

        db.CodeFirst.InitTables<ObjectiveTestResultRecord, ProjectARVRReuslt>();
        ResultJsonPayloadStorage.EnsureSchema(db);

        db.Ado.ExecuteCommand($"CREATE INDEX IF NOT EXISTS \"IX_{ObjectiveTable}_SN\" ON \"{ObjectiveTable}\" (\"SN\");");
        db.Ado.ExecuteCommand($"CREATE INDEX IF NOT EXISTS \"IX_{ObjectiveTable}_CreateTime\" ON \"{ObjectiveTable}\" (\"CreateTime\");");
        db.Ado.ExecuteCommand($"CREATE INDEX IF NOT EXISTS \"IX_{ObjectiveTable}_UpdateTime\" ON \"{ObjectiveTable}\" (\"UpdateTime\");");
        db.Ado.ExecuteCommand($"CREATE INDEX IF NOT EXISTS \"IX_{ObjectiveTable}_TotalResult\" ON \"{ObjectiveTable}\" (\"TotalResult\");");
        db.Ado.ExecuteCommand($"CREATE INDEX IF NOT EXISTS \"IX_{ObjectiveTable}_IsFinalized_UpdateTime\" ON \"{ObjectiveTable}\" (\"IsFinalized\", \"UpdateTime\");");
        db.Ado.ExecuteCommand($"CREATE INDEX IF NOT EXISTS \"IX_{ObjectiveTable}_Statistics\" ON \"{ObjectiveTable}\" (\"IsFinalized\", \"UpdateTime\", \"CreateTime\", \"TotalResult\");");
        db.Ado.ExecuteCommand($"CREATE INDEX IF NOT EXISTS \"IX_{FlowTable}_CreateTime\" ON \"{FlowTable}\" (\"CreateTime\");");
        db.Ado.ExecuteCommand($"CREATE INDEX IF NOT EXISTS \"IX_{FlowTable}_SN_CreateTime\" ON \"{FlowTable}\" (\"SN\", \"CreateTime\");");
        db.Ado.ExecuteCommand($"CREATE INDEX IF NOT EXISTS \"IX_{FlowTable}_SN_Id\" ON \"{FlowTable}\" (\"SN\", \"Id\");");
        db.Ado.ExecuteCommand($"CREATE INDEX IF NOT EXISTS \"IX_{FlowTable}_BatchId\" ON \"{FlowTable}\" (\"BatchId\");");
    }
}
