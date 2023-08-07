using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;

namespace ColorVision.MVVM
{
    /// <summary>
    /// ViewMode的扩展
    /// </summary>
    public static class ViewModeBaseExtensions
    {

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



        public static string ToJson(this ViewModelBase viewModelBase)
        {
            JsonSerializerOptions jsonSerializerOptions = new JsonSerializerOptions() { Encoder = JavaScriptEncoder.Create(UnicodeRanges.All) };
            return JsonSerializer.Serialize(viewModelBase, jsonSerializerOptions);
        }

        public static bool ToJsonFile(this ViewModelBase viewModelBase, string filePath)
        {
            try
            {
                JsonSerializerOptions jsonSerializerOptions = new JsonSerializerOptions();
                string jsonString = JsonSerializer.Serialize(viewModelBase, jsonSerializerOptions);
                File.WriteAllText(filePath, jsonString);
                return true;
            }
            catch (Exception ex)
            {
                Trace.WriteLine("### [" + ex.Source + "] Exception: " + ex.Message);
                Trace.WriteLine("### " + ex.StackTrace);
                return false;
            }
        }
    }
}
