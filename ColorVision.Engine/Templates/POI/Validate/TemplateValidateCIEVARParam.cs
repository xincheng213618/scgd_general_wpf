﻿using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.UI.Menus;
using System.Collections.ObjectModel;
using System.Windows;

namespace ColorVision.Engine.Templates.POI.Validate
{
    public class ExportValidueCIEVAR : IMenuItem
    {
        public string OwnerGuid => "Validue";

        public string GuidId => "ValidueCIEVAR";
        public int Order => 2;
        public Visibility Visibility => Visibility.Visible;
        public string Header => "CIE方差合规";

        public string InputGestureText { get; }

        public object Icon { get; }

        public RelayCommand Command => new RelayCommand(a =>
        {
            new WindowTemplate(new TemplateValidateCIEVARParam()) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog(); ;
        });
    }

    public class TemplateValidateCIEVARParam : TemplateValidateParam
    {
        public static ObservableCollection<TemplateModel<ValidateParam>> CIEVARParams { get; set; } = new ObservableCollection<TemplateModel<ValidateParam>>();
        public TemplateValidateCIEVARParam()
        {
            Title = "Comply.CIE.VAR";
            TemplateParams = CIEVARParams;
            Code = "Comply.CIE.VAR";
            IsUserControl = true;
            ValidateControl = new ValidateControl();
        }
    }
}
