using System.Security.Claims;
using MealApp.Services.AgentTools;

namespace MealApp.Services.AgentActions;

public enum ToolPolicyDecision { Allow, RequireApproval, Deny }

public class AgentToolPolicy
{
    public ToolPolicyDecision Evaluate(ToolDefinition tool, ClaimsPrincipal user)
    {
        if (user.Identity?.IsAuthenticated != true || string.IsNullOrWhiteSpace(user.FindFirstValue(ClaimTypes.NameIdentifier)))
            return ToolPolicyDecision.Deny;
        if (tool.Capability == ToolCapability.Read && (user.IsInRole("User") || user.IsInRole("Admin")))
            return ToolPolicyDecision.Allow;
        if (tool.Capability == ToolCapability.Write && tool.Name == "create_recipe_draft" && user.IsInRole("Admin"))
            return ToolPolicyDecision.RequireApproval;
        return ToolPolicyDecision.Deny;
    }
}
