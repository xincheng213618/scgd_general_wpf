using ColorVision.Database;
using SqlSugar;
using System;

namespace ColorVision.Engine.FlowProcessing.Diagnostics
{
    internal readonly record struct FlowNodeMessagePayloads(
        string? SendPayload,
        string? RecvPayload);

    /// <summary>
    /// Compressed payload storage kept outside the list entity mapping. Normal
    /// diagnostics queries read metadata only; detail views load one row by Id.
    /// </summary>
    internal static class FlowNodeMessagePayloadStorage
    {
        internal const string TableName = "FlowNodeMessage";
        internal const string IdColumnName = "id";
        internal const string SendLegacyColumnName = "send_payload";
        internal const string SendGzipColumnName = "send_payload_gzip";
        internal const string SendLengthColumnName = "send_payload_utf8_length";
        internal const string RecvLegacyColumnName = "recv_payload";
        internal const string RecvGzipColumnName = "recv_payload_gzip";
        internal const string RecvLengthColumnName = "recv_payload_utf8_length";

        internal static void EnsureSchema(SqlSugarClient db)
        {
            ArgumentNullException.ThrowIfNull(db);
            SqliteGzipTextPayloadStore.EnsureSchema(
                db,
                TableName,
                SendGzipColumnName,
                SendLengthColumnName);
            SqliteGzipTextPayloadStore.EnsureSchema(
                db,
                TableName,
                RecvGzipColumnName,
                RecvLengthColumnName);
        }

        internal static void SaveSendPayload(
            SqlSugarClient db,
            int messageId,
            string? payload)
        {
            SqliteGzipTextPayloadStore.Save(
                db,
                TableName,
                IdColumnName,
                messageId,
                SendGzipColumnName,
                SendLengthColumnName,
                payload);
        }

        internal static void SaveRecvPayload(
            SqlSugarClient db,
            int messageId,
            string? payload)
        {
            SqliteGzipTextPayloadStore.Save(
                db,
                TableName,
                IdColumnName,
                messageId,
                RecvGzipColumnName,
                RecvLengthColumnName,
                payload);
        }

        internal static FlowNodeMessagePayloads LoadPayloads(
            SqlSugarClient db,
            int messageId)
        {
            ArgumentNullException.ThrowIfNull(db);
            if (messageId <= 0)
                return default;

            object sendGzip, sendLength, recvGzip, recvLength;
            using (var reader = db.Ado.GetDataReader(
                $"SELECT \"{SendGzipColumnName}\", \"{SendLengthColumnName}\", " +
                $"\"{RecvGzipColumnName}\", \"{RecvLengthColumnName}\" " +
                $"FROM \"{TableName}\" WHERE \"{IdColumnName}\" = @id LIMIT 1;",
                new SugarParameter("@id", messageId)))
            {
                if (!reader.Read())
                    return default;
                sendGzip = reader.GetValue(0);
                sendLength = reader.GetValue(1);
                recvGzip = reader.GetValue(2);
                recvLength = reader.GetValue(3);
            }

            // Release the database reader before either payload is decompressed.
            return new FlowNodeMessagePayloads(
                Decode(sendGzip, sendLength),
                Decode(recvGzip, recvLength));
        }

        private static string? Decode(object gzipValue, object lengthValue)
        {
            byte[]? bytes = gzipValue == DBNull.Value ? null : gzipValue as byte[];
            int? length = lengthValue == DBNull.Value
                ? null
                : Convert.ToInt32(lengthValue);
            return GzipTextPayloadCodec.Decode(bytes, length);
        }
    }
}
