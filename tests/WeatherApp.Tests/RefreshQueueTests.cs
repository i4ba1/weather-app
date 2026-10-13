using Microsoft.Extensions.Options;
using WeatherApp.Infrastructure.RefreshJobs;

namespace WeatherApp.Tests;

public class RefreshQueueTests
{
    [Fact]
    public async Task BoundedQueueWaitsForCapacityAndSuppressesDuplicateOutstandingIds()
    {
        var queue = new WeatherRefreshQueue(Options.Create(new WeatherRefreshOptions { Capacity = 1 }));
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        await queue.EnqueueAsync(first, default);
        await queue.EnqueueAsync(first, default);
        var waiting = queue.EnqueueAsync(second, default);
        Assert.False(waiting.IsCompleted);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await using var reader = queue.ReadAllAsync(deadline.Token).GetAsyncEnumerator();
        Assert.True(await reader.MoveNextAsync());
        Assert.Equal(first, reader.Current);
        await waiting.WaitAsync(deadline.Token);
        Assert.True(await reader.MoveNextAsync());
        Assert.Equal(second, reader.Current);
        // Outstanding includes executing work, not just items still in the channel.
        await queue.EnqueueAsync(first, default);
        queue.Complete(first);
        await queue.EnqueueAsync(first, default);
        Assert.True(await reader.MoveNextAsync());
        Assert.Equal(first, reader.Current);
    }

    [Fact]
    public async Task CancelledWriteCanBeEnqueuedAgainWithoutLosingId()
    {
        var queue = new WeatherRefreshQueue(Options.Create(new WeatherRefreshOptions { Capacity = 1 }));
        await queue.EnqueueAsync(Guid.NewGuid(), default);
        var id = Guid.NewGuid();
        using var cancellation = new CancellationTokenSource();
        var blocked = queue.EnqueueAsync(id, cancellation.Token);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => blocked);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await using var reader = queue.ReadAllAsync(deadline.Token).GetAsyncEnumerator();
        await reader.MoveNextAsync();
        await queue.EnqueueAsync(id, deadline.Token);
        await reader.MoveNextAsync();
        Assert.Equal(id, reader.Current);
    }
}
