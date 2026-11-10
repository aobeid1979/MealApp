using MealApp.DTOs.Agent;
using MealApp.Models;
using MealApp.Services.Agent;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MealApp.Controllers;

[ApiController]
[Authorize(Roles = "User,Admin")]
[Route("api/agent")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[RequestSizeLimit(16 * 1024)]
public class AgentController(AgentChatService agent, AgentConversationService conversations) : ApiControllerBase
{
    [HttpPost("chat")]
    public async Task<IActionResult> Chat(AgentChatRequest request, CancellationToken ct)
    {
        if (request.ConversationId.HasValue || request.RequestId.HasValue)
        {
            try
            {
                var conversation = await conversations.ChatAsync(request, ct);
                return StatusCode(conversation.StatusCode, new ApiResponse { IsSuccess = conversation.StatusCode == 200,
                    StatusCode = conversation.StatusCode, Result = conversation.Data, ErrorMessages = conversation.Errors.ToList() });
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception) { return StatusCode(503, new ApiResponse { IsSuccess = false, StatusCode = 503,
                ErrorMessages = ["Unable to access or save the conversation. Retry with the same requestId or reload."] }); }
        }
        var result = await agent.RunAsync(request.Message, ct);
        return StatusCode(result.StatusCode, new ApiResponse
        {
            IsSuccess = result.StatusCode == 200, StatusCode = result.StatusCode,
            Result = result.Response, ErrorMessages = result.Errors.ToList()
        });
    }
}
