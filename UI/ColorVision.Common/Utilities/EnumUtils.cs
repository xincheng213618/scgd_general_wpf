using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;

namespace ColorVision.Common.Utilities
{
    public static class EnumExtensions
    {
        public static string ToDescription(this Enum This) => This?.GetType()?.GetRuntimeField(This.ToString())?.GetCustomAttributes<System.ComponentModel.DescriptionAttribute>().FirstOrDefault()?.Description ?? This.ToString();

        public static IEnumerable<KeyValuePair<TEnum, string>> ToKeyValuePairs<TEnum>() where TEnum : Enum
        {
            return Enum.GetValues(typeof(TEnum)).Cast<TEnum>().Select(e => new KeyValuePair<TEnum, string>(e, e.ToString()));
        }

        public static Byte[] StructToBytes(Object structure)
        {
            Int32 size = Marshal.SizeOf(structure);
            IntPtr buffer = Marshal.AllocHGlobal(size);             // 从进程的非托管内存中分配内存
            try
            {
                Marshal.StructureToPtr(structure, buffer, false);   // 将数据从托管对象封送到非托管内存块
                Byte[] bytes = new Byte[size];
                Marshal.Copy(buffer, bytes, 0, size);               // 将数据从非托管内存复制到托管8位无符号整数数组
                return bytes;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);                        // 释放从非托管内存中分配的内存
            }
        }
        public static Object? BytesToStruct(Byte[] bytes, Type strcutType)
        {
            Int32 size = Marshal.SizeOf(strcutType);
            IntPtr buffer = Marshal.AllocHGlobal(size);
            try
            {
                Marshal.Copy(bytes, 0, buffer, size);               // 将托管的8位无符号整数数组复制到非托管内存指针
                return Marshal.PtrToStructure(buffer, strcutType);  // 将数据从非托管内存块封送到指定类型的托管对象
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
    }
}
