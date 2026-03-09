using Pilot.Core;

namespace Pilot.Tests;

public class PilotCoreBootstrapTests
{
    [Test]
    public async Task ProjectNameMatchesTheCoreAssemblyName()
    {
        var assemblyName = typeof(PilotCoreMarker).Assembly.GetName().Name;

        await Assert.That(assemblyName).IsEqualTo(PilotCoreMarker.ProjectName);
    }
}
