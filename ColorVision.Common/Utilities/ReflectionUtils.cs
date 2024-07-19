using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Text;

namespace ColorVision.Common.Utilities
{

    internal static class ReflectionUtils
    {
        public static bool IsVirtual(this PropertyInfo propertyInfo)
        {
            ArgumentNullException.ThrowIfNull(propertyInfo);

            MethodInfo? m = propertyInfo.GetGetMethod(true);
            if (m != null && m.IsVirtual)
            {
                return true;
            }

            m = propertyInfo.GetSetMethod(true);
            if (m != null && m.IsVirtual)
            {
                return true;
            }

            return false;
        }

        public static MethodInfo? GetBaseDefinition(this PropertyInfo propertyInfo)
        {
            ArgumentNullException.ThrowIfNull(propertyInfo);

            MethodInfo? m = propertyInfo.GetGetMethod(true);
            if (m != null)
            {
                return m.GetBaseDefinition();
            }

            return propertyInfo.GetSetMethod(true)?.GetBaseDefinition();
        }

        public static bool IsPublic(PropertyInfo property)
        {
            var getMethod = property.GetGetMethod();
            if (getMethod != null && getMethod.IsPublic)
            {
                return true;
            }
            var setMethod = property.GetSetMethod();
            if (setMethod != null && setMethod.IsPublic)
            {
                return true;
            }

            return false;
        }

        public static Type? GetObjectType(object? v)
        {
            return v?.GetType();
        }


        private static string RemoveAssemblyDetails(string fullyQualifiedTypeName)
        {
            StringBuilder builder = new StringBuilder();

            // loop through the type name and filter out qualified assembly details from nested type names
            bool writingAssemblyName = false;
            bool skippingAssemblyDetails = false;
            bool followBrackets = false;
            for (int i = 0; i < fullyQualifiedTypeName.Length; i++)
            {
                char current = fullyQualifiedTypeName[i];
                switch (current)
                {
                    case '[':
                        writingAssemblyName = false;
                        skippingAssemblyDetails = false;
                        followBrackets = true;
                        builder.Append(current);
                        break;
                    case ']':
                        writingAssemblyName = false;
                        skippingAssemblyDetails = false;
                        followBrackets = false;
                        builder.Append(current);
                        break;
                    case ',':
                        if (followBrackets)
                        {
                            builder.Append(current);
                        }
                        else if (!writingAssemblyName)
                        {
                            writingAssemblyName = true;
                            builder.Append(current);
                        }
                        else
                        {
                            skippingAssemblyDetails = true;
                        }
                        break;
                    default:
                        followBrackets = false;
                        if (!skippingAssemblyDetails)
                        {
                            builder.Append(current);
                        }
                        break;
                }
            }

            return builder.ToString();
        }

        public static bool HasDefaultConstructor(Type t, bool nonPublic)
        {
            ArgumentNullException.ThrowIfNull(t);

            if (t.IsValueType)
            {
                return true;
            }

            return (GetDefaultConstructor(t, nonPublic) != null);
        }

        public static ConstructorInfo? GetDefaultConstructor(Type t)
        {
            return GetDefaultConstructor(t, false);
        }

        public static ConstructorInfo? GetDefaultConstructor(Type t, bool nonPublic)
        {
            BindingFlags bindingFlags = BindingFlags.Instance | BindingFlags.Public;
            if (nonPublic)
            {
                bindingFlags = bindingFlags | BindingFlags.NonPublic;
            }

            return t.GetConstructors(bindingFlags).SingleOrDefault(c => c.GetParameters().Length == 0);
        }

        public static bool IsNullable(Type t)
        {
            ArgumentNullException.ThrowIfNull(t);

            if (t.IsValueType)
            {
                return IsNullableType(t);
            }

            return true;
        }

        public static bool IsNullableType(Type t)
        {
            ArgumentNullException.ThrowIfNull(t);

            return (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Nullable<>));
        }

        public static Type EnsureNotNullableType(Type t)
        {
            return (IsNullableType(t))
                ? Nullable.GetUnderlyingType(t)!
                : t;
        }

        public static Type EnsureNotByRefType(Type t)
        {
            return (t.IsByRef && t.HasElementType)
                ? t.GetElementType()!
                : t;
        }

        public static bool IsGenericDefinition(Type type, Type genericInterfaceDefinition)
        {
            if (!type.IsGenericType)
            {
                return false;
            }

            Type t = type.GetGenericTypeDefinition();
            return (t == genericInterfaceDefinition);
        }

        public static bool ImplementsGenericDefinition(Type type, Type genericInterfaceDefinition)
        {
            return ImplementsGenericDefinition(type, genericInterfaceDefinition, out _);
        }

        public static bool ImplementsGenericDefinition(Type type, Type genericInterfaceDefinition, [NotNullWhen(true)]out Type? implementingType)
        {
            ArgumentNullException.ThrowIfNull(type);
            ArgumentNullException.ThrowIfNull(genericInterfaceDefinition);


            if (!genericInterfaceDefinition.IsInterface || !genericInterfaceDefinition.IsGenericTypeDefinition)
            {
                throw new ArgumentNullException($"'{genericInterfaceDefinition}' is not a generic interface definition.");
            }

            if (type.IsInterface)
            {
                if (type.IsGenericType)
                {
                    Type interfaceDefinition = type.GetGenericTypeDefinition();

                    if (genericInterfaceDefinition == interfaceDefinition)
                    {
                        implementingType = type;
                        return true;
                    }
                }
            }

            foreach (Type i in type.GetInterfaces())
            {
                if (i.IsGenericType)
                {
                    Type interfaceDefinition = i.GetGenericTypeDefinition();

                    if (genericInterfaceDefinition == interfaceDefinition)
                    {
                        implementingType = i;
                        return true;
                    }
                }
            }

            implementingType = null;
            return false;
        }

        public static bool InheritsGenericDefinition(Type type, Type genericClassDefinition)
        {
            return InheritsGenericDefinition(type, genericClassDefinition, out _);
        }

        public static bool InheritsGenericDefinition(Type type, Type genericClassDefinition, out Type? implementingType)
        {
            ArgumentNullException.ThrowIfNull(type);
            ArgumentNullException.ThrowIfNull(genericClassDefinition);

            if (!genericClassDefinition.IsClass || !genericClassDefinition.IsGenericTypeDefinition)
            {
                throw new ArgumentNullException($"'{genericClassDefinition}' is not a generic class definition.");
            }

            return InheritsGenericDefinitionInternal(type, genericClassDefinition, out implementingType);
        }

        private static bool InheritsGenericDefinitionInternal(Type type, Type genericClassDefinition, out Type? implementingType)
        {
            Type? currentType = type;
            do
            {
                if (currentType.IsGenericType && genericClassDefinition == currentType.GetGenericTypeDefinition())
                {
                    implementingType = currentType;
                    return true;
                }

                currentType = currentType.BaseType;
            }
            while (currentType != null);

            implementingType = null;
            return false;
        }

        /// <summary>
        /// Gets the type of the typed collection's items.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <returns>The type of the typed collection's items.</returns>
        public static Type? GetCollectionItemType(Type type)
        {
            ArgumentNullException.ThrowIfNull(type);

            if (type.IsArray)
            {
                return type.GetElementType();
            }
            if (ImplementsGenericDefinition(type, typeof(IEnumerable<>), out Type? genericListType))
            {
                if (genericListType!.IsGenericTypeDefinition)
                {
                    throw new ArgumentNullException($"'{type}' is not a generic class definition.");
                }

                return genericListType!.GetGenericArguments()[0];
            }
            if (typeof(IEnumerable).IsAssignableFrom(type))
            {
                return null;
            }
            throw new ArgumentNullException($"'{type}' is not a collection.");
        }

        public static void GetDictionaryKeyValueTypes(Type dictionaryType, out Type? keyType, out Type? valueType)
        {
            ArgumentNullException.ThrowIfNull(dictionaryType);

            if (ImplementsGenericDefinition(dictionaryType, typeof(IDictionary<,>), out Type? genericDictionaryType))
            {
                if (genericDictionaryType!.IsGenericTypeDefinition)
                {
                    throw new ArgumentNullException($"'{dictionaryType}' is not a dictionary.");
                }

                Type[] dictionaryGenericArguments = genericDictionaryType!.GetGenericArguments();

                keyType = dictionaryGenericArguments[0];
                valueType = dictionaryGenericArguments[1];
                return;
            }
            if (typeof(IDictionary).IsAssignableFrom(dictionaryType))
            {
                keyType = null;
                valueType = null;
                return;
            }
            throw new ArgumentNullException($"'{dictionaryType}' is not a collection.");
        }

        /// <summary>
        /// Gets the member's underlying type.
        /// </summary>
        /// <param name="member">The member.</param>
        /// <returns>The underlying type of the member.</returns>
        public static Type GetMemberUnderlyingType(MemberInfo member)
        {
            ArgumentNullException.ThrowIfNull(member);

            switch (member.MemberType)
            {
                case MemberTypes.Field:
                    return ((FieldInfo)member).FieldType;
                case MemberTypes.Property:
                    return ((PropertyInfo)member).PropertyType;
                case MemberTypes.Event:
                    return ((EventInfo)member).EventHandlerType!;
                case MemberTypes.Method:
                    return ((MethodInfo)member).ReturnType;
                default:
                    throw new ArgumentException("MemberInfo must be of type FieldInfo, PropertyInfo, EventInfo or MethodInfo", nameof(member));
            }
        }

        public static bool IsByRefLikeType(Type type)
        {
            if (!type.IsValueType)
            {
                return false;
            }

            // IsByRefLike flag on type is not available in netstandard2.0
            Attribute[] attributes = GetAttributes(type, null, false);
            for (int i = 0; i < attributes.Length; i++)
            {
                if (string.Equals(attributes[i].GetType().FullName, "System.Runtime.CompilerServices.IsByRefLikeAttribute", StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Determines whether the property is an indexed property.
        /// </summary>
        /// <param name="property">The property.</param>
        /// <returns>
        /// 	<c>true</c> if the property is an indexed property; otherwise, <c>false</c>.
        /// </returns>
        public static bool IsIndexedProperty(PropertyInfo property)
        {
            ArgumentNullException.ThrowIfNull(property);

            return (property.GetIndexParameters().Length > 0);
        }

        /// <summary>
        /// Gets the member's value on the object.
        /// </summary>
        /// <param name="member">The member.</param>
        /// <param name="target">The target object.</param>
        /// <returns>The member's value on the object.</returns>
        public static object? GetMemberValue(MemberInfo member, object target)
        {
            ArgumentNullException.ThrowIfNull(member);
            ArgumentNullException.ThrowIfNull(target);

            switch (member.MemberType)
            {
                case MemberTypes.Field:
                    return ((FieldInfo)member).GetValue(target);
                case MemberTypes.Property:
                    try
                    {
                        return ((PropertyInfo)member).GetValue(target, null);
                    }
                    catch (TargetParameterCountException e)
                    {
                        throw new ArgumentException($"MemberInfo '{member.Name}' has index parameters {e}");
                    }
                default:

                    throw new ArgumentException($"MemberInfo '{member}' is not of type FieldInfo or PropertyInfo");
            }
        }

        /// <summary>
        /// Sets the member's value on the target object.
        /// </summary>
        /// <param name="member">The member.</param>
        /// <param name="target">The target.</param>
        /// <param name="value">The value.</param>
        public static void SetMemberValue(MemberInfo member, object target, object? value)
        {
            ArgumentNullException.ThrowIfNull(member);
            ArgumentNullException.ThrowIfNull(target);

            switch (member.MemberType)
            {
                case MemberTypes.Field:
                    ((FieldInfo)member).SetValue(target, value);
                    break;
                case MemberTypes.Property:
                    ((PropertyInfo)member).SetValue(target, value, null);
                    break;
                default:
                    throw new ArgumentException($"MemberInfo '{member}' must be of type FieldInfo or PropertyInfo");
            }
        }

        /// <summary>
        /// Determines whether the specified MemberInfo can be read.
        /// </summary>
        /// <param name="member">The MemberInfo to determine whether can be read.</param>
        /// /// <param name="nonPublic">if set to <c>true</c> then allow the member to be gotten non-publicly.</param>
        /// <returns>
        /// 	<c>true</c> if the specified MemberInfo can be read; otherwise, <c>false</c>.
        /// </returns>
        public static bool CanReadMemberValue(MemberInfo member, bool nonPublic)
        {
            switch (member.MemberType)
            {
                case MemberTypes.Field:
                    FieldInfo fieldInfo = (FieldInfo)member;

                    if (nonPublic)
                    {
                        return true;
                    }
                    else if (fieldInfo.IsPublic)
                    {
                        return true;
                    }
                    return false;
                case MemberTypes.Property:
                    PropertyInfo propertyInfo = (PropertyInfo)member;

                    if (!propertyInfo.CanRead)
                    {
                        return false;
                    }
                    if (nonPublic)
                    {
                        return true;
                    }
                    return (propertyInfo.GetGetMethod(nonPublic) != null);
                default:
                    return false;
            }
        }

        /// <summary>
        /// Determines whether the specified MemberInfo can be set.
        /// </summary>
        /// <param name="member">The MemberInfo to determine whether can be set.</param>
        /// <param name="nonPublic">if set to <c>true</c> then allow the member to be set non-publicly.</param>
        /// <param name="canSetReadOnly">if set to <c>true</c> then allow the member to be set if read-only.</param>
        /// <returns>
        /// 	<c>true</c> if the specified MemberInfo can be set; otherwise, <c>false</c>.
        /// </returns>
        public static bool CanSetMemberValue(MemberInfo member, bool nonPublic, bool canSetReadOnly)
        {
            switch (member.MemberType)
            {
                case MemberTypes.Field:
                    FieldInfo fieldInfo = (FieldInfo)member;

                    if (fieldInfo.IsLiteral)
                    {
                        return false;
                    }
                    if (fieldInfo.IsInitOnly && !canSetReadOnly)
                    {
                        return false;
                    }
                    if (nonPublic)
                    {
                        return true;
                    }
                    if (fieldInfo.IsPublic)
                    {
                        return true;
                    }
                    return false;
                case MemberTypes.Property:
                    PropertyInfo propertyInfo = (PropertyInfo)member;

                    if (!propertyInfo.CanWrite)
                    {
                        return false;
                    }
                    if (nonPublic)
                    {
                        return true;
                    }
                    return (propertyInfo.GetSetMethod(nonPublic) != null);
                default:
                    return false;
            }
        }

        private static bool IsOverridenGenericMember(MemberInfo memberInfo, BindingFlags bindingAttr)
        {
            if (memberInfo.MemberType != MemberTypes.Property)
            {
                return false;
            }

            PropertyInfo propertyInfo = (PropertyInfo)memberInfo;
            if (!IsVirtual(propertyInfo))
            {
                return false;
            }

            Type declaringType = propertyInfo.DeclaringType!;
            if (!declaringType.IsGenericType)
            {
                return false;
            }
            Type genericTypeDefinition = declaringType.GetGenericTypeDefinition();
            if (genericTypeDefinition == null)
            {
                return false;
            }
            MemberInfo[] members = genericTypeDefinition.GetMember(propertyInfo.Name, bindingAttr);
            if (members.Length == 0)
            {
                return false;
            }
            Type memberUnderlyingType = GetMemberUnderlyingType(members[0]);
            if (!memberUnderlyingType.IsGenericParameter)
            {
                return false;
            }

            return true;
        }

        public static T? GetAttribute<T>(object attributeProvider) where T : Attribute
        {
            return GetAttribute<T>(attributeProvider, true);
        }

        public static T? GetAttribute<T>(object attributeProvider, bool inherit) where T : Attribute
        {
            T[] attributes = GetAttributes<T>(attributeProvider, inherit);

            return attributes?.FirstOrDefault();
        }

        public static T[] GetAttributes<T>(object attributeProvider, bool inherit) where T : Attribute
        {
            Attribute[] a = GetAttributes(attributeProvider, typeof(T), inherit);

            if (a is T[] attributes)
            {
                return attributes;
            }

            return a.Cast<T>().ToArray();
        }

        public static Attribute[] GetAttributes(object attributeProvider, Type? attributeType, bool inherit)
        {
            ArgumentNullException.ThrowIfNull(attributeProvider);


            object provider = attributeProvider;

            // http://hyperthink.net/blog/getcustomattributes-gotcha/
            // ICustomAttributeProvider doesn't do inheritance

            switch (provider)
            {
                case Type t:
                    object[] array = attributeType != null ? t.GetCustomAttributes(attributeType, inherit) : t.GetCustomAttributes(inherit);
                    Attribute[] attributes = array.Cast<Attribute>().ToArray();

#if (NET20 || NET35)
                    // ye olde .NET GetCustomAttributes doesn't respect the inherit argument
                    if (inherit && t.BaseType != null)
                    {
                        attributes = attributes.Union(GetAttributes(t.BaseType, attributeType, inherit)).ToArray();
                    }
#endif

                    return attributes;
                case Assembly a:
                    return (attributeType != null) ? Attribute.GetCustomAttributes(a, attributeType) : Attribute.GetCustomAttributes(a);
                case MemberInfo mi:
                    return (attributeType != null) ? Attribute.GetCustomAttributes(mi, attributeType, inherit) : Attribute.GetCustomAttributes(mi, inherit);
#if !PORTABLE40
                case Module m:
                    return (attributeType != null) ? Attribute.GetCustomAttributes(m, attributeType, inherit) : Attribute.GetCustomAttributes(m, inherit);
#endif
                case ParameterInfo p:
                    return (attributeType != null) ? Attribute.GetCustomAttributes(p, attributeType, inherit) : Attribute.GetCustomAttributes(p, inherit);
                default:
#if !PORTABLE40
                    ICustomAttributeProvider customAttributeProvider = (ICustomAttributeProvider)attributeProvider;
                    object[] result = (attributeType != null) ? customAttributeProvider.GetCustomAttributes(attributeType, inherit) : customAttributeProvider.GetCustomAttributes(inherit);

                    return (Attribute[])result;
#else
                    throw new Exception("Cannot get attributes from '{0}'.".FormatWith(CultureInfo.InvariantCulture, provider));
#endif
            }
        }


        private static int? GetAssemblyDelimiterIndex(string fullyQualifiedTypeName)
        {
            // we need to get the first comma following all surrounded in brackets because of generic types
            // e.g. System.Collections.Generic.Dictionary`2[[System.String, mscorlib,Version=2.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089],[System.String, mscorlib, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089]], mscorlib, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089
            int scope = 0;
            for (int i = 0; i < fullyQualifiedTypeName.Length; i++)
            {
                char current = fullyQualifiedTypeName[i];
                switch (current)
                {
                    case '[':
                        scope++;
                        break;
                    case ']':
                        scope--;
                        break;
                    case ',':
                        if (scope == 0)
                        {
                            return i;
                        }
                        break;
                }
            }

            return null;
        }

        public static MemberInfo? GetMemberInfoFromType(Type targetType, MemberInfo memberInfo)
        {
            const BindingFlags bindingAttr = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

            switch (memberInfo.MemberType)
            {
                case MemberTypes.Property:
                    PropertyInfo propertyInfo = (PropertyInfo)memberInfo;

                    Type[] types = propertyInfo.GetIndexParameters().Select(p => p.ParameterType).ToArray();

                    return targetType.GetProperty(propertyInfo.Name, bindingAttr, null, propertyInfo.PropertyType, types, null);
                default:
                    return targetType.GetMember(memberInfo.Name, memberInfo.MemberType, bindingAttr).SingleOrDefault();
            }
        }


        public static BindingFlags RemoveFlag(this BindingFlags bindingAttr, BindingFlags flag)
        {
            return ((bindingAttr & flag) == flag)
                ? bindingAttr ^ flag
                : bindingAttr;
        }


        public static bool IsMethodOverridden(Type currentType, Type methodDeclaringType, string method)
        {
            bool isMethodOverriden = currentType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Any(info =>
                    info.Name == method &&
                    // check that the method overrides the original on DynamicObjectProxy
                    info.DeclaringType != methodDeclaringType
                    && info.GetBaseDefinition().DeclaringType == methodDeclaringType
                );

            return isMethodOverriden;
        }
    }
}
