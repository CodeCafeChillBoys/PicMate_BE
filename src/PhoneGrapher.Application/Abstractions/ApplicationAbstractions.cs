using PhoneGrapher.Application.Dtos;
using PhoneGrapher.Domain.Entities;

namespace PhoneGrapher.Application.Abstractions;

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default);
    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
    Task<AuthResponse> GoogleAuthAsync(GoogleAuthRequest request, CancellationToken cancellationToken = default);
    Task<CurrentUserResponse> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken = default);
}

public interface IBookingService
{
    Task<CreateBookingPaymentResponse> CreateBookingAsync(Guid customerId, CreateBookingRequest request, string clientIpAddress, CancellationToken cancellationToken = default);
    Task<VnPayCallbackResult> HandleVnPayCallbackAsync(IReadOnlyDictionary<string, string> query, CancellationToken cancellationToken = default);
    Task<CompleteBookingResponse> CompleteBookingAsync(Guid bookingId, Guid actorUserId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BookingResponse>> GetBookingsForUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GrapherBookingResponse>> GetBookingsForGrapherAsync(Guid userId, string? status, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CustomerBookingResponse>> GetBookingsByCustomerIdAsync(Guid customerId, string? status, CancellationToken cancellationToken = default);
    Task<BookingDetailResponse> GetBookingDetailAsync(Guid bookingId, Guid userId, CancellationToken cancellationToken = default);
    Task CancelBookingAsync(Guid bookingId, Guid userId, CancelBookingRequest request, CancellationToken cancellationToken = default);
    Task ConfirmBookingAsync(Guid bookingId, Guid userId, CancellationToken cancellationToken = default);
    Task StartBookingAsync(Guid bookingId, Guid customerUserId, CancellationToken cancellationToken = default);
}

public interface IGrapherService
{
    Task<IReadOnlyList<GrapherSummaryResponse>> SearchAsync(GrapherSearchRequest request, CancellationToken cancellationToken = default);
    Task<GrapherDetailResponse> GetProfileAsync(Guid id, CancellationToken cancellationToken = default);
    Task<GrapherDetailResponse> GetMyProfileAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<GrapherSummaryResponse> UpsertProfileAsync(Guid userId, UpsertGrapherProfileRequest request, CancellationToken cancellationToken = default);
    Task SetOnlineStatusAsync(Guid userId, bool isOnline, CancellationToken cancellationToken = default);
    Task ApproveKycAsync(Guid grapherProfileId, bool approved, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ServicePackageResponse>> SeedDefaultPackagesAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ServicePackageResponse>> GetMyServicesAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<ServicePackageResponse> AddServiceAsync(Guid userId, ServiceRequest request, CancellationToken cancellationToken = default);
    Task<ServicePackageResponse> UpdateServiceAsync(Guid userId, Guid serviceId, ServiceRequest request, CancellationToken cancellationToken = default);
    Task DeleteServiceAsync(Guid userId, Guid serviceId, CancellationToken cancellationToken = default);
}

public interface IReviewService
{
    Task<ReviewResponse> CreateReviewAsync(Guid customerId, Guid bookingId, ReviewRequest request, CancellationToken cancellationToken = default);
}

public interface IBootstrapService
{
    Task<BootstrapResponse> GetAsync(CancellationToken cancellationToken = default);
}

public interface IAdminService
{
    Task<RevenueSummaryResponse> GetRevenueSummaryAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AdminUserResponse>> GetAllUsersAsync(string? search, string? role, CancellationToken cancellationToken = default);
    Task<AdminUserResponse> ToggleUserStatusAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AdminPendingGrapherResponse>> GetPendingGraphersAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AdminActiveGrapherResponse>> GetActiveGraphersAsync(CancellationToken cancellationToken = default);
    Task<AdminActiveGrapherResponse> ToggleGrapherStatusAsync(Guid grapherProfileId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AdminBookingResponse>> GetAllBookingsAsync(string? status, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AdminActivityResponse>> GetRecentActivitiesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AdminDisputeResponse>> GetDisputesAsync(string? status, CancellationToken cancellationToken = default);
    Task<AdminDisputeResponse> ResolveDisputeAsync(Guid disputeId, ResolveDisputeRequest request, CancellationToken cancellationToken = default);
    Task<SystemSettingsResponse> GetSystemSettingsAsync(CancellationToken cancellationToken = default);
    Task<SystemSettingsResponse> UpdateSystemSettingsAsync(UpdateSystemSettingsRequest request, CancellationToken cancellationToken = default);
    Task<AdminUserDetailResponse> GetUserDetailAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<AdminGrapherDetailResponse> GetGrapherDetailAsync(Guid grapherProfileId, CancellationToken cancellationToken = default);
    Task<AdminBookingDetailResponse> GetBookingDetailAsync(Guid bookingId, CancellationToken cancellationToken = default);
}

public interface IJwtTokenService
{
    string CreateAccessToken(User user);
    string CreateRefreshToken();
}

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string passwordHash);
}

public interface IVnPayService
{
    string CreatePaymentUrl(PaymentTransaction payment, string clientIpAddress);
    bool VerifyCallback(IReadOnlyDictionary<string, string> query);
}

public interface IEmailService
{
    Task SendAsync(string toEmail, string toName, string subject, string htmlBody, CancellationToken cancellationToken = default);
}

public interface IDisputeService
{
    Task<Guid> CreateDisputeAsync(Guid reporterId, CreateDisputeRequest request, CancellationToken cancellationToken = default);
}

public interface INotificationService
{
    Task CreateAsync(Guid userId, string type, string title, string message, Guid? relatedBookingId = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<NotificationResponse>> GetForUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<int> GetUnreadCountAsync(Guid userId, CancellationToken cancellationToken = default);
    Task MarkReadAsync(Guid userId, Guid notificationId, CancellationToken cancellationToken = default);
    Task MarkAllReadAsync(Guid userId, CancellationToken cancellationToken = default);
}

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
