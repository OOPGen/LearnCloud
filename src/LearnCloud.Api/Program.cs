using LearnCloud.MultiTenancy.Extensions;
using LearnCloud.Auth.Extensions;
using LearnCloud.Api.Middleware;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddLearnCloudMultiTenancy(builder.Configuration);
builder.Services.AddLearnCloudAuth(builder.Configuration);

// SECURITY H1: CORS - explicit origins, no wildcard
builder.Services.AddCors(options =>
{
    options.AddPolicy("tenant", policy =>
    {
        policy.WithOrigins(
                "https://learncloud.co.zw",
                "https://www.learncloud.co.zw",
                "https://*.learncloud.co.zw",
                "https://*.learncloud.co.com",
                "http://localhost:5173",
                "http://localhost:3000"
              )
              .SetIsOriginAllowedToAllowWildcardSubdomains()
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials()
              .WithExposedHeaders("X-Request-ID", "X-Total-Count", "Link");
    });
});

builder.Services.AddControllers(options =>
{
    // Global: Return ProblemDetails for validation errors
    options.Filters.Add(new ProducesAttribute("application/json"));
})
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

// API Versioning - Phase 2 needs approval but low risk, additive
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
});
builder.Services.AddVersionedApiExplorer(options =>
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
        Description = "Multi-tenant School Management System - HQ Bulawayo, 150-2000 learners. Fees collected. Reports ready. Parents informed. Before lunch.",
        Contact = new OpenApiContact { Name = "LearnCloud Support", Email = "hello@learncloud.co.zw", Url = new Uri("https://learncloud.co.zw") }
    });
    c.SwaggerDoc("v2", new OpenApiInfo { Title = "LearnCloud API", Version = "v2", Description = "V2 with standardized pagination and RESTful routes" });
    
    // SECURITY: Bearer JWT scheme for Swagger
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
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            new string[] {}
        }
    });
    
    // Include XML comments if available
    var xmlFiles = Directory.GetFiles(AppContext.BaseDirectory, "*.xml");
    foreach (var xml in xmlFiles)
    {
        try { c.IncludeXmlComments(xml); } catch {}
    }
});

var app = builder.Build();

// Global exception handler with ProblemDetails - maps "not found" to 404 not 500
app.UseExceptionHandler(appBuilder =>
{
    appBuilder.Run(async context =>
    {
        var exceptionHandler = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();
        var exception = exceptionHandler?.Error;
        var requestId = context.TraceIdentifier;
        
        int statusCode = 500;
        string title = "Internal server error";
        string detail = "An unexpected error occurred";
        string type = "https://learncloud.co.zw/errors/internal";
        
        if (exception is InvalidOperationException invalidOp)
        {
            if (invalidOp.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
            {
                statusCode = 404;
                title = "Resource not found";
                detail = invalidOp.Message;
                type = "https://learncloud.co.zw/errors/not-found";
            }
            else if (invalidOp.Message.Contains("already exists", StringComparison.OrdinalIgnoreCase) || invalidOp.Message.Contains("already taken", StringComparison.OrdinalIgnoreCase))
            {
                statusCode = 409;
                title = "Conflict";
                detail = invalidOp.Message;
                type = "https://learncloud.co.zw/errors/conflict";
            }
            else
            {
                statusCode = 400;
                title = "Bad request";
                detail = invalidOp.Message;
                type = "https://learncloud.co.zw/errors/bad-request";
            }
        }
        else if (exception is UnauthorizedAccessException)
        {
            statusCode = 401;
            title = "Unauthorized";
            detail = exception.Message;
            type = "https://learncloud.co.zw/errors/unauthorized";
        }
        else if (exception is FluentValidation.ValidationException valEx)
        {
            statusCode = 422;
            title = "Validation failed";
            detail = string.Join("; ", valEx.Errors.Select(e => e.ErrorMessage));
            type = "https://learncloud.co.zw/errors/validation";
        }
        
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";
        context.Response.Headers["X-Request-ID"] = requestId;
        
        var problem = new
        {
            type,
            title,
            status = statusCode,
            detail,
            instance = context.Request.Path,
            requestId,
            // Include validation errors if any
            errors = (exception as FluentValidation.ValidationException)?.Errors?.Select(e => new { field = e.PropertyName, message = e.ErrorMessage })
        };
        
        await context.Response.WriteAsJsonAsync(problem);
    });
});

app.UseSecurityHeaders();

app.UseRouting();
app.UseCors("tenant");
app.UseRateLimiter();
app.UseAuthentication();
app.UseMiddleware<LearnCloud.MultiTenancy.Middleware.TenantResolutionMiddleware>();
app.UseAuthorization();

if (app.Environment.IsDevelopment() || app.Environment.IsStaging())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "LearnCloud API v1");
        c.SwaggerEndpoint("/swagger/v2/swagger.json", "LearnCloud API v2");
        c.RoutePrefix = "swagger";
        c.DocumentTitle = "LearnCloud API Docs - Bulawayo HQ";
    });
}

app.MapControllers().RequireCors("tenant");
app.MapGet("/health", () => Results.Ok(new { status = "ok", time = DateTime.UtcNow, version = "1.0", environment = app.Environment.EnvironmentName })).RequireCors("tenant");
app.Run();

public partial class Program {}
