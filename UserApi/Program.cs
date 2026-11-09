using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using UserApi.Data;
using UserApi.Models;
using UserApi.Services;

var builder = WebApplication.CreateBuilder(args);

// Bind to port 8080 as required by the assignment (overridable via ASPNETCORE_URLS).
if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ASPNETCORE_URLS")))
{
    builder.WebHost.UseUrls("http://0.0.0.0:8080");
}

// Add services to the container.
builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// Connection string: prefer ConnectionStrings:Default, fall back to DATABASE_URL env var.
var connectionString = builder.Configuration.GetConnectionString("Default")
                       ?? Environment.GetEnvironmentVariable("DATABASE_URL");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "No database connection string found. Set ConnectionStrings:Default or the DATABASE_URL environment variable.");
}

var normalizedConnectionString = ConnectionStringHelper.Normalize(connectionString);
builder.Services.AddSingleton(_ =>
{
    var csb = new NpgsqlConnectionStringBuilder(normalizedConnectionString);
    // Headroom for bursty admission traffic; keep a bounded wait so a stuck
    // request fails fast (as 503) instead of hanging.
    if (csb.MaxPoolSize < 200) csb.MaxPoolSize = 200;
    if (csb.Timeout < 30) csb.Timeout = 30;
    return NpgsqlDataSource.Create(csb.ConnectionString);
});
builder.Services.AddScoped<AdmissionService>();
builder.Services.AddHostedService<HoldExpiryService>();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
});

var app = builder.Build();

// Ensure database schema exists.
using (var scope = app.Services.CreateScope())
{
    var dataSource = scope.ServiceProvider.GetRequiredService<NpgsqlDataSource>();
    await DbInitializer.InitializeAsync(dataSource);
}

// Global safety net: translate any unhandled exception into a clean JSON error.
// Transient DB overload becomes 503; everything else a generic 500-free message,
// so the UI never sees a raw stack trace.
app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var feature = context.Features.Get<IExceptionHandlerFeature>();
        var ex = feature?.Error;
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Unhandled exception for {Method} {Path}.", context.Request.Method, context.Request.Path);

        var (status, error, message) = ex is NpgsqlException or TimeoutException
            ? (StatusCodes.Status503ServiceUnavailable, "UNAVAILABLE", "The system is busy. Please try again in a moment.")
            : (StatusCodes.Status500InternalServerError, "INTERNAL_ERROR", "Something went wrong. Please try again.");

        context.Response.StatusCode = status;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new ErrorResponse(error, message));
    });
});

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "School Admission API v1");
    });
}

app.UseCors();

app.MapControllers();

app.Run();

public partial class Program { }
