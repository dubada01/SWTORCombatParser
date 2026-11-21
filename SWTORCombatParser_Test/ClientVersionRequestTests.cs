using NUnit.Framework;
using SWTORCombatParser.Model.CloudRaiding;

namespace SWTORCombatParser_Test;

public class ClientVersionRequestTests
{
    [Test]
    public void LaunchCheckKeepsTheExistingRouteAndEscapesMetadata()
    {
        var uri = ClientVersionRequest.BuildUri("https://example.test/api/", "2.4.0+test & next", "linux");
        Assert.That(uri.AbsolutePath, Is.EqualTo("/api/SoftwareVersion"));
        Assert.That(uri.Query, Is.EqualTo("?client=desktop&version=2.4.0%2Btest%20%26%20next&platform=linux"));
    }
}
