using ColorVision.Engine.Templates.Jsons.MTF2;
using ProjectARVRPro.Recipe;
using System.IO;

namespace ProjectARVRPro.Process.MTF.MTF07
{
    internal static class MTF07DynamicResultBuilder
    {
        public static void PopulateItem(ObjectiveTestItem item, MTFItem mtf, RecipeBase recipe, string showConfig, string unit, MtfRecipeCorrection correction)
        {
            ArgumentNullException.ThrowIfNull(item);
            ArgumentNullException.ThrowIfNull(mtf);
            ArgumentNullException.ThrowIfNull(recipe);

            if (mtf.mtfValue is not double rawValue || !double.IsFinite(rawValue))
                throw new InvalidDataException($"MTF 测量点 [{mtf.name}] 缺少有限的测量值。");

            double value = correction.Apply(rawValue, recipe, mtf.name);
            item.Unit = unit;
            item.Value = value;
            item.TestValue = value.ToString(showConfig);
            item.LowLimit = recipe.Min;
            item.UpLimit = recipe.Max;
        }
    }
}
