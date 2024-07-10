using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Engine.MySql;
using ColorVision.Engine.Templates;
using ColorVision.Engine.Templates.POI;
using ColorVision.UI.Menus;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.Engine.Services.Devices.Algorithm.Templates.LEDStripDetection
{

    public class LEDStripDetectionParam : ParamBase
    {
        public static ObservableCollection<TemplateModel<LEDStripDetectionParam>> Params { get; set; } = new ObservableCollection<TemplateModel<LEDStripDetectionParam>>();

        public LEDStripDetectionParam()
        {
        }
        public LEDStripDetectionParam(ModMasterModel modMaster, List<ModDetailModel> modDetails) : base(modMaster.Id, modMaster.Name ?? string.Empty, modDetails)
        {

        }

        [Category("LEDStripDetection"), Description("method")]
        public int Method { get => GetValue(_Method); set { SetProperty(ref _Method, value); } }
        private int _Method = 1;

        [Category("LEDStripDetection"), Description("pointNumber")]
        public int PointNumber { get => GetValue(_PointNumber); set { SetProperty(ref _PointNumber, value); } }
        private int _PointNumber = 160;

        [Category("LEDStripDetection"), Description("pointDistance")]
        public int PointDistance { get => GetValue(_PointDistance); set { SetProperty(ref _PointDistance, value); } }
        private int _PointDistance = 50;

        [Category("LEDStripDetection"), Description("startPosition")]
        public int StartPosition { get => GetValue(_StartPosition); set { SetProperty(ref _StartPosition, value); } }
        private int _StartPosition = 100;

        [Category("LEDStripDetection"), Description("binaryPercentage")]
        public int BinaryPercentage { get => GetValue(_BinaryPercentage); set { SetProperty(ref _BinaryPercentage, value); } }
        private int _BinaryPercentage = 10;
    }
}
