﻿using ColorVision.Engine.Services.Devices.Algorithm.Templates.LEDStripDetection;
using ColorVision.Engine.Templates;
using System.Collections.ObjectModel;
using System.Windows.Controls;

namespace ColorVision.Engine.Services.Devices.Algorithm.Templates.SFR
{
    public class TemplateSFR : ITemplate<SFRParam>, IITemplateLoad
    {
        public static ObservableCollection<TemplateModel<SFRParam>> Params { get; set; } = new ObservableCollection<TemplateModel<SFRParam>>();

        public TemplateSFR()
        {
            Title = "SFRParam算法设置";
            Code = "SFR";
            TemplateParams = Params;
            IsUserControl = true;
        }

        public override void SetUserControlDataContext(int index)
        {
            EditSFR.SetParam(TemplateParams[index].Value);
        }
        public EditSFR EditSFR { get; set; } = new EditSFR();

        public override UserControl GetUserControl()
        {
            return EditSFR;
        }

    }
}