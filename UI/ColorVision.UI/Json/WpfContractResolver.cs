using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System.Reflection;
using System.Windows;

namespace ColorVision.UI.Json
{
    public class WpfContractResolver : DefaultContractResolver
    {
        protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
        {
            var property = base.CreateProperty(member, memberSerialization);

            if (typeof(Freezable).IsAssignableFrom(property.PropertyType))
            {
                property.ObjectCreationHandling = ObjectCreationHandling.Replace;
            }
            return property;
        }
    }


}
