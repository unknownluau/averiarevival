using System.Text.Json;
using Roblox.Services.App.FeatureFlags;
using Roblox.Services.Exceptions;

namespace Roblox.UnitTest;

public class FeatureFlagsTests
{
    [Fact]
    public void DeserializeSnapshot_DefaultsEveryFlagToEnabled_WhenPayloadIsMissing()
    {
        var snapshot = FeatureFlags.DeserializeSnapshotForTests(null);

        foreach (var flag in Enum.GetValues<FeatureFlag>())
        {
            Assert.True(snapshot[flag]);
        }
    }

    [Fact]
    public void DeserializeSnapshot_PreservesExplicitDisabledFlag_AndDefaultsMissingFlagsEnabled()
    {
        var payload = JsonSerializer.Serialize(new Dictionary<FeatureFlag, bool>
        {
            [FeatureFlag.TradingEnabled] = false,
        });

        var snapshot = FeatureFlags.DeserializeSnapshotForTests(payload);

        Assert.False(snapshot[FeatureFlag.TradingEnabled]);
        Assert.True(snapshot[FeatureFlag.GroupsEnabled]);
    }

    [Fact]
    public void GetAllFlags_ReturnsCopyThatCannotMutateInternalSnapshot()
    {
        FeatureFlags.ReplaceSnapshotForTests(new Dictionary<FeatureFlag, bool>
        {
            [FeatureFlag.TradingEnabled] = false,
        });

        var returned = FeatureFlags.GetAllFlags();
        ((IDictionary<FeatureFlag, bool>)returned)[FeatureFlag.TradingEnabled] = true;

        Assert.True(FeatureFlags.IsDisabled(FeatureFlag.TradingEnabled));
    }

    [Fact]
    public void FeatureCheck_ThrowsUnavailable_WhenFlagIsDisabled()
    {
        FeatureFlags.ReplaceSnapshotForTests(new Dictionary<FeatureFlag, bool>
        {
            [FeatureFlag.TradingEnabled] = false,
        });

        var exception = Assert.Throws<RobloxException>(() => FeatureFlags.FeatureCheck(FeatureFlag.TradingEnabled));

        Assert.Equal(503, exception.statusCode);
        Assert.Equal("Feature temporarily unavailable", exception.errorMessage);
    }
}
