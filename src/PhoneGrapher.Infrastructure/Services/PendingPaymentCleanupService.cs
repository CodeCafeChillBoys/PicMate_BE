using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PhoneGrapher.Domain.Enums;
using PhoneGrapher.Infrastructure.Persistence;

namespace PhoneGrapher.Infrastructure.Services;

/// <summary>
/// Huỷ các đơn VietQR quá hạn mà khách chưa bấm "Tôi đã chuyển khoản".
/// Đơn đã bấm nút thì tiền thật có thể đã vào tài khoản, tuyệt đối không tự huỷ.
/// </summary>
public sealed class PendingPaymentCleanupService(
    IServiceScopeFactory scopeFactory,
    ILogger<PendingPaymentCleanupService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(60);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<PhoneGrapherDbContext>();

                var cancelled = await CancelExpiredAsync(dbContext, stoppingToken);
                if (cancelled > 0)
                {
                    logger.LogInformation("Đã tự huỷ {Count} đơn VietQR quá hạn thanh toán.", cancelled);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Job hỏng không được làm sập API.
                logger.LogError(ex, "Lỗi khi dọn đơn VietQR quá hạn.");
            }
        }
    }

    internal static async Task<int> CancelExpiredAsync(PhoneGrapherDbContext dbContext, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;

        var expired = await dbContext.PaymentTransactions
            .Include(x => x.Booking)
            .Where(x => x.Provider == PaymentProvider.VietQr
                && x.Status == PaymentStatus.Pending
                && x.CustomerClaimedAt == null
                && x.ExpiresAt != null
                && x.ExpiresAt < now)
            .ToArrayAsync(cancellationToken);

        if (expired.Length == 0)
        {
            return 0;
        }

        foreach (var payment in expired)
        {
            payment.Status = PaymentStatus.Failed;
            payment.VerificationNote = "Tự huỷ do quá hạn thanh toán.";
            payment.UpdatedAt = now;
            payment.Booking.Status = BookingStatus.Cancelled;
            payment.Booking.CancellationReason = "Quá hạn thanh toán";
            payment.Booking.UpdatedAt = now;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return expired.Length;
    }
}
