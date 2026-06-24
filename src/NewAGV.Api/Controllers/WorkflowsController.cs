using Microsoft.AspNetCore.Mvc;
using NewAGV.Api.Infrastructure;
using NewAGV.Contracts;
using NewAGV.Api.Services;

namespace NewAGV.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class WorkflowsController(
    WorkflowDefinitionService definitionService,
    WorkflowValidationService validationService,
    WorkflowExecutionService executionService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<WorkflowSummaryDto>>> GetWorkflows(CancellationToken cancellationToken)
        => Ok(await definitionService.GetWorkflowsAsync(cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<WorkflowDetailDto>> GetWorkflow(Guid id, CancellationToken cancellationToken)
    {
        var workflow = await definitionService.GetWorkflowAsync(id, cancellationToken);
        return workflow is null ? NotFound() : Ok(workflow);
    }

    [HttpPost]
    public async Task<ActionResult<WorkflowDetailDto>> CreateWorkflow([FromBody] CreateWorkflowRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var workflow = await definitionService.CreateWorkflowAsync(request, cancellationToken);
            return CreatedAtAction(nameof(GetWorkflow), new { id = workflow.Id }, workflow);
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(exception.Message);
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<WorkflowDetailDto>> UpdateWorkflow(Guid id, [FromBody] UpdateWorkflowRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var workflow = await definitionService.UpdateWorkflowAsync(id, request, cancellationToken);
            return workflow is null ? NotFound() : Ok(workflow);
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(exception.Message);
        }
    }

    [HttpPut("{id:guid}/steps")]
    public async Task<ActionResult<WorkflowDetailDto>> ReplaceSteps(Guid id, [FromBody] ReplaceWorkflowStepsRequest request, CancellationToken cancellationToken)
    {
        var workflow = await definitionService.ReplaceStepsAsync(id, request, cancellationToken);
        return workflow is null ? NotFound() : Ok(workflow);
    }

    [HttpPost("{id:guid}/duplicate")]
    public async Task<ActionResult<WorkflowDetailDto>> DuplicateWorkflow(Guid id, [FromBody] DuplicateWorkflowRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var workflow = await definitionService.DuplicateAsync(id, request, cancellationToken);
            if (workflow is null)
            {
                return NotFound();
            }

            return CreatedAtAction(nameof(GetWorkflow), new { id = workflow.Id }, workflow);
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(exception.Message);
        }
    }

    [HttpPost("{id:guid}/publish")]
    public async Task<ActionResult<WorkflowDetailDto>> PublishWorkflow(Guid id, [FromBody] PublishWorkflowRequest request, CancellationToken cancellationToken)
    {
        var validation = await validationService.ValidateWorkflowAsync(id, cancellationToken);
        if (request.IsPublished && !validation.CanPublish)
        {
            return BadRequest(validation);
        }

        var workflow = await definitionService.SetPublishStateAsync(id, request, cancellationToken);
        return workflow is null ? NotFound() : Ok(workflow);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteWorkflow(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var deleted = await definitionService.DeleteWorkflowAsync(id, cancellationToken);
            return deleted ? NoContent() : NotFound();
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(exception.Message);
        }
    }

    [HttpPost("{id:guid}/validate")]
    public async Task<ActionResult<WorkflowValidationResult>> ValidateWorkflow(Guid id, CancellationToken cancellationToken)
    {
        var result = await validationService.ValidateWorkflowAsync(id, cancellationToken);
        return result.Issues.Any(issue => issue.Code == "workflow.not_found") ? NotFound(result) : Ok(result);
    }

    [HttpPost("{id:guid}/execute")]
    public async Task<ActionResult<WorkflowRunDto>> ExecuteWorkflow(Guid id, [FromBody] ExecuteWorkflowRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await executionService.ExecuteAsync(id, request, Request.ResolveRole(), cancellationToken));
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(exception.Message);
        }
    }

    [HttpGet("active-run")]
    public async Task<ActionResult<WorkflowRunDto?>> GetActiveRun([FromQuery] string? robotId, CancellationToken cancellationToken)
    {
        var run = await executionService.GetActiveRunAsync(robotId, cancellationToken);
        return run is null ? NoContent() : Ok(run);
    }

    [HttpGet("history")]
    public async Task<ActionResult<IReadOnlyList<WorkflowHistoryEntryDto>>> GetHistory([FromQuery] string? robotId, CancellationToken cancellationToken)
        => Ok(await executionService.GetHistoryAsync(robotId, cancellationToken));

    [HttpPost("pause")]
    public async Task<ActionResult<WorkflowRunDto>> Pause([FromBody] WorkflowControlRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await executionService.PauseAsync(request, Request.ResolveRole(), cancellationToken));
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(exception.Message);
        }
    }

    [HttpPost("resume")]
    public async Task<ActionResult<WorkflowRunDto>> Resume([FromBody] WorkflowControlRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await executionService.ResumeAsync(request, Request.ResolveRole(), cancellationToken));
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(exception.Message);
        }
    }

    [HttpPost("cancel")]
    public async Task<ActionResult<WorkflowRunDto>> Cancel([FromBody] WorkflowControlRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await executionService.CancelAsync(request, Request.ResolveRole(), cancellationToken));
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(exception.Message);
        }
    }
}
