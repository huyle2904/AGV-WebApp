using Microsoft.EntityFrameworkCore;
using NewAGV.Persistence;

namespace NewAGV.Api.Tests.TestSupport;

internal static class DbContextFactory
{
    public static NewAgvDbContext Create()
    {
        var options = new DbContextOptionsBuilder<NewAgvDbContext>()
            .UseInMemoryDatabase($"newagv-tests-{Guid.NewGuid():N}")
            .Options;

        return new NewAgvDbContext(options);
    }
}
