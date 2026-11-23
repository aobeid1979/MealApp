using MealApp.DTOs;
using MealApp.Models;
using MealApp.Services.Email;
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
    // Identity's built-in email token provider creates short-lived 6-digit codes.
    private static readonly string VerificationCodeProvider = Microsoft.AspNetCore.Identity.TokenOptions.DefaultEmailProvider;
    private const string VerificationCodePurpose = "VerifyEmail";
    private const string InvalidCodeMessage = "The code is invalid or has expired. Request a new code and try again.";
    private const string ResendCodeMessage = "If an unverified account exists for this email, a new code has been sent.";
    private const int MinimumAge = 18;
    private const int MaximumAge = 120;

    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IConfiguration _configuration;
    private readonly IVerificationCodeSender _verificationCodeSender;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        UserManager<ApplicationUser> userManager,
        IConfiguration configuration,
        IVerificationCodeSender verificationCodeSender,
        ILogger<AuthController> logger)
    {
        _userManager = userManager;
        _configuration = configuration;
        _verificationCodeSender = verificationCodeSender;
        _logger = logger;
    }

    // Turned on with "EmailVerification:Enabled" (on in Development, off by default elsewhere).
    private bool EmailVerificationEnabled => _configuration.GetValue<bool>("EmailVerification:Enabled");

    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterDto registerDto)
    {
        var email = registerDto.Email.Trim();
        var firstName = registerDto.FirstName.Trim();
        var lastName = registerDto.LastName.Trim();

        var ageError = ValidateDateOfBirth(registerDto.DateOfBirth!.Value);

        if (ageError is not null)
        {
            return BadRequest(new
            {
                Message = ageError
            });
        }

        var existingUser = await _userManager.FindByEmailAsync(email);

        if (existingUser is not null)
        {
            return BadRequest(new
            {
                Message = "Email is already registered."
            });
        }

        var requiresVerification = EmailVerificationEnabled;

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FirstName = firstName,
            LastName = lastName,
            Name = $"{firstName} {lastName}".Trim(),
            EmailConfirmed = !requiresVerification
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

        if (requiresVerification)
        {
            await SendVerificationCodeAsync(user);
        }

        return Ok(new
        {
            Message = requiresVerification
                ? "Registration successful. We sent a 6-digit code to your email. Enter it to activate your account."
                : "Registration successful. You can now log in.",
            RequiresEmailVerification = requiresVerification,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Name = user.Name,
            Role = "User"
        });
    }

    [HttpPost("verify-email")]
    public async Task<IActionResult> VerifyEmail(VerifyEmailDto verifyEmailDto)
    {
        var user = await _userManager.FindByEmailAsync(verifyEmailDto.Email.Trim());

        // Same answer for unknown emails and wrong codes, so the endpoint does not reveal who has an account.
        if (user is null)
        {
            return BadRequest(new
            {
                Message = InvalidCodeMessage
            });
        }

        if (user.EmailConfirmed)
        {
            return Ok(new
            {
                Message = "Your email is already verified. You can log in."
            });
        }

        if (await _userManager.IsLockedOutAsync(user))
        {
            return StatusCode(StatusCodes.Status429TooManyRequests, new ApiResponse
            {
                IsSuccess = false,
                StatusCode = StatusCodes.Status429TooManyRequests,
                ErrorMessages = ["Too many wrong codes. Wait a few minutes, then request a new code."]
            });
        }

        var codeIsValid = await _userManager.VerifyUserTokenAsync(
            user,
            VerificationCodeProvider,
            VerificationCodePurpose,
            verifyEmailDto.Code.Trim());

        if (!codeIsValid)
        {
            // Counts wrong guesses; Identity locks the account for a few minutes after repeated failures.
            await _userManager.AccessFailedAsync(user);

            return BadRequest(new
            {
                Message = InvalidCodeMessage
            });
        }

        user.EmailConfirmed = true;
        var updateResult = await _userManager.UpdateAsync(user);

        if (!updateResult.Succeeded)
        {
            return BadRequest(new
            {
                Message = "Email verification failed.",
                Errors = updateResult.Errors.Select(error => error.Description)
            });
        }

        await _userManager.ResetAccessFailedCountAsync(user);

        return Ok(new
        {
            Message = "Your email is verified. You can now log in."
        });
    }

    [HttpPost("resend-verification-code")]
    public async Task<IActionResult> ResendVerificationCode(ResendVerificationCodeDto resendDto)
    {
        if (EmailVerificationEnabled)
        {
            var user = await _userManager.FindByEmailAsync(resendDto.Email.Trim());

            if (user is not null && !user.EmailConfirmed)
            {
                await SendVerificationCodeAsync(user);
            }
        }

        // Always the same answer, so the endpoint does not reveal who has an account.
        return Ok(new
        {
            Message = ResendCodeMessage
        });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginDto loginDto)
    {
        var email = loginDto.Email.Trim();
        var user = await _userManager.FindByEmailAsync(email);

        if (user is null)
        {
            return Unauthorized(new
            {
                Message = "Invalid email or password."
            });
        }

        var passwordIsValid = await _userManager.CheckPasswordAsync(user, loginDto.Password);

        if (!passwordIsValid)
        {
            return Unauthorized(new
            {
                Message = "Invalid email or password."
            });
        }

        // Checked after the password, so only the account owner learns the account is unverified.
        if (EmailVerificationEnabled && !await _userManager.IsEmailConfirmedAsync(user))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ApiResponse
            {
                IsSuccess = false,
                StatusCode = StatusCodes.Status403Forbidden,
                ErrorMessages = ["Please verify your email before logging in."]
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

    private string? ValidateDateOfBirth(DateOnly dateOfBirth)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        if (dateOfBirth > today)
        {
            return "Date of birth cannot be in the future.";
        }

        var age = CalculateAge(dateOfBirth, today);

        if (age > MaximumAge)
        {
            return "Please enter a valid date of birth.";
        }

        if (age < MinimumAge)
        {
            return $"You must be at least {MinimumAge} years old to register.";
        }

        return null;
    }

    private static int CalculateAge(DateOnly dateOfBirth, DateOnly today)
    {
        var age = today.Year - dateOfBirth.Year;

        if (dateOfBirth > today.AddYears(-age))
        {
            age--;
        }

        return age;
    }

    private async Task SendVerificationCodeAsync(ApplicationUser user)
    {
        var code = await _userManager.GenerateUserTokenAsync(user, VerificationCodeProvider, VerificationCodePurpose);

        try
        {
            await _verificationCodeSender.SendAsync(user.Email!, code, HttpContext.RequestAborted);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // The account still exists; the user can ask for a new code from the verify page.
            _logger.LogError(exception, "Sending the verification code failed.");
        }
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
