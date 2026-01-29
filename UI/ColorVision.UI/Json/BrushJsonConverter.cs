using Newtonsoft.Json;
using System.Windows.Media;

namespace ColorVision.UI.Json
{

    public class BrushJsonConverter : JsonConverter<Brush>
    {
        public override void WriteJson(JsonWriter writer, Brush value, JsonSerializer serializer)
        {
            // 将 SolidColorBrush 转换为十六进制字符串 (例如 "#FFFF0000")
            if (value is SolidColorBrush brush)
            {
                string colorStr = null;

                // 检查是否需要跨线程访问
                if (brush.CheckAccess())
                {
                    // 当前线程拥有该对象，直接读取
                    colorStr = brush.Color.ToString();
                }
                else
                {
                    writer.WriteNull();
                }
            }
            else
            {
                // 对于其他类型的 Brush (如 GradientBrush)，这里可以抛出异常或返回 null，视需求而定
                writer.WriteNull();
            }
        }

        public override Brush ReadJson(JsonReader reader, Type objectType, Brush existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            if (reader.Value == null) return null;

            string colorString = reader.Value.ToString();

            try
            {
                // 将十六进制字符串转换回 Color
                var color = (Color)ColorConverter.ConvertFromString(colorString);
                return new SolidColorBrush(color);
            }
            catch
            {
                return null; // 或者提供一个默认颜色
            }
        }
    }


}
