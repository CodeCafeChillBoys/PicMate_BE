using Microsoft.EntityFrameworkCore;
using PhoneGrapher.Application.Abstractions;
using PhoneGrapher.Application.Dtos;
using PhoneGrapher.Domain.Enums;
using PhoneGrapher.Infrastructure.Persistence;

namespace PhoneGrapher.Infrastructure.Services;

public sealed class BootstrapService(PhoneGrapherDbContext dbContext) : IBootstrapService
{
    public async Task<BootstrapResponse> GetAsync(CancellationToken cancellationToken = default)
    {
        var graphers = await dbContext.GrapherProfiles
            .AsNoTracking()
            .Include(x => x.User)
            .Include(x => x.PortfolioItems)
            .Include(x => x.ServicePackages)
            .Include(x => x.StyleTags)
            .ThenInclude(x => x.StyleTag)
            .Where(x => x.IsVerified && x.User.IsActive)
            .OrderByDescending(x => x.AverageRating)
            .Take(12)
            .ToArrayAsync(cancellationToken);

        var styles = await dbContext.StyleTags
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .Select(x => new { x.Id, x.Name, x.Emoji, x.Color })
            .ToArrayAsync(cancellationToken);

        var presets = await dbContext.Presets
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderByDescending(x => x.DownloadCount)
            .Select(x => new PresetResponse(
                x.Id,
                x.Name,
                x.Category,
                x.ImageUrl,
                x.Rating,
                x.DownloadCount >= 1000 ? $"{decimal.Round(x.DownloadCount / 1000m, 1)}K" : x.DownloadCount.ToString(),
                x.Price))
            .ToArrayAsync(cancellationToken);

        return new BootstrapResponse(
            graphers.Select(x => x.ToSummaryResponse()).ToArray(),
            Array.Empty<object>(),
            styles,
            presets,
            Enum.GetNames<BookingStatus>(),
            Array.Empty<object>(),
            Array.Empty<object>(),
            Array.Empty<object>(),
            Array.Empty<object>(),
            Array.Empty<object>(),
            Array.Empty<object>(),
            Array.Empty<object>(),
            Array.Empty<object>(),
            Array.Empty<Guid>());
    }
}
