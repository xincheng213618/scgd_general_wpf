using ColorVision.Database;
using SqlSugar;

namespace ProjectARVRPro.Offline;

public sealed class OfflineMessageRow
{
    public int Id { get; set; }
    public DateTime Time { get; set; }
    public string Direction { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Correlation { get; set; } = string.Empty;
    public string Endpoint { get; set; } = string.Empty;
}

public sealed class OfflineMessageStore
{
    private readonly ReadOnlySqliteDatabase _database;
    private readonly bool _socket;
    public OfflineMessageStore(string path, bool socket)
    {
        _database = new ReadOnlySqliteDatabase(path);
        _socket = socket;
        _database.RequireColumns(socket ? "SocketMessage" : "MsgRecord", "id", socket ? "MessageTime" : "SendTime");
    }

    public (IReadOnlyList<OfflineMessageRow> Rows, int Count) Query(DateTime from, DateTime to, int page)
    {
        if (page < 1 || to < from) throw new ArgumentOutOfRangeException(nameof(page));
        using var db = _database.OpenClient();
        string table = _socket ? "SocketMessage" : "MsgRecord";
        string filter = _socket ? "MessageTime >= @From AND MessageTime <= @To"
            : "(SendTime >= @From AND SendTime <= @To) OR (ReciveTime >= @From AND ReciveTime <= @To)";
        SugarParameter[] parameters = [new("@From", from), new("@To", to), new("@Skip", checked((page - 1) * 200))];
        string projection = _socket
            ? "id AS Id, MessageTime AS Time, CASE Direction WHEN 0 THEN '接收' ELSE '发送' END AS Direction, EventName AS Name, MsgID AS Correlation, ClientEndPoint AS Endpoint"
            : "id AS Id, SendTime AS Time, '请求/响应' AS Direction, SendTopic AS Name, MsgID AS Correlation, SubscribeTopic AS Endpoint";
        int count = db.Ado.GetInt($"SELECT COUNT(*) FROM {table} WHERE {filter}", parameters);
        return (db.Ado.SqlQuery<OfflineMessageRow>($"SELECT {projection} FROM {table} WHERE {filter} ORDER BY Id LIMIT 200 OFFSET @Skip", parameters), count);
    }

    public string LoadPayload(int id) => _socket
        ? _database.ReadPayload("SocketMessage", "id", id, "Content", "ContentGzip") ?? "该消息没有正文。"
        : "请求：\n" + _database.ReadPayload("MsgRecord", "id", id, "MsgSendJson", "MsgSendJsonGzip")
            + "\n\n响应：\n" + _database.ReadPayload("MsgRecord", "id", id, "MsgReturnJson", "MsgReturnJsonGzip");
}
