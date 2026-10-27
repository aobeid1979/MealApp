using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MealApp.Controllers;
using MealApp.Middleware;

namespace MealApp.Tests;

public class AuthLockoutAndRateLimitTests
{
    private const string Password = "Pass123$";

    [Fact]
    public async Task Login_AfterFiveFailedAttempts_LocksAccountEvenForCorrectPassword()
    {
        await using var factory = new MealAppApiFactory();
        var client = factory.CreateClient();
        var email = await RegisterAsync(client);

        for (var attempt = 0; attempt < 4; attempt++)
        {
            var failed = await LoginAsync(client, email, "WrongPass123$");
            Assert.Equal(HttpStatusCode.Unauthorized, failed.StatusCode);
        }

        // The fifth failure triggers the lockout.
        var fifth = await LoginAsync(client, email, "WrongPass123$");
        await AssertErrorAsync(fifth, HttpStatusCode.TooManyRequests, AuthController.LockedOutMessage);

        var correct = await LoginAsync(client, email, Password);
        await AssertErrorAsync(correct, HttpStatusCode.TooManyRequests, AuthController.LockedOutMessage);
    }

    [Fact]
    public async Task Login_Success_ResetsFailedAttemptCount()
    {
        await using var factory = new MealAppApiFactory();
        var client = factory.CreateClient();
        var email = await RegisterAsync(client);

        for (var attempt = 0; attempt < 4; attempt++)
        {
            await LoginAsync(client, email, "WrongPass123$");
        }

        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(client, email, Password)).StatusCode);

        for (var attempt = 0; attempt < 4; attempt++)
        {
            var failed = await LoginAsync(client, email, "WrongPass123$");
            Assert.Equal(HttpStatusCode.Unauthorized, failed.StatusCode);
        }

        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(client, email, Password)).StatusCode);
    }

    [Fact]
    public async Task Login_WithUnknownEmail_IsNotReportedAsLockedOut()
    {
        await using var factory = new MealAppApiFactory();
        var client = factory.CreateClient();

        for (var attempt = 0; attempt < 6; attempt++)
        {
            var response = await LoginAsync(client, "nobody@example.com", "WrongPass123$");
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }

    [Fact]
    public async Task AuthEndpoints_OverRateLimit_Return429ApiResponse()
    {
        await using var factory = new MealAppApiFactory(authRateLimit: 3);
        var client = factory.CreateClient();

        for (var request = 0; request < 3; request++)
        {
            var allowed = await LoginAsync(client, "nobody@example.com", "WrongPass123$");
            Assert.Equal(HttpStatusCode.Unauthorized, allowed.StatusCode);
        }

        var limited = await client.PostAsJsonAsync("/api/auth/register", new
        {
            firstName = "Test",
            lastName = "User",
            email = "limited@example.com",
            password = Password
        });

        Assert.True(limited.Headers.Contains("Retry-After"));
        await AssertErrorAsync(limited, HttpStatusCode.TooManyRequests, AuthRateLimiting.RejectedMessage);
    }

    [Fact]
    public async Task RateLimit_DoesNotApplyToNonAuthEndpoints()
    {
        await using var factory = new MealAppApiFactory(authRateLimit: 1);
        var client = factory.CreateClient();

        for (var request = 0; request < 5; request++)
        {
            var response = await client.GetAsync("/api/recipe");
            Assert.NotEqual(HttpStatusCode.TooManyRequests, response.StatusCode);
        }
    }

    private static async Task<string> RegisterAsync(HttpClient client)
    {
        var email = $"lockout-{Guid.NewGuid():N}@example.com";
        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            firstName = "Test",
            lastName = "User",
            email,
            password = Password
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return email;
    }

    private static Task<HttpResponseMessage> LoginAsync(HttpClient client, string email, string password)
    {
        return client.PostAsJsonAsync("/api/auth/login", new { email, password });
    }

    private static async Task AssertErrorAsync(HttpResponseMessage response, HttpStatusCode expectedStatus, string expectedMessage)
    {
        using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var root = json.RootElement;

        Assert.Equal(expectedStatus, response.StatusCode);
        Assert.False(root.GetProperty("isSuccess").GetBoolean());
        Assert.Equal((int)expectedStatus, root.GetProperty("statusCode").GetInt32());
        Assert.Equal(expectedMessage, root.GetProperty("errorMessages")[0].GetString());
    }
}
