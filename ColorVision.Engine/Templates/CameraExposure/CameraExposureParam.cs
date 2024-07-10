using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace ColorVision.Engine.Templates.CameraExposure
{
    public class CameraExposureParam : ParamBase
    {
        public static ObservableCollection<TemplateModel<CameraExposureParam>> Params { get; set; } = new ObservableCollection<TemplateModel<CameraExposureParam>>();

        public CameraExposureParam() : base()
        {

        }
        public CameraExposureParam(ModMasterModel modMaster, List<ModDetailModel> modDetails) : base(modMaster.Id, modMaster.Name ?? string.Empty, modDetails)
        {

        }

        [Category("CamerExp"), Description("ExpTime")]
        public int ExpTime { get => GetValue(_ExpTime); set { SetProperty(ref _ExpTime, value); } }
        private int _ExpTime = 10;


        [Category("CamerExp"), Description("ExpTimeR")]
        public int ExpTimeR { get => GetValue(_ExpTimeR); set { SetProperty(ref _ExpTimeR, value); } }
        private int _ExpTimeR = 10;


        [Category("CamerExp"), Description("ExpTimeG")]
        public int ExpTimeG { get => GetValue(_ExpTimeG); set { SetProperty(ref _ExpTimeG, value); } }
        private int _ExpTimeG = 10;


        [Category("CamerExp"), Description("ExpTimeB")]
        public int ExpTimeB { get => GetValue(_ExpTimeB); set { SetProperty(ref _ExpTimeB, value); } }
        private int _ExpTimeB = 10;

    }

}
