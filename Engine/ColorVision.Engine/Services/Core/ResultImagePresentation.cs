using ColorVision.Common.Utilities;
using Newtonsoft.Json.Linq;
using System;
using System.Windows.Media;

namespace ColorVision.Engine.Services;

/// <summary>
/// Reuses a lightweight vector background for result records whose source image is no longer available.
/// </summary>
public sealed class ResultImagePlaceholderCache
{
    private DrawingImage? _source;

    public int Width { get; private set; }
    public int Height { get; private set; }

    public DrawingImage GetOrCreate(int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        if (_source == null || Width != width || Height != height)
        {
            _source = ImageUtils.CreateSolidColorDrawing(width, height, Colors.White);
            Width = width;
            Height = height;
        }

        return _source;
    }

    public bool IsCurrent(ImageSource? source, int width, int height)
    {
        return Width == width && Height == height && ReferenceEquals(_source, source);
    }
}

public static class ResultImageDimensions
{
    public static bool IsValid(int? width, int? height) => width > 0 && height > 0;

    public static bool TryReadFrameInfo(string? json, out int width, out int height)
    {
        width = 0;
        height = 0;
        if (string.IsNullOrWhiteSpace(json))
            return false;

        try
        {
            JObject frameInfo = JObject.Parse(json);
            int parsedWidth = ReadPositiveInt(frameInfo, "width");
            int parsedHeight = ReadPositiveInt(frameInfo, "height");
            if (!IsValid(parsedWidth, parsedHeight))
                return false;

            width = parsedWidth;
            height = parsedHeight;
            return true;
        }
        catch
        {
            width = 0;
            height = 0;
            return false;
        }
    }

    private static int ReadPositiveInt(JObject value, string propertyName)
    {
        JToken? token = value.GetValue(propertyName, StringComparison.OrdinalIgnoreCase);
        if (token?.Type != JTokenType.Integer)
            return 0;

        long number = token.Value<long>();
        return number is > 0 and <= int.MaxValue ? (int)number : 0;
    }
}
