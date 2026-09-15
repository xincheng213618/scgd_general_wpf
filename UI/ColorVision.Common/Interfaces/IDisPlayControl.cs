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

        /// <summary>
        /// Stable identity used for persisted presentation state. Display text can
        /// change, so device controls should return their configuration code.
        /// </summary>
        string PersistenceKey => DisPlayName;
    }
}
