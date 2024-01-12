using System;

namespace ColorVision.Common.Extension
{
    public static class TypeExtensions
    {
        public static T? CreateInstance<T>(this Type t, params object[] paramArray)
        {
            return (T)Activator.CreateInstance(t, paramArray);
        }
    }
}