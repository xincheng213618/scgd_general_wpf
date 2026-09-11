#pragma warning disable CA1010
using System.Windows;
using System.Windows.Input;

namespace ColorVision.Themes;

/// <summary>Compatibility class for the public Themes/Base.xaml dictionary.</summary>
public partial class BaseEvent : ResourceDictionary
{
    public void NumberValidationTextBox(object sender, KeyEventArgs e) => InputKeyboardNavigation.FilterNumberKeys(sender, e);
}
