namespace PhoneGrapher.Application.Dtos;

public sealed record CreateBookingRequest(
    Guid GrapherProfileId,
    Guid ServicePackageId,
    DateTimeOffset ScheduledAt,
    string Location,
    string? Note);

public sealed record CancelBookingRequest(string Reason);

public sealed record BookingDetailResponse(
    Guid Id,
    string GrapherName,
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
    string? CancellationReason);

public sealed record BookingResponse(
    Guid Id,
    Guid CustomerId,
    Guid GrapherProfileId,
    Guid ServicePackageId,
    DateTimeOffset ScheduledAt,
    string Location,
    string Status,
    decimal TotalAmount,
    decimal PlatformFeeAmount,
    decimal GrapherPayoutAmount,
    PaymentTransactionResponse? Payment);

public sealed record PaymentTransactionResponse(
    Guid Id,
    string Provider,
    string Status,
    string EscrowStatus,
    string TransactionCode,
    string? ProviderTransactionId,
    decimal Amount,
    DateTimeOffset? PaidAt,
    DateTimeOffset? ReleasedAt);

public sealed record CreateBookingPaymentResponse(
    BookingResponse Booking,
    string PaymentUrl);

public sealed record CompleteBookingResponse(
    Guid BookingId,
    string BookingStatus,
    string EscrowStatus,
    decimal PlatformFeeAmount,
    decimal GrapherPayoutAmount);

public sealed record VnPayCallbackResult(
    bool Success,
    Guid? BookingId,
    string Message);

public sealed record GrapherBookingResponse(
    Guid Id,
    Guid CustomerId,
    string CustomerName,
    string? CustomerAvatar,
    string ServiceName,
    DateTimeOffset ScheduledAt,
    int DurationMinutes,
    string Location,
    string? Note,
    string Status,
    decimal TotalAmount,
    decimal GrapherPayoutAmount,
    DateTimeOffset CreatedAt);

public sealed record CustomerBookingResponse(
    Guid Id,
    Guid GrapherUserId,
    string GrapherName,
    string? GrapherAvatar,
    string ServiceName,
    DateTimeOffset ScheduledAt,
    int DurationMinutes,
    string Location,
    string? Note,
    string Status,
    decimal TotalAmount,
    DateTimeOffset CreatedAt);
