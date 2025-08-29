using ColorVision.ImageEditor;
using CVCommCore.CVAlgorithm;
using System.Collections.Generic;
using System.ComponentModel;

namespace ColorVision.Engine.Templates.POI.BuildPoi
{
    public class ParamBuildPoi : ParamModBase
    {

        public ParamBuildPoi() { }


        public ParamBuildPoi(ModMasterModel modMaster, List<ModDetailModel> modDetails) : base(modMaster, modDetails)
        {
        }


        [Category("BuildPOI"), Description("POI布点范围类型")]
        public POILayoutTypes POILayout { get => GetValue(_POILayout); set { SetProperty(ref _POILayout, value); } }
        private POILayoutTypes _POILayout = POILayoutTypes.Rect;


        [Category("BuildPOI"), Description("POI极坐标布点数量")]
        public int LayoutCircleNum { get => GetValue(_LayoutCircleNum); set { SetProperty(ref _LayoutCircleNum, value); } }
        private int _LayoutCircleNum = 4;


        [Category("BuildPOI"), Description("POI极坐标布点起始角度")]
        public int LayoutCircleAngle { get => GetValue(_LayoutCircleAngle); set { SetProperty(ref _LayoutCircleAngle, value); } }
        private int _LayoutCircleAngle;

        [Category("BuildPOI"), Description("POI布点行数")]
        public int LayoutRows { get => GetValue(_LayoutRows); set { SetProperty(ref _LayoutRows, value); } }
        private int _LayoutRows = 3;

        [Category("BuildPOI"), Description("POI布点列数")]
        public int LayoutCols { get => GetValue(_LayoutCols); set { SetProperty(ref _LayoutCols, value); } }
        private int _LayoutCols = 3;

        [Category("BuildPOI"), Description("POI点类型")]
        public POIPointTypes PointType { get => GetValue(_PointType); set { SetProperty(ref _PointType, value); } }
        private POIPointTypes _PointType = POIPointTypes.Circle;

        [Category("BuildPOI"), Description("POI点位置")]
        public DrawingGraphicPosition PointPosition { get => GetValue(_PointPosition); set { SetProperty(ref _PointPosition, value); } }
        private DrawingGraphicPosition _PointPosition = DrawingGraphicPosition.LineOn;

        [Category("BuildPOI"), Description("POI点宽度")]
        public int PointWidth { get => GetValue(_PointWidth); set { SetProperty(ref _PointWidth, value); } }
        private int _PointWidth = 3;

        [Category("BuildPOI"), Description("POI点高度")]
        public int PointHeight { get => GetValue(_PointHeight); set { SetProperty(ref _PointHeight, value); } }
        private int _PointHeight = 3;

        [Category("BuildPOI"), Description("布点边距类型")]
        public GraphicBorderType MarginType { get => GetValue(_MarginType); set { SetProperty(ref _MarginType, value); } }
        private GraphicBorderType _MarginType = GraphicBorderType.Relative;

        [Category("BuildPOI"), Description("布点左边距")]
        public int MarginLeft { get => GetValue(_MarginLeft); set { SetProperty(ref _MarginLeft, value); } }
        private int _MarginLeft = 10;

        [Category("BuildPOI"), Description("布点上边距")]
        public int MarginTop { get => GetValue(_MarginTop); set { SetProperty(ref _MarginTop, value); } }
        private int _MarginTop = 10;

        [Category("BuildPOI"), Description("布点右边距")]
        public int MarginRight { get => GetValue(_MarginRight); set { SetProperty(ref _MarginRight, value); } }
        private int _MarginRight = 10;

        [Category("BuildPOI"), Description("布点下边距")]
        public int MarginBottom { get => GetValue(_MarginBottom); set { SetProperty(ref _MarginBottom, value); } }
        private int _MarginBottom = 10;

        [Category("BuildPOI"), Description("4角点顺序")]
        public string PolygonFour_OIndex { get => GetValue(_PolygonFour_OIndex); set { SetProperty(ref _PolygonFour_OIndex, value); } }
        private string _PolygonFour_OIndex = string.Empty;


    }
}
