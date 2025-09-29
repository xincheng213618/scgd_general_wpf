using System.Reflection;
using System.Windows.Controls;

namespace ColorVision.UI
{
    public interface IPropertyEditor
    {
        DockPanel GenProperties(PropertyInfo property, object obj);
    }
}
