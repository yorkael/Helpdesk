using Microsoft.EntityFrameworkCore;

namespace Helpdesk.Tests.Persistence;

public class ModelSnapshotTests
{
    [Fact]
    public void Model_has_no_changes_missing_from_migrations()
    {
        using var context = PostgreSqlFixture.CreateContext("Host=localhost;Database=unused");

        Assert.False(
            context.Database.HasPendingModelChanges(),
            "The EF model changed without a migration. Run 'dotnet ef migrations add <Name>'.");
    }
}
