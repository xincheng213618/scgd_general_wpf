using ColorVision.Database;
using ColorVision.Engine.Services;
using Microsoft.Data.Sqlite;
using Newtonsoft.Json;
using System.IO;

namespace ColorVision.UI.Tests;

public sealed class LocalConfigurationLookupTests : IDisposable
{
    private const string Kind = "configuration-lookup-fixture";
    private readonly string directory = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "ColorVision-configuration-lookup-tests", Guid.NewGuid().ToString("N")));
    private LocalTemplateStore Store => new(Path.Combine(directory, "config.db"));

    [Fact]
    public void ExactLookupPreservesKindLocalIdentityAndMissingDocumentSemantics()
    {
        LocalTemplateStore store = Store;
        bool connected = false;
        var dao = new LookupDao(Kind, store, () => connected);
        int id = store.Save(Kind, null, "first", 1, """{"Id":42,"Name":"first"}""");
        int localId = -id - 1;

        Assert.Equal(localId, dao.GetById(localId)!.Id);
        connected = true;
        store.Save(Kind, id, "updated", 1, """{"Id":42,"Name":"updated"}""");
        Assert.Equal("updated", dao.GetById(localId)!.Name);
        Assert.Null(new LookupDao("another-kind", store, () => connected).GetById(localId));
        Assert.Null(store.Find("another-kind", id));
        Assert.Throws<InvalidDataException>(() => store.Read("another-kind", id));
        Assert.Equal(store.Read(Kind, id), store.Find(Kind, id));
        store.Delete(Kind, id);
        Assert.Null(dao.GetById(localId));
        Assert.Null(store.Find(Kind, id));
        Assert.Throws<InvalidDataException>(() => store.Read(Kind, id));
        connected = false;
        Assert.Null(dao.GetById(null));
        Assert.Null(dao.GetById(-1));
        Assert.Null(dao.GetById(0));
        Assert.Null(dao.GetById(42));
    }

    // A bad target remains an error, while unrelated documents need not be decoded for an exact lookup.
    [Theory]
    [InlineData(999, "{}", typeof(InvalidDataException))]
    [InlineData(1, "not json", typeof(JsonReaderException))]
    [InlineData(1, "null", typeof(InvalidDataException))]
    public void TargetPayloadFailuresStillPropagate(int schemaVersion, string payload, Type exceptionType)
    {
        LocalTemplateStore store = Store;
        int badId = store.Save(Kind, null, "invalid", schemaVersion, payload);
        int goodId = store.Save(Kind, null, "valid", 1, """{"Name":"valid"}""");
        var dao = new LookupDao(Kind, store, () => false);

        Assert.Equal("valid", dao.GetById(-goodId - 1)!.Name);
        Assert.Throws(exceptionType, () => dao.GetById(-badId - 1));
    }

    [Fact]
    public void TheFullLocalIdRangeCanBeReadWithoutInverseMappingOverflow()
    {
        LocalTemplateStore store = Store;
        _ = store.List(Kind);
        using (var connection = new SqliteConnection($"Data Source={store.DatabasePath};Pooling=False"))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "INSERT INTO local_templates(id,kind,name,schema_version,payload) VALUES($id,$kind,'boundary',1,'{}')";
            command.Parameters.AddWithValue("$id", int.MaxValue);
            command.Parameters.AddWithValue("$kind", Kind);
            command.ExecuteNonQuery();
        }

        var dao = new LookupDao(Kind, store, () => throw new InvalidOperationException("Local IDs must not consult MySQL connection state."));
        Assert.Equal(int.MinValue, dao.GetById(int.MinValue)!.Id);
    }

    public void Dispose()
    {
        string parent = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "ColorVision-configuration-lookup-tests"));
        Assert.StartsWith(parent + Path.DirectorySeparatorChar, directory, StringComparison.OrdinalIgnoreCase);
        if (Directory.Exists(directory)) Directory.Delete(directory, true);
    }

    private sealed class LookupDao(string kind, LocalTemplateStore store, Func<bool> connected) : LocalConfigurationDao<LookupModel>(kind, store, connected);

    public sealed class LookupModel : IEntity
    {
        public int Id { get; set; }
        public string? Name { get; set; }
    }
}
