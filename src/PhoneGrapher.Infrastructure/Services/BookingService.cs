using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PhoneGrapher.Application.Abstractions;
using PhoneGrapher.Application.Dtos;
using PhoneGrapher.Domain.Entities;
using PhoneGrapher.Domain.Enums;
using PhoneGrapher.Infrastructure.Persistence;

namespace PhoneGrapher.Infrastructure.Services;

public sealed class BookingService(
    PhoneGrapherDbContext dbContext,
    IVnPayService vnPayService) : IBookingService
{
    private const decimal PlatformFeeRate = 0.15m;

    public async Task<CreateBookingPaymentResponse> CreateBookingAsync(
        Guid customerId,
        CreateBookingRequest request,
        string clientIpAddress,
        CancellationToken cancellationToken = default)
    {
        var customer = await dbContext.Users.FirstOrDefaultAsync(x => x.Id == customerId && x.Role == UserRole.Customer, cancellationToken)
            ?? throw new InvalidOperationException("Only customers can create bookings.");

        var package = await dbContext.GrapherServicePackages
            .Include(x => x.GrapherProfile)
            .FirstOrDefaultAsync(x =>
                x.Id == request.ServicePackageId
                && x.GrapherProfileId == request.GrapherProfileId
                && x.IsActive
                && x.GrapherProfile.IsVerified,
                cancellationToken)
            ?? throw new InvalidOperationException("Service package is unavailable.");

        var hasTimeConflict = await dbContext.Bookings.AnyAsync(x =>
            x.GrapherProfileId == request.GrapherProfileId
            && x.ScheduledAt == request.ScheduledAt
            && x.Status != BookingStatus.Cancelled,
            cancellationToken);

        if (hasTimeConflict)
        {
            throw new InvalidOperationException("This grapher already has a booking at the requested time.");
        }

        var platformFee = decimal.Round(package.Price * PlatformFeeRate, 2);
        var payout = package.Price - platformFee;

        await using var tx = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var booking = new Booking
        {
            CustomerId = customer.Id,
            GrapherProfileId = package.GrapherProfileId,
            ServicePackageId = package.Id,
            ScheduledAt = request.ScheduledAt,
            DurationMinutes = package.DurationMinutes,
            Location = request.Location.Trim(),
            Note = request.Note?.Trim(),
            Status = BookingStatus.PendingPayment,
            TotalAmount = package.Price,
            PlatformFeeAmount = platformFee,
            GrapherPayoutAmount = payout
        };

        var payment = new PaymentTransaction
        {
            Booking = booking,
            Amount = booking.TotalAmount,
            PlatformFeeAmount = platformFee,
            GrapherPayoutAmount = payout,
            TransactionCode = CreateTransactionCode()
        };

        dbContext.Bookings.Add(booking);
        dbContext.PaymentTransactions.Add(payment);
        await dbContext.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

        var paymentUrl = vnPayService.CreatePaymentUrl(payment, clientIpAddress);
        return new CreateBookingPaymentResponse(booking.ToResponse(), paymentUrl);
    }

    public async Task<VnPayCallbackResult> HandleVnPayCallbackAsync(
        IReadOnlyDictionary<string, string> query,
        CancellationToken cancellationToken = default)
    {
        if (!vnPayService.VerifyCallback(query))
        {
            return new VnPayCallbackResult(false, null, "Invalid VNPAY signature.");
        }

        if (!query.TryGetValue("vnp_TxnRef", out var transactionCode))
        {
            return new VnPayCallbackResult(false, null, "Missing VNPAY transaction reference.");
        }

        var payment = await dbContext.PaymentTransactions
            .Include(x => x.Booking)
            .FirstOrDefaultAsync(x => x.TransactionCode == transactionCode, cancellationToken);

        if (payment is null)
        {
            return new VnPayCallbackResult(false, null, "Payment transaction not found.");
        }

        var success = query.TryGetValue("vnp_ResponseCode", out var responseCode)
            && responseCode == "00"
            && (!query.TryGetValue("vnp_TransactionStatus", out var transactionStatus) || transactionStatus == "00");

        await using var tx = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        payment.ProviderTransactionId = query.GetValueOrDefault("vnp_TransactionNo");
        payment.ProviderResponseCode = responseCode;
        payment.RawCallbackPayload = JsonSerializer.Serialize(query);

        if (success)
        {
            payment.Status = PaymentStatus.Succeeded;
            payment.EscrowStatus = EscrowStatus.Held;
            payment.PaidAt = DateTimeOffset.UtcNow;
            payment.Booking.Status = BookingStatus.PendingConfirmation;
        }
        else
        {
            payment.Status = PaymentStatus.Failed;
        }

        payment.UpdatedAt = DateTimeOffset.UtcNow;
        payment.Booking.UpdatedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

        return new VnPayCallbackResult(success, payment.BookingId, success ? "Payment captured and escrow held." : "Payment failed.");
    }

    public async Task<CompleteBookingResponse> CompleteBookingAsync(Guid bookingId, Guid actorUserId, CancellationToken cancellationToken = default)
    {
        var booking = await dbContext.Bookings
            .Include(x => x.GrapherProfile)
            .Include(x => x.PaymentTransaction)
            .FirstOrDefaultAsync(x => x.Id == bookingId, cancellationToken)
            ?? throw new InvalidOperationException("Booking not found.");

        if (booking.GrapherProfile.UserId != actorUserId)
        {
            throw new UnauthorizedAccessException("Only the assigned grapher can complete this booking.");
        }

        if (booking.Status is not (BookingStatus.Confirmed or BookingStatus.InProgress or BookingStatus.PendingConfirmation))
        {
            throw new InvalidOperationException("Booking cannot be completed from the current status.");
        }

        var payment = booking.PaymentTransaction
            ?? throw new InvalidOperationException("Booking payment transaction is missing.");

        if (payment.Status != PaymentStatus.Succeeded || payment.EscrowStatus != EscrowStatus.Held)
        {
            throw new InvalidOperationException("Escrow is not ready to release.");
        }

        await using var tx = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        booking.Status = BookingStatus.Completed;
        booking.CompletedAt = DateTimeOffset.UtcNow;
        booking.UpdatedAt = DateTimeOffset.UtcNow;
        payment.EscrowStatus = EscrowStatus.Released;
        payment.ReleasedAt = DateTimeOffset.UtcNow;
        payment.UpdatedAt = DateTimeOffset.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

        return new CompleteBookingResponse(
            booking.Id,
            booking.Status.ToString(),
            payment.EscrowStatus.ToString(),
            payment.PlatformFeeAmount,
            payment.GrapherPayoutAmount);
    }

    public async Task<IReadOnlyList<BookingResponse>> GetBookingsForUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await dbContext.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == userId, cancellationToken)
            ?? throw new InvalidOperationException("User not found.");

        var query = dbContext.Bookings
            .AsNoTracking()
            .Include(x => x.PaymentTransaction)
            .Include(x => x.GrapherProfile)
            .AsQueryable();

        query = user.Role switch
        {
            UserRole.Grapher => query.Where(x => x.GrapherProfile.UserId == userId),
            UserRole.Admin => query,
            _ => query.Where(x => x.CustomerId == userId)
        };

        return await query
            .OrderByDescending(x => x.ScheduledAt)
            .Select(x => x.ToResponse())
            .ToArrayAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<GrapherBookingResponse>> GetBookingsForGrapherAsync(
        Guid userId,
        string? status,
        CancellationToken cancellationToken = default)
    {
        var profile = await dbContext.GrapherProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken)
            ?? throw new InvalidOperationException("Grapher profile not found.");

        var query = dbContext.Bookings
            .AsNoTracking()
            .Include(x => x.Customer)
            .Include(x => x.ServicePackage)
            .Where(x => x.GrapherProfileId == profile.Id);

        if (!string.IsNullOrWhiteSpace(status)
            && Enum.TryParse<BookingStatus>(status, ignoreCase: true, out var parsed))
        {
            query = query.Where(x => x.Status == parsed);
        }

        return await query
            .OrderByDescending(x => x.ScheduledAt)
            .Select(x => new GrapherBookingResponse(
                x.Id,
                x.Customer.FullName,
                x.Customer.AvatarUrl,
                x.ServicePackage.Name,
                x.ScheduledAt,
                x.DurationMinutes,
                x.Location,
                x.Note,
                x.Status.ToString(),
                x.TotalAmount,
                x.GrapherPayoutAmount,
                x.CreatedAt))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CustomerBookingResponse>> GetBookingsByCustomerIdAsync(
        Guid customerId,
        string? status,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.Bookings
            .AsNoTracking()
            .Include(x => x.GrapherProfile)
                .ThenInclude(p => p.User)
            .Include(x => x.ServicePackage)
            .Where(x => x.CustomerId == customerId);

        if (!string.IsNullOrWhiteSpace(status)
            && Enum.TryParse<BookingStatus>(status, ignoreCase: true, out var parsed))
        {
            query = query.Where(x => x.Status == parsed);
        }

        return await query
            .OrderByDescending(x => x.ScheduledAt)
            .Select(x => new CustomerBookingResponse(
                x.Id,
                x.GrapherProfile.User.FullName,
                x.GrapherProfile.User.AvatarUrl,
                x.ServicePackage.Name,
                x.ScheduledAt,
                x.DurationMinutes,
                x.Location,
                x.Note,
                x.Status.ToString(),
                x.TotalAmount,
                x.CreatedAt))
            .ToArrayAsync(cancellationToken);
    }

    private static string CreateTransactionCode()
    {
        return $"PG{DateTimeOffset.UtcNow:yyyyMMddHHmmss}{Random.Shared.Next(100000, 999999)}";
    }

    public async Task<BookingDetailResponse> GetBookingDetailAsync(Guid bookingId, Guid userId, CancellationToken cancellationToken = default)
    {
        var booking = await dbContext.Bookings
            .AsNoTracking()
            .Include(x => x.Customer)
            .Include(x => x.GrapherProfile)
                .ThenInclude(p => p.User)
            .Include(x => x.ServicePackage)
            .FirstOrDefaultAsync(x => x.Id == bookingId, cancellationToken)
            ?? throw new KeyNotFoundException("Booking not found.");

        if (booking.CustomerId != userId && booking.GrapherProfile.UserId != userId)
        {
            var user = await dbContext.Users.FindAsync(userId);
            if (user?.Role != UserRole.Admin)
            {
                throw new UnauthorizedAccessException("You don't have permission to view this booking.");
            }
        }

        return new BookingDetailResponse(
            booking.Id,
            booking.GrapherProfile.User.FullName,
            booking.Customer.FullName,
            booking.ServicePackage.Name,
            booking.ScheduledAt,
            booking.DurationMinutes,
            booking.Location,
            booking.Note,
            booking.Status.ToString(),
            booking.TotalAmount,
            booking.PlatformFeeAmount,
            booking.GrapherPayoutAmount,
            booking.CreatedAt,
            booking.CancellationReason);
    }

    public async Task CancelBookingAsync(Guid bookingId, Guid userId, CancelBookingRequest request, CancellationToken cancellationToken = default)
    {
        var booking = await dbContext.Bookings
            .Include(x => x.GrapherProfile)
            .Include(x => x.PaymentTransaction)
            .FirstOrDefaultAsync(x => x.Id == bookingId, cancellationToken)
            ?? throw new KeyNotFoundException("Booking not found.");

        if (booking.CustomerId != userId && booking.GrapherProfile.UserId != userId)
        {
            var user = await dbContext.Users.FindAsync(userId);
            if (user?.Role != UserRole.Admin)
            {
                throw new UnauthorizedAccessException("You don't have permission to cancel this booking.");
            }
        }

        if (booking.Status is BookingStatus.Completed or BookingStatus.Cancelled)
        {
            throw new InvalidOperationException("Booking is already completed or cancelled.");
        }

        await using var tx = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        booking.Status = BookingStatus.Cancelled;
        booking.CancellationReason = request.Reason?.Trim();
        booking.UpdatedAt = DateTimeOffset.UtcNow;

        var payment = booking.PaymentTransaction;
        if (payment != null && payment.Status == PaymentStatus.Succeeded && payment.EscrowStatus == EscrowStatus.Held)
        {
            // Logic for refund would go here. For now, mark escrow as returned.
            payment.EscrowStatus = EscrowStatus.Refunded;
            payment.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
    }
}
