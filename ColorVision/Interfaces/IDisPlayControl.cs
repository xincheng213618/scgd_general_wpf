using System;
using System.Windows;

namespace ColorVision.Interfaces
{
    public interface IDisPlayControl
    {
        public event RoutedEventHandler Selected;

        public event RoutedEventHandler Unselected;

        public event EventHandler SelectChanged;

        public bool IsSelected { get; set; }
    }
}
