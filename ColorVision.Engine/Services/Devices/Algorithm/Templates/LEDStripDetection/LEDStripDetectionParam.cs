using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Engine.Templates;
using ColorVision.Engine.Templates.POI.Comply;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;

namespace ColorVision.Engine.Services.Devices.Algorithm.Templates.LEDStripDetection
{

    public class LEDStripDetectionParam : ParamBase
    {
        public static ObservableCollection<TemplateModel<LEDStripDetectionParam>> Params { get; set; } = new ObservableCollection<TemplateModel<LEDStripDetectionParam>>();

        public LEDStripDetectionParam()
        {
        }
        public RelayCommand SelectFileCommand { get; set; }

        public LEDStripDetectionParam(ModMasterModel modMaster, List<ModDetailModel> modDetails) : base(modMaster, modDetails)
        {
            SelectFileCommand = new RelayCommand(a => SelectFile());
        }
        public void SelectFile()
        {
            using (var dialog = new System.Windows.Forms.SaveFileDialog())
            {
                dialog.FileName = "binim.tif";
                dialog.DefaultExt = ".tif";
                dialog.Filter = "TIFF files (*.tif)|*.tif|All files (*.*)|*.*";

                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    SaveName = dialog.FileName;
                }
            }
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

        public int ValidateCIEAVGId { get => GetValue(_ValidateId); set { SetProperty(ref _ValidateId, value); } }
        private int _ValidateId;

        public int ValidateCIEId { get => GetValue(_ValidateCIEId); set { SetProperty(ref _ValidateCIEId, value); } }
        private int _ValidateCIEId;


        [Category("LEDStripDetection"), Description("是否开启debug ")]
        public bool IsDebug { get => GetValue(_IsDebug); set { SetProperty(ref _IsDebug, value); } }
        private bool _IsDebug;

        [Category("LEDStripDetection"), Description("存图路径")]
        public string? SaveName { get => GetValue(_SaveName); set { SetProperty(ref _SaveName, value); } }
        private string? _SaveName = "binim.tif";

        public RelayCommand ValidateCIEAVGCommand => new RelayCommand(a =>
        {
            var Template = new TemplateComplyParam("Comply.CIE.AVG");
            new WindowTemplate(Template, Template.FindIndex(ValidateCIEAVGId)) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog(); ;
        });

        public RelayCommand ValidateCIECommand => new RelayCommand(a =>
        {
            var Template = new TemplateComplyParam("Comply.CIE");
            new WindowTemplate(Template, Template.FindIndex(ValidateCIEId)) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog(); ;
        });

    }
}
