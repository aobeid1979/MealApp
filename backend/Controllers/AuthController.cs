using MealApp.DTOs;
using MealApp.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace MealApp.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ApiControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IConfiguration _configuration;

    public AuthController(UserManager<ApplicationUser> userManager, IConfiguration configuration)
    {
        _userManager = userManager;
        _configuration = configuration;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterDto registerDto)
    {
        var selectedRole = string.IsNullOrWhiteSpace(registerDto.Role)
            ? "User"
            : registerDto.Role;

        if (!string.Equals(selectedRole, "Admin", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(selectedRole, "User", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new
            {
                Message = "Invalid role. Allowed roles are Admin and User."
            });
        }

        selectedRole = string.Equals(selectedRole, "Admin", StringComparison.OrdinalIgnoreCase)
            ? "Admin"
            : "User";

        var user = new ApplicationUser
        {
            UserName = registerDto.Email,
            Email = registerDto.Email,
            Name = registerDto.Name
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

        var roleResult = await _userManager.AddToRoleAsync(user, selectedRole);

        if (!roleResult.Succeeded)
        {
            return BadRequest(new
            {
                Message = "User was created, but role assignment failed.",
                Errors = roleResult.Errors.Select(error => error.Description)
            });
        }

        return Ok(new
        {
            Message = "User registered successfully.",
            Email = user.Email,
            Role = selectedRole
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
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
