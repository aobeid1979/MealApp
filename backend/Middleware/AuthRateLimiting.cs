using System.Threading.RateLimiting;
using MealApp.Models;
using Microsoft.AspNetCore.RateLimiting;

namespace MealApp.Middleware;

public static class AuthRateLimiting
{
    public const string PolicyName = "AuthEndpoints";
    public const string RejectedMessage = "Too many requests. Please wait a moment and try again.";

    // Limits requests to the auth endpoints per client IP address.
    // Configure with RateLimiting:Auth:PermitLimit and RateLimiting:Auth:WindowSeconds.
    public static IServiceCollection AddAuthRateLimiting(this IServiceCollection services)
    {
        return services.AddRateLimiter(options =>
        {
            options.AddPolicy(PolicyName, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ =>
                    {
                        // Read from the built app's configuration so environment and test overrides apply.
                        var configuration = httpContext.RequestServices.GetRequiredService<IConfiguration>();

                        return new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = configuration.GetValue("RateLimiting:Auth:PermitLimit", 10),
                            Window = TimeSpan.FromSeconds(configuration.GetValue("RateLimiting:Auth:WindowSeconds", 60)),
                            QueueLimit = 0
                        };
                    }));

            options.OnRejected = async (context, cancellationToken) =>
            {
                var response = context.HttpContext.Response;
                response.StatusCode = StatusCodes.Status429TooManyRequests;

                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    response.Headers.RetryAfter = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString();
                }

                await response.WriteAsJsonAsync(new ApiResponse
                {
                    IsSuccess = false,
                    StatusCode = StatusCodes.Status429TooManyRequests,
                    ErrorMessages = [RejectedMessage]
                }, cancellationToken);
            };
        });
    }
}
