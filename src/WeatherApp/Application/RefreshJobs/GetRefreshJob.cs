using MediatR;
using WeatherApp.Application.Abstractions;
using WeatherApp.Application.Common;

namespace WeatherApp.Application.RefreshJobs;

public sealed record GetRefreshJobQuery(Guid Id) : IRequest<RefreshJobDto>;
public sealed class GetRefreshJobHandler(IWeatherRefreshRepository repository) : IRequestHandler<GetRefreshJobQuery, RefreshJobDto>
{
    public async Task<RefreshJobDto> Handle(GetRefreshJobQuery request, CancellationToken cancellationToken) =>
        await repository.GetAsync(request.Id, cancellationToken) ?? throw new ResourceNotFoundException("Refresh job not found.");
}
