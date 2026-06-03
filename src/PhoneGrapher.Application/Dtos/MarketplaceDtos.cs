namespace PhoneGrapher.Application.Dtos;

public sealed record GrapherSearchRequest(
    string? Location,
    string? Style,
    decimal? MinPrice,
    decimal? MaxPrice,
    decimal? MinRating,
    bool? Verified);

public sealed record GrapherSummaryResponse(
    Guid Id,
    string Name,
    string? Avatar,
    string Location,
    decimal Rating,
    int ReviewCount,
    bool IsOnline,
    bool IsVerified,
    IReadOnlyList<string> Styles,
    IReadOnlyList<string> Portfolio,
    GrapherPricingResponse Pricing);

public sealed record GrapherPricingResponse(decimal Hourly, decimal Daily);

public sealed record UpsertGrapherProfileRequest(
    string Bio,
    string Location,
    string? District,
    IReadOnlyList<string> Styles,
    IReadOnlyList<string> Portfolio,
    IReadOnlyList<UpsertServicePackageRequest> ServicePackages);

public sealed record UpsertServicePackageRequest(
    Guid? Id,
    string Name,
    string Description,
    decimal Price,
    int DurationMinutes);

public sealed record ReviewRequest(int Rating, string Comment);

public sealed record ReviewResponse(
    Guid Id,
    Guid BookingId,
    int Rating,
    string Comment,
    DateTimeOffset CreatedAt);

public sealed record PresetResponse(
    Guid Id,
    string Name,
    string Category,
    string Image,
    decimal Rating,
    string Downloads,
    decimal Price);

public sealed record BootstrapResponse(
    IReadOnlyList<GrapherSummaryResponse> Photographers,
    IReadOnlyList<object> Services,
    IReadOnlyList<object> Styles,
    IReadOnlyList<PresetResponse> Presets,
    IReadOnlyList<string> BookingStatuses,
    IReadOnlyList<object> Bookings,
    IReadOnlyList<object> DemoAccounts,
    IReadOnlyList<object> Testimonials,
    IReadOnlyList<object> MembershipPlans,
    IReadOnlyList<object> MockUsers,
    IReadOnlyList<object> MockDisputes,
    IReadOnlyList<object> MockActivities,
    IReadOnlyList<object> MockMessages,
    IReadOnlyList<Guid> FavoritePhotographerIds);

public sealed record RevenueSummaryResponse(
    decimal GrossRevenue,
    decimal PlatformRevenue,
    decimal GrapherPayouts,
    int CompletedBookings,
    int PendingKycCount);
