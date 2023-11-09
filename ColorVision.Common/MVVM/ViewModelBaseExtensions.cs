using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using static System.Windows.Forms.Design.AxImporter;

namespace ColorVision.MVVM
{
    /// <summary>
    /// ViewMode的扩展
    /// </summary>
    public static class ViewModeBaseExtensions
    {
        //复制一个新的对象
        public static T CopyTo<T>(this T source) where T : ViewModelBase, new()
        {
            T target = new T();
            source.CopyTo(target);
            return target;
        }
        //复制一个新的对象
        public static void CopyTo<T>(this T source, T target) where T:ViewModelBase
        {
            Type type = source.GetType();
            var fields = type.GetRuntimeFields().ToList();
            foreach (var field in fields)
            {
                field.SetValue(target, field.GetValue(source));
            }

            var properties = type.GetRuntimeProperties().ToList();
            foreach (var property in properties)
            {
                property.SetValue(target, property.GetValue(source));
            }
        }

    }
}
