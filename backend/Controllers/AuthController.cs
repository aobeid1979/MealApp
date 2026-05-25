using MealApp.DTOs;
using MealApp.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace MealApp.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ApiControllerBase
{
    // Email verification is disabled temporarily until SMTP/email provider is configured.
    private static readonly bool EmailVerificationEnabled = false;

    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        UserManager<ApplicationUser> userManager,
        IConfiguration configuration,
        IWebHostEnvironment environment,
        ILogger<AuthController> logger)
    {
        _userManager = userManager;
        _configuration = configuration;
        _environment = environment;
        _logger = logger;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterDto registerDto)
    {
        var email = registerDto.Email.Trim();
        var firstName = registerDto.FirstName.Trim();
        var lastName = registerDto.LastName.Trim();

        var existingUser = await _userManager.FindByEmailAsync(email);

        if (existingUser is not null)
        {
            return BadRequest(new
            {
                Message = "Email is already registered."
            });
        }

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FirstName = firstName,
            LastName = lastName,
            Name = $"{firstName} {lastName}".Trim(),
            EmailConfirmed = !EmailVerificationEnabled
        };

        var result = await _userManager.CreateAsync(user, registerDto.Password);

        if (!result.Succeeded)
        {
            return BadRequest(new
            {
                Message = "Registration failed.",
                Errors = result.Errors.Select(error => error.Description)
            });
        }

        var roleResult = await _userManager.AddToRoleAsync(user, "User");

        if (!roleResult.Succeeded)
        {
            return BadRequest(new
            {
                Message = "User was created, but role assignment failed.",
                Errors = roleResult.Errors.Select(error => error.Description)
            });
        }

        if (EmailVerificationEnabled)
        {
            var confirmationToken = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            var frontendBaseUrl = _configuration["Frontend:BaseUrl"] ?? "http://localhost:3000";
            var confirmationLink = QueryHelpers.AddQueryString(
                $"{frontendBaseUrl.TrimEnd('/')}/confirm-email",
                new Dictionary<string, string?>
                {
                    ["userId"] = user.Id,
                    ["token"] = confirmationToken
                });

            if (_environment.IsDevelopment())
            {
                _logger.LogInformation("Development email confirmation link: {ConfirmationLink}", confirmationLink);
                _logger.LogInformation("Development email confirmation token: {ConfirmationToken}", confirmationToken);
            }
        }

        return Ok(new
        {
            Message = "Registration successful. You can now log in.",
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Name = user.Name,
            Role = "User"
        });
    }

    [HttpGet("confirm-email")]
    public async Task<IActionResult> ConfirmEmail(string userId, string token)
    {
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(token))
        {
            return BadRequest(new
            {
                Message = "Invalid email confirmation request."
            });
        }

        var user = await _userManager.FindByIdAsync(userId);

        if (user is null)
        {
            return BadRequest(new
            {
                Message = "Invalid email confirmation request."
            });
        }

        var result = await _userManager.ConfirmEmailAsync(user, token);

        if (!result.Succeeded)
        {
            return BadRequest(new
            {
                Message = "Email confirmation failed.",
                Errors = result.Errors.Select(error => error.Description)
            });
        }

        return Ok(new
        {
            Message = "Email confirmed successfully."
        });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginDto loginDto)
    {
        var user = await _userManager.FindByEmailAsync(loginDto.Email);

        if (user is null)
        {
            return BadRequest(new
            {
                Message = "Invalid email or password."
            });
        }

        if (EmailVerificationEnabled && !await _userManager.IsEmailConfirmedAsync(user))
        {
            return BadRequest(new
            {
                Message = "Please verify your email before logging in."
            });
        }

        var passwordIsValid = await _userManager.CheckPasswordAsync(user, loginDto.Password);

        if (!passwordIsValid)
        {
            return BadRequest(new
            {
                Message = "Invalid email or password."
            });
        }

        var roles = await _userManager.GetRolesAsync(user);
        var token = CreateJwtToken(user, roles);

        return Ok(new
        {
            Message = "Login successful.",
            Email = user.Email,
            Name = user.Name,
            Role = roles.FirstOrDefault(),
            Token = token
        });
    }

    private string CreateJwtToken(ApplicationUser user, IEnumerable<string> roles)
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id),
            new Claim(ClaimTypes.Email, user.Email ?? string.Empty),
            new Claim(ClaimTypes.Name, user.Name)
        };

        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_configuration["Jwt:Secret"]!));

        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"],
            audience: _configuration["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(30),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
