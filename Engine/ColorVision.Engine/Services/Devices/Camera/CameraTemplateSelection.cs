using ColorVision.Engine.Templates;

namespace ColorVision.Engine.Services.Devices.Camera
{
    internal static class CameraTemplateSelection
    {
        internal static T ResolveOptional<T>(object? selectedValue) where T : ParamBase, new()
        {
            return selectedValue is T template && !IsUninitialized(template)
                ? template
                : TemplatesExtension.CreateEmptyParam<T>();
        }

        internal static bool TryResolveRequired<T>(object? selectedValue, out T template) where T : ParamBase
        {
            // Template reload replaces Value without refreshing ComboBox.SelectedValue's cached object.
            if (selectedValue is TemplateModel<T> model) selectedValue = model.Value;
            if (selectedValue is T selected && selected.Id != -1 && !IsUninitialized(selected))
            {
                template = selected;
                return true;
            }

            template = null!;
            return false;
        }

        private static bool IsUninitialized(ParamBase template)
        {
            return template.Id == 0 && string.IsNullOrWhiteSpace(template.Name);
        }
    }
}
