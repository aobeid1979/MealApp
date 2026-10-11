using MealApp.Data;
using MealApp.Middleware;
using MealApp.Models;
using MealApp.Services;
using MealApp.Services.Import;
using MealApp.Services.AgentTools;
using MealApp.Services.Agent;
using MealApp.Services.AgentActions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);
const string FrontendCorsPolicy = "FrontendCorsPolicy";

ValidateProductionConfiguration(builder.Configuration, builder.Environment);

// Add services to the container.

builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            var errors = context.ModelState
                .Where(modelState => modelState.Value?.Errors.Count > 0)
                .SelectMany(modelState => modelState.Value!.Errors)
                .Select(error => string.IsNullOrWhiteSpace(error.ErrorMessage)
                    ? "Invalid request."
                    : error.ErrorMessage)
                .ToList();

            var response = new ApiResponse
            {
                IsSuccess = false,
                StatusCode = StatusCodes.Status400BadRequest,
                ErrorMessages = errors
            };

            return new BadRequestObjectResult(response);
        };
    });
builder.Services.AddScoped<DataImportService>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<RecipeReadService>();
builder.Services.AddSingleton<ToolRegistry>();
builder.Services.AddScoped<AgentToolExecutor>();
builder.Services.AddSingleton<AgentToolPolicy>();
builder.Services.AddSingleton<IPendingActionStore, InMemoryPendingActionStore>();
builder.Services.AddScoped<AgentActionService>();
builder.Services.Configure<AgentOptions>(builder.Configuration.GetSection("Agent"));
builder.Services.AddHttpClient<IAgentModelClient, OllamaModelClient>(client => client.Timeout = Timeout.InfiniteTimeSpan)
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
builder.Services.AddScoped<AgentChatService>();
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sqlOptions =>
        {
            sqlOptions.EnableRetryOnFailure(
                maxRetryCount: 6,
                maxRetryDelay: TimeSpan.FromSeconds(10),
                errorNumbersToAdd: [40613]);
        }));
builder.Services.AddCors(options =>
{
    options.AddPolicy(FrontendCorsPolicy, policy =>
    {
        var allowedOrigins = builder.Configuration
            .GetSection("Cors:AllowedOrigins")
            .Get<string[]>() ?? [];

        policy.WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        // Email verification is disabled temporarily until SMTP/email provider is configured.
        options.SignIn.RequireConfirmedEmail = false;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

var jwtSecret = builder.Configuration["Jwt:Secret"];

if (string.IsNullOrWhiteSpace(jwtSecret))
{
    throw new InvalidOperationException(
        "Jwt:Secret is not configured. Set it with .NET User Secrets or the Jwt__Secret environment variable.");
}

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            ClockSkew = TimeSpan.Zero,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtSecret))
        };
    });

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter your JWT token."
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    await RoleSeeder.SeedRolesAsync(scope.ServiceProvider);
    await AdminSeeder.SeedDefaultAdminAsync(scope.ServiceProvider);
    await SampleDataSeeder.SeedAsync(scope.ServiceProvider);
}

// Configure the HTTP request pipeline.
app.UseMiddleware<GlobalExceptionMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseStatusCodePages(async statusCodeContext =>
{
    var httpContext = statusCodeContext.HttpContext;
    var response = httpContext.Response;

    if (response.HasStarted || response.StatusCode < 400)
    {
        return;
    }

    var message = response.StatusCode switch
    {
        StatusCodes.Status401Unauthorized => "Unauthorized.",
        StatusCodes.Status403Forbidden => "Forbidden.",
        StatusCodes.Status404NotFound => "Not found.",
        _ => "An error occurred."
    };

    response.ContentType = "application/json";

    await response.WriteAsJsonAsync(new ApiResponse
    {
        IsSuccess = false,
        StatusCode = response.StatusCode,
        ErrorMessages = [message]
    });
});

app.UseCors(FrontendCorsPolicy);

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

static void ValidateProductionConfiguration(IConfiguration configuration, IWebHostEnvironment environment)
{
    if (!environment.IsProduction())
    {
        return;
    }

    string[] requiredEnvironmentVariables =
    [
        "ConnectionStrings__DefaultConnection",
        "Jwt__Secret",
        "Jwt__Issuer",
        "Jwt__Audience",
        "AdminUser__Email",
        "AdminUser__Password",
        "Cors__AllowedOrigins__0",
        "SeedSampleData"
    ];

    foreach (var variableName in requiredEnvironmentVariables)
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(variableName)))
        {
            throw new InvalidOperationException(
                $"{variableName} is not configured. Set it as a production environment variable.");
        }
    }

    var requiredValues = new Dictionary<string, string?>
    {
        ["ConnectionStrings:DefaultConnection"] = configuration.GetConnectionString("DefaultConnection"),
        ["Jwt:Secret"] = configuration["Jwt:Secret"],
        ["Jwt:Issuer"] = configuration["Jwt:Issuer"],
        ["Jwt:Audience"] = configuration["Jwt:Audience"],
        ["AdminUser:Email"] = configuration["AdminUser:Email"],
        ["AdminUser:Password"] = configuration["AdminUser:Password"]
    };

    foreach (var requiredValue in requiredValues)
    {
        if (string.IsNullOrWhiteSpace(requiredValue.Value))
        {
            throw new InvalidOperationException(
                $"{requiredValue.Key} is not configured. Set it with a production environment variable.");
        }
    }

    var connectionString = configuration.GetConnectionString("DefaultConnection")!;

    if (connectionString.Contains("(localdb)", StringComparison.OrdinalIgnoreCase)
        || connectionString.Contains("mssqllocaldb", StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException(
            "ConnectionStrings:DefaultConnection must not use LocalDB in Production.");
    }

    var allowedOrigins = configuration
        .GetSection("Cors:AllowedOrigins")
        .Get<string[]>() ?? [];

    if (allowedOrigins.Length == 0 || allowedOrigins.Any(string.IsNullOrWhiteSpace))
    {
        throw new InvalidOperationException(
            "Cors:AllowedOrigins is not configured. Set Cors__AllowedOrigins__0 to the production frontend URL.");
    }

    if (!configuration.GetSection("SeedSampleData").Exists())
    {
        throw new InvalidOperationException(
            "SeedSampleData is not configured. Set SeedSampleData to false unless sample data is intentionally needed.");
    }
}

public partial class Program
{
}
