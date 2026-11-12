using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Text;

namespace ST.Library.UI.NodeEditor;

/// <summary>Detached display information. Reading it never restores node properties or connects options.</summary>
public sealed record STNodePreviewAppearance(string Title, Color TitleColor, string FontName, float FontSize,
    FontStyle FontStyle, GraphicsUnit FontUnit, int TitleHeight, int ItemHeight, STNodePreviewText[] Texts,
    STNodePreviewPort[] Inputs, STNodePreviewPort[] Outputs);

public sealed record STNodePreviewText(Rectangle Bounds, string Label, string Value, bool Centered = false);
public sealed record STNodePreviewPort(string Name, bool IsSingle, bool IsEmpty, int DotSize, Color DotColor);

public abstract partial class STNode
{
    public STNodePreviewAppearance CapturePreviewAppearance(IReadOnlyDictionary<string, byte[]> savedProperties)
    {
        var texts = _ShowControls ? _Controls.Cast<STNodeControl>().Where(control => control.Visable)
            .Select(control => control.CapturePreviewText(savedProperties)).Where(text => text != null).Cast<STNodePreviewText>().ToArray()
            : Array.Empty<STNodePreviewText>();
        return new STNodePreviewAppearance(Title, TitleColor, Font.Name, Font.Size, Font.Style, Font.Unit,
            TitleHeight, ItemHeight, texts, CapturePorts(GetAllInputOptions()), CapturePorts(GetAllOutputOptions()));
    }

    private static STNodePreviewPort[] CapturePorts(STNodeOption[] options) => options.Select(option =>
        new STNodePreviewPort(option.Text, option.IsSingle, ReferenceEquals(option, STNodeOption.Empty), option.DotSize, option.DotColor)).ToArray();

    /// <summary>Reads an ordinary saved display value by the control's localized property label, without invoking a setter.</summary>
    public string ReadPreviewValue(string label, IReadOnlyDictionary<string, byte[]> savedProperties)
    {
        static string Normalize(string text) => new((text ?? string.Empty).Where(char.IsLetterOrDigit).ToArray());
        string key = Normalize(label);
        if (key.Length == 0) return null;
        foreach (var property in GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var attribute = property.GetCustomAttribute<STNodePropertyAttribute>(inherit: true);
            if (attribute?.DescriptorType != typeof(STNodePropertyDescriptor)
                || (key != Normalize(attribute.Name) && key != Normalize(Lang.Get(attribute.Name)))) continue;
            if (savedProperties.TryGetValue(property.Name, out var bytes) && bytes.Length <= 4096)
                return Encoding.UTF8.GetString(bytes);
        }
        return null;
    }
}
