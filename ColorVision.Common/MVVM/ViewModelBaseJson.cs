using ColorVision.Common.MVVM;
using System;
using System.Diagnostics;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;

namespace ColorVision.Common.MVVM.Json
{
    public static class Extensions
    {
        private static JsonSerializerOptions jsonSerializerOptions = new() { Encoder = JavaScriptEncoder.Create(UnicodeRanges.All) };

        public static string ToJson(this ViewModelBase viewModelBase, JsonSerializerOptions? options = null)
        {
            return JsonSerializer.Serialize(viewModelBase, options ?? jsonSerializerOptions);
        }

        public static bool ToJsonFile(this ViewModelBase viewModelBase, string filePath, JsonSerializerOptions? options = null)
        {
            try
            {
                string jsonString = JsonSerializer.Serialize(viewModelBase, options ?? jsonSerializerOptions);
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
