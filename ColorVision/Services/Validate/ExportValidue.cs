using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Services.Templates;
using ColorVision.UI;
using System.Windows;

namespace ColorVision.Services.Validate
{
    public class ExportValidue : IMenuItem
    {
        public string? OwnerGuid => "Template";

        public string? GuidId => "Validue";
        public int Order => 4;
        public Visibility Visibility => Visibility.Visible;
        public string? Header => "CIE合规";

        public string? InputGestureText { get; }

        public object? Icon { get; }

        public RelayCommand Command => new(a =>
        {
            new WindowTemplate(new TemplateValidateParam() { Title = "Validate.CIE" }) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog(); ;
        });
    }

    public class ExportValidueAVG : IMenuItem
    {
        public string? OwnerGuid => "Template";

        public string? GuidId => "Validue";
        public int Order => 5;
        public Visibility Visibility => Visibility.Visible;
        public string? Header => "CIE均值合规";

        public string? InputGestureText { get; }

        public object? Icon { get; }

        public RelayCommand Command => new(a =>
        {
            new WindowTemplate(new TemplateValidateParam() { Code = "Validate.CIE.AVG" ,Title = "Validate.CIE.AVG" }) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog(); ;
        });
    }
}
