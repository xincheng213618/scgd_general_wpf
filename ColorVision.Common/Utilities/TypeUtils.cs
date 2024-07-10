using System;

namespace ColorVision.Common.Utilities
{
    public static class TypeUtils
    {
        public static T? CreateInstance<T>(this Type type, params object[] args)
        {
            return (T)Activator.CreateInstance(type, args);
        }
    }
}