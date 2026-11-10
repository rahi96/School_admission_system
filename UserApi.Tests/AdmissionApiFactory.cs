using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.PostgreSql;

namespace UserApi.Tests;

/// <summary>
/// Spins up a throwaway PostgreSQL container and boots the real API against it,
/// so tests exercise the full stack (controllers + SQL + concurrency logic)
/// without touching any development or cloud database.
/// </summary>
public class AdmissionApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16")
        .WithDatabase("admission_test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    /// <summary>Hold duration used by the API under test (seconds).</summary>
    public int HoldDurationSeconds { get; set; } = 600;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");

        // Program.cs reads these from configuration / environment at startup. Setting
        // them as host settings guarantees they are present before the app builds.
        builder.UseSetting("ConnectionStrings:Default", _postgres.GetConnectionString());
        builder.UseSetting("HOLD_DURATION_SECONDS", HoldDurationSeconds.ToString());
    }

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
    }

    public new async Task DisposeAsync()
    {
        await _postgres.DisposeAsync();
        await base.DisposeAsync();
    }
}
