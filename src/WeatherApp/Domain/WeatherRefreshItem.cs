namespace WeatherApp.Domain;

public enum RefreshStatus { Pending, Running, Completed, Failed }

public sealed class WeatherRefreshJob
{
    private WeatherRefreshJob() { }
    public WeatherRefreshJob(DateTimeOffset createdAtUtc) { Id = Guid.NewGuid(); CreatedAtUtc = createdAtUtc; }
    public Guid Id { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public List<WeatherRefreshItem> Cities { get; private set; } = [];
}

public sealed class WeatherRefreshItem
{
    private WeatherRefreshItem() { }
    public WeatherRefreshItem(Guid jobId, string cityName, string countryCode, int position)
    {
        Id = Guid.NewGuid(); JobId = jobId; CityName = cityName; CountryCode = countryCode; Position = position;
    }
    public Guid Id { get; private set; }
    public Guid JobId { get; private set; }
    public string CityName { get; private set; } = "";
    public string CountryCode { get; private set; } = "";
    public int Position { get; private set; }
    public RefreshStatus Status { get; private set; }
    public int Attempts { get; private set; }
    public long NextAttemptAtUnixMs { get; private set; }
    public long? StartedAtUnixMs { get; private set; }
    public string? WeatherJson { get; private set; }
    public string? Error { get; private set; }

    public void Complete(string weatherJson)
    {
        RequireRunning(); Status = RefreshStatus.Completed; WeatherJson = weatherJson; Error = null;
    }
    public void Fail(string safeError, bool retry, long nextAttemptAtUnixMs)
    {
        RequireRunning(); Status = retry ? RefreshStatus.Pending : RefreshStatus.Failed;
        Error = safeError; NextAttemptAtUnixMs = nextAttemptAtUnixMs; StartedAtUnixMs = null;
    }
    private void RequireRunning()
    {
        if (Status != RefreshStatus.Running) throw new InvalidOperationException("Only running work can finish.");
    }
}
