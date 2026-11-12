#pragma warning disable CA1707,CA1852,CS8601
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Reflection;
using System.Resources;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace ColorVision.UI
{
    public static partial class PropertyEditorHelper
    {
        internal static string? GetDisplayMetadata(MemberInfo member, Func<DisplayAttribute, string?> selector)
        {
            var display = member.GetCustomAttribute<DisplayAttribute>();
            if (display == null) return null;
            try { return selector(display); }
            catch (InvalidOperationException ex)
            {
                Log.Warn($"Invalid display resource on '{member.DeclaringType?.FullName}.{member.Name}'; using legacy metadata.", ex);
                return null;
            }
        }

        public static string GetDisplayName(ResourceManager? rm, PropertyInfo prop, string? overrideName = null)
        {
            var displayNameAttr = prop.GetCustomAttribute<DisplayNameAttribute>();
            var metadataProvider = MetadataProviderContext.Value;
            var metadataName = metadataProvider?.IsPropertyManaged(prop) == true
                ? metadataProvider.GetDisplayName(prop)
                : null;
            var raw = overrideName ?? metadataName ?? displayNameAttr?.DisplayName ?? GetDisplayMetadata(prop, display => display.GetName()) ?? prop.Name;
            return GetLocalizedString(rm, raw);
        }

        public static string GetDescription(ResourceManager? rm, PropertyInfo prop)
        {
            var metadataProvider = MetadataProviderContext.Value;
            var metadataDescription = metadataProvider?.IsPropertyManaged(prop) == true
                ? metadataProvider.GetDescription(prop)
                : null;
            var raw = metadataDescription ?? prop.GetCustomAttribute<DescriptionAttribute>()?.Description ?? GetDisplayMetadata(prop, display => display.GetDescription());

            return GetLocalizedString(rm, raw);
        }

        public static string GetLocalizedString(ResourceManager? rm, string? key)
        {
            if (rm == null || string.IsNullOrWhiteSpace(key))
            {
                return key ?? string.Empty;
            }

            var culture = CultureInfo.CurrentUICulture;
            return ResourceStringCache.GetOrAdd((rm, culture.Name, key), _ =>
            {
                try
                {
                    return rm.GetString(key, culture) ?? key;
                }
                catch
                {
                    return key;
                }
            });
        }

        public static TextBlock CreateLabel(PropertyInfo property, ResourceManager? rm)
        {
            var desc = GetDescription(rm, property);
            var tb = new TextBlock
            {
                Text = GetDisplayName(rm, property),
                MinWidth = LabelMinWidth,
                ToolTip = string.IsNullOrWhiteSpace(desc) ? null : desc
            };
            tb.SetResourceReference(TextBlock.ForegroundProperty, "GlobalTextBrush");

            return tb;
        }

        public static Binding CreateTwoWayBinding(object source, string path)
        {
            return new Binding(path)
            {
                Source = source,
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
                ValidatesOnExceptions = true,
                ValidatesOnDataErrors = true,
                NotifyOnValidationError = true
            };
        }

        public static Binding CreateTwoWayBinding(object source, PropertyInfo property, UpdateSourceTrigger defaultUpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged)
        {
            ArgumentNullException.ThrowIfNull(property);
            Binding binding = CreateTwoWayBinding(source, property.Name);
            if (!property.CanWrite || property.GetCustomAttribute<ReadOnlyAttribute>()?.IsReadOnly == true)
                binding.Mode = BindingMode.OneWay;
            binding.UpdateSourceTrigger = property.GetCustomAttribute<PropertyEditorTypeAttribute>()?.UpdateSourceTrigger
                ?? defaultUpdateSourceTrigger;
            return binding;
        }

        public static TextBox CreateSmallTextBox(Binding binding)
        {
            var tb = new TextBox
            {
                Margin = new Thickness(5, 0, 0, 0),
                Style = TextBoxSmallStyle
            };
            tb.SetBinding(TextBox.TextProperty, binding);
            return tb;
        }

        public static Button CreateIconSpinButton(ICommand command)
        {
            var btn = new Button
            {
                Width = 25,
                Height = 25,
                Margin = new Thickness(5, 1, 5, 0),
                HorizontalAlignment = HorizontalAlignment.Left,
                Padding = new Thickness(2),
                Command = command
            };

            var glyph = new TextBlock
            {
                Text = "\uE713",
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 16,
                HorizontalAlignment = HorizontalAlignment.Center,
                RenderTransformOrigin = new Point(0.5, 0.5),
            };
            glyph.SetResourceReference(TextBlock.ForegroundProperty, "GlobalTextBrush");

            var rotate = new RotateTransform();
            glyph.RenderTransform = rotate;
            btn.Content = glyph;

            var anim = new DoubleAnimation
            {
                From = 0,
                To = 360,
                Duration = new Duration(TimeSpan.FromSeconds(0.5)),
                FillBehavior = FillBehavior.Stop
            };

            var storyboard = new Storyboard();
            storyboard.Children.Add(anim);
            Storyboard.SetTarget(anim, rotate);
            Storyboard.SetTargetProperty(anim, new PropertyPath(RotateTransform.AngleProperty));

            btn.Click += (_, __) => storyboard.Begin();
            return btn;
        }

        /// <summary>
        /// Converts a value to the specified target type, handling string-to-numeric conversions.
        /// </summary>
        /// <param name="value">The value to convert</param>
        /// <param name="targetType">The target type to convert to</param>
        /// <returns>The converted value, or a default value if conversion fails</returns>
        public static object? ConvertToTargetType(object? value, Type targetType)
        {
            if (value == null)
            {
                return targetType.IsValueType ? Activator.CreateInstance(targetType) : null;
            }

            var valueType = value.GetType();
            
            // If the value is already the correct type, return it directly
            if (valueType == targetType || targetType.IsAssignableFrom(valueType))
            {
                return value;
            }

            // Handle nullable types
            var underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;

            // Convert from string for numeric types
            if (value is string strValue)
            {
                if (string.IsNullOrWhiteSpace(strValue))
                {
                    return underlyingType.IsValueType ? Activator.CreateInstance(underlyingType) : null;
                }

                try
                {
                    if (underlyingType == typeof(int))
                        return int.Parse(strValue, System.Globalization.CultureInfo.InvariantCulture);
                    if (underlyingType == typeof(long))
                        return long.Parse(strValue, System.Globalization.CultureInfo.InvariantCulture);
                    if (underlyingType == typeof(short))
                        return short.Parse(strValue, System.Globalization.CultureInfo.InvariantCulture);
                    if (underlyingType == typeof(byte))
                        return byte.Parse(strValue, System.Globalization.CultureInfo.InvariantCulture);
                    if (underlyingType == typeof(uint))
                        return uint.Parse(strValue, System.Globalization.CultureInfo.InvariantCulture);
                    if (underlyingType == typeof(ulong))
                        return ulong.Parse(strValue, System.Globalization.CultureInfo.InvariantCulture);
                    if (underlyingType == typeof(ushort))
                        return ushort.Parse(strValue, System.Globalization.CultureInfo.InvariantCulture);
                    if (underlyingType == typeof(sbyte))
                        return sbyte.Parse(strValue, System.Globalization.CultureInfo.InvariantCulture);
                    if (underlyingType == typeof(float))
                        return float.Parse(strValue, System.Globalization.CultureInfo.InvariantCulture);
                    if (underlyingType == typeof(double))
                        return double.Parse(strValue, System.Globalization.CultureInfo.InvariantCulture);
                    if (underlyingType == typeof(decimal))
                        return decimal.Parse(strValue, System.Globalization.CultureInfo.InvariantCulture);
                    if (underlyingType == typeof(bool))
                        return bool.Parse(strValue);

                    return Convert.ChangeType(strValue, underlyingType, System.Globalization.CultureInfo.InvariantCulture);
                }
                catch
                {
                    return underlyingType.IsValueType ? Activator.CreateInstance(underlyingType) : null;
                }
            }

            // Try direct conversion for other types
            try
            {
                return Convert.ChangeType(value, underlyingType, System.Globalization.CultureInfo.InvariantCulture);
            }
            catch
            {
                return value;
            }
        }
    }
}
