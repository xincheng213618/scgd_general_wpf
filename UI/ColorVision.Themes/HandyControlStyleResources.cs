#pragma warning disable CA1010 // WPF ResourceDictionary defines the collection contract.
using System;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Themes;

/// <summary>Exports only the vendor base styles needed by ColorVision overrides.</summary>
public sealed class HandyControlStyleResources : ResourceDictionary
{
    public HandyControlStyleResources()
    {
        var vendor = new ResourceDictionary { Source = new Uri("/HandyControl;component/Themes/Theme.xaml", UriKind.Relative) };
        Add("CV.HandyControl.ComboBoxItem", vendor["ComboBoxItemBaseStyle"]);
        Add("CV.HandyControl.ComboBox", vendor[typeof(ComboBox)]);
        Add("CV.HandyControl.ComboBoxPlus", vendor[typeof(HandyControl.Controls.ComboBox)]);
        Add("CV.HandyControl.ComboBox.Small", vendor["ComboBox.Small"]);
        Add("CV.HandyControl.ComboBoxExtend.Small", vendor["ComboBoxExtend.Small"]);
        Add("CV.HandyControl.ComboBoxPlus.Small", vendor["ComboBoxPlus.Small"]);
        Add("CV.HandyControl.GridViewColumnHeader", vendor[typeof(GridViewColumnHeader)]);
    }
}
