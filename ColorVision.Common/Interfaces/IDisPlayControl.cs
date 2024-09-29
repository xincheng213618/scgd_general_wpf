using System;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.UI
{
    public interface IDisPlayControl
    {
        public event RoutedEventHandler Selected;

        public event RoutedEventHandler Unselected;

        public event EventHandler SelectChanged;

        public bool IsSelected { get; set; }

        public string DisPlayName { get; }
    }
}
