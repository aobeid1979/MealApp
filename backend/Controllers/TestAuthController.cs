#if DEBUG
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace MealApp.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TestAuthController : ApiControllerBase
{
    [HttpGet("public")]
    public IActionResult Public()
    {
        return Ok(new
        {
            Message = "This endpoint is public"
        });
    }

    [Authorize]
    [HttpGet("protected")]
    public IActionResult Protected()
    {
        var email = User.FindFirstValue(ClaimTypes.Email);

        return Ok(new
        {
            Message = "You are authenticated",
            Email = email
        });
    }
}
#endif
