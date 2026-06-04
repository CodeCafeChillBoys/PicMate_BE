using PhoneGrapher.Application.Dtos;
using PhoneGrapher.Domain.Entities;

namespace PhoneGrapher.Application.Abstractions;

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default);
    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
    Task<CurrentUserResponse> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken = default);
}

public interface IBookingService
{
    Task<CreateBookingPaymentResponse> CreateBookingAsync(Guid customerId, CreateBookingRequest request, string clientIpAddress, CancellationToken cancellationToken = default);
    Task<VnPayCallbackResult> HandleVnPayCallbackAsync(IReadOnlyDictionary<string, string> query, CancellationToken cancellationToken = default);
    Task<CompleteBookingResponse> CompleteBookingAsync(Guid bookingId, Guid actorUserId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BookingResponse>> GetBookingsForUserAsync(Guid userId, CancellationToken cancellationToken = default);
}

public interface IGrapherService
{
    Task<IReadOnlyList<GrapherSummaryResponse>> SearchAsync(GrapherSearchRequest request, CancellationToken cancellationToken = default);
    Task<GrapherSummaryResponse> UpsertProfileAsync(Guid userId, UpsertGrapherProfileRequest request, CancellationToken cancellationToken = default);
    Task ApproveKycAsync(Guid grapherProfileId, bool approved, CancellationToken cancellationToken = default);
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
    Task<IReadOnlyList<AdminBookingResponse>> GetAllBookingsAsync(string? status, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AdminActivityResponse>> GetRecentActivitiesAsync(CancellationToken cancellationToken = default);
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

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
