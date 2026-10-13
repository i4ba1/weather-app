namespace WeatherApp.Infrastructure.RefreshJobs;

public sealed class WeatherRefreshOptions
{
    public int Capacity { get; set; } = 20;
    public int Workers { get; set; } = 2;
    public int MaxAttempts { get; set; } = 3;
    public int DispatcherIntervalMilliseconds { get; set; } = 1000;
    public int ProviderIntervalMilliseconds { get; set; } = 1000;
    public int RetryBaseMilliseconds { get; set; } = 2000;
}
