using ColorVision.Database;
using ColorVision.Engine.Archive.Dao;

namespace ColorVision.UI.Tests;

public class ArchiveConfigurationSchemaMigrationTests
{
    public static IEnumerable<object[]> PartialColumnStates()
    {
        yield return new object[] { new[] { ExcludingImages }, new[] { DeleteLocalFile, DataSaveHours } };
        yield return new object[] { new[] { DeleteLocalFile }, new[] { ExcludingImages, DataSaveHours } };
        yield return new object[] { new[] { DataSaveHours }, new[] { ExcludingImages, DeleteLocalFile } };
        yield return new object[] { new[] { ExcludingImages, DeleteLocalFile }, new[] { DataSaveHours } };
        yield return new object[] { new[] { ExcludingImages, DataSaveHours }, new[] { DeleteLocalFile } };
        yield return new object[] { new[] { DeleteLocalFile, DataSaveHours }, new[] { ExcludingImages } };
    }

    [Fact]
    public void NoColumnsPresentBuildsOneAlterWithAllThreeFixedClauses()
    {
        string? executedSql = null;
        bool editorOpened = false;

        int count = ArchiveConfigurationSchemaMigration.EnsureColumnsAndExecute(
            () => Columns(),
            sql =>
            {
                executedSql = sql;
                return 0;
            },
            () => { editorOpened = true; });

        Assert.Equal(0, count);
        Assert.NotNull(executedSql);
        AssertSingleAlter(executedSql, expectedAddClauseCount: 3);
        Assert.Contains($"`{ExcludingImages}`", executedSql, StringComparison.Ordinal);
        Assert.Contains($"`{DeleteLocalFile}`", executedSql, StringComparison.Ordinal);
        Assert.Contains($"`{DataSaveHours}`", executedSql, StringComparison.Ordinal);
        Assert.True(editorOpened);
    }

    [Theory]
    [MemberData(nameof(PartialColumnStates))]
    public void EveryPartialStateAddsOnlyTheMissingColumns(string[] presentColumns, string[] missingColumns)
    {
        string? executedSql = null;
        bool editorOpened = false;

        ArchiveConfigurationSchemaMigration.EnsureColumnsAndExecute(
            () => Columns(presentColumns),
            sql =>
            {
                executedSql = sql;
                return 0;
            },
            () => { editorOpened = true; });

        Assert.NotNull(executedSql);
        AssertSingleAlter(executedSql, missingColumns.Length);
        foreach (string column in missingColumns)
            Assert.Contains($"`{column}`", executedSql, StringComparison.Ordinal);
        foreach (string column in presentColumns)
            Assert.DoesNotContain($"`{column}`", executedSql, StringComparison.Ordinal);
        Assert.True(editorOpened);
    }

    [Fact]
    public void AllColumnsPresentOnRerunSkipsAlterAndOpensEditor()
    {
        bool executorCalled = false;
        bool editorOpened = false;

        int count = ArchiveConfigurationSchemaMigration.EnsureColumnsAndExecute(
            AllColumns,
            _ =>
            {
                executorCalled = true;
                return 0;
            },
            () => { editorOpened = true; });

        Assert.Equal(0, count);
        Assert.False(executorCalled);
        Assert.True(editorOpened);
    }

    [Fact]
    public void DuplicateRaceIsSuccessOnlyWhenReinspectionFindsAllColumns()
    {
        var inspections = new Queue<IReadOnlySet<string>>([Columns(), AllColumns()]);
        bool editorOpened = false;

        int count = ArchiveConfigurationSchemaMigration.EnsureColumnsAndExecute(
            () => inspections.Dequeue(),
            _ => throw CreateDuplicateColumnFailure(),
            () => { editorOpened = true; });

        Assert.Equal(0, count);
        Assert.Empty(inspections);
        Assert.True(editorOpened);
    }

    [Fact]
    public void DuplicateRaceStillMissingAColumnPreservesTypedFailureAndDoesNotOpenEditor()
    {
        var inspections = new Queue<IReadOnlySet<string>>([Columns(), Columns(ExcludingImages, DeleteLocalFile)]);
        BatchExecuteNonQueryException failure = CreateDuplicateColumnFailure();
        bool editorOpened = false;

        BatchExecuteNonQueryException thrown = Assert.Throws<BatchExecuteNonQueryException>(() =>
            ArchiveConfigurationSchemaMigration.EnsureColumnsAndExecute(
                () => inspections.Dequeue(),
                _ => throw failure,
                () => { editorOpened = true; }));

        Assert.Same(failure, thrown);
        Assert.Empty(inspections);
        Assert.False(editorOpened);
        Assert.DoesNotContain("duplicate provider detail", thrown.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void InspectionFailureIsSanitizedAndDoesNotExecuteOrOpenEditor()
    {
        const string secret = "information_schema provider secret";
        bool executorCalled = false;
        bool editorOpened = false;

        ArchiveSchemaMigrationException exception = Assert.Throws<ArchiveSchemaMigrationException>(() =>
            ArchiveConfigurationSchemaMigration.EnsureColumnsAndExecute(
                () => throw new InvalidOperationException(secret),
                _ =>
                {
                    executorCalled = true;
                    return 0;
                },
                () => { editorOpened = true; }));

        Assert.Equal(ArchiveSchemaMigrationStage.InspectColumns, exception.Stage);
        Assert.Null(exception.InnerException);
        Assert.DoesNotContain(secret, exception.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(secret, exception.GetDiagnosticSummary(), StringComparison.Ordinal);
        Assert.False(executorCalled);
        Assert.False(editorOpened);
    }

    private static IReadOnlySet<string> AllColumns()
    {
        return Columns(ExcludingImages, DeleteLocalFile, DataSaveHours);
    }

    private static IReadOnlySet<string> Columns(params string[] names)
    {
        return new HashSet<string>(names, StringComparer.OrdinalIgnoreCase);
    }

    private static BatchExecuteNonQueryException CreateDuplicateColumnFailure()
    {
        return new BatchExecuteNonQueryException(
            BatchExecutionStage.ExecuteStatement,
            1,
            new InvalidOperationException("duplicate provider detail"),
            null);
    }

    private static void AssertSingleAlter(string sql, int expectedAddClauseCount)
    {
        Assert.StartsWith($"ALTER TABLE `{ArchiveConfigurationSchemaMigration.TableName}`", sql, StringComparison.Ordinal);
        Assert.Equal(1, sql.Split("ALTER TABLE", StringSplitOptions.None).Length - 1);
        Assert.Equal(expectedAddClauseCount, sql.Split("ADD COLUMN", StringSplitOptions.None).Length - 1);
        Assert.Equal(1, sql.Count(character => character == ';'));
    }

    private const string ExcludingImages = ArchiveConfigurationSchemaMigration.ExcludingImagesColumn;
    private const string DeleteLocalFile = ArchiveConfigurationSchemaMigration.DeleteLocalFileColumn;
    private const string DataSaveHours = ArchiveConfigurationSchemaMigration.DataSaveHoursColumn;
}
