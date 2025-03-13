#pragma warning disable CS8602,CS8604
using System.Globalization;
using System.Windows.Controls;

namespace ColorVision.Engine.Templates.POI
{
    public class RadiusValidationRule : ValidationRule
    {
        public override ValidationResult Validate(object value, CultureInfo cultureInfo)
        {
            if (double.TryParse(value.ToString(), out double result))
            {
                if (result % 1 == 0 || result % 1 == 0.5)
                {
                    return ValidationResult.ValidResult;
                }
            }
            return new ValidationResult(false, "Value must be an integer or end with .5");
        }
    }
}
