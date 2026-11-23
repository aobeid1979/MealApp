using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MealApp.Models;
using MealApp.Services.Email;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace MealApp.Tests;

public class EmailVerificationTests
{
    private const string Password = "Pass123$";

    [Fact]
    public async Task Register_DayBeforeEighteenthBirthday_IsRejected()
    {
        await using var factory = new MealAppApiFactory();
        var client = factory.CreateClient();
        var dateOfBirth = Today().AddYears(-18).AddDays(1);

        var response = await RegisterAsync(client, CreateEmail(), dateOfBirth);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("at least 18 years old", body);
    }

    [Fact]
    public async Task Register_OnEighteenthBirthday_IsAccepted()
    {
        await using var factory = new MealAppApiFactory();
        var client = factory.CreateClient();

        var response = await RegisterAsync(client, CreateEmail(), Today().AddYears(-18));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Register_WithoutDateOfBirth_IsRejected()
    {
        await using var factory = new MealAppApiFactory();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            firstName = "Test",
            lastName = "User",
            email = CreateEmail(),
            password = Password
        });
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Date of birth is required.", body);
    }

    [Fact]
    public async Task Register_WithFutureDateOfBirth_IsRejected()
    {
        await using var factory = new MealAppApiFactory();
        var client = factory.CreateClient();

        var response = await RegisterAsync(client, CreateEmail(), Today().AddDays(1));
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("cannot be in the future", body);
    }

    [Fact]
    public async Task VerificationEnabled_UserMustEnterCodeBeforeLogin()
    {
        var sender = new CapturingVerificationCodeSender();
        await using var baseFactory = new MealAppApiFactory();
        await using var factory = WithVerificationEnabled(baseFactory, sender);
        var client = factory.CreateClient();
        var email = CreateEmail();

        var registerResponse = await RegisterAsync(client, email, Today().AddYears(-30));
        Assert.Equal(HttpStatusCode.OK, registerResponse.StatusCode);

        using (var json = JsonDocument.Parse(await registerResponse.Content.ReadAsStringAsync()))
        {
            Assert.True(json.RootElement.GetProperty("result").GetProperty("requiresEmailVerification").GetBoolean());
        }

        Assert.True(sender.Codes.TryGetValue(email, out var code));
        Assert.Matches(@"^\d{6}$", code);

        using (var scope = factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.FindByEmailAsync(email);
            Assert.NotNull(user);
            Assert.False(user.EmailConfirmed);
        }

        var blockedLogin = await LoginAsync(client, email);
        Assert.Equal(HttpStatusCode.Forbidden, blockedLogin.StatusCode);
        Assert.Contains("verify your email", await blockedLogin.Content.ReadAsStringAsync());

        var wrongCode = code == "000000" ? "111111" : "000000";
        var wrongResponse = await client.PostAsJsonAsync("/api/auth/verify-email", new { email, code = wrongCode });
        Assert.Equal(HttpStatusCode.BadRequest, wrongResponse.StatusCode);

        var verifyResponse = await client.PostAsJsonAsync("/api/auth/verify-email", new { email, code });
        Assert.Equal(HttpStatusCode.OK, verifyResponse.StatusCode);

        var login = await LoginAsync(client, email);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    [Fact]
    public async Task VerificationEnabled_WrongPasswordDoesNotRevealUnverifiedAccount()
    {
        var sender = new CapturingVerificationCodeSender();
        await using var baseFactory = new MealAppApiFactory();
        await using var factory = WithVerificationEnabled(baseFactory, sender);
        var client = factory.CreateClient();
        var email = CreateEmail();

        await RegisterAsync(client, email, Today().AddYears(-30));

        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "WrongPass123$" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ResendCode_SendsCodeOnlyToUnverifiedAccounts_WithSameAnswerForUnknownEmails()
    {
        var sender = new CapturingVerificationCodeSender();
        await using var baseFactory = new MealAppApiFactory();
        await using var factory = WithVerificationEnabled(baseFactory, sender);
        var client = factory.CreateClient();
        var email = CreateEmail();

        await RegisterAsync(client, email, Today().AddYears(-30));
        sender.Codes.Clear();

        var known = await client.PostAsJsonAsync("/api/auth/resend-verification-code", new { email });
        var unknown = await client.PostAsJsonAsync("/api/auth/resend-verification-code", new { email = CreateEmail() });

        Assert.Equal(HttpStatusCode.OK, known.StatusCode);
        Assert.Equal(HttpStatusCode.OK, unknown.StatusCode);
        Assert.Equal(await known.Content.ReadAsStringAsync(), await unknown.Content.ReadAsStringAsync());
        Assert.True(sender.Codes.ContainsKey(email));
        Assert.Single(sender.Codes);
    }

    [Fact]
    public async Task VerifyEmail_UnknownEmail_GetsSameAnswerAsWrongCode()
    {
        await using var factory = new MealAppApiFactory();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/verify-email", new { email = CreateEmail(), code = "123456" });
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("invalid or has expired", body);
    }

    private static WebApplicationFactory<Program> WithVerificationEnabled(
        MealAppApiFactory factory,
        CapturingVerificationCodeSender sender) =>
        factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["EmailVerification:Enabled"] = "true"
                }));

            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IVerificationCodeSender>();
                services.AddSingleton<IVerificationCodeSender>(sender);
            });
        });

    private static Task<HttpResponseMessage> RegisterAsync(HttpClient client, string email, DateOnly dateOfBirth) =>
        client.PostAsJsonAsync("/api/auth/register", new
        {
            firstName = "Test",
            lastName = "User",
            email,
            password = Password,
            dateOfBirth = dateOfBirth.ToString("yyyy-MM-dd")
        });

    private static Task<HttpResponseMessage> LoginAsync(HttpClient client, string email) =>
        client.PostAsJsonAsync("/api/auth/login", new { email, password = Password });

    private static DateOnly Today() => DateOnly.FromDateTime(DateTime.UtcNow);

    private static string CreateEmail() => $"verify-{Guid.NewGuid():N}@example.com";

    private sealed class CapturingVerificationCodeSender : IVerificationCodeSender
    {
        public ConcurrentDictionary<string, string> Codes { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Task SendAsync(string email, string code, CancellationToken cancellationToken = default)
        {
            Codes[email] = code;
            return Task.CompletedTask;
        }
    }
}
