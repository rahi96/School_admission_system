namespace UserApi.Services;

public class HoldExpiryService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<HoldExpiryService> _logger;
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(5);

    public HoldExpiryService(IServiceProvider services, ILogger<HoldExpiryService> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _services.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<AdmissionService>();
                var expired = await service.ExpireHoldsAsync();
                if (expired > 0)
                {
                    _logger.LogInformation("Released {Count} expired hold(s).", expired);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while expiring holds.");
            }

            try
            {
                await Task.Delay(Interval, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                break;
            }
        }
    }
}
