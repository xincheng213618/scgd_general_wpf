using ColorVision.Engine.Templates;
using System.Runtime.CompilerServices;

namespace ColorVision.UI.Tests;

public sealed class TemplateSearchProviderTests
{
    [Fact]
    public void IdenticalNamesInDifferentTemplateRegistrationsHaveStableDistinctIdentities()
    {
        FakeTemplate first = FakeTemplate.Create("First kind", "Preset");
        FakeTemplate second = FakeTemplate.Create("Second kind", "Preset");
        Dictionary<string, ITemplate> registrations = new() { ["first"] = first, ["second"] = second };

        ISearch[] results = TemplateSearchProvider.CreateItems(registrations, key => registrations.GetValueOrDefault(key)).ToArray();
        ISearch[] repeated = TemplateSearchProvider.CreateItems(registrations, key => registrations.GetValueOrDefault(key)).ToArray();

        Assert.Equal(2, results.Length);
        Assert.Equal(2, results.Select(result => result.GuidId).Distinct().Count());
        Assert.Equal(results.Select(result => result.GuidId), repeated.Select(result => result.GuidId));
        Assert.All(results, result => Assert.Equal("Templates", Assert.IsType<SearchMeta>(result).CategoryKey));
        Assert.Contains(results, result => Assert.IsType<SearchMeta>(result).Description == "Second kind");
        Assert.Equal(0, first.OpenCalls + second.OpenCalls);
    }

    [Fact]
    public void OldResultsResolveTheCurrentRegistrationAndCannotOpenARemovedTemplate()
    {
        FakeTemplate first = FakeTemplate.Create("First", "Preset");
        Dictionary<string, ITemplate> registrations = new() { ["kind"] = first };
        ISearch result = Assert.Single(TemplateSearchProvider.CreateItems(registrations, key => registrations.GetValueOrDefault(key)));
        FakeTemplate replacement = FakeTemplate.Create("Replacement", "Preset");
        registrations["kind"] = replacement;
        Assert.True(result.Command!.CanExecute(null));
        result.Command.Execute(null);
        Assert.Equal(0, first.OpenCalls);
        Assert.Equal(1, replacement.OpenCalls);

        registrations.Clear();
        Assert.False(result.Command.CanExecute(null));
        result.Command.Execute(null);
        Assert.Equal(1, replacement.OpenCalls);
    }

    private sealed class FakeTemplate : ITemplate
    {
        private List<string> _names = [];
        public int OpenCalls;
        public static FakeTemplate Create(string title, string name)
        {
            // Bypass ITemplate's production registry constructor; this fixture
            // contains only in-memory names and a harmless navigation callback.
            var template = (FakeTemplate)RuntimeHelpers.GetUninitializedObject(typeof(FakeTemplate));
            template._names = [name];
            template.Name = title;
            template.Title = title;
            template.IsSideHide = true;
            return template;
        }
        public override List<string> GetTemplateNames() => _names;
        public override int GetTemplateIndex(string templateName) => _names.IndexOf(templateName);
        public override void PreviewMouseDoubleClick(int index) => OpenCalls++;
    }
}
