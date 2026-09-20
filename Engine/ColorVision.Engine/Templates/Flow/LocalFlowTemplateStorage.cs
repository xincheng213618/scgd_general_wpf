using ColorVision.Database;
using ColorVision.Engine.Templates.Flow.Versioning;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ColorVision.Engine.Templates.Flow;

/// <summary>Stores flow configuration directly, without MySQL template/resource rows or execution results.</summary>
public sealed class LocalFlowTemplateStorage
{
    private const string Kind = "flow";
    private const string KeyPrefix = "local-flow:";
    private readonly LocalTemplateStore documents;
    public static LocalFlowTemplateStorage Default { get; } = new(new LocalTemplateStore(LocalTemplateStore.DefaultDatabasePath));

    public LocalFlowTemplateStorage(LocalTemplateStore documents) => this.documents = documents;

    // Match the local template convention: -1 is the new/empty sentinel, MySQL IDs stay positive.
    public static bool IsLocalId(int id) => id < -1;
    private static int ConvertId(int id) => checked(-id - 1);

    public IReadOnlyList<FlowParam> Load() => documents.List(Kind).Select(Decode).ToList();

    public FlowParam Read(int id)
    {
        if (!IsLocalId(id)) throw new ArgumentOutOfRangeException(nameof(id));
        return Decode(documents.Read(Kind, ConvertId(id)));
    }

    private FlowParam Decode(LocalTemplateDocument document)
    {
        if (document.SchemaVersion != 1) throw new InvalidDataException($"不支持的本地流程版本：{document.SchemaVersion}");
        var content = JsonConvert.DeserializeObject<FlowDocument>(document.Payload)
            ?? throw new InvalidDataException("本地流程内容无效。");
        if (content.FlowKey == null || !content.FlowKey.StartsWith(KeyPrefix, StringComparison.Ordinal)
            || !Guid.TryParse(content.FlowKey[KeyPrefix.Length..], out _))
            throw new InvalidDataException("本地流程缺少有效的稳定标识。");
        string hash = ValidateAndHash(content.DataBase64);
        return new FlowParam
        {
            Id = ConvertId(document.Id), Name = document.Name, DataBase64 = content.DataBase64,
            FlowKey = content.FlowKey, LoadedContentHash = hash, LocalStorage = this
        };
    }

    public void Save(FlowParam value, FlowTemplateSaveCondition? condition = null)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Id > 0) throw new InvalidOperationException("服务器流程不能直接保存到本地，请复制或导入为新的本地流程。");
        string hash = ValidateAndHash(value.DataBase64);
        string flowKey;
        int id;
        if (IsLocalId(value.Id))
        {
            LocalTemplateDocument previous = documents.Read(Kind, ConvertId(value.Id));
            FlowParam persisted = Decode(previous);
            string? expectedHash = TemplateFlow.ResolveExpectedContentHash(value, condition);
            if (value.FlowKey != persisted.FlowKey)
                throw new InvalidDataException("本地流程身份不匹配，请重新打开流程。");
            if (expectedHash != null && expectedHash != persisted.LoadedContentHash)
                throw new FlowTemplateConcurrencyException(value.Name, expectedHash, persisted.LoadedContentHash!);
            flowKey = persisted.FlowKey!;
            string payload = JsonConvert.SerializeObject(new FlowDocument(flowKey, value.DataBase64));
            if (!documents.TryUpdate(Kind, previous, value.Name, 1, payload))
                throw new FlowTemplateConcurrencyException(value.Name, expectedHash ?? string.Empty, Read(value.Id).LoadedContentHash!);
            id = previous.Id;
        }
        else
        {
            flowKey = KeyPrefix + Guid.NewGuid().ToString("N");
            id = documents.Save(Kind, null, value.Name, 1,
                JsonConvert.SerializeObject(new FlowDocument(flowKey, value.DataBase64)));
        }
        value.Id = ConvertId(id);
        value.FlowKey = flowKey;
        value.LoadedContentHash = hash;
        value.LocalStorage = this;
    }

    public void Delete(int id)
    {
        if (!IsLocalId(id)) throw new ArgumentOutOfRangeException(nameof(id));
        documents.Delete(Kind, ConvertId(id));
    }

    public void SwapOrder(int first, int second)
    {
        if (!IsLocalId(first) || !IsLocalId(second)) throw new ArgumentException("只能调整本地流程顺序。");
        documents.SwapOrder(Kind, ConvertId(first), ConvertId(second));
    }

    private static string ValidateAndHash(string dataBase64)
    {
        if (dataBase64 == null) throw new InvalidDataException("本地流程缺少画布内容。");
        byte[] bytes = Convert.FromBase64String(dataBase64);
        if (bytes.Length > 0) FlowPackageStnValidator.ValidateAndDecompress(bytes);
        return FlowSemanticHash.ComputeBinaryHash(bytes);
    }

    private sealed record FlowDocument(string FlowKey, string DataBase64);
}
