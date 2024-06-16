#pragma warning disable CA1707,IDE1006

using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Engine.MySql;
using ColorVision.Engine.Templates;
using ColorVision.Engine.Services.Dao;
using ColorVision.Engine.Services.Templates;
using ColorVision.UI.Menus;
using cvColorVision;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using ColorVision.Engine.Templates.POI;

namespace ColorVision.Engine.Services.Devices.Algorithm.Templates
{
    public class ExportMTFParam : ExportTemplateBase
    {
        public override string OwnerGuid => "TemplateAlgorithm";
        public override string GuidId => "MTFParam";
        public override string Header => Properties.Resources.MenuMTF;
        public override int Order => 2;
        public override ITemplate Template => new TemplateMTFParam();
    }

    public class TemplateMTFParam : ITemplate<MTFParam>, IITemplateLoad
    {
        public TemplateMTFParam()
        {
            Title = "MTFParam算法设置";
            Code = ModMasterType.MTF;
            TemplateParams = MTFParam.MTFParams;
        }
    }

    public class MTFParam : ParamBase
    {
        public static ObservableCollection<TemplateModel<MTFParam>> MTFParams { get; set; } = new ObservableCollection<TemplateModel<MTFParam>>();

        public MTFParam() { }
        public MTFParam(ModMasterModel modMaster, List<ModDetailModel> modDetails) : base(modMaster.Id, modMaster.Name ?? string.Empty, modDetails)
        {
        }

        [Category("MTF"), Description("MTF dRatio")]
        public double MTF_dRatio { get => GetValue(_MTF_dRatio); set { SetProperty(ref _MTF_dRatio, value); } }
        private double _MTF_dRatio = 0.01;


        [Category("MTF"), Description("EvaFunc")]
        public EvaFunc eEvaFunc { get => GetValue(_eEvaFunc); set { SetProperty(ref _eEvaFunc, value); } }
        private EvaFunc _eEvaFunc = EvaFunc.CalResol;


        [Category("MTF"), Description("dx")]
        public int dx { get => GetValue(_dx); set { SetProperty(ref _dx, value); } }
        private int _dx;

        [Category("MTF"), Description("dy")]
        public int dy { get => GetValue(_dy); set { SetProperty(ref _dy, value); } }
        private int _dy = 1;

        [Category("MTF"), Description("dx")]
        public int ksize { get => GetValue(_ksize); set { SetProperty(ref _ksize, value); } }
        private int _ksize = 5;

    }
}
