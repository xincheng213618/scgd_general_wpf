using ProjectARVRPro.Process;
using ProjectARVRPro.Process.AOI;
using ProjectARVRPro.Process.Blank;
using ProjectARVRPro.Process.Chessboard;
using ProjectARVRPro.Process.Demura;
using ProjectARVRPro.Process.DemuraAOI;
using ProjectARVRPro.Process.MTF.MTFH;
using ProjectARVRPro.Process.MTF.MTFHV;
using Xunit;

namespace ProjectARVRPro.Tests
{
    public class ProcessTypeCatalogTests
    {
        [Fact]
        public void CatalogUsesRequestedCategoriesAndOmitsAutomaticBlankChoice()
        {
            IReadOnlyList<ProcessTypeOption> options = ProcessTypeCatalog.CreateOptions(
            [
                new BlankProcess(),
                new ChessboardProcess(),
                new AOIProcess(),
                new DemuraProcess(),
                new DemuraAoiProcess()
            ]);

            Assert.DoesNotContain(options, option => option.Process is BlankProcess);
            Assert.Equal(ProcessTypeCatalog.BlankCategory, ProcessTypeCatalog.GetCategory(typeof(BlankProcess)));
            Assert.Equal(ProcessTypeCatalog.ArvrCategory, Assert.Single(options, option => option.Process is ChessboardProcess).Category);
            Assert.Equal(ProcessTypeCatalog.AoiCategory, Assert.Single(options, option => option.Process is AOIProcess).Category);
            Assert.All(options.Where(option => option.Process is DemuraProcess or DemuraAoiProcess),
                option => Assert.Equal(ProcessTypeCatalog.DemuraCategory, option.Category));
        }

        [Fact]
        public void CatalogProvidesPurposeAndCapabilityDocumentation()
        {
            ProcessTypeOption option = Assert.Single(
                ProcessTypeCatalog.CreateOptions([new ChessboardProcess()]));

            Assert.False(string.IsNullOrWhiteSpace(option.Description));
            Assert.False(string.IsNullOrWhiteSpace(option.Capabilities));
            Assert.Contains("对比度", option.Capabilities);
        }

        [Fact]
        public void CatalogGroupsRelatedArvrProcessesTogether()
        {
            IReadOnlyList<ProcessTypeOption> options = ProcessTypeCatalog.CreateOptions(
            [
                new MTFHProcess(),
                new MTFHVProcess(),
                new ChessboardProcess(),
                new ChessboardDynamicProcess()
            ]);

            Assert.All(options.Where(option => option.Process is MTFHProcess or MTFHVProcess),
                option => Assert.Equal(ProcessTypeCatalog.MtfSubcategory, option.Subcategory));
            Assert.All(options.Where(option => option.Process is ChessboardProcess or ChessboardDynamicProcess),
                option => Assert.Equal(ProcessTypeCatalog.ChessboardSubcategory, option.Subcategory));
        }
    }
}
