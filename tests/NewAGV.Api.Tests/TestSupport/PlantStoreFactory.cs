using Microsoft.Extensions.Options;
using NewAGV.Api.Services;

namespace NewAGV.Api.Tests.TestSupport;

internal static class PlantStoreFactory
{
    public static AgvPlantStore Create(bool seedDemoData = false)
    {
        return new AgvPlantStore(Options.Create(new IntegrationOptions
        {
            SeedDemoData = seedDemoData,
            EnableSimulation = seedDemoData
        }));
    }
}
