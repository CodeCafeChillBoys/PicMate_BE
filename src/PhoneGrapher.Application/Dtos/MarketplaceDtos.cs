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

public sealed record SetOnlineStatusRequest(bool IsOnline);

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
    string? PhoneNumber,
    IReadOnlyList<string> Styles,
    IReadOnlyList<string> Portfolio,
    IReadOnlyList<ServicePackageResponse> Packages,
    IReadOnlyList<ActivityAreaResponse> ActivityAreas,
    IReadOnlyList<GrapherReviewResponse> Reviews);

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
    IReadOnlyList<UpsertServicePackageRequest> ServicePackages,
    IReadOnlyList<ActivityAreaRequest>? ActivityAreas);

public sealed record ActivityAreaRequest(string City, string? District);

public sealed record ActivityAreaResponse(Guid Id, string City, string? District);

public sealed record UpsertServicePackageRequest(
    Guid? Id,
    string Name,
    string Description,
    decimal Price,
    int DurationMinutes);

public sealed record ServiceRequest(
    string Name,
    string Description,
    decimal Price,
    int DurationMinutes);

public sealed record ReviewRequest(int Rating, string Comment);

public sealed record NotificationResponse(
    Guid Id,
    string Type,
    string Title,
    string Message,
    Guid? RelatedBookingId,
    bool IsRead,
    DateTimeOffset CreatedAt);

public sealed record ReviewResponse(
    Guid Id,
    Guid BookingId,
    int Rating,
    string Comment,
    DateTimeOffset CreatedAt);

/// <summary>Review đã chọn lọc để hiển thị ở mục "Khách hàng nói gì?" trên trang chủ.</summary>
/// <param name="Role">Tên gói dịch vụ khách đã đặt, hiển thị dưới tên khách.</param>
public sealed record TestimonialResponse(
    Guid Id,
    int Rating,
    string Text,
    string Name,
    string Role,
    string? Avatar);

/// <summary>Review hiển thị trong tab "Đánh giá" của trang thợ.</summary>
public sealed record GrapherReviewResponse(
    Guid Id,
    int Rating,
    string Text,
    string User,
    DateTimeOffset CreatedAt,
    string? Avatar);

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
    IReadOnlyList<TestimonialResponse> Testimonials,
    IReadOnlyList<object> MembershipPlans,
    IReadOnlyList<object> MockUsers,
    IReadOnlyList<object> MockDisputes,
    IReadOnlyList<object> MockActivities,
    IReadOnlyList<object> MockMessages,
    IReadOnlyList<Guid> FavoritePhotographerIds);

public sealed record MonthlyRevenueItem(int Month, string Label, decimal GrossRevenue, decimal PlatformRevenue, int BookingCount);

// ── Dashboard phân tích ──────────────────────────────────────────────────────

/// <summary>Một chỉ số kèm so sánh với kỳ liền trước và chuỗi số để vẽ sparkline.</summary>
/// <param name="ChangePercent">
/// Null khi kỳ trước bằng 0 — không có mẫu số để chia. Frontend hiển thị "—".
/// </param>
public sealed record MetricResponse(
    decimal Current,
    decimal Previous,
    decimal? ChangePercent,
    IReadOnlyList<decimal> Sparkline);

public sealed record AnalyticsKpisResponse(
    MetricResponse ActiveUsers,
    MetricResponse ShootMinutes,
    MetricResponse Revenue,
    MetricResponse VerifiedGraphers,
    MetricResponse CompletionRate);

/// <summary>Một mốc trên biểu đồ xu hướng.</summary>
public sealed record TimeBucketResponse(string Label, DateTimeOffset StartUtc, decimal Value);

/// <summary>Một lát của biểu đồ tròn.</summary>
public sealed record BreakdownSliceResponse(string Label, decimal Value, decimal Percent);

public sealed record GoalProgressResponse(
    string Label,
    decimal Current,
    decimal Target,
    decimal Percent);

public sealed record HealthItemResponse(string Name, string Status, bool IsHealthy);

public sealed record SystemHealthResponse(
    IReadOnlyList<HealthItemResponse> Items,
    long ApiLatencyMs);

public sealed record AdminAnalyticsResponse(
    string Range,
    DateTimeOffset FromUtc,
    DateTimeOffset ToUtc,
    AnalyticsKpisResponse Kpis,
    IReadOnlyList<TimeBucketResponse> RevenueTrend,
    IReadOnlyList<TimeBucketResponse> ActiveUserTrend,
    IReadOnlyList<BreakdownSliceResponse> UserComposition,
    IReadOnlyList<BreakdownSliceResponse> RevenueByGateway,
    IReadOnlyList<GoalProgressResponse> Goals,
    SystemHealthResponse Health);

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
    string Status,
    string CustomerName,
    /// <summary>Null khi đơn chưa có giao dịch thanh toán nào.</summary>
    Guid? PaymentId,
    string? PaymentProvider,
    string? PaymentStatus,
    /// <summary>
    /// Ngày tạo đơn. Khác với <c>Date</c> vốn là ngày chụp đã hẹn.
    /// Bộ lọc khoảng ngày và biểu đồ số đơn theo ngày đều dựa trên mốc này.
    /// </summary>
    DateTimeOffset CreatedAt);

/// <summary>Yêu cầu admin đánh dấu một đơn đã được hoàn tiền.</summary>
public sealed record RefundBookingRequest(string? Note);

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
    bool MaintenanceMode,
    decimal QuarterlyRevenueTarget,
    int VerifiedGrapherTarget,
    decimal CompletionRateTarget);

public sealed record UpdateSystemSettingsRequest(
    decimal PlatformFeePercent,
    decimal MinWithdrawalAmount,
    bool MomoEnabled,
    bool VnPayEnabled,
    bool ZaloPayEnabled,
    bool EmailNotifyNewBooking,
    bool EmailNotifyDispute,
    bool MaintenanceMode,
    decimal QuarterlyRevenueTarget,
    int VerifiedGrapherTarget,
    decimal CompletionRateTarget);

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
