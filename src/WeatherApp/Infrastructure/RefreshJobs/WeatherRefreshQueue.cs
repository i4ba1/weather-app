using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.Extensions.Options;

namespace WeatherApp.Infrastructure.RefreshJobs;

public sealed class WeatherRefreshQueue(IOptions<WeatherRefreshOptions> options)
{
    private readonly Channel<Guid> channel = Channel.CreateBounded<Guid>(new BoundedChannelOptions(options.Value.Capacity)
    {
        FullMode = BoundedChannelFullMode.Wait,
        SingleWriter = true,
        SingleReader = false
    });
    private readonly ConcurrentDictionary<Guid, byte> outstanding = new();

    public async Task EnqueueAsync(Guid id, CancellationToken token)
    {
        if (!outstanding.TryAdd(id, 0)) return;
        try { await channel.Writer.WriteAsync(id, token); }
        catch { outstanding.TryRemove(id, out _); throw; }
    }
    public IAsyncEnumerable<Guid> ReadAllAsync(CancellationToken token) => channel.Reader.ReadAllAsync(token);
    public void Complete(Guid id) => outstanding.TryRemove(id, out _);
}
