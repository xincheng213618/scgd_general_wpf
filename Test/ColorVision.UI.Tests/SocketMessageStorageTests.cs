using ColorVision.Database;
using ColorVision.SocketProtocol;
using SqlSugar;
using System.Data;
using System.IO;

namespace ColorVision.UI.Tests;

public sealed class SocketMessageStorageTests : IDisposable
{
    private readonly string _tempDirectory = Directory.CreateTempSubdirectory("ColorVision-SocketMessages-").FullName;
    private string DatabasePath => Path.Combine(_tempDirectory, "SocketMessages.db");

    [Theory]
    [InlineData("maintenance")]
    [InlineData("advanced clear")]
    [InlineData("legacy truncate")]
    public void WholeTableClearRetainsMessageIdHighWaterAndRejectsStaleResend(string entry)
    {
        StaTest.Run(() =>
        {
            using var manager = new SocketMessageManager(DatabasePath, new SocketMessageManagerConfig(), dispatcher: null);
            manager.AddMessage(Message("original connection", "old body"));
            manager.LoadAll();
            SocketMessage cached = Assert.Single(manager.Messages);
            Assert.Equal("old body", manager.LoadContent(cached));
            manager.LoadAll();
            SocketMessage unloaded = Assert.Single(manager.Messages);
            Assert.False(unloaded.IsContentLoaded);

            if (entry == "maintenance")
            {
                new SocketMessagesSqliteCleanupProvider(DatabasePath).CleanupAll();
            }
            else
            {
                GenericQuery<SocketMessage> query = manager.CreateGenericQuery();
                Assert.False(query.SupportsTruncateTable);
                if (entry == "advanced clear") query.DeleteAll();
                else query.TruncateTable();
                Assert.Empty(manager.Messages);
            }

            manager.AddMessage(Message("replacement connection", "new body"));
            manager.LoadAll();
            SocketMessage current = Assert.Single(manager.Messages);
            Assert.True(current.Id > cached.Id);
            Assert.Equal("replacement connection", current.ClientConnectionId);

            Assert.Equal("old body", manager.LoadContent(cached));
            Assert.Null(manager.LoadContentForResend(cached));
            Assert.Null(manager.LoadContent(unloaded));
            manager.DeleteMessage(cached);

            var mismatchedConnection = Message("original connection", "cached different body");
            mismatchedConnection.Id = current.Id;
            Assert.Null(manager.LoadContentForResend(mismatchedConnection));
            manager.DeleteMessage(mismatchedConnection);

            var transient = Message("replacement connection", "transient body");
            Assert.Equal("transient body", manager.LoadContent(transient));
            Assert.Null(manager.LoadContentForResend(transient));
            manager.DeleteMessage(transient);

            Assert.Equal("new body", manager.LoadContentForResend(current));
            manager.LoadAll();
            Assert.Equal(current.Id, Assert.Single(manager.Messages).Id);
        });
    }

    [Fact]
    public void OpeningLegacyTableAddsNullableConnectionIdWithoutReusingDeletedIds()
    {
        int retainedId;
        int deletedId;
        using (SqlSugarClient db = CreateDbClient())
        {
            db.CodeFirst.InitTables<SocketMessage>();
            SocketMessagePayloadStorage.EnsureSchema(db);
            retainedId = db.Insertable(Message(null, "legacy body")).ExecuteReturnIdentity();
            SocketMessagePayloadStorage.Save(db, retainedId, "legacy body");
            deletedId = db.Insertable(Message(null, "discarded body")).ExecuteReturnIdentity();
            db.Deleteable<SocketMessage>().Where(message => message.Id == deletedId).ExecuteCommand();
            db.Ado.ExecuteCommand("ALTER TABLE \"SocketMessage\" DROP COLUMN \"ClientConnectionId\";");
        }

        using var manager = new SocketMessageManager(DatabasePath, new SocketMessageManagerConfig(), dispatcher: null);
        manager.LoadAll();
        SocketMessage retained = Assert.Single(manager.Messages);
        Assert.Equal(retainedId, retained.Id);
        Assert.Null(retained.ClientConnectionId);
        Assert.Equal("legacy body", manager.LoadContent(retained));

        manager.AddMessage(Message("current connection", "current body"));
        manager.LoadAll();
        SocketMessage current = Assert.Single(manager.Messages, message => message.ClientConnectionId == "current connection");
        Assert.True(current.Id > deletedId);
        Assert.Equal("current body", manager.LoadContentForResend(current));
    }

    private static SocketMessage Message(string? connectionId, string content) => new()
    {
        ClientEndPoint = "127.0.0.1:6666",
        ClientConnectionId = connectionId,
        Direction = SocketMessageDirection.Sent,
        Content = content,
    };

    [Fact]
    public void RuntimeStorageWritesOnlyGzipAndLoadsContentById()
    {
        using SqlSugarClient db = CreateDbClient();
        db.CodeFirst.InitTables<SocketMessage>();
        db.Ado.ExecuteCommand("ALTER TABLE \"SocketMessage\" ADD COLUMN \"Content\" TEXT NULL;");
        SocketMessagePayloadStorage.EnsureSchema(db);

        string content = "{\"message\":\"测试🙂\",\"values\":[1,2,3]}";
        var message = new SocketMessage
        {
            ClientEndPoint = "127.0.0.1:6666",
            Direction = SocketMessageDirection.Sent,
            Content = content,
            ContentPreview = GzipTextPayloadCodec.CreatePreview(
                content,
                SocketMessagePayloadStorage.PreviewCharacters),
            MessageTime = new DateTime(2026, 8, 10, 3, 0, 0),
            EventName = "TestEvent",
            MsgID = "test-1",
            ResponseCode = 0,
        };

        db.Ado.BeginTran();
        int id;
        try
        {
            id = db.Insertable(message).ExecuteReturnIdentity();
            SocketMessagePayloadStorage.Save(db, id, content);
            db.Ado.CommitTran();
        }
        catch
        {
            db.Ado.RollbackTran();
            throw;
        }

        DataRow stored = db.Ado.GetDataTable(
            "SELECT \"Content\", \"ContentGzip\", \"ContentUtf8Length\", \"ContentPreview\" " +
            "FROM \"SocketMessage\" WHERE \"id\" = @id;",
            new SugarParameter("@id", id)).Rows[0];
        Assert.Equal(DBNull.Value, stored["Content"]);
        Assert.NotEmpty(Assert.IsType<byte[]>(stored["ContentGzip"]));
        Assert.Equal(System.Text.Encoding.UTF8.GetByteCount(content), Convert.ToInt32(stored["ContentUtf8Length"]));
        Assert.Equal(content, Convert.ToString(stored["ContentPreview"]));

        SocketMessage summary = db.Queryable<SocketMessage>().Where(item => item.Id == id).Single();
        Assert.False(summary.IsContentLoaded);
        Assert.Null(summary.Content);
        Assert.Equal(content, summary.ContentPreview);

        summary.Content = SocketMessagePayloadStorage.Load(db, id);
        Assert.True(summary.IsContentLoaded);
        Assert.Equal(content, summary.Content);
    }

    [Fact]
    public void ListEntitySqlDoesNotSelectLegacyOrCompressedContentColumns()
    {
        using SqlSugarClient db = CreateDbClient();
        db.CodeFirst.InitTables<SocketMessage>();
        SocketMessagePayloadStorage.EnsureSchema(db);

        string sql = db.Queryable<SocketMessage>().ToSqlString();

        Assert.Contains("ContentPreview", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ContentGzip", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ContentUtf8Length", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("`Content`", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"Content\"", sql, StringComparison.OrdinalIgnoreCase);

        DataTable indexColumns = db.Ado.GetDataTable(
            "PRAGMA index_info(\"IX_SocketMessage_MessageTime\");");
        Assert.Single(indexColumns.Rows.Cast<DataRow>());
        Assert.Equal("MessageTime", Convert.ToString(indexColumns.Rows[0]["name"]));
    }

    [Fact]
    public void LegacyMigrationCreatesPreviewClearsTextAndIsIdempotent()
    {
        using (SqlSugarClient db = CreateDbClient())
        {
            db.Ado.ExecuteCommand(
                "CREATE TABLE \"SocketMessage\"(" +
                "\"id\" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT," +
                "\"ClientEndPoint\" TEXT NULL," +
                "\"Direction\" INTEGER NOT NULL," +
                "\"Content\" TEXT NULL," +
                "\"MessageTime\" DATETIME NOT NULL," +
                "\"EventName\" TEXT NULL," +
                "\"MsgID\" TEXT NULL," +
                "\"ResponseCode\" INTEGER NULL);");
            db.Ado.ExecuteCommand(
                "INSERT INTO \"SocketMessage\"(" +
                "\"ClientEndPoint\",\"Direction\",\"Content\",\"MessageTime\",\"EventName\") VALUES " +
                "('client',1,@first,'2026-08-01 00:00:00','Large')," +
                "('client',0,@second,'2026-08-01 00:00:01','Empty')," +
                "('client',0,NULL,'2026-08-01 00:00:02','Null');",
                new SugarParameter("@first", new string('数', 600)),
                new SugarParameter("@second", string.Empty));
        }

        SqliteGzipTextMigrationReport first = LegacySocketMessageMigration.Execute(DatabasePath);
        SqliteGzipTextMigrationReport second = LegacySocketMessageMigration.Execute(DatabasePath);

        Assert.Equal(2, Assert.Single(first.Tables).MigratedRows);
        Assert.Equal(0, Assert.Single(second.Tables).MigratedRows);
        Assert.Equal("ok", first.IntegrityCheck);

        using SqlSugarClient verification = CreateDbClient();
        DataTable rows = verification.Ado.GetDataTable(
            "SELECT \"id\",\"Content\",\"ContentPreview\",\"ContentGzip\",\"ContentUtf8Length\" " +
            "FROM \"SocketMessage\" ORDER BY \"id\";");
        Assert.Equal(DBNull.Value, rows.Rows[0]["Content"]);
        Assert.Equal(DBNull.Value, rows.Rows[1]["Content"]);
        Assert.Equal(DBNull.Value, rows.Rows[2]["Content"]);
        Assert.Equal(
            new string('数', SocketMessagePayloadStorage.PreviewCharacters) + "…",
            Convert.ToString(rows.Rows[0]["ContentPreview"]));
        Assert.Equal(string.Empty, Convert.ToString(rows.Rows[1]["ContentPreview"]));
        Assert.Equal(DBNull.Value, rows.Rows[2]["ContentGzip"]);
        Assert.Equal(new string('数', 600), SocketMessagePayloadStorage.Load(verification, 1));
        Assert.Equal(string.Empty, SocketMessagePayloadStorage.Load(verification, 2));
        Assert.Null(SocketMessagePayloadStorage.Load(verification, 3));
    }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (Directory.Exists(_tempDirectory))
            Directory.Delete(_tempDirectory, recursive: true);
    }

    private SqlSugarClient CreateDbClient()
    {
        return new SqlSugarClient(new ConnectionConfig
        {
            ConnectionString = $"Data Source={DatabasePath};Default Timeout=30",
            DbType = SqlSugar.DbType.Sqlite,
            IsAutoCloseConnection = true,
            InitKeyType = InitKeyType.Attribute,
        });
    }
}
