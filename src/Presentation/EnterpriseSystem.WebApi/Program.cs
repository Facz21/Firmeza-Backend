using System.Text;
using EnterpriseSystem.Application;
using EnterpriseSystem.Application.Common.Interfaces;
using EnterpriseSystem.Infrastructure;
using EnterpriseSystem.Infrastructure.Persistence;
using EnterpriseSystem.WebApi.Middleware;
using EnterpriseSystem.WebApi.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// 1. Add Application & Infrastructure Services
builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration);

// 2. Add WebApi Services
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddControllers();

// 3. Configure CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("CorsPolicy", policy =>
    {
        policy.WithOrigins(
                  "http://localhost:5173",
                  "http://localhost:3000",
                  "http://localhost:4173",
                  "http://localhost:8080",
                  "http://localhost",
                  "http://127.0.0.1:5173",
                  "http://127.0.0.1:8080")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials()
              .WithExposedHeaders("Content-Disposition");
    });
});

// 4. Configure Authentication & Authorization
var secretKey = builder.Configuration["Jwt:SecretKey"] ?? "EnterpriseSystem_SuperSecretSecureKey_2026_DotNet10_DefaultSigningKey!";
var issuer = builder.Configuration["Jwt:Issuer"] ?? "EnterpriseSystem";
var audience = builder.Configuration["Jwt:Audience"] ?? "EnterpriseSystemClient";

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
        ValidateIssuer = true,
        ValidIssuer = issuer,
        ValidateAudience = true,
        ValidAudience = audience,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };
});

builder.Services.AddAuthorization();

// 5. Configure OpenAPI
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, context, cancellationToken) =>
    {
        document.Info = new Microsoft.OpenApi.OpenApiInfo
        {
            Title = "Enterprise Asset & Inventory Management API",
            Version = "v1",
            Description = "Production-grade Clean Architecture .NET 10 Web API with JWT Authentication, CQRS, and Scalar Documentation."
        };
        return Task.CompletedTask;
    });
});

var app = builder.Build();

// 6. Global Exception Handling Middleware
app.UseMiddleware<ExceptionHandlingMiddleware>();

// 7. Configure Middleware Pipeline
app.UseCors("CorsPolicy");

app.MapOpenApi();
app.MapScalarApiReference();

// Redirect root to /scalar/v1 for developer convenience
app.MapGet("/", () => Results.Redirect("/scalar/v1"));

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// 8. Auto-migrate and seed database
using (var scope = app.Services.CreateScope())
{
    var initialiser = scope.ServiceProvider.GetRequiredService<ApplicationDbContextInitialiser>();
    await initialiser.InitialiseAsync();
    await initialiser.SeedAsync();
}

app.Run();

// Make Program public for integration testing if needed
public partial class Program { }
