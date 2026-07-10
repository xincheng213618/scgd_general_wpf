using Newtonsoft.Json;
using ProjectARVRPro.Process.Black;
using Xunit;

namespace ProjectARVRPro.Tests
{
    public class RecipeConfigPersistenceTests
    {
        [Fact]
        public void RecipeConfigRoundTripsWithConcreteRecipeTypes()
        {
            var config = new RecipeConfig();
            config.GetRequiredService<BlackRecipeConfig>().FOFOContrast.Min = 123.45;
            var settings = new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All };

            string json = JsonConvert.SerializeObject(config, settings);
            RecipeConfig? restored = JsonConvert.DeserializeObject<RecipeConfig>(json, settings);

            Assert.NotNull(restored);
            Assert.Equal(123.45, restored.GetRequiredService<BlackRecipeConfig>().FOFOContrast.Min);
        }
    }
}
