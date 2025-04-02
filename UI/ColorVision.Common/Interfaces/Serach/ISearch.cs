using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace ColorVision.UI
{
    public interface ISearch
    {
        public SearchType Type { get; }
        public string? GuidId { get; }
        public string? Header { get; }
        public object? Icon { get; }
        public ICommand? Command { get; }
    }

}
