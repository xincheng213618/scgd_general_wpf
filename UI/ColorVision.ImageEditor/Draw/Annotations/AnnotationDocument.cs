using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;

namespace ColorVision.ImageEditor.Draw.Annotations
{
    public enum AnnotationCoordinateSpace
    {
        ImagePixels,
    }

    public enum AnnotationKind
    {
        Circle,
        Rectangle,
        Text,
        Line,
        Polygon,
        BezierCurve,
    }

    public enum AnnotationRectangleTextPosition
    {
        Center,
        Top,
        Bottom,
        Left,
        Right,
    }

    public sealed class AnnotationDocument
    {
        public int SchemaVersion { get; set; } = 1;

        public AnnotationCoordinateSpace CoordinateSpace { get; set; } = AnnotationCoordinateSpace.ImagePixels;

        [JsonProperty(ItemConverterType = typeof(AnnotationItemJsonConverter))]
        public List<AnnotationItem> Items { get; set; } = new();
    }

    public abstract class AnnotationItem
    {
        [JsonProperty(Order = -10)]
        public abstract AnnotationKind Kind { get; }

        public int Id { get; set; }

        public string? Name { get; set; }

        public string? Msg { get; set; }
    }

    public sealed class CircleAnnotationItem : AnnotationItem
    {
        public override AnnotationKind Kind => AnnotationKind.Circle;

        public AnnotationPoint Center { get; set; } = new();

        public double RadiusX { get; set; }

        public double RadiusY { get; set; }

        public AnnotationShapeStyle Style { get; set; } = new();

        public AnnotationTextStyle? TextStyle { get; set; }
    }

    public sealed class RectangleAnnotationItem : AnnotationItem
    {
        public override AnnotationKind Kind => AnnotationKind.Rectangle;

        public AnnotationRect Rect { get; set; } = new();

        public AnnotationShapeStyle Style { get; set; } = new();

        public AnnotationTextStyle? TextStyle { get; set; }

        public AnnotationRectangleTextPosition TextPosition { get; set; } = AnnotationRectangleTextPosition.Center;
    }

    public sealed class TextAnnotationItem : AnnotationItem
    {
        public override AnnotationKind Kind => AnnotationKind.Text;

        public AnnotationPoint Position { get; set; } = new();

        public AnnotationTextStyle TextStyle { get; set; } = new();
    }

    public sealed class LineAnnotationItem : AnnotationItem
    {
        public override AnnotationKind Kind => AnnotationKind.Line;

        public List<AnnotationPoint> Points { get; set; } = new();

        public AnnotationShapeStyle Style { get; set; } = new();
    }

    public sealed class PolygonAnnotationItem : AnnotationItem
    {
        public override AnnotationKind Kind => AnnotationKind.Polygon;

        public List<AnnotationPoint> Points { get; set; } = new();

        public AnnotationShapeStyle Style { get; set; } = new();

        public bool IsClosed { get; set; }
    }

    public sealed class BezierCurveAnnotationItem : AnnotationItem
    {
        public override AnnotationKind Kind => AnnotationKind.BezierCurve;

        public List<AnnotationPoint> Points { get; set; } = new();

        public AnnotationShapeStyle Style { get; set; } = new();
    }

    public sealed class AnnotationPoint
    {
        public double X { get; set; }

        public double Y { get; set; }
    }

    public sealed class AnnotationRect
    {
        public double X { get; set; }

        public double Y { get; set; }

        public double Width { get; set; }

        public double Height { get; set; }
    }

    public sealed class AnnotationShapeStyle
    {
        public string? FillColor { get; set; }

        public string? StrokeColor { get; set; }

        public double StrokeThickness { get; set; }
    }

    public sealed class AnnotationTextStyle
    {
        public string Text { get; set; } = string.Empty;

        public bool Visible { get; set; } = true;

        public double FontSize { get; set; }

        public string? ForegroundColor { get; set; }

        public string? BackgroundColor { get; set; }

        public string? FontFamily { get; set; }

        public string? FontStyle { get; set; }

        public int FontWeight { get; set; }

        public string? FontStretch { get; set; }

        public string? FlowDirection { get; set; }
    }

    internal sealed class AnnotationItemJsonConverter : JsonConverter<AnnotationItem>
    {
        public override bool CanWrite => false;

        public override void WriteJson(JsonWriter writer, AnnotationItem? value, JsonSerializer serializer)
        {
            throw new NotSupportedException();
        }

        public override AnnotationItem? ReadJson(JsonReader reader, Type objectType, AnnotationItem? existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
                return null;

            JObject jsonObject = JObject.Load(reader);
            AnnotationKind kind = ParseKind(jsonObject[nameof(AnnotationItem.Kind)]);

            return kind switch
            {
                AnnotationKind.Circle => jsonObject.ToObject<CircleAnnotationItem>(serializer),
                AnnotationKind.Rectangle => jsonObject.ToObject<RectangleAnnotationItem>(serializer),
                AnnotationKind.Text => jsonObject.ToObject<TextAnnotationItem>(serializer),
                AnnotationKind.Line => jsonObject.ToObject<LineAnnotationItem>(serializer),
                AnnotationKind.Polygon => jsonObject.ToObject<PolygonAnnotationItem>(serializer),
                AnnotationKind.BezierCurve => jsonObject.ToObject<BezierCurveAnnotationItem>(serializer),
                _ => throw new JsonSerializationException($"Unsupported annotation kind: {kind}"),
            };
        }

        private static AnnotationKind ParseKind(JToken? token)
        {
            if (token == null)
                throw new JsonSerializationException("Annotation item is missing Kind.");

            if (token.Type == JTokenType.Integer)
                return (AnnotationKind)token.Value<int>();

            if (token.Type == JTokenType.String && Enum.TryParse(token.Value<string>(), true, out AnnotationKind kind))
                return kind;

            throw new JsonSerializationException($"Unsupported annotation kind value: {token}");
        }
    }
}