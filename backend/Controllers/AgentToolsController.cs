using MealApp.DTOs.AgentTools;
using MealApp.Models;
using MealApp.Services.AgentTools;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MealApp.Controllers;

[ApiController]
[Authorize(Roles = "User,Admin")]
[Route("api/agent-tools")]
[RequestSizeLimit(16 * 1024)]
public class AgentToolsController(AgentToolExecutor executor) : ApiControllerBase
{
    [HttpPost("execute")]
    public async Task<IActionResult> Execute(ToolExecutionRequest request, CancellationToken ct)
    {
        var result = await executor.ExecuteAsync(request, ct);
        return StatusCode(result.StatusCode, new ApiResponse
        {
            IsSuccess = result.StatusCode == 200,
            StatusCode = result.StatusCode,
            Result = new { result.Tool, result.Code, result.Data },
            ErrorMessages = result.Errors.ToList()
        });
    }
}
