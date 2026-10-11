using System.Net;
using System.Text.Json;
using MealApp.Services;
using MealApp.Services.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace MealApp.Tests;

public class BrevoEmailTests
{
    [Fact]
    public async Task DisabledEmailStartsWithoutCredentials()
    {
        using var host = CreateOptionsHost(new BrevoEmailOptions());
        await host.StartAsync();
        Assert.False(host.Services.GetRequiredService<IOptions<BrevoEmailOptions>>().Value.Enabled);
        await host.StopAsync();
    }

    [Theory]
    [InlineData("ApiKey")]
    [InlineData("SenderEmail")]
    [InlineData("SenderName")]
    [InlineData("ToEmail")]
    public async Task EnabledEmailRejectsEachMissingSettingAtStartup(string property)
    {
        var options = ValidOptions();
        typeof(BrevoEmailOptions).GetProperty(property)!.SetValue(options, "");
        using var host = CreateOptionsHost(options);
        var exception = await Assert.ThrowsAsync<OptionsValidationException>(() => host.StartAsync());
        Assert.Contains($"BrevoEmail__{property}", exception.Message);
        Assert.DoesNotContain(options.ApiKey == "" ? "not-a-real-key" : options.ApiKey, exception.Message);
    }

    [Theory]
    [InlineData("SenderEmail")]
    [InlineData("ToEmail")]
    public async Task EnabledEmailRejectsInvalidAddressesAtStartup(string property)
    {
        var options = ValidOptions();
        typeof(BrevoEmailOptions).GetProperty(property)!.SetValue(options, "invalid");
        using var host = CreateOptionsHost(options);
        await Assert.ThrowsAsync<OptionsValidationException>(() => host.StartAsync());
    }

    [Fact]
    public async Task EnabledEmailStartsWithValidSettings()
    {
        using var host = CreateOptionsHost(ValidOptions());
        await host.StartAsync();
        await host.StopAsync();
    }

    [Fact]
    public async Task DisabledSupportReturns503WithoutCallingBrevoAndStillValidatesGuests()
    {
        using var factory = new SupportFactory(false);
        using var client = factory.CreateClient();
        using var validForm = CreateForm(true);
        using var response = await client.PostAsync("/api/support", validForm);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.False(json.RootElement.GetProperty("isSuccess").GetBoolean());
        Assert.Equal(503, json.RootElement.GetProperty("statusCode").GetInt32());
        Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty("result").ValueKind);
        Assert.Equal("Support email delivery is temporarily unavailable. Please try again later.",
            json.RootElement.GetProperty("errorMessages")[0].GetString());

        using var invalidForm = CreateForm(false);
        using var invalid = await client.PostAsync("/api/support", invalidForm);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal(0, factory.Handler.Calls);
    }

    [Theory]
    [InlineData(201, 200)]
    [InlineData(400, 500)]
    public async Task EnabledSupportReportsSuccessOnlyWhenBrevoAccepts(int providerStatus, int apiStatus)
    {
        using var factory = new SupportFactory(true, providerStatus);
        using var client = factory.CreateClient();
        using var form = CreateForm(true);
        using var response = await client.PostAsync("/api/support", form);
        Assert.Equal(apiStatus, (int)response.StatusCode);
        Assert.Equal(1, factory.Handler.Calls);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(apiStatus == 200, json.RootElement.GetProperty("isSuccess").GetBoolean());
    }

    private static MultipartFormDataContent CreateForm(bool contact)
    {
        var form = new MultipartFormDataContent
        {
            { new StringContent("Test support"), "title" },
            { new StringContent("Test description"), "description" }
        };
        if (contact) form.Add(new StringContent("guest@example.com"), "email");
        return form;
    }

    private static BrevoEmailOptions ValidOptions() => new()
    {
        Enabled = true,
        ApiKey = "not-a-real-key",
        SenderEmail = "sender@example.com",
        SenderName = "Test Support",
        ToEmail = "support@example.com"
    };

    private static IHost CreateOptionsHost(BrevoEmailOptions options) => new HostBuilder()
        .ConfigureServices(services =>
        {
            services.AddSingleton<IValidateOptions<BrevoEmailOptions>, BrevoEmailOptionsValidator>();
            services.AddOptions<BrevoEmailOptions>().Configure(value => Copy(options, value)).ValidateOnStart();
        }).Build();

    private static void Copy(BrevoEmailOptions source, BrevoEmailOptions target)
    {
        target.Enabled = source.Enabled;
        target.ApiKey = source.ApiKey;
        target.SenderEmail = source.SenderEmail;
        target.SenderName = source.SenderName;
        target.ToEmail = source.ToEmail;
    }

    private sealed class SupportFactory(bool enabled, int status = 201) : MealAppApiFactory
    {
        public BrevoHandler Handler { get; } = new(status);

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureTestServices(services =>
            {
                services.PostConfigure<BrevoEmailOptions>(options =>
                {
                    Copy(enabled ? ValidOptions() : new BrevoEmailOptions(), options);
                });
                services.RemoveAll<ISupportRequestService>();
                services.AddHttpClient<ISupportRequestService, SupportRequestService>()
                    .ConfigurePrimaryHttpMessageHandler(() => Handler);
            });
        }
    }

    private sealed class BrevoHandler(int status) : HttpMessageHandler
    {
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            Assert.Equal("https://api.brevo.com/v3/smtp/email", request.RequestUri!.AbsoluteUri);
            Assert.True(request.Headers.Contains("api-key"));
            return Task.FromResult(new HttpResponseMessage((HttpStatusCode)status)
            {
                Content = new StringContent("{}")
            });
        }
    }
}
