using ColorVision.Common.Utilities;
using ColorVision.UI;
using System.Globalization;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;

namespace System.ComponentModel
{
    public interface IPropertyEditor
    {
        DockPanel GenProperties(PropertyInfo property, object obj);
    }
}
