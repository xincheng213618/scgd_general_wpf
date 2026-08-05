using ColorVision.ImageEditor;
using System;
using AlgorithmPoiShape = CVCommCore.CVAlgorithm.POIPointTypes;
using FlowPoiShape = FlowEngineLib.Node.POI.POIPointTypes;

namespace ColorVision.Engine.Templates.POI
{
    /// <summary>
    /// Historical POI enums are converted only at subsystem boundaries.
    /// Application code should use <see cref="PoiShape"/>.
    /// </summary>
    public static class PoiShapeConversions
    {
        public static PoiShape ToPoiShape(this GraphicTypes value)
        {
            return value switch
            {
                GraphicTypes.Circle => PoiShape.Circle,
                GraphicTypes.Rect => PoiShape.Rect,
                GraphicTypes.Quadrilateral => PoiShape.Quadrilateral,
                GraphicTypes.Point => PoiShape.Point,
                GraphicTypes.Polygon => PoiShape.Polygon,
                _ => PoiShape.None
            };
        }

        public static GraphicTypes ToGraphicType(this PoiShape value)
        {
            return value switch
            {
                PoiShape.Circle => GraphicTypes.Circle,
                PoiShape.Rect or PoiShape.LeftTopRect => GraphicTypes.Rect,
                PoiShape.Quadrilateral => GraphicTypes.Quadrilateral,
                PoiShape.Point or PoiShape.LegacySolidPoint => GraphicTypes.Point,
                PoiShape.Polygon => GraphicTypes.Polygon,
                _ => throw new NotSupportedException($"POI shape cannot be stored as GraphicTypes: {value}")
            };
        }

        public static PoiShape ToPoiShape(this AlgorithmPoiShape value)
        {
            return value switch
            {
                AlgorithmPoiShape.SolidPoint_KB or AlgorithmPoiShape.SolidPoint => PoiShape.Point,
                AlgorithmPoiShape.Circle => PoiShape.Circle,
                AlgorithmPoiShape.Rect => PoiShape.Rect,
                AlgorithmPoiShape.LTRect => PoiShape.LeftTopRect,
                AlgorithmPoiShape.PolygonFour => PoiShape.Quadrilateral,
                AlgorithmPoiShape.Polygon => PoiShape.Polygon,
                _ => PoiShape.None
            };
        }

        public static AlgorithmPoiShape ToAlgorithmPoiShape(this PoiShape value)
        {
            return value switch
            {
                PoiShape.Point or PoiShape.LegacySolidPoint => AlgorithmPoiShape.SolidPoint,
                PoiShape.Circle => AlgorithmPoiShape.Circle,
                PoiShape.Rect => AlgorithmPoiShape.Rect,
                PoiShape.LeftTopRect => AlgorithmPoiShape.LTRect,
                PoiShape.Quadrilateral => AlgorithmPoiShape.PolygonFour,
                PoiShape.Polygon => AlgorithmPoiShape.Polygon,
                _ => AlgorithmPoiShape.None
            };
        }

        public static PoiShape ToPoiShape(this FlowPoiShape value)
        {
            return value switch
            {
                FlowPoiShape.SolidPoint_KB or FlowPoiShape.SolidPoint => PoiShape.Point,
                FlowPoiShape.Circle => PoiShape.Circle,
                FlowPoiShape.Rect => PoiShape.Rect,
                _ => PoiShape.None
            };
        }
    }
}
