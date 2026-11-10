using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Tenlith.Api;

// Design-time construction does not connect to or seed a database.
public sealed class DesignTimeDbFactory : IDesignTimeDbContextFactory<AppDb>
{
    public AppDb CreateDbContext(string[] args) =>
        new(
            new DbContextOptionsBuilder<AppDb>()
                .UseNpgsql(
                    Environment.GetEnvironmentVariable("ConnectionStrings__Database")
                        ?? "Host=localhost;Database=tenlith;Username=tenlith"
                )
                .Options
        );
}
