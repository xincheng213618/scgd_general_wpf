using ColorVision.FloatingBall;

namespace ColorVision.UI.Tests
{
    public sealed class DesktopPetAssetCatalogTests
    {
        [Fact]
        public async Task CatalogAlwaysProvidesTheColorVisionDefaultPet()
        {
            var assets = await DesktopPetAssetCatalog.Shared.RefreshAsync();

            var defaultPet = Assert.Single(assets, asset => asset.Id == DesktopPetAssetCatalog.DefaultAssetId);
            Assert.Equal("小彩", defaultPet.DisplayName);
            Assert.Equal(DesktopPetAssetSource.ColorVisionBuiltIn, defaultPet.Source);
            Assert.False(defaultPet.IsSpriteSheet);
            Assert.Equal(
                "pack://application:,,,/ColorVision;component/Assets/Pets/xiaocai.png",
                defaultPet.StaticImageUri);
        }

        [Fact]
        public void AnimationPlanUsesTheExpectedCodexSpriteRows()
        {
            Assert.All(DesktopPetAnimationPlan.GetFrames(DesktopPetActivityState.Idle), frame => Assert.Equal(0, frame.Row));
            Assert.All(DesktopPetAnimationPlan.GetFrames(DesktopPetActivityState.Running), frame => Assert.Equal(7, frame.Row));
            Assert.All(DesktopPetAnimationPlan.GetFrames(DesktopPetActivityState.RunningLeft), frame => Assert.Equal(2, frame.Row));
            Assert.All(DesktopPetAnimationPlan.GetFrames(DesktopPetActivityState.RunningRight), frame => Assert.Equal(1, frame.Row));
            Assert.All(DesktopPetAnimationPlan.GetFrames(DesktopPetActivityState.Waiting), frame => Assert.Equal(6, frame.Row));
            Assert.All(DesktopPetAnimationPlan.GetFrames(DesktopPetActivityState.Review), frame => Assert.Equal(8, frame.Row));
            Assert.All(DesktopPetAnimationPlan.GetFrames(DesktopPetActivityState.Waving), frame => Assert.Equal(3, frame.Row));
            Assert.All(DesktopPetAnimationPlan.GetFrames(DesktopPetActivityState.Failed), frame => Assert.Equal(5, frame.Row));
            Assert.All(DesktopPetAnimationPlan.GetFrames(DesktopPetActivityState.Jumping), frame => Assert.Equal(4, frame.Row));
        }

        [Theory]
        [InlineData(DesktopPetActivityState.Idle, 3.99, DesktopPetActivityState.Idle)]
        [InlineData(DesktopPetActivityState.Waiting, -3.99, DesktopPetActivityState.Waiting)]
        [InlineData(DesktopPetActivityState.Idle, 4, DesktopPetActivityState.RunningRight)]
        [InlineData(DesktopPetActivityState.RunningLeft, 12, DesktopPetActivityState.RunningRight)]
        [InlineData(DesktopPetActivityState.Idle, -4, DesktopPetActivityState.RunningLeft)]
        [InlineData(DesktopPetActivityState.RunningRight, -12, DesktopPetActivityState.RunningLeft)]
        public void DragAnimationUsesTheCodexHorizontalDirectionThreshold(
            DesktopPetActivityState currentState,
            double deltaX,
            DesktopPetActivityState expectedState)
        {
            Assert.Equal(expectedState, DesktopPetAnimationPlan.ResolveDragState(currentState, deltaX));
        }

        [Fact]
        public async Task CurrentCodexInstallProvidesDecodableBuiltInPetsWhenPresent()
        {
            var catalog = DesktopPetAssetCatalog.Shared;
            var assets = await catalog.RefreshAsync();
            if (catalog.CodexArchivePath == null)
                return;

            var codexAssets = assets
                .Where(asset => asset.Source == DesktopPetAssetSource.CodexBuiltIn)
                .ToArray();

            Assert.Equal(9, codexAssets.Length);
            Assert.Contains(codexAssets, asset => asset.Id == "codex-builtin:codex");
            foreach (var asset in codexAssets)
            {
                using var spriteSheet = DesktopPetSpriteSheet.Load(
                    asset.ReadSpriteSheetBytes(),
                    asset.SpriteVersionNumber);
                Assert.Equal(DesktopPetSpriteSheet.Version2RowCount, spriteSheet.RowCount);
                Assert.Equal(192, spriteSheet.FrameWidth);
                Assert.Equal(208, spriteSheet.FrameHeight);
                Assert.True(spriteSheet.GetFrame(0, 0).IsFrozen);
            }
        }
    }
}
