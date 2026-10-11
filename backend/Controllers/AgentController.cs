using MealApp.DTOs.Agent;
using MealApp.Models;
using MealApp.Services.Agent;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MealApp.Controllers;

[ApiController]
[Authorize(Roles = "User,Admin")]
[Route("api/agent")]
[RequestSizeLimit(16 * 1024)]
public class AgentController(AgentChatService agent) : ApiControllerBase
{
    [HttpPost("chat")]
    public async Task<IActionResult> Chat(AgentChatRequest request, CancellationToken ct)
    {
        var result = await agent.RunAsync(request.Message, ct);
        return StatusCode(result.StatusCode, new ApiResponse
        {
            IsSuccess = result.StatusCode == 200, StatusCode = result.StatusCode,
            Result = result.Response, ErrorMessages = result.Errors.ToList()
        });
    }
}
