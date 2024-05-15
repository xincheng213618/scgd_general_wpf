using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.MySql;
using ColorVision.Services.Dao;
using ColorVision.Services.Flow;
using ColorVision.Services.Templates;
using ColorVision.Services.Templates.POI;
using ColorVision.Settings;
using ColorVision.UI;
using NPOI.SS.Formula.Functions;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Services.Devices.Algorithm.Templates
{
    public class ExportTemplateAlgorithm : IMenuItem
    {
        public string? OwnerGuid => "Template";

        public string? GuidId => "TemplateAlgorithm";
        public int Order => 2;
        public string? Header => ColorVision.Properties.Resource.MenuAlgorithm;

        public string? InputGestureText { get; }

        public object? Icon { get; }

        public RelayCommand Command {get; }
    }


    public class ExportBuildPOI : IMenuItem
    {
        public string? OwnerGuid => "TemplateAlgorithm";

        public string? GuidId => "BuildPOI";
        public int Order => 0;
        public string? Header => ColorVision.Properties.Resource.MenuBuildPOI;

        public string? InputGestureText { get; }

        public object? Icon { get; }

        public RelayCommand Command => new(a => {
            if (MySqlSetting.Instance.IsUseMySql && !MySqlSetting.IsConnect)
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), "数据库连接失败，请先连接数据库在操作", "ColorVision");
                return;
            }
            new WindowTemplate(new TemplateBuildPOIParam()) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog(); ;
        }); 
    }

    public class TemplateBuildPOIParam : ITemplate<BuildPOIParam>, IITemplateLoad
    {
        public TemplateBuildPOIParam()
        {
            Title = "BuildPOI算法设置";
            Code = ModMasterType.BuildPOI;
            TemplateParams = BuildPOIParam.BuildPOIParams;
        }
    }


    public class BuildPOIParam : ParamBase
    {
        public static ObservableCollection<TemplateModel<BuildPOIParam>> BuildPOIParams { get; set; } =  new ObservableCollection<TemplateModel<BuildPOIParam>>();

        public BuildPOIParam() { }

        public BuildPOIParam(ModMasterModel modMaster, List<ModDetailModel> modDetails) : base(modMaster.Id, modMaster.Name ?? string.Empty, modDetails)
        {
        }


        [Category("BuildPOI"), Description("POI布点范围类型")]
        public RiPointTypes POILayout { get => GetValue(_POILayout); set { SetProperty(ref _POILayout, value); } }
        private RiPointTypes _POILayout = RiPointTypes.Rect;


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
        public RiPointTypes PointType { get => GetValue(_PointType); set { SetProperty(ref _PointType, value); } }
        private RiPointTypes _PointType = RiPointTypes.Circle;

        [Category("BuildPOI"), Description("POI点位置")]
        public DrawingPOIPosition PointPosition { get => GetValue(_PointPosition); set { SetProperty(ref _PointPosition, value); } }
        private DrawingPOIPosition _PointPosition = DrawingPOIPosition.LineOn;

        [Category("BuildPOI"), Description("POI点宽度")]
        public int PointWidth { get => GetValue(_PointWidth); set { SetProperty(ref _PointWidth, value); } }
        private int _PointWidth = 3;

        [Category("BuildPOI"), Description("POI点高度")]
        public int PointHeight { get => GetValue(_PointHeight); set { SetProperty(ref _PointHeight, value); } }
        private int _PointHeight = 3;

        [Category("BuildPOI"), Description("布点边距类型")]
        public BorderType MarginType { get => GetValue(_MarginType); set { SetProperty(ref _MarginType, value); } }
        private BorderType _MarginType = BorderType.Relative;

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


    }
}
