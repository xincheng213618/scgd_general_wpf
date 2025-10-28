using System.Reflection;
using System.Windows.Controls;

namespace System.ComponentModel
{
    public interface IPropertyEditor
    {
        DockPanel GenProperties(PropertyInfo property, object obj);
    }
}
