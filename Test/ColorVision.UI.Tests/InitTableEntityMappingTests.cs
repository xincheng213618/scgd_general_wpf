using ColorVision.Database;
using ColorVision.Engine;
using ColorVision.Engine.Services.Devices.SMU.Dao;
using ColorVision.Engine.Services.Devices.Spectrum.Dao;
using SqlSugar;
using System.Reflection;

namespace ColorVision.UI.Tests;

public sealed class InitTableEntityMappingTests
{
    private static readonly IReadOnlyDictionary<string, string[]> RequiredColumns = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
    {
        ["t_scgd_algorithm_result_detail_common"] = ["id", "pid", "result"],
        ["t_scgd_algorithm_result_detail_image"] = ["id", "pid", "file_name", "order_index", "file_info"],
        ["t_scgd_algorithm_result_master"] =
        [
            "id", "img_file", "img_file_type", "nd_port", "params", "tid", "tname", "batch_id", "z_index",
            "device_code", "smu_data_id", "i_result", "v_result", "result_code", "result", "img_result", "version",
            "total_time", "create_date"
        ],
        ["t_scgd_measure_batch"] =
        [
            "id", "t_id", "name", "code", "create_date", "total_time", "archived_flag", "result_code", "result", "tenant_id"
        ],
        ["t_scgd_measure_result_img"] =
        [
            "id", "batch_id", "z_index", "nd_port", "params", "smu_data_id", "i_result", "v_result", "raw_file",
            "file_url", "file_type", "file_data", "result_code", "result", "total_time", "device_code", "create_date"
        ],
        ["t_scgd_measure_result_sensor"] =
        [
            "id", "batch_id", "z_index", "cmd_type", "result_code", "result", "total_time", "device_code", "create_date"
        ],
        ["t_scgd_measure_result_smu"] =
        [
            "id", "device_code", "batch_id", "z_index", "channel", "is_source_v", "src_value", "limit_value",
            "v_result", "i_result", "result_code", "total_time", "create_date"
        ],
        ["t_scgd_measure_result_smu_scan"] =
        [
            "id", "device_code", "batch_id", "z_index", "channel", "is_source_v", "src_begin", "src_end", "points",
            "limit_value", "v_result", "i_result", "result_code", "total_time", "create_date"
        ],
        ["t_scgd_measure_result_spectrometer"] =
        [
            "id", "data_type", "device_code", "batch_id", "z_index", "smu_data_id", "v_result", "i_result", "fIntTime",
            "iAveNum", "auto_integration", "auto_init_dark", "self_adaption_init_dark", "nd_port", "params", "a_factor",
            "eqe", "luminous_flux", "radiant_flux", "luminous_efficacy", "fPL", "fPL_file_name", "fRi", "cie_data_ex",
            "fx", "fy", "fu", "fv", "fCCT", "dC", "fLd", "fPur", "fLp", "fHW", "fLav", "fRa", "fRR", "fGR",
            "fBR", "fIp", "fPh", "fPhe", "fPlambda", "fSpect1", "fSpect2", "fInterval", "result_code", "total_time",
            "create_date"
        ],
        ["t_scgd_sys_resource_group"] = ["id", "resource_id", "group_id"]
    };

    [Fact]
    public void InitTableEntities_ContainAllRequiredDatabaseColumns()
    {
        Type[] entityTypes = typeof(AlgResultMasterModel).Assembly.GetTypes()
            .Where(type => typeof(IInitTables).IsAssignableFrom(type) && !type.IsAbstract)
            .ToArray();

        Assert.Equal(RequiredColumns.Count, entityTypes.Length);
        foreach (Type entityType in entityTypes)
        {
            SugarTable? table = entityType.GetCustomAttribute<SugarTable>();
            Assert.NotNull(table);
            Assert.True(RequiredColumns.TryGetValue(table.TableName, out string[]? required), $"Unexpected IInitTables entity: {entityType.FullName}");

            HashSet<string> mappedColumns = entityType.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Select(property => property.GetCustomAttribute<SugarColumn>())
                .Where(column => column is not null && !column.IsIgnore)
                .Select(column => column!.ColumnName)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            string[] missing = required.Except(mappedColumns, StringComparer.OrdinalIgnoreCase).ToArray();

            Assert.True(missing.Length == 0, $"{table.TableName} is missing mapped columns: {string.Join(", ", missing)}");
        }
    }

    [Fact]
    public void RiskyInitTableColumns_MatchMySqlSchema()
    {
        AssertColumn<AlgResultMasterModel>("nd_port", typeof(int?), "tinyint", 2);
        AssertColumn<AlgResultMasterModel>("i_result", typeof(float?));
        AssertColumn<AlgResultMasterModel>("v_result", typeof(float?));
        AssertColumn<AlgResultMasterModel>("version", typeof(string), expectedLength: 16);
        AssertColumn<MeasureResultImgModel>("i_result", typeof(float?));
        AssertColumn<MeasureResultImgModel>("v_result", typeof(float?));
        AssertColumn<MeasureResultImgModel>("file_url", typeof(string), expectedLength: 2048);
        AssertColumn<SmuScanModel>("channel", typeof(SMUChannelType), "tinyint", 1);
        AssertColumn<SpectumResultEntity>("nd_port", typeof(int?), "tinyint", 2);
    }

    private static void AssertColumn<TEntity>(string columnName, Type propertyType, string? expectedDataType = null, int? expectedLength = null)
    {
        (PropertyInfo Property, SugarColumn Column) mapping = typeof(TEntity).GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Select(property => (Property: property, Column: property.GetCustomAttribute<SugarColumn>()))
            .Where(item => item.Column is not null && !item.Column.IsIgnore)
            .Select(item => (item.Property, item.Column!))
            .Single(item => string.Equals(item.Item2.ColumnName, columnName, StringComparison.OrdinalIgnoreCase));

        Assert.Equal(propertyType, mapping.Property.PropertyType);
        if (expectedDataType is not null)
        {
            Assert.Equal(expectedDataType, mapping.Column.ColumnDataType, ignoreCase: true);
        }
        if (expectedLength.HasValue)
        {
            Assert.Equal(expectedLength.Value, mapping.Column.Length);
        }
    }
}
