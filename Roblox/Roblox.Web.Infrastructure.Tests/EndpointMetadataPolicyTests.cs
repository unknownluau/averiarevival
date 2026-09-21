using Roblox.Web.Infrastructure.Metadata;

namespace Roblox.Web.Infrastructure.Tests;

public class EndpointMetadataPolicyTests
{
    [Fact]
    public void DefaultEndpoint_HasNoExplicitRobloxRequirements()
    {
        var endpoint = InfrastructureTestHelpers.Endpoint();

        Assert.False(endpoint.HasExplicitRobloxRequestRequirement());
        Assert.False(endpoint.IsInternalServiceOnly());
        Assert.False(endpoint.RequiresRobloxSession());
        Assert.False(endpoint.RequiresRobloxCsrf());
        Assert.False(endpoint.RequiresRccRequest());
        Assert.False(endpoint.RequiresRobloxClient());
    }

    [Fact]
    public void ProtectedMetadata_IsDetectedIndependently()
    {
        var endpoint = InfrastructureTestHelpers.Endpoint(
            new InternalServiceOnlyAttribute(),
            new RequireRobloxSessionAttribute(),
            new RequireRobloxCsrfAttribute(),
            new RequireRccRequestAttribute(),
            new RequireRobloxClientAttribute());

        Assert.True(endpoint.HasExplicitRobloxRequestRequirement());
        Assert.True(endpoint.IsInternalServiceOnly());
        Assert.True(endpoint.RequiresRobloxSession());
        Assert.True(endpoint.RequiresRobloxCsrf());
        Assert.True(endpoint.RequiresRccRequest());
        Assert.True(endpoint.RequiresRobloxClient());
    }

    [Fact]
    public void DocumentationAndLegacyCsrfMetadata_DoNotCreateProtectedRequestRequirement()
    {
        var endpoint = InfrastructureTestHelpers.Endpoint(
            new AllowRobloxAnonymousAttribute(),
            new BrowserFacingEndpointAttribute(),
            new SkipRobloxCsrfAttribute());

        Assert.False(endpoint.HasExplicitRobloxRequestRequirement());
        Assert.False(endpoint.RequiresRobloxCsrf());
        Assert.True(endpoint.ShouldSkipRobloxCsrf());
    }
}
