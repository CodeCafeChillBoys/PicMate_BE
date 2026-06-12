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
    Guid UserId,
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

public sealed record GrapherDetailResponse(
    Guid Id,
    Guid UserId,
    string Name,
    string? Avatar,
    string Location,
    decimal Rating,
    int ReviewCount,
    bool IsOnline,
    bool IsVerified,
    string Bio,
    IReadOnlyList<string> Styles,
    IReadOnlyList<string> Portfolio,
    IReadOnlyList<ServicePackageResponse> Packages);

public sealed record ServicePackageResponse(
    Guid Id,
    string Name,
    string Description,
    decimal Price,
    int DurationMinutes);

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

public sealed record MonthlyRevenueItem(int Month, string Label, decimal GrossRevenue, decimal PlatformRevenue, int BookingCount);

public sealed record RevenueSummaryResponse(
    decimal GrossRevenue,
    decimal PlatformRevenue,
    decimal GrapherPayouts,
    int CompletedBookings,
    int PendingKycCount,
    int TotalUsers,
    int TotalGraphers,
    int BookingsThisMonth,
    decimal RevenueThisMonth,
    IReadOnlyList<MonthlyRevenueItem> MonthlyRevenue);

// ── Admin management DTOs ──────────────────────────────────────────────────

public sealed record AdminUserResponse(
    Guid Id,
    string Name,
    string Email,
    string Role,
    string JoinDate,
    int TotalBookings,
    bool IsActive);

public sealed record AdminPendingGrapherResponse(
    Guid Id,
    string Name,
    string? Avatar,
    IReadOnlyList<string> Styles,
    int PortfolioCount,
    string Location,
    string AppliedDate);

public sealed record AdminBookingResponse(
    Guid Id,
    string PhotographerName,
    string? PhotographerAvatar,
    string Service,
    string Date,
    string Location,
    decimal Total,
    string Status);

public sealed record AdminActivityResponse(
    string Id,
    string Icon,
    string Text,
    string Time);

// ── Admin – Active Graphers ────────────────────────────────────────────────

public sealed record AdminActiveGrapherResponse(
    Guid Id,
    Guid UserId,
    string Name,
    string? Avatar,
    string Location,
    decimal Rating,
    int ReviewCount,
    bool IsOnline,
    bool IsVerified,
    bool IsActive,           // User.IsActive – có thể khóa tài khoản
    string KycStatus,
    int TotalBookings);

// ── Admin – Disputes ───────────────────────────────────────────────────────

public sealed record AdminDisputeResponse(
    Guid Id,
    Guid BookingId,
    string ReporterName,
    string? ReporterAvatar,
    string RespondentName,
    string? RespondentAvatar,
    string Reason,
    string Status,           // 'Pending' | 'Resolved' | 'Closed'
    string Priority,         // 'Medium' | 'High' | 'Urgent'
    string? AdminNote,
    string? Resolution,
    decimal BookingAmount,
    string CreatedAt,
    string? ResolvedAt);

public sealed record CreateDisputeRequest(
    Guid BookingId,
    string Reason,
    string Priority);        // 'Medium' | 'High' | 'Urgent'

public sealed record ResolveDisputeRequest(
    string Action,           // 'refund' | 'warning' | 'resolved'
    string? AdminNote);

// ── Admin – System Settings ────────────────────────────────────────────────

public sealed record SystemSettingsResponse(
    decimal PlatformFeePercent,
    decimal MinWithdrawalAmount,
    bool MomoEnabled,
    bool VnPayEnabled,
    bool ZaloPayEnabled,
    bool EmailNotifyNewBooking,
    bool EmailNotifyDispute,
    bool MaintenanceMode);

public sealed record UpdateSystemSettingsRequest(
    decimal PlatformFeePercent,
    decimal MinWithdrawalAmount,
    bool MomoEnabled,
    bool VnPayEnabled,
    bool ZaloPayEnabled,
    bool EmailNotifyNewBooking,
    bool EmailNotifyDispute,
    bool MaintenanceMode);

// ── Admin – Detail Views ───────────────────────────────────────────────────

public sealed record AdminUserDetailResponse(
    Guid Id,
    string Name,
    string Email,
    string Role,
    string JoinDate,
    int TotalBookings,
    bool IsActive,
    IReadOnlyList<AdminBookingResponse> RecentBookings);

public sealed record AdminGrapherDetailResponse(
    Guid Id,
    Guid UserId,
    string Name,
    string? Avatar,
    string Location,
    decimal Rating,
    int ReviewCount,
    bool IsOnline,
    bool IsVerified,
    bool IsActive,
    string KycStatus,
    int TotalBookings,
    decimal TotalRevenue,
    string Bio,
    IReadOnlyList<string> Styles,
    IReadOnlyList<string> Portfolio,
    IReadOnlyList<ServicePackageResponse> Packages,
    string JoinDate);

public sealed record AdminBookingDetailResponse(
    Guid Id,
    Guid GrapherProfileId,
    string GrapherName,
    Guid CustomerId,
    string CustomerName,
    string ServiceName,
    DateTimeOffset ScheduledAt,
    int DurationMinutes,
    string Location,
    string? Note,
    string Status,
    decimal TotalAmount,
    decimal PlatformFeeAmount,
    decimal GrapherPayoutAmount,
    DateTimeOffset CreatedAt,
    string? CancellationReason,
    PhoneGrapher.Application.Dtos.PaymentTransactionResponse? Payment);
