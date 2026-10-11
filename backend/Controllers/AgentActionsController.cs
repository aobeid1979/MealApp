using MealApp.DTOs.AgentActions;
using MealApp.Models;
using MealApp.Services.AgentActions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace MealApp.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/agent-actions")]
public class AgentActionsController(AgentActionService actions) : ApiControllerBase
{
    [HttpGet("pending")]
    public async Task<IActionResult> Pending() => Respond(await actions.ListPendingAsync());

    [HttpPost("{actionId:guid}/approve")]
    public async Task<IActionResult> Approve(Guid actionId,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] ActionDecisionRequest? request, CancellationToken ct)
        => Respond(await actions.DecideAsync(actionId, true, ct));

    [HttpPost("{actionId:guid}/reject")]
    public async Task<IActionResult> Reject(Guid actionId,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] ActionDecisionRequest? request, CancellationToken ct)
        => Respond(await actions.DecideAsync(actionId, false, ct));

    private ObjectResult Respond(ActionOperationResult result) => StatusCode(result.StatusCode, new ApiResponse
    {
        IsSuccess = result.StatusCode == 200, StatusCode = result.StatusCode, Result = result.Data, ErrorMessages = result.Errors.ToList()
    });
}
