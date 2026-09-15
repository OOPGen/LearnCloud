using Asp.Versioning;
using FluentValidation;
using LearnCloud.AI.Extensions;
using LearnCloud.Api.BackgroundJobs;
using LearnCloud.Api.Filters;
using LearnCloud.Api.Hosting;
using LearnCloud.Api.Middleware;
using LearnCloud.AttendanceTimetable.Extensions;
using LearnCloud.Auth.Extensions;
using LearnCloud.Communication.Extensions;
using LearnCloud.Core.Extensions;
using LearnCloud.Examinations.Extensions;
using LearnCloud.Fees.Extensions;
using LearnCloud.Finance.Extensions;
using LearnCloud.HR.Extensions;
using LearnCloud.Hostel.Extensions;
using LearnCloud.Library.Extensions;
using LearnCloud.Messaging.Extensions;
using LearnCloud.MultiTenancy.Context;
using LearnCloud.MultiTenancy.Extensions;
using LearnCloud.MultiTenancy.Middleware;
using LearnCloud.OnlinePayments.Extensions;
using LearnCloud.ParentPortal.Extensions;
using LearnCloud.PlatformAdmin.Extensions;
using LearnCloud.PlatformAdmin.Middleware;
using LearnCloud.PlatformBilling.Extensions;
using LearnCloud.PlatformBilling.Middleware;
using LearnCloud.SetupWizard.Extensions;
using LearnCloud.StudentPortal.Extensions;
using LearnCloud.TeacherPortal.Extensions;
using LearnCloud.Transport.Extensions;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Hosting platform conventions (Railway): PORT to bind, DATABASE_URL for PostgreSQL.
builder.WebHost.BindPlatformPort();
builder.Configuration.UseDatabaseUrlIfPresent();

// Structured JSON logs outside development, so Railway log search can filter by field.
if (!builder.Environment.IsDevelopment())
{
    builder.Logging.ClearProviders();
    builder.Logging.AddJsonConsole(options => options.IncludeScopes = true);
}

// Behind Railway's edge (and the Cloudflare Worker proxy) the connection comes from a
// proxy. Without this, every request appears to come from the proxy's address: rate
// limits would be shared by all users and Request.IsHttps would be false.
// ForwardLimit covers "client -> Cloudflare Worker -> Railway edge". Anyone who can
// reach the API directly can still spoof X-Forwarded-For; see docs/DEPLOYMENT.md.
var forwardedHeadersEnabled = builder.Configuration.GetValue("ForwardedHeaders:Enabled", false);
if (forwardedHeadersEnabled)
{
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.ForwardLimit = builder.Configuration.GetValue("ForwardedHeaders:ForwardLimit", 2);
        options.KnownNetworks.Clear();
        options.KnownProxies.Clear();
    });
}

// Fail at startup rather than on the first request: every registered service and every
// controller (registered as services below) must have resolvable dependencies, and no
// singleton may capture a scoped service. Before Phase 2 only Auth and MultiTenancy were
// registered, so 20 of 22 controllers would have thrown on their first request.
builder.Host.UseDefaultServiceProvider(options =>
{
    options.ValidateScopes = true;
    options.ValidateOnBuild = true;
});

// Data and identity
builder.Services.AddLearnCloudMultiTenancy(builder.Configuration, migrationsAssembly: typeof(Program).Assembly.GetName().Name!);
builder.Services.AddLearnCloudAuth(builder.Configuration);

// Feature modules
builder.Services
    .AddLearnCloudCore()
    .AddLearnCloudAttendanceTimetable()
    .AddLearnCloudExaminations()
    .AddLearnCloudFees()
    .AddLearnCloudFinance()
    .AddLearnCloudHR()
    .AddLearnCloudHostel()
    .AddLearnCloudLibrary()
    .AddLearnCloudTransport()
    .AddLearnCloudSetupWizard()
    .AddLearnCloudStudentPortal()
    .AddLearnCloudTeacherPortal()
    .AddLearnCloudParentPortal()
    .AddLearnCloudPlatformAdmin()
    .AddLearnCloudPlatformBilling()
    .AddLearnCloudCommunication()
    .AddLearnCloudAI(builder.Configuration)
    .AddLearnCloudOnlinePayments(builder.Configuration)
    .AddLearnCloudMessaging(builder.Configuration);

foreach (var moduleAssembly in LearnCloudModel.Assemblies)
    builder.Services.AddValidatorsFromAssembly(moduleAssembly);

if (builder.Configuration.GetValue("Jobs:Enabled", true))
    builder.Services.AddHostedService<ScheduledJobsWorker>();

// CORS - explicit origins from configuration (Cors:AllowedOrigins), no wildcard hosts
// beyond the ones listed. The web app is served same-origin through the Cloudflare
// Pages proxy and does not need CORS; this is for other browser clients.
// Local Vite origins are the fallback only in Development; elsewhere no origin is allowed
// unless configured.
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() is { Length: > 0 } configured
    ? configured
    : builder.Environment.IsDevelopment() ? new[] { "http://localhost:5173", "http://localhost:3000" } : Array.Empty<string>();
builder.Services.AddCors(options =>
{
    options.AddPolicy("tenant", policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .SetIsOriginAllowedToAllowWildcardSubdomains()
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials()
              .WithExposedHeaders("X-Request-ID", "X-Total-Count", "Link");
    });
});

builder.Services.AddControllers(options =>
{
    options.Filters.Add(new ProducesAttribute("application/json"));
    options.Filters.Add<FluentValidationFilter>();
})
.AddControllersAsServices()
.ConfigureApiBehaviorOptions(options =>
{
    // Return 422 for validation errors with ProblemDetails
    options.InvalidModelStateResponseFactory = context =>
    {
        var problem = new ValidationProblemDetails(context.ModelState)
        {
            Status = 422,
            Title = "Validation failed",
            Type = "https://learncloud.co.zw/errors/validation",
            Instance = context.HttpContext.Request.Path
        };
        return new UnprocessableEntityObjectResult(problem);
    };
});

builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
    options.ApiVersionReader = ApiVersionReader.Combine(
        new UrlSegmentApiVersionReader(),
        new HeaderApiVersionReader("X-API-Version"),
        new QueryStringApiVersionReader("api-version")
    );
})
.AddMvc()
.AddApiExplorer(options =>
{
    options.GroupNameFormat = "'v'VVV";
    options.SubstituteApiVersionInUrl = true;
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "LearnCloud API",
        Version = "v1",
        Description = "Multi-tenant School Management System - HQ Bulawayo, 150-2000 learners.",
        Contact = new OpenApiContact { Name = "LearnCloud Support", Email = "hello@learncloud.co.zw", Url = new Uri("https://learncloud.co.zw") }
    });
    // Several modules declare the same DTO names; schema ids must be unique.
    c.CustomSchemaIds(type => type.FullName!.Replace('+', '.'));
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

// `dotnet LearnCloud.Api.dll --migrate` applies pending EF Core migrations and exits.
// This is the single migration runner for every environment (local, CI, deploy).
if (args.Contains("--migrate"))
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<LearnCloudDbContext>();
    var pending = (await db.Database.GetPendingMigrationsAsync()).ToList();
    app.Logger.LogInformation("Applying {Count} pending migration(s): {Migrations}", pending.Count, string.Join(", ", pending));
    await db.Database.MigrateAsync();
    app.Logger.LogInformation("Database is up to date");
    return;
}

// Global exception handler with ProblemDetails
app.UseExceptionHandler(appBuilder =>
{
    appBuilder.Run(async context =>
    {
        var exception = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()?.Error;
        var requestId = context.TraceIdentifier;

        int statusCode = 500;
        string title = "Internal server error";
        string detail = "An unexpected error occurred";
        string type = "https://learncloud.co.zw/errors/internal";

        // Services signal business errors ("Slug already taken", "Subject not found") with
        // InvalidOperationException, and those messages are written for API clients. EF Core
        // and Npgsql throw the same type with messages that describe internals, so only
        // exceptions thrown from LearnCloud code are mapped; anything else is a logged 500.
        var thrownByLearnCloud = exception?.TargetSite?.DeclaringType?.Assembly.GetName().Name?.StartsWith("LearnCloud.", StringComparison.Ordinal) == true;

        if (exception is InvalidOperationException invalidOp && thrownByLearnCloud)
        {
            if (invalidOp.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
            {
                statusCode = 404; title = "Resource not found"; detail = invalidOp.Message; type = "https://learncloud.co.zw/errors/not-found";
            }
            else if (invalidOp.Message.Contains("already exists", StringComparison.OrdinalIgnoreCase) || invalidOp.Message.Contains("already taken", StringComparison.OrdinalIgnoreCase)
                     || invalidOp.Message.Contains("already linked", StringComparison.OrdinalIgnoreCase))
            {
                statusCode = 409; title = "Conflict"; detail = invalidOp.Message; type = "https://learncloud.co.zw/errors/conflict";
            }
            else
            {
                statusCode = 400; title = "Bad request"; detail = invalidOp.Message; type = "https://learncloud.co.zw/errors/bad-request";
            }
        }
        else if (exception is UnauthorizedAccessException && thrownByLearnCloud)
        {
            statusCode = 401; title = "Unauthorized"; detail = exception.Message; type = "https://learncloud.co.zw/errors/unauthorized";
        }
        else if (exception is ValidationException valEx)
        {
            statusCode = 422; title = "Validation failed"; detail = string.Join("; ", valEx.Errors.Select(e => e.ErrorMessage)); type = "https://learncloud.co.zw/errors/validation";
        }

        if (statusCode >= 500)
            app.Logger.LogError(exception, "Unhandled exception for {Method} {Path} request {RequestId}", context.Request.Method, context.Request.Path, requestId);

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";
        context.Response.Headers["X-Request-ID"] = requestId;

        await context.Response.WriteAsJsonAsync(new
        {
            type,
            title,
            status = statusCode,
            detail,
            instance = context.Request.Path.Value,
            requestId,
            errors = (exception as ValidationException)?.Errors?.Select(e => new { field = e.PropertyName, message = e.ErrorMessage })
        });
    });
});

if (forwardedHeadersEnabled)
    app.UseForwardedHeaders();
// After forwarded headers, before rate limiting: a request from the Cloudflare Worker proxy
// carrying the shared secret gets its real client IP.
app.UseMiddleware<TrustedProxyClientIpMiddleware>();

app.UseSecurityHeaders();

app.UseRouting();
app.UseCors("tenant");
app.UseRateLimiter();
app.UseAuthentication();
app.UseMiddleware<TenantResolutionMiddleware>();
app.UseMiddleware<SecondFactorMiddleware>();
app.UseMiddleware<FeatureGatingMiddleware>();
app.UseAuthorization();

if (app.Environment.IsDevelopment() || app.Environment.IsStaging())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "LearnCloud API v1");
        c.RoutePrefix = "swagger";
        c.DocumentTitle = "LearnCloud API Docs";
    });
}

app.MapControllers().RequireCors("tenant");

// Liveness: the process is up. Readiness: the database answers.
app.MapGet("/health", () => Results.Ok(new { status = "ok", time = DateTime.UtcNow, environment = app.Environment.EnvironmentName }))
   .RequireCors("tenant");
// Readiness is also served under /api so it can be checked through the Cloudflare Worker
// proxy, which only forwards /api/* (everything else there is the web app).
foreach (var readinessPath in new[] { "/health/ready", "/api/health" })
{
    app.MapGet(readinessPath, async (LearnCloudDbContext db, CancellationToken ct) =>
            await db.Database.CanConnectAsync(ct)
                ? Results.Ok(new { status = "ready" })
                : Results.Json(new { status = "database_unavailable" }, statusCode: 503))
       .RequireCors("tenant");
}

app.Run();

// Exposed for WebApplicationFactory in integration tests.
public partial class Program { }
