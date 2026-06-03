namespace PhoneGrapher.Application.Dtos;

public sealed record CreateBookingRequest(
    Guid GrapherProfileId,
    Guid ServicePackageId,
    DateTimeOffset ScheduledAt,
    string Location,
    string? Note);

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
