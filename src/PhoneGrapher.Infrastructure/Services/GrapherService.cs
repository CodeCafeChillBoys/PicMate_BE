using Microsoft.EntityFrameworkCore;
using PhoneGrapher.Application.Abstractions;
using PhoneGrapher.Application.Dtos;
using PhoneGrapher.Domain.Entities;
using PhoneGrapher.Domain.Enums;
using PhoneGrapher.Infrastructure.Persistence;

namespace PhoneGrapher.Infrastructure.Services;

public sealed class GrapherService(PhoneGrapherDbContext dbContext) : IGrapherService
{
    public async Task<IReadOnlyList<GrapherSummaryResponse>> SearchAsync(GrapherSearchRequest request, CancellationToken cancellationToken = default)
    {
        var query = IncludeSummary(dbContext.GrapherProfiles.AsNoTracking()).Where(x => x.IsVerified && x.User.IsActive);

        if (!string.IsNullOrWhiteSpace(request.Location))
        {
            var location = request.Location.Trim().ToLowerInvariant();
            query = query.Where(x => x.Location.ToLower().Contains(location) || x.ActivityAreas.Any(a => a.City.ToLower().Contains(location)));
        }

        if (!string.IsNullOrWhiteSpace(request.Style))
        {
            var style = request.Style.Trim().ToLowerInvariant();
            query = query.Where(x => x.StyleTags.Any(t => t.StyleTag.Name.ToLower() == style));
        }

        if (request.MinRating.HasValue)
        {
            query = query.Where(x => x.AverageRating >= request.MinRating.Value);
        }

        if (request.Verified.HasValue)
        {
            query = query.Where(x => x.IsVerified == request.Verified.Value);
        }

        if (request.MinPrice.HasValue)
        {
            query = query.Where(x => x.ServicePackages.Any(p => p.IsActive && p.Price >= request.MinPrice.Value));
        }

        if (request.MaxPrice.HasValue)
        {
            query = query.Where(x => x.ServicePackages.Any(p => p.IsActive && p.Price <= request.MaxPrice.Value));
        }

        var profiles = await query
            .OrderByDescending(x => x.AverageRating)
            .ThenByDescending(x => x.ReviewCount)
            .Take(50)
            .ToArrayAsync(cancellationToken);

        return profiles.Select(x => x.ToSummaryResponse()).ToArray();
    }

    public async Task<GrapherDetailResponse> GetProfileAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var profile = await IncludeSummary(dbContext.GrapherProfiles.AsNoTracking())
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
            
        if (profile is null || !profile.User.IsActive)
        {
            throw new KeyNotFoundException($"Grapher profile with ID {id} not found.");
        }

        return profile.ToDetailResponse();
    }

    public async Task<GrapherSummaryResponse> UpsertProfileAsync(Guid userId, UpsertGrapherProfileRequest request, CancellationToken cancellationToken = default)
    {
        var user = await dbContext.Users
            .Include(x => x.GrapherProfile)
            .ThenInclude(x => x!.PortfolioItems)
            .Include(x => x.GrapherProfile)
            .ThenInclude(x => x!.ServicePackages)
            .Include(x => x.GrapherProfile)
            .ThenInclude(x => x!.StyleTags)
            .FirstOrDefaultAsync(x => x.Id == userId && x.Role == UserRole.Grapher, cancellationToken)
            ?? throw new InvalidOperationException("Grapher user not found.");

        var profile = user.GrapherProfile ?? new GrapherProfile { UserId = userId };
        profile.Bio = request.Bio.Trim();
        profile.Location = request.Location.Trim();
        profile.District = request.District?.Trim();
        profile.UpdatedAt = DateTimeOffset.UtcNow;

        if (user.GrapherProfile is null)
        {
            dbContext.GrapherProfiles.Add(profile);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        dbContext.GrapherPortfolioItems.RemoveRange(profile.PortfolioItems);
        var portfolioItems = request.Portfolio
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select((url, index) => new GrapherPortfolioItem
            {
                GrapherProfileId = profile.Id,
                ImageUrl = url.Trim(),
                DisplayOrder = index + 1
            })
            .ToList();
        dbContext.GrapherPortfolioItems.AddRange(portfolioItems);

        // Merge ServicePackages to avoid FK violations with Bookings
        var existingPackages = profile.ServicePackages.ToList();
        foreach (var existing in existingPackages)
        {
            var updated = request.ServicePackages.FirstOrDefault(x => x.Id == existing.Id);
            if (updated == null)
            {
                // Mark as inactive instead of deleting to prevent FK errors with existing bookings
                existing.IsActive = false;
            }
            else
            {
                existing.Name = updated.Name.Trim();
                existing.Description = updated.Description.Trim();
                existing.Price = updated.Price;
                existing.DurationMinutes = updated.DurationMinutes;
                existing.IsActive = true;
            }
        }

        var newPackages = request.ServicePackages
            .Where(x => x.Id == null || !existingPackages.Any(e => e.Id == x.Id))
            .Select(x => new GrapherServicePackage
            {
                Id = x.Id ?? Guid.NewGuid(),
                GrapherProfileId = profile.Id,
                Name = x.Name.Trim(),
                Description = x.Description.Trim(),
                Price = x.Price,
                DurationMinutes = x.DurationMinutes,
                IsActive = true
            });
        dbContext.GrapherServicePackages.AddRange(newPackages);

        dbContext.GrapherStyleTags.RemoveRange(profile.StyleTags);
        var styleLinks = new List<GrapherStyleTag>();
        foreach (var styleName in request.Styles.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var style = await dbContext.StyleTags.FirstOrDefaultAsync(x => x.Name.ToLower() == styleName.ToLower(), cancellationToken);
            if (style is null)
            {
                style = new StyleTag { Name = styleName };
                dbContext.StyleTags.Add(style);
            }

            styleLinks.Add(new GrapherStyleTag { GrapherProfileId = profile.Id, StyleTag = style });
        }
        dbContext.GrapherStyleTags.AddRange(styleLinks);

        // ── Activity Areas ─────────────────────────────────────────────────
        if (request.ActivityAreas is not null)
        {
            var existingAreas = await dbContext.GrapherActivityAreas
                .Where(a => a.GrapherProfileId == profile.Id)
                .ToListAsync(cancellationToken);
            dbContext.GrapherActivityAreas.RemoveRange(existingAreas);

            var newAreas = request.ActivityAreas
                .Where(a => !string.IsNullOrWhiteSpace(a.City))
                .Select(a => new GrapherActivityArea
                {
                    GrapherProfileId = profile.Id,
                    City = a.City.Trim(),
                    District = a.District?.Trim()
                })
                .ToList();
            dbContext.GrapherActivityAreas.AddRange(newAreas);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        var saved = await IncludeSummary(dbContext.GrapherProfiles.AsNoTracking())
            .FirstAsync(x => x.Id == profile.Id, cancellationToken);
        return saved.ToSummaryResponse();
    }

    public async Task<IReadOnlyList<ServicePackageResponse>> GetMyServicesAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var profile = await dbContext.GrapherProfiles
            .AsNoTracking()
            .Include(x => x.ServicePackages)
            .FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken)
            ?? throw new KeyNotFoundException("Grapher profile not found.");

        return profile.ServicePackages
            .Where(p => p.IsActive)
            .OrderBy(p => p.Price)
            .Select(p => new ServicePackageResponse(p.Id, p.Name, p.Description, p.Price, p.DurationMinutes))
            .ToArray();
    }

    public async Task<ServicePackageResponse> AddServiceAsync(Guid userId, ServiceRequest request, CancellationToken cancellationToken = default)
    {
        var profile = await dbContext.GrapherProfiles.FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken)
            ?? throw new InvalidOperationException("Grapher profile not found.");

        var pkg = new GrapherServicePackage
        {
            GrapherProfileId = profile.Id,
            Name = request.Name.Trim(),
            Description = request.Description?.Trim() ?? string.Empty,
            Price = request.Price,
            DurationMinutes = request.DurationMinutes,
            IsActive = true
        };
        dbContext.GrapherServicePackages.Add(pkg);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new ServicePackageResponse(pkg.Id, pkg.Name, pkg.Description, pkg.Price, pkg.DurationMinutes);
    }

    public async Task<ServicePackageResponse> UpdateServiceAsync(Guid userId, Guid serviceId, ServiceRequest request, CancellationToken cancellationToken = default)
    {
        var pkg = await dbContext.GrapherServicePackages
            .Include(x => x.GrapherProfile)
            .FirstOrDefaultAsync(x => x.Id == serviceId, cancellationToken)
            ?? throw new KeyNotFoundException("Service not found.");

        if (pkg.GrapherProfile.UserId != userId)
        {
            throw new UnauthorizedAccessException("You don't own this service.");
        }

        pkg.Name = request.Name.Trim();
        pkg.Description = request.Description?.Trim() ?? string.Empty;
        pkg.Price = request.Price;
        pkg.DurationMinutes = request.DurationMinutes;
        await dbContext.SaveChangesAsync(cancellationToken);

        return new ServicePackageResponse(pkg.Id, pkg.Name, pkg.Description, pkg.Price, pkg.DurationMinutes);
    }

    public async Task DeleteServiceAsync(Guid userId, Guid serviceId, CancellationToken cancellationToken = default)
    {
        var pkg = await dbContext.GrapherServicePackages
            .Include(x => x.GrapherProfile)
            .FirstOrDefaultAsync(x => x.Id == serviceId, cancellationToken)
            ?? throw new KeyNotFoundException("Service not found.");

        if (pkg.GrapherProfile.UserId != userId)
        {
            throw new UnauthorizedAccessException("You don't own this service.");
        }

        // Soft-delete: ẩn gói đi nhưng giữ bản ghi để không vỡ FK với các booking đã dùng gói này.
        pkg.IsActive = false;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task ApproveKycAsync(Guid grapherProfileId, bool approved, CancellationToken cancellationToken = default)
    {
        var profile = await dbContext.GrapherProfiles.FirstOrDefaultAsync(x => x.Id == grapherProfileId, cancellationToken)
            ?? throw new InvalidOperationException("Grapher profile not found.");

        profile.KycStatus = approved ? KycStatus.Approved : KycStatus.Rejected;
        profile.IsVerified = approved;
        profile.UpdatedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ServicePackageResponse>> SeedDefaultPackagesAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var profile = await dbContext.GrapherProfiles
            .Include(x => x.ServicePackages)
            .FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken)
            ?? throw new InvalidOperationException("Grapher profile not found.");

        if (profile.ServicePackages.Any(p => p.IsActive))
        {
            // Already has packages, just return them
            return profile.ServicePackages
                .Where(p => p.IsActive)
                .Select(p => new ServicePackageResponse(p.Id, p.Name, p.Description, p.Price, p.DurationMinutes))
                .ToArray();
        }

        var defaults = new List<GrapherServicePackage>
        {
            new()
            {
                GrapherProfileId = profile.Id,
                Name = "Chụp ngoại cảnh",
                Description = "Một giờ chụp bằng điện thoại ngoài trời, nhận ảnh trong ngày.",
                Price = 150000m,
                DurationMinutes = 60,
                IsActive = true
            },
            new()
            {
                GrapherProfileId = profile.Id,
                Name = "Chụp Studio",
                Description = "Một giờ chụp bằng điện thoại trong studio, nhận ảnh trong ngày.",
                Price = 200000m,
                DurationMinutes = 60,
                IsActive = true
            },
            new()
            {
                GrapherProfileId = profile.Id,
                Name = "Chụp sự kiện",
                Description = "Hai giờ chụp sự kiện bằng điện thoại, nhận ảnh trong 24h.",
                Price = 350000m,
                DurationMinutes = 120,
                IsActive = true
            }
        };

        dbContext.GrapherServicePackages.AddRange(defaults);
        await dbContext.SaveChangesAsync(cancellationToken);

        return defaults
            .Select(p => new ServicePackageResponse(p.Id, p.Name, p.Description, p.Price, p.DurationMinutes))
            .ToArray();
    }

    public async Task<GrapherDetailResponse> GetMyProfileAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var profile = await IncludeSummary(dbContext.GrapherProfiles.AsNoTracking())
            .FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken)
            ?? throw new KeyNotFoundException("Grapher profile not found.");

        return profile.ToDetailResponse();
    }

    public async Task SetOnlineStatusAsync(Guid userId, bool isOnline, CancellationToken cancellationToken = default)
    {
        var profile = await dbContext.GrapherProfiles
            .FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken)
            ?? throw new InvalidOperationException("Grapher profile not found.");

        profile.IsOnline = isOnline;
        profile.UpdatedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task ToggleFavoriteAsync(Guid userId, Guid grapherProfileId, CancellationToken cancellationToken = default)
    {
        var existing = await dbContext.UserFavoriteGraphers
            .FirstOrDefaultAsync(x => x.UserId == userId && x.GrapherProfileId == grapherProfileId, cancellationToken);

        if (existing is not null)
        {
            dbContext.UserFavoriteGraphers.Remove(existing);
        }
        else
        {
            var exists = await dbContext.GrapherProfiles.AnyAsync(x => x.Id == grapherProfileId, cancellationToken);
            if (!exists)
            {
                throw new KeyNotFoundException($"Grapher profile with ID {grapherProfileId} not found.");
            }

            dbContext.UserFavoriteGraphers.Add(new UserFavoriteGrapher
            {
                UserId = userId,
                GrapherProfileId = grapherProfileId
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Guid>> GetFavoriteGrapherIdsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await dbContext.UserFavoriteGraphers
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .Select(x => x.GrapherProfileId)
            .ToListAsync(cancellationToken);
    }

    private static IQueryable<GrapherProfile> IncludeSummary(IQueryable<GrapherProfile> query)
    {
        return query
            .Include(x => x.User)
            .Include(x => x.PortfolioItems)
            .Include(x => x.ServicePackages)
            .Include(x => x.ActivityAreas)
            .Include(x => x.StyleTags)
            .ThenInclude(x => x.StyleTag);
    }
}
