using ColorVision.Database;
using System;
using System.Collections.Generic;

namespace ColorVision.UI.Tests;

public sealed class ExportMySqlInitTablesTests
{
    [Fact]
    public void PerTableFailureContinuesRemainingTablesAndFailsOperation()
    {
        List<Type> attemptedTypes = new();

        AggregateException exception = Assert.Throws<AggregateException>(() =>
            MySqlTableInitializer.InitTableTypes(
                [typeof(FailingTable), typeof(SucceedingTable)],
                type =>
                {
                    attemptedTypes.Add(type);
                    if (type == typeof(FailingTable))
                    {
                        throw new InvalidOperationException("simulated database failure");
                    }
                }));

        Assert.Equal([typeof(FailingTable), typeof(SucceedingTable)], attemptedTypes);
        InvalidOperationException tableFailure = Assert.IsType<InvalidOperationException>(Assert.Single(exception.InnerExceptions));
        Assert.Contains(typeof(FailingTable).FullName!, tableFailure.Message, StringComparison.Ordinal);
        Assert.Equal("simulated database failure", tableFailure.InnerException?.Message);
    }

    [Fact]
    public void SuccessfulTablesCompleteWithoutAggregateFailure()
    {
        List<Type> attemptedTypes = new();

        MySqlTableInitializer.InitTableTypes(
            [typeof(FailingTable), typeof(SucceedingTable)],
            attemptedTypes.Add);

        Assert.Equal([typeof(FailingTable), typeof(SucceedingTable)], attemptedTypes);
    }

    private sealed class FailingTable : IInitTables
    {
    }

    private sealed class SucceedingTable : IInitTables
    {
    }
}
