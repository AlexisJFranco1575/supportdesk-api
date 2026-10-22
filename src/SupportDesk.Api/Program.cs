using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using SupportDesk.Api.Documentation;
using SupportDesk.Api.Endpoints;
using SupportDesk.Api.ErrorHandling;
using SupportDesk.Api.Security;
using SupportDesk.Application;
using SupportDesk.Application.Auth;
using SupportDesk.Infrastructure;
using SupportDesk.Infrastructure.Auth;
using SupportDesk.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("SupportDesk")
    ?? throw new InvalidOperationException(
        "Connection string 'SupportDesk' is not configured. See the README for local setup.");

var jwtSection = builder.Configuration.GetSection(JwtOptions.SectionName);
var jwt = jwtSection.Get<JwtOptions>() ?? new JwtOptions();

if (jwt.SigningKey.Length < 32)
{
    throw new InvalidOperationException(
        "Jwt:SigningKey must be at least 32 characters. See the README for local setup.");
}

builder.Services.Configure<JwtOptions>(jwtSection);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(connectionString);

// NEW: the identity of the caller, exposed to the Application layer through ICurrentUser.
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpContextCurrentUser>();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Keep claim names exactly as issued ("sub", "role", ...) instead of remapping them.
        options.MapInboundClaims = false;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = "name",
            RoleClaimType = "role"
        };
    });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy(Policies.Agent, policy => policy.RequireRole("Agent", "Admin"))
    .AddPolicy(Policies.Admin, policy => policy.RequireRole("Admin"));

// Enums travel as readable strings ("InProgress") instead of numbers.
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();

builder.Services.AddOpenApi(options => options.AddDocumentTransformer<BearerSecuritySchemeTransformer>());

// "ready" checks verify dependencies (the database); "live" only proves the process responds.
builder.Services.AddHealthChecks()
    .AddDbContextCheck<SupportDeskDbContext>("database", tags: ["ready"]);

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();            // OpenAPI document at /openapi/v1.json
    app.MapScalarApiReference(); // Interactive docs at /scalar
}
else
{
    // In production TLS is usually terminated by the platform, so only redirect
    // when the app itself is the one serving HTTPS.
    app.UseHttpsRedirection();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => Results.Redirect("/scalar")).ExcludeFromDescription();

app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});

app.MapAuthEndpoints();
app.MapUserEndpoints();
app.MapTicketEndpoints();

// Bootstrap the first administrator when credentials are provided (user-secrets or environment).
var adminEmail = builder.Configuration["Seed:AdminEmail"];
var adminPassword = builder.Configuration["Seed:AdminPassword"];

if (!string.IsNullOrWhiteSpace(adminEmail) && !string.IsNullOrWhiteSpace(adminPassword))
{
    using var scope = app.Services.CreateScope();
    var auth = scope.ServiceProvider.GetRequiredService<IAuthService>();
    await auth.EnsureAdminAsync(adminEmail, adminPassword, "Administrator", CancellationToken.None);
}

app.Run();

// Makes the entry point visible to integration tests.
public partial class Program;