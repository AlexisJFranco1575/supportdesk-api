using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Scalar.AspNetCore;
using SupportDesk.Api.Endpoints;
using SupportDesk.Api.ErrorHandling;
using SupportDesk.Application;
using SupportDesk.Infrastructure;
using SupportDesk.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("SupportDesk")
    ?? throw new InvalidOperationException(
        "Connection string 'SupportDesk' is not configured. See the README for local setup.");

builder.Services.AddApplication();
builder.Services.AddInfrastructure(connectionString);

// Enums travel as readable strings ("InProgress") instead of numbers.
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();

builder.Services.AddOpenApi();

// "ready" checks verify dependencies (the database); "live" only proves the process responds.
builder.Services.AddHealthChecks()
    .AddDbContextCheck<SupportDeskDbContext>("database", tags: ["ready"]);

var app = builder.Build();

app.UseExceptionHandler();

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

app.MapGet("/", () => Results.Redirect("/scalar")).ExcludeFromDescription();

app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});

app.MapTicketEndpoints();

app.Run();

// Makes the entry point visible to integration tests.
public partial class Program;