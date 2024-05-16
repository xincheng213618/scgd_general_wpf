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
        public string? Header => Properties.Resource.MenuValidue;

        public string? InputGestureText { get; }

        public object? Icon { get; }

        public RelayCommand Command => new(a =>
        {
            new WindowTemplate(new TemplateValidateParam()) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog(); ;
        });
    }
}
