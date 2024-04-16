using System;
using System.Windows;

namespace ColorVision.Services.Core
{
    public interface IDisPlayControl
    {
        public event RoutedEventHandler Selected;

        public event RoutedEventHandler Unselected;

        public event EventHandler SelectChanged;


        public bool IsSelected { get; set; }
    }
}
