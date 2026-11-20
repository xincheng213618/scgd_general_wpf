namespace ProjectARVRPro.Tests;

[Collection(ProcessManagerPersistenceTestGroup.Name)]
public sealed class ArvrSqliteCleanupProviderTests
{
    [Fact]
    public void FullCleanupDoesNotGiveDeletedHistoryIdsToNewResults()
    {
        using var database = new ResultJsonPayloadStorageTests.TemporaryPayloadDatabase();
        var store = new ResultStatisticsDataStore(database.Path);
        store.InitializeSchema();
        const string oldJson = "{\"reading\":1}";
        const string newJson = "{\"reading\":2}";
        var selectedResult = new ProjectARVRReuslt { SN = "first measurement" };
        int selectedObjectiveId;
        using (var db = database.CreateClient())
        {
            selectedResult.Id = db.Insertable(selectedResult).ExecuteReturnIdentity();
            selectedObjectiveId = db.Insertable(new ObjectiveTestResultRecord { ResultId = selectedResult.Id }).ExecuteReturnIdentity();
            ResultJsonPayloadStorage.SaveViewResultJson(db, selectedResult.Id, oldJson);
            ResultJsonPayloadStorage.SaveObjectiveTestResultJson(db, selectedObjectiveId, oldJson);
        }

        string originalPath = ViewResultManager.SqliteDbPath;
        try
        {
            ViewResultManager.SqliteDbPath = database.Path;
            new ArvrSqliteCleanupProvider().CleanupAll();
        }
        finally { ViewResultManager.SqliteDbPath = originalPath; }

        var nextResult = new ProjectARVRReuslt { SN = "second measurement" };
        int nextObjectiveId;
        using (var db = database.CreateClient())
        {
            Assert.Equal(0, db.Queryable<ProjectARVRReuslt>().Count());
            Assert.Equal(0, db.Queryable<ObjectiveTestResultRecord>().Count());
            nextResult.Id = db.Insertable(nextResult).ExecuteReturnIdentity();
            nextObjectiveId = db.Insertable(new ObjectiveTestResultRecord { ResultId = nextResult.Id }).ExecuteReturnIdentity();
            ResultJsonPayloadStorage.SaveViewResultJson(db, nextResult.Id, newJson);
            ResultJsonPayloadStorage.SaveObjectiveTestResultJson(db, nextObjectiveId, newJson);
        }

        Assert.True(nextResult.Id > selectedResult.Id);
        Assert.True(nextObjectiveId > selectedObjectiveId);
        Assert.Equal(string.Empty, store.LoadViewResultJson(selectedResult));
        Assert.Null(store.GetRecord(selectedObjectiveId));
        Assert.Equal(newJson, store.LoadViewResultJson(nextResult));
        Assert.Equal(newJson, store.GetRecord(nextObjectiveId)?.ObjectiveTestResultJson);

        using var manager = new ViewResultManager(database.Path, new ViewResultManagerConfig());
        selectedResult.ViewResultJson = oldJson;
        var timing = new ResultSqliteWriteTiming();
        manager.Save(selectedResult, timing);
        Assert.False(timing.Succeeded);
        Assert.Null(timing.PayloadMs);
        Assert.Null(timing.PublicationMs);
        Assert.Equal(nextResult.Id, Assert.Single(manager.ViewResluts).Id);

        var delayedImage = new ResultImageExportPathUpdate(true, "old-result.png", false, null);
        var failure = Assert.Throws<InvalidOperationException>(() => manager.UpdateSavedImagePaths(selectedResult, delayedImage));
        Assert.Contains("affectedRows=0", failure.Message);
        Assert.Null(selectedResult.SavedResultImageFileName);
        using var verification = database.CreateClient();
        Assert.Null(Assert.Single(verification.Queryable<ProjectARVRReuslt>().ToList()).SavedResultImageFileName);
        Assert.Equal(newJson, ResultJsonPayloadStorage.LoadViewResultJson(verification, nextResult.Id));
    }
}
