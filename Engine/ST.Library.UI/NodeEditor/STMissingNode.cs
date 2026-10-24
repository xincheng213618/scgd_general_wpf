using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Text;

namespace ST.Library.UI.NodeEditor;

/// <summary>Editor-only placeholder. The saved identity remains the original type's identity.</summary>
public sealed class STMissingNode : STNode
{
    private readonly Dictionary<string, byte[]> properties;
    private readonly STNodePortLayout layout;
    internal STNodeLegacyConnections LegacyConnections { get; set; }

    [ReadOnly(true), DisplayName("原结点类型")]
    public string OriginalModelKey { get; }

    [ReadOnly(true), DisplayName("原类型标识")]
    public string OriginalTypeKey { get; }

    public bool HasSavedPortLayout => layout != null;

    public static string GetExecutionBlockReason(IEnumerable<STNode> nodes)
    {
        var missing = nodes.OfType<STMissingNode>().ToArray();
        return missing.Length == 0 ? null : $"流程包含 {missing.Length} 个缺失结点，请恢复组件或删除、替换后再运行："
            + Environment.NewLine + string.Join(Environment.NewLine, missing.Take(10)
                .Select(node => node.Title + " · " + node.OriginalModelKey));
    }

    public string MissingDescription => HasSavedPortLayout
        ? "缺失结点，仅供查看。请恢复所需组件，或删除、替换后再运行。"
        : "缺失结点，仅供查看。旧格式没有保存端口定义，原连线已封存，需恢复组件或重新连线。";

    public string SavedContent => string.Join(Environment.NewLine + Environment.NewLine,
        STNodeSavedState.Parse(GetSaveData()).Properties.Where(pair => !pair.Key.StartsWith("__STNode.", StringComparison.Ordinal))
            .Select(pair => pair.Key + Environment.NewLine + FormatValue(pair.Key, pair.Value)));

    internal STMissingNode(STNodeSavedState state)
    {
        OriginalModelKey = state.ModelKey;
        OriginalTypeKey = state.TypeKey;
        properties = state.Properties.ToDictionary(pair => pair.Key, pair => (byte[])pair.Value.Clone(), StringComparer.Ordinal);
        properties.Remove(STNodeSavedState.LegacyConnectionsKey);
        layout = STNodeSavedState.ReadPortLayout(properties);
        Title = ReadText("Title") ?? layout?.Title ?? ReadText("NodeName") ?? OriginalModelKey.Split('|').Last();
        Create();
        OnLoadNode(properties);
    }

    protected override void OnCreate()
    {
        TitleColor = Color.FromArgb(160, 75, 30);
        BackColor = Color.FromArgb(65, 52, 42);
        if (layout != null)
        {
            AddPorts(InputOptions, layout.Inputs);
            AddPorts(OutputOptions, layout.Outputs);
        }
        Height = Math.Max(90, 46 + Math.Max(InputOptions.Count, OutputOptions.Count) * 20);
        Width = 220;
    }

    private static void AddPorts(STNodeOptionCollection options, IEnumerable<STNodeSavedPort> ports)
    {
        foreach (STNodeSavedPort port in ports)
            options.Add(port.IsEmpty ? STNodeOption.Empty : new STNodeOption(port.Name ?? string.Empty, typeof(object), port.IsSingle));
    }

    public override string OnGetDrawTitle() => "[缺失] " + Title;

    protected override Size GetDefaultNodeSize(Graphics graphics)
    {
        Size size = base.GetDefaultNodeSize(graphics);
        return new Size(Math.Max(size.Width + 40, 220), Math.Max(size.Height + 26, 90));
    }

    protected override void OnDrawBody(DrawingTools dt)
    {
        base.OnDrawBody(dt);
        dt.SolidBrush.Color = Color.FromArgb(255, 200, 120);
        dt.Graphics.DrawString("仅供查看 · 不可执行", Font, dt.SolidBrush, Left + 8, Top + Height - 22);
    }

    protected internal override bool OnInputDataTransfer(STNodeOption input, STNodeOptionEventArgs e) => false;
    protected internal override bool OnOutputDataTransfer(STNodeOption output) => false;

    public override void OnLoadNode(Dictionary<string, byte[]> data)
    {
        // Only the framework's geometry is loaded. Original business setters never run.
        foreach (var pair in data.Where(pair => properties.ContainsKey(pair.Key)))
            properties[pair.Key] = (byte[])pair.Value.Clone();
        base.OnLoadNode(data);
    }

    public override byte[] GetSaveData()
    {
        var saved = properties.ToDictionary(pair => pair.Key, pair => (byte[])pair.Value.Clone(), StringComparer.Ordinal);
        saved["Guid"] = Guid.ToByteArray();
        saved["Left"] = BitConverter.GetBytes(Left);
        saved["Top"] = BitConverter.GetBytes(Top);
        if (!HasSavedPortLayout) saved[STNodeSavedState.LegacyPlaceholderKey] = new byte[] { 1 };
        // Keep opaque business data, including the original AutoSize and dimensions.
        return STNodeSavedState.Write(OriginalModelKey, OriginalTypeKey, saved);
    }

    private string ReadText(string key) => properties.TryGetValue(key, out byte[] data) ? Encoding.UTF8.GetString(data) : null;

    private static string FormatValue(string key, byte[] value)
    {
        if (key == "Guid" && value.Length == 16) return new Guid(value).ToString();
        if (key is "Left" or "Top" or "Width" or "Height" && value.Length == 4) return BitConverter.ToInt32(value).ToString();
        if (key is "AutoSize" or "LockOption" or "LockLocation" && value.Length == 1) return (value[0] != 0).ToString();
        try
        {
            string text = new UTF8Encoding(false, true).GetString(value);
            if (!text.Any(character => char.IsControl(character) && character is not '\r' and not '\n' and not '\t')) return text;
        }
        catch (DecoderFallbackException) { }
        return "Base64: " + Convert.ToBase64String(value);
    }
}
