using Microsoft.EntityFrameworkCore;
using PhoneGrapher.Application.Abstractions;
using PhoneGrapher.Application.Dtos;
using PhoneGrapher.Domain.Enums;
using PhoneGrapher.Infrastructure.Persistence;

namespace PhoneGrapher.Infrastructure.Services;

public sealed class AdminService(PhoneGrapherDbContext dbContext) : IAdminService
{
    public async Task<RevenueSummaryResponse> GetRevenueSummaryAsync(CancellationToken cancellationToken = default)
    {
        var completedPayments = await dbContext.PaymentTransactions
            .AsNoTracking()
            .Where(x => x.Status == PaymentStatus.Succeeded && x.EscrowStatus == EscrowStatus.Released)
            .ToArrayAsync(cancellationToken);

        var completedBookings = await dbContext.Bookings
            .AsNoTracking()
            .CountAsync(x => x.Status == BookingStatus.Completed, cancellationToken);

        var pendingKyc = await dbContext.GrapherProfiles
            .AsNoTracking()
            .CountAsync(x => x.KycStatus == KycStatus.Pending, cancellationToken);

        return new RevenueSummaryResponse(
            completedPayments.Sum(x => x.Amount),
            completedPayments.Sum(x => x.PlatformFeeAmount),
            completedPayments.Sum(x => x.GrapherPayoutAmount),
            completedBookings,
            pendingKyc);
    }
}
