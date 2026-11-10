using Tenlith.Api;
using Xunit;

namespace Tenlith.Api.Tests;

public sealed class ConflictTests
{
    [Fact]
    public void Serialization_conflicts_are_recognized_through_execution_strategy_wrappers()
    {
        var postgres = new Npgsql.PostgresException(
            "could not serialize access",
            "ERROR",
            "ERROR",
            "40001"
        );
        var wrapped = new InvalidOperationException(
            "Likely transient failure",
            new Microsoft.EntityFrameworkCore.DbUpdateException("Save failed", postgres)
        );
        Assert.True(Rules.IsWriteConflict(wrapped));
        Assert.False(Rules.IsWriteConflict(new InvalidOperationException("An unrelated bug")));
    }
}
