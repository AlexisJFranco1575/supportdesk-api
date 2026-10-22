using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();

var app = builder.Build();

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
app.MapHealthChecks("/health/live");

app.Run();

// Makes the entry point visible to integration tests (added in a later milestone).
public partial class Program;