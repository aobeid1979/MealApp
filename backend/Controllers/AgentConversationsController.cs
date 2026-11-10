using MealApp.DTOs.Agent;
using MealApp.Models;
using MealApp.Services.Agent;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace MealApp.Controllers;

[ApiController, Authorize(Roles = "User,Admin"), Route("api/agent/conversations")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class AgentConversationsController(
    AgentConversationService conversations,
    ILogger<AgentConversationsController> logger) : ApiControllerBase
{
    [HttpGet]
    public Task<IActionResult> List([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => Execute(() => conversations.ListAsync(page, pageSize, ct));
    [HttpPost]
    public Task<IActionResult> Create([FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] CreateAgentConversationRequest? request, CancellationToken ct)
        => Execute(() => conversations.CreateAsync(ct));
    [HttpGet("{id:guid}")]
    public Task<IActionResult> Get(Guid id, [FromQuery] long? beforeSequence, [FromQuery] int pageSize = 50, CancellationToken ct = default)
        => Execute(() => conversations.GetAsync(id, beforeSequence, pageSize, ct));
    [HttpDelete("{id:guid}")]
    public Task<IActionResult> Delete(Guid id, CancellationToken ct) => Execute(() => conversations.DeleteAsync(id, ct));

    private async Task<IActionResult> Execute(Func<Task<ConversationResult>> operation)
    {
        try { var result = await operation(); return StatusCode(result.StatusCode, new ApiResponse {
            IsSuccess = result.StatusCode == 200, StatusCode = result.StatusCode, Result = result.Data, ErrorMessages = result.Errors.ToList() }); }
        catch (OperationCanceledException) { throw; }
        catch (Exception exception)
        {
            // Log the real cause (for example a missing database table) so it shows in the backend console.
            logger.LogError(exception, "Agent conversation request failed.");
            return StatusCode(503, new ApiResponse { IsSuccess = false, StatusCode = 503,
                ErrorMessages = ["Unable to access conversations. Please try again."] });
        }
    }
}
