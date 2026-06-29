using NewAGV.Api.Services;
using NewAGV.Api.Tests.TestSupport;
using NewAGV.Contracts;

namespace NewAGV.Api.Tests;

public sealed class WorkflowValidationServiceTests
{
    [Fact]
    public async Task ValidateWorkflowAsync_PassesForSequentialWorkflowWithAvailableTaskChain()
    {
        await using var dbContext = DbContextFactory.Create();
        var taskChainCatalogService = new TaskChainCatalogService(dbContext);
        await taskChainCatalogService.SyncAsync(
        [
            new SeerTaskChainSummary("TC-READY", DateTimeOffset.UtcNow, TaskChainStatus.Completed)
        ], CancellationToken.None);

        var definitionService = new WorkflowDefinitionService(dbContext);
        var workflow = await definitionService.CreateWorkflowAsync(
            WorkflowTestData.CreateWorkflow("WF-READY", WorkflowTestData.CreateStep(1, "TC-READY")),
            CancellationToken.None);

        var validationService = new WorkflowValidationService(
            definitionService,
            taskChainCatalogService,
            PlantStoreFactory.Create());

        var result = await validationService.ValidateWorkflowAsync(workflow.Id, CancellationToken.None);

        Assert.True(result.IsValid);
        Assert.True(result.CanPublish);
        Assert.True(result.CanExecute);
        Assert.Empty(result.Issues);
    }

    [Fact]
    public async Task ValidateWorkflowAsync_BlocksMissingTaskChain()
    {
        await using var dbContext = DbContextFactory.Create();
        var taskChainCatalogService = new TaskChainCatalogService(dbContext);
        var definitionService = new WorkflowDefinitionService(dbContext);
        var workflow = await definitionService.CreateWorkflowAsync(
            WorkflowTestData.CreateWorkflow("WF-MISSING", WorkflowTestData.CreateStep(1, "TC-MISSING")),
            CancellationToken.None);

        var validationService = new WorkflowValidationService(
            definitionService,
            taskChainCatalogService,
            PlantStoreFactory.Create());

        var result = await validationService.ValidateWorkflowAsync(workflow.Id, CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, issue => issue.Code == "workflow.step_taskchain_missing");
    }

    [Fact]
    public async Task ValidateWorkflowAsync_BlocksStaleTaskChain()
    {
        await using var dbContext = DbContextFactory.Create();
        dbContext.TaskChainSnapshots.Add(new NewAGV.Persistence.TaskChainSnapshotEntity
        {
            Name = "TC-STALE",
            Availability = "Available",
            SourceState = "Synced",
            LastSyncedAt = DateTimeOffset.UtcNow.AddMinutes(-16)
        });
        await dbContext.SaveChangesAsync(CancellationToken.None);

        var taskChainCatalogService = new TaskChainCatalogService(dbContext);
        var definitionService = new WorkflowDefinitionService(dbContext);
        var workflow = await definitionService.CreateWorkflowAsync(
            WorkflowTestData.CreateWorkflow("WF-STALE", WorkflowTestData.CreateStep(1, "TC-STALE")),
            CancellationToken.None);

        var validationService = new WorkflowValidationService(
            definitionService,
            taskChainCatalogService,
            PlantStoreFactory.Create());

        var result = await validationService.ValidateWorkflowAsync(workflow.Id, CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, issue => issue.Code == "workflow.step_taskchain_stale");
    }

    [Fact]
    public async Task ValidateWorkflowAsync_BlocksInvalidStepSequence()
    {
        await using var dbContext = DbContextFactory.Create();
        var taskChainCatalogService = new TaskChainCatalogService(dbContext);
        await taskChainCatalogService.SyncAsync(
        [
            new SeerTaskChainSummary("TC-SEQ", DateTimeOffset.UtcNow, TaskChainStatus.Completed)
        ], CancellationToken.None);

        var definitionService = new WorkflowDefinitionService(dbContext);
        var validationService = new WorkflowValidationService(
            definitionService,
            taskChainCatalogService,
            PlantStoreFactory.Create());

        var result = await validationService.ValidateWorkflowAsync(new WorkflowDetailDto
        {
            Name = "WF-BAD-SEQUENCE",
            ExecutionMode = "Sequential",
            Steps =
            [
                WorkflowTestData.CreateStep(2, "TC-SEQ")
            ]
        }, CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, issue => issue.Code == "workflow.step_sequence_invalid");
    }
}
