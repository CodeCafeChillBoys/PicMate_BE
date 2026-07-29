using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PhoneGrapher.Application.Abstractions;
using PhoneGrapher.Application.Dtos;
using PhoneGrapher.Domain.Entities;
using PhoneGrapher.Domain.Enums;
using PhoneGrapher.Infrastructure.Options;
using PhoneGrapher.Infrastructure.Persistence;

namespace PhoneGrapher.Infrastructure.Services;

public sealed class PaymentReconciliationService(
    PhoneGrapherDbContext dbContext,
    IVietQrService vietQrService,
    IOptions<VietQrOptions> vietQrOptions,
    PaymentSucceededNotifier notifier,
    ILogger<PaymentReconciliationService> logger) : IPaymentReconciliationService
{
    private readonly VietQrOptions _options = vietQrOptions.Value;

    public async Task<VietQrPaymentResponse> GetVietQrAsync(
        Guid bookingId,
        Guid customerId,
        CancellationToken cancellationToken = default)
    {
        var payment = await LoadOwnedVietQrPaymentAsync(bookingId, customerId, cancellationToken);

        // Đơn đã huỷ/thất bại vẫn trả 200 kèm trạng thái, để FE dựng được màn hình thất bại.
        var qrPayload = payment.Status is PaymentStatus.Pending or PaymentStatus.AwaitingVerification
            ? vietQrService.BuildPayload(payment.Amount, payment.TransactionCode)
            : string.Empty;

        return new VietQrPaymentResponse(
            payment.BookingId,
            qrPayload,
            _options.BankBin,
            _options.BankName,
            _options.AccountNumber,
            _options.AccountName,
            payment.Amount,
            payment.TransactionCode,
            payment.ExpiresAt,
            payment.Status.ToString(),
            payment.Booking.Status.ToString(),
            payment.VerificationNote);
    }

    public async Task<PaymentStatusResponse> ClaimTransferredAsync(
        Guid bookingId,
        Guid customerId,
        CancellationToken cancellationToken = default)
    {
        var payment = await LoadOwnedVietQrPaymentAsync(bookingId, customerId, cancellationToken);

        if (payment.Status == PaymentStatus.AwaitingVerification)
        {
            return ToStatusResponse(payment);
        }

        if (payment.Status != PaymentStatus.Pending)
        {
            throw new InvalidOperationException("Đơn này không còn ở trạng thái chờ chuyển khoản.");
        }

        payment.Status = PaymentStatus.AwaitingVerification;
        payment.CustomerClaimedAt = DateTimeOffset.UtcNow;
        payment.UpdatedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Khách báo đã chuyển khoản cho giao dịch {TransactionCode} (booking {BookingId}).",
            payment.TransactionCode, payment.BookingId);

        return ToStatusResponse(payment);
    }

    public async Task<PaymentStatusResponse> GetStatusAsync(
        Guid bookingId,
        Guid customerId,
        CancellationToken cancellationToken = default)
    {
        var payment = await LoadOwnedVietQrPaymentAsync(bookingId, customerId, cancellationToken);
        return ToStatusResponse(payment);
    }

    public async Task<IReadOnlyList<PendingPaymentResponse>> GetPendingAsync(
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;

        // Lọc ở DB, còn sắp xếp và ánh xạ làm trong bộ nhớ: ToPendingResponse là phương thức
        // C# thường, EF không dịch được sang SQL. Hàng đợi đối soát vốn nhỏ nên không tốn kém.
        var rows = await BaseQuery()
            .Where(x => x.Provider == PaymentProvider.VietQr
                && (x.Status == PaymentStatus.AwaitingVerification
                    || (x.Status == PaymentStatus.Pending && x.ExpiresAt != null && x.ExpiresAt > now)))
            .ToArrayAsync(cancellationToken);

        return rows
            // Đơn khách đã báo chuyển lên trước, trong đó ai báo sớm hơn thì đứng trên.
            .OrderBy(x => x.Status == PaymentStatus.AwaitingVerification ? 0 : 1)
            .ThenBy(x => x.CustomerClaimedAt ?? x.CreatedAt)
            .Select(ToPendingResponse)
            .ToArray();
    }

    public async Task<IReadOnlyList<PendingPaymentResponse>> GetRecentlyExpiredAsync(
        CancellationToken cancellationToken = default)
    {
        var since = DateTimeOffset.UtcNow.AddHours(-24);

        var rows = await BaseQuery()
            .Where(x => x.Provider == PaymentProvider.VietQr
                && x.Status == PaymentStatus.Failed
                && x.CustomerClaimedAt == null
                && x.Booking.Status == BookingStatus.Cancelled
                && x.UpdatedAt != null
                && x.UpdatedAt >= since)
            .ToArrayAsync(cancellationToken);

        return rows
            .OrderByDescending(x => x.UpdatedAt)
            .Select(ToPendingResponse)
            .ToArray();
    }

    public async Task<PaymentStatusResponse> VerifyAsync(
        Guid paymentId,
        Guid adminUserId,
        VerifyPaymentRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!request.Approved && string.IsNullOrWhiteSpace(request.Note))
        {
            throw new InvalidOperationException("Phải nhập lý do khi từ chối giao dịch.");
        }

        var payment = await dbContext.PaymentTransactions
            .Include(x => x.Booking).ThenInclude(b => b.Customer)
            .Include(x => x.Booking).ThenInclude(b => b.GrapherProfile).ThenInclude(gp => gp.User)
            .Include(x => x.Booking).ThenInclude(b => b.ServicePackage)
            .FirstOrDefaultAsync(x => x.Id == paymentId, cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy giao dịch.");

        if (payment.Provider != PaymentProvider.VietQr)
        {
            throw new InvalidOperationException("Chỉ đối soát được giao dịch VietQR.");
        }

        // Duyệt hai lần không được cộng tiền hay gửi mail lần nữa.
        if (payment.Status == PaymentStatus.Succeeded)
        {
            return ToStatusResponse(payment);
        }

        payment.VerifiedByUserId = adminUserId;
        payment.VerifiedAt = DateTimeOffset.UtcNow;
        payment.VerificationNote = request.Note?.Trim();
        payment.UpdatedAt = DateTimeOffset.UtcNow;

        if (request.Approved)
        {
            payment.Status = PaymentStatus.Succeeded;
            payment.EscrowStatus = EscrowStatus.Held;
            payment.PaidAt = DateTimeOffset.UtcNow;
            payment.Booking.Status = BookingStatus.PendingConfirmation;
            payment.Booking.UpdatedAt = DateTimeOffset.UtcNow;
        }
        else
        {
            payment.Status = PaymentStatus.Failed;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Admin {AdminId} {Action} giao dịch {TransactionCode}.",
            adminUserId, request.Approved ? "duyệt" : "từ chối", payment.TransactionCode);

        if (request.Approved)
        {
            await notifier.NotifyAsync(payment.Booking, cancellationToken);
        }

        return ToStatusResponse(payment);
    }

    private async Task<PaymentTransaction> LoadOwnedVietQrPaymentAsync(
        Guid bookingId,
        Guid customerId,
        CancellationToken cancellationToken)
    {
        var payment = await dbContext.PaymentTransactions
            .Include(x => x.Booking)
            .FirstOrDefaultAsync(x => x.BookingId == bookingId, cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy đơn đặt lịch.");

        if (payment.Booking.CustomerId != customerId)
        {
            throw new UnauthorizedAccessException("Bạn không có quyền xem đơn này.");
        }

        if (payment.Provider != PaymentProvider.VietQr)
        {
            throw new InvalidOperationException("Đơn này không thanh toán bằng VietQR.");
        }

        return payment;
    }

    private IQueryable<PaymentTransaction> BaseQuery() => dbContext.PaymentTransactions
        .AsNoTracking()
        .Include(x => x.Booking).ThenInclude(b => b.Customer)
        .Include(x => x.Booking).ThenInclude(b => b.GrapherProfile).ThenInclude(gp => gp.User)
        .Include(x => x.Booking).ThenInclude(b => b.ServicePackage);

    private static PaymentStatusResponse ToStatusResponse(PaymentTransaction payment) => new(
        payment.BookingId,
        payment.Status.ToString(),
        payment.Booking.Status.ToString(),
        payment.VerificationNote);

    private static PendingPaymentResponse ToPendingResponse(PaymentTransaction payment) => new(
        payment.Id,
        payment.BookingId,
        payment.TransactionCode,
        payment.Amount,
        payment.Booking.Customer.FullName,
        payment.Booking.Customer.Email,
        payment.Booking.GrapherProfile.User.FullName,
        payment.Booking.ServicePackage.Name,
        payment.CreatedAt,
        payment.CustomerClaimedAt,
        payment.ExpiresAt,
        payment.Status.ToString(),
        payment.Booking.Status.ToString());
}
