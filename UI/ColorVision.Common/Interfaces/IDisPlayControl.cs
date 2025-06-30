using System;
using System.Windows;

namespace ColorVision.UI
{
    public interface IDisPlayControl
    {
        event RoutedEventHandler Selected;

        event RoutedEventHandler Unselected;

        event EventHandler SelectChanged;

        bool IsSelected { get; set; }

        string DisPlayName { get; }
    }
}
